using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Contacts;
using FRP1.Plugin.Localization;
using FRP1.Plugin.Presentation;
using FRP1.Plugin.RpBar;

namespace FRP1.Plugin;

internal sealed class RpBarWindow : Window, IDisposable
{
    private sealed record DisplayOwner(RpBarSceneSnapshot Scene, RpBarContextKind Kind, ProfileKey? Foreign,
        Guid Profile, long Revision, long PolicyEpoch);
    private sealed record DisplayState(RpBarDisplayContext Context, ProfileDocument? Profile, DisplayOwner Owner);
    private readonly OwnProfileSession session;
    private readonly MainWindow main;
    private readonly PresentationResolver resolver;
    private readonly AudiencePolicy policy;
    private readonly Func<RpBarSceneSnapshot> scene;
    private readonly Action<Func<ResolvedProfile?>> openForeign;
    private readonly Action showTargetDetails;
    private readonly List<Task> actions = [];
    private RpBarDisplayContext? cachedContext;
    private RpBarSceneSnapshot? cachedScene;
    private ProfileDocument? cachedProfile;
    private long cachedVersion = -1;
    private long cachedPolicyEpoch = -1;
    private long policyEpoch;
    private DisplayOwner? currentOwner;
    private DisplayOwner? glanceOwner;
    private DisplayOwner? menuOwner;
    private DisplayOwner? availabilityOwner;
    private int glanceIndex = -1;
    private bool glancePinned;
    private bool glanceHovered;
    private long glanceUntil;
    private Vector2 glanceAnchor;
    private bool resetGlanceScroll;
    private bool disposed;
    private bool positionInitialized;
    private bool forcePosition;
    private bool compactStylePushed;
    private Vector2 lastPreferencePosition;
    private Vector2 lastWorkPosition;
    private Vector2 lastWorkSize;
    private Vector2 measuredSize;
    private float lastScale;
    private int requestedVisibility;
    private string? feedback;

    public RpBarWindow(OwnProfileSession ownProfiles, MainWindow mainWindow, PresentationResolver presentationResolver,
        AudiencePolicy audiencePolicy, Func<RpBarSceneSnapshot> currentScene, Action<Func<ResolvedProfile?>> openForeignProfile,
        Action openTargetDetails) : base("Mein RP##FRP1OwnBar")
    {
        session = ownProfiles; main = mainWindow; resolver = presentationResolver; policy = audiencePolicy;
        scene = currentScene; openForeign = openForeignProfile; showTargetDetails = openTargetDetails;
        policy.Changed += OnPolicyChanged;
    }

    public bool OverlaysVisible { get; set; } = true;
    private float Unit => session.Settings.Scale * ImGuiHelpers.GlobalScale;

    public void Poll()
    {
        if (disposed) return;
        foreach (var action in actions.Where(action => action.IsCompleted).ToArray())
        {
            actions.Remove(action);
            try { action.GetAwaiter().GetResult(); feedback = null; }
            catch (Exception ex) when (Expected(ex))
            { feedback = T("Änderung nicht gespeichert. Entwurf und laufende Vorgänge prüfen.", "Change not saved. Check your draft and pending operations."); }
        }
        if (!session.SettingsLoaded) return;
        if (requestedVisibility != 0)
        {
            var visible = requestedVisibility == 1 || !session.Settings.Visible;
            session.SetPreferences(session.Settings with { Visible = visible }); requestedVisibility = 0;
        }
        // The preference is authoritative; IsOpen may still reflect the previous frame.
        IsOpen = session.Settings.Visible;
        if (!DrawConditions() || session.Settings.Collapsed) ClearGlance();
    }

    public void RequestShow(bool toggle = false)
    {
        requestedVisibility = toggle ? 2 : 1;
        if (!main.ProfileAccessReady || !session.HasCharacter) main.IsOpen = true;
    }

    public override bool DrawConditions()
    {
        var available = !disposed && OverlaysVisible && main.ProfileAccessReady && session.AccessReady && session.HasCharacter && session.SettingsLoaded;
        if (!available) { ClearGlance(); currentOwner = null; cachedProfile = null; cachedVersion = -1; }
        return available;
    }

