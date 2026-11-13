using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FRP1.Core.Persistence;
using FRP1.Core.Safety;
using FRP1.Core.Identity;
using FRP1.Core.Networking;
using FRP1.Plugin.Theming;
using FRP1.Plugin.RpBar;
using FRP1.Plugin.Contacts;
using FRP1.Plugin.Networking;
using FRP1.Plugin.Presentation;
using FRP1.Plugin.UiProfiles;
using FRP1.Plugin.UiSocial;
using FRP1.Plugin.UiTools;
using FRP1.Plugin.Utility;
using FRP1.Plugin.Automation;
using Dalamud.Game.ClientState.Conditions;
using FRP1.Plugin.GameIntegration;
using FRP1.Plugin.Localization;
using FRP1.Plugin.Integrations;
using FRP1.Plugin.Ipc;

namespace FRP1.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static INamePlateGui NamePlateGui { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    private readonly WindowSystem windows = new("FRP1");
    private readonly MainWindow mainWindow;
    private readonly ThemeSession themeSession;
    private readonly OwnProfileSession ownProfiles;
    private readonly RpBarWindow rpBarWindow;
    private readonly NetworkSession network;
    private readonly RegisterSession register;
    private readonly PresentationResolver resolver;
    private readonly ProfileUiModule profileUi;
    private readonly SocialUiModule socialUi;
    private readonly LocalToolsModule localTools;
    private readonly GamePresentationModule gamePresentation;
    private readonly IntegrationSession integrations;
    private readonly MoodlesAdapter moodlesAdapter;
    private readonly MoodlesSession moodles;
    private readonly AudiencePolicy policy;
    private readonly FrpIpcSession ipcSession;
    private readonly FrpIpcProvider? ipcProvider;
    private long ipcCatalogVersion = -1;
    private FrpIpcProfile? ipcActiveProfile;
    private IntegrationActor ipcSceneActor;

    public Plugin()
    {
        var directory = PluginInterface.GetPluginConfigDirectory();
        policy = new AudiencePolicy();
        var profileStore = new ProfileStore(directory, policy);
        ownProfiles = new OwnProfileSession(profileStore, new RpBarSettingsStore(directory));
        network = new NetworkSession(profileStore, new PeerIdentityStore(directory), new PeerTrustStore(directory));
        register = new RegisterSession(new ContactStore(directory));
        resolver = new PresentationResolver(policy, register);
        profileUi = new ProfileUiModule(policy, TextureProvider, DataManager);
        socialUi = new SocialUiModule(profileStore, ownProfiles, network, register, resolver,
            TargetManager, ObjectTable, ContextMenu, Framework, profileUi.OpenForeign, directory, TextureProvider,
            () => ClientState.IsLoggedIn && !Condition[ConditionFlag.BetweenAreas] && !Condition[ConditionFlag.BetweenAreas51]);
        localTools = new LocalToolsModule(ownProfiles, directory, OpenExistingProfile, policy);
        gamePresentation = new GamePresentationModule(NamePlateGui, ChatGui, GameGui, Framework, ClientState, DataManager,
            policy, () => GamePresentationModule.CaptureAllowed(resolver, socialUi.ActorSnapshots,
                ClientState.TerritoryType, ClientState.MapId), directory);
        themeSession = new ThemeSession(new ThemeSettingsStore(directory));
        mainWindow = new MainWindow(profileStore, new AudienceSettings(directory),
            new UsageAcknowledgementStore(directory), themeSession, ownProfiles, profileUi, socialUi, TextureProvider);
        rpBarWindow = new RpBarWindow(ownProfiles, mainWindow, resolver, policy, () => socialUi.BarScene,
            profileUi.OpenForeign, socialUi.OpenTargetDetails);
        mainWindow.RpBarWindow = rpBarWindow;
        mainWindow.LocalTools = localTools;
        mainWindow.GamePresentation = gamePresentation;
        integrations = new IntegrationSession(new IntegrationSettingsStore(directory), HonorificAdapter.Create(PluginInterface),
            () => mainWindow.CaptureIntegrationContext(CaptureIntegrationActor()), mainWindow.TryApplyHonorificTitle);
        mainWindow.Integrations = integrations;
        moodlesAdapter = new MoodlesAdapter(PluginInterface);
        moodles = new MoodlesSession(moodlesAdapter, CaptureMoodlesContext, mainWindow.TryApplyMoodlesGlance,
            () => integrations.MoodlesOptions, integrations.SetMoodlesOptions);
        integrations.MoodlesOptionsChanged += moodles.OptionsChanged;
        mainWindow.Moodles = moodles;
        ipcSession = new FrpIpcSession(CaptureIpcContext, WriteIpcActionAsync, mainWindow.TryOpenIpcOwnProfile);
        integrations.IpcOptionsChanged += ipcSession.SetOptions;
        ipcProvider = FrpIpcProvider.Create(PluginInterface, ipcSession);
        mainWindow.IpcSession = ipcSession;
        mainWindow.IpcAvailable = ipcProvider is not null;
        policy.Changed += OnIpcPolicyChanged;
        Framework.Update += ObserveIpcScene;
        gamePresentation.PreserveOwnTitle = (objectId, address) => integrations.HasConfirmedReadiness && integrations.CanYieldOwnTitle(CaptureIntegrationActor(),
            policy.Snapshot, mainWindow.IntegrationPolicyGeneration, mainWindow.ProfileAccessReady, objectId, address);
        mainWindow.LanguageProvider = () => localTools.Language;
        mainWindow.OwnProfileReferenceProvider = CreateOwnReference;
        windows.AddWindow(mainWindow);
        windows.AddWindow(rpBarWindow);
        foreach (var window in profileUi.Windows.Concat(socialUi.Windows).Concat(localTools.Windows)) windows.AddWindow(window);
        CommandManager.AddHandler("/frp", new CommandInfo(OnCommand)
        {
            HelpMessage = "Öffnet den lokalen FRP1-Profileditor. /frp bar show oder /frp bar toggle steuert die eigene RP-Leiste.",
        });
        CommandManager.AddHandler("/frpbar", new CommandInfo((_, arguments) => rpBarWindow.RequestShow(arguments.Trim().Equals("toggle", StringComparison.OrdinalIgnoreCase)))
        { HelpMessage = "Zeigt die eigene RP-Leiste; /frpbar toggle blendet sie um." });
        PluginInterface.UiBuilder.Draw += Draw;
        PluginInterface.UiBuilder.OpenMainUi += Open;
        PluginInterface.UiBuilder.OpenConfigUi += Open;
    }

