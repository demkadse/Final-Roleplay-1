using System.Collections.Concurrent;
using FRP1.Plugin.Localization;
using FRP1.Plugin.Utility;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class LocalizationUtilityTests
{
    [Fact]
    public async Task ExplicitLanguageChoiceDuringDelayedLoadWinsAndIsSaved()
    {
        await using var harness = new LanguageHarness();
        harness.Session.SetLanguage(UiLanguage.English);
        harness.Loads.ToArray()[0].SetResult(UiLanguage.German);
        await harness.PumpAsync(() => harness.Writes.Count == 1);
        Assert.Equal(UiLanguage.English, harness.Session.Language);
        var write = harness.Writes.ToArray()[0];
        Assert.Equal(UiLanguage.English, write.Language);
        write.Completion.SetResult();
        await harness.PumpAsync(() => !harness.Session.IsBusy);
        Assert.False(harness.Session.HasUnsavedChanges);
    }

    [Fact]
    public async Task LanguageRetryCannotReplaceNewerChoiceAndFailedSaveKeepsLatestChoice()
    {
        await using var harness = new LanguageHarness();
        harness.Loads.ToArray()[0].SetException(new InvalidDataException("Synthetic bad language file"));
        await harness.PumpAsync(() => harness.Session.Loaded && !harness.Session.IsBusy);
        Assert.NotNull(harness.Session.ErrorMessage);
        harness.Session.Retry();
        await harness.PumpAsync(() => harness.Loads.Count == 2);
        harness.Session.SetLanguage(UiLanguage.English);
        harness.Loads.ToArray()[1].SetResult(UiLanguage.German);
        await harness.PumpAsync(() => harness.Writes.Count == 1);
        harness.Writes.ToArray()[0].Completion.SetException(new IOException("Synthetic failed save"));
        await harness.PumpAsync(() => !harness.Session.IsBusy);
        Assert.Equal(UiLanguage.English, harness.Session.Language);
        Assert.True(harness.Session.HasUnsavedChanges);
        harness.Session.Retry();
        await harness.PumpAsync(() => harness.Writes.Count == 2);
        Assert.Equal(UiLanguage.English, harness.Writes.ToArray()[1].Language);
        harness.Writes.ToArray()[1].Completion.SetResult();
        await harness.PumpAsync(() => !harness.Session.IsBusy);
        Assert.False(harness.Session.HasUnsavedChanges);
        Assert.Null(harness.Session.ErrorMessage);
    }

    [Fact]
    public async Task ChangingLanguageWhileOldSaveIsPendingPersistsNewestValue()
    {
        await using var harness = new LanguageHarness();
        harness.Loads.ToArray()[0].SetResult(UiLanguage.German);
        await harness.PumpAsync(() => harness.Session.Loaded && !harness.Session.IsBusy);
        harness.Session.SetLanguage(UiLanguage.English);
        await harness.PumpAsync(() => harness.Writes.Count == 1);
        harness.Session.SetLanguage(UiLanguage.German);
        harness.Writes.ToArray()[0].Completion.SetResult();
        await harness.PumpAsync(() => harness.Writes.Count == 2);
        Assert.Equal(UiLanguage.German, harness.Session.Language);
        Assert.Equal(UiLanguage.German, harness.Writes.ToArray()[1].Language);
        harness.Writes.ToArray()[1].Completion.SetResult();
        await harness.PumpAsync(() => !harness.Session.IsBusy);
        Assert.False(harness.Session.HasUnsavedChanges);
    }

    [Fact]
    public async Task LanguageStoreDefaultsGermanAndPersistsOnlyValidatedChoice()
    {
        using var directory = new TestDirectory();
        var store = new LanguageSettingsStore(directory.Path);
        Assert.Equal(UiLanguage.German, await store.LoadAsync());
        Assert.Empty(Directory.EnumerateFiles(directory.Path));
        await store.SaveAsync(UiLanguage.English);
        Assert.Equal(UiLanguage.English, await new LanguageSettingsStore(directory.Path).LoadAsync());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(UiLanguage.German, cancellation.Token));
        Assert.Equal(UiLanguage.English, await store.LoadAsync());
        Assert.Single(Directory.EnumerateFiles(directory.Path));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.SaveAsync((UiLanguage)999));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":2,\"language\":0}")]
    [InlineData("{\"schemaVersion\":1,\"language\":999}")]
    [InlineData("{\"schemaVersion\":1,\"language\":0,\"language\":1}")]
    [InlineData("{\"schemaVersion\":1,\"language\":0,\"unexpected\":true}")]
    public async Task LanguageStoreRejectsIncompleteUnknownOrAmbiguousData(string json)
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "language.json"), json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new LanguageSettingsStore(directory.Path).LoadAsync());
    }

    [Fact]
    public void TranslationCatalogProvidesBothLanguagesWithoutTranslatingUnrecognizedUserContent()
    {
        Assert.NotEmpty(UiText.Keys);
        Assert.Equal(UiText.Keys.Count, UiText.Keys.Distinct().Count());
        foreach (var key in UiText.Keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(UiText.Get(key, UiLanguage.German)));
            Assert.False(string.IsNullOrWhiteSpace(UiText.Get(key, UiLanguage.English)));
            Assert.NotEqual(key, UiText.Get(key, UiLanguage.English));
        }
        Assert.Equal("Synthetic player text", UiText.Translate("Synthetic player text", UiLanguage.English));
        var original = UiText.CurrentLanguage;
        try
        {
            UiText.CurrentLanguage = UiLanguage.English;
            Assert.Equal(UiText.Get("Common.Save", UiLanguage.English), UiText.Get("Common.Save"));
            Assert.Throws<ArgumentOutOfRangeException>(() => UiText.CurrentLanguage = (UiLanguage)999);
        }
        finally { UiText.CurrentLanguage = original; }
    }

    [Fact]
    public void ProfileReferenceRoundtripIsStrictAndContainsNoAddressOrConnectionPermission()
    {
        var reference = new ProfileReference(new string('a', 64), Guid.NewGuid());
        var encoded = reference.Format();
        Assert.Equal(ProfileReference.EncodedLength, encoded.Length);
        var parsed = ProfileReference.Parse(encoded);
        Assert.Equal(new string('A', 64), parsed.Fingerprint);
        Assert.Equal(reference.ProfileId, parsed.ProfileId);
        Assert.False(ProfileReference.TryParse(encoded + " ", out _));
        Assert.False(ProfileReference.TryParse(" " + encoded, out _));
        Assert.False(ProfileReference.TryParse(encoded.Replace(reference.ProfileId.ToString("N"), reference.ProfileId.ToString("D")), out _));
        Assert.Throws<InvalidDataException>(() => new ProfileReference(new string('A', 64), Guid.Empty).Format());
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("https://example.invalid/profile")]
    [InlineData("frp1:127.0.0.1:1234")] [InlineData("frp1-profile:invalid")]
    public void NonReferencesAreNeverAcceptedAsLocalProfileLinks(string? value)
    {
        Assert.False(ProfileReference.TryParse(value, out var reference));
        Assert.Null(reference);
    }

    [Fact]
    public void TextDraftIsBoundedLiteralPreparationAndNeverChangesPublicProfile()
    {
        var draft = new RpTextDraft(" Synthetic NPC ", "Literal /say text\nSecond line", RpTextTemplate.Speech);
        Assert.Contains("Synthetic NPC", draft.Preview());
        Assert.Contains(draft.Text, draft.Preview());
        Assert.StartsWith("* ", (draft with { Template = RpTextTemplate.Emote }).Preview());
        Assert.Equal("", (draft with { Text = "  " }).Preview());
        Assert.Throws<InvalidDataException>(() => (draft with { Name = new string('x', 81) }).Validate());
        Assert.Throws<InvalidDataException>(() => (draft with { Text = new string('x', 2049) }).Validate());
        Assert.Throws<InvalidDataException>(() => (draft with { Name = "Bad\nName" }).Validate());
        Assert.Throws<InvalidDataException>(() => (draft with { Text = "Bad\0Text" }).Validate());
    }
}

