using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FRP1.Core.Profiles;
using FRP1.Plugin.RpBar;

namespace FRP1.Plugin;

internal sealed class RpBarWindow(OwnProfileSession session, MainWindow main) : Window("Mein RP##FRP1OwnBar"), IDisposable
{
    public bool OverlaysVisible { get; set; } = true;
    private readonly List<Task> actions = [];
    private IReadOnlyList<ProfileDocument> catalog = [];
    private long catalogVersion = -1;
    private bool disposed;
    private bool positionInitialized;
    private bool visibilityInitialized;
    private bool forcePosition;
    private Vector2 lastPreferencePosition;
    private Vector2 lastWorkPosition;
    private Vector2 lastWorkSize;
    private Vector2 measuredSize;
    private float lastScale;
    private int requestedVisibility;
    private int glanceIndex = -1;
    private Guid glanceProfile;
    private string? feedback;

    public void Poll()
    {
        if (disposed) return;
        foreach (var action in actions.Where(action => action.IsCompleted).ToArray())
        {
            actions.Remove(action);
            try { action.GetAwaiter().GetResult(); feedback = null; }
            catch (Exception ex) when (Expected(ex))
            { feedback = "Die Änderung konnte nicht gespeichert werden. Bitte den Entwurf und laufende Vorgänge prüfen."; }
        }
        if (catalogVersion != session.Version)
        {
            catalogVersion = session.Version;
            catalog = main.ProfileAccessReady ? session.Profiles : [];
        }
        if (session.SettingsLoaded)
        {
            // Preserve a native close even if the window system reports OnClose
            // on its next pass; policy-driven hiding never changes IsOpen.
            if (visibilityInitialized && requestedVisibility == 0 && !IsOpen && session.Settings.Visible)
                session.SetPreferences(session.Settings with { Visible = false });
            if (requestedVisibility != 0)
            {
                var visible = requestedVisibility == 1 || !session.Settings.Visible;
                session.SetPreferences(session.Settings with { Visible = visible });
                requestedVisibility = 0;
            }
            IsOpen = session.Settings.Visible;
            visibilityInitialized = true;
        }
    }

    public void RequestShow(bool toggle = false)
    {
        requestedVisibility = toggle ? 2 : 1;
        if (!main.ProfileAccessReady || !session.HasCharacter) main.IsOpen = true;
    }

    public override bool DrawConditions()
    {
        var available = OverlaysVisible && main.ProfileAccessReady && session.AccessReady && session.HasCharacter && session.SettingsLoaded;
        if (!available) { glanceIndex = -1; glanceProfile = Guid.Empty; }
        return available;
    }

