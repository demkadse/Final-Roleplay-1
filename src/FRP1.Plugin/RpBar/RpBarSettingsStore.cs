using System.Text.Json;
using System.Text.Json.Serialization;

namespace FRP1.Plugin.RpBar;

public sealed class RpBarSettingsStore
{
    public const int MaximumBytes = 32 * 1024;
    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 5,
    };

    public RpBarSettingsStore(string configurationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationDirectory);
        path = Path.Combine(Path.GetFullPath(configurationDirectory), "rp-bar.json");
    }

    public static void Validate(RpBarPreferences value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RpBarGeometry.ValidateScale(value.Scale);
        if (!float.IsFinite(value.PositionX) || !float.IsFinite(value.PositionY) ||
            Math.Abs(value.PositionX) > 100_000 || Math.Abs(value.PositionY) > 100_000 ||
            value.ActiveProfiles is null || value.ActiveProfiles.Count > 128 ||
            value.ActiveProfiles.Any(pair => pair.Key == 0 || pair.Value == Guid.Empty))
            throw new InvalidDataException("Invalid RP bar settings.");
    }

    public async Task<RpBarPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                4096, FileOptions.Asynchronous);
            if (stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Invalid RP bar settings size.");
            var bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            var document = JsonSerializer.Deserialize<StoredSettings>(bytes, Options);
            if (document is null || document.SchemaVersion != 1) throw new InvalidDataException("Unsupported RP bar settings.");
            return RpBarPreferences.Snapshot(new RpBarPreferences
            {
                Visible = document.Visible, Locked = document.Locked, ShowHints = document.ShowHints, Collapsed = document.Collapsed, Scale = document.Scale,
                PositionX = document.PositionX, PositionY = document.PositionY, ActiveProfiles = document.ActiveProfiles,
            });
        }
        catch (FileNotFoundException) { return new(); }
        catch (DirectoryNotFoundException) { return new(); }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        { throw new InvalidDataException("Invalid RP bar settings.", ex); }
    }

    public async Task SaveAsync(RpBarPreferences value, CancellationToken cancellationToken = default)
    {
        var snapshot = RpBarPreferences.Snapshot(value);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new StoredSettings
        {
            SchemaVersion = 1, Visible = snapshot.Visible, Locked = snapshot.Locked, ShowHints = snapshot.ShowHints, Collapsed = snapshot.Collapsed, Scale = snapshot.Scale,
            PositionX = snapshot.PositionX, PositionY = snapshot.PositionY,
            ActiveProfiles = new Dictionary<ulong, Guid>(snapshot.ActiveProfiles),
        }, Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("RP bar settings exceed the size limit.");
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporary = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { if (temporary is not null) File.Delete(temporary); }
            finally { gate.Release(); }
        }
    }

    private sealed record StoredSettings
    {
        [JsonRequired] public int SchemaVersion { get; init; }
        [JsonRequired] public bool Visible { get; init; }
        [JsonRequired] public bool Locked { get; init; }
        [JsonRequired] public bool ShowHints { get; init; }
        public bool Collapsed { get; init; }
        [JsonRequired] public float Scale { get; init; }
        [JsonRequired] public float PositionX { get; init; }
        [JsonRequired] public float PositionY { get; init; }
        [JsonRequired] public Dictionary<ulong, Guid> ActiveProfiles { get; init; } = [];
    }
}
