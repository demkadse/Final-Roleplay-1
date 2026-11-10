using FRP1.Core.Safety;
using FRP1.Core.Profiles;
using FRP1.Plugin;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class AudienceSettingsTests
{
    [Fact]
    public async Task NoPreferencesFileMeansNoAgeChoiceAndNoMatureOptIn()
    {
        using var directory = new TestDirectory();
        var preferences = await new AudienceSettings(directory.Path).LoadAsync(CancellationToken.None);
        Assert.Equal(new AudiencePreferences(), preferences);
        Assert.Equal(new AudiencePreferences(), await new AudienceSettings(directory.Path).LoadForSessionAsync(CancellationToken.None));
        Assert.Empty(Directory.EnumerateFiles(directory.Path));
    }

    [Theory]
    [InlineData(AudienceSelection.Unspecified, false)]
    [InlineData(AudienceSelection.Under18, false)]
    [InlineData(AudienceSelection.Adult, false)]
    [InlineData(AudienceSelection.Adult, true)]
    public async Task ExplicitPreferencesPersistLocallyAndReload(AudienceSelection selection, bool optIn)
    {
        using var directory = new TestDirectory();
        var preferences = new AudiencePreferences(selection, optIn);
        await new AudienceSettings(directory.Path).SaveAsync(preferences, CancellationToken.None);
        Assert.Equal(preferences, await new AudienceSettings(directory.Path).LoadAsync(CancellationToken.None));
        var file = Assert.Single(Directory.EnumerateFiles(directory.Path));
        Assert.Equal("audience.json", Path.GetFileName(file));
        var json = await File.ReadAllTextAsync(file);
        Assert.DoesNotContain("Birth", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Account", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"Selection\":\"UnknownValue\"}")]
    [InlineData("{\"Selection\":2,\"MatureContentOptIn\":true}")]
    [InlineData("{\"Selection\":\"Under18\",\"MatureContentOptIn\":true}")]
    [InlineData("{\"Selection\":\"Unspecified\",\"MatureContentOptIn\":true}")]
    [InlineData("{\"Selection\":\"Adult\",\"unexpected\":true}")]
    [InlineData("{\"Selection\":\"Adult\",\"Selection\":\"Under18\"}")]
    public async Task MalformedOrInconsistentPreferencesAreRejected(string json)
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "audience.json"), json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new AudienceSettings(directory.Path).LoadAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => new AudienceSettings(directory.Path).LoadForSessionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OversizedPreferencesAreRejected()
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "audience.json"), new string(' ', 1025));
        await Assert.ThrowsAsync<InvalidDataException>(() => new AudienceSettings(directory.Path).LoadAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => new AudienceSettings(directory.Path).LoadForSessionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CancelledSavePreservesPreviousChoiceAndLeavesNoTemporaryFile()
    {
        using var directory = new TestDirectory();
        var settings = new AudienceSettings(directory.Path);
        var original = new AudiencePreferences(AudienceSelection.Under18, false);
        await settings.SaveAsync(original, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => settings.SaveAsync(new AudiencePreferences(AudienceSelection.Adult, true), cancellation.Token));
        Assert.Equal(original, await settings.LoadAsync(CancellationToken.None));
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }

    [Theory]
    [InlineData(AudienceSelection.Unspecified, false)]
    [InlineData(AudienceSelection.Under18, false)]
    [InlineData(AudienceSelection.Adult, false)]
    [InlineData(AudienceSelection.Adult, true)]
    public async Task EveryNewSessionRequiresActiveChoiceRegardlessOfPersistedPreferences(AudienceSelection selection, bool optIn)
    {
        using var directory = new TestDirectory();
        var stored = new AudiencePreferences(selection, optIn);
        await new AudienceSettings(directory.Path).SaveAsync(stored, CancellationToken.None);
        var reloadedSettings = new AudienceSettings(directory.Path);
        Assert.Equal(stored, await reloadedSettings.LoadAsync(CancellationToken.None));
        var sessionPreferences = await reloadedSettings.LoadForSessionAsync(CancellationToken.None);
        Assert.Equal(new AudiencePreferences(AudienceSelection.Unspecified, false), sessionPreferences);
        var sessionPolicy = new AudiencePolicy(sessionPreferences);
        Assert.False(sessionPolicy.AcceptsMatureContent);
        Assert.Throws<InvalidDataException>(() => sessionPolicy.EnsureAllowed(ProfileTests.Valid() with { ContentRating = ContentRating.Mature18 }));
    }

    [Fact]
    public async Task CancelledRestrictiveSaveCannotRestorePreviousAdultAccessInNextSession()
    {
        using var directory = new TestDirectory();
        var settings = new AudienceSettings(directory.Path);
        var oldAdultChoice = new AudiencePreferences(AudienceSelection.Adult, true);
        await settings.SaveAsync(oldAdultChoice, CancellationToken.None);
        using var interruptedUnload = new CancellationTokenSource();
        interruptedUnload.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => settings.SaveAsync(
            new AudiencePreferences(AudienceSelection.Under18, false), interruptedUnload.Token));

        var nextSettings = new AudienceSettings(directory.Path);
        Assert.Equal(oldAdultChoice, await nextSettings.LoadAsync(CancellationToken.None));
        var nextSession = await nextSettings.LoadForSessionAsync(CancellationToken.None);
        Assert.Equal(new AudiencePreferences(AudienceSelection.Unspecified, false), nextSession);
        var nextPolicy = new AudiencePolicy(nextSession);
        Assert.False(nextPolicy.AcceptsMatureContent);
        Assert.Throws<InvalidDataException>(() => nextPolicy.EnsureAllowed(ProfileTests.Valid() with { ContentRating = ContentRating.Mature18 }));
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }
}
