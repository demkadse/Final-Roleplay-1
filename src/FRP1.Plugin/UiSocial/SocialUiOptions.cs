using System.Text.Json;
using System.Text.Json.Serialization;
using FRP1.Core.Profiles;

namespace FRP1.Plugin.UiSocial;

public sealed record SocialUiOptions
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public bool MouseoverTooltip { get; init; } = true;
    [JsonRequired] public bool TargetVisible { get; init; } = true;
    [JsonRequired] public bool TargetLocked { get; init; }
    [JsonRequired] public bool OnlyKnownEnvironment { get; init; } = true;
    [JsonRequired] public float TargetX { get; init; } = 32;
    [JsonRequired] public float TargetY { get; init; } = 180;
    // Optional schema-1 additions preserve earlier preferences without opting into a new network action.
    public MouseoverDetail MouseoverDetail { get; init; } = MouseoverDetail.Compact;
    public RpStatus? EnvironmentStatus { get; init; }
    public RpAvailability? EnvironmentAvailability { get; init; }
    public bool HideEmptyEnvironment { get; init; }
    public int ListenPort { get; init; }
}

public sealed class SocialUiOptionsStore(string directory)
{
    private readonly string path = Path.Combine(Path.GetFullPath(directory), "social-ui.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 3,
    };

    public static void Validate(SocialUiOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaVersion != 1 || !float.IsFinite(value.TargetX) || !float.IsFinite(value.TargetY) ||
            Math.Abs(value.TargetX) > 100000 || Math.Abs(value.TargetY) > 100000 || !Enum.IsDefined(value.MouseoverDetail) ||
            (value.EnvironmentStatus.HasValue && !Enum.IsDefined(value.EnvironmentStatus.Value)) ||
            (value.EnvironmentAvailability.HasValue && !Enum.IsDefined(value.EnvironmentAvailability.Value)) ||
            value.ListenPort is < 0 or > 65535)
            throw new InvalidDataException("Invalid social display settings.");
    }

    public async Task<SocialUiOptions> LoadAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                1024, FileOptions.Asynchronous);
            if (stream.Length is <= 0 or > 1024) throw new InvalidDataException("Invalid social display settings.");
            var bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
            var value = JsonSerializer.Deserialize<SocialUiOptions>(bytes, JsonOptions) ?? throw new InvalidDataException("Missing social display settings.");
            Validate(value);
            return value;
        }
        catch (FileNotFoundException) { return new(); }
        catch (DirectoryNotFoundException) { return new(); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid social display settings.", ex); }
    }

    public async Task SaveAsync(SocialUiOptions value, CancellationToken token = default)
    {
        Validate(value);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
