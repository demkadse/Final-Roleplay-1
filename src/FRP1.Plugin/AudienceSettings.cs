using System.Text.Json;
using System.Text.Json.Serialization;
using FRP1.Core.Safety;

namespace FRP1.Plugin;

// This self-declaration stays local; it is not proof or verification of age.
internal sealed class AudienceSettings(string directory)
{
    private readonly string path = Path.Combine(Path.GetFullPath(directory), "audience.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 4,
        Converters = { new JsonStringEnumConverter<AudienceSelection>(allowIntegerValues: false) },
    };

    // Stored preferences are never authorization for a new plugin session. This also
    // fails closed when a previous restrictive change could not finish writing.
    public async Task<AudiencePreferences> LoadForSessionAsync(CancellationToken token)
    {
        _ = await LoadAsync(token).ConfigureAwait(false);
        return new AudiencePreferences();
    }

    public async Task<AudiencePreferences> LoadAsync(CancellationToken token)
    {
        if (!File.Exists(path)) return new AudiencePreferences();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            1024, FileOptions.Asynchronous);
        if (stream.Length > 1024) throw new InvalidDataException("Invalid audience preferences.");
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        AudiencePreferences? preferences;
        try { preferences = JsonSerializer.Deserialize<AudiencePreferences>(bytes, JsonOptions); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid audience preferences.", ex); }
        if (preferences is null) throw new InvalidDataException("Missing audience preferences.");
        Validate(preferences);
        return preferences;
    }

    public async Task SaveAsync(AudiencePreferences preferences, CancellationToken token)
    {
        Validate(preferences);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(preferences, JsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void Validate(AudiencePreferences preferences)
    {
        if (!Enum.IsDefined(preferences.Selection) ||
            (preferences.Selection != AudienceSelection.Adult && preferences.MatureContentOptIn))
            throw new InvalidDataException("Invalid audience preferences.");
    }
}
