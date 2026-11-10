using System.Text.Json.Serialization;
using FRP1.Plugin.Utility;

namespace FRP1.Plugin.Localization;

public enum UiLanguage { German, English }

public sealed class LanguageSettingsStore
{
    private readonly LocalJsonFile<Document> file;

    public LanguageSettingsStore(string configurationDirectory) => file = new(configurationDirectory, "language.json", 1024,
        () => new Document(), value => value is null || value.SchemaVersion != 1 || !Enum.IsDefined(value.Language)
            ? throw new InvalidDataException("Unsupported UI language settings.") : value);

    public async Task<UiLanguage> LoadAsync(CancellationToken cancellationToken = default) =>
        (await file.LoadAsync(cancellationToken).ConfigureAwait(false)).Language;

    public Task SaveAsync(UiLanguage language, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(language)) throw new ArgumentOutOfRangeException(nameof(language));
        return file.SaveAsync(new Document { Language = language }, cancellationToken);
    }

    private sealed record Document
    {
        [JsonRequired] public int SchemaVersion { get; init; } = 1;
        [JsonRequired] public UiLanguage Language { get; init; } = UiLanguage.German;
    }
}
