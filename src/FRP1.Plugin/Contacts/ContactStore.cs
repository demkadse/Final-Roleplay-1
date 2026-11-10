using System.Text.Json;
using System.Text.Json.Serialization;

namespace FRP1.Plugin.Contacts;

public sealed class ContactStore
{
    public const int MaximumBytes = 512 * 1024;
    private readonly string path;
    private readonly SemaphoreSlim writes = new(1, 1);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 10,
        Converters = { new JsonStringEnumConverter<RelationshipKind>(allowIntegerValues: false),
            new JsonStringEnumConverter<EncounterKind>(allowIntegerValues: false) },
    };

    public ContactStore(string configurationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationDirectory);
        path = Path.Combine(Path.GetFullPath(configurationDirectory), "contacts.json");
    }

    public async Task<ContactRegister> LoadAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                4096, FileOptions.Asynchronous);
            if (stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Local register exceeds its limit.");
            var bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<ContactRegister>(bytes, Options) ?? throw new InvalidDataException("Missing local register.");
            return ContactData.Copy(data);
        }
        catch (FileNotFoundException) { return ContactData.Copy(new()); }
        catch (DirectoryNotFoundException) { return ContactData.Copy(new()); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid local register.", ex); }
    }

    public async Task SaveAsync(ContactRegister register, CancellationToken token = default)
    {
        var snapshot = ContactData.Copy(register);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Local register exceeds its limit.");
        await writes.WaitAsync(token).ConfigureAwait(false);
        string? temporary = null;
        try
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { if (temporary is not null) File.Delete(temporary); }
            finally { writes.Release(); }
        }
    }
}