internal sealed class LanguageHarness : IAsyncDisposable
{
    public ConcurrentQueue<TaskCompletionSource<UiLanguage>> Loads { get; } = new();
    public ConcurrentQueue<LanguageWrite> Writes { get; } = new();
    public LanguageSession Session { get; }
    public LanguageHarness()
    {
        Session = new LanguageSession(_ =>
        {
            var completion = new TaskCompletionSource<UiLanguage>(TaskCreationOptions.RunContinuationsAsynchronously);
            Loads.Enqueue(completion);
            return completion.Task;
        }, (language, _) =>
        {
            var write = new LanguageWrite(language);
            Writes.Enqueue(write);
            return write.Completion.Task;
        });
        if (!SpinWait.SpinUntil(() => Loads.Count == 1, TimeSpan.FromSeconds(3))) throw new TimeoutException("Synthetic language load did not start.");
    }
    public async Task PumpAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (true)
        {
            Session.Poll();
            if (condition()) return;
            await Task.Delay(5, timeout.Token);
        }
    }
    public async ValueTask DisposeAsync()
    {
        Session.Dispose();
        foreach (var load in Loads) load.TrySetCanceled();
        foreach (var write in Writes) write.Completion.TrySetCanceled();
        await Session.DisposalCompletion.WaitAsync(TimeSpan.FromSeconds(3));
    }
}

internal sealed record LanguageWrite(UiLanguage Language)
{
    public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
