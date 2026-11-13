using System.Text.Json;
using System.Text.Json.Serialization;

namespace FRP1.Plugin.Utility;

// Shared bounded file primitive for private local preferences only.
internal sealed class LocalJsonFile<T>(string directory, string name, int maximumBytes, Func<T> missing, Func<T, T> validate)
{
    private readonly string path = Path.Combine(Path.GetFullPath(directory), name);
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 10,
    };

    public async Task<T> LoadAsync(CancellationToken cancellationToken = default)
    {
        // Coordinate readers with the atomic rename on Windows, using the same per-file instance gate.
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
            if (stream.Length is <= 0 || stream.Length > maximumBytes) throw new InvalidDataException("Invalid local settings size.");
            var bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            return validate(JsonSerializer.Deserialize<T>(bytes, Options) ?? throw new InvalidDataException("Missing local settings."));
        }
        catch (FileNotFoundException) { return validate(missing()); }
        catch (DirectoryNotFoundException) { return validate(missing()); }
        catch (Exception ex) when (ex is JsonException or ArgumentException) { throw new InvalidDataException("Invalid local settings.", ex); }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(validate(value), Options);
        if (bytes.Length > maximumBytes) throw new InvalidDataException("Local settings exceed the size limit.");
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                // Rename the previous file aside before deleting it: Windows readers may still hold its handle.
                var previous = temporary + ".previous";
                File.Replace(temporary, path, previous);
                // Keep the previous file recoverable if replacement itself fails partway through.
                File.Delete(previous);
            }
            else File.Move(temporary, path);
        }
        finally { try { File.Delete(temporary); } finally { gate.Release(); } }
    }
}
