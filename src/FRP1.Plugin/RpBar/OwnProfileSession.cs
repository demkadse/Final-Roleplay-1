using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;

namespace FRP1.Plugin.RpBar;

// Public state is observed and advanced by the UI thread through ObserveContext/Poll.
// Workers perform I/O and return private snapshots; callers receive separate copies.
public sealed class OwnProfileSession : IDisposable
{
    private readonly ProfileStore store;
    private readonly RpBarSettingsStore settingsStore;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim writer = new(1, 1);
    private readonly List<Operation> operations = [];
    private readonly List<CatalogLoad> loads = [];
    private readonly List<ProfileDocument> profiles = [];
    private Task<RpBarPreferences>? settingsLoad;
    private RpBarPreferences settings = new();
    private RpBarPreferences desiredSettings = new();
    private RpBarPreferences committedSettings = new();
    private ulong contentId;
    private bool observedAccess;
    private bool editorBlocked;
    private bool disposed;
    private bool settingsFailed;
    private bool preferencesDirty;
    private bool preferencesFailed;
    private long contextVersion;
    private long preferencesVersion;
    private long settingsLoadPreferencesVersion;
    private IReadOnlyDictionary<ulong, Guid> settingsLoadBindings = new Dictionary<ulong, Guid>();
    private long saveDue;
    private int policyVersion;
    private int observedPolicyVersion;

    public OwnProfileSession(ProfileStore store, RpBarSettingsStore settingsStore)
    {
        this.store = store;
        this.settingsStore = settingsStore;
        store.Policy.Changed += PolicyChanged;
        StartSettingsLoad();
    }

    public bool AccessReady => Volatile.Read(ref observedAccess) && store.Policy.Snapshot.Selection != AudienceSelection.Unspecified;
    public bool HasCharacter => contentId != 0;
    public bool SettingsLoaded { get; private set; }
    public bool IsLoading => settingsLoad is not null || loads.Any(load => load.Context == contextVersion);
    public bool IsBusy => operations.Any(operation => operation.Kind != OperationKind.Layout);
    public string? ErrorMessage { get; private set; }
    public long Version { get; private set; }
    public Task DisposalCompletion { get; private set; } = Task.CompletedTask;
    public RpBarPreferences Settings => RpBarPreferences.Snapshot(settings);

    // Callers may cache these defensive snapshots until Version changes.
    public IReadOnlyList<ProfileDocument> Profiles => AccessReady ? Array.AsReadOnly(profiles.Where(Allowed).Select(Copy).ToArray()) : Array.AsReadOnly(Array.Empty<ProfileDocument>());
    public ProfileDocument? ActiveProfile
    {
        get
        {
            if (!AccessReady || !HasCharacter || !SettingsLoaded ||
                !settings.ActiveProfiles.TryGetValue(contentId, out var id)) return null;
            var profile = profiles.FirstOrDefault(profile => profile.Id == id && Allowed(profile));
            return profile is null ? null : Copy(profile);
        }
    }

    public void ObserveContext(ulong currentContentId, bool accessReady)
    {
        ThrowIfDisposed();
        if (contentId == currentContentId && observedAccess == accessReady && observedPolicyVersion == Volatile.Read(ref policyVersion)) return;
        contentId = currentContentId;
        Volatile.Write(ref observedAccess, accessReady);
        observedPolicyVersion = Volatile.Read(ref policyVersion);
        contextVersion++;
        profiles.Clear();
        Version++;
        if (AccessReady) StartCatalogLoad();
    }

    public void SetEditorBlocked(bool blocked) => editorBlocked = blocked;

    public void Reload()
    {
        ThrowIfDisposed();
        ErrorMessage = null;
        contextVersion++;
        profiles.Clear();
        Version++;
        if (AccessReady) StartCatalogLoad();
        if (settingsFailed && settingsLoad is null) StartSettingsLoad();
    }

    public void Retry()
    {
        ThrowIfDisposed();
        if (preferencesFailed)
        {
            preferencesFailed = false;
            saveDue = 0;
            ErrorMessage = null;
        }
        else Reload();
    }

