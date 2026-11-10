using System.Text.Json.Nodes;
using FRP1.Plugin.GameIntegration;
using FRP1.Plugin.UiSocial;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class SocialDisplaySettingsTests
{
    [Fact]
    public async Task SocialOptionsPersistLocalDisplayChoicesAndCancelledSaveKeepsPreviousState()
    {
        using var directory = new TestDirectory();
        var store = new SocialUiOptionsStore(directory.Path);
        var defaults = await store.LoadAsync();
        Assert.True(defaults.OnlyKnownEnvironment);
        Assert.True(defaults.MouseoverTooltip);
        await store.SaveAsync(defaults with { MouseoverTooltip = false, TargetVisible = false, TargetLocked = true, TargetX = -50 });
        var restored = await new SocialUiOptionsStore(directory.Path).LoadAsync();
        Assert.False(restored.MouseoverTooltip);
        Assert.False(restored.TargetVisible);
        Assert.True(restored.TargetLocked);
        Assert.Equal(-50, restored.TargetX);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(defaults, cancellation.Token));
        Assert.Equal(restored, await store.LoadAsync());
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("mouseoverTooltip")]
    [InlineData("targetVisible")]
    [InlineData("targetLocked")]
    [InlineData("onlyKnownEnvironment")]
    [InlineData("targetX")]
    [InlineData("targetY")]
    public async Task SocialFieldsAreRequired(string field)
    {
        using var directory = new TestDirectory();
        var store = new SocialUiOptionsStore(directory.Path);
        await store.SaveAsync(new());
        var path = Path.Combine(directory.Path, "social-ui.json");
        var json = JsonNode.Parse(await File.ReadAllBytesAsync(path))!.AsObject();
        json.Remove(field);
        await File.WriteAllTextAsync(path, json.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Fact]
    public async Task InvalidSocialPositionsAndOversizedOrDuplicateRecordsAreRejected()
    {
        using var directory = new TestDirectory();
        var store = new SocialUiOptionsStore(directory.Path);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new() { TargetX = float.NaN }));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new() { TargetY = float.PositiveInfinity }));
        await store.SaveAsync(new());
        var path = Path.Combine(directory.Path, "social-ui.json");
        var valid = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, valid[..^1] + ",\"targetX\":0}");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
        await File.WriteAllTextAsync(path, valid.PadRight(1025));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Fact]
    public async Task GameRewritesStartOffAndExplicitOptInPersistsSeparately()
    {
        using var directory = new TestDirectory();
        var store = new GamePresentationOptionsStore(directory.Path);
        var defaults = await store.LoadAsync();
        Assert.False(defaults.Nameplates || defaults.ChatNames || defaults.Titles || defaults.ProfileColors || defaults.ProfileIcons);
        await store.SaveAsync(defaults with { Nameplates = true });
        var restored = await new GamePresentationOptionsStore(directory.Path).LoadAsync();
        Assert.True(restored.Nameplates);
        Assert.False(restored.ChatNames || restored.Titles || restored.ProfileColors);
    }

    [Fact]
    public async Task EarlierGameSettingsWithoutProfileIconsStayReadableAndDoNotOptIn()
    {
        using var directory = new TestDirectory();
        var store = new GamePresentationOptionsStore(directory.Path);
        await store.SaveAsync(new() { Nameplates = true, Titles = true, ProfileIcons = true });
        var path = Path.Combine(directory.Path, "game-presentation.json");
        var json = JsonNode.Parse(await File.ReadAllBytesAsync(path))!.AsObject();
        json.Remove("profileIcons");
        var earlier = json.ToJsonString();
        await File.WriteAllTextAsync(path, earlier);
        var loaded = await store.LoadAsync();
        Assert.True(loaded.Nameplates);
        Assert.True(loaded.Titles);
        Assert.False(loaded.ProfileIcons);
        Assert.Equal(earlier, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ProfileIconOptInPersistsWithoutImplicitlyEnablingOtherGameChanges()
    {
        using var directory = new TestDirectory();
        await new GamePresentationOptionsStore(directory.Path).SaveAsync(new() { ProfileIcons = true });
        var loaded = await new GamePresentationOptionsStore(directory.Path).LoadAsync();
        Assert.True(loaded.ProfileIcons);
        Assert.False(loaded.Nameplates || loaded.ChatNames || loaded.Titles || loaded.ProfileColors);
    }
}
