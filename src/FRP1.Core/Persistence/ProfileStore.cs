using FRP1.Core.Profiles;
using FRP1.Core.Safety;

namespace FRP1.Core.Persistence;

public sealed class ProfileStore
{
    private readonly string root;
    private readonly SemaphoreSlim gate = new(1, 1);

    public AudiencePolicy Policy { get; }

    public ProfileStore(string directory, AudiencePolicy? policy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        root = Path.GetFullPath(directory);
        Policy = policy ?? new AudiencePolicy();
    }

    public Task SaveLocalAsync(ProfileDocument profile, CancellationToken cancellationToken = default)
    {
        Policy.EnsureAllowed(profile);
        return SaveAsync(Path.Combine(root, "profiles", profile.Id.ToString("N") + ".json"), profile, cancellationToken);
    }

    public async Task<ProfileDocument?> LoadLocalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await LoadAsync(Path.Combine(root, "profiles", id.ToString("N") + ".json"), cancellationToken).ConfigureAwait(false);
        if (profile is not null) Policy.EnsureAllowed(profile);
        return profile;
    }

    public async Task<IReadOnlyList<ProfileDocument>> ListLocalAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(root, "profiles");
        if (!Directory.Exists(directory)) return [];
        var result = new List<ProfileDocument>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Take(128))
        {
            var profile = await LoadAsync(file, cancellationToken).ConfigureAwait(false);
            if (profile is not null && (profile.ContentRating != ContentRating.Mature18 || Policy.AcceptsMatureContent)) result.Add(profile);
        }
        // Re-evaluate after asynchronous reads in case consent was withdrawn during enumeration.
        return result.Where(p => p.ContentRating != ContentRating.Mature18 || Policy.AcceptsMatureContent).ToArray();
    }

    public async Task<ProfileDocument?> GetCachedAsync(string fingerprint, Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await LoadAsync(CachePath(fingerprint, id), cancellationToken).ConfigureAwait(false);
        if (profile is not null) Policy.EnsureAllowed(profile);
        return profile;
    }

    public async Task<IReadOnlyList<CachedProfile>> ListCachedAsync(CancellationToken cancellationToken = default)
    {
        var cacheRoot = Path.Combine(root, "cache");
        if (!Directory.Exists(cacheRoot)) return Array.Empty<CachedProfile>();
        var result = new List<CachedProfile>();
        foreach (var directory in Directory.EnumerateDirectories(cacheRoot).Take(16))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fingerprint = Path.GetFileName(directory);
            if (!Networking.PeerInvitation.IsFingerprint(fingerprint)) continue;
            foreach (var file in Directory.EnumerateFiles(directory, "*.json").Take(32))
            {
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var id)) continue;
                var profile = await LoadAsync(file, cancellationToken).ConfigureAwait(false);
                if (profile is null || profile.Id != id || (profile.ContentRating == ContentRating.Mature18 && !Policy.AcceptsMatureContent)) continue;
                result.Add(new CachedProfile(fingerprint.ToUpperInvariant(), profile, new DateTimeOffset(File.GetLastWriteTimeUtc(file))));
            }
        }
        return result.Where(item => item.Profile.ContentRating != ContentRating.Mature18 || Policy.AcceptsMatureContent).ToArray();
    }

    public async Task SaveCachedAsync(string fingerprint, ProfileDocument profile, CancellationToken cancellationToken = default)
    {
        ProfileJson.Validate(profile);
        Policy.EnsureAllowed(profile);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = CachePath(fingerprint, profile.Id);
            var peerDirectory = Path.GetDirectoryName(path)!;
            var cacheRoot = Path.Combine(root, "cache");
            if (!Directory.Exists(peerDirectory) && Directory.Exists(cacheRoot) && Directory.EnumerateDirectories(cacheRoot).Take(16).Count() >= 16)
                throw new InvalidDataException("Offline cache peer limit reached.");
            if (!File.Exists(path) && Directory.Exists(peerDirectory) && Directory.EnumerateFiles(peerDirectory, "*.json").Take(32).Count() >= 32)
                throw new InvalidDataException("Offline cache profile limit reached.");
            var previous = await LoadAsync(path, cancellationToken).ConfigureAwait(false);
            if (previous is not null && (profile.Revision < previous.Revision ||
                (profile.Revision == previous.Revision && ProfileJson.Hash(profile) != ProfileJson.Hash(previous))))
                throw new InvalidDataException("Profile revision rollback or conflict.");
            await SaveAsync(path, profile, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    // Metadata only: does not read profile contents or expose a restricted profile's name.
    public async Task<IReadOnlyList<CachedPeerSummary>> ListCachedPeersAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cacheRoot = Path.Combine(root, "cache");
            if (!Directory.Exists(cacheRoot)) return Array.Empty<CachedPeerSummary>();
            EnsureOrdinaryDirectory(cacheRoot);
            var result = new List<CachedPeerSummary>();
            foreach (var directory in Directory.EnumerateDirectories(cacheRoot).Take(17))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pin = Path.GetFileName(directory);
                if (!Networking.PeerInvitation.IsFingerprint(pin)) continue;
                EnsureOrdinaryDirectory(directory);
                var count = Directory.EnumerateFiles(directory, "*.json").Take(33)
                    .Count(file => Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var id) && id != Guid.Empty);
                result.Add(new(pin.ToUpperInvariant(), count));
                if (result.Count > 16) throw new InvalidDataException("Offline cache peer limit exceeded.");
            }
            return Array.AsReadOnly(result.ToArray());
        }
        finally { gate.Release(); }
    }

    // The caller must explicitly confirm removal and disconnect this peer first. A later reception may refill it.
    public async Task<CacheRemovalResult> RemoveCachedPeerAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        var peerDirectory = Path.GetDirectoryName(CachePath(fingerprint, Guid.Empty))!;
        var cacheRoot = Path.GetFullPath(Path.Combine(root, "cache"));
        peerDirectory = Path.GetFullPath(peerDirectory);
        if (!string.Equals(Path.GetDirectoryName(peerDirectory), cacheRoot, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFileName(peerDirectory), fingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid offline cache location.");
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(peerDirectory)) return new(0, false);
            EnsureOrdinaryDirectory(cacheRoot); EnsureOrdinaryDirectory(peerDirectory);
            var files = Directory.EnumerateFileSystemEntries(peerDirectory).Take(33).ToArray();
            if (files.Length > 32) throw new InvalidDataException("Offline cache profile limit exceeded.");
            // Validate the complete bounded deletion set before touching any file. Never delete unexpected children.
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                var attributes = File.GetAttributes(file);
                if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(file)), peerDirectory, StringComparison.OrdinalIgnoreCase) ||
                    !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                    !Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out var id) || id == Guid.Empty ||
                    (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    throw new InvalidDataException("Unexpected offline cache entry; removal refused.");
            }
            var removed = 0;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureOrdinaryDirectory(cacheRoot); EnsureOrdinaryDirectory(peerDirectory);
                File.Delete(file); removed++;
            }
            cancellationToken.ThrowIfCancellationRequested();
            EnsureOrdinaryDirectory(peerDirectory);
            // Nonrecursive deletion also refuses any concurrently introduced unexpected child.
            Directory.Delete(peerDirectory, recursive: false);
            return new(removed, true);
        }
        finally { gate.Release(); }
    }

    private static void EnsureOrdinaryDirectory(string directory)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Linked offline cache directories are not supported.");
    }

    private string CachePath(string fingerprint, Guid id)
    {
        if (!Networking.PeerInvitation.IsFingerprint(fingerprint))
            throw new InvalidDataException("Invalid peer identity.");
        return Path.Combine(root, "cache", fingerprint.ToUpperInvariant(), id.ToString("N") + ".json");
    }

    private async Task SaveAsync(string path, ProfileDocument profile, CancellationToken cancellationToken)
    {
        Policy.EnsureAllowed(profile);
        var json = ProfileJson.Export(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, json, cancellationToken).ConfigureAwait(false);
            Policy.EnsureAllowed(profile);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static async Task<ProfileDocument?> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            4096, FileOptions.Asynchronous);
        if (stream.Length > ProfileJson.MaximumBytes) throw new InvalidDataException("Stored profile exceeds the size limit.");
        var buffer = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return ProfileJson.Import(buffer);
    }
}

public sealed record CachedPeerSummary(string Fingerprint, int ProfileCount);
public sealed record CacheRemovalResult(int RemovedProfiles, bool PeerDirectoryRemoved);