    public override void PreDraw()
    {
        var settings = session.Settings;
        RespectCloseHotkey = false;
        Flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing;
        if (settings.Locked) Flags |= ImGuiWindowFlags.NoMove;
        var unit = Unit;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6, 5) * unit); compactStylePushed = true;
        var viewport = ImGui.GetMainViewport();
        var available = Vector2.Max(Vector2.One, viewport.WorkSize - new Vector2(16));
        var width = MathF.Min(280 * unit, available.X);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, available.Y));
        var stored = new Vector2(settings.PositionX, settings.PositionY);
        var size = new Vector2(width, measuredSize.Y > 0 ? measuredSize.Y : (settings.Collapsed ? 36 : 68) * unit);
        var clamped = RpBarGeometry.Clamp(stored, size, viewport.WorkSize);
        if (!positionInitialized || forcePosition || Vector2.Distance(stored, lastPreferencePosition) > .5f ||
            viewport.WorkPos != lastWorkPosition || viewport.WorkSize != lastWorkSize || settings.Scale != lastScale)
            ImGui.SetNextWindowPos(viewport.WorkPos + clamped, ImGuiCond.Always);
        positionInitialized = true; forcePosition = false; lastPreferencePosition = stored;
        lastWorkPosition = viewport.WorkPos; lastWorkSize = viewport.WorkSize; lastScale = settings.Scale;
    }

    public override void Draw()
    {
        ImGui.SetWindowFontScale(session.Settings.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(3, 2) * Unit);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 3) * Unit);
        try
        {
            var display = CurrentDisplay();
            if (display is null) return;
            DrawHeader(display);
            if (!session.Settings.Collapsed) DrawStrip(display);
            if (ImGui.BeginPopupContextWindow("rpbar-context", ImGuiPopupFlags.MouseButtonRight))
            {
                try
                {
                    if (ImGui.IsWindowAppearing()) menuOwner = display.Owner;
                    if (menuOwner != display.Owner) ImGui.CloseCurrentPopup();
                    else DrawContextMenu(display);
                }
                finally { ImGui.EndPopup(); }
            }
            CapturePosition();
        }
        finally { ImGui.PopStyleVar(2); ImGui.SetWindowFontScale(1); }
    }

    public override void PostDraw()
    {
        if (compactStylePushed) { ImGui.PopStyleVar(); compactStylePushed = false; }
        DrawGlanceCard();
    }

    private DisplayState? CurrentDisplay()
    {
        if (!DrawConditions()) return null;
        var captured = scene();
        var context = RpBarContextResolver.Resolve(captured, main.ProfileAccessReady && session.AccessReady, resolver);
        var version = context.Kind == RpBarContextKind.Own ? session.Version : resolver.Version;
        var epoch = Interlocked.Read(ref policyEpoch);
        if (cachedContext != context || cachedScene != captured || cachedVersion != version || cachedPolicyEpoch != epoch)
        {
            cachedContext = context; cachedScene = captured; cachedVersion = version; cachedPolicyEpoch = epoch;
            cachedProfile = context.Kind switch
            {
                RpBarContextKind.Own => session.ActiveProfile,
                RpBarContextKind.Foreign => resolver.Resolve(context.Actor).Profile,
                _ => null,
            };
        }
        if (captured != scene() || context != RpBarContextResolver.Resolve(scene(), main.ProfileAccessReady && session.AccessReady, resolver) ||
            epoch != Interlocked.Read(ref policyEpoch)) { cachedProfile = null; ClearGlance(); return null; }
        var profile = cachedProfile;
        if (profile is not null && !main.CanDisplay(profile)) profile = null;
        if (context.Kind == RpBarContextKind.Foreign && (profile is null || !resolver.Resolve(context.Actor).IsReadable))
            context = new(RpBarContextKind.Neutral, context.Actor);
        var owner = new DisplayOwner(captured, context.Kind, context.Profile, profile?.Id ?? Guid.Empty, profile?.Revision ?? 0, epoch);
        if (owner != currentOwner) { ClearGlance(); currentOwner = owner; }
        return new(context, profile, owner);
    }

    private bool IsCurrent(DisplayState display)
    {
        if (!DrawConditions() || display.Owner.PolicyEpoch != Interlocked.Read(ref policyEpoch) || display.Owner.Scene != scene()) return false;
        var live = RpBarContextResolver.Resolve(scene(), main.ProfileAccessReady && session.AccessReady, resolver);
        return live == display.Context && (display.Profile is null || main.CanDisplay(display.Profile)) &&
            (live.Kind == RpBarContextKind.Own ? cachedVersion == session.Version : cachedVersion == resolver.Version);
    }

    private void DrawHeader(DisplayState display)
    {
        if (!IsCurrent(display)) return;
        var unit = Unit;
        var profile = display.Profile;
        if (IconButton("header", display.Context.Kind == RpBarContextKind.Own ? "@" : "RP", profile?.AppearanceStyle.IconId ?? 0, 26 * unit).Clicked)
            Open(display);
        ImGui.SameLine();
        var name = profile?.DisplayName ?? (display.Context.Kind == RpBarContextKind.Own
            ? T("Profil wählen", "Choose profile") : T("Kein RP-Profil", "No RP profile"));
        var contextLabel = display.Context.Kind switch
        {
            RpBarContextKind.Own => T("Eigenes Profil", "Your profile"),
            RpBarContextKind.Foreign => T("Zielprofil · nur lesen", "Target profile · read only"),
            _ => T("Kein erlaubtes zugeordnetes Zielprofil", "No permitted assigned target profile"),
        };
        var prefix = display.Context.Kind switch
        {
            RpBarContextKind.Own => T("Ich · ", "Me · "), RpBarContextKind.Foreign => T("Ziel · ", "Target · "), _ => "",
        };
        ImGui.TextUnformatted(Fit(prefix + name.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' '),
            MathF.Max(1, ImGui.GetContentRegionAvail().X - 50 * unit)));
        if ((ImGui.IsItemHovered() || ImGui.IsItemFocused()) && IsCurrent(display))
        {
            ConstrainTooltip(); ImGui.BeginTooltip(); Wrapped(name); Wrapped(contextLabel); ImGui.EndTooltip();
        }
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - 44 * unit);
        if (ImGui.Button(session.Settings.Collapsed ? ">##bar-collapse" : "v##bar-collapse", new Vector2(19 * unit, 26 * unit)))
        { session.SetPreferences(session.Settings with { Collapsed = !session.Settings.Collapsed }); ClearGlance(); }
        main.Help.AfterItem(HelpTopicId.BarCollapse);
        ImGui.SameLine();
        if (ImGui.Button("...##bar-menu", new Vector2(21 * unit, 26 * unit))) { menuOwner = display.Owner; ImGui.OpenPopup("bar-menu"); }
        if (ImGui.BeginPopup("bar-menu"))
        { try { if (menuOwner == display.Owner) DrawContextMenu(display); else ImGui.CloseCurrentPopup(); } finally { ImGui.EndPopup(); } }
    }

    private void DrawStrip(DisplayState display)
    {
        if (!IsCurrent(display)) return;
        var profile = display.Profile;
        var unit = Unit;
        var size = 23 * unit;
        var own = display.Context.Kind == RpBarContextKind.Own;
        ImGui.BeginDisabled(profile is null || !own || !CanMutate());
        if (ImGui.Button(profile?.Status.ToString().ToUpperInvariant() ?? "--", new Vector2(35 * unit, size)) && IsCurrent(display))
            Run(display, () => session.UpdateStatusAsync(profile!.Status switch { RpStatus.Ic => RpStatus.Ooc, RpStatus.Ooc => RpStatus.Afk, _ => RpStatus.Ic }));
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            HoverText(own ? T("Eigener RP-Status: IC / OOC / AFK", "Your RP status: IC / OOC / AFK") : T("RP-Status des Ziels; nur lesbar", "Target RP status; read only"));
        ImGui.EndDisabled();
        ImGui.SameLine();
        var availability = profile?.Availability switch { RpAvailability.Open => "+", RpAvailability.Busy => "~", RpAvailability.Unavailable => "x", _ => "-" };
        ImGui.BeginDisabled(profile is null || !own || !CanMutate());
        if (ImGui.Button(availability + "##bar-availability", new Vector2(size)) && IsCurrent(display))
        { availabilityOwner = display.Owner; ImGui.OpenPopup("bar-availability"); }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) HoverText(profile is null ? T("Keine Verfügbarkeit", "No availability") : AvailabilityLabel(profile.Availability));
        if (ImGui.BeginPopup("bar-availability"))
        {
            try
            {
                if (!IsCurrent(display) || !own || availabilityOwner != display.Owner) ImGui.CloseCurrentPopup();
                else foreach (var value in Enum.GetValues<RpAvailability>())
                    if (ImGui.Selectable(AvailabilityLabel(value), profile?.Availability == value)) Run(display, () => session.UpdateAvailabilityAsync(value));
            }
            finally { ImGui.EndPopup(); }
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(profile is null);
        if (ImGui.Button("P##bar-open", new Vector2(size))) Open(display);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) HoverText(T("Profil öffnen", "Open profile"));
        if (profile is null) return;
        var shown = 0;
        for (var index = 0; index < profile.Glances.Count && shown < 5; index++)
        {
            var glance = profile.Glances[index];
            if (!glance.Enabled || !IsCurrent(display)) continue;
            ImGui.SameLine();
            var item = IconButton("glance-" + index, (++shown).ToString(), glance.IconId, size);
            if (item.Hovered || item.Focused || item.Clicked)
            {
                if (glanceOwner != display.Owner || glanceIndex != index) { glancePinned = false; resetGlanceScroll = true; }
                glanceOwner = display.Owner; glanceIndex = index; glanceUntil = Environment.TickCount64 + 500;
                glanceAnchor = new Vector2(item.RectMin.X, item.RectMax.Y + 5 * unit);
                if (item.Clicked) glancePinned = true;
            }
        }
    }

    private void DrawGlanceCard()
    {
        if (glanceIndex < 0 || glanceOwner is null || session.Settings.Collapsed) return;
        var display = CurrentDisplay();
        if (display is null || !IsCurrent(display) || display.Owner != glanceOwner || display.Profile is not { } profile ||
            glanceIndex >= profile.Glances.Count || !profile.Glances[glanceIndex].Enabled)
        { ClearGlance(); return; }
        if (!glancePinned && !glanceHovered && Environment.TickCount64 > glanceUntil) { ClearGlance(); return; }
        var glance = profile.Glances[glanceIndex];
        var viewport = ImGui.GetMainViewport();
        var unit = Unit;
        var width = MathF.Min(420 * unit, MathF.Max(1, viewport.WorkSize.X - 16));
        var maximumHeight = MathF.Max(1, viewport.WorkSize.Y - 16);
        var contentHeight = ImGui.CalcTextSize(glance.Text, false, MathF.Max(1, width - 36 * unit)).Y * session.Settings.Scale;
        var height = MathF.Min(maximumHeight, Math.Clamp(contentHeight + 100 * unit, 130 * unit, 440 * unit));
        var position = RpBarGeometry.Clamp(glanceAnchor - viewport.WorkPos, new Vector2(width, height), viewport.WorkSize);
        ImGui.SetNextWindowPos(viewport.WorkPos + position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
        var open = true;
        var visible = ImGui.Begin(T("Auf den ersten Blick##FRP1BarGlance", "At first glance##FRP1BarGlance"), ref open,
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing);
        try
        {
            glanceHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) || ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            if (glanceHovered) glanceUntil = Environment.TickCount64 + 500;
            if (!visible || !IsCurrent(display)) return;
            ImGui.SetWindowFontScale(session.Settings.Scale);
            ImGui.BeginChild("##full-glance-scroll", Vector2.Zero, false);
            try
            {
                if (resetGlanceScroll) { ImGui.SetScrollY(0); resetGlanceScroll = false; }
                main.DrawLocalIcon(glance.IconId, new Vector2(28 * unit));
                Wrapped(glance.Label); ImGui.Separator(); Wrapped(glance.Text);
            }
            finally { ImGui.EndChild(); }
        }
        finally { ImGui.SetWindowFontScale(1); ImGui.End(); if (!open) ClearGlance(); }
    }

    private void DrawContextMenu(DisplayState display)
    {
        if (!IsCurrent(display)) { ImGui.CloseCurrentPopup(); return; }
        if (ImGui.MenuItem(T("Profil öffnen", "Open profile"))) Open(display);
        if (display.Context.Kind == RpBarContextKind.Foreign && ImGui.MenuItem(T("Zielkarte festhalten / Details", "Pin target card / details"))) showTargetDetails();
        if (display.Context.Kind == RpBarContextKind.Own) DrawProfileSelection();
        ImGui.Separator();
        DrawLayoutSettings();
        if (feedback is not null) Wrapped(feedback);
        if (session.ErrorMessage is not null)
        {
            Wrapped(T("Leisteneinstellungen konnten nicht gespeichert oder geladen werden.", "Bar settings could not be saved or loaded."));
            if (ImGui.Button(T("Erneut versuchen##bar-retry", "Retry##bar-retry"))) session.Retry();
        }
        if (ImGui.MenuItem(T("FRP1 öffnen", "Open FRP1"))) main.IsOpen = true;
        if (ImGui.BeginMenu(T("Hilfe", "Help")))
        {
            if (ImGui.MenuItem(T("Hilfe: aktuelles Ziel", "Help: current target"))) main.OpenHelp(HelpTopicId.TargetContext);
            if (ImGui.MenuItem(T("Hilfe: ein- und ausklappen", "Help: collapse and expand"))) main.OpenHelp(HelpTopicId.BarCollapse);
            if (ImGui.MenuItem(T("Hilfe: Auf den ersten Blick", "Help: at first glance"))) main.OpenHelp(HelpTopicId.Glances);
            ImGui.EndMenu();
        }
    }

    private void DrawProfileSelection()
    {
        ImGui.BeginDisabled(!CanMutate());
        if (ImGui.BeginMenu(T("Eigenes aktives Profil wählen", "Choose your active profile")))
        {
            foreach (var profile in session.Profiles)
            {
                if (!main.ProfileAccessReady || !session.AccessReady || !main.CanDisplay(profile)) continue;
                ImGui.PushID(profile.Id.ToString("N"));
                if (MainWindow.ProfileSelectable(profile, cachedProfile?.Id == profile.Id)) Run(null, () => session.SelectActiveAsync(profile.Id));
                ImGui.PopID();
            }
            ImGui.EndMenu();
        }
        ImGui.EndDisabled();
    }

    private void DrawLayoutSettings()
    {
        ImGui.BeginDisabled(!session.SettingsLoaded);
        var settings = session.Settings;
        var visible = settings.Visible;
        if (ImGui.Checkbox(main.T("Bar.Visible") + "###BarVisible", ref visible)) session.SetPreferences(session.Settings with { Visible = visible });
        main.Help.AfterItem(HelpTopicId.BarVisible); main.Help.Badge(HelpTopicId.BarVisible);
        var collapsed = session.Settings.Collapsed;
        if (ImGui.Checkbox(T("Leiste einklappen###BarCollapsed", "Collapse bar###BarCollapsed"), ref collapsed))
        { session.SetPreferences(session.Settings with { Collapsed = collapsed }); ClearGlance(); }
        main.Help.AfterItem(HelpTopicId.BarCollapse); main.Help.Badge(HelpTopicId.BarCollapse);
        var locked = session.Settings.Locked;
        if (ImGui.Checkbox(main.T("Bar.Locked") + "###BarLocked", ref locked)) session.SetPreferences(session.Settings with { Locked = locked });
        main.Help.AfterItem(HelpTopicId.BarLock); main.Help.Badge(HelpTopicId.BarLock);
        var scale = session.Settings.Scale;
        if (ImGui.SliderFloat(main.T("Bar.Scale") + "###BarScale", ref scale, .75f, 1.75f, "%.2f")) session.SetPreferences(session.Settings with { Scale = scale });
        main.Help.AfterItem(HelpTopicId.BarScale); main.Help.Badge(HelpTopicId.BarScale);
        if (ImGui.Button(T("Anzeigen und Position zurücksetzen", "Show and reset position")))
            session.SetPreferences(session.Settings with { Visible = true, PositionX = RpBarGeometry.ResetPosition.X, PositionY = RpBarGeometry.ResetPosition.Y });
        main.Help.AfterItem(HelpTopicId.BarReset); main.Help.Badge(HelpTopicId.BarReset);
        ImGui.EndDisabled();
    }

    public void DrawSettings()
    {
        if (!main.Help.Section(T("Schwebende RP-Leiste", "Floating RP bar"), HelpTopicId.TargetContext, true)) return;
        DrawLayoutSettings();
        DrawProfileSelection();
    }

    private void Open(DisplayState display)
    {
        if (!IsCurrent(display)) return;
        if (display.Context.Kind == RpBarContextKind.Own) main.OpenOwnProfile();
        else if (display.Context.Kind == RpBarContextKind.Foreign && display.Context.Profile is { } key)
        {
            var owner = display.Owner;
            openForeign(() => main.ProfileAccessReady && scene() == owner.Scene &&
                RpBarContextResolver.Resolve(scene(), session.AccessReady, resolver) is { Kind: RpBarContextKind.Foreign, Profile: { } current } && current == key
                ? resolver.Resolve(owner.Scene.Target) : null);
        }
    }

    private bool CanMutate()
    {
        session.SetEditorBlocked(main.EditorBlocked);
        return main.ProfileAccessReady && session.AccessReady && session.HasCharacter && session.SettingsLoaded &&
            !main.EditorBlocked && !session.IsBusy && !session.IsLoading;
    }

    private void Run(DisplayState? display, Func<Task> action)
    {
        if (!CanMutate() || (display is not null && (!IsCurrent(display) || display.Context.Kind != RpBarContextKind.Own))) return;
        try { actions.Add(action()); }
        catch (Exception ex) when (Expected(ex))
        { feedback = T("Bitte zuerst speichern oder verwerfen und laufende Vorgänge abwarten.", "Save or discard the draft and wait for pending operations first."); }
    }

    private (bool Clicked, bool Hovered, bool Focused, Vector2 RectMin, Vector2 RectMax) IconButton(string id, string fallback, uint icon, float size)
    {
        var before = ImGui.GetCursorScreenPos();
        var clicked = ImGui.Button((icon == 0 ? fallback : "") + "##" + id, new Vector2(size));
        var hovered = ImGui.IsItemHovered(); var focused = ImGui.IsItemFocused();
        var minimum = ImGui.GetItemRectMin(); var maximum = ImGui.GetItemRectMax();
        if (icon != 0)
        {
            var after = ImGui.GetCursorScreenPos(); ImGui.SetCursorScreenPos(before + new Vector2(2 * Unit));
            main.DrawLocalIcon(icon, new Vector2(MathF.Max(1, size - 4 * Unit))); ImGui.SetCursorScreenPos(after);
        }
        return (clicked, hovered, focused, minimum, maximum);
    }

    private void CapturePosition()
    {
        var viewport = ImGui.GetMainViewport(); measuredSize = ImGui.GetWindowSize();
        var actual = ImGui.GetWindowPos() - viewport.WorkPos;
        var clamped = RpBarGeometry.Clamp(actual, measuredSize, viewport.WorkSize);
        if (Vector2.Distance(clamped, new Vector2(session.Settings.PositionX, session.Settings.PositionY)) > .5f)
            session.SetPreferences(session.Settings with { PositionX = clamped.X, PositionY = clamped.Y });
        lastPreferencePosition = clamped; forcePosition = Vector2.Distance(actual, clamped) > .5f;
    }

    private void ClearGlance() { glanceIndex = -1; glanceOwner = null; glancePinned = false; glanceHovered = false; glanceUntil = 0; }
    private void OnPolicyChanged(object? sender, EventArgs args) => Interlocked.Increment(ref policyEpoch);
    private string AvailabilityLabel(RpAvailability value) => main.T("Availability." + value);
    private static string T(string german, string english) => UiText.CurrentLanguage == UiLanguage.English ? english : german;
    private static string Fit(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width) return text;
        while (text.Length > 1 && ImGui.CalcTextSize(text + "…").X > width) text = text[..^1];
        return text + "…";
    }
    private static void ConstrainTooltip()
    {
        var viewport = ImGui.GetMainViewport();
        var width = MathF.Min(ImGui.GetFontSize() * 26, MathF.Max(1, viewport.WorkSize.X - 16));
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, MathF.Max(1, viewport.WorkSize.Y - 16)));
    }
    private static void HoverText(string text) { ConstrainTooltip(); ImGui.BeginTooltip(); Wrapped(text); ImGui.EndTooltip(); }
    private static void Wrapped(string text) { ImGui.PushTextWrapPos(0); try { ImGui.TextUnformatted(text); } finally { ImGui.PopTextWrapPos(); } }
    private static bool Expected(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidDataException or
        InvalidOperationException or ArgumentException or OperationCanceledException or OverflowException;

    public override void OnClose()
    {
        ClearGlance();
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; ClearGlance(); policy.Changed -= OnPolicyChanged;
        _ = Task.WhenAll(actions).ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
