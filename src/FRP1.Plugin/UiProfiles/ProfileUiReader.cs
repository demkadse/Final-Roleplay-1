using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FRP1.Core.Profiles;
using FRP1.Plugin.Profiles;

namespace FRP1.Plugin.UiProfiles;

public sealed partial class ProfileUiModule
{
    // An embedded own profile must never replace the independently opened journal.
    public void DrawOwnOverview(Func<ProfileDocument?> liveProfile)
    {
        ArgumentNullException.ThrowIfNull(liveProfile);
        if (!HasAccess) { ownReader.Clear(); return; }
        ownReader.Provider = liveProfile;
        ImGui.PushID("own-profile-reader");
        try { DrawReader(ownReader); }
        finally { ImGui.PopID(); }
    }

    private ProfileReadSnapshot? GetSnapshot(ReaderContext context)
    {
        if (!HasAccess || context.Provider is null) { context.ClearContent(); return null; }
        try
        {
            var profile = context.Provider();
            if (profile is null) { context.ClearContent(); return null; }
            policy.EnsureAllowed(profile);
            var hash = ProfileJson.Hash(profile);
            if (context.Reader is null || context.Hash != hash)
            {
                context.ClearGlance();
                var next = new ProfileReader(profile, policy, context.StandardPresentation);
                if (context.ProfileId != profile.Id)
                {
                    context.Area = ProfileArea.Overview;
                    context.ProfileId = profile.Id;
                    context.Portrait.Clear();
                }
                context.Reader = next;
                context.Additional = ExistingAdditionalContent.Read(profile);
                context.CharacterFields = ReadCharacterFields(profile);
                context.Hash = hash;
            }
            if (!context.Reader.TryRead(out var snapshot)) { context.ClearContent(); return null; }
            context.Portrait.Set(snapshot!.Portrait);
            return snapshot;
        }
        catch (Exception ex) when (Expected(ex))
        { context.ClearContent(); return null; }
    }

    private void DrawJournal() => DrawReader(journalReader);

    private void DrawReader(ReaderContext context)
    {
        var view = GetSnapshot(context);
        if (view is null) { Wrapped(ProfileUiText.Get("Dieses Profil ist derzeit nicht verfügbar.")); return; }
        var frame = ImGui.GetFrameCount();
        if (context.LastDrawFrame < frame - 1) context.ClearGlance();
        context.LastDrawFrame = frame;
        ImGui.PushID(view.Id.ToString("N"));
        try
        {
            DrawReaderHeader(view, context);
            ProfileNavigation.Draw(ref context.Area);
            if (context.Area != ProfileArea.Overview) context.ClearGlance();
            switch (context.Area)
            {
                case ProfileArea.Overview:
                    if (!string.IsNullOrWhiteSpace(view.CurrentLocation))
                        Wrapped(ProfileUiText.Get("Aktueller Ort") + ": " + view.CurrentLocation);
                    if (!string.IsNullOrWhiteSpace(view.Summary)) Wrapped(view.Summary);
                    DrawReaderGlances(view, context);
                    break;
                case ProfileArea.Character:
                    DrawReaderCharacter(view, context);
                    break;
                case ProfileArea.Ooc:
                    DrawReaderOoc(view);
                    break;
            }
            DrawReaderGlanceCard(view, context);
        }
        finally { ImGui.PopID(); }
    }

