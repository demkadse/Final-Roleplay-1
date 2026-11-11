using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using FRP1.Plugin.Contacts;
using FRP1.Plugin.Networking;
using FRP1.Plugin.Presentation;
using FRP1.Plugin.RpBar;

namespace FRP1.Plugin.UiSocial;

public sealed record VisibleActorSnapshot(ActorSnapshot Actor, Vector3 Position, float Distance, string HomeWorldName = "");

// Framework callbacks publish game-only immutable snapshots. All register/resolver/UI work stays on Tick/Draw.
public sealed partial class SocialUiModule : IDisposable
{
    private readonly ProfileStore profiles;
    private readonly OwnProfileSession own;
    private readonly NetworkSession network;
    private readonly RegisterSession register;
    private readonly PresentationResolver resolver;
    private readonly ITargetManager targets;
    private readonly IObjectTable objects;
    private readonly IContextMenu contextMenu;
    private readonly IFramework framework;
    private readonly ITextureProvider? textures;
    private readonly Action<Func<ResolvedProfile?>> openForeign;
    private readonly Func<bool>? sceneReady;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ConcurrentQueue<ProfileKey> openRequests = new();
    private readonly List<UiAction> actions = [];
    private readonly Dictionary<string, bool> pendingBlocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly SocialTargetWindow targetWindow;
    private readonly SocialNotesWindow notesWindow;
    private readonly SocialUiOptionsStore? optionsStore;
    private SceneSnapshot scene = new([], null, null);
    private SocialUiOptions options = new();
    private Task<SocialUiOptions>? optionsLoad;
    private Task? optionsSave;
    private long optionsVersion;
    private long optionsLoadVersion;
    private long savingOptionsVersion;
    private long optionsSaveDue;
    private bool optionsDirty;
    private bool optionsSaveFailed;
    private bool optionsLoadFailed;
    private Task<IReadOnlyList<CachedProfile>>? cacheLoad;
    private long cacheGeneration;
    private long cacheEpoch;
    private long cacheReadEpoch;
    private long generation;
    private long nextCacheRead;
    private IReadOnlyList<ProfileCatalogEntry> cachedEntries = [];
    private bool accessReady;
    private ulong contentId;
    private bool initializedRequested;
    private bool overlaysVisible = true;
    private bool disposed;
    private Guid? publication;
    private string? publishedHash;
    private string? feedback;
    private ProfileKey? selectedContact;
    private string networkSignature = "";
    private long contextMapVersion = -1;
    private IReadOnlyDictionary<string, ProfileKey> contextProfiles = new Dictionary<string, ProfileKey>(StringComparer.OrdinalIgnoreCase);

    public SocialUiModule(ProfileStore profileStore, OwnProfileSession ownProfiles, NetworkSession networkSession,
        RegisterSession registerSession, PresentationResolver presentationResolver, ITargetManager targetManager,
        IObjectTable objectTable, IContextMenu contextMenus, IFramework gameFramework,
        Action<Func<ResolvedProfile?>> openForeignProfile, string? configurationDirectory = null, ITextureProvider? textureProvider = null,
        Func<bool>? isSceneReady = null)
    {
        profiles = profileStore; own = ownProfiles; network = networkSession; register = registerSession; resolver = presentationResolver;
        targets = targetManager; objects = objectTable; contextMenu = contextMenus; framework = gameFramework;
        openForeign = openForeignProfile; textures = textureProvider; sceneReady = isSceneReady;
        targetWindow = new SocialTargetWindow(this);
        notesWindow = new SocialNotesWindow(this);
        Windows = Array.AsReadOnly<Window>([targetWindow, notesWindow]);
        if (configurationDirectory is not null)
        {
            optionsStore = new SocialUiOptionsStore(configurationDirectory);
            var token = lifetime.Token;
            optionsLoadVersion = optionsVersion;
            optionsLoad = Task.Run(() => optionsStore.LoadAsync(token), token);
        }
        framework.Update += CaptureScene;
        contextMenu.OnMenuOpened += OnContextMenu;
    }

