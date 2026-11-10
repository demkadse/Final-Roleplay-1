using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Contacts;

namespace FRP1.Plugin.Presentation;

public readonly record struct ActorSnapshot(string Name, uint HomeWorldId, uint? LocalEntityId = null);
public sealed record ProfileCatalogEntry(string Fingerprint, ProfileDocument Profile, DateTimeOffset ReceivedAt, bool Connected);
public enum ProfilePresentationState { NoTarget, Unknown, Restricted, Blocked, Muted, Ambiguous, Available, Offline }
public sealed record ProfileVisitView(ResolvedProfile Profile, EncounterKind Kind, DateTimeOffset At);
public sealed record ProfileDisplayMetadata(string DisplayName, string Title, ContentRating ContentRating, string NameColor, uint IconId);

// A view rechecks the current policy and local blocks on every property access.
// Callers should still request the current actor every draw, never retain raw profile copies for display.
public sealed class ResolvedProfile
{
    private readonly PresentationResolver resolver;
    internal ActorSnapshot? Actor { get; }
    internal ProfileKey? ExplicitKey { get; }

    internal ResolvedProfile(PresentationResolver resolver, ActorSnapshot? actor, ProfileKey? key)
    { this.resolver = resolver; Actor = actor; ExplicitKey = key; }

    public ProfilePresentationState State => resolver.Describe(this).State;
    public bool IsReadable => State is ProfilePresentationState.Available or ProfilePresentationState.Offline;
    public ProfileKey? Key => resolver.ReadableKey(this);
    public string DisplayName => resolver.ReadableEntry(this)?.Profile.DisplayName ?? "";
    public string Title => resolver.ReadableEntry(this)?.Profile.Title ?? "";
    public DateTimeOffset? ReceivedAt => resolver.ReadableEntry(this)?.ReceivedAt;
    public bool Connected => resolver.ReadableEntry(this)?.Connected ?? false;
    public LocalContact? Contact => Key is { } key ? resolver.Register.GetContact(key) : null;
    public CharacterKey? Character => Contact?.Binding?.Character;
    public ProfileDocument? Profile => resolver.CopyReadableProfile(this);
    public ProfileDisplayMetadata? DisplayMetadata => resolver.ReadableMetadata(this);
}

public sealed class PresentationResolver : IDisposable
{
    private readonly AudiencePolicy policy;
    private Dictionary<ProfileKey, ProfileCatalogEntry> catalog = [];
    private long version;
    private bool accessEnabled;
    private bool disposed;

    public PresentationResolver(AudiencePolicy policy, RegisterSession register)
    {
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        Register = register ?? throw new ArgumentNullException(nameof(register));
        policy.Changed += OnPolicyChanged;
    }

    public RegisterSession Register { get; }
    public long Version => Interlocked.Read(ref version) + Register.Version;
    public bool AccessEnabled => accessEnabled;

