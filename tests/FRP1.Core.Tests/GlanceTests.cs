using System.Text.Json;
using System.Text.Json.Nodes;
using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class GlanceTests
{
    [Fact]
    public void NewProfilesStartOocAndUnavailable()
    {
        var profile = ProfileTests.Valid();
        Assert.Equal(RpStatus.Ooc, profile.Status);
        Assert.Equal(RpAvailability.Unavailable, profile.Availability);
        Assert.Empty(profile.Glances);
        Assert.Empty(profile.RpHooks);
    }

    [Fact]
    public async Task GlancesStatusHooksAndMetadataRoundtripThroughJsonAndLocalStorage()
    {
        using var directory = new TestDirectory();
        var profile = RichProfile();
        var imported = ProfileJson.Import(ProfileJson.Export(profile));
        AssertRichFieldsEqual(profile, imported);
        Assert.Equal(ProfileJson.Hash(profile), ProfileJson.Hash(imported));
        await new ProfileStore(directory.Path).SaveLocalAsync(profile);
        var restored = await new ProfileStore(directory.Path).LoadLocalAsync(profile.Id);
        Assert.NotNull(restored);
        AssertRichFieldsEqual(profile, restored);
        Assert.Equal(ProfileJson.Hash(profile), ProfileJson.Hash(restored));
    }

    [Theory]
    [InlineData("summary", 512)]
    [InlineData("pronouns", 80)]
    [InlineData("oocInformation", 4096)]
    [InlineData("currentLocation", 256)]
    [InlineData("freeCompany", 160)]
    public void MetadataAcceptsDocumentedBoundaryAndRejectsLongerText(string property, int limit)
    {
        var json = JsonNode.Parse(ProfileJson.Export(ProfileTests.Valid()))!.AsObject();
        json[property] = new string('x', limit);
        Assert.NotNull(ProfileJson.Import(JsonSerializer.SerializeToUtf8Bytes(json)));
        json[property] = new string('x', limit + 1);
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(JsonSerializer.SerializeToUtf8Bytes(json)));
    }

    [Theory]
    [InlineData("summary", "null")]
    [InlineData("pronouns", "null")]
    [InlineData("oocInformation", "null")]
    [InlineData("currentLocation", "null")]
    [InlineData("freeCompany", "null")]
    [InlineData("glances", "null")]
    [InlineData("rpHooks", "null")]
    [InlineData("status", "999")]
    [InlineData("availability", "999")]
    [InlineData("glances", "[null]")]
    [InlineData("glances", "[{\"label\":null,\"text\":\"x\"}]")]
    [InlineData("glances", "[{\"label\":\"   \",\"text\":\"x\"}]")]
    [InlineData("glances", "[{\"label\":\"x\",\"text\":null}]")]
    [InlineData("rpHooks", "[null]")]
    [InlineData("rpHooks", "[{\"title\":null,\"text\":\"x\"}]")]
    [InlineData("rpHooks", "[{\"title\":\"  \",\"text\":\"x\"}]")]
    [InlineData("rpHooks", "[{\"title\":\"x\",\"text\":null}]")]
    public void MalformedGlancesHooksAndMetadataAreRejected(string property, string value)
    {
        var json = JsonNode.Parse(ProfileJson.Export(ProfileTests.Valid()))!.AsObject();
        json[property] = JsonNode.Parse(value);
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(JsonSerializer.SerializeToUtf8Bytes(json)));
    }

    [Fact]
    public void EntryLimitsAndTextBoundariesAreEnforced()
    {
        var profile = ProfileTests.Valid() with
        {
            Glances = Enumerable.Range(0, 5).Select(_ => new GlanceEntry(new string('L', 80), new string('T', 1024), 1_000_000)).ToList(),
            RpHooks = Enumerable.Range(0, 10).Select(_ => new RpHook(new string('H', 80), new string('T', 1024))).ToList(),
        };
        Assert.NotNull(ProfileJson.Import(ProfileJson.Export(profile)));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { Glances = [.. profile.Glances, new("Extra", "")] }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { RpHooks = [.. profile.RpHooks, new("Extra", "")] }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { Glances = [new(new string('L', 81), "")] }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { Glances = [new("Label", new string('T', 1025))] }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { Glances = [new("Label", "Text", 1_000_001)] }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { RpHooks = [new(new string('H', 81), "")] }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { RpHooks = [new("Title", new string('T', 1025))] }));
    }

    [Fact]
    public void FullProfileRetainsOverallByteLimit()
    {
        var profile = RichProfile() with
        {
            Appearance = new string('界', 8192),
            Personality = new string('x', 8192),
            Background = new string('x', 8192),
        };
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile));
    }

    [Fact]
    public void OrderAndPublicRoleplayChangesAffectHash()
    {
        var profile = RichProfile();
        var hash = ProfileJson.Hash(profile);
        Assert.NotEqual(hash, ProfileJson.Hash(profile with { Glances = profile.Glances.AsEnumerable().Reverse().ToList() }));
        Assert.NotEqual(hash, ProfileJson.Hash(profile with { RpHooks = profile.RpHooks.AsEnumerable().Reverse().ToList() }));
        Assert.NotEqual(hash, ProfileJson.Hash(profile with { Status = RpStatus.Afk }));
        Assert.NotEqual(hash, ProfileJson.Hash(profile with { Availability = RpAvailability.Busy }));
    }

    [Fact]
    public async Task UpdatedGlancesAndHooksSynchronizeAsOneVersionAndRespectMatureGate()
    {
        await using var pair = await NodePair.CreateAsync();
        var profile = RichProfile();
        pair.First.SetLocalProfile(profile);
        pair.Pair();
        pair.First.SetSharing(pair.SecondIdentity.Fingerprint, true);
        await using var session = await pair.First.ConnectAsync(pair.Second.CreateInvitation());
        await NetworkTests.UntilAsync(async () => await pair.SecondStore.GetCachedAsync(pair.FirstIdentity.Fingerprint, profile.Id) is not null);
        var cached = await pair.SecondStore.GetCachedAsync(pair.FirstIdentity.Fingerprint, profile.Id);
        AssertRichFieldsEqual(profile, cached!);
        var updated = profile with
        {
            Revision = 2,
            Status = RpStatus.Afk,
            Availability = RpAvailability.Busy,
            Glances = [new("Verändert", "Neue Beschreibung", 42)],
            RpHooks = [new("Neuer Anlass", "Neue Geschichte")],
            Summary = "Aktualisiert", Pronouns = "they/them", OocInformation = "Neue OOC-Info",
            CurrentLocation = "Ishgard", FreeCompany = "Andere Gemeinschaft",
        };
        pair.First.SetLocalProfile(updated);
        await NetworkTests.UntilAsync(async () => (await pair.SecondStore.GetCachedAsync(pair.FirstIdentity.Fingerprint, profile.Id))?.Revision == 2);
        AssertRichFieldsEqual(updated, (await pair.SecondStore.GetCachedAsync(pair.FirstIdentity.Fingerprint, profile.Id))!);
        var path = Path.Combine(pair.Directory.Path, "second", "cache", pair.FirstIdentity.Fingerprint, profile.Id.ToString("N") + ".json");
        var written = File.GetLastWriteTimeUtc(path);
        await Task.Delay(650);
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        pair.FirstStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Adult, true));
        pair.First.SetLocalProfile(updated with { Revision = 3, ContentRating = ContentRating.Mature18, Summary = "Mature synthetic glance" });
        await Task.Delay(650);
        Assert.Equal(2, (await pair.SecondStore.GetCachedAsync(pair.FirstIdentity.Fingerprint, profile.Id))!.Revision);
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(ProfileJson.Export(updated with { ContentRating = ContentRating.Mature18 }), pair.SecondStore.Policy));
    }

    private static ProfileDocument RichProfile() => ProfileTests.Valid() with
    {
        Status = RpStatus.Ic, Availability = RpAvailability.Open,
        Summary = "Kurzvorstellung mit äöü 灯", Pronouns = "sie/ihr", OocInformation = "Nur synthetische OOC-Info",
        CurrentLocation = "Gridania", FreeCompany = "Synthetische Gemeinschaft",
        Glances = [new("Kleidung", "Grüner Mantel", 123), new("Ausstrahlung", "Ruhig", 456)],
        RpHooks = [new("Heilkunst", "Interessiert an Kräutern"), new("Bibliothek", "Sucht Bücher")],
    };

    private static void AssertRichFieldsEqual(ProfileDocument expected, ProfileDocument actual)
    {
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Availability, actual.Availability);
        Assert.Equal(expected.Summary, actual.Summary);
        Assert.Equal(expected.Pronouns, actual.Pronouns);
        Assert.Equal(expected.OocInformation, actual.OocInformation);
        Assert.Equal(expected.CurrentLocation, actual.CurrentLocation);
        Assert.Equal(expected.FreeCompany, actual.FreeCompany);
        Assert.Equal(expected.Glances, actual.Glances);
        Assert.Equal(expected.RpHooks, actual.RpHooks);
    }
}
