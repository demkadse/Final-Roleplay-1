using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using FRP1.Core.Identity;
using FRP1.Core.Networking;
using Xunit;

namespace FRP1.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class IdentityTrustTests
{
    [Fact]
    public async Task ProtectedIdentityRetainsItsPinAcrossIndependentStoreReloads()
    {
        using var directory = new TestDirectory();
        using var first = await new PeerIdentityStore(directory.Path).LoadOrCreateAsync();
        using var second = await new PeerIdentityStore(directory.Path).LoadOrCreateAsync();
        using var existing = await new PeerIdentityStore(directory.Path).LoadExistingAsync();
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.Fingerprint, existing.Fingerprint);
        var file = Assert.Single(Directory.EnumerateFiles(directory.Path));
        Assert.Equal("peer-identity.dpapi", Path.GetFileName(file));
    }

    [Fact]
    public async Task ConcurrentInitialCreationAdoptsOneStableIdentity()
    {
        using var directory = new TestDirectory();
        var identities = await Task.WhenAll(new PeerIdentityStore(directory.Path).LoadOrCreateAsync(), new PeerIdentityStore(directory.Path).LoadOrCreateAsync());
        try { Assert.Equal(identities[0].Fingerprint, identities[1].Fingerprint); }
        finally { foreach (var identity in identities) identity.Dispose(); }
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }

    [Fact]
    public async Task CorruptProtectedIdentityIsRejectedWithoutReplacement()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "peer-identity.dpapi");
        byte[] damaged = [1, 2, 3, 4];
        await File.WriteAllBytesAsync(path, damaged);
        await Assert.ThrowsAsync<CryptographicException>(() => new PeerIdentityStore(directory.Path).LoadOrCreateAsync());
        Assert.Equal(damaged, await File.ReadAllBytesAsync(path));
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }

    [Fact]
    public async Task ExpiredProtectedIdentityIsRejectedWithoutReplacement()
    {
        using var directory = new TestDirectory();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Synthetic expired FRP1 test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var expired = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(-1));
        var cleartext = expired.Export(X509ContentType.Pkcs12);
        byte[] protectedBytes;
        try { protectedBytes = ProtectedData.Protect(cleartext, "FRP1.peer-identity.v1"u8.ToArray(), DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(cleartext); }
        var path = Path.Combine(directory.Path, "peer-identity.dpapi");
        await File.WriteAllBytesAsync(path, protectedBytes);
        await Assert.ThrowsAsync<CryptographicException>(() => new PeerIdentityStore(directory.Path).LoadOrCreateAsync());
        Assert.Equal(protectedBytes, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task LockedIdentityAndCancelledCreationNeverSilentlyGenerateReplacement()
    {
        using var directory = new TestDirectory();
        using var identity = await new PeerIdentityStore(directory.Path).LoadOrCreateAsync();
        var path = Path.Combine(directory.Path, "peer-identity.dpapi");
        var before = await File.ReadAllBytesAsync(path);
        using (var fileLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            await Assert.ThrowsAsync<IOException>(() => new PeerIdentityStore(directory.Path).LoadOrCreateAsync());
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        using var freshDirectory = new TestDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PeerIdentityStore(freshDirectory.Path).LoadOrCreateAsync(cancellation.Token));
        Assert.Empty(Directory.EnumerateFiles(freshDirectory.Path));
    }

    [Fact]
    public async Task TrustPinsSurviveReloadAndRejectChangedLocalIdentity()
    {
        using var directory = new TestDirectory();
        var store = new PeerTrustStore(directory.Path);
        store.BindLocalIdentity(new string('A', 64));
        await store.SaveAsync([Trust('B')]);
        var reopened = new PeerTrustStore(directory.Path);
        var loaded = Assert.Single(await reopened.LoadAsync());
        Assert.False(loaded.ShareProfile);
        reopened.BindLocalIdentity(new string('a', 64));
        Assert.Throws<InvalidDataException>(() => reopened.BindLocalIdentity(new string('C', 64)));
        Assert.Equal(new string('A', 64), reopened.LocalIdentityFingerprint);
    }

    [Fact]
    public void TrustSnapshotsRejectDuplicatePinsAndLimitsAndNeverShareWithBlockedPeers()
    {
        var normalized = PeerTrustStore.ValidateAndSnapshot([Trust('a') with { ShareProfile = true, Blocked = true }]);
        Assert.Equal(new string('A', 64), normalized[0].Invitation.Fingerprint);
        Assert.False(normalized[0].ShareProfile);
        Assert.Throws<InvalidDataException>(() => PeerTrustStore.ValidateAndSnapshot([Trust('a'), Trust('A')]));
        var maximum = Enumerable.Range(1, 16).Select(i => new PeerTrust(new(1, "127.0.0.1", 1234, i.ToString("X64")))).ToArray();
        Assert.Equal(16, PeerTrustStore.ValidateAndSnapshot(maximum).Count);
        Assert.Throws<InvalidDataException>(() => PeerTrustStore.ValidateAndSnapshot(maximum.Append(Trust('F'))));
        Assert.Throws<InvalidDataException>(() => PeerTrustStore.ValidateAndSnapshot([Trust('A') with { DisplayName = "invalid\0name" }]));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":1,\"localIdentityFingerprint\":null,\"peers\":[],\"unknown\":1}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"localIdentityFingerprint\":null,\"peers\":[]}")]
    public async Task CorruptOrUnexpectedTrustDocumentsAreRejected(string text)
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "peer-trust.json"), text);
        await Assert.ThrowsAsync<InvalidDataException>(() => new PeerTrustStore(directory.Path).LoadAsync());
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("localIdentityFingerprint")]
    [InlineData("peers")]
    public async Task TrustDocumentFieldsAreRequired(string property)
    {
        using var directory = new TestDirectory();
        var store = new PeerTrustStore(directory.Path);
        await store.SaveAsync([Trust('A')]);
        var path = Path.Combine(directory.Path, "peer-trust.json");
        var json = JsonNode.Parse(await File.ReadAllBytesAsync(path))!.AsObject();
        json.Remove(property);
        await File.WriteAllTextAsync(path, json.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Fact]
    public async Task TrustByteLimitAndCancellationPreserveCommittedFile()
    {
        using var directory = new TestDirectory();
        var store = new PeerTrustStore(directory.Path);
        await store.SaveAsync([Trust('A')]);
        var path = Path.Combine(directory.Path, "peer-trust.json");
        var before = await File.ReadAllBytesAsync(path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync([], cancellation.Token));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Single(Directory.EnumerateFiles(directory.Path));
        await File.WriteAllTextAsync(path, new string(' ', PeerTrustStore.MaximumBytes + 1));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    internal static PeerTrust Trust(char pin) => new(new(1, "127.0.0.1", 1234, new string(pin, 64)), "Synthetic contact");
}