    public IReadOnlyList<Window> Windows { get; }
    public IReadOnlyList<VisibleActorSnapshot> ActorSnapshots => Volatile.Read(ref scene).Actors;
    public RpBarSceneSnapshot BarScene
    {
        get
        {
            var captured = Volatile.Read(ref scene).BarContext ?? new(false, false, null, null);
            return captured with { SceneValid = captured.SceneValid && AccessReady && (sceneReady?.Invoke() ?? true) };
        }
    }
    public SocialUiOptions Options => options;
    public bool AccessReady => !disposed && accessReady && contentId != 0 &&
        profiles.Policy.Snapshot.Selection != FRP1.Core.Safety.AudienceSelection.Unspecified;
    public string? Feedback => feedback;
    public Task DisposalCompletion { get; private set; } = Task.CompletedTask;

    // Presentation only: never changes consent, publication, stored visibility or note drafts.
    public void SetOverlaysVisible(bool visible) => Volatile.Write(ref overlaysVisible, visible);

    public void SetContext(ulong currentContentId, bool ready)
    {
        if (disposed) return;
        ready = ready && currentContentId != 0;
        if (contentId == currentContentId && accessReady == ready) return;
        if (publication.HasValue) { network.SetPublishedProfile(null); publication = null; publishedHash = null; }
        contentId = currentContentId; accessReady = ready; generation++;
        Volatile.Write(ref scene, new SceneSnapshot([], null, null));
        selectedContact = null; ClearContactDraft();
        ClearPinnedTarget(); cacheRemovalChoice = null; cacheRemovalConfirmed = false;
        while (openRequests.TryDequeue(out _)) { }
        notesWindow.IsOpen = false;
        Volatile.Write(ref contextProfiles, new Dictionary<string, ProfileKey>(StringComparer.OrdinalIgnoreCase));
        cachedEntries = []; resolver.SetCatalog([]); resolver.SetAccess(ready);
        network.ObserveContext(currentContentId, ready);
        nextCacheRead = 0;
    }

    public void Tick()
    {
        if (disposed) return;
        register.Poll();
        resolver.SetAccess(AccessReady);
        PollOptions();
        PollCacheManagement();
        targetWindow.ApplyVisibility();
        foreach (var action in actions.Where(a => a.Task.IsCompleted).ToArray())
        {
            actions.Remove(action);
            try { action.Task.GetAwaiter().GetResult(); if (action.Generation == generation) action.OnSuccess?.Invoke(); }
            catch (Exception ex) when (Expected(ex)) { feedback = T("Die Aktion konnte nicht sicher abgeschlossen werden. Bitte erneut versuchen.", "The action could not complete safely. Please retry."); }
        }
        if (!AccessReady) { ClearPinnedTarget(); return; }
        if (pinnedTarget is { } pinned && (!resolver.Resolve(pinned).IsReadable || resolver.Resolve(pinned).Key != pinnedTargetKey)) ClearPinnedTarget();
        if (selectedContact is { } selected && !resolver.GetProfile(selected).IsReadable)
        { selectedContact = null; ClearContactDraft(); notesWindow.IsOpen = false; }
        if (!initializedRequested)
        {
            initializedRequested = true;
            Queue(() => network.InitializeAsync(lifetime.Token), () => nextCacheRead = 0);
        }
        while (register.TryDequeueNetworkBlockChange(out var block)) pendingBlocks[block.Fingerprint] = block.Blocked;
        if (network.IsInitialized)
            foreach (var block in pendingBlocks.ToArray())
            {
                if (!network.Peers.Any(p => string.Equals(p.Invitation.Fingerprint, block.Key, StringComparison.OrdinalIgnoreCase))) continue;
                if (Queue(() => network.SetBlockedAsync(block.Key, block.Value, lifetime.Token))) pendingBlocks.Remove(block.Key);
            }
        var received = false;
        while (network.TryDequeueReceived(out _)) received = true;
        if (received) nextCacheRead = 0;
        var signature = string.Join('|', network.Peers.Select(p => p.Invitation.Fingerprint + p.Blocked)) +
            string.Join('|', network.ConnectedPeerFingerprints.Order(StringComparer.Ordinal));
        if (signature != networkSignature) { networkSignature = signature; RefreshCatalog(); }
        if (cacheLoad is { IsCompleted: true } completed)
        {
            cacheLoad = null;
            try
            {
                var cache = completed.GetAwaiter().GetResult();
                if (cacheGeneration == generation && cacheReadEpoch == cacheEpoch && AccessReady)
                {
                    cachedEntries = cache.Select(item => new ProfileCatalogEntry(item.Fingerprint, item.Profile, item.StoredAtUtc, false)).ToArray();
                    RefreshCatalog();
                }
            }
            catch (Exception ex) when (Expected(ex)) { feedback = T("Der lokale Profilcache konnte nicht gelesen werden.", "The local profile cache could not be read."); }
        }
        if (cacheLoad is null && Environment.TickCount64 >= nextCacheRead)
        {
            cacheGeneration = generation;
            cacheReadEpoch = cacheEpoch;
            var token = lifetime.Token;
            cacheLoad = Task.Run(() => profiles.ListCachedAsync(token), token);
            nextCacheRead = Environment.TickCount64 + 3000;
        }
        while (openRequests.TryDequeue(out var key))
            if (resolver.GetProfile(key).IsReadable) Open(key);
        if (publication.HasValue)
        {
            var active = own.ActiveProfile;
            if (!network.IsRunning || active is null || active.Id != publication.Value)
            {
                if (network.IsRunning) network.SetPublishedProfile(null);
                publication = null; publishedHash = null;
            }
            else
            {
                var hash = ProfileJson.Hash(active);
                if (hash != publishedHash) { network.SetPublishedProfile(active); publishedHash = hash; }
            }
        }
        if (contextMapVersion != resolver.Version)
        {
            var map = new Dictionary<string, ProfileKey>(StringComparer.OrdinalIgnoreCase);
            foreach (var contact in register.Snapshot.Contacts)
            {
                if (contact.Binding is not { Confirmed: true } binding) continue;
                var view = resolver.Resolve(new ActorSnapshot(binding.Character.Name, binding.Character.HomeWorldId));
                if (view.IsReadable && view.Key is { } key) map[ContactData.CharacterIndex(binding.Character)] = key;
            }
            Volatile.Write(ref contextProfiles, map);
            contextMapVersion = resolver.Version;
        }
    }

