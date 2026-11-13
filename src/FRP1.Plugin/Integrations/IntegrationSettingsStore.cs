using FRP1.Plugin.Utility;

namespace FRP1.Plugin.Integrations;

public sealed class IntegrationSettingsStore(string configurationDirectory)
{
    public const int MaximumBytes = 1024;
    private readonly LocalJsonFile<IntegrationSettings> file = new(configurationDirectory, "integrations.json", MaximumBytes,
        () => new(), value => value is null || value.SchemaVersion != 1
            ? throw new InvalidDataException("Invalid local integration settings.") : value);

    public Task<IntegrationSettings> LoadAsync(CancellationToken cancellationToken = default) => file.LoadAsync(cancellationToken);
    public Task SaveAsync(IntegrationSettings value, CancellationToken cancellationToken = default) => file.SaveAsync(value, cancellationToken);
}