    public void SetPreferences(RpBarPreferences value)
    {
        ThrowIfDisposed();
        if (!SettingsLoaded) throw new InvalidOperationException("Die Leisten-Einstellungen werden noch geladen.");
        // Layout edits cannot discard character associations from an older UI snapshot.
        var snapshot = RpBarPreferences.Snapshot(value with { ActiveProfiles = settings.ActiveProfiles });
        if (SameLayout(settings, snapshot)) return;
        settings = snapshot;
        Volatile.Write(ref desiredSettings, snapshot);
        preferencesVersion++;
        preferencesDirty = true;
        preferencesFailed = false;
        saveDue = Environment.TickCount64 + 300;
        ErrorMessage = null;
        Version++;
    }

    public Task<ProfileDocument> SaveEditorAsync(ProfileDocument draft, long expectedRevision, string? expectedHash,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureAccess();
        var snapshot = Copy(draft);
        store.Policy.EnsureAllowed(snapshot);
        if (expectedRevision < 0 || (expectedRevision == 0 && !string.IsNullOrEmpty(expectedHash)) ||
            (expectedRevision > 0 && !FRP1.Core.Networking.PeerInvitation.IsFingerprint(expectedHash)))
            throw new ArgumentException("Ungültige Entwurfsversion.");
        var task = RunAsync(async token =>
        {
            EnsureAccess();
            var current = await store.LoadLocalAsync(snapshot.Id, token).ConfigureAwait(false);
            if (expectedRevision == 0 ? current is not null : current is null || current.Revision != expectedRevision ||
                !string.Equals(ProfileJson.Hash(current), expectedHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Das Profil wurde inzwischen geändert. Bitte neu laden.");
            var next = snapshot with { Revision = current is null ? 1 : checked(current.Revision + 1) };
            EnsureAccess();
            store.Policy.EnsureAllowed(next);
            token.ThrowIfCancellationRequested();
            await store.SaveLocalAsync(next, token).ConfigureAwait(false);
            store.Policy.EnsureAllowed(next);
            return new Result(next);
        }, cancellationToken);
        operations.Add(new Operation(contextVersion, Volatile.Read(ref policyVersion), OperationKind.Profile, preferencesVersion, task));
        return ProfileResultAsync(task);
    }

    public Task SelectActiveAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        EnsureBarMutation();
        var selected = profiles.FirstOrDefault(profile => profile.Id == profileId && Allowed(profile)) ??
            throw new InvalidOperationException("Dieses Profil ist derzeit nicht verfügbar.");
        var originalContentId = contentId;
        var selectedSnapshot = Copy(selected);
        var task = RunAsync(async token =>
        {
            EnsureAccess();
            var current = await store.LoadLocalAsync(selectedSnapshot.Id, token).ConfigureAwait(false) ??
                throw new InvalidOperationException("Dieses Profil ist derzeit nicht verfügbar.");
            store.Policy.EnsureAllowed(current);
            var associations = new Dictionary<ulong, Guid>(committedSettings.ActiveProfiles) { [originalContentId] = current.Id };
            var next = RpBarPreferences.Snapshot(Volatile.Read(ref desiredSettings) with { ActiveProfiles = associations });
            EnsureAccess();
            await settingsStore.SaveAsync(next, token).ConfigureAwait(false);
            Volatile.Write(ref committedSettings, next);
            return new Result(current);
        }, cancellationToken);
        operations.Add(new Operation(contextVersion, Volatile.Read(ref policyVersion), OperationKind.Selection, preferencesVersion, task));
        return AwaitResultAsync(task);
    }

    public Task<ProfileDocument> UpdateStatusAsync(RpStatus status, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        return UpdateQuickAsync(profile => profile with { Status = status }, cancellationToken);
    }

    public Task<ProfileDocument> UpdateAvailabilityAsync(RpAvailability availability, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(availability)) throw new ArgumentOutOfRangeException(nameof(availability));
        return UpdateQuickAsync(profile => profile with { Availability = availability }, cancellationToken);
    }

    private Task<ProfileDocument> UpdateQuickAsync(Func<ProfileDocument, ProfileDocument> change, CancellationToken cancellationToken)
    {
        EnsureBarMutation();
        var original = ActiveProfile ?? throw new InvalidOperationException("Bitte zuerst ein verfügbares Profil aktivieren.");
        var expectedHash = ProfileJson.Hash(original);
        var changed = change(original);
        if (ProfileJson.Hash(changed) == expectedHash) return Task.FromResult(Copy(original));
        return SaveEditorAsync(changed, original.Revision, expectedHash, cancellationToken);
    }

