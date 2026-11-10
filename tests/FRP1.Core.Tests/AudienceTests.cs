using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using FRP1.Core.Networking;
using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class AudienceTests
{
    [Fact]
    public void MatureAccessDefaultsToDeniedAndRequiresBothAdultAndOptIn()
    {
        var policy = new AudiencePolicy();
        Assert.Equal(AudienceSelection.Unspecified, policy.Snapshot.Selection);
        Assert.False(policy.AcceptsMatureContent);
        policy.SetAudience(new AudiencePreferences(AudienceSelection.Adult, false));
        Assert.False(policy.AcceptsMatureContent);
        policy.SetAudience(new AudiencePreferences(AudienceSelection.Adult, true));
        Assert.True(policy.AcceptsMatureContent);
        policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18, true));
        Assert.False(policy.AcceptsMatureContent);
        Assert.False(policy.Snapshot.MatureContentOptIn);
    }

    [Theory]
    [InlineData(AudienceSelection.Unspecified, false)]
    [InlineData(AudienceSelection.Unspecified, true)]
    [InlineData(AudienceSelection.Under18, false)]
    [InlineData(AudienceSelection.Under18, true)]
    [InlineData(AudienceSelection.Adult, false)]
    public async Task RestrictedAudiencesCannotImportStoreOrReadMatureProfiles(AudienceSelection selection, bool optIn)
    {
        using var directory = new TestDirectory();
        var policy = new AudiencePolicy(new AudiencePreferences(AudienceSelection.Adult, true));
        var store = new ProfileStore(directory.Path, policy);
        var mature = Mature();
        var pin = new string('A', 64);
        await store.SaveLocalAsync(mature);
        await store.SaveCachedAsync(pin, mature);
        policy.SetAudience(new AudiencePreferences(selection, optIn));
        Assert.False(policy.AcceptsMatureContent);
        Assert.Throws<InvalidDataException>(() => policy.EnsureAllowed(mature));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(ProfileJson.Export(mature), policy));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadLocalAsync(mature.Id));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetCachedAsync(pin, mature.Id));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveLocalAsync(mature));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveCachedAsync(pin, mature));
        Assert.Empty(await store.ListLocalAsync());
        var general = ProfileTests.Valid();
        await store.SaveLocalAsync(general);
        Assert.Single(await store.ListLocalAsync());
        policy.EnsureAllowed(general);
    }

    [Fact]
    public void MatureRatingCannotBeOmittedOrSetToUnknownValue()
    {
        var json = JsonNode.Parse(ProfileJson.Export(Mature()))!.AsObject();
        json.Remove("contentRating");
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(JsonSerializer.SerializeToUtf8Bytes(json)));
        json["contentRating"] = 999;
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(JsonSerializer.SerializeToUtf8Bytes(json)));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(Mature() with { ContentRating = (ContentRating)999 }));
    }

    [Theory]
    [InlineData(true, AudienceSelection.Unspecified, false)]
    [InlineData(false, AudienceSelection.Unspecified, false)]
    [InlineData(true, AudienceSelection.Under18, false)]
    [InlineData(false, AudienceSelection.Under18, true)]
    [InlineData(true, AudienceSelection.Adult, false)]
    [InlineData(false, AudienceSelection.Adult, false)]
    public async Task MatureProfileNeverReachesRestrictedReceiver(bool senderIsFirst, AudienceSelection selection, bool optIn)
    {
        await using var pair = await NodePair.CreateAsync();
        var sender = senderIsFirst ? pair.First : pair.Second;
        var senderStore = senderIsFirst ? pair.FirstStore : pair.SecondStore;
        var receiverStore = senderIsFirst ? pair.SecondStore : pair.FirstStore;
        var senderIdentity = senderIsFirst ? pair.FirstIdentity : pair.SecondIdentity;
        var receiverIdentity = senderIsFirst ? pair.SecondIdentity : pair.FirstIdentity;
        senderStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Adult, true));
        receiverStore.Policy.SetAudience(new AudiencePreferences(selection, optIn));
        var mature = Mature();
        sender.SetLocalProfile(mature);
        pair.Pair();
        sender.SetSharing(receiverIdentity.Fingerprint, true);
        await using var session = await pair.First.ConnectAsync(pair.Second.CreateInvitation());
        await Task.Delay(650);
        var receiverRoot = Path.Combine(pair.Directory.Path, senderIsFirst ? "second" : "first");
        Assert.False(File.Exists(Path.Combine(receiverRoot, "cache", senderIdentity.Fingerprint, mature.Id.ToString("N") + ".json")));
        Assert.False(session.Completion.IsCompleted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BothAdultsWithOptInCanSynchronizeThenDowngradeStopsAccess(bool senderIsFirst)
    {
        await using var pair = await NodePair.CreateAsync();
        pair.FirstStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Adult, true));
        pair.SecondStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Adult, true));
        var sender = senderIsFirst ? pair.First : pair.Second;
        var senderStore = senderIsFirst ? pair.FirstStore : pair.SecondStore;
        var receiverStore = senderIsFirst ? pair.SecondStore : pair.FirstStore;
        var senderIdentity = senderIsFirst ? pair.FirstIdentity : pair.SecondIdentity;
        var receiverIdentity = senderIsFirst ? pair.SecondIdentity : pair.FirstIdentity;
        var mature = Mature();
        sender.SetLocalProfile(mature);
        pair.Pair();
        sender.SetSharing(receiverIdentity.Fingerprint, true);
        await using var session = await pair.First.ConnectAsync(pair.Second.CreateInvitation());
        await NetworkTests.UntilAsync(async () => await receiverStore.GetCachedAsync(senderIdentity.Fingerprint, mature.Id) is not null);
        Assert.Equal(mature.DisplayName, (await receiverStore.GetCachedAsync(senderIdentity.Fingerprint, mature.Id))!.DisplayName);
        receiverStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18, false));
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAsync<InvalidDataException>(() => receiverStore.GetCachedAsync(senderIdentity.Fingerprint, mature.Id));
        Assert.True(senderStore.Policy.AcceptsMatureContent);
    }

    [Fact]
    public async Task ServerWithMatureProfileDoesNotIncludePayloadWithoutRemoteOptIn()
    {
        await using var pair = await NodePair.CreateAsync();
        pair.SecondStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Adult, true));
        pair.Second.SetLocalProfile(Mature());
        pair.Second.Pair(pair.First.CreateInvitation(), true);
        using var client = new TcpClient();
        await using var stream = await NetworkTests.RawConnectionAsync(client, pair.FirstIdentity, pair.Second.CreateInvitation());
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { protocolVersion = 1, knownHash = (string?)null, hash = (string?)null, profile = (object?)null, acceptsMatureContent = false });
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await stream.ReadExactlyAsync(header, timeout.Token);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        Assert.InRange(length, 1, 65536);
        var response = new byte[length];
        await stream.ReadExactlyAsync(response, timeout.Token);
        using var json = JsonDocument.Parse(response);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("profile").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("hash").ValueKind);
        Assert.DoesNotContain("MATURE TEST DATA", System.Text.Encoding.UTF8.GetString(response));
    }

    [Fact]
    public async Task ClientStartsEachConnectionWithoutRemoteConsentIncludingAfterReconnect()
    {
        await using var pair = await NodePair.CreateAsync();
        pair.FirstStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Adult, true));
        pair.First.SetLocalProfile(Mature());
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var remote = new PeerInvitation(1, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, pair.SecondIdentity.Fingerprint);
        pair.First.Pair(remote, true);
        var firstConnection = CaptureFirstClientFramesAsync(expectMatureAfterConsent: true);
        await using (var session = await pair.First.ConnectAsync(remote)) await firstConnection;
        var reconnection = CaptureFirstClientFramesAsync(expectMatureAfterConsent: false);
        await using (var session = await pair.First.ConnectAsync(remote)) await reconnection;

        async Task CaptureFirstClientFramesAsync(bool expectMatureAfterConsent)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = new SslStream(client.GetStream(), false,
                (_, certificate, _, _) => certificate?.GetCertHashString(HashAlgorithmName.SHA256) == pair.FirstIdentity.Fingerprint);
            await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = pair.SecondIdentity.Certificate,
                ClientCertificateRequired = true,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            }, timeout.Token);
            using var first = await ReadJsonFrameAsync(stream, timeout.Token);
            Assert.Equal(JsonValueKind.Null, first.RootElement.GetProperty("profile").ValueKind);
            Assert.True(first.RootElement.GetProperty("acceptsMatureContent").GetBoolean());
            if (!expectMatureAfterConsent) return;
            var payload = JsonSerializer.SerializeToUtf8Bytes(new { protocolVersion = 1, knownHash = (string?)null, hash = (string?)null, profile = (object?)null, acceptsMatureContent = true });
            var header = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
            await stream.WriteAsync(header, timeout.Token);
            await stream.WriteAsync(payload, timeout.Token);
            using var second = await ReadJsonFrameAsync(stream, timeout.Token);
            Assert.Equal("MATURE TEST DATA (synthetic marker only)", second.RootElement.GetProperty("profile").GetProperty("background").GetString());
        }
    }

    private static async Task<JsonDocument> ReadJsonFrameAsync(SslStream stream, CancellationToken token)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, token);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        Assert.InRange(length, 1, 65536);
        var response = new byte[length];
        await stream.ReadExactlyAsync(response, token);
        return JsonDocument.Parse(response);
    }

    private static ProfileDocument Mature() => ProfileTests.Valid() with
    {
        ContentRating = ContentRating.Mature18,
        Background = "MATURE TEST DATA (synthetic marker only)",
    };
}
