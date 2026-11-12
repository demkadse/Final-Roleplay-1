using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FRP1.Core.Profiles;
using FRP1.Plugin.Contacts;
using FRP1.Plugin.Presentation;
using FRP1.Plugin.RpBar;

namespace FRP1.Plugin.UiSocial;

public sealed partial class SocialUiModule
{
    private sealed record ForeignContentOwner(ActorSnapshot Actor, ProfileKey Key, long Revision, long Version, long Generation);
    private ForeignContentOwner? glanceOwner;
    private int foreignGlance = -1;
    private int foreignDetail = -1;
    private ActorSnapshot? pinnedTarget;
    private ProfileKey? pinnedTargetKey;

    private ActorSnapshot? CurrentTarget => pinnedTarget ?? Volatile.Read(ref scene).Target;
    private void ClearPinnedTarget() { pinnedTarget = null; pinnedTargetKey = null; glanceOwner = null; foreignGlance = foreignDetail = -1; }

    public void OpenTargetDetails()
    {
        if (!AccessReady || Volatile.Read(ref scene).Target is not { } actor) return;
        var view = resolver.Resolve(actor);
        if (!view.IsReadable || view.Key is not { } key) return;
        pinnedTarget = actor; pinnedTargetKey = key;
        SetOptions(options with { TargetVisible = true }); targetWindow.IsOpen = true;
    }

    private bool IsCurrentForeign(ForeignContentOwner owner, bool mouseover = false)
    {
        if (!AccessReady || owner.Generation != generation || owner.Version != resolver.Version ||
            owner.Actor != (mouseover ? Volatile.Read(ref scene).Mouseover : CurrentTarget)) return false;
        if (!mouseover && pinnedTarget.HasValue && pinnedTargetKey != owner.Key) return false;
        var live = resolver.Resolve(owner.Actor);
        return live.IsReadable && live.Key == owner.Key && live.Profile?.Revision == owner.Revision && owner.Version == resolver.Version;
    }

