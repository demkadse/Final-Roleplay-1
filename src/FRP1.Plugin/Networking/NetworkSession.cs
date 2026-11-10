using System.Net;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography;
using FRP1.Core.Identity;
using FRP1.Core.Networking;
using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;

namespace FRP1.Plugin.Networking;

public sealed record NetworkStartOptions
{
    public bool NetworkEnabled { get; init; }
    public bool DirectIpConsent { get; init; }
    public IPAddress BindAddress { get; init; } = IPAddress.Loopback;
    public int ListenPort { get; init; }
}

// Pure service: no UI calls, no automatic network startup, no profile publication on restart.
public sealed class NetworkSession : IAsyncDisposable
{
    private readonly ProfileStore profiles;
    private readonly PeerIdentityStore identities;
    private readonly PeerTrustStore trustStore;
    private readonly Func<IReadOnlyList<PeerTrust>, CancellationToken, Task> saveTrust;
    private readonly object state = new();
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<string, SyncSession> sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, Task> pending = new();
    private int pendingCount;
    private int operationId;
    private readonly HashSet<string> immediatelyDenied = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> sharingSuppressed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> peerDecisions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> connectionDecisions = new(StringComparer.OrdinalIgnoreCase);
    private long nextPeerDecision;
    private long nextConnectionDecision;
    private long networkIntent;
    private IReadOnlyList<PeerTrust> peers = Array.Empty<PeerTrust>();
    private PeerIdentity? identity;
    private PeerNode? node;
    private bool initialized;
    private bool enabled;
    private bool accessReady;
    private bool disposed;
    private bool previouslyAcceptedMature;
    private bool ageSelected;
    private ulong contentId;
    private long contextVersion;
    private string? errorCode;

    public NetworkSession(ProfileStore profileStore, PeerIdentityStore identityStore, PeerTrustStore peerTrustStore,
        Func<IReadOnlyList<PeerTrust>, CancellationToken, Task>? saveTrust = null)
    {
        profiles = profileStore;
        identities = identityStore;
        trustStore = peerTrustStore;
        this.saveTrust = saveTrust ?? peerTrustStore.SaveAsync;
        previouslyAcceptedMature = profiles.Policy.AcceptsMatureContent;
        ageSelected = profiles.Policy.Snapshot.Selection != AudienceSelection.Unspecified;
        profiles.Policy.Changed += OnAudienceChanged;
    }

    public bool IsInitialized { get { lock (state) return initialized; } }
    public bool IsRunning { get { lock (state) return enabled && node is not null; } }
    public string? ErrorCode { get { lock (state) return errorCode ?? node?.LastConnectionError; } }
    public IReadOnlyList<string> ConnectedPeerFingerprints
    {
        get
        {
            lock (state)
                return enabled && ageSelected && accessReady && contentId != 0 && node is not null
                    ? Array.AsReadOnly(node.ConnectedPeerFingerprints.Where(pin => !immediatelyDenied.Contains(pin) &&
                        peers.Any(peer => SamePin(peer, pin) && !peer.Blocked)).ToArray())
                    : Array.Empty<string>();
        }
    }
    public IReadOnlyList<PeerTrust> Peers
    {
        get
        {
            lock (state) return PeerTrustStore.ValidateAndSnapshot(peers.Select(peer => peer with
            {
                Blocked = peer.Blocked || immediatelyDenied.Contains(peer.Invitation.Fingerprint),
                ShareProfile = peer.ShareProfile && !sharingSuppressed.Contains(peer.Invitation.Fingerprint),
            }));
        }
    }

