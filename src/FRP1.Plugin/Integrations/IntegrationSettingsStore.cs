using FRP1.Plugin.Utility;

namespace FRP1.Plugin.Integrations;

public sealed class IntegrationSettingsStore(string configurationDirectory)
{
    // Five ID/hash/rating bindings and one typed housing destination, all local.
    public const int MaximumBytes = 8192;
    private readonly LocalJsonFile<IntegrationSettings> file = new(configurationDirectory, "integrations.json", MaximumBytes,
        () => new(), Validate);

    private static IntegrationSettings Validate(IntegrationSettings value)
    {
        if (value is null || value.SchemaVersion != 1 || value.MoodlesBindings is null)
            throw new InvalidDataException("Invalid local integration settings.");
        var options = MoodlesRules.ValidateOptions(new(value.MoodlesEnabled, value.MoodlesBindings));
        var lifestream = LifestreamRules.ValidateOptions(new(value.LifestreamEnabled, value.LifestreamAddress));
        return value with { MoodlesBindings = options.Bindings!, LifestreamAddress = lifestream.Address };
    }

    public Task<IntegrationSettings> LoadAsync(CancellationToken cancellationToken = default) => file.LoadAsync(cancellationToken);
    public Task SaveAsync(IntegrationSettings value, CancellationToken cancellationToken = default) => file.SaveAsync(value, cancellationToken);
}