    public void DrawOverlays()
    {
        if (!AccessReady || !Volatile.Read(ref overlaysVisible) || !options.MouseoverTooltip || ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow) || ImGui.IsAnyItemActive()) return;
        if (Volatile.Read(ref scene).Mouseover is not { } actor) return;
        var version = resolver.Version;
        var view = resolver.Resolve(actor);
        if (!view.IsReadable || view.Profile is not { } profile || view.Key is not { } key) return;
        var owner = new ForeignContentOwner(actor, key, profile.Revision, version, generation);
        if (!IsCurrentForeign(owner, true)) return;
        var available = Vector2.Max(Vector2.One, ImGui.GetMainViewport().WorkSize - new Vector2(16));
        var width = MathF.Min(310 * ImGuiHelpers.GlobalScale, available.X);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, available.Y));
        ImGui.BeginTooltip();
        try
        {
            DrawSummary(view, profile, owner, true);
            foreach (var glance in profile.Glances.Where(glance => glance.Enabled).Take(5))
            {
                if (!IsCurrentForeign(owner, true)) return;
                Text("• " + Preview(glance.Label, 60));
                if (options.MouseoverDetail == MouseoverDetail.Detailed) Text(Preview(glance.Text, 160));
            }
        }
        finally { ImGui.EndTooltip(); }
    }

    public void DrawWindows() => DrawOverlays();

    internal bool CanDrawTarget => AccessReady && Volatile.Read(ref overlaysVisible) && options.TargetVisible && CurrentTarget.HasValue;
    internal void CloseTarget() { SetOptions(options with { TargetVisible = false }); glanceOwner = null; foreignGlance = foreignDetail = -1; }

    internal void DrawTarget()
    {
        if (!AccessReady || CurrentTarget is not { } actor) return;
        var version = resolver.Version;
        var view = resolver.Resolve(actor);
        if (!view.IsReadable || view.Profile is not { } profile || view.Key is not { } key)
        {
            glanceOwner = null; foreignGlance = foreignDetail = -1;
            Text(view.State == ProfilePresentationState.Ambiguous ? T("Zuordnung mehrdeutig. Im Kontakt klären.", "Ambiguous assignment. Review the contact.") : T("Kein erlaubtes RP-Profil.", "No permitted RP profile."));
            return;
        }
        var owner = new ForeignContentOwner(actor, key, profile.Revision, version, generation);
        if (!IsCurrentForeign(owner)) return;
        if (glanceOwner != owner) { glanceOwner = owner; foreignGlance = foreignDetail = -1; }
        DrawSummary(view, profile, owner);
        DrawForeignDetailPopup(owner);
        if (!IsCurrentForeign(owner)) return;
        if (ImGui.SmallButton(T("Profil", "Profile"))) Open(key);
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Notiz", "Note"))) ShowNotes(key);
        ImGui.SameLine();
        if (ImGui.SmallButton(pinnedTarget.HasValue ? T("Lösen", "Unpin") : T("Anheften", "Pin")))
        {
            if (pinnedTarget.HasValue) ClearPinnedTarget();
            else if (IsCurrentForeign(owner)) { pinnedTarget = actor; pinnedTargetKey = key; }
            return;
        }
        if (ImGui.BeginPopupContextWindow("target-actions", ImGuiPopupFlags.MouseButtonRight))
        {
            try
            {
                if (!IsCurrentForeign(owner)) ImGui.CloseCurrentPopup();
                else if (ImGui.MenuItem(options.TargetLocked ? T("Position entsperren", "Unlock position") : T("Position sperren", "Lock position")))
                    SetOptions(options with { TargetLocked = !options.TargetLocked });
            }
            finally { ImGui.EndPopup(); }
        }
        var enabled = profile.Glances.Where(g => g.Enabled).Take(5).ToArray();
        if (enabled.Length > 0) ImGui.Separator();
        for (var index = 0; index < enabled.Length; index++)
        {
            if (!IsCurrentForeign(owner)) return;
            ImGui.PushID(index);
            var glance = enabled[index];
            DrawIcon(glance.IconId);
            if (ImGui.SmallButton((glance.Label.Length == 0 ? T("Blick ", "Glance ") + (index + 1) : Preview(glance.Label, 40)) + "##glance"))
            { foreignGlance = index; ImGui.OpenPopup("foreign-glance"); }
            DrawContentHover(glance.Label, glance.Text, owner);
            DrawForeignGlancePopup(owner);
            ImGui.PopID();
        }
    }

    private void DrawForeignGlancePopup(ForeignContentOwner owner)
    {
        ConstrainForeignContent();
        if (!ImGui.BeginPopup("foreign-glance")) return;
        try
        {
            if (glanceOwner != owner || !IsCurrentForeign(owner)) { ImGui.CloseCurrentPopup(); return; }
            var glances = resolver.Resolve(owner.Actor).Profile?.Glances.Where(g => g.Enabled).Take(5).ToArray();
            if (!IsCurrentForeign(owner) || glances is null || foreignGlance < 0 || foreignGlance >= glances.Length)
            { ImGui.CloseCurrentPopup(); return; }
            DrawFullContent(glances[foreignGlance].Label, glances[foreignGlance].Text, owner);
        }
        finally { ImGui.EndPopup(); }
    }

    private void DrawForeignDetailPopup(ForeignContentOwner owner)
    {
        ConstrainForeignContent();
        if (!ImGui.BeginPopup("target-detail")) return;
        try
        {
            if (glanceOwner != owner || !IsCurrentForeign(owner) || foreignDetail is < 0 or > 1)
            { ImGui.CloseCurrentPopup(); return; }
            var profile = resolver.Resolve(owner.Actor).Profile;
            if (profile is null || !IsCurrentForeign(owner)) { ImGui.CloseCurrentPopup(); return; }
            DrawFullContent(foreignDetail == 0 ? T("Aktuelles", "Currently") : T("OOC-Info", "OOC info"),
                foreignDetail == 0 ? profile.Summary : profile.OocInformation, owner);
        }
        finally { ImGui.EndPopup(); }
    }

    private void DrawContentHover(string label, string content, ForeignContentOwner owner)
    {
        if ((!ImGui.IsItemHovered() && !ImGui.IsItemFocused()) || !IsCurrentForeign(owner)) return;
        ConstrainForeignContent();
        ImGui.BeginTooltip();
        try { DrawFullContent(label, content, owner); }
        finally { ImGui.EndTooltip(); }
    }

    private static void ConstrainForeignContent()
    {
        var available = Vector2.Max(Vector2.One, ImGui.GetMainViewport().WorkSize - new Vector2(16));
        var width = MathF.Min(380 * ImGuiHelpers.GlobalScale, available.X);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, available.Y));
    }

    private void DrawFullContent(string label, string content, ForeignContentOwner owner)
    {
        if (!IsCurrentForeign(owner)) return;
        var scale = ImGuiHelpers.GlobalScale;
        var available = Vector2.Max(Vector2.One, ImGui.GetMainViewport().WorkSize - new Vector2(40));
        var width = MathF.Max(1, MathF.Min(350 * scale, available.X));
        var height = MathF.Min(MathF.Min(360 * scale, available.Y), MathF.Max(60 * scale, ImGui.CalcTextSize(content, false, width).Y + 50 * scale));
        ImGui.BeginChild("##foreign-full-content", new Vector2(0, height), false);
        try
        {
            Text(label); ImGui.Separator();
            if (IsCurrentForeign(owner)) Text(content);
        }
        finally { ImGui.EndChild(); }
    }

    private void DrawSummary(ResolvedProfile view, ProfileDocument profile, ForeignContentOwner owner, bool mouseover = false)
    {
        if (!IsCurrentForeign(owner, mouseover)) return;
        DrawIcon(profile.AppearanceStyle.IconId); Text(view.DisplayName);
        if (!IsCurrentForeign(owner, mouseover)) return;
        if (profile.Title.Length != 0) Text(Preview(profile.Title, 90));
        if (!IsCurrentForeign(owner, mouseover)) return;
        Text(profile.Status.ToString().ToUpperInvariant() + " · " + AvailabilityLabel(profile.Availability));
        if (profile.Summary.Length != 0)
        {
            if (!IsCurrentForeign(owner, mouseover)) return;
            if (!mouseover)
            {
                if (ImGui.SmallButton(T("Aktuelles", "Currently"))) { foreignDetail = 0; ImGui.OpenPopup("target-detail"); }
                DrawContentHover(T("Aktuelles", "Currently"), profile.Summary, owner);
            }
            if (IsCurrentForeign(owner, mouseover)) Text(Preview(profile.Summary, 160));
        }
        if (profile.CurrentLocation.Length != 0 && IsCurrentForeign(owner, mouseover))
            Text(T("Ort: ", "Location: ") + Preview(profile.CurrentLocation, 80));
        if (!mouseover && profile.OocInformation.Length != 0 && IsCurrentForeign(owner))
        {
            if (ImGui.SmallButton(T("OOC-Info", "OOC info"))) { foreignDetail = 1; ImGui.OpenPopup("target-detail"); }
            DrawContentHover(T("OOC-Info", "OOC info"), profile.OocInformation, owner);
        }
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
        private Vector2 measured = new(310, 230);
        public SocialTargetWindow(SocialUiModule module) : base(T("RP-Ziel##FRP1ForeignTarget", "RP target##FRP1ForeignTarget"))
        {
            owner = module; IsOpen = true; Size = new Vector2(310, 230); SizeCondition = ImGuiCond.FirstUseEver;
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
            Flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize | (owner.options.TargetLocked ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None);
            var viewport = ImGui.GetMainViewport();
            var pos = RpBarGeometry.Clamp(new Vector2(owner.options.TargetX, owner.options.TargetY), measured, viewport.WorkSize);
            if (!initialized || forcePosition || pos != new Vector2(owner.options.TargetX, owner.options.TargetY))
            { ImGui.SetNextWindowPos(viewport.WorkPos + pos); initialized = true; forcePosition = false; }
            var width = MathF.Min(310 * ImGuiHelpers.GlobalScale, MathF.Max(1, viewport.WorkSize.X - 16));
            ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, MathF.Max(1, viewport.WorkSize.Y - 16)));
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
