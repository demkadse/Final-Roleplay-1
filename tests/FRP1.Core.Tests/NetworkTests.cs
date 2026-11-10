using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FRP1.Core.Identity;
using FRP1.Core.Networking;
using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class NetworkTests
{
    [Fact]
    public async Task NetworkingAndIpDisclosureRequireSeparateConsent()
    {
        using var directory = new TestDirectory();
        using var identity = PeerIdentity.CreateEphemeral();
        var store = new ProfileStore(directory.Path);
        await using var disabled = new PeerNode(identity, store);
        await Assert.ThrowsAsync<InvalidOperationException>(() => disabled.StartAsync());
        await using var noIpConsent = new PeerNode(identity, store, new PeerNodeOptions { NetworkEnabled = true, BindAddress = IPAddress.Parse("192.0.2.1") });
        await Assert.ThrowsAsync<InvalidOperationException>(() => noIpConsent.StartAsync());
        await using var node = new PeerNode(identity, store, new PeerNodeOptions { NetworkEnabled = true });
        await node.StartAsync();
        var remote = new PeerInvitation(1, "192.0.2.2", 1, new string('A', 64));
        node.Pair(remote);
        await Assert.ThrowsAsync<InvalidOperationException>(() => node.ConnectAsync(remote));
    }

    [Fact]
    public async Task MutualTlsSynchronizesOnlySharedProfilesAndUpdatesAutomatically()
    {
        await using var pair = await NodePair.CreateAsync();
        var firstProfile = ProfileTests.Valid();
        var secondProfile = ProfileTests.Valid() with { DisplayName = "Second synthetic character" };
        pair.First.SetLocalProfile(firstProfile);
        pair.Second.SetLocalProfile(secondProfile);
        pair.Pair();
        await using (var session = await pair.First.ConnectAsync(pair.Second.CreateInvitation()))
        {
            await Task.Delay(600);
            Assert.Null(await pair.SecondStore.GetCachedAsync(pair.FirstIdentity.Fingerprint, firstProfile.Id));
            Assert.Null(await pair.FirstStore.GetCachedAsync(pair.SecondIdentity.Fingerprint, secondProfile.Id));
            pair.First.SetSharing(pair.SecondIdentity.Fingerprint, true);
            pair.Second.SetSharing(pair.FirstIdentity.Fingerprint, true);
            await UntilAsync(async () => await pair.SecondStore.GetCachedAsync(pair.FirstIdentity.Fingerprint, firstProfile.Id) is not null);
            await UntilAsync(async () => await pair.FirstStore.GetCachedAsync(pair.SecondIdentity.Fingerprint, secondProfile.Id) is not null);
            var cachePath = Path.Combine(pair.Directory.Path, "second", "cache", pair.FirstIdentity.Fingerprint, firstProfile.Id.ToString("N") + ".json");
            var written = File.GetLastWriteTimeUtc(cachePath);
            await Task.Delay(700);
            Assert.Equal(written, File.GetLastWriteTimeUtc(cachePath));
            var updated = firstProfile with { Revision = 2, Title = "Automatic update" };
            pair.First.SetLocalProfile(updated);
            await UntilAsync(async () => (await pair.SecondStore.GetCachedAsync(pair.FirstIdentity.Fingerprint, firstProfile.Id))?.Revision == 2);
            pair.First.SetSharing(pair.SecondIdentity.Fingerprint, false);
            pair.First.SetLocalProfile(updated with { Revision = 3, Title = "Private revision" });
            await Task.Delay(650);
            Assert.Equal(2, (await pair.SecondStore.GetCachedAsync(pair.FirstIdentity.Fingerprint, firstProfile.Id))!.Revision);
        }
        var offline = new ProfileStore(Path.Combine(pair.Directory.Path, "second"));
        Assert.Equal("Automatic update", (await offline.GetCachedAsync(pair.FirstIdentity.Fingerprint, firstProfile.Id))!.Title);
    }

    [Fact]
    public async Task WrongPinnedServerIsRejected()
    {
        await using var pair = await NodePair.CreateAsync();
        using var unrelatedIdentity = PeerIdentity.CreateEphemeral();
        var forged = pair.Second.CreateInvitation() with { Fingerprint = unrelatedIdentity.Fingerprint };
        pair.First.Pair(forged, true);
        pair.Second.Pair(pair.First.CreateInvitation(), true);
        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var session = await pair.First.ConnectAsync(forged);
        });
        Assert.NotNull(exception);
        Assert.True(exception is AuthenticationException or IOException, exception.GetType().Name);
    }

    [Fact]
    public async Task UnpairedClientCannotSendProfile()
    {
        await using var pair = await NodePair.CreateAsync();
        var profile = ProfileTests.Valid();
        pair.First.SetLocalProfile(profile);
        pair.First.Pair(pair.Second.CreateInvitation(), true);
        try
        {
            await using var session = await pair.First.ConnectAsync(pair.Second.CreateInvitation());
            await session.Completion.WaitAsync(TimeSpan.FromSeconds(4));
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException) { }
        Assert.Null(await pair.SecondStore.GetCachedAsync(pair.FirstIdentity.Fingerprint, profile.Id));
        Assert.Equal("connection-ended", pair.Second.LastConnectionError);
    }

    [Fact]
    public async Task UnpairTerminatesActiveSession()
    {
        await using var pair = await NodePair.CreateAsync();
        pair.Pair();
        await using var session = await pair.First.ConnectAsync(pair.Second.CreateInvitation());
        pair.First.Unpair(pair.SecondIdentity.Fingerprint);
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.True(session.Completion.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65537)]
    public async Task InvalidFrameLengthIsRejectedBeforePayloadAllocation(int length)
    {
        await using var pair = await NodePair.CreateAsync();
        pair.Second.Pair(pair.First.CreateInvitation());
        using var client = new TcpClient();
        await using var stream = await RawConnectionAsync(client, pair.FirstIdentity, pair.Second.CreateInvitation());
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, length);
        await stream.WriteAsync(header);
        await UntilAsync(() => Task.FromResult(pair.Second.LastConnectionError is not null));
        var read = await stream.ReadAsync(new byte[1]).AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, read);
    }

    [Theory]
    [InlineData("{\"protocolVersion\":999,\"knownHash\":null,\"hash\":null,\"profile\":null}")]
    [InlineData("{\"protocolVersion\":1,\"knownHash\":\"invalid\",\"hash\":null,\"profile\":null}")]
    [InlineData("{\"protocolVersion\":1,\"knownHash\":null,\"hash\":\"invalid\",\"profile\":null}")]
    public async Task UnsupportedProtocolAndInvalidHashTerminateConnection(string message)
    {
        await using var pair = await NodePair.CreateAsync();
        pair.Second.Pair(pair.First.CreateInvitation());
        using var client = new TcpClient();
        await using var stream = await RawConnectionAsync(client, pair.FirstIdentity, pair.Second.CreateInvitation());
        var bytes = Encoding.UTF8.GetBytes(message);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, bytes.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(bytes);
        await UntilAsync(() => Task.FromResult(pair.Second.LastConnectionError is not null));
        Assert.Equal(0, await stream.ReadAsync(new byte[1]).AsTask().WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task LocalProfileChangesRequireHigherRevision()
    {
        await using var pair = await NodePair.CreateAsync();
        var profile = ProfileTests.Valid();
        pair.First.SetLocalProfile(profile);
        Assert.Throws<InvalidDataException>(() => pair.First.SetLocalProfile(profile with { Title = "Changed without revision" }));
        pair.First.SetLocalProfile(profile with { Revision = 2 });
        Assert.Throws<InvalidDataException>(() => pair.First.SetLocalProfile(profile));
    }

    internal static async Task<SslStream> RawConnectionAsync(TcpClient client, PeerIdentity identity, PeerInvitation remote)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        await client.ConnectAsync(IPAddress.Parse(remote.Address), remote.Port, timeout.Token);
        var stream = new SslStream(client.GetStream(), false,
            (_, certificate, _, _) => certificate?.GetCertHashString(HashAlgorithmName.SHA256) == remote.Fingerprint,
            (_, _, _, _, _) => identity.Certificate);
        await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "FRP1",
            ClientCertificates = new X509CertificateCollection { identity.Certificate },
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        }, timeout.Token);
        return stream;
    }

    internal static async Task UntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition()) await Task.Delay(30, timeout.Token);
    }
}