    private void DrawReaderHeader(ProfileReadSnapshot view, ReaderContext context)
    {
        var portrait = context.Portrait;
        if (ImGui.BeginTable("profile-identity", portrait.Current is null ? 2 : 3, ImGuiTableFlags.SizingFixedFit))
        {
            if (portrait.Current is not null) ImGui.TableSetupColumn("Portrait", ImGuiTableColumnFlags.WidthFixed, 44);
            ImGui.TableSetupColumn("Identity", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
            if (portrait.Current is { } image)
            {
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                ImGui.Image(image.Handle, new Vector2(40));
                ImGui.TableNextColumn();
            }
            else { ImGui.TableNextRow(); ImGui.TableNextColumn(); }
            if (view.Appearance.IconId != 0) { DrawIcon(view.Appearance.IconId, new Vector2(18)); ImGui.SameLine(); }
            var colored = view.Appearance.NameColor.Length != 0;
            if (colored) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertU32ToFloat4(RichTextView.Color(view.Appearance.NameColor)));
            try { Wrapped(view.DisplayName); }
            finally { if (colored) ImGui.PopStyleColor(); }
            if (!string.IsNullOrWhiteSpace(view.Title)) Wrapped(view.Title);
            var status = Status(view.Status) + " · " + Availability(view.Availability);
            if (!string.IsNullOrWhiteSpace(view.Pronouns)) status += " · " + view.Pronouns;
            status += " · " + ProfileUiText.Get(view.ContentRating == ContentRating.Mature18 ? "Ab 18" : "Allgemein");
            Wrapped(status);
            ImGui.TableNextColumn();
            if (ImGui.SmallButton("…##reader-actions")) ImGui.OpenPopup("reader-appearance");
            if (ImGui.IsItemHovered() || ImGui.IsItemFocused())
            {
                ImGui.BeginTooltip(); Wrapped(ProfileUiText.Get("Darstellung")); ImGui.EndTooltip();
            }
            if (ImGui.BeginPopup("reader-appearance"))
            {
                try
                {
                    if (ImGui.Checkbox(ProfileUiText.Id("Standarddarstellung verwenden"), ref context.StandardPresentation))
                    { context.Reader = null; context.Hash = null; context.ClearGlance(); }
                }
                finally { ImGui.EndPopup(); }
            }
            ImGui.EndTable();
        }
    }

