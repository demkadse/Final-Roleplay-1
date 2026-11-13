using System.Collections.Frozen;
using Dalamud.Game.Chat;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Presentation;
using FRP1.Plugin.UiSocial;

namespace FRP1.Plugin.GameIntegration;

public sealed partial class GamePresentationModule : IDisposable
{
    private sealed record DisplaySnapshot(uint Territory, uint Map, long Generation, AudiencePreferences Audience,
        GamePresentationOptions Options, FrozenDictionary<GameActorKey, GameDisplayEntry> Entries);

    private readonly INamePlateGui nameplates;
    private readonly IChatGui chat;
    private readonly IGameGui game;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IDataManager data;
    private readonly AudiencePolicy policy;
    private readonly Func<GamePresentationInput> capture;
    private readonly GamePresentationOptionsStore store;
    private readonly CancellationTokenSource lifetime = new();
    private DisplaySnapshot? published;
    private GamePresentationOptions options = new();
    private Task<GamePresentationOptions>? load;
    private Task? save;
    private long optionsVersion;
    private long loadVersion;
    private bool savePending;
    private bool loadFailed;
    private bool saveFailed;
    private int disposed;
    private int access;
    private int redrawRequested;
    private long generation;
    private ulong character;
    private long presentationVersion = -1;
    private long nativeScene;
    private long acceptedScene = -1;
    private object? sceneBarrierToken;

    public GamePresentationModule(INamePlateGui nameplates, IChatGui chat, IGameGui game, IFramework framework,
        IClientState client, IDataManager data, AudiencePolicy policy, Func<GamePresentationInput> captureAllowed,
        string configurationDirectory)
    {
        this.nameplates = nameplates; this.chat = chat; this.game = game; this.framework = framework;
        this.client = client; this.data = data; this.policy = policy; capture = captureAllowed;
        store = new GamePresentationOptionsStore(configurationDirectory);
        load = Task.Run(() => store.LoadAsync(lifetime.Token));
        nameplates.OnNamePlateUpdate += OnNameplates;
        chat.ChatMessage += OnChat;
        framework.Update += OnFramework;
        policy.Changed += OnAudienceChanged;
    }

    public GamePresentationOptions Options => options;
    public bool LoadFailed => loadFailed;
    public bool SaveFailed => saveFailed;
    public Func<ulong, long, bool>? PreserveOwnTitle { get; set; }
    public Task DisposalCompletion { get; private set; } = Task.CompletedTask;

    // This adapter and Update are UI-thread-only. The resulting entries contain no live profile references.
    public static GamePresentationInput CaptureAllowed(PresentationResolver resolver, IReadOnlyList<VisibleActorSnapshot> actors,
        uint territoryId, uint mapId)
    {
        var entries = new List<GameDisplayEntry>();
        foreach (var actor in actors.Take(GameDisplayRules.MaximumActors))
        {
            var resolved = resolver.Resolve(actor.Actor);
            var metadata = resolved.DisplayMetadata;
            if (metadata is null || !resolved.IsReadable || !float.IsFinite(actor.Distance)) continue;
            entries.Add(new(new GameActorKey(actor.Actor.Name, actor.Actor.HomeWorldId), GameDisplayRules.SingleLine(metadata.DisplayName), GameDisplayRules.SingleLine(metadata.Title),
                metadata.ContentRating, metadata.NameColor, actor.Position, actor.Distance, metadata.IconId));
        }
        return new(territoryId, mapId, entries.AsReadOnly(), actors);
    }

    public void Update(ulong localCharacterId, bool accessReady, long currentPresentationVersion)
    {
        if (Volatile.Read(ref disposed) != 0) return;
        PollOptions();
        var allowed = accessReady && localCharacterId != 0 && client.IsLoggedIn &&
            policy.Snapshot.Selection != AudienceSelection.Unspecified;
        var previouslyAllowed = Interlocked.Exchange(ref access, allowed ? 1 : 0) != 0;
        if (character != localCharacterId || !previouslyAllowed || !allowed) acceptedScene = -1;
        if (character != localCharacterId || presentationVersion != currentPresentationVersion || !allowed)
        {
            Invalidate(); character = localCharacterId; presentationVersion = currentPresentationVersion;
        }
        if (!allowed) return;
        try
        {
            var input = capture();
            var scene = SceneKey(input.TerritoryId, input.MapId);
            if (input.TerritoryId == 0 || input.MapId == 0 || input.TerritoryId != client.TerritoryType ||
                input.MapId != client.MapId || scene != Interlocked.Read(ref nativeScene)) { Invalidate(); return; }
            if (acceptedScene != scene)
            {
                acceptedScene = scene; sceneBarrierToken = input.SourceToken; Invalidate(); return;
            }
            if (ReferenceEquals(sceneBarrierToken, input.SourceToken)) return;
            sceneBarrierToken = null;
            var audience = policy.Snapshot;
            var validated = GameDisplayRules.CopyUnique(input.Entries);
            var entries = validated.Where(pair => pair.Value.Rating == ContentRating.General ||
                (audience.Selection == AudienceSelection.Adult && audience.MatureContentOptIn)).ToFrozenDictionary();
            var next = new DisplaySnapshot(input.TerritoryId, input.MapId, Interlocked.Read(ref generation), audience, options, entries);
            var previous = Volatile.Read(ref published);
            Volatile.Write(ref published, next);
            if (previous is null || previous.Options != next.Options || !SameEntries(previous.Entries, entries))
                Interlocked.Exchange(ref redrawRequested, 1);
        }
        catch (Exception ex) when (Expected(ex)) { Invalidate(); }
    }

