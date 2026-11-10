using System.Text.Json;
using System.Text.Json.Serialization;

namespace FRP1.Plugin.Theming;

public sealed class ThemeSettingsStore
{
    public const int MaximumBytes = 1024;
    private readonly string directory;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 4,
    };

    public ThemeSettingsStore(string configurationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationDirectory);
        directory = Path.Combine(Path.GetFullPath(configurationDirectory), "themes");
    }

    public async Task<ThemePreferences> LoadAsync(ulong contentId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(contentId);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                1024, FileOptions.Asynchronous);
            if (stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Invalid theme settings size.");
            var bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            var settings = JsonSerializer.Deserialize<StoredSettings>(bytes, JsonOptions);
            if (settings is null || settings.SchemaVersion != 1)
                throw new InvalidDataException("Unsupported theme settings.");
            var preferences = new ThemePreferences(settings.Mode, settings.ManualThemeId, settings.ReduceDecoration);
            ThemeCatalog.ValidatePreferences(preferences);
            return preferences;
        }
        catch (FileNotFoundException) { return new ThemePreferences(); }
        catch (DirectoryNotFoundException) { return new ThemePreferences(); }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        { throw new InvalidDataException("Invalid theme settings.", ex); }
    }

    public async Task SaveAsync(ulong contentId, ThemePreferences preferences, CancellationToken cancellationToken = default)
    {
        var path = PathFor(contentId);
        ThemeCatalog.ValidatePreferences(preferences);
        var settings = new StoredSettings
        {
            SchemaVersion = 1,
            Mode = preferences.Mode,
            ManualThemeId = preferences.ManualThemeId,
            ReduceDecoration = preferences.ReduceDecoration,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Theme settings exceed the size limit.");
        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporary = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { if (temporary is not null) File.Delete(temporary); }
            finally { writeGate.Release(); }
        }
    }

    private string PathFor(ulong contentId)
    {
        if (contentId == 0) throw new ArgumentOutOfRangeException(nameof(contentId), "A logged-in character is required.");
        return Path.Combine(directory, contentId.ToString("X16", System.Globalization.CultureInfo.InvariantCulture) + ".json");
    }

    private sealed record StoredSettings
    {
        [JsonRequired] public int SchemaVersion { get; init; }
        [JsonRequired] public ThemeSelectionMode Mode { get; init; }
        [JsonRequired] public string ManualThemeId { get; init; } = "";
        [JsonRequired] public bool ReduceDecoration { get; init; }
    }
}
