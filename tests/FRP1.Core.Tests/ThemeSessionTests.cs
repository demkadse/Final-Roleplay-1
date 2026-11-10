using System.Collections.Concurrent;
using FRP1.Plugin;
using FRP1.Plugin.Theming;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class ThemeSessionTests
{
    [Fact]
    public async Task CorruptThemeFileUsesNeutralErrorStateAndRetryLoadsRepairedSettings()
    {
        using var directory = new TestDirectory();
        var themeDirectory = Path.Combine(directory.Path, "themes");
        System.IO.Directory.CreateDirectory(themeDirectory);
        await File.WriteAllTextAsync(Path.Combine(themeDirectory, "0000000000000001.json"), "broken");
        var store = new ThemeSettingsStore(directory.Path);
        using var session = new ThemeSession(store);
        session.ObserveCharacter(1, 4);
        await PollUntilLoadedAsync();
        Assert.True(session.LoadFailed);
        Assert.Equal("obsidian", session.Theme.Id);
        Assert.False(string.IsNullOrWhiteSpace(session.ErrorMessage));
        await store.SaveAsync(1, Manual("pergament"));
        session.Retry();
        await PollUntilLoadedAsync();
        Assert.False(session.LoadFailed);
        Assert.Null(session.ErrorMessage);
        Assert.Equal("pergament", session.Theme.Id);
        session.Dispose();
        await session.DisposalCompletion;

        async Task PollUntilLoadedAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            do
            {
                session.Poll();
                if (!session.IsLoading) return;
                await Task.Delay(5, timeout.Token);
            } while (true);
        }
    }

    [Fact]
    public async Task LatePreviousCharacterLoadCannotOverrideCurrentCharacter()
    {
        await using var harness = new SessionHarness();
        var session = harness.Session;
        session.ObserveCharacter(1, 1);
        var first = await harness.LoadAsync(0);
        Assert.Equal("obsidian", session.Theme.Id);
        session.ObserveCharacter(2, 4);
        var second = await harness.LoadAsync(1);
        Assert.True(first.Token.IsCancellationRequested);
        second.Result.SetResult(Manual("mondlicht"));
        await harness.PumpUntilAsync(() => !session.IsLoading);
        Assert.Equal("mondlicht", session.Theme.Id);
        first.Result.SetResult(Manual("pergament"));
        await harness.DrainAsync();
        Assert.Equal("mondlicht", session.Theme.Id);
        Assert.Equal(Manual("mondlicht"), session.Preferences);
    }

    [Fact]
    public async Task ManualChoiceSurvivesRaceChangesAndAutomaticModeTracksKnownRace()
    {
        await using var harness = new SessionHarness();
        await harness.ReadyAsync(1, 1, Manual("pergament"));
        var session = harness.Session;
        session.ObserveCharacter(1, 8);
        Assert.Equal("pergament", session.Theme.Id);
        session.ObserveCharacter(1, null);
        Assert.Equal("pergament", session.Theme.Id);
        Assert.Single(harness.Loads);
        session.SetPreferences(new ThemePreferences());
        Assert.Equal("obsidian", session.Theme.Id);
        session.ObserveCharacter(1, 4);
        Assert.Equal(4u, session.Theme.RaceId);
        session.ObserveCharacter(1, uint.MaxValue);
        Assert.Equal("obsidian", session.Theme.Id);
    }

    [Fact]
    public async Task PendingLoadAndLogoutUseNeutralThemeWithoutWritingAnySettings()
    {
        await using var harness = new SessionHarness();
        var session = harness.Session;
        session.ObserveCharacter(1, 4);
        var request = await harness.LoadAsync(0);
        Assert.True(session.IsLoading);
        Assert.Equal("obsidian", session.Theme.Id);
        session.ObserveCharacter(0, 4);
        Assert.False(session.HasCharacter);
        Assert.False(session.IsLoading);
        Assert.Equal("obsidian", session.Theme.Id);
        session.SetPreferences(Manual("pergament"));
        request.Result.SetResult(Manual("mondlicht"));
        await harness.DrainAsync();
        Assert.Equal("obsidian", session.Theme.Id);
        Assert.Equal(new ThemePreferences(), session.Preferences);
        Assert.Empty(harness.Saves);
    }

    [Fact]
    public async Task ExplicitChoiceDuringLoadCannotBeReplacedByOlderDiskResult()
    {
        await using var harness = new SessionHarness();
        var session = harness.Session;
        session.ObserveCharacter(1, 4);
        var load = await harness.LoadAsync(0);
        session.SetPreferences(Manual("pergament"));
        Assert.Equal("pergament", session.Theme.Id);
        load.Result.SetResult(Manual("mondlicht"));
        await harness.DrainAsync();
        Assert.Equal(Manual("pergament"), session.Preferences);
        Assert.Equal("pergament", session.Theme.Id);
        var save = await harness.SaveAsync(0);
        Assert.Equal(1ul, save.Character);
        Assert.Equal(Manual("pergament"), save.Preferences);
    }

    [Fact]
    public async Task OldCharacterSaveFailureCannotContaminateNewCharacter()
    {
        await using var harness = new SessionHarness();
        await harness.ReadyAsync(1, 1, new ThemePreferences());
        var session = harness.Session;
        session.SetPreferences(Manual("pergament"));
        var save = await harness.SaveAsync(0);
        session.ObserveCharacter(2, 8);
        var newLoad = await harness.LoadAsync(1);
        newLoad.Result.SetResult(Manual("mondlicht"));
        await harness.PumpUntilAsync(() => !session.IsLoading);
        save.Result.SetException(new IOException("Synthetic write failure"));
        await harness.DrainAsync();
        Assert.Equal("mondlicht", session.Theme.Id);
        Assert.False(session.SaveFailed);
        Assert.False(session.HasPendingSave);
        Assert.Null(session.ErrorMessage);
    }

    [Fact]
    public async Task RapidChoicesPersistNewestSelectionWithoutRollingBackAfterOldCompletion()
    {
        await using var harness = new SessionHarness();
        await harness.ReadyAsync(1, 1, new ThemePreferences());
        var session = harness.Session;
        session.SetPreferences(Manual("pergament"));
        var firstSave = await harness.SaveAsync(0);
        session.SetPreferences(Manual("miqote"));
        session.SetPreferences(Manual("mondlicht"));
        Assert.Equal("mondlicht", session.Theme.Id);
        Assert.True(session.HasPendingSave);
        firstSave.Result.SetResult();
        var finalSave = await harness.SaveAsync(1);
        Assert.Equal(Manual("mondlicht"), finalSave.Preferences);
        Assert.Equal("mondlicht", session.Theme.Id);
        finalSave.Result.SetResult();
        await harness.PumpUntilAsync(() => !session.HasPendingSave);
        Assert.Equal(Manual("mondlicht"), session.Preferences);
        Assert.False(session.SaveFailed);
        Assert.Equal(2, harness.Saves.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturningToCharacterWithPendingOrFailedSaveKeepsNewestExplicitChoice(bool saveFailedBeforeLeaving)
    {
        await using var harness = new SessionHarness();
        await harness.ReadyAsync(1, 1, new ThemePreferences());
        var session = harness.Session;
        session.SetPreferences(Manual("pergament"));
        var pendingSave = await harness.SaveAsync(0);
        if (saveFailedBeforeLeaving)
        {
            pendingSave.Result.SetException(new IOException("Synthetic write failure before changing character"));
            await harness.PumpUntilAsync(() => session.SaveFailed);
        }
        session.ObserveCharacter(2, 4);
        var otherLoad = await harness.LoadAsync(1);
        otherLoad.Result.SetResult(new ThemePreferences());
        await harness.PumpUntilAsync(() => !session.IsLoading);
        session.ObserveCharacter(1, 8);
        await harness.PumpUntilAsync(() => session.Theme.Id == "pergament" || harness.Loads.Count >= 3);
        if (harness.Loads.Count >= 3)
        {
            (await harness.LoadAsync(2)).Result.SetResult(new ThemePreferences());
            await harness.PumpUntilAsync(() => !session.IsLoading);
        }
        Assert.Equal("pergament", session.Theme.Id);
        Assert.Equal(Manual("pergament"), session.Preferences);
        if (saveFailedBeforeLeaving)
        {
            Assert.True(session.SaveFailed);
            session.Retry();
            var retriedSave = await harness.SaveAsync(1);
            Assert.Equal(1ul, retriedSave.Character);
            Assert.Equal(Manual("pergament"), retriedSave.Preferences);
            retriedSave.Result.SetResult();
            await harness.PumpUntilAsync(() => !session.HasPendingSave);
            Assert.False(session.SaveFailed);
        }
        else pendingSave.Result.SetResult();
        await harness.DrainAsync();
        Assert.Equal("pergament", session.Theme.Id);
    }

    [Fact]
    public async Task FailedLoadUsesNeutralFallbackAndRetryDoesNotWriteOverUnreadPreferences()
    {
        await using var harness = new SessionHarness();
        var session = harness.Session;
        session.ObserveCharacter(1, 4);
        (await harness.LoadAsync(0)).Result.SetException(new IOException("Synthetic read failure"));
        await harness.PumpUntilAsync(() => session.LoadFailed);
        Assert.Equal("obsidian", session.Theme.Id);
        Assert.False(string.IsNullOrWhiteSpace(session.ErrorMessage));
        Assert.Empty(harness.Saves);
        session.Retry();
        var retry = await harness.LoadAsync(1);
        Assert.Equal(1ul, retry.Character);
        retry.Result.SetResult(Manual("pergament"));
        await harness.PumpUntilAsync(() => !session.IsLoading);
        Assert.Equal("pergament", session.Theme.Id);
        Assert.False(session.LoadFailed);
        Assert.Null(session.ErrorMessage);
        Assert.Empty(harness.Saves);
    }

    [Fact]
    public async Task FailedSaveKeepsVisibleChoiceAndRetrySavesSameCharacterAndChoice()
    {
        await using var harness = new SessionHarness();
        await harness.ReadyAsync(1, 4, new ThemePreferences());
        var session = harness.Session;
        session.SetPreferences(Manual("pergament"));
        (await harness.SaveAsync(0)).Result.SetException(new IOException("Synthetic write failure"));
        await harness.PumpUntilAsync(() => session.SaveFailed);
        Assert.Equal("pergament", session.Theme.Id);
        Assert.False(string.IsNullOrWhiteSpace(session.ErrorMessage));
        session.Retry();
        var retry = await harness.SaveAsync(1);
        Assert.Equal(1ul, retry.Character);
        Assert.Equal(Manual("pergament"), retry.Preferences);
        retry.Result.SetResult();
        await harness.PumpUntilAsync(() => !session.HasPendingSave);
        Assert.Equal("pergament", session.Theme.Id);
        Assert.False(session.SaveFailed);
        Assert.Null(session.ErrorMessage);
    }

    [Fact]
    public async Task DisposalCancelsPendingOperationsAndObservesLateFailures()
    {
        await using var harness = new SessionHarness();
        var session = harness.Session;
        session.ObserveCharacter(1, 4);
        var pendingLoad = await harness.LoadAsync(0);
        session.SetPreferences(Manual("pergament"));
        var pendingSave = await harness.SaveAsync(0);
        session.Dispose();
        Assert.True(pendingLoad.Token.IsCancellationRequested);
        Assert.True(pendingSave.Token.IsCancellationRequested);
        pendingLoad.Result.SetException(new IOException("Late synthetic load failure"));
        pendingSave.Result.SetException(new IOException("Late synthetic save failure"));
        await session.DisposalCompletion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(session.DisposalCompletion.IsCompletedSuccessfully);
        Assert.Throws<ObjectDisposedException>(() => session.SetPreferences(new ThemePreferences()));
        Assert.Throws<ObjectDisposedException>(() => session.ObserveCharacter(2, 8));
    }

    private static ThemePreferences Manual(string id) => new(ThemeSelectionMode.Manual, id);
}

internal sealed class SessionHarness : IAsyncDisposable
{
    public ConcurrentQueue<ControlledLoad> Loads { get; } = new();
    public ConcurrentQueue<ControlledSave> Saves { get; } = new();
    public ThemeSession Session { get; }

    public SessionHarness()
    {
        Session = new ThemeSession((character, token) =>
        {
            var request = new ControlledLoad(character, token);
            Loads.Enqueue(request);
            return request.Result.Task;
        }, (character, preferences, token) =>
        {
            var request = new ControlledSave(character, preferences, token);
            Saves.Enqueue(request);
            return request.Result.Task;
        });
    }

    public async Task ReadyAsync(ulong character, uint? race, ThemePreferences preferences)
    {
        var index = Loads.Count;
        Session.ObserveCharacter(character, race);
        var request = await LoadAsync(index);
        request.Result.SetResult(preferences);
        await PumpUntilAsync(() => !Session.IsLoading);
    }

    public async Task<ControlledLoad> LoadAsync(int index)
    {
        await PumpUntilAsync(() => Loads.Count > index);
        return Loads.ToArray()[index];
    }

    public async Task<ControlledSave> SaveAsync(int index)
    {
        await PumpUntilAsync(() => Saves.Count > index);
        return Saves.ToArray()[index];
    }

    public async Task PumpUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (true)
        {
            Session.Poll();
            if (condition()) return;
            await Task.Delay(5, timeout.Token);
        }
    }

    public async Task DrainAsync()
    {
        // Task.Run unwraps the controlled tasks asynchronously before Poll can collect them.
        await Task.Delay(30);
        Session.Poll();
    }

    public async ValueTask DisposeAsync()
    {
        Session.Dispose();
        foreach (var request in Loads) request.Result.TrySetCanceled();
        foreach (var request in Saves) request.Result.TrySetCanceled();
        await Session.DisposalCompletion.WaitAsync(TimeSpan.FromSeconds(3));
    }
}

internal sealed record ControlledLoad(ulong Character, CancellationToken Token)
{
    public TaskCompletionSource<ThemePreferences> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed record ControlledSave(ulong Character, ThemePreferences Preferences, CancellationToken Token)
{
    public TaskCompletionSource Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