    // Call on the UI thread immediately after a local block/mute/context change, before another draw/event can reuse it.
    public void Invalidate()
    {
        Interlocked.Increment(ref generation);
        if (Interlocked.Exchange(ref published, null) is not null) Interlocked.Exchange(ref redrawRequested, 1);
    }

    private void OnAudienceChanged(object? sender, EventArgs args) => Invalidate();

    private void OnFramework(IFramework _)
    {
        if (Volatile.Read(ref disposed) != 0) return;
        try
        {
            var current = client.IsLoggedIn ? SceneKey(client.TerritoryType, client.MapId) : 0;
            if (Interlocked.Exchange(ref nativeScene, current) != current) Invalidate();
            if (!client.IsLoggedIn) Volatile.Write(ref access, 0);
            if (Interlocked.Exchange(ref redrawRequested, 0) != 0) nameplates.RequestRedraw();
        }
        catch (Exception ex) when (Expected(ex)) { Invalidate(); }
    }

    private bool CanUse(DisplaySnapshot snapshot)
    {
        if (Volatile.Read(ref disposed) != 0 || Volatile.Read(ref access) == 0 ||
            snapshot.Generation != Interlocked.Read(ref generation) || !ReferenceEquals(snapshot, Volatile.Read(ref published)) ||
            snapshot.Audience != policy.Snapshot || snapshot.Audience.Selection == AudienceSelection.Unspecified ||
            SceneKey(snapshot.Territory, snapshot.Map) != Interlocked.Read(ref nativeScene) || !client.IsLoggedIn ||
            snapshot.Territory != client.TerritoryType || snapshot.Map != client.MapId) return false;
        return true;
    }

    private void OnNameplates(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        var snapshot = Volatile.Read(ref published);
        if (snapshot is null || !snapshot.Options.Nameplates || !CanUse(snapshot)) return;
        try
        {
            foreach (var handler in handlers)
            {
                var player = handler.PlayerCharacter;
                if (player is null || !snapshot.Entries.TryGetValue(new GameActorKey(player.Name.TextValue, player.HomeWorld.RowId).Canonical(), out var entry) ||
                    !CanUse(snapshot)) continue;
                handler.Name = new SeString(new TextPayload(entry.RpName));
                if (snapshot.Options.Titles && !(PreserveOwnTitle?.Invoke(player.GameObjectId, (long)player.Address) ?? false))
                {
                    handler.Title = new SeString(new TextPayload(entry.Title));
                    handler.DisplayTitle = !string.IsNullOrWhiteSpace(entry.Title);
                }
                if (snapshot.Options.ProfileColors && GameDisplayRules.TryParseColor(entry.NameColor, out var color)) handler.TextColor = color;
                if (snapshot.Options.ProfileIcons && entry.IconId is > 0 and <= int.MaxValue) handler.NameIconId = (int)entry.IconId;
            }
        }
        catch (Exception ex) when (Expected(ex)) { Invalidate(); }
    }

    private void OnChat(IHandleableChatMessage message)
    {
        var snapshot = Volatile.Read(ref published);
        if (snapshot is null || !snapshot.Options.ChatNames || !CanUse(snapshot) || message.IsHandled ||
            message.SourceKind == XivChatRelationKind.LocalPlayer) return;
        try
        {
            var payloads = message.Sender.Payloads;
            // Refuse ambiguous or already customized sender strings; preserve all link and formatting payloads.
            if (payloads.Count > 64) return;
            var parts = payloads.Select(payload => payload switch
            {
                PlayerPayload player => new ChatSenderPart(ChatSenderPartKind.PlayerLink, Player: new(player.PlayerName, player.World.RowId)),
                TextPayload text => new ChatSenderPart(ChatSenderPartKind.Text, text.Text),
                _ => new ChatSenderPart(ChatSenderPartKind.Other),
            }).ToArray();
            if (!GameDisplayRules.TryFindChatReplacement(parts, snapshot.Entries, out var index, out var replacement)) return;
            var changed = payloads.ToArray();
            changed[index] = new TextPayload(replacement);
            if (CanUse(snapshot)) message.Sender = new SeString(changed);
        }
        catch (Exception ex) when (Expected(ex)) { Invalidate(); }
    }

    private static long SceneKey(uint territory, uint map) => (long)(((ulong)territory << 32) | map);
    private static bool SameEntries(FrozenDictionary<GameActorKey, GameDisplayEntry> left, FrozenDictionary<GameActorKey, GameDisplayEntry> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) &&
            pair.Value.RpName == value.RpName && pair.Value.Title == value.Title && pair.Value.NameColor == value.NameColor &&
            pair.Value.Rating == value.Rating && pair.Value.IconId == value.IconId);
    private static bool Expected(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or
        ArgumentException or InvalidOperationException or OperationCanceledException;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        Volatile.Write(ref access, 0); Invalidate();
        policy.Changed -= OnAudienceChanged; framework.Update -= OnFramework;
        nameplates.OnNamePlateUpdate -= OnNameplates; chat.ChatMessage -= OnChat;
        try { nameplates.RequestRedraw(); } catch (Exception ex) when (Expected(ex)) { }
        lifetime.Cancel();
        DisposalCompletion = ObserveDisposalAsync(load, save);
    }

    private async Task ObserveDisposalAsync(Task? pendingLoad, Task? pendingSave)
    {
        try
        {
            if (pendingLoad is not null) try { await pendingLoad.ConfigureAwait(false); } catch (Exception ex) when (Expected(ex)) { }
            if (pendingSave is not null) try { await pendingSave.ConfigureAwait(false); } catch (Exception ex) when (Expected(ex)) { }
        }
        finally { lifetime.Dispose(); }
    }
}
