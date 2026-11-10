using FRP1.Core.Profiles;

namespace FRP1.Core.Persistence;

// StoredAtUtc is the local cache file's write time, not a peer-supplied timestamp.
public sealed record CachedProfile(string Fingerprint, ProfileDocument Profile, DateTimeOffset StoredAtUtc);