internal sealed class NodePair : IAsyncDisposable
{
    public TestDirectory Directory { get; } = new();
    public PeerIdentity FirstIdentity { get; } = PeerIdentity.CreateEphemeral();
    public PeerIdentity SecondIdentity { get; } = PeerIdentity.CreateEphemeral();
    public ProfileStore FirstStore { get; }
    public ProfileStore SecondStore { get; }
    public PeerNode First { get; }
    public PeerNode Second { get; }
    private NodePair()
    {
        FirstStore = new ProfileStore(Path.Combine(Directory.Path, "first"));
        SecondStore = new ProfileStore(Path.Combine(Directory.Path, "second"));
        First = new PeerNode(FirstIdentity, FirstStore, new PeerNodeOptions { NetworkEnabled = true });
        Second = new PeerNode(SecondIdentity, SecondStore, new PeerNodeOptions { NetworkEnabled = true });
    }
    public static async Task<NodePair> CreateAsync()
    {
        var pair = new NodePair();
        await pair.First.StartAsync();
        await pair.Second.StartAsync();
        return pair;
    }
    public void Pair()
    {
        First.Pair(Second.CreateInvitation());
        Second.Pair(First.CreateInvitation());
    }
    public async ValueTask DisposeAsync()
    {
        await First.DisposeAsync();
        await Second.DisposeAsync();
        FirstIdentity.Dispose();
        SecondIdentity.Dispose();
        Directory.Dispose();
    }
}