    private void OnCommand(string command, string arguments)
    {
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 0 && parts[0].Equals("bar", StringComparison.OrdinalIgnoreCase))
            rpBarWindow.RequestShow(parts.Length > 1 && parts[1].Equals("toggle", StringComparison.OrdinalIgnoreCase));
        else Open();
    }
    private void Open() => mainWindow.IsOpen = true;

    private bool OpenExistingProfile(ProfileReference reference)
    {
        if (!mainWindow.ProfileAccessReady) return false;
        var key = new ProfileKey(reference.Fingerprint, reference.ProfileId);
        if (resolver.GetProfile(key).IsReadable)
        {
            profileUi.OpenForeign(() => mainWindow.ProfileAccessReady ? resolver.GetProfile(key) : null);
            return true;
        }
        if (!network.IsRunning) return false;
        try
        {
            if (!string.Equals(network.CreateInvitation().Fingerprint, reference.Fingerprint, StringComparison.OrdinalIgnoreCase) ||
                mainWindow.GetSavedOwnProfile(reference.ProfileId) is null) return false;
            profileUi.OpenOwn(() => mainWindow.GetSavedOwnProfile(reference.ProfileId));
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }

    private string? CreateOwnReference(Guid profileId)
    {
        if (!mainWindow.ProfileAccessReady || !network.IsRunning || mainWindow.GetSavedOwnProfile(profileId) is null) return null;
        try { return new ProfileReference(network.CreateInvitation().Fingerprint, profileId).Format(); }
        catch (InvalidOperationException) { return null; }
    }

    private static IntegrationActor CaptureIntegrationActor()
    {
        try
        {
            if (!PlayerState.IsLoaded || !ClientState.IsLoggedIn || Condition[ConditionFlag.BetweenAreas] ||
                Condition[ConditionFlag.BetweenAreas51] || ObjectTable.LocalPlayer is not { } local || !local.IsValid()) return default;
            return new(PlayerState.ContentId, local.GameObjectId, (long)local.Address, local.Name.TextValue,
                local.HomeWorld.RowId, ClientState.TerritoryType, ClientState.MapId);
        }
        catch (Exception) { return default; }
    }

    private MoodlesContext CaptureMoodlesContext()
    {
        var actor = CaptureIntegrationActor();
        moodlesAdapter.ObserveActor(actor);
        return mainWindow.CaptureMoodlesContext(actor);
    }

    private FrpIpcContext CaptureIpcContext()
    {
        if (ipcCatalogVersion != ownProfiles.Version)
        {
            ipcCatalogVersion = ownProfiles.Version;
            var active = ownProfiles.ActiveProfile;
            ipcActiveProfile = active is null ? null : FrpIpcProfile.From(active);
        }
        return new(CaptureIntegrationActor(), mainWindow.ProfileAccessReady, policy.Snapshot,
            mainWindow.IntegrationPolicyGeneration, ipcCatalogVersion, mainWindow.IntegrationEditorGeneration,
            mainWindow.IntegrationDraftDirty, mainWindow.IntegrationBusy, ipcActiveProfile);
    }

    private async Task<FrpIpcProfile> WriteIpcActionAsync(FrpIpcAction action, FrpIpcProfile expected, CancellationToken cancellationToken)
    {
        var active = ownProfiles.ActiveProfile;
        if (active is null || FrpIpcProfile.From(active) != expected) throw new InvalidOperationException("The active profile changed.");
        var operation = action.Kind switch
        {
            FrpIpcActionKind.SetStatus => ownProfiles.UpdateStatusAsync((Core.Profiles.RpStatus)action.Value, cancellationToken),
            FrpIpcActionKind.SetAvailability => ownProfiles.UpdateAvailabilityAsync((Core.Profiles.RpAvailability)action.Value, cancellationToken),
            _ => throw new ArgumentException("Unsupported local action."),
        };
        return FrpIpcProfile.From(await operation.ConfigureAwait(false));
    }

    private void OnIpcPolicyChanged(object? sender, EventArgs args) => ipcSession.InvalidatePolicy();

    private void ObserveIpcScene(IFramework _)
    {
        if (!ipcSession.HasCapabilities) { ipcSceneActor = default; return; }
        var current = CaptureIntegrationActor();
        if (current == ipcSceneActor) return;
        ipcSceneActor = current;
        ipcSession.InvalidatePolicy();
    }

    private void Draw()
    {
        var contentId = PlayerState.IsLoaded ? PlayerState.ContentId : 0;
        uint? raceId = contentId != 0 ? PlayerState.Race.RowId : null;
        themeSession.ObserveCharacter(contentId, raceId);
        themeSession.Poll();
        mainWindow.PollOperation();
        ownProfiles.ObserveContext(contentId, mainWindow.ProfileAccessReady);
        ownProfiles.SetEditorBlocked(mainWindow.EditorBlocked);
        ownProfiles.Poll();
        mainWindow.SynchronizeProfiles();
        var integrationGeneration = integrations.Generation;
        var integrationStatus = integrations.Status;
        integrations.Tick();
        if (Framework.IsInFrameworkUpdateThread) moodles.Tick();
        if (integrations.Generation != integrationGeneration || integrations.Status != integrationStatus) gamePresentation.Invalidate();
        ownProfiles.SetEditorBlocked(mainWindow.EditorBlocked);
        ipcSession.Tick();
        localTools.SetContext(new AutomationContext(contentId, mainWindow.ProfileAccessReady, contentId != 0,
            Condition[ConditionFlag.InCombat], Condition[ConditionFlag.BoundByDuty] || Condition[ConditionFlag.BoundByDuty56] ||
            Condition[ConditionFlag.BoundByDuty95], ClientState.TerritoryType));
        localTools.Tick();
        UiText.CurrentLanguage = localTools.Language;
        rpBarWindow.OverlaysVisible = !localTools.OverlaysSuppressed;
        socialUi.SetOverlaysVisible(!localTools.OverlaysSuppressed);
        profileUi.SetAccess(mainWindow.ProfileAccessReady);
        socialUi.SetContext(contentId, mainWindow.ProfileAccessReady);
        socialUi.Tick();
        gamePresentation.Update(contentId, mainWindow.ProfileAccessReady, resolver.Version);
        profileUi.Poll();
        rpBarWindow.Poll();
        using var themeScope = NativeThemeRenderer.Begin(themeSession.Theme, themeSession.Preferences.ReduceDecoration);
        var drawnPresentationVersion = resolver.Version;
        var drawnAccess = mainWindow.ProfileAccessReady;
        windows.Draw();
        ipcSession.Refresh();
        if (integrations.Generation != integrationGeneration) gamePresentation.Invalidate();
        // Local block/binding or audience changes made by the panels invalidate
        // native display snapshots immediately, before drawing further overlays.
        var currentContentId = PlayerState.IsLoaded ? PlayerState.ContentId : 0;
        if (resolver.Version != drawnPresentationVersion || mainWindow.ProfileAccessReady != drawnAccess || currentContentId != contentId)
            gamePresentation.Invalidate();
        gamePresentation.Update(currentContentId, mainWindow.ProfileAccessReady && currentContentId == contentId, resolver.Version);
        profileUi.DrawOverlays();
        socialUi.DrawOverlays();
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler("/frp");
        CommandManager.RemoveHandler("/frpbar");
        PluginInterface.UiBuilder.Draw -= Draw;
        PluginInterface.UiBuilder.OpenMainUi -= Open;
        PluginInterface.UiBuilder.OpenConfigUi -= Open;
        windows.RemoveAllWindows();
        policy.Changed -= OnIpcPolicyChanged;
        Framework.Update -= ObserveIpcScene;
        integrations.IpcOptionsChanged -= ipcSession.SetOptions;
        integrations.MoodlesOptionsChanged -= moodles.OptionsChanged;
        ipcProvider?.Dispose();
        ipcSession.Dispose();
        moodles.Dispose();
        integrations.Dispose();
        gamePresentation.Dispose();
        socialUi.Dispose();
        profileUi.Dispose();
        localTools.Dispose();
        mainWindow.Dispose();
        rpBarWindow.Dispose();
        ownProfiles.Dispose();
        themeSession.Dispose();
        register.Dispose();
        resolver.Dispose();
        var networkDisposal = Task.Run(async () => await network.DisposeAsync().ConfigureAwait(false));
        _ = Task.WhenAll(networkDisposal, socialUi.DisposalCompletion, ownProfiles.DisposalCompletion,
            register.DisposalCompletion, themeSession.DisposalCompletion, localTools.DisposalCompletion,
            gamePresentation.DisposalCompletion, integrations.DisposalCompletion, ipcSession.DisposalCompletion).ContinueWith(task => { _ = task.Exception; },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
