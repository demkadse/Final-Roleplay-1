using System.Text.Json.Serialization;
using FRP1.Plugin.Utility;

namespace FRP1.Plugin.Automation;

public sealed class AutomationSettingsStore
{
    private readonly LocalJsonFile<Document> file;

    public AutomationSettingsStore(string configurationDirectory) => file = new(configurationDirectory, "automation.json", 32 * 1024,
        () => new Document(), value => value is null || value.SchemaVersion != 1
            ? throw new InvalidDataException("Unsupported automation settings.")
            : value with { Preferences = AutomationPreferences.Snapshot(value.Preferences) });

    public async Task<AutomationPreferences> LoadAsync(CancellationToken cancellationToken = default) =>
        (await file.LoadAsync(cancellationToken).ConfigureAwait(false)).Preferences;

    public Task SaveAsync(AutomationPreferences value, CancellationToken cancellationToken = default) =>
        file.SaveAsync(new Document { Preferences = AutomationPreferences.Snapshot(value) }, cancellationToken);

    private sealed record Document
    {
        [JsonRequired] public int SchemaVersion { get; init; } = 1;
        [JsonRequired] public AutomationPreferences Preferences { get; init; } = new();
    }
}