    public void ObserveContext(ulong currentContentId, bool currentAccessReady)
    {
        lock (state)
        {
            ThrowIfDisposed();
            if (contentId == currentContentId && accessReady == currentAccessReady) return;
            contentId = currentContentId;
            accessReady = currentAccessReady;
            contextVersion++;
            SuspendLocked();
        }
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => ExecuteAsync(async token =>
    {
        await InitializeCoreAsync(token).ConfigureAwait(false);
    }, "initialization-failed", cancellationToken);

    private async Task InitializeCoreAsync(CancellationToken token)
    {
        lock (state) { ThrowIfDisposed(); if (initialized) return; }
        var loadedPeers = await trustStore.LoadAsync(token).ConfigureAwait(false);
        var loadedIdentity = trustStore.LocalIdentityFingerprint is not null || loadedPeers.Count > 0
            ? await identities.LoadExistingAsync(token).ConfigureAwait(false)
            : await identities.LoadOrCreateAsync(token).ConfigureAwait(false);
        try
        {
            trustStore.BindLocalIdentity(loadedIdentity.Fingerprint);
            if (loadedPeers.Any(peer => string.Equals(peer.Invitation.Fingerprint, loadedIdentity.Fingerprint, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("The peer list contains the local identity.");
            lock (state)
            {
                ThrowIfDisposed();
                identity = loadedIdentity;
                peers = loadedPeers;
                initialized = true;
                loadedIdentity = null!;
            }
        }
        finally { loadedIdentity?.Dispose(); }
    }

    public Task StartAsync(NetworkStartOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.BindAddress);
        if (options.ListenPort is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(options), "Invalid local listen port.");
        if (!options.NetworkEnabled || !options.DirectIpConsent)
            throw new InvalidOperationException("Netzwerk und direkte IP-Sichtbarkeit müssen ausdrücklich freigegeben werden.");
        long requestedContext;
        long requestedIntent;
        lock (state) { EnsureContextLocked(); requestedContext = contextVersion; requestedIntent = checked(++networkIntent); }
        return ExecuteAsync(async token =>
        {
            lock (state) if (requestedIntent != networkIntent || requestedContext != contextVersion) return;
            await InitializeCoreAsync(token).ConfigureAwait(false);
            lock (state) if (requestedIntent != networkIntent || requestedContext != contextVersion) return;
            await StopCoreAsync().ConfigureAwait(false);
            long generation;
            lock (state)
            {
                EnsureContextLocked();
                if (contextVersion != requestedContext || requestedIntent != networkIntent) return;
                generation = contextVersion;
                enabled = true;
            }
            var next = new PeerNode(identity!, profiles, new PeerNodeOptions
            {
                NetworkEnabled = true, DirectIpDisclosureAccepted = true, BindAddress = options.BindAddress, ListenPort = options.ListenPort,
                AccessAllowed = () => IsAllowed(generation),
            });
            lock (state)
            {
                if (requestedIntent != networkIntent || requestedContext != contextVersion)
                    next.Suspend();
                foreach (var peer in peers.Where(peer => !peer.Blocked && !immediatelyDenied.Contains(peer.Invitation.Fingerprint)))
                    next.Pair(peer.Invitation, peer.ShareProfile && !sharingSuppressed.Contains(peer.Invitation.Fingerprint));
                node = next;
            }
            try { await next.StartAsync(token).ConfigureAwait(false); }
            catch
            {
                lock (state) { enabled = false; if (ReferenceEquals(node, next)) node = null; }
                await next.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }, "start-failed", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        long requestedIntent;
        lock (state) { ThrowIfDisposed(); requestedIntent = checked(++networkIntent); enabled = false; node?.Suspend(); }
        return ExecuteAsync(async _ =>
        {
            lock (state) if (requestedIntent != networkIntent) return;
            await StopCoreAsync().ConfigureAwait(false);
        }, "stop-failed", cancellationToken);
    }

    private async Task StopCoreAsync()
    {
        PeerNode? previous;
        lock (state) { enabled = false; previous = node; node = null; previous?.Suspend(); }
        foreach (var session in sessions.Values) await session.DisposeAsync().ConfigureAwait(false);
        sessions.Clear();
        if (previous is not null) await previous.DisposeAsync().ConfigureAwait(false);
    }

    public PeerInvitation CreateInvitation()
    {
        lock (state) { EnsureRunningLocked(); return node!.CreateInvitation(); }
    }

    public Task PairAsync(PeerInvitation invitation, string displayName = "", bool shareProfile = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        invitation.Validate();
        long decision;
        lock (state)
        {
            decision = BeginPeerDecisionLocked(invitation.Fingerprint);
            if (!shareProfile) SuppressSharingLocked(invitation.Fingerprint);
        }
        return ExecuteAsync(async token =>
        {
            await InitializeCoreAsync(token).ConfigureAwait(false);
            IReadOnlyList<PeerTrust> updated;
            lock (state)
            {
                if (!IsCurrentDecisionLocked(invitation.Fingerprint, decision)) return;
                if (string.Equals(invitation.Fingerprint, identity!.Fingerprint, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Die eigene Identität kann nicht gekoppelt werden.");
                var previous = FindPeerLocked(invitation.Fingerprint, required: false);
                if (previous?.Blocked == true || immediatelyDenied.Contains(invitation.Fingerprint))
                    throw new InvalidOperationException("Die Gegenstelle ist gesperrt; Sperre zuerst ausdrücklich aufheben.");
                updated = PeerTrustStore.ValidateAndSnapshot(peers.Where(peer => !SamePin(peer, invitation.Fingerprint))
                    .Append(new PeerTrust(invitation, displayName, shareProfile)));
            }
            await saveTrust(updated, token).ConfigureAwait(false);
            lock (state)
            {
                if (!IsCurrentDecisionLocked(invitation.Fingerprint, decision)) return;
                peers = updated;
                if (shareProfile) sharingSuppressed.Remove(invitation.Fingerprint);
                if (enabled) node?.Pair(invitation, shareProfile && !sharingSuppressed.Contains(invitation.Fingerprint));
            }
        }, "pairing-failed", cancellationToken);
    }

    public Task ConnectAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        long requestedContext;
        long requestedConnection;
        long requestedNetworkIntent;
        lock (state)
        {
            EnsureRunningLocked();
            requestedContext = contextVersion;
            requestedConnection = BeginConnectionDecisionLocked(fingerprint);
            requestedNetworkIntent = networkIntent;
        }
        return ExecuteAsync(async token =>
        {
        PeerNode current;
        PeerTrust peer;
        long generation;
        lock (state)
        {
            if (requestedNetworkIntent != networkIntent) return;
            EnsureRunningLocked();
            if (!IsCurrentConnectionDecisionLocked(fingerprint, requestedConnection)) return;
            if (contextVersion != requestedContext)
                throw new InvalidOperationException("Der Spielkontext hat sich geändert. Verbindung erneut ausdrücklich starten.");
            peer = FindPeerLocked(fingerprint)!;
            if (peer.Blocked || immediatelyDenied.Contains(fingerprint)) throw new InvalidOperationException("Gegenstelle ist gesperrt.");
            current = node!;
            generation = contextVersion;
        }
        foreach (var completed in sessions.Where(pair => pair.Value.Completion.IsCompleted).ToArray())
        { await completed.Value.DisposeAsync().ConfigureAwait(false); sessions.Remove(completed.Key); }
        if (sessions.ContainsKey(fingerprint)) return;
        if (sessions.Count >= 4) throw new InvalidOperationException("Verbindungslimit erreicht.");
        Task<SyncSession> connecting;
        lock (state)
        {
            if (!IsCurrentConnectionDecisionLocked(fingerprint, requestedConnection) || !enabled || contextVersion != generation ||
                requestedNetworkIntent != networkIntent) return;
            connecting = current.ConnectAsync(peer.Invitation, token);
        }
        var session = await connecting.ConfigureAwait(false);
        lock (state)
        {
            if (enabled && contextVersion == generation && requestedNetworkIntent == networkIntent && !immediatelyDenied.Contains(fingerprint) &&
                IsCurrentConnectionDecisionLocked(fingerprint, requestedConnection))
            { sessions[fingerprint] = session; return; }
        }
        await session.DisposeAsync().ConfigureAwait(false);
        throw new InvalidOperationException("Der Spielkontext hat sich geändert. Verbindung erneut ausdrücklich starten.");
        }, "connection-failed", cancellationToken);
    }

    public Task DisconnectAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        long requestedConnection;
        long requestedNetworkIntent;
        lock (state)
        {
            ThrowIfDisposed();
            requestedConnection = BeginConnectionDecisionLocked(fingerprint);
            requestedNetworkIntent = networkIntent;
            node?.DisconnectPeer(fingerprint);
        }
        return ExecuteAsync(async _ =>
        {
            lock (state) if (!IsCurrentConnectionDecisionLocked(fingerprint, requestedConnection) || requestedNetworkIntent != networkIntent) return;
            if (sessions.Remove(fingerprint, out var session)) await session.DisposeAsync().ConfigureAwait(false);
            lock (state)
                if (IsCurrentConnectionDecisionLocked(fingerprint, requestedConnection) && requestedNetworkIntent == networkIntent) node?.DisconnectPeer(fingerprint);
        }, "disconnect-failed", cancellationToken);
    }

    public Task SetSharingAsync(string fingerprint, bool shareProfile, CancellationToken cancellationToken = default)
    {
        if (!PeerInvitation.IsFingerprint(fingerprint)) throw new ArgumentException("Ungültige Peer-Identität.");
        long decision;
        lock (state)
        {
            decision = BeginPeerDecisionLocked(fingerprint);
            if (!shareProfile) SuppressSharingLocked(fingerprint);
        }
        return ChangePeerAsync(fingerprint, decision, peer => peer with { ShareProfile = shareProfile }, false, cancellationToken);
    }

    public Task SetBlockedAsync(string fingerprint, bool blocked, CancellationToken cancellationToken = default)
    {
        if (!PeerInvitation.IsFingerprint(fingerprint)) throw new ArgumentException("Ungültige Peer-Identität.");
        long decision;
        lock (state)
        {
            decision = BeginPeerDecisionLocked(fingerprint);
            if (blocked) { immediatelyDenied.Add(fingerprint); sharingSuppressed.Add(fingerprint); node?.Unpair(fingerprint); }
        }
        return ChangePeerAsync(fingerprint, decision, peer => peer with { Blocked = blocked, ShareProfile = false }, !blocked, cancellationToken);
    }

    private Task ChangePeerAsync(string fingerprint, long decision, Func<PeerTrust, PeerTrust> change, bool removeImmediateDenial, CancellationToken cancellationToken) =>
        ExecuteAsync(async token =>
        {
            IReadOnlyList<PeerTrust> updated;
            lock (state)
            {
                if (!IsCurrentDecisionLocked(fingerprint, decision)) return;
                if (!initialized) throw new InvalidOperationException("Lokale Kopplungen sind noch nicht geladen.");
                var existing = FindPeerLocked(fingerprint)!;
                var replacement = change(existing);
                if (replacement.ShareProfile && (replacement.Blocked || immediatelyDenied.Contains(fingerprint)))
                    throw new InvalidOperationException("Eine gesperrte Gegenstelle erhält keine Profilfreigabe.");
                updated = PeerTrustStore.ValidateAndSnapshot(peers.Select(peer => SamePin(peer, fingerprint) ? replacement : peer));
            }
            await saveTrust(updated, token).ConfigureAwait(false);
            lock (state)
            {
                if (!IsCurrentDecisionLocked(fingerprint, decision)) return;
                peers = updated;
                if (removeImmediateDenial) immediatelyDenied.Remove(fingerprint);
                var replacement = FindPeerLocked(fingerprint)!;
                if (replacement.ShareProfile) sharingSuppressed.Remove(fingerprint);
                if (enabled && !replacement.Blocked && !immediatelyDenied.Contains(fingerprint)) node?.Pair(replacement.Invitation,
                    replacement.ShareProfile && !sharingSuppressed.Contains(fingerprint));
            }
        }, "peer-settings-failed", cancellationToken);

    public Task UnpairAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        if (!PeerInvitation.IsFingerprint(fingerprint)) throw new ArgumentException("Ungültige Peer-Identität.");
        long decision;
        lock (state)
        {
            decision = BeginPeerDecisionLocked(fingerprint);
            immediatelyDenied.Add(fingerprint);
            sharingSuppressed.Add(fingerprint);
            node?.Unpair(fingerprint);
        }
        return ExecuteAsync(async token =>
        {
            IReadOnlyList<PeerTrust> updated;
            lock (state)
            {
                if (!IsCurrentDecisionLocked(fingerprint, decision)) return;
                _ = FindPeerLocked(fingerprint);
                updated = PeerTrustStore.ValidateAndSnapshot(peers.Where(peer => !SamePin(peer, fingerprint)));
            }
            await saveTrust(updated, token).ConfigureAwait(false);
            lock (state)
            {
                if (!IsCurrentDecisionLocked(fingerprint, decision)) return;
                peers = updated;
                immediatelyDenied.Remove(fingerprint);
                sharingSuppressed.Remove(fingerprint);
                peerDecisions.Remove(fingerprint);
            }
        }, "unpairing-failed", cancellationToken);
    }

    public void SetPublishedProfile(ProfileDocument? profile)
    {
        if (profile is not null) profiles.Policy.EnsureAllowed(profile);
        PeerNode current;
        long generation;
        lock (state)
        {
            ThrowIfDisposed();
            if (profile is null) { SuspendLocked(); return; }
            EnsureRunningLocked();
            current = node!;
            generation = contextVersion;
        }
        current.SetLocalProfile(profile);
        lock (state)
        {
            if (!enabled || contextVersion != generation || !ReferenceEquals(node, current)) current.Suspend();
        }
    }

    public bool TryDequeueReceived(out ReceivedProfileNotification? notification)
    {
        lock (state)
        {
            while (enabled && node is not null && node.TryDequeueReceived(out var next))
            {
                if (next is not null && peers.Any(peer => SamePin(peer, next.Fingerprint) && !peer.Blocked) && !immediatelyDenied.Contains(next.Fingerprint))
                { notification = next; return true; }
            }
            notification = null;
            return false;
        }
    }

    private Task ExecuteAsync(Func<CancellationToken, Task> action, string failureCode, CancellationToken callerToken)
    {
        CancellationToken lifetimeToken;
        lock (state) { ThrowIfDisposed(); lifetimeToken = lifetime.Token; }
        if (Interlocked.Increment(ref pendingCount) > 16)
        {
            Interlocked.Decrement(ref pendingCount);
            throw new InvalidOperationException("Zu viele ausstehende Netzwerkaktionen.");
        }
        var id = Interlocked.Increment(ref operationId);
        var task = Task.Run(async () =>
        {
            try
            {
                using var source = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken, callerToken);
                var token = source.Token;
                await operations.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    ThrowIfDisposed();
                    await action(token).ConfigureAwait(false);
                    lock (state) errorCode = null;
                }
                catch (OperationCanceledException) { lock (state) errorCode = "operation-cancelled"; throw; }
                catch (Exception ex) when (Expected(ex))
                {
                    lock (state) errorCode = failureCode;
                    throw new InvalidOperationException("Die Netzwerkaktion konnte nicht sicher abgeschlossen werden. Einstellungen prüfen und erneut versuchen.");
                }
                finally { operations.Release(); }
            }
            finally { Interlocked.Decrement(ref pendingCount); }
        });
        pending[id] = task;
        _ = task.ContinueWith(completed =>
        {
            if (completed.IsFaulted) _ = completed.Exception;
            pending.TryRemove(id, out _);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }

    private bool IsAllowed(long generation)
    {
        // No policy callback lock inversion: AudiencePolicy invokes Changed under its own lock.
        lock (state) return !disposed && enabled && accessReady && contentId != 0 && contextVersion == generation;
    }

    private void OnAudienceChanged(object? sender, EventArgs args)
    {
        var acceptsMature = profiles.Policy.AcceptsMatureContent;
        var unspecified = profiles.Policy.Snapshot.Selection == AudienceSelection.Unspecified;
        lock (state)
        {
            ageSelected = !unspecified;
            if (unspecified || (previouslyAcceptedMature && !acceptsMature)) { contextVersion++; SuspendLocked(); }
            previouslyAcceptedMature = acceptsMature;
        }
    }

    private void SuspendLocked()
    {
        networkIntent = checked(networkIntent + 1);
        enabled = false;
        node?.Suspend();
    }

    private void EnsureContextLocked()
    {
        ThrowIfDisposed();
        if (!accessReady || contentId == 0 || !ageSelected)
            throw new InvalidOperationException("Ein freigegebener, angemeldeter Spielcharakter ist erforderlich.");
    }

    private void EnsureRunningLocked()
    {
        EnsureContextLocked();
        if (!enabled || node is null) throw new InvalidOperationException("Direktverbindungen sind ausgeschaltet.");
    }

    private PeerTrust? FindPeerLocked(string fingerprint, bool required = true)
    {
        if (!PeerInvitation.IsFingerprint(fingerprint)) throw new ArgumentException("Ungültige Peer-Identität.");
        var peer = peers.FirstOrDefault(peer => SamePin(peer, fingerprint));
        if (peer is null && required) throw new InvalidOperationException("Die Gegenstelle ist nicht gekoppelt.");
        return peer;
    }

    private long BeginPeerDecisionLocked(string fingerprint)
    {
        ThrowIfDisposed();
        if (!peerDecisions.ContainsKey(fingerprint) && peerDecisions.Count >= 64)
            throw new InvalidOperationException("Zu viele ausstehende Peer-Entscheidungen.");
        var decision = checked(++nextPeerDecision);
        peerDecisions[fingerprint] = decision;
        return decision;
    }

    private bool IsCurrentDecisionLocked(string fingerprint, long decision) =>
        peerDecisions.TryGetValue(fingerprint, out var current) && current == decision;

    private long BeginConnectionDecisionLocked(string fingerprint)
    {
        if (!PeerInvitation.IsFingerprint(fingerprint)) throw new ArgumentException("Ungültige Peer-Identität.");
        if (!connectionDecisions.ContainsKey(fingerprint) && connectionDecisions.Count >= 64)
            throw new InvalidOperationException("Zu viele ausstehende Verbindungsentscheidungen.");
        var decision = checked(++nextConnectionDecision);
        connectionDecisions[fingerprint] = decision;
        return decision;
    }

    private bool IsCurrentConnectionDecisionLocked(string fingerprint, long decision) =>
        connectionDecisions.TryGetValue(fingerprint, out var current) && current == decision;

    private void SuppressSharingLocked(string fingerprint)
    {
        sharingSuppressed.Add(fingerprint);
        if (node is not null && peers.Any(peer => SamePin(peer, fingerprint) && !peer.Blocked && !immediatelyDenied.Contains(fingerprint)))
            node.SetSharing(fingerprint, false);
    }

    private static bool SamePin(PeerTrust peer, string fingerprint) => string.Equals(peer.Invitation.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase);
    private static bool Expected(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or
        ArgumentException or InvalidOperationException or CryptographicException or AuthenticationException or System.Net.Sockets.SocketException or PlatformNotSupportedException;
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    public async ValueTask DisposeAsync()
    {
        lock (state)
        {
            if (disposed) return;
            disposed = true;
            profiles.Policy.Changed -= OnAudienceChanged;
            SuspendLocked();
        }
        await lifetime.CancelAsync().ConfigureAwait(false);
        try { await Task.WhenAll(pending.Values).ConfigureAwait(false); }
        catch (Exception ex) when (Expected(ex) || ex is OperationCanceledException) { /* Observe controlled action failures on unload. */ }
        await operations.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
            identity?.Dispose();
            identity = null;
        }
        finally { operations.Release(); lifetime.Dispose(); }
    }
}
