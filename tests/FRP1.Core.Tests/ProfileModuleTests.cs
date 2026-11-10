using System.Text.Json;
using System.Text.Json.Nodes;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Profiles;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class ProfileModuleTests
{
    [Fact]
    public void EarlierSchemaOneProfilesRemainReadableWithSafeModuleDefaults()
    {
        var source = ProfileTests.Valid();
        var json = new JsonObject
        {
            ["schemaVersion"] = 1, ["id"] = source.Id.ToString(), ["revision"] = 1,
            ["displayName"] = source.DisplayName, ["contentRating"] = 0,
            ["glances"] = new JsonArray(new JsonObject { ["label"] = "Earlier glance", ["text"] = "Text" }),
        };
        var profile = ProfileJson.Import(JsonSerializer.SerializeToUtf8Bytes(json));
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Empty(profile.RpLanguages);
        Assert.Empty(profile.PersonalityTraits);
        Assert.Empty(profile.Companions);
        Assert.Null(profile.Portrait);
        Assert.Equal(PreferenceAnswer.Unspecified, profile.Preferences.Romance);
        Assert.True(Assert.Single(profile.Glances).Enabled);
    }

    [Fact]
    public void ModulesRoundtripWithoutBecomingAnAgeOrConsentGrant()
    {
        var profile = Expanded();
        var imported = ProfileJson.Import(ProfileJson.Export(profile));
        Assert.Equal(ProfileJson.Hash(profile), ProfileJson.Hash(imported));
        Assert.Equal(profile.RpLanguages, imported.RpLanguages);
        Assert.Equal(profile.PersonalityTraits, imported.PersonalityTraits);
        Assert.Equal(profile.Companions, imported.Companions);
        Assert.Equal(profile.Preferences, imported.Preferences);
        Assert.Equal(profile.AppearanceStyle, imported.AppearanceStyle);
        var policy = new AudiencePolicy(new AudiencePreferences(AudienceSelection.Under18));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(ProfileJson.Export(imported with { ContentRating = ContentRating.Mature18 }), policy));
    }

    [Theory]
    [InlineData("rpLanguages", "null")]
    [InlineData("rpLanguages", "[null]")]
    [InlineData("rpLanguages", "[\"  \"]")]
    [InlineData("personalityTraits", "null")]
    [InlineData("personalityTraits", "[null]")]
    [InlineData("personalityTraits", "[{\"leftLabel\":\"a\",\"rightLabel\":\"b\",\"value\":101}]")]
    [InlineData("personalityTraits", "[{\"leftLabel\":\"a\",\"rightLabel\":\"b\",\"value\":-1}]")]
    [InlineData("preferences", "null")]
    [InlineData("preferences", "{\"experience\":999}")]
    [InlineData("preferences", "{\"romance\":999}")]
    [InlineData("appearanceStyle", "null")]
    [InlineData("appearanceStyle", "{\"layout\":999}")]
    [InlineData("appearanceStyle", "{\"nameColor\":\"red\"}")]
    [InlineData("appearanceStyle", "{\"accentColor\":\"#GG0000\"}")]
    [InlineData("appearanceStyle", "{\"nameColor\":\"#00000000\"}")]
    [InlineData("companions", "null")]
    [InlineData("companions", "[null]")]
    public void MalformedModulesAreRejected(string property, string value)
    {
        var json = JsonNode.Parse(ProfileJson.Export(ProfileTests.Valid()))!.AsObject();
        json[property] = JsonNode.Parse(value);
        Assert.Throws<InvalidDataException>(() => ProfileJson.Import(JsonSerializer.SerializeToUtf8Bytes(json)));
    }

    [Fact]
    public void CollectionLimitsTraitEndpointsAndUniqueCompanionIdsAreEnforced()
    {
        var profile = ProfileTests.Valid() with
        {
            RpLanguages = Enumerable.Range(0, 12).Select(i => "Language " + i).ToList(),
            PersonalityTraits = Enumerable.Range(0, 8).Select(i => new PersonalityTrait("Left", "Right", i % 2 == 0 ? 0 : 100)).ToList(),
            Companions = Enumerable.Range(0, 8).Select(i => new CompanionProfile { Name = "Companion " + i }).ToList(),
        };
        Assert.NotNull(ProfileJson.Import(ProfileJson.Export(profile)));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { RpLanguages = [.. profile.RpLanguages, "Extra"] }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { PersonalityTraits = [.. profile.PersonalityTraits, new("L", "R")] }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { Companions = [.. profile.Companions, new() { Name = "Extra" }] }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { Companions = [profile.Companions[0], profile.Companions[0]] }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { Companions = [new() { Id = Guid.Empty, Name = "Invalid" }] }));
        Assert.Throws<InvalidDataException>(() => ProfileJson.Export(profile with { Companions = [new() { Name = "Invalid", Kind = (CompanionKind)999 }] }));
    }

    [Fact]
    public void ReaderFiltersDisabledGlancesAndStandardViewRemovesCustomColors()
    {
        var policy = Adult();
        var profile = Expanded() with
        {
            Background = "# History\n[b]Bold[/b] [color=#AABBCC]Tinted[/color]",
            Glances = [new("Shown", "Text"), new("Hidden", "Must not display", Enabled: false)],
        };
        var normal = new ProfileReader(profile, policy).Read();
        Assert.Equal("Shown", Assert.Single(normal.Glances).Label);
        Assert.Contains(normal.Sections.SelectMany(s => s.Blocks).SelectMany(b => b.Runs), run => run.Color == "#AABBCC");
        var standard = new ProfileReader(profile, policy, standardPresentation: true).Read();
        Assert.Equal("", standard.Appearance.NameColor);
        Assert.Equal("", standard.Appearance.AccentColor);
        Assert.Equal(AboutLayout.Sections, standard.Appearance.Layout);
        Assert.All(standard.Sections.SelectMany(s => s.Blocks).SelectMany(b => b.Runs), run => Assert.Equal("", run.Color));
    }

    [Fact]
    public void ReaderRechecksAudienceOnEveryReadAndDefensivelyCopiesPortraitBytes()
    {
        var policy = Adult();
        var source = Expanded() with { ContentRating = ContentRating.Mature18, Portrait = new("image/png", PngFixture.Create()) };
        var reader = new ProfileReader(source, policy);
        var first = reader.Read();
        first.Portrait!.Data[0] = 0;
        source.Portrait!.Data[1] = 0;
        Assert.Equal(137, reader.Read().Portrait!.Data[0]);
        Assert.Equal(80, reader.Read().Portrait!.Data[1]);
        policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18));
        Assert.Throws<InvalidDataException>(() => reader.Read());
        Assert.False(reader.TryRead(out var restricted));
        Assert.Null(restricted);
        policy.SetAudience(new AudiencePreferences());
        Assert.Throws<InvalidDataException>(() => new ProfileReader(ProfileTests.Valid(), policy));
    }

    [Fact]
    public void GlanceTemplatesNeverDowngradeMatureContentAndRemainDetached()
    {
        var policy = Adult();
        var source = Expanded() with { ContentRating = ContentRating.Mature18, Glances = [new("Template", "Synthetic")] };
        var template = GlanceTemplates.Copy(source, policy);
        source.Glances.Clear();
        Assert.Single(template.Entries);
        Assert.Throws<InvalidDataException>(() => GlanceTemplates.Apply(ProfileTests.Valid(), template, policy));
        var applied = GlanceTemplates.Apply(ProfileTests.Valid() with { ContentRating = ContentRating.Mature18 }, template, policy);
        applied.Glances.Clear();
        Assert.Single(template.Entries);
        policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18));
        Assert.Throws<InvalidDataException>(() => GlanceTemplates.Copy(source, policy));
        Assert.Throws<InvalidDataException>(() => GlanceTemplates.Apply(applied, template, policy));
    }

    [Fact]
    public void RichTextSupportsBoundedFormattingButNeverCreatesActiveLinks()
    {
        var blocks = SafeRichText.Parse("# Heading\n- [b]Bold[/b] and [i]italic[/i]\n[url=https://example.invalid]Link[/url]");
        Assert.Equal(RichBlockKind.Heading, blocks[0].Kind);
        Assert.Equal(RichBlockKind.Bullet, blocks[1].Kind);
        Assert.Contains(blocks[1].Runs, run => run.Bold && run.Text == "Bold");
        Assert.Contains(blocks[1].Runs, run => run.Italic && run.Text == "italic");
        Assert.Contains("[url=https://example.invalid]Link[/url]", SafeRichText.PlainText(blocks));
        Assert.Throws<InvalidDataException>(() => SafeRichText.Parse(new string('x', SafeRichText.MaximumCharacters + 1)));
    }

    [Theory]
    [InlineData("[b]unclosed")]
    [InlineData("[b][i]wrong close[/b][/i]")]
    [InlineData("[b][b][b][b][b]too deep[/b][/b][/b][/b][/b]")]
    public void InvalidOrExcessivelyNestedFormattingFallsBackToLiteralText(string text)
    {
        var parsed = SafeRichText.Parse(text);
        Assert.Equal(text, SafeRichText.PlainText(parsed));
        Assert.All(parsed.SelectMany(block => block.Runs), run => Assert.False(run.Bold || run.Italic || run.Color.Length > 0));
    }

    [Fact]
    public void RichTextTokenOverflowUsesOneBoundedPlainFallback()
    {
        var text = string.Concat(Enumerable.Repeat("[b]x[/b]", 500));
        var blocks = SafeRichText.Parse(text);
        Assert.Single(blocks);
        Assert.Single(blocks[0].Runs);
        Assert.Equal(text, SafeRichText.PlainText(blocks));
    }

    internal static AudiencePolicy Adult() => new(new AudiencePreferences(AudienceSelection.Adult, true));
    internal static ProfileDocument Expanded() => ProfileTests.Valid() with
    {
        EyeColor = "Green", Height = "Tall", Weight = "Unknown", Birthplace = "Gridania", Residence = "Ishgard",
        RpLanguages = ["Deutsch", "English"], PersonalityTraits = [new("Quiet", "Talkative", 25)],
        Preferences = new() { Experience = RpExperience.Experienced, PreferredStyle = "Adventure", Injury = PreferenceAnswer.AskFirst, Romance = PreferenceAnswer.Yes },
        AppearanceStyle = new() { IconId = 123, NameColor = "#AABBCC", AccentColor = "#123456", Layout = AboutLayout.Chronicle },
        Companions = [new() { Name = "Synthetic chocobo", Kind = CompanionKind.Mount, Species = "Chocobo", Description = "Test only" }],
    };
}
