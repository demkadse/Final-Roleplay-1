using System.Text.Json;
using System.Text.Json.Serialization;

namespace FRP1.Core.Networking;

public sealed record PeerTrust(PeerInvitation Invitation, string DisplayName = "", bool ShareProfile = false, bool Blocked = false);

public sealed class PeerTrustStore
{
    public const int MaximumPeers = 16;
    public const int MaximumBytes = 32 * 1024;
    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);
    public string? LocalIdentityFingerprint { get; private set; }
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 8,
    };

    public PeerTrustStore(string configurationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationDirectory);
        path = Path.Combine(Path.GetFullPath(configurationDirectory), "peer-trust.json");
    }

    public void BindLocalIdentity(string fingerprint)
    {
        if (!PeerInvitation.IsFingerprint(fingerprint)) throw new InvalidDataException("Invalid local identity pin.");
        if (LocalIdentityFingerprint is not null && !string.Equals(LocalIdentityFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The local identity changed. Explicit recovery and re-pairing are required.");
        LocalIdentityFingerprint = fingerprint.ToUpperInvariant();
    }

    public static IReadOnlyList<PeerTrust> ValidateAndSnapshot(IEnumerable<PeerTrust> peers)
    {
        ArgumentNullException.ThrowIfNull(peers);
        var result = peers.Take(MaximumPeers + 1).ToArray();
        if (result.Length > MaximumPeers) throw new InvalidDataException("Too many paired peers.");
        var pins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var peer in result)
        {
            if (peer is null || peer.Invitation is null || peer.DisplayName is null || peer.DisplayName.Length > 160 ||
                peer.DisplayName.Any(char.IsControl)) throw new InvalidDataException("Invalid paired peer.");
            peer.Invitation.Validate();
            if (!pins.Add(peer.Invitation.Fingerprint)) throw new InvalidDataException("Duplicate peer identity.");
        }
        return Array.AsReadOnly(result.Select(peer => peer with
        {
            Invitation = peer.Invitation with { Fingerprint = peer.Invitation.Fingerprint.ToUpperInvariant() },
            ShareProfile = !peer.Blocked && peer.ShareProfile,
        }).ToArray());
    }

    public async Task<IReadOnlyList<PeerTrust>> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
            if (stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Invalid peer trust size.");
            var bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            var document = JsonSerializer.Deserialize<TrustDocument>(bytes, Options);
            if (document is null || document.SchemaVersion != 1 || document.Peers is null)
                throw new InvalidDataException("Unsupported peer trust settings.");
            if (document.LocalIdentityFingerprint is not null && !PeerInvitation.IsFingerprint(document.LocalIdentityFingerprint))
                throw new InvalidDataException("Invalid local identity pin.");
            LocalIdentityFingerprint = document.LocalIdentityFingerprint;
            return ValidateAndSnapshot(document.Peers.Select(peer => peer is null || peer.Invitation is null
                ? throw new InvalidDataException("Invalid paired peer.")
                : new PeerTrust(peer.Invitation, peer.DisplayName, peer.ShareProfile, peer.Blocked)));
        }
        catch (FileNotFoundException) { return Array.Empty<PeerTrust>(); }
        catch (DirectoryNotFoundException) { return Array.Empty<PeerTrust>(); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid peer trust settings.", ex); }
    }

    public async Task SaveAsync(IReadOnlyList<PeerTrust> peers, CancellationToken cancellationToken = default)
    {
        var snapshot = ValidateAndSnapshot(peers);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new TrustDocument
        {
            SchemaVersion = 1,
            LocalIdentityFingerprint = LocalIdentityFingerprint,
            Peers = snapshot.Select(peer => new StoredPeer
            {
                Invitation = peer.Invitation, DisplayName = peer.DisplayName, ShareProfile = peer.ShareProfile, Blocked = peer.Blocked,
            }).ToArray(),
        }, Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Peer trust exceeds the size limit.");
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { try { File.Delete(temporary); } finally { gate.Release(); } }
    }

    private sealed record TrustDocument
    {
        [JsonRequired] public int SchemaVersion { get; init; }
        [JsonRequired] public string? LocalIdentityFingerprint { get; init; }
        [JsonRequired] public StoredPeer[] Peers { get; init; } = [];
    }

    private sealed record StoredPeer
    {
        [JsonRequired] public PeerInvitation Invitation { get; init; } = null!;
        [JsonRequired] public string DisplayName { get; init; } = "";
        [JsonRequired] public bool ShareProfile { get; init; }
        [JsonRequired] public bool Blocked { get; init; }
    }
}
