using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using FRP1.Core.Profiles;
using FRP1.Plugin.Contacts;
using FRP1.Plugin.Presentation;
using FRP1.Plugin.RpBar;

namespace FRP1.Plugin.UiSocial;

public sealed partial class SocialUiModule
{
    private ProfileKey? glanceOwner;
    private int foreignGlance = -1;
    private ActorSnapshot? pinnedTarget;
    private ProfileKey? pinnedTargetKey;

    private ActorSnapshot? CurrentTarget => pinnedTarget ?? Volatile.Read(ref scene).Target;
    private void ClearPinnedTarget() { pinnedTarget = null; pinnedTargetKey = null; glanceOwner = null; foreignGlance = -1; }

    public void OpenTargetDetails()
    {
        if (!AccessReady || Volatile.Read(ref scene).Target is not { } actor) return;
        var view = resolver.Resolve(actor);
        if (!view.IsReadable || view.Key is not { } key) return;
        pinnedTarget = actor; pinnedTargetKey = key;
        SetOptions(options with { TargetVisible = true }); targetWindow.IsOpen = true;
    }

    public void DrawOverlays()
    {
        if (!AccessReady || !Volatile.Read(ref overlaysVisible) || !options.MouseoverTooltip || ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow) || ImGui.IsAnyItemActive()) return;
        var view = resolver.Resolve(Volatile.Read(ref scene).Mouseover);
        if (!view.IsReadable || view.Profile is not { } profile) return;
        var available = Vector2.Max(Vector2.One, ImGui.GetMainViewport().WorkSize - new Vector2(16));
        var width = MathF.Min(ImGui.GetFontSize() * 29, available.X);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, available.Y));
        ImGui.BeginTooltip();
        try
        {
            if (!view.IsReadable) return;
            var compact = options.MouseoverDetail == MouseoverDetail.Compact;
            DrawSummary(view, profile, compact, true);
            if (!compact)
                foreach (var glance in profile.Glances.Where(glance => glance.Enabled).Take(5))
                {
                    if (!view.IsReadable) break;
                    ImGui.Separator(); DrawIcon(glance.IconId); Text(glance.Label); Text(Preview(glance.Text, 480));
                }
            Text(T("Lange Inhalte in der Zielkarte oder im vollständigen Profil lesen.", "Read long content in the target card or full profile."));
        }
        finally { ImGui.EndTooltip(); }
    }

    public void DrawWindows() => DrawOverlays();

    internal bool CanDrawTarget => AccessReady && Volatile.Read(ref overlaysVisible) && options.TargetVisible && CurrentTarget.HasValue;
    internal void CloseTarget() => SetOptions(options with { TargetVisible = false });

    internal void DrawTarget()
    {
        var view = resolver.Resolve(CurrentTarget);
        if (!view.IsReadable || view.Profile is not { } profile || view.Key is not { } key)
        {
            Text(view.State switch
            {
                ProfilePresentationState.Ambiguous => T("Mehrdeutige lokale Zuordnung. Bitte im Kontaktregister klären.", "Ambiguous local assignment. Resolve it in the contact register."),
                ProfilePresentationState.Restricted or ProfilePresentationState.Blocked or ProfilePresentationState.Muted => T("Profil nicht verfügbar.", "Profile unavailable."),
                _ => T("Kein bekanntes, ausdrücklich zugeordnetes RP-Profil für dieses Ziel.", "No known, explicitly assigned RP profile for this target."),
            });
            return;
        }
        DrawSummary(view, profile, false);
        if (ImGui.Button(pinnedTarget.HasValue ? T("Zielkarte freigeben", "Release target card") : T("Dieses Ziel festhalten", "Pin this target")))
        {
            if (pinnedTarget.HasValue) ClearPinnedTarget();
            else if (CurrentTarget is { } actor && view.IsReadable) { pinnedTarget = actor; pinnedTargetKey = key; }
            return;
        }
        if (pinnedTarget.HasValue) Text(T("Festgehaltene lokale Zuordnung; Berechtigung und Bindung werden weiterhin geprüft.",
            "Pinned local assignment; permissions and binding are still checked."));
        if (ImGui.Button(T("Profil öffnen", "Open profile"))) Open(key);
        ImGui.SameLine();
        if (ImGui.Button(T("Private Notiz / Beziehung", "Private note / relationship"))) ShowNotes(key);
        if (glanceOwner != key) { glanceOwner = key; foreignGlance = -1; }
        var enabled = profile.Glances.Where(g => g.Enabled).Take(5).ToArray();
        for (var index = 0; index < enabled.Length; index++)
        {
            ImGui.PushID(index);
            var glance = enabled[index];
            DrawIcon(glance.IconId);
            if (ImGui.Button(T("Blick ", "Glance ") + (index + 1))) { foreignGlance = index; ImGui.OpenPopup("foreign-glance"); }
            var hovered = ImGui.IsItemHovered() || ImGui.IsItemFocused();
            ImGui.SameLine(); ImGui.TextUnformatted(glance.Label);
            if ((hovered || ImGui.IsItemHovered()) && view.IsReadable)
            {
                var available = Vector2.Max(Vector2.One, ImGui.GetMainViewport().WorkSize - new Vector2(16));
                var width = MathF.Min(ImGui.GetFontSize() * 29, available.X);
                ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, available.Y));
                ImGui.BeginTooltip(); Text(glance.Text); ImGui.EndTooltip();
            }
            DrawForeignGlancePopup(key);
            ImGui.PopID();
        }
        if (ImGui.Button(options.TargetLocked ? T("Position entsperren", "Unlock position") : T("Position sperren", "Lock position")))
            SetOptions(options with { TargetLocked = !options.TargetLocked });
    }

    private void DrawForeignGlancePopup(ProfileKey owner)
    {
        var available = Vector2.Max(Vector2.One, ImGui.GetMainViewport().WorkSize - new Vector2(16));
        var width = MathF.Min(ImGui.GetFontSize() * 29, available.X);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, available.Y));
        if (!ImGui.BeginPopup("foreign-glance")) return;
        try
        {
            var current = resolver.Resolve(CurrentTarget);
            var glances = current.Profile?.Glances.Where(g => g.Enabled).Take(5).ToArray();
            if (!AccessReady || !current.IsReadable || current.Key != owner || glanceOwner != owner || glances is null ||
                foreignGlance < 0 || foreignGlance >= glances.Length)
            { ImGui.CloseCurrentPopup(); return; }
            Text(glances[foreignGlance].Label); ImGui.Separator(); Text(glances[foreignGlance].Text);
            if (ImGui.Button(T("Schließen", "Close"))) ImGui.CloseCurrentPopup();
        }
        finally { ImGui.EndPopup(); }
    }

    private void DrawSummary(ResolvedProfile view, ProfileDocument profile, bool compact, bool tooltip = false)
    {
        if (!view.IsReadable) return;
        DrawIcon(profile.AppearanceStyle.IconId);
        ImGui.TextUnformatted(view.DisplayName);
        if (profile.Title.Length != 0) Text(profile.Title);
        Text(profile.Status switch { RpStatus.Ic => T("IC – im Charakter", "IC – in character"), RpStatus.Ooc => T("OOC – außerhalb des Charakters", "OOC – out of character"), _ => T("AFK – abwesend", "AFK – away") });
        Text(profile.Availability switch { RpAvailability.Open => T("Offen für RP", "Open for RP"), RpAvailability.Busy => T("Beschäftigt", "Busy"), _ => T("Kein RP", "No RP") });
        if (profile.Summary.Length != 0) { ImGui.Separator(); Text(T("Aktuelles", "Currently")); Text(compact ? Preview(profile.Summary, 220) : tooltip ? Preview(profile.Summary, 700) : profile.Summary); }
        if (profile.CurrentLocation.Length != 0) Text(T("Ort: ", "Location: ") + (compact ? Preview(profile.CurrentLocation, 80) : tooltip ? Preview(profile.CurrentLocation, 160) : profile.CurrentLocation));
        if (profile.OocInformation.Length != 0)
        {
            ImGui.Separator(); Text(T("Andere Informationen (OOC)", "Other information (OOC)"));
            Text(compact ? Preview(profile.OocInformation, 220) : tooltip ? Preview(profile.OocInformation, 700) : profile.OocInformation);
        }
        if (compact)
        {
            foreach (var glance in profile.Glances.Where(g => g.Enabled).Take(5))
                Text("• " + Preview(glance.Label, 60));
            Text(T("Vollständige Inhalte über die Zielkarte öffnen.", "Open full content using the target card."));
        }
        Text(view.Connected ? T("Direkt verbunden", "Directly connected") : T("Lokal zwischengespeichert", "Locally cached"));
    }

    private void DrawIcon(uint id)
    {
        if (id == 0 || textures is null) return;
        try
        {
            var texture = textures.GetFromGameIcon(new GameIconLookup(id)).GetWrapOrDefault();
            if (texture is null) return;
            ImGui.Image(texture.Handle, new Vector2(ImGui.GetFontSize() * 1.8f));
            ImGui.SameLine();
        }
        catch (Exception ex) when (Expected(ex)) { }
    }

    private static string Preview(string text, int limit) => text.Length <= limit ? text : text[..limit] + " …";

    private void ShowNotes(ProfileKey key)
    {
        if (selectedContact != key) SelectContact(key);
        if (selectedContact == key) notesWindow.IsOpen = true;
    }

    private sealed class SocialTargetWindow : Window
    {
        private readonly SocialUiModule owner;
        private bool initialized;
        private bool forcePosition;
        private bool lastVisible = true;
        private Vector2 measured = new(390, 400);
        public SocialTargetWindow(SocialUiModule module) : base(T("RP-Ziel##FRP1ForeignTarget", "RP target##FRP1ForeignTarget"))
        {
            owner = module; IsOpen = true; Size = new Vector2(390, 400); SizeCondition = ImGuiCond.FirstUseEver;
            Flags = ImGuiWindowFlags.NoSavedSettings;
        }
        public void ResetPosition() => forcePosition = true;
        public void ApplyVisibility()
        {
            if (lastVisible == owner.options.TargetVisible) return;
            lastVisible = owner.options.TargetVisible; IsOpen = lastVisible;
        }
        public override bool DrawConditions() => owner.CanDrawTarget;
        public override void PreDraw()
        {
            WindowName = T("RP-Ziel##FRP1ForeignTarget", "RP target##FRP1ForeignTarget");
            Flags = ImGuiWindowFlags.NoSavedSettings | (owner.options.TargetLocked ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None);
            var viewport = ImGui.GetMainViewport();
            var pos = RpBarGeometry.Clamp(new Vector2(owner.options.TargetX, owner.options.TargetY), measured, viewport.WorkSize);
            if (!initialized || forcePosition || pos != new Vector2(owner.options.TargetX, owner.options.TargetY))
            { ImGui.SetNextWindowPos(viewport.WorkPos + pos); initialized = true; forcePosition = false; }
            ImGui.SetNextWindowSizeConstraints(Vector2.Min(new Vector2(220, 100), viewport.WorkSize), Vector2.Max(Vector2.One, viewport.WorkSize));
        }
        public override void Draw()
        {
            owner.DrawTarget();
            var viewport = ImGui.GetMainViewport(); measured = ImGui.GetWindowSize();
            var pos = RpBarGeometry.Clamp(ImGui.GetWindowPos() - viewport.WorkPos, measured, viewport.WorkSize);
            if (Vector2.Distance(pos, new Vector2(owner.options.TargetX, owner.options.TargetY)) > .5f)
                owner.SetOptions(owner.options with { TargetX = pos.X, TargetY = pos.Y });
        }
        public override void OnClose() => owner.CloseTarget();
    }

    private sealed class SocialNotesWindow : Window
    {
        private readonly SocialUiModule owner;
        public SocialNotesWindow(SocialUiModule module) : base(T("Private RP-Notizen##FRP1ContactNotes", "Private RP notes##FRP1ContactNotes"))
        { owner = module; Size = new Vector2(600, 650); SizeCondition = ImGuiCond.FirstUseEver; }
        public override bool DrawConditions() => owner.AccessReady && owner.selectedContact is { } key && owner.resolver.GetProfile(key).IsReadable;
        public override void PreDraw() => WindowName = T("Private RP-Notizen##FRP1ContactNotes", "Private RP notes##FRP1ContactNotes");
        public override void Draw()
        {
            if (owner.selectedContact is { } key && owner.resolver.GetProfile(key) is { IsReadable: true } view)
                owner.DrawSelectedContact(view, key);
        }
    }
}
