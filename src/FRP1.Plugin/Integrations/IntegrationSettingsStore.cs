using FRP1.Plugin.Utility;

namespace FRP1.Plugin.Integrations;

public sealed class IntegrationSettingsStore(string configurationDirectory)
{
    // Five ID/hash/rating bindings plus the existing optional integrations.
    public const int MaximumBytes = 8192;
    private readonly LocalJsonFile<IntegrationSettings> file = new(configurationDirectory, "integrations.json", MaximumBytes,
        () => new(), Validate);

    private static IntegrationSettings Validate(IntegrationSettings value)
    {
        if (value is null || value.SchemaVersion != 1 || value.MoodlesBindings is null)
            throw new InvalidDataException("Invalid local integration settings.");
        var options = MoodlesRules.ValidateOptions(new(value.MoodlesEnabled, value.MoodlesBindings));
        return value with { MoodlesBindings = options.Bindings! };
    }

    public Task<IntegrationSettings> LoadAsync(CancellationToken cancellationToken = default) => file.LoadAsync(cancellationToken);
    public Task SaveAsync(IntegrationSettings value, CancellationToken cancellationToken = default) => file.SaveAsync(value, cancellationToken);
}