    private void RefreshCatalog()
    {
        var peers = network.Peers.ToDictionary(p => p.Invitation.Fingerprint, StringComparer.OrdinalIgnoreCase);
        var connected = network.ConnectedPeerFingerprints.ToHashSet(StringComparer.OrdinalIgnoreCase);
        resolver.SetCatalog(cachedEntries.Where(e => peers.TryGetValue(e.Fingerprint, out var peer) && !peer.Blocked)
            .Select(e => e with { Connected = connected.Contains(e.Fingerprint) }));
    }

    private void CaptureScene(IFramework _)
    {
        if (disposed) return;
        try
        {
            var local = objects.LocalPlayer;
            var origin = local?.Position;
            var target = targets.Target;
            var actors = objects.OfType<IPlayerCharacter>().Take(512).Select(player =>
            {
                var actor = Snapshot(player);
                var position = player.Position;
                return actor is null ? null : new VisibleActorSnapshot(actor.Value, position,
                    origin.HasValue ? Vector3.Distance(origin.Value, position) : float.PositiveInfinity, player.HomeWorld.Value.Name.ToString());
            }).OfType<VisibleActorSnapshot>().ToArray();
            var localActor = Snapshot(local);
            Volatile.Write(ref scene, new SceneSnapshot(Array.AsReadOnly(actors), Snapshot(target),
                Snapshot(targets.MouseOverTarget ?? targets.MouseOverNameplateTarget),
                new RpBarSceneSnapshot(localActor.HasValue && (sceneReady?.Invoke() ?? true), target is not null, Snapshot(target), localActor)));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        { Volatile.Write(ref scene, new SceneSnapshot([], null, null)); }
    }

    private static ActorSnapshot? Snapshot(IGameObject? value) => value is IPlayerCharacter player && player.IsValid()
        ? new ActorSnapshot(player.Name.TextValue, player.HomeWorld.RowId, player.EntityId) : null;

    private void OnContextMenu(IMenuOpenedArgs args)
    {
        // Native menu events use an immutable UI-published map, never mutable register dictionaries.
        if (!AccessReady || args.Target is not MenuTargetDefault target) return;
        var actor = Snapshot(target.TargetObject);
        if (actor is null) return;
        var map = Volatile.Read(ref contextProfiles);
        if (!map.TryGetValue(ContactData.CharacterIndex(new CharacterKey(actor.Value.Name, actor.Value.HomeWorldId)), out var key)) return;
        args.AddMenuItem(new MenuItem
        {
            Name = T("FRP1-Profil öffnen", "Open FRP1 profile"),
            OnClicked = _ => { if (openRequests.Count < 8) openRequests.Enqueue(key); },
        });
    }

    private bool Queue(Func<Task> action, Action? onSuccess = null)
    {
        if (disposed || actions.Count >= 16) { feedback = T("Bitte die laufenden Aktionen abschließen lassen.", "Please wait for pending actions to complete."); return false; }
        try { actions.Add(new UiAction(action(), onSuccess, generation)); return true; }
        catch (Exception ex) when (Expected(ex)) { feedback = T("Die Aktion ist momentan nicht möglich. Einstellungen und Freigaben prüfen.", "The action is currently unavailable. Check settings and permissions."); return false; }
    }

    private void Open(ProfileKey key)
    {
        if (!AccessReady || !resolver.GetProfile(key).IsReadable) return;
        openForeign(() => AccessReady ? resolver.GetProfile(key) : null);
        if (!register.IsBusy) Queue(() => register.RecordEncounterAsync(key, resolver.GetProfile(key).Character, EncounterKind.ProfileOpened));
    }

    private void PollOptions()
    {
        if (optionsLoad is { IsCompleted: true } completed)
        {
            optionsLoad = null;
            try { var loaded = completed.GetAwaiter().GetResult(); if (optionsVersion == optionsLoadVersion) options = loaded; }
            catch (Exception ex) when (Expected(ex)) { if (optionsVersion == optionsLoadVersion) optionsLoadFailed = true; }
        }
        if (optionsSave is { IsCompleted: true } saved)
        {
            optionsSave = null;
            try { saved.GetAwaiter().GetResult(); if (optionsVersion == savingOptionsVersion) { optionsDirty = false; optionsSaveFailed = false; } }
            catch (Exception ex) when (Expected(ex)) { optionsSaveFailed = true; optionsDirty = true; }
        }
        if (optionsStore is not null && optionsLoad is null && optionsSave is null && optionsDirty && !optionsSaveFailed && Environment.TickCount64 >= optionsSaveDue)
        {
            var snapshot = options; savingOptionsVersion = optionsVersion; var token = lifetime.Token;
            optionsSave = Task.Run(() => optionsStore.SaveAsync(snapshot, token), token);
        }
    }

    internal void SetOptions(SocialUiOptions next)
    {
        SocialUiOptionsStore.Validate(next);
        if (next == options) return;
        options = next; optionsVersion++; optionsDirty = true; optionsSaveFailed = false; optionsLoadFailed = false; optionsSaveDue = Environment.TickCount64 + 300;
    }

    private static bool Expected(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or
        ArgumentException or InvalidOperationException or OperationCanceledException or System.Security.Cryptography.CryptographicException or
        System.Net.NetworkInformation.NetworkInformationException;

    internal static void Text(string value)
    {
        ImGui.PushTextWrapPos(0);
        try { ImGui.TextUnformatted(value); }
        finally { ImGui.PopTextWrapPos(); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; accessReady = false;
        framework.Update -= CaptureScene; contextMenu.OnMenuOpened -= OnContextMenu;
        resolver.SetAccess(false);
        Volatile.Write(ref contextProfiles, new Dictionary<string, ProfileKey>(StringComparer.OrdinalIgnoreCase));
        if (publication.HasValue) network.SetPublishedProfile(null);
        lifetime.Cancel();
        var pending = actions.Select(a => a.Task).ToList();
        if (cacheLoad is not null) pending.Add(cacheLoad);
        if (optionsLoad is not null) pending.Add(optionsLoad);
        if (optionsSave is not null) pending.Add(optionsSave);
        if (addressLoad is not null) pending.Add(addressLoad);
        if (cachePeersLoad is not null) pending.Add(cachePeersLoad);
        DisposalCompletion = Task.WhenAll(pending).ContinueWith(t => { _ = t.Exception; lifetime.Dispose(); },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private sealed record UiAction(Task Task, Action? OnSuccess, long Generation);
    private sealed record SceneSnapshot(IReadOnlyList<VisibleActorSnapshot> Actors, ActorSnapshot? Target, ActorSnapshot? Mouseover,
        RpBarSceneSnapshot? BarContext = null);
}