    public override void PreDraw()
    {
        var settings = session.Settings;
        RespectCloseHotkey = false;
        Flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing;
        if (settings.Locked) Flags |= ImGuiWindowFlags.NoMove;
        var viewport = ImGui.GetMainViewport();
        var available = Vector2.Max(Vector2.One, viewport.WorkSize - new Vector2(16));
        var width = MathF.Min(610 * settings.Scale, available.X);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, available.Y));
        var stored = new Vector2(settings.PositionX, settings.PositionY);
        var size = new Vector2(width, measuredSize.Y > 0 ? measuredSize.Y : MathF.Min(220 * settings.Scale, available.Y));
        var clamped = RpBarGeometry.Clamp(stored, size, viewport.WorkSize);
        if (!positionInitialized || forcePosition || Vector2.Distance(stored, lastPreferencePosition) > .5f ||
            viewport.WorkPos != lastWorkPosition || viewport.WorkSize != lastWorkSize || settings.Scale != lastScale)
            ImGui.SetNextWindowPos(viewport.WorkPos + clamped, ImGuiCond.Always);
        positionInitialized = true;
        forcePosition = false;
        lastPreferencePosition = stored;
        lastWorkPosition = viewport.WorkPos;
        lastWorkSize = viewport.WorkSize;
        lastScale = settings.Scale;
    }

    public override void Draw()
    {
        var scale = session.Settings.Scale;
        var padding = ImGui.GetStyle().FramePadding * scale;
        var spacing = ImGui.GetStyle().ItemSpacing * scale;
        ImGui.SetWindowFontScale(scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, padding);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, spacing);
        try
        {
            DrawContents(scale);
            CapturePosition();
        }
        finally
        {
            ImGui.PopStyleVar(2);
            ImGui.SetWindowFontScale(1f);
        }
    }

    private void DrawContents(float scale)
    {
        if (!DrawConditions()) return;
        ImGui.TextUnformatted("Eigenes RP-Profil");
        var active = session.ActiveProfile;
        if (active is null) Wrapped("Noch kein aktives Profil. Bitte ein gespeichertes Profil ausdrücklich zuordnen.");
        else
        {
            Wrapped((active.ContentRating == ContentRating.Mature18 ? "[AB 18] " : "") + active.DisplayName);
            if (active.Title.Length != 0) Wrapped(active.Title);
        }
        if (ImGui.Button("Profil öffnen")) main.OpenOwnProfile();
        main.Help.AfterItem(HelpTopicId.BarOpen);
        main.Help.Badge(HelpTopicId.BarVisible);
        DrawProfileSelection();
        var blocked = !CanMutate();
        if (blocked) Wrapped(main.EditorBlocked ? "Entwurf zuerst speichern oder verwerfen; laufende Vorgänge abwarten." : "Profilvorgang läuft …");
        if (feedback is not null) Wrapped(feedback);
        if (active is null) return;
        ImGui.BeginDisabled(blocked);
        var rowFits = ImGui.GetContentRegionAvail().X >= 3 * (58 * scale + spacingWidth(scale));
        var statuses = new[] { (RpStatus.Ic, "IC"), (RpStatus.Ooc, "OOC"), (RpStatus.Afk, "AFK") };
        for (var index = 0; index < statuses.Length; index++)
        {
            if (index != 0 && rowFits) ImGui.SameLine();
            var entry = statuses[index];
            var selected = active.Status == entry.Item1;
            if (selected) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.Header]);
            try
            {
                if (ImGui.Button(entry.Item2, new Vector2(58 * scale, 0))) Run(() => session.UpdateStatusAsync(entry.Item1));
                main.Help.AfterItem(HelpTopicId.Status);
            }
            finally { if (selected) ImGui.PopStyleColor(); }
        }
        ImGui.TextUnformatted(main.T("Availability.Label"));
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##own-availability", AvailabilityLabel(active.Availability)))
        {
            foreach (var value in Enum.GetValues<RpAvailability>())
                if (ImGui.Selectable(AvailabilityLabel(value), active.Availability == value)) Run(() => session.UpdateAvailabilityAsync(value));
            ImGui.EndCombo();
        }
        main.Help.AfterItem(HelpTopicId.Availability);
        ImGui.EndDisabled();
        DrawGlances(active, scale);
    }

    private static float spacingWidth(float scale) => 10 * scale;

    private bool CanMutate()
    {
        session.SetEditorBlocked(main.EditorBlocked);
        return main.ProfileAccessReady && session.AccessReady && session.HasCharacter && session.SettingsLoaded &&
            !main.EditorBlocked && !session.IsBusy && !session.IsLoading;
    }

    private void Run(Func<Task> action)
    {
        if (!CanMutate()) return;
        try { actions.Add(action()); }
        catch (Exception ex) when (Expected(ex))
        { feedback = "Änderung derzeit gesperrt. Bitte zuerst speichern oder verwerfen und laufende Vorgänge abwarten."; }
    }

    private void DrawProfileSelection()
    {
        ImGui.BeginDisabled(!CanMutate());
        ImGui.TextUnformatted(main.T("Profile.Active"));
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##active-own-profile", "Profil wählen"))
        {
            var activeId = session.ActiveProfile?.Id;
            foreach (var profile in catalog)
            {
                // Catalog snapshots are replaced on Version changes; policy is checked live as well.
                if (!main.ProfileAccessReady || !session.AccessReady || !main.CanDisplay(profile)) continue;
                ImGui.PushID(profile.Id.ToString("N"));
                if (MainWindow.ProfileSelectable(profile, activeId == profile.Id))
                    Run(() => session.SelectActiveAsync(profile.Id));
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }
        main.Help.AfterItem(HelpTopicId.ProfileSelect);
        ImGui.EndDisabled();
    }

    private void DrawGlances(ProfileDocument profile, float scale)
    {
        if (!profile.Glances.Any(entry => entry.Enabled)) return;
        ImGui.Separator();
        ImGui.TextUnformatted(main.T("Profile.Glances"));
        var width = ImGui.GetContentRegionAvail().X;
        var columns = Math.Clamp((int)(width / (95 * scale)), 1, 5);
        var cell = MathF.Max(1, (width - (columns - 1) * spacingWidth(scale)) / columns);
        var shown = 0;
        for (var index = 0; index < profile.Glances.Count; index++)
        {
            if (!profile.Glances[index].Enabled) continue;
            if (shown++ % columns != 0) ImGui.SameLine();
            ImGui.PushID(index);
            var before = ImGui.GetCursorScreenPos();
            var clicked = ImGui.Button("##own-glance", new Vector2(cell, 0));
            main.Help.AfterItem(HelpTopicId.Glances);
            var after = ImGui.GetCursorScreenPos();
            var label = profile.Glances[index].Label.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            var fullLabel = label;
            while (label.Length > 1 && ImGui.CalcTextSize(label + "…").X > cell - 20 * scale) label = label[..^1];
            if (label != fullLabel) label += "…";
            ImGui.SetCursorScreenPos(before + ImGui.GetStyle().FramePadding);
            ImGui.TextUnformatted(label);
            ImGui.SetCursorScreenPos(after);
            if (clicked && session.ActiveProfile is { } allowed && allowed.Id == profile.Id)
            {
                glanceIndex = index;
                glanceProfile = profile.Id;
                ImGui.OpenPopup("own-glance-detail");
            }
            DrawGlancePopup();
            ImGui.PopID();
        }
    }

    private void DrawGlancePopup()
    {
        var available = Vector2.Max(Vector2.One, ImGui.GetMainViewport().WorkSize - new Vector2(16));
        var width = MathF.Min(ImGui.GetFontSize() * 29, available.X);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, available.Y));
        if (!ImGui.BeginPopup("own-glance-detail")) return;
        try
        {
            var current = session.ActiveProfile;
            if (!main.ProfileAccessReady || current is null || current.Id != glanceProfile || glanceIndex < 0 ||
                glanceIndex >= current.Glances.Count || !current.Glances[glanceIndex].Enabled)
            { ImGui.CloseCurrentPopup(); return; }
            var glance = current.Glances[glanceIndex];
            main.DrawLocalIcon(glance.IconId, new Vector2(28));
            ImGui.PushTextWrapPos(0);
            try { ImGui.TextUnformatted(glance.Label); ImGui.Separator(); ImGui.TextUnformatted(glance.Text); }
            finally { ImGui.PopTextWrapPos(); }
            if (ImGui.Button(main.T("Common.Close") + "###CloseOwnGlance")) ImGui.CloseCurrentPopup();
        }
        finally { ImGui.EndPopup(); }
    }

    private void CapturePosition()
    {
        var viewport = ImGui.GetMainViewport();
        measuredSize = ImGui.GetWindowSize();
        var actual = ImGui.GetWindowPos() - viewport.WorkPos;
        var clamped = RpBarGeometry.Clamp(actual, measuredSize, viewport.WorkSize);
        var settings = session.Settings;
        if (Vector2.Distance(clamped, new Vector2(settings.PositionX, settings.PositionY)) > .5f)
            session.SetPreferences(settings with { PositionX = clamped.X, PositionY = clamped.Y });
        lastPreferencePosition = clamped;
        forcePosition = Vector2.Distance(actual, clamped) > .5f;
    }

    public void DrawSettings()
    {
        if (!ImGui.CollapsingHeader("RP-Leiste", ImGuiTreeNodeFlags.DefaultOpen)) return;
        main.Help.Badge(HelpTopicId.BarVisible);
        ImGui.BeginDisabled(!session.SettingsLoaded);
        var settings = session.Settings;
        var visible = settings.Visible;
        if (ImGui.Checkbox(main.T("Bar.Visible") + "###BarVisible", ref visible)) session.SetPreferences(session.Settings with { Visible = visible });
        main.Help.AfterItem(HelpTopicId.BarVisible);
        var locked = session.Settings.Locked;
        if (ImGui.Checkbox(main.T("Bar.Locked") + "###BarLocked", ref locked)) session.SetPreferences(session.Settings with { Locked = locked });
        main.Help.AfterItem(HelpTopicId.BarLock);
        var scale = session.Settings.Scale;
        if (ImGui.SliderFloat(main.T("Bar.Scale") + "###BarScale", ref scale, .75f, 1.75f, "%.2f")) session.SetPreferences(session.Settings with { Scale = scale });
        main.Help.AfterItem(HelpTopicId.BarScale);
        if (ImGui.Button("Leiste anzeigen und Position zurücksetzen"))
            session.SetPreferences(session.Settings with { Visible = true, PositionX = RpBarGeometry.ResetPosition.X, PositionY = RpBarGeometry.ResetPosition.Y });
        main.Help.AfterItem(HelpTopicId.BarReset);
        ImGui.EndDisabled();
        if (!session.HasCharacter) Wrapped("Die eigene RP-Leiste bleibt ohne angemeldeten Spielcharakter ausgeblendet.");
        DrawProfileSelection();
    }

    public override void OnClose()
    {
        if (!disposed && session.SettingsLoaded && session.Settings.Visible)
            session.SetPreferences(session.Settings with { Visible = false });
    }

    private string AvailabilityLabel(RpAvailability value) => main.T("Availability." + value);
    private static void Wrapped(string text)
    { ImGui.PushTextWrapPos(0); try { ImGui.TextUnformatted(text); } finally { ImGui.PopTextWrapPos(); } }
    private static bool Expected(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidDataException or
        InvalidOperationException or ArgumentException or OperationCanceledException or OverflowException;

    public void Dispose()
    {
        disposed = true;
        _ = Task.WhenAll(actions).ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
