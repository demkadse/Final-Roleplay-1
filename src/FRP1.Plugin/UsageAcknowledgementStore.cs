using System.Text.Json;
using System.Text.Json.Serialization;

namespace FRP1.Plugin;

internal sealed class UsageAcknowledgementStore(string directory)
{
    private const int MaximumBytes = 128;
    private readonly string path = Path.Combine(Path.GetFullPath(directory), "usage-notices.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 2,
    };

    public async Task<bool> IsAcknowledgedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!File.Exists(path)) return false;
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                128, FileOptions.Asynchronous);
            if (stream.Length is <= 0 or > MaximumBytes) return false;
            var bytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            var acknowledgement = JsonSerializer.Deserialize<Acknowledgement>(bytes, JsonOptions);
            return acknowledgement?.Version == UsageGuidelines.CurrentVersion;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task AcknowledgeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new Acknowledgement { Version = UsageGuidelines.CurrentVersion }, JsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record Acknowledgement
    {
        [JsonRequired] public int Version { get; init; }
    }
}
