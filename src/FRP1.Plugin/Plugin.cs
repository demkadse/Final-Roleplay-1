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

    public Plugin()
    {
        var directory = PluginInterface.GetPluginConfigDirectory();
        var policy = new AudiencePolicy();
        var profileStore = new ProfileStore(directory, policy);
        ownProfiles = new OwnProfileSession(profileStore, new RpBarSettingsStore(directory));
        network = new NetworkSession(profileStore, new PeerIdentityStore(directory), new PeerTrustStore(directory));
        register = new RegisterSession(new ContactStore(directory));
        resolver = new PresentationResolver(policy, register);
        profileUi = new ProfileUiModule(policy, TextureProvider, DataManager);
        socialUi = new SocialUiModule(profileStore, ownProfiles, network, register, resolver,
            TargetManager, ObjectTable, ContextMenu, Framework, profileUi.OpenForeign, directory, TextureProvider);
        localTools = new LocalToolsModule(ownProfiles, directory, OpenExistingProfile, policy);
        gamePresentation = new GamePresentationModule(NamePlateGui, ChatGui, GameGui, Framework, ClientState, DataManager,
            policy, () => GamePresentationModule.CaptureAllowed(resolver, socialUi.ActorSnapshots,
                ClientState.TerritoryType, ClientState.MapId), directory);
        themeSession = new ThemeSession(new ThemeSettingsStore(directory));
        mainWindow = new MainWindow(profileStore, new AudienceSettings(directory),
            new UsageAcknowledgementStore(directory), themeSession, ownProfiles, profileUi, socialUi, TextureProvider);
        rpBarWindow = new RpBarWindow(ownProfiles, mainWindow);
        mainWindow.RpBarWindow = rpBarWindow;
        mainWindow.LocalTools = localTools;
        mainWindow.GamePresentation = gamePresentation;
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
        ownProfiles.SetEditorBlocked(mainWindow.EditorBlocked);
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
            gamePresentation.DisposalCompletion).ContinueWith(task => { _ = task.Exception; },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
