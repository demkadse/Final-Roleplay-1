using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using FRP1.Core.Identity;
using FRP1.Core.Persistence;
using FRP1.Core.Profiles;

namespace FRP1.Core.Networking;

public sealed record PeerNodeOptions
{
    public bool NetworkEnabled { get; init; }
    public bool DirectIpDisclosureAccepted { get; init; }
    public IPAddress BindAddress { get; init; } = IPAddress.Loopback;
    public int ListenPort { get; init; }
    public Func<bool>? AccessAllowed { get; init; }
}

public sealed class PeerNode : IAsyncDisposable
{
    private const int MaximumFrameBytes = 64 * 1024;
    private readonly PeerIdentity identity;
    private readonly ProfileStore store;
    private readonly PeerNodeOptions options;
    private readonly ConcurrentDictionary<string, bool> peers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, Task> connections = new();
    private readonly ConcurrentDictionary<int, TcpClient> sockets = new();
    private readonly ConcurrentDictionary<int, string> socketPeers = new();
    private readonly object authenticationState = new();
    private readonly Dictionary<int, string> authenticatedPeers = [];
    private readonly Dictionary<string, long> disconnectVersions = new(StringComparer.OrdinalIgnoreCase);
    private long disconnectVersion;
    private readonly ConcurrentDictionary<int, TaskCompletionSource> handshakes = new();
    private readonly ConcurrentQueue<ReceivedProfileNotification> received = new();
    private int receivedCount;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim slots = new(4, 4);
    private TcpListener? listener;
    private Task? acceptTask;
    private ProfileDocument? localProfile;
    private int connectionId;
    private int acceptsMatureContent;
    private bool disposed;
    private volatile bool suspended;

    public PeerNode(PeerIdentity identity, ProfileStore store, PeerNodeOptions? options = null)
    {
        this.identity = identity;
        this.store = store;
        this.options = options ?? new PeerNodeOptions();
        acceptsMatureContent = store.Policy.AcceptsMatureContent ? 1 : 0;
        store.Policy.Changed += OnAudienceChanged;
    }

    public string? LastConnectionError { get; private set; }