    public void SetAccess(bool enabled)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (accessEnabled == enabled) return;
        accessEnabled = enabled;
        Interlocked.Increment(ref version);
    }

    // Only feed authenticated cache entries here; network callbacks must marshal to the UI thread first.
    public void SetCatalog(IEnumerable<ProfileCatalogEntry> entries)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(entries);
        var next = new Dictionary<ProfileKey, ProfileCatalogEntry>();
        var count = 0;
        foreach (var entry in entries)
        {
            if (++count > 512 || entry is null || entry.Profile is null)
                throw new InvalidDataException("Invalid authenticated profile catalog.");
            var key = new ProfileKey(entry.Fingerprint, entry.Profile.Id);
            ContactData.ValidateKey(key);
            var profile = ProfileJson.Import(ProfileJson.Export(entry.Profile));
            if (entry.ReceivedAt < DateTimeOffset.UnixEpoch || entry.ReceivedAt > DateTimeOffset.UtcNow.AddMinutes(5) ||
                !next.TryAdd(key.Canonical(), entry with { Fingerprint = key.Canonical().Fingerprint, Profile = profile }))
                throw new InvalidDataException("Invalid authenticated profile catalog.");
        }
        catalog = next;
        Interlocked.Increment(ref version);
    }

    public ResolvedProfile Resolve(ActorSnapshot? actor) => new(this, actor, null);

    // Explicitly reading a trusted cache profile does not assert a verified game-character binding.
    public ResolvedProfile GetProfile(ProfileKey key)
    {
        ContactData.ValidateKey(key);
        return new ResolvedProfile(this, null, key.Canonical());
    }

    public IReadOnlyList<ResolvedProfile> Search(string query = "", uint? homeWorldId = null,
        bool favoritesOnly = false, RelationshipKind? relationship = null, bool includeOffline = true)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Length > 160 || (relationship.HasValue && !Enum.IsDefined(relationship.Value)))
            throw new ArgumentException("Invalid local search.");
        if (!CanPresent()) return Array.Empty<ResolvedProfile>();
        query = query.Trim();
        var result = new List<(string Name, ResolvedProfile View)>();
        foreach (var pair in catalog)
        {
            if (!Readable(StateForKey(pair.Key))) continue;
            var entry = pair.Value;
            if (!includeOffline && !entry.Connected) continue;
            var contact = Register.GetContact(pair.Key);
            if (favoritesOnly && contact?.Favorite != true) continue;
            if (relationship.HasValue && contact?.Relationship != relationship.Value) continue;
            if (homeWorldId.HasValue && contact?.Binding?.Character.HomeWorldId != homeWorldId.Value) continue;
            if (query.Length != 0 && !entry.Profile.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !(contact?.Alias.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) &&
                !(contact?.Binding?.Character.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)) continue;
            result.Add((entry.Profile.DisplayName, new ResolvedProfile(this, null, pair.Key)));
        }
        // Recheck before returning in case another thread withdrew the shared policy during search.
        return Array.AsReadOnly(result.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.View).Where(view => view.IsReadable).ToArray());
    }

    public IReadOnlyList<ProfileVisitView> History()
    {
        if (!CanPresent()) return Array.Empty<ProfileVisitView>();
        return Array.AsReadOnly(Register.Snapshot.Encounters.OrderByDescending(e => e.At)
            .Where(e => Readable(StateForKey(e.Profile.Canonical())))
            .Select(e => new ProfileVisitView(GetProfile(e.Profile), e.Kind, e.At)).ToArray());
    }

    internal (ProfilePresentationState State, ProfileKey? Key) Describe(ResolvedProfile view)
    {
        if (!CanPresent()) return (ProfilePresentationState.Restricted, null);
        if (view.ExplicitKey is { } explicitKey) return (StateForKey(explicitKey), explicitKey);
        if (view.Actor is not { } actor || string.IsNullOrWhiteSpace(actor.Name) || actor.HomeWorldId == 0)
            return (ProfilePresentationState.NoTarget, null);
        if (actor.Name.Length > 80 || actor.HomeWorldId > 65535)
            return (ProfilePresentationState.Unknown, null);
        IReadOnlyList<ProfileKey> bindings;
        try { bindings = Register.FindBindings(new CharacterKey(actor.Name, actor.HomeWorldId)); }
        catch (InvalidDataException) { return (ProfilePresentationState.Unknown, null); }
        if (bindings.Count == 0) return (ProfilePresentationState.Unknown, null);
        if (bindings.Count != 1) return (ProfilePresentationState.Ambiguous, null);
        return (StateForKey(bindings[0]), bindings[0]);
    }

    internal ProfileKey? ReadableKey(ResolvedProfile view)
    {
        var descriptor = Describe(view);
        return Readable(descriptor.State) ? descriptor.Key : null;
    }

    internal ProfileCatalogEntry? ReadableEntry(ResolvedProfile view) =>
        ReadableKey(view) is { } key && catalog.TryGetValue(key, out var entry) && Allowed(entry.Profile) ? entry : null;

    internal ProfileDocument? CopyReadableProfile(ResolvedProfile view)
    {
        var entry = ReadableEntry(view);
        if (entry is null) return null;
        try { return ProfileJson.Import(ProfileJson.Export(entry.Profile), policy); }
        catch (InvalidDataException) { return null; }
    }

    internal ProfileDisplayMetadata? ReadableMetadata(ResolvedProfile view)
    {
        var initialVersion = Version;
        var initialAudience = policy.Snapshot;
        var before = Describe(view);
        if (!Readable(before.State) || before.Key is not { } key || !catalog.TryGetValue(key, out var entry)) return null;
        // SetCatalog already validated and detached the complete profile. These immutable scalar fields need no
        // JSON round trip or portrait decoding; callers must still recheck the live view before displaying them.
        var profile = entry.Profile;
        var metadata = new ProfileDisplayMetadata(profile.DisplayName, profile.Title, profile.ContentRating,
            profile.AppearanceStyle.NameColor, profile.AppearanceStyle.IconId);
        var after = Describe(view);
        if (!Readable(after.State) || after.Key != key || !catalog.TryGetValue(key, out var current) ||
            !ReferenceEquals(current, entry) || !Allowed(profile) || initialVersion != Version || initialAudience != policy.Snapshot)
            return null;
        return metadata;
    }

    private ProfilePresentationState StateForKey(ProfileKey key)
    {
        if (!CanPresent()) return ProfilePresentationState.Restricted;
        if (Register.IsPeerBlocked(key.Fingerprint)) return ProfilePresentationState.Blocked;
        if (Register.GetContact(key)?.Muted == true) return ProfilePresentationState.Muted;
        if (!catalog.TryGetValue(key, out var entry)) return ProfilePresentationState.Unknown;
        if (!Allowed(entry.Profile)) return ProfilePresentationState.Restricted;
        return entry.Connected ? ProfilePresentationState.Available : ProfilePresentationState.Offline;
    }

    private bool CanPresent() => !disposed && accessEnabled && Register.IsLoaded && policy.Snapshot.Selection != AudienceSelection.Unspecified;

    private bool Allowed(ProfileDocument profile)
    {
        try { policy.EnsureAllowed(profile); return true; }
        catch (InvalidDataException) { return false; }
    }

    private static bool Readable(ProfilePresentationState state) => state is ProfilePresentationState.Available or ProfilePresentationState.Offline;
    private void OnPolicyChanged(object? sender, EventArgs args) => Interlocked.Increment(ref version);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        accessEnabled = false;
        catalog.Clear();
        policy.Changed -= OnPolicyChanged;
        Interlocked.Increment(ref version);
    }
}
