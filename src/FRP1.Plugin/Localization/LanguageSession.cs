using FRP1.Plugin.Utility;

namespace FRP1.Plugin.Localization;

public sealed class LanguageSession : IDisposable
{
    private readonly LocalPreferences<UiLanguage> state;
    public LanguageSession(LanguageSettingsStore store) : this(store.LoadAsync, store.SaveAsync) { }
    public LanguageSession(Func<CancellationToken, Task<UiLanguage>> load, Func<UiLanguage, CancellationToken, Task> save) =>
        state = new(UiLanguage.German, load, save, value => Enum.IsDefined(value) ? value : throw new InvalidDataException("Invalid UI language."));
    public UiLanguage Language => state.Value;
    public bool Loaded => state.Loaded;
    public bool IsBusy => state.IsBusy;
    public bool HasUnsavedChanges => state.HasUnsavedChanges;
    public string? ErrorMessage => state.ErrorKey is { } key ? UiText.Get(key, Language) : null;
    public long Version => state.Version;
    public Task DisposalCompletion => state.DisposalCompletion;
    public void SetLanguage(UiLanguage language) => state.Set(language);
    public void Poll() => state.Poll();
    public void Retry() => state.Retry();
    public void Dispose() => state.Dispose();
}