    public IReadOnlyList<string> ConnectedPeerFingerprints
    {
        get
        {
            if (!HasAccess) return Array.Empty<string>();
            lock (authenticationState)
                return Array.AsReadOnly(authenticatedPeers.Values.Where(peers.ContainsKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Select(pin => pin.ToUpperInvariant()).ToArray());
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (options.ListenPort is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(options), "Invalid local listen port.");
        if (!options.NetworkEnabled) throw new InvalidOperationException("Networking requires explicit consent.");
        EnsureAccess();
        if (!IPAddress.IsLoopback(options.BindAddress) && !options.DirectIpDisclosureAccepted)
            throw new InvalidOperationException("Direct IP disclosure requires explicit consent.");
        if (listener is not null) throw new InvalidOperationException("Peer is already listening.");
        if (options.BindAddress.Equals(IPAddress.Any) || options.BindAddress.Equals(IPAddress.IPv6Any))
            throw new InvalidOperationException("Choose one explicit local interface.");
        lock (authenticationState)
        {
            if (suspended || disposed) throw new AuthenticationException("Peer access is unavailable.");
            listener = new TcpListener(options.BindAddress, options.ListenPort);
            listener.Start(4);
            var acceptCancellation = lifetime.Token;
            acceptTask = Task.Run(() => AcceptLoopAsync(acceptCancellation));
        }
        return Task.CompletedTask;
    }

    public PeerInvitation CreateInvitation()
    {
        if (listener?.LocalEndpoint is not IPEndPoint endpoint) throw new InvalidOperationException("Start the peer first.");
        return new PeerInvitation(1, endpoint.Address.ToString(), endpoint.Port, identity.Fingerprint);
    }

    public void Pair(PeerInvitation invitation, bool shareProfile = false)
    {
        invitation.Validate();
        if (string.Equals(invitation.Fingerprint, identity.Fingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot pair with yourself.");
        if (!peers.ContainsKey(invitation.Fingerprint) && peers.Count >= 16)
            throw new InvalidOperationException("Phase 1 supports at most 16 paired peers.");
        peers[invitation.Fingerprint] = shareProfile;
    }

    public void SetSharing(string fingerprint, bool enabled)
    {
        if (!peers.ContainsKey(fingerprint)) throw new InvalidOperationException("Peer must be paired first.");
        peers[fingerprint] = enabled;
    }

    public void Unpair(string fingerprint)
    {
        peers.TryRemove(fingerprint, out _);
        DisconnectPeer(fingerprint);
    }

    public void DisconnectPeer(string fingerprint)
    {
        if (!PeerInvitation.IsFingerprint(fingerprint)) throw new ArgumentException("Invalid peer identity.");
        lock (authenticationState)
        {
            disconnectVersions[fingerprint] = checked(++disconnectVersion);
            foreach (var id in authenticatedPeers.Where(pair => string.Equals(pair.Value, fingerprint, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToArray())
                authenticatedPeers.Remove(id);
        }
        foreach (var pair in socketPeers)
            if (string.Equals(pair.Value, fingerprint, StringComparison.OrdinalIgnoreCase) && sockets.TryGetValue(pair.Key, out var socket)) socket.Dispose();
    }

    public void SetLocalProfile(ProfileDocument? profile)
    {
        if (profile is null) { Volatile.Write(ref localProfile, null); return; }
        EnsureAccess();
        var snapshot = ProfileJson.Import(ProfileJson.Export(profile));
        store.Policy.EnsureAllowed(snapshot);
        var previous = Volatile.Read(ref localProfile);
        if (previous is not null && previous.Id == snapshot.Id && (snapshot.Revision < previous.Revision ||
            (snapshot.Revision == previous.Revision && ProfileJson.Hash(snapshot) != ProfileJson.Hash(previous))))
            throw new InvalidDataException("A changed profile requires a higher revision.");
        Volatile.Write(ref localProfile, snapshot);
    }

    public bool TryDequeueReceived(out ReceivedProfileNotification? notification)
    {
        while (received.TryDequeue(out var next))
        {
            Interlocked.Decrement(ref receivedCount);
            if (HasAccess && peers.ContainsKey(next.Fingerprint)) { notification = next; return true; }
        }
        notification = null;
        return false;
    }

    public void Suspend()
    {
        lock (authenticationState)
        {
            suspended = true;
            authenticatedPeers.Clear();
            Volatile.Write(ref localProfile, null);
            listener?.Stop();
        }
        foreach (var socket in sockets.Values) socket.Dispose();
        while (received.TryDequeue(out _)) Interlocked.Decrement(ref receivedCount);
    }

    public async Task<SyncSession> ConnectAsync(PeerInvitation invitation, CancellationToken cancellationToken = default)
    {
        var startedBeforeDisconnect = Volatile.Read(ref disconnectVersion);
        ObjectDisposedException.ThrowIf(disposed, this);
        EnsureAccess();
        invitation.Validate();
        if (listener is null || !peers.ContainsKey(invitation.Fingerprint))
            throw new InvalidOperationException("Start and explicitly pair this peer before connecting.");
        if (!IPAddress.IsLoopback(IPAddress.Parse(invitation.Address)) && !options.DirectIpDisclosureAccepted)
            throw new InvalidOperationException("Direct IP disclosure requires explicit consent.");
        if (!await slots.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Connection limit reached.");
        var client = new TcpClient();
        var id = Interlocked.Increment(ref connectionId);
        sockets[id] = client;
        socketPeers[id] = invitation.Fingerprint;
        var handshakeCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handshakes[id] = handshakeCompletion;
        var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        SslStream? stream = null;
        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellation.Token);
            handshake.CancelAfter(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(IPAddress.Parse(invitation.Address), invitation.Port, handshake.Token).ConfigureAwait(false);
            stream = new SslStream(client.GetStream(), false,
                (_, certificate, _, _) => IsPinned(certificate, invitation.Fingerprint),
                (_, _, _, _, _) => identity.Certificate);
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "FRP1",
                ClientCertificates = new X509CertificateCollection { identity.Certificate },
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            }, handshake.Token).ConfigureAwait(false);
            if (!stream.IsMutuallyAuthenticated) throw new AuthenticationException("Peer authentication failed.");
            MarkAuthenticated(id, invitation.Fingerprint, startedBeforeDisconnect);
            var task = ClientLoopAsync(id, client, stream, invitation.Fingerprint, sessionCancellation.Token);
            connections[id] = task;
            _ = task.ContinueWith(_ => connections.TryRemove(id, out var ignored), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return new SyncSession(task, sessionCancellation, client);
        }
        catch
        {
            if (stream is not null) await stream.DisposeAsync().ConfigureAwait(false);
            sessionCancellation.Dispose();
            client.Dispose();
            sockets.TryRemove(id, out _);
            socketPeers.TryRemove(id, out _);
            slots.Release();
            throw;
        }
        finally
        {
            handshakeCompletion.TrySetResult();
            handshakes.TryRemove(id, out _);
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener!.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested || !slots.Wait(0)) { client.Dispose(); continue; }
                var id = Interlocked.Increment(ref connectionId);
                sockets[id] = client;
                var task = ServeAsync(id, client, Volatile.Read(ref disconnectVersion), cancellationToken);
                connections[id] = task;
                _ = task.ContinueWith(_ => connections.TryRemove(id, out var ignored), CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException or InvalidOperationException)
        { if (!cancellationToken.IsCancellationRequested && !suspended) LastConnectionError = "listener-failed"; }
    }

    private async Task ServeAsync(int id, TcpClient client, long startedBeforeDisconnect, CancellationToken cancellationToken)
    {
        try
        {
            string? remote = null;
            await using var stream = new SslStream(client.GetStream(), false, (_, certificate, _, _) =>
            {
                if (certificate is null) return false;
                var fingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
                if (!IsPinned(certificate, fingerprint)) return false;
                remote = fingerprint;
                socketPeers[id] = fingerprint;
                return true;
            });
            using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                handshake.CancelAfter(TimeSpan.FromSeconds(5));
                await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = identity.Certificate,
                    ClientCertificateRequired = true,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                }, handshake.Token).ConfigureAwait(false);
            }
            if (remote is null || !stream.IsMutuallyAuthenticated) throw new AuthenticationException("Peer authentication failed.");
            MarkAuthenticated(id, remote, startedBeforeDisconnect);
            var incomingCount = 0;
            var rateWindow = DateTime.UtcNow;
            var remoteAcceptsMatureContent = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                operation.CancelAfter(TimeSpan.FromSeconds(5));
                EnsureAccess();
                if (!peers.ContainsKey(remote)) throw new AuthenticationException("Peer is no longer paired.");
                var message = await ReadFrameAsync(stream, operation.Token).ConfigureAwait(false);
                if ((DateTime.UtcNow - rateWindow).TotalSeconds >= 1) { rateWindow = DateTime.UtcNow; incomingCount = 0; }
                if (++incomingCount > 8) throw new InvalidDataException("Peer rate limit exceeded.");
                await ApplyAsync(remote, message, operation.Token).ConfigureAwait(false);
                remoteAcceptsMatureContent = message.AcceptsMatureContent;
                await WriteFrameAsync(stream, CreateMessage(remote, message.KnownHash, null, remoteAcceptsMatureContent), operation.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (IsConnectionException(ex))
        { if (!cancellationToken.IsCancellationRequested) LastConnectionError = "connection-ended"; }
        finally { Release(id, client); }
    }

    private async Task ClientLoopAsync(int id, TcpClient client, SslStream stream, string remote, CancellationToken cancellationToken)
    {
        try
        {
            string? knownHash = null;
            string? sentHash = null;
            var remoteAcceptsMatureContent = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!peers.ContainsKey(remote)) throw new AuthenticationException("Peer is no longer paired.");
                using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                operation.CancelAfter(TimeSpan.FromSeconds(5));
                EnsureAccess();
                var outgoing = CreateMessage(remote, sentHash, knownHash, remoteAcceptsMatureContent);
                await WriteFrameAsync(stream, outgoing, operation.Token).ConfigureAwait(false);
                if (outgoing.Profile is not null) sentHash = outgoing.Hash;
                var incoming = await ReadFrameAsync(stream, operation.Token).ConfigureAwait(false);
                await ApplyAsync(remote, incoming, operation.Token).ConfigureAwait(false);
                remoteAcceptsMatureContent = incoming.AcceptsMatureContent;
                if (incoming.Profile is not null) knownHash = incoming.Hash;
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (IsConnectionException(ex))
        { if (!cancellationToken.IsCancellationRequested) LastConnectionError = "connection-ended"; }
        finally { await stream.DisposeAsync().ConfigureAwait(false); Release(id, client); }
    }

    private WireMessage CreateMessage(string remote, string? alreadySentHash, string? knownHash, bool remoteAcceptsMatureContent)
    {
        EnsureAccess();
        var profile = Volatile.Read(ref localProfile);
        var acceptsMatureContent = store.Policy.AcceptsMatureContent;
        if (profile is null || !peers.TryGetValue(remote, out var share) || !share ||
            (profile.ContentRating == ContentRating.Mature18 && (!acceptsMatureContent || !remoteAcceptsMatureContent)))
            return new WireMessage(1, knownHash, null, null, acceptsMatureContent);
        var hash = ProfileJson.Hash(profile);
        return hash == alreadySentHash ? new WireMessage(1, knownHash, null, null, acceptsMatureContent) :
            new WireMessage(1, knownHash, hash, profile, acceptsMatureContent);
    }

    private async Task ApplyAsync(string remote, WireMessage message, CancellationToken cancellationToken)
    {
        EnsureAccess();
        if (message.ProtocolVersion != 1 || (message.KnownHash is not null && !PeerInvitation.IsFingerprint(message.KnownHash)))
            throw new InvalidDataException("Unsupported protocol or invalid hash.");
        if (message.Profile is null)
        {
            if (message.Hash is not null) throw new InvalidDataException("Unexpected profile hash.");
            return;
        }
        if (!peers.ContainsKey(remote) || !PeerInvitation.IsFingerprint(message.Hash) ||
            !string.Equals(message.Hash, ProfileJson.Hash(message.Profile), StringComparison.Ordinal))
            throw new InvalidDataException("Profile hash or authenticated origin mismatch.");
        store.Policy.EnsureAllowed(message.Profile);
        await store.SaveCachedAsync(remote, message.Profile, cancellationToken).ConfigureAwait(false);
        EnsureAccess();
        store.Policy.EnsureAllowed(message.Profile);
        if (!peers.ContainsKey(remote)) throw new AuthenticationException("Peer is no longer paired.");
        if (Interlocked.Increment(ref receivedCount) <= 64)
            received.Enqueue(new ReceivedProfileNotification(remote, message.Profile.Id, message.Profile.Revision, DateTimeOffset.UtcNow));
        else Interlocked.Decrement(ref receivedCount);
    }

    private void OnAudienceChanged(object? sender, EventArgs args)
    {
        var allowed = store.Policy.AcceptsMatureContent ? 1 : 0;
        var previouslyAllowed = Interlocked.Exchange(ref acceptsMatureContent, allowed);
        if (allowed != 0 || previouslyAllowed == 0) return;
        var current = Volatile.Read(ref localProfile);
        if (current?.ContentRating == ContentRating.Mature18) Interlocked.CompareExchange(ref localProfile, null, current);
        // Disconnect immediately: a reconnect must negotiate the remote capability from false.
        foreach (var socket in sockets.Values) socket.Dispose();
    }

    private bool IsPinned(X509Certificate? certificate, string expected)
    {
        if (!HasAccess || certificate is null || !peers.ContainsKey(expected)) return false;
        using var publicCertificate = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        return publicCertificate.NotBefore.ToUniversalTime() <= DateTime.UtcNow &&
            publicCertificate.NotAfter.ToUniversalTime() >= DateTime.UtcNow &&
            string.Equals(publicCertificate.GetCertHashString(HashAlgorithmName.SHA256), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteFrameAsync(SslStream stream, WireMessage message, CancellationToken cancellationToken)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(message, ProfileJson.Options);
        if (data.Length > MaximumFrameBytes) throw new InvalidDataException("Frame exceeds size limit.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<WireMessage> ReadFrameAsync(SslStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is <= 0 or > MaximumFrameBytes) throw new InvalidDataException("Invalid frame size.");
        var data = new byte[length];
        await stream.ReadExactlyAsync(data, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<WireMessage>(data, ProfileJson.Options) ?? throw new InvalidDataException("Empty message.");
    }

    private static bool IsConnectionException(Exception ex) => ex is IOException or SocketException or AuthenticationException or
        OperationCanceledException or ObjectDisposedException or JsonException or InvalidDataException or ArgumentException or UnauthorizedAccessException;

    private bool HasAccess => !suspended && !disposed && (options.AccessAllowed?.Invoke() ?? true);
    private void MarkAuthenticated(int id, string fingerprint, long startedBeforeDisconnect)
    {
        EnsureAccess();
        lock (authenticationState)
        {
            if (suspended || disposed || !peers.ContainsKey(fingerprint) ||
                (disconnectVersions.TryGetValue(fingerprint, out var disconnected) && disconnected > startedBeforeDisconnect))
                throw new AuthenticationException("The peer connection was withdrawn.");
            authenticatedPeers[id] = fingerprint;
        }
    }
    private void EnsureAccess()
    {
        if (!HasAccess) throw new AuthenticationException("Peer access is unavailable.");
    }

    private void Release(int id, TcpClient client)
    {
        lock (authenticationState) authenticatedPeers.Remove(id);
        client.Dispose();
        sockets.TryRemove(id, out _);
        socketPeers.TryRemove(id, out _);
        slots.Release();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        store.Policy.Changed -= OnAudienceChanged;
        await lifetime.CancelAsync().ConfigureAwait(false);
        listener?.Stop();
        foreach (var socket in sockets.Values) socket.Dispose();
        if (acceptTask is not null) await acceptTask.ConfigureAwait(false);
        await Task.WhenAll(handshakes.Values.Select(completion => completion.Task)).ConfigureAwait(false);
        await Task.WhenAll(connections.Values).ConfigureAwait(false);
        lifetime.Dispose();
        // A caller's in-flight outbound handshake can still unwind and release its slot.
        // This semaphore uses no OS wait handle and must remain releasable until then.
    }

    private sealed record WireMessage(int ProtocolVersion, string? KnownHash, string? Hash, ProfileDocument? Profile,
        bool AcceptsMatureContent = false);
}

public sealed class SyncSession : IAsyncDisposable
{
    private readonly CancellationTokenSource cancellation;
    private readonly TcpClient client;
    public Task Completion { get; }

    internal SyncSession(Task completion, CancellationTokenSource cancellation, TcpClient client)
    { Completion = completion; this.cancellation = cancellation; this.client = client; }

    public async ValueTask DisposeAsync()
    {
        await cancellation.CancelAsync().ConfigureAwait(false);
        client.Dispose();
        await Completion.ConfigureAwait(false);
        cancellation.Dispose();
    }
}
