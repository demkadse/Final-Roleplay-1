using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Theming;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class ThemeTests
{
    [Fact]
    public void CatalogContainsEightRaceThemesAndThreeNeutralThemesAndCannotBeModified()
    {
        Assert.Equal(11, ThemeCatalog.All.Count);
        Assert.Equal(11, ThemeCatalog.All.Select(theme => theme.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Enumerable.Range(1, 8).Select(value => (uint?)value), ThemeCatalog.All.Where(theme => theme.RaceId.HasValue).Select(theme => theme.RaceId).Order());
        Assert.Equal(new[] { "mondlicht", "obsidian", "pergament" }, ThemeCatalog.All.Where(theme => !theme.RaceId.HasValue).Select(theme => theme.Id).Order());
        var collection = Assert.IsAssignableFrom<ICollection<ThemeDefinition>>(ThemeCatalog.All);
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
        Assert.All(ThemeCatalog.All, theme => Assert.False(string.IsNullOrWhiteSpace(theme.Name)));
    }

    [Theory]
    [InlineData(1u)] [InlineData(2u)] [InlineData(3u)] [InlineData(4u)]
    [InlineData(5u)] [InlineData(6u)] [InlineData(7u)] [InlineData(8u)]
    public void AutomaticModeUsesLocalRaceWithoutChangingManualPreference(uint race)
    {
        var preferences = new ThemePreferences(ManualThemeId: "pergament", ReduceDecoration: true);
        Assert.Equal(race, ThemeCatalog.Resolve(preferences, race).RaceId);
        Assert.Equal("pergament", preferences.ManualThemeId);
        Assert.Equal(ThemeSelectionMode.Automatic, preferences.Mode);
        Assert.True(preferences.ReduceDecoration);
    }

    [Theory]
    [InlineData(null)] [InlineData(0u)] [InlineData(9u)] [InlineData(uint.MaxValue)]
    public void AutomaticModeFallsBackToObsidianForMissingOrUnknownRace(uint? race) =>
        Assert.Equal("obsidian", ThemeCatalog.Resolve(new ThemePreferences(), race).Id);

    [Fact]
    public void AllThemesCanBeSelectedManuallyAndRaceChangesNeverOverrideThem()
    {
        foreach (var theme in ThemeCatalog.All)
        {
            var preferences = new ThemePreferences(ThemeSelectionMode.Manual, theme.Id, true);
            foreach (var race in new uint?[] { null, 0, 1, 4, 8, uint.MaxValue })
                Assert.Equal(theme.Id, ThemeCatalog.Resolve(preferences, race).Id);
        }
        var manual = new ThemePreferences(ThemeSelectionMode.Manual, "pergament");
        Assert.Equal("pergament", ThemeCatalog.Resolve(manual, 4).Id);
        Assert.Equal(4u, ThemeCatalog.Resolve(manual with { Mode = ThemeSelectionMode.Automatic }, 4).RaceId);
        Assert.Equal("pergament", ThemeCatalog.Resolve(manual, 8).Id);
    }

    [Fact]
    public void PalettesHaveFiniteOpaqueTokensAndReadableTextOnEveryUsedSurface()
    {
        foreach (var theme in ThemeCatalog.All)
        {
            var palette = theme.Palette;
            var tokens = new[] { palette.Background, palette.Panel, palette.Text, palette.MutedText, palette.Accent,
                palette.Border, palette.Focus, palette.Input, palette.Button, palette.Hover, palette.Selected };
            foreach (var token in tokens)
            {
                Assert.True(float.IsFinite(token.X) && float.IsFinite(token.Y) && float.IsFinite(token.Z) && float.IsFinite(token.W), theme.Id);
                Assert.InRange(token.X, 0, 1);
                Assert.InRange(token.Y, 0, 1);
                Assert.InRange(token.Z, 0, 1);
                Assert.Equal(1f, token.W);
            }
            Assert.True(float.IsFinite(theme.Rounding) && theme.Rounding >= 0, theme.Id);
            foreach (var surface in new[] { palette.Background, palette.Panel, palette.Input, palette.Button, palette.Hover, palette.Selected })
            {
                Assert.True(Contrast(palette.Text, surface) >= 4.5, $"{theme.Id}: text contrast {Contrast(palette.Text, surface):F3}");
                Assert.True(Contrast(palette.MutedText, surface) >= 4.5, $"{theme.Id}: secondary text contrast {Contrast(palette.MutedText, surface):F3}");
            }
        }
        Assert.True(Luminance(ThemeCatalog.Get("pergament").Palette.Background) > 0.5);
    }

    [Fact]
    public async Task MissingCharacterSettingsDefaultToAutomaticAndDoNotWriteAnything()
    {
        using var directory = new TestDirectory();
        var preferences = await new ThemeSettingsStore(directory.Path).LoadAsync(123);
        Assert.Equal(new ThemePreferences(), preferences);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
    }

    [Fact]
    public async Task ManualSettingsReloadPerCharacterAndRaceResolutionDoesNotOverwriteFile()
    {
        using var directory = new TestDirectory();
        var store = new ThemeSettingsStore(directory.Path);
        var first = new ThemePreferences(ThemeSelectionMode.Manual, "pergament", true);
        var second = new ThemePreferences(ThemeSelectionMode.Automatic, "mondlicht", false);
        await store.SaveAsync(1, first);
        await store.SaveAsync(ulong.MaxValue, second);
        var reopened = new ThemeSettingsStore(directory.Path);
        Assert.Equal(first, await reopened.LoadAsync(1));
        Assert.Equal(second, await reopened.LoadAsync(ulong.MaxValue));
        Assert.Equal(new ThemePreferences(), await reopened.LoadAsync(2));
        var firstPath = Path.Combine(directory.Path, "themes", "0000000000000001.json");
        var secondPath = Path.Combine(directory.Path, "themes", "FFFFFFFFFFFFFFFF.json");
        Assert.True(File.Exists(secondPath));
        var before = await File.ReadAllBytesAsync(firstPath);
        Assert.Equal("pergament", ThemeCatalog.Resolve(await reopened.LoadAsync(1), 1).Id);
        Assert.Equal("pergament", ThemeCatalog.Resolve(await reopened.LoadAsync(1), 8).Id);
        Assert.Equal(before, await File.ReadAllBytesAsync(firstPath));
        using var json = JsonDocument.Parse(before);
        Assert.Equal(new[] { "manualThemeId", "mode", "reduceDecoration", "schemaVersion" }, json.RootElement.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task ZeroCharacterIdCannotCreateOrLoadSettings()
    {
        using var directory = new TestDirectory();
        var store = new ThemeSettingsStore(directory.Path);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.LoadAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.SaveAsync(0, new ThemePreferences()));
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
    }

    [Theory]
    [InlineData("schemaVersion")] [InlineData("mode")]
    [InlineData("manualThemeId")] [InlineData("reduceDecoration")]
    public async Task AllStoredFieldsAreRequired(string property)
    {
        using var directory = new TestDirectory();
        var json = ValidRecord();
        json.Remove(property);
        await WriteRecordAsync(directory.Path, json.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => new ThemeSettingsStore(directory.Path).LoadAsync(1));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("{\"schemaVersion\":1,\"mode\":0,\"manualThemeId\":\"obsidian\",\"reduceDecoration\":false,\"unknown\":1}")]
    [InlineData("{\"schemaVersion\":1,\"mode\":0,\"mode\":0,\"manualThemeId\":\"obsidian\",\"reduceDecoration\":false}")]
    public async Task MalformedOrUnexpectedSettingsAreRejected(string content)
    {
        using var directory = new TestDirectory();
        await WriteRecordAsync(directory.Path, content);
        await Assert.ThrowsAsync<InvalidDataException>(() => new ThemeSettingsStore(directory.Path).LoadAsync(1));
    }

    [Theory]
    [InlineData("schemaVersion", "0")]
    [InlineData("schemaVersion", "2")]
    [InlineData("schemaVersion", "null")]
    [InlineData("mode", "999")]
    [InlineData("mode", "null")]
    [InlineData("manualThemeId", "null")]
    [InlineData("manualThemeId", "\"\"")]
    [InlineData("manualThemeId", "\"unknown\"")]
    [InlineData("manualThemeId", "\"../../outside\"")]
    [InlineData("reduceDecoration", "null")]
    [InlineData("reduceDecoration", "\"false\"")]
    public async Task UnsupportedOrInvalidSettingsAreRejected(string property, string value)
    {
        using var directory = new TestDirectory();
        var json = ValidRecord();
        json[property] = JsonNode.Parse(value);
        await WriteRecordAsync(directory.Path, json.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => new ThemeSettingsStore(directory.Path).LoadAsync(1));
    }

    [Fact]
    public async Task StoredByteLimitAcceptsBoundaryButRejectsLargerValidJson()
    {
        using var directory = new TestDirectory();
        var json = ValidRecord().ToJsonString();
        var boundary = json.PadRight(ThemeSettingsStore.MaximumBytes, ' ');
        await WriteRecordAsync(directory.Path, boundary);
        Assert.Equal(new ThemePreferences(), await new ThemeSettingsStore(directory.Path).LoadAsync(1));
        await WriteRecordAsync(directory.Path, boundary + " ");
        await Assert.ThrowsAsync<InvalidDataException>(() => new ThemeSettingsStore(directory.Path).LoadAsync(1));
    }

    [Fact]
    public async Task CancelledWritesPreservePreviousSettingsAndLeaveNoTemporaryFiles()
    {
        using var directory = new TestDirectory();
        var store = new ThemeSettingsStore(directory.Path);
        var original = new ThemePreferences(ThemeSelectionMode.Manual, "pergament", true);
        await store.SaveAsync(1, original);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(1, new ThemePreferences(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(2, new ThemePreferences(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.LoadAsync(1, cancellation.Token));
        Assert.Equal(original, await store.LoadAsync(1));
        Assert.Equal(new ThemePreferences(), await store.LoadAsync(2));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(directory.Path, "themes")));
    }

    [Fact]
    public async Task ConcurrentChangesPublishOneCompleteChoiceWithoutLeavingTemporaryFiles()
    {
        using var directory = new TestDirectory();
        var store = new ThemeSettingsStore(directory.Path);
        var choices = ThemeCatalog.All.Select(theme => new ThemePreferences(ThemeSelectionMode.Manual, theme.Id, true)).ToArray();
        await Task.WhenAll(choices.Select(choice => store.SaveAsync(1, choice)));
        var restored = await new ThemeSettingsStore(directory.Path).LoadAsync(1);
        Assert.Contains(restored, choices);
        Assert.Single(Directory.EnumerateFiles(Path.Combine(directory.Path, "themes")));
    }

    [Fact]
    public async Task LocalThemesCannotChangePublicProfileHashOrAgeAuthorization()
    {
        using var directory = new TestDirectory();
        var profile = ProfileTests.Valid();
        var publicJson = ProfileJson.Export(profile);
        var policy = new AudiencePolicy();
        await new ThemeSettingsStore(directory.Path).SaveAsync(123, new ThemePreferences(ThemeSelectionMode.Manual, "pergament", true));
        var preferences = await new ThemeSettingsStore(directory.Path).LoadAsync(123);
        _ = ThemeCatalog.Resolve(preferences, 4);
        Assert.Equal(publicJson, ProfileJson.Export(profile));
        Assert.False(policy.AcceptsMatureContent);
        Assert.Equal(AudienceSelection.Unspecified, policy.Snapshot.Selection);
    }

    private static JsonObject ValidRecord() => new()
    {
        ["schemaVersion"] = 1, ["mode"] = 0, ["manualThemeId"] = "obsidian", ["reduceDecoration"] = false,
    };

    private static async Task WriteRecordAsync(string root, string content)
    {
        var directory = Path.Combine(root, "themes");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "0000000000000001.json"), content);
    }

    private static double Contrast(Vector4 first, Vector4 second)
    {
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double Luminance(Vector4 color)
    {
        static double Linear(double value) => value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        return 0.2126 * Linear(color.X) + 0.7152 * Linear(color.Y) + 0.0722 * Linear(color.Z);
    }
}
