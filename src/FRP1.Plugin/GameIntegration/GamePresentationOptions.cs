using System.Text.Json.Serialization;
using FRP1.Plugin.Utility;

namespace FRP1.Plugin.GameIntegration;

public sealed record GamePresentationOptions
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public bool Nameplates { get; init; }
    [JsonRequired] public bool Titles { get; init; }
    [JsonRequired] public bool ProfileColors { get; init; }
    [JsonRequired] public bool ChatNames { get; init; }
    // Optional schema-1 addition: old preferences keep profile icons disabled.
    public bool ProfileIcons { get; init; }
}

public sealed class GamePresentationOptionsStore(string configurationDirectory)
{
    private readonly LocalJsonFile<GamePresentationOptions> file = new(configurationDirectory, "game-presentation.json", 1024,
        () => new(), value => value is null || value.SchemaVersion != 1
            ? throw new InvalidDataException("Invalid local game presentation settings.") : value);

    public Task<GamePresentationOptions> LoadAsync(CancellationToken cancellationToken = default) => file.LoadAsync(cancellationToken);
    public Task SaveAsync(GamePresentationOptions value, CancellationToken cancellationToken = default) => file.SaveAsync(value, cancellationToken);
}