    private void DrawReaderGlances(ProfileReadSnapshot view, ReaderContext context)
    {
        if (view.Glances.Count == 0) return;
        var owner = new ReaderContentOwner(view.Id, view.Revision, context.Hash!, context.Generation, Volatile.Read(ref policyVersion));
        if (!IsCurrentReaderOwner(context, owner)) { context.ClearGlance(); return; }
        var size = MathF.Max(22, ImGui.GetFrameHeight());
        var available = ImGui.GetContentRegionAvail().X;
        var used = 0f;
        for (var index = 0; index < view.Glances.Count; index++)
        {
            var glance = view.Glances[index];
            if (used > 0 && used + ImGui.GetStyle().ItemSpacing.X + size <= available)
            { ImGui.SameLine(); used += ImGui.GetStyle().ItemSpacing.X; }
            else used = 0;
            ImGui.PushID(index);
            try
            {
                var icon = glance.IconId == 0 ? null : textures.GetFromGameIcon(glance.IconId).GetWrapOrDefault();
                var clicked = icon is null
                    ? ImGui.Button((index + 1).ToString(), new Vector2(size))
                    : ImGui.ImageButton(icon.Handle, new Vector2(MathF.Max(1, size - ImGui.GetStyle().FramePadding.X * 2)));
                if (ImGui.IsItemHovered() || ImGui.IsItemFocused() || clicked)
                {
                    if (context.GlanceOwner != owner || context.GlanceIndex != index)
                    { context.GlancePinned = false; context.ResetGlanceScroll = true; }
                    context.GlanceOwner = owner;
                    context.GlanceIndex = index;
                    context.GlanceUntil = Environment.TickCount64 + 500;
                    context.GlanceAnchor = new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y + 4);
                    if (clicked) context.GlancePinned = true;
                }
            }
            finally { ImGui.PopID(); }
            used += size;
        }
    }

    private bool IsCurrentReaderOwner(ReaderContext context, ReaderContentOwner owner)
    {
        if (!HasAccess || context.Generation != owner.Generation || context.Hash != owner.Hash ||
            context.ProfileId != owner.ProfileId || owner.PolicyVersion != Volatile.Read(ref policyVersion)) return false;
        try
        {
            var current = context.Provider?.Invoke();
            if (current is null || current.Id != owner.ProfileId || current.Revision != owner.Revision) return false;
            policy.EnsureAllowed(current);
            return ProfileJson.Hash(current) == owner.Hash;
        }
        catch (Exception ex) when (Expected(ex)) { return false; }
    }

    private void DrawReaderGlanceCard(ProfileReadSnapshot view, ReaderContext context)
    {
        if (context.GlanceOwner is not { } owner || context.GlanceIndex < 0) return;
        if (!IsCurrentReaderOwner(context, owner) || context.GlanceIndex >= view.Glances.Count)
        { context.ClearGlance(); return; }
        if (!context.GlancePinned && !context.GlanceHovered && Environment.TickCount64 > context.GlanceUntil)
        { context.ClearGlance(); return; }
        var glance = view.Glances[context.GlanceIndex];
        var viewport = ImGui.GetMainViewport();
        var available = Vector2.Max(Vector2.One, viewport.WorkSize - new Vector2(16));
        var width = MathF.Min(380, available.X);
        var textHeight = ImGui.CalcTextSize(glance.Text, false, MathF.Max(1, width - 32)).Y;
        var height = MathF.Min(available.Y, Math.Clamp(textHeight + 100, 120, 360));
        var size = new Vector2(width, height);
        var position = Vector2.Clamp(context.GlanceAnchor, viewport.WorkPos + new Vector2(8),
            Vector2.Max(viewport.WorkPos + new Vector2(8), viewport.WorkPos + viewport.WorkSize - size - new Vector2(8)));
        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        var open = true;
        var visible = ImGui.Begin(ProfileUiText.Get("Auf den ersten Blick") + "###FRP1ReaderGlance" + context.WindowSuffix, ref open,
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing);
        try
        {
            context.GlanceHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) ||
                ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            if (context.GlanceHovered) context.GlanceUntil = Environment.TickCount64 + 500;
            if (!visible || !IsCurrentReaderOwner(context, owner)) { context.ClearGlance(); return; }
            ImGui.BeginChild("##reader-glance-content", Vector2.Zero, false);
            try
            {
                if (context.ResetGlanceScroll) { ImGui.SetScrollY(0); context.ResetGlanceScroll = false; }
                var title = string.IsNullOrWhiteSpace(glance.Label) ? ProfileUiText.Get("Eindruck") + " " + (context.GlanceIndex + 1) : glance.Label;
                Wrapped(title); ImGui.Separator();
                if (IsCurrentReaderOwner(context, owner)) Wrapped(glance.Text);
                else context.ClearGlance();
            }
            finally { ImGui.EndChild(); }
        }
        finally { ImGui.End(); if (!open) context.ClearGlance(); }
    }

    private void DrawReaderCharacter(ProfileReadSnapshot view, ReaderContext context)
    {
        if (context.CharacterFields.Count != 0 && ImGui.BeginTable("character-facts", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthStretch, 1);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 2);
            foreach (var field in context.CharacterFields)
            {
                ImGui.TableNextRow(); ImGui.TableNextColumn(); Wrapped(Label(field.Key));
                ImGui.TableNextColumn(); Wrapped(field.Value);
            }
            ImGui.EndTable();
        }
        if (view.Languages.Count != 0) Wrapped(ProfileUiText.Get("RP-Sprachen: ") + string.Join(", ", view.Languages));
        var sections = view.Appearance.Layout == AboutLayout.Chronicle
            ? view.Sections.OrderBy(section => section.Key == "Background" ? 0 : 1)
            : view.Sections.AsEnumerable();
        foreach (var section in sections)
        {
            if (view.Appearance.Layout != AboutLayout.Compact) ImGui.Separator();
            DrawReaderHeading(Label(section.Key), view.Appearance.AccentColor);
            RichTextView.Draw(section.Blocks);
        }
        foreach (var entry in context.Additional)
        {
            ImGui.Separator();
            if (entry.IconId != 0) { DrawIcon(entry.IconId, new Vector2(18)); ImGui.SameLine(); }
            Wrapped(entry.Title);
            foreach (var value in entry.Values.Where(value => !string.IsNullOrWhiteSpace(value.Value)))
                Wrapped(ProfileUiText.Get(value.Label) + ": " + value.Value);
        }
    }

    private static void DrawReaderHeading(string label, string accent)
    {
        if (accent.Length != 0) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertU32ToFloat4(RichTextView.Color(accent)));
        try { Wrapped(label); }
        finally { if (accent.Length != 0) ImGui.PopStyleColor(); }
    }

    private static void DrawReaderOoc(ProfileReadSnapshot view)
    {
        if (!string.IsNullOrWhiteSpace(view.OocInformation)) Wrapped(view.OocInformation);
        var preferences = view.Preferences;
        if (preferences.Experience != RpExperience.Unspecified)
            Wrapped(ProfileUiText.Get("Erfahrung: ") + Experience(preferences.Experience));
        if (!string.IsNullOrWhiteSpace(preferences.PreferredStyle)) Wrapped(preferences.PreferredStyle);
        foreach (var pair in PreferenceValues(preferences).Where(pair => pair.Value != PreferenceAnswer.Unspecified))
            Wrapped(pair.Label + ": " + Answer(pair.Value));
    }

    private static IReadOnlyList<ProfileField> ReadCharacterFields(ProfileDocument profile)
    {
        var values = new List<ProfileField>();
        void Add(string key, string value) { if (!string.IsNullOrWhiteSpace(value)) values.Add(new(key, value)); }
        Add("FirstName", profile.FirstName); Add("LastName", profile.LastName);
        Add("Race", profile.Race); Add("Clan", profile.Clan); Add("Age", profile.Age);
        Add("Gender", profile.Gender); Add("Origin", profile.Origin); Add("Occupation", profile.Occupation);
        Add("EyeColor", profile.EyeColor); Add("Height", profile.Height); Add("Weight", profile.Weight);
        Add("Birthplace", profile.Birthplace); Add("Residence", profile.Residence); Add("FreeCompany", profile.FreeCompany);
        return values.AsReadOnly();
    }

    private sealed record ReaderContentOwner(Guid ProfileId, long Revision, string Hash, long Generation, int PolicyVersion);

    private sealed class ReaderContext(ITextureProvider textures, string windowSuffix) : IDisposable
    {
        public Func<ProfileDocument?>? Provider;
        public ProfileReader? Reader;
        public string? Hash;
        public Guid? ProfileId;
        public ProfileArea Area = ProfileArea.Overview;
        public bool StandardPresentation;
        public long Generation;
        public int LastDrawFrame = -1;
        public ReaderContentOwner? GlanceOwner;
        public int GlanceIndex = -1;
        public bool GlancePinned;
        public bool GlanceHovered;
        public bool ResetGlanceScroll;
        public long GlanceUntil;
        public Vector2 GlanceAnchor;
        public string WindowSuffix { get; } = windowSuffix;
        public IReadOnlyList<AdditionalContentEntry> Additional = [];
        public IReadOnlyList<ProfileField> CharacterFields = [];
        public PortraitTexture Portrait { get; } = new(textures);

        public void ClearContent()
        {
            Generation++;
            ClearGlance();
            Reader = null; Hash = null; ProfileId = null;
            Additional = []; CharacterFields = [];
            Portrait.Clear();
        }

        public void ClearGlance()
        {
            GlanceOwner = null; GlanceIndex = -1;
            GlancePinned = GlanceHovered = false;
            ResetGlanceScroll = true;
        }

        public void Clear()
        {
            ClearContent();
            Provider = null;
            Area = ProfileArea.Overview;
        }

        public void Dispose() { Clear(); Portrait.Dispose(); }
    }
}