    private Task<Result> RunAsync(Func<CancellationToken, Task<Result>> operation, CancellationToken callerToken)
    {
        return Task.Run(async () =>
        {
            using var source = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, callerToken);
            var token = source.Token;
            await writer.WaitAsync(token).ConfigureAwait(false);
            try { return await operation(token).ConfigureAwait(false); }
            finally { writer.Release(); }
        }, lifetime.Token);
    }

    public void Poll()
    {
        if (disposed) return;
        if (observedPolicyVersion != Volatile.Read(ref policyVersion)) ObserveContext(contentId, observedAccess);
        if (settingsLoad is { IsCompleted: true } completedSettings &&
            !operations.Any(operation => operation.Kind is OperationKind.Selection or OperationKind.Layout && !operation.Task.IsCompleted))
        {
            settingsLoad = null;
            try
            {
                var loaded = RpBarPreferences.Snapshot(completedSettings.GetAwaiter().GetResult());
                var associations = new Dictionary<ulong, Guid>(loaded.ActiveProfiles);
                // A retry can discover previously unknown associations. Keep those while
                // retaining any explicit binding that committed after this read started.
                foreach (var pair in Volatile.Read(ref committedSettings).ActiveProfiles)
                    if (!settingsLoadBindings.TryGetValue(pair.Key, out var original) || original != pair.Value)
                        associations[pair.Key] = pair.Value;
                var preserveLocalLayout = preferencesDirty || preferencesVersion != settingsLoadPreferencesVersion;
                settings = RpBarPreferences.Snapshot((preserveLocalLayout ? settings : loaded) with { ActiveProfiles = associations });
                Volatile.Write(ref desiredSettings, settings);
                Volatile.Write(ref committedSettings, RpBarPreferences.Snapshot(loaded with { ActiveProfiles = associations }));
                settingsFailed = false;
                ErrorMessage = null;
            }
            catch (Exception ex) when (Expected(ex))
            {
                settingsFailed = true;
                ErrorMessage = "Leisten-Einstellungen konnten nicht geladen werden. Neutraler Zustand; erneut versuchen oder ausdrücklich ändern.";
            }
            SettingsLoaded = true;
            Version++;
        }
        foreach (var load in loads.Where(item => item.Task.IsCompleted).ToArray())
        {
            loads.Remove(load);
            try
            {
                var result = load.Task.GetAwaiter().GetResult();
                if (load.Context == contextVersion && load.Policy == Volatile.Read(ref policyVersion) && AccessReady)
                {
                    profiles.Clear();
                    profiles.AddRange(result.Where(Allowed).Select(Copy));
                    Version++;
                }
            }
            catch (Exception ex) when (Expected(ex))
            { if (load.Context == contextVersion) ErrorMessage = "Lokale Profile konnten nicht geladen werden. Bitte erneut versuchen."; }
        }
        foreach (var operation in operations.Where(item => item.Task.IsCompleted).ToArray())
        {
            operations.Remove(operation);
            try
            {
                var result = operation.Task.GetAwaiter().GetResult();
                if (operation.Kind is OperationKind.Selection or OperationKind.Layout)
                {
                    settings = RpBarPreferences.Snapshot(settings with { ActiveProfiles = Volatile.Read(ref committedSettings).ActiveProfiles });
                    Volatile.Write(ref desiredSettings, settings);
                    if (operation.Kind == OperationKind.Layout && operation.Choice == preferencesVersion) preferencesDirty = false;
                }
                if (operation.Context == contextVersion && operation.Policy == Volatile.Read(ref policyVersion) && AccessReady && result.Profile is { } profile && Allowed(profile))
                {
                    var index = profiles.FindIndex(item => item.Id == profile.Id);
                    if (index < 0) profiles.Add(Copy(profile)); else profiles[index] = Copy(profile);
                }
                else if (result.Profile is not null && AccessReady && !loads.Any(item => item.Context == contextVersion)) StartCatalogLoad();
                if (operation.Context == contextVersion && operation.Policy == Volatile.Read(ref policyVersion)) ErrorMessage = null;
                Version++;
            }
            catch (Exception ex) when (Expected(ex))
            {
                if (operation.Kind == OperationKind.Layout && operation.Choice == preferencesVersion) preferencesFailed = true;
                if (operation.Context == contextVersion)
                    ErrorMessage = "Die Änderung konnte nicht gespeichert werden. Bei einem Versionskonflikt bitte Profile neu laden; der Entwurf bleibt erhalten.";
            }
        }
        if (SettingsLoaded && settingsLoad is null && preferencesDirty && !preferencesFailed && Environment.TickCount64 >= saveDue &&
            !operations.Any(item => item.Kind == OperationKind.Layout))
        {
            var task = RunAsync(async token =>
            {
                var next = RpBarPreferences.Snapshot(Volatile.Read(ref desiredSettings) with { ActiveProfiles = committedSettings.ActiveProfiles });
                await settingsStore.SaveAsync(next, token).ConfigureAwait(false);
                Volatile.Write(ref committedSettings, next);
                return new Result(null);
            }, CancellationToken.None);
            operations.Add(new Operation(contextVersion, Volatile.Read(ref policyVersion), OperationKind.Layout, preferencesVersion, task));
        }
    }

    private void StartCatalogLoad()
    {
        var token = lifetime.Token;
        loads.Add(new CatalogLoad(contextVersion, Volatile.Read(ref policyVersion), Task.Run(async () =>
        {
            await writer.WaitAsync(token).ConfigureAwait(false);
            try { return await store.ListLocalAsync(token).ConfigureAwait(false); }
            finally { writer.Release(); }
        }, token)));
    }

    private void StartSettingsLoad()
    {
        var token = lifetime.Token;
        settingsLoadPreferencesVersion = preferencesVersion;
        settingsLoadBindings = Volatile.Read(ref committedSettings).ActiveProfiles;
        settingsLoad = Task.Run(async () =>
        {
            await writer.WaitAsync(token).ConfigureAwait(false);
            try { return await settingsStore.LoadAsync(token).ConfigureAwait(false); }
            finally { writer.Release(); }
        }, token);
    }

    private async Task<ProfileDocument> ProfileResultAsync(Task<Result> task)
    {
        var result = (await task.ConfigureAwait(false)).Profile ?? throw new InvalidOperationException("Kein Profil verfügbar.");
        EnsureAccess();
        store.Policy.EnsureAllowed(result);
        return Copy(result);
    }

    private static async Task AwaitResultAsync(Task<Result> task) => await task.ConfigureAwait(false);
    private bool Allowed(ProfileDocument profile) => profile.ContentRating != ContentRating.Mature18 || store.Policy.AcceptsMatureContent;
    private static ProfileDocument Copy(ProfileDocument profile) => ProfileJson.Import(ProfileJson.Export(profile));
    private void PolicyChanged(object? sender, EventArgs args) => Interlocked.Increment(ref policyVersion);

    private void EnsureAccess()
    {
        ThrowIfDisposed();
        if (!AccessReady) throw new InvalidOperationException("Profilzugriff ist derzeit gesperrt.");
    }

    private void EnsureBarMutation()
    {
        EnsureAccess();
        if (!HasCharacter || !SettingsLoaded || editorBlocked || IsBusy || IsLoading)
            throw new InvalidOperationException("Bitte den Entwurf zuerst speichern oder verwerfen und laufende Vorgänge abwarten.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
    private static bool Expected(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
        InvalidOperationException or OperationCanceledException or OverflowException;
    private static bool SameLayout(RpBarPreferences a, RpBarPreferences b) => a.Visible == b.Visible && a.Locked == b.Locked && a.ShowHints == b.ShowHints &&
        a.Scale == b.Scale && a.PositionX == b.PositionX && a.PositionY == b.PositionY;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        store.Policy.Changed -= PolicyChanged;
        lifetime.Cancel();
        var pending = operations.Select(item => (Task)item.Task).Concat(loads.Select(item => (Task)item.Task)).ToList();
        if (settingsLoad is not null) pending.Add(settingsLoad);
        DisposalCompletion = ObserveDisposalAsync(pending);
    }

    private async Task ObserveDisposalAsync(IEnumerable<Task> tasks)
    {
        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch (Exception) { /* Observe unload failures without exposing character or profile data. */ }
        finally { lifetime.Dispose(); }
    }

    private enum OperationKind { Profile, Selection, Layout }
    private sealed record Result(ProfileDocument? Profile);
    private sealed record Operation(long Context, int Policy, OperationKind Kind, long Choice, Task<Result> Task);
    private sealed record CatalogLoad(long Context, int Policy, Task<IReadOnlyList<ProfileDocument>> Task);
}
