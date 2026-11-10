using System.Text;
using System.Text.Json.Nodes;
using FRP1.Core.Networking;
using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FRP1.Core.Tests;

public sealed class ProfileTests
{
    [Fact]
    public void PublicProfileRoundtripPreservesUnicodeAndAllPublicFields()
    {
        var profile = new ProfileDocument
        {
            DisplayName = "Äther 灯", FirstName = "Vorname", LastName = "Nachname", Title = "Titel",
            Race = "Volk", Clan = "Clan", Age = "29", Gender = "weiblich", Origin = "Gridania",
            Occupation = "Heilerin", Appearance = "Augen\nHaare", Personality = "freundlich", Background = "Geschichte",
            ExtraFields = new() { ["Sprache"] = "Deutsch" },
        };
        var imported = ProfileJson.Import(ProfileJson.Export(profile));
        Assert.Equal(ProfileJson.Hash(profile), ProfileJson.Hash(imported));
        Assert.Equal(profile.ExtraFields, imported.ExtraFields);
        var publicJson = Encoding.UTF8.GetString(ProfileJson.Export(profile));
        Assert.DoesNotContain("contentId", publicJson);
        Assert.DoesNotContain("homeWorldId", publicJson);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("id")]
    [InlineData("revision")]
    [InlineData("displayName")]
    public void MissingRequiredMetadataIsRejected(string property)
    {
        var json = JsonNode.Parse(ProfileJson.Export(Valid()))!.AsObject();
        json.Remove(property);
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("")]
    public void MalformedOrIncompleteProfilesAreRejected(string json) =>
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(Encoding.UTF8.GetBytes(json)));

    [Theory]
    [InlineData("schemaVersion", "2")]
    [InlineData("revision", "0")]
    [InlineData("displayName", "null")]
    [InlineData("extraFields", "null")]
    [InlineData("background", "null")]
    [InlineData("id", "\"00000000-0000-0000-0000-000000000000\"")]
    public void InvalidMetadataAndNullFieldsAreRejected(string property, string value)
    {
        var json = JsonNode.Parse(ProfileJson.Export(Valid()))!.AsObject();
        json[property] = JsonNode.Parse(value);
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Fact]
    public void UnknownAndDuplicateJsonFieldsAreRejected()
    {
        var json = Encoding.UTF8.GetString(ProfileJson.Export(Valid()));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(Encoding.UTF8.GetBytes(json[..^1] + ",\"secret\":\"x\"}")));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(Encoding.UTF8.GetBytes(json[..^1] + ",\"revision\":2}")));
    }

    [Fact]
    public void SizeAndTextLimitsAreEnforced()
    {
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(new byte[ProfileJson.MaximumBytes + 1]));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(Valid() with { DisplayName = new string('x', 161) }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(Valid() with { Background = new string('x', 8193) }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(Valid() with { Title = "a\0b" }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(Valid() with { DisplayName = "  " }));
        var tooManyFields = new SortedDictionary<string, string>();
        for (var i = 0; i < 25; i++) tooManyFields.Add(i.ToString(), "value");
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(Valid() with { ExtraFields = tooManyFields }));
    }

    [Fact]
    public void HashIsIndependentOfAdditionalFieldComparer()
    {
        var profile = Valid() with { ExtraFields = new(StringComparer.Ordinal) { ["a"] = "1", ["Z"] = "2" } };
        var differentlySorted = profile with { ExtraFields = new(StringComparer.OrdinalIgnoreCase) { ["Z"] = "2", ["a"] = "1" } };
        Assert.Equal(ProfileJson.Hash(profile), ProfileJson.Hash(differentlySorted));
        Assert.NotEqual(ProfileJson.Hash(profile), ProfileJson.Hash(profile with { Revision = 2 }));
    }

    [Theory]
    [InlineData("127.0.0.1", 1234)]
    [InlineData("::1", 65535)]
    public void InvitationRoundtripHasOnlyPublicIdentity(string address, int port)
    {
        var invitation = new PeerInvitation(1, address, port, new string('A', 64));
        Assert.Equal(invitation, PeerInvitation.Parse(invitation.Encode()));
    }

    [Theory]
    [InlineData("example.com", 1)]
    [InlineData("0.0.0.0", 1)]
    [InlineData("::", 1)]
    [InlineData("224.0.0.1", 1)]
    [InlineData("ff02::1", 1)]
    [InlineData("127.0.0.1", 0)]
    [InlineData("127.0.0.1", 65536)]
    public void InvalidInvitationEndpointsAreRejected(string address, int port) =>
        Assert.Throws<InvalidDataException>(() => new PeerInvitation(1, address, port, new string('A', 64)).Encode());

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("frp1:notbase64")]
    [InlineData("frp1:e30=")]
    public void InvalidInvitationCodesAreRejected(string code) =>
        Assert.Throws<InvalidDataException>(() => PeerInvitation.Parse(code));

    [Fact]
    public async Task LocalAndCachedProfilesSurviveStoreRecreation()
    {
        using var directory = new TestDirectory();
        var store = new ProfileStore(directory.Path);
        var profile = Valid();
        var pin = new string('A', 64);
        await store.SaveLocalAsync(profile);
        await store.SaveCachedAsync(pin, profile);
        var reopened = new ProfileStore(directory.Path);
        Assert.Equal(ProfileJson.Hash(profile), ProfileJson.Hash((await reopened.LoadLocalAsync(profile.Id))!));
        Assert.Single(await reopened.ListLocalAsync());
        Assert.Equal(ProfileJson.Hash(profile), ProfileJson.Hash((await reopened.GetCachedAsync(pin.ToLowerInvariant(), profile.Id))!));
        Assert.Null(await reopened.LoadLocalAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CacheRejectsRollbacksConflictsAndPathInjection()
    {
        using var directory = new TestDirectory();
        var store = new ProfileStore(directory.Path);
        var profile = Valid() with { Revision = 2 };
        var pin = new string('B', 64);
        await store.SaveCachedAsync(pin, profile);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveCachedAsync(pin, profile with { Revision = 1 }));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveCachedAsync(pin, profile with { Title = "Conflicting content" }));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveCachedAsync("../../outside", profile));
        await store.SaveCachedAsync(pin, profile with { Revision = 3, Title = "Updated" });
        Assert.Equal(3, (await store.GetCachedAsync(pin, profile.Id))!.Revision);
    }

    [Fact]
    public async Task CacheLimitsNewProfilesAndPeersWhileAllowingExistingUpdates()
    {
        using var directory = new TestDirectory();
        var store = new ProfileStore(directory.Path);
        var pin = new string('C', 64);
        var first = Valid();
        await store.SaveCachedAsync(pin, first);
        for (var i = 1; i < 32; i++) await store.SaveCachedAsync(pin, Valid());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveCachedAsync(pin, Valid()));
        await store.SaveCachedAsync(pin, first with { Revision = 2 });
        for (var i = 1; i < 16; i++) await store.SaveCachedAsync(i.ToString("X64"), Valid());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveCachedAsync(new string('D', 64), Valid()));
    }

    [Fact]
    public async Task OversizedStoredProfileIsRejectedBeforeRead()
    {
        using var directory = new TestDirectory();
        var profile = Valid();
        var store = new ProfileStore(directory.Path);
        await store.SaveLocalAsync(profile);
        await File.WriteAllBytesAsync(System.IO.Path.Combine(directory.Path, "profiles", profile.Id.ToString("N") + ".json"), new byte[ProfileJson.MaximumBytes + 1]);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadLocalAsync(profile.Id));
    }

    internal static ProfileDocument Valid() => new() { DisplayName = "Synthetic test profile" };
}

internal sealed class TestDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
    public TestDirectory() => Directory.CreateDirectory(Path);
    public void Dispose()
    {
        var resolved = System.IO.Path.GetFullPath(Path);
        var expected = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "TestData")) + System.IO.Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test path.");
        Directory.Delete(resolved, true);
    }
}
