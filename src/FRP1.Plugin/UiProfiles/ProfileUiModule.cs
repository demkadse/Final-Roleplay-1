using System.Numerics;
using System.Security.Cryptography;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Presentation;
using FRP1.Plugin.Profiles;
using Lumina.Excel.Sheets;

namespace FRP1.Plugin.UiProfiles;

public sealed partial class ProfileUiModule : IDisposable
{
    private readonly AudiencePolicy policy;
    private readonly ITextureProvider textures;
    private readonly JournalWindow journal;
    private readonly PickerWindow picker;
    private readonly PortraitTexture journalPortrait;
    private readonly PortraitTexture editorPortrait;
    private readonly FileDialogManager dialogs = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task<IconOption[]> iconLoading;
    private IReadOnlyList<IconOption> icons = [];
    private Func<ProfileDocument?>? provider;
    private ProfileReader? reader;
    private string? readerHash;
    private ProfileArea journalArea = ProfileArea.StatusSummary;
    private IReadOnlyList<AdditionalContentEntry> journalAdditional = [];
    private IReadOnlyList<ProfileField> journalCharacterFields = [];
    private string journalFreeCompany = "";
    private Action<uint>? iconCallback;
    private long iconGeneration;
    private ContentRating iconRating;
    private long editorGeneration;
    private int policyVersion;
    private int observedPolicyVersion;
    private bool access;
    private bool disposed;
    private bool standardPresentation;
    private string search = "";
    private string category = "Alle";
    private int page;
    private string? feedback;

    public ProfileUiModule(AudiencePolicy policy, ITextureProvider textures, IDataManager dataManager)
    {
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        ArgumentNullException.ThrowIfNull(dataManager);
        journalPortrait = new PortraitTexture(textures);
        editorPortrait = new PortraitTexture(textures);
        journal = new JournalWindow(this);
        picker = new PickerWindow(this);
        Windows = Array.AsReadOnly<Window>([journal, picker]);
        iconLoading = Task.Run(() => ReadIcons(dataManager), lifetime.Token);
        policy.Changed += PolicyChanged;
    }

    public IReadOnlyList<Window> Windows { get; }

    public void SetAccess(bool enabled)
    {
        if (disposed) return;
        if (access == enabled) return;
        access = enabled;
        if (!enabled)
        {
            journal.IsOpen = false;
            reader = null; readerHash = null; provider = null;
            ClearJournalMetadata();
            journalPortrait.Clear();
            ResetEditorContext();
        }
    }

    public void OpenOwn(Func<ProfileDocument?> liveProfile)
    {
        ArgumentNullException.ThrowIfNull(liveProfile);
        if (!HasAccess) return;
        provider = liveProfile;
        reader = null; readerHash = null;
        journalArea = ProfileArea.StatusSummary;
        ClearJournalMetadata();
        journalPortrait.Clear();
        journal.IsOpen = GetSnapshot() is not null;
    }

    public void OpenForeign(Func<ResolvedProfile?> liveProfile)
    {
        ArgumentNullException.ThrowIfNull(liveProfile);
        OpenOwn(() => liveProfile()?.Profile);
    }

    public void OpenIconPicker(uint current, Action<uint> selected, ContentRating? contentRating = null)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (!HasAccess) return;
        iconGeneration = editorGeneration;
        iconRating = contentRating ?? editorRating;
        try { policy.EnsureAllowed(new ProfileDocument { ContentRating = iconRating }); }
        catch (InvalidDataException) { return; }
        iconCallback = selected;
        picker.IsOpen = true;
        page = 0;
    }

    public void Poll()
    {
        if (disposed) return;
        var currentPolicy = Volatile.Read(ref policyVersion);
        if (observedPolicyVersion != currentPolicy)
        {
            observedPolicyVersion = currentPolicy;
            journal.IsOpen = false;
            reader = null; readerHash = null;
            ClearJournalMetadata();
            journalPortrait.Clear();
            ResetEditorContext();
        }
        if (iconLoading.IsCompleted && icons.Count == 0)
        {
            try { icons = Array.AsReadOnly(iconLoading.GetAwaiter().GetResult()); }
            catch (Exception ex) when (Expected(ex)) { feedback = "Die lokale Symbolauswahl konnte nicht geladen werden."; }
        }
        if (!HasAccess)
        {
            journalPortrait.Clear(); editorPortrait.Clear();
            journal.IsOpen = picker.IsOpen = false;
        }
        else if (journal.IsOpen) _ = GetSnapshot();
        PollPortraitImport();
        journalPortrait.Poll(); editorPortrait.Poll();
    }

    public void DrawOverlays()
    { if (HasAccess) dialogs.Draw(); }

    private bool HasAccess => !disposed && access && policy.Snapshot.Selection != AudienceSelection.Unspecified;
    private void PolicyChanged(object? sender, EventArgs args) => Interlocked.Increment(ref policyVersion);

    private ProfileReadSnapshot? GetSnapshot()
    {
        if (!HasAccess || provider is null) { journalPortrait.Clear(); ClearJournalMetadata(); return null; }
        try
        {
            var profile = provider();
            if (profile is null) { journalPortrait.Clear(); reader = null; readerHash = null; ClearJournalMetadata(); return null; }
            policy.EnsureAllowed(profile);
            var hash = ProfileJson.Hash(profile);
            if (reader is null || readerHash != hash)
            {
                reader = new ProfileReader(profile, policy, standardPresentation);
                journalAdditional = ExistingAdditionalContent.Read(profile);
                journalCharacterFields = ReadCharacterFields(profile);
                journalFreeCompany = profile.FreeCompany;
                readerHash = hash;
            }
            if (!reader.TryRead(out var snapshot)) { journalPortrait.Clear(); ClearJournalMetadata(); return null; }
            journalPortrait.Set(snapshot!.Portrait);
            return snapshot;
        }
        catch (Exception ex) when (Expected(ex))
        { journalPortrait.Clear(); reader = null; readerHash = null; ClearJournalMetadata(); return null; }
    }

    private void DrawJournal()
    {
        var view = GetSnapshot();
        if (view is null) { Wrapped(ProfileUiText.Get("Dieses Profil ist derzeit nicht verfügbar.")); return; }
        if (ImGui.Checkbox(ProfileUiText.Id("Standarddarstellung verwenden"), ref standardPresentation))
        { reader = null; readerHash = null; view = GetSnapshot(); if (view is null) return; }
        if (ImGui.BeginTable("ProfileJournalHeader", 2, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Media", ImGuiTableColumnFlags.WidthFixed, 72);
            ImGui.TableSetupColumn("Identity", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            if (journalPortrait.Current is { } portrait) ImGui.Image(portrait.Handle, new Vector2(64));
            DrawIcon(view.Appearance.IconId, new Vector2(28));
            ImGui.TableNextColumn();
            var colored = view.Appearance.NameColor.Length != 0;
            if (colored) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertU32ToFloat4(RichTextView.Color(view.Appearance.NameColor)));
            try { Wrapped(view.DisplayName); }
            finally { if (colored) ImGui.PopStyleColor(); }
            if (view.Title.Length != 0) Wrapped(view.Title);
            Wrapped(ProfileUiText.Get(view.ContentRating == ContentRating.Mature18 ? "Gesamtes Profil: ab 18" : "Gesamtes Profil: allgemein / jugendfrei"));
            ImGui.EndTable();
        }
        if (journalAdditional.Count != 0)
            Wrapped(ProfileUiText.Get("Vorhandene zusätzliche Inhalte: ") + journalAdditional.Count +
                ProfileUiText.Get(" – unter Weitere Charakterdaten vollständig lesbar."));
        ProfileNavigation.Draw(ref journalArea);
        var help = journalArea switch
        {
            ProfileArea.StatusSummary => "Status",
            ProfileArea.OocInformation => "OocInformation",
            ProfileArea.Glances => "Glances",
            ProfileArea.FurtherCharacterData => "FurtherCharacterData",
            _ => "RpExperience",
        };
        DrawHelpHeading(ProfileAreas.Title(journalArea), help);
        switch (journalArea)
        {
            case ProfileArea.StatusSummary:
                Wrapped(Status(view.Status) + " · " + Availability(view.Availability));
                if (view.Pronouns.Length != 0) Wrapped(view.Pronouns);
                if (view.CurrentLocation.Length != 0) Wrapped(ProfileUiText.Get("Aktueller Ort") + ": " + view.CurrentLocation);
                if (view.Summary.Length != 0) Wrapped(view.Summary);
                if (journalFreeCompany.Length != 0) Wrapped(ProfileUiText.Get("Freie Gesellschaft") + ": " + journalFreeCompany);
                break;
            case ProfileArea.OocInformation:
                Wrapped(view.OocInformation.Length != 0 ? view.OocInformation : ProfileUiText.Get("Keine OOC-Informationen."));
                break;
            case ProfileArea.Glances:
                foreach (var glance in view.Glances)
                { DrawIcon(glance.IconId, new Vector2(28)); Wrapped(glance.Label); Wrapped(glance.Text); ImGui.Separator(); }
                if (view.Glances.Count == 0) Wrapped(ProfileUiText.Get("Keine aktiven Einträge."));
                break;
            case ProfileArea.FurtherCharacterData:
                DrawJournalCharacterData(view);
                break;
            case ProfileArea.RpExperience:
                Wrapped(ProfileUiText.Get("Erfahrung: ") + Experience(view.Preferences.Experience));
                if (view.Preferences.PreferredStyle.Length != 0) Wrapped(view.Preferences.PreferredStyle);
                Wrapped(ProfileUiText.Get("Die folgenden Angaben sind Gesprächshinweise und ersetzen keine Absprache oder Einwilligung."));
                foreach (var pair in PreferenceValues(view.Preferences)) Wrapped(pair.Label + ": " + Answer(pair.Value));
                break;
        }
    }

    private void DrawJournalCharacterData(ProfileReadSnapshot view)
    {
        foreach (var field in journalCharacterFields)
        { Wrapped(Label(field.Key)); Wrapped(field.Value); ImGui.Spacing(); }
        if (view.Languages.Count != 0) Wrapped(ProfileUiText.Get("RP-Sprachen: ") + string.Join(", ", view.Languages));
        foreach (var section in view.Sections)
        {
            if (view.Appearance.Layout != AboutLayout.Compact) ImGui.Separator();
            Wrapped(Label(section.Key));
            RichTextView.Draw(section.Blocks);
        }
        if (journalAdditional.Count == 0) return;
        ImGui.Separator();
        DrawHelpHeading(ProfileUiText.Get("Vorhandene zusätzliche Inhalte") + " (" + journalAdditional.Count + ")", "ExistingAdditionalContent");
        foreach (var entry in journalAdditional)
        {
            Wrapped(entry.Title);
            DrawIcon(entry.IconId, new Vector2(28));
            foreach (var value in entry.Values) Wrapped(ProfileUiText.Get(value.Label) + ": " + value.Value);
            ImGui.Separator();
        }
    }

    private static IReadOnlyList<ProfileField> ReadCharacterFields(ProfileDocument profile)
    {
        var values = new List<ProfileField>();
        void Add(string key, string value) { if (value.Length != 0) values.Add(new(key, value)); }
        Add("FirstName", profile.FirstName); Add("LastName", profile.LastName);
        Add("Race", profile.Race); Add("Clan", profile.Clan); Add("Age", profile.Age);
        Add("Gender", profile.Gender); Add("Origin", profile.Origin); Add("Occupation", profile.Occupation);
        Add("EyeColor", profile.EyeColor); Add("Height", profile.Height); Add("Weight", profile.Weight);
        Add("Birthplace", profile.Birthplace); Add("Residence", profile.Residence);
        return values.AsReadOnly();
    }

    private void ClearJournalMetadata()
    {
        journalAdditional = [];
        journalCharacterFields = [];
        journalFreeCompany = "";
    }
    private void DrawPicker()
    {
        if (!HasAccess) return;
        if (ImGui.InputText(ProfileUiText.Id("Symbole suchen"), ref search, 160)) page = 0;
        if (ImGui.BeginCombo(ProfileUiText.Id("Kategorie"), ProfileUiText.Get(category)))
        {
            foreach (var value in new[] { "Alle", "Kampf und Magie", "Handwerk", "Sammeln" })
                if (ImGui.Selectable(ProfileUiText.Id(value), value == category)) { category = value; page = 0; }
            ImGui.EndCombo();
        }
        if (ImGui.Button(ProfileUiText.Id("Kein Symbol"))) SelectIcon(0);
        var filtered = icons.Where(icon => (category == "Alle" || icon.Category == category) &&
            icon.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        var pages = Math.Max(1, (filtered.Length + 23) / 24);
        page = Math.Clamp(page, 0, pages - 1);
        var columns = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / 110));
        var index = 0;
        foreach (var icon in filtered.Skip(page * 24).Take(24))
        {
            ImGui.PushID((int)icon.Id);
            ImGui.BeginGroup();
            var texture = textures.GetFromGameIcon(icon.Id).GetWrapOrDefault();
            var chosen = texture is null ? ImGui.Button(ProfileUiText.Id("Auswählen##icon"), new Vector2(64, 40)) :
                ImGui.ImageButton(texture.Handle, new Vector2(40));
            if (chosen) SelectIcon(icon.Id);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 100);
            ImGui.TextUnformatted(icon.Name);
            ImGui.PopTextWrapPos();
            ImGui.EndGroup();
            ImGui.PopID();
            if (++index % columns != 0) ImGui.SameLine();
        }
        ImGui.NewLine();
        ImGui.BeginDisabled(page == 0); if (ImGui.Button(ProfileUiText.Id("Zurück"))) page--; ImGui.EndDisabled();
        ImGui.SameLine(); Wrapped($"{page + 1} / {pages}"); ImGui.SameLine();
        ImGui.BeginDisabled(page + 1 >= pages); if (ImGui.Button(ProfileUiText.Id("Weiter"))) page++; ImGui.EndDisabled();
        if (feedback is not null) Wrapped(ProfileUiText.Get(feedback));
    }

    private void SelectIcon(uint id)
    {
        var callback = iconCallback;
        iconCallback = null;
        picker.IsOpen = false;
        if (!HasAccess || iconGeneration != editorGeneration) return;
        try { policy.EnsureAllowed(new ProfileDocument { ContentRating = iconRating }); }
        catch (InvalidDataException) { return; }
        callback?.Invoke(id);
    }

    private void DrawIcon(uint id, Vector2 size)
    {
        if (id == 0) return;
        var texture = textures.GetFromGameIcon(id).GetWrapOrDefault();
        if (texture is not null) ImGui.Image(texture.Handle, size);
    }

    private static IconOption[] ReadIcons(IDataManager data)
    {
        var result = new List<IconOption>();
        foreach (var row in data.GetExcelSheet<ClassJob>())
        {
            if (row.RowId is 0 or > 64) continue;
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var group = row.RowId is >= 8 and <= 15 ? "Handwerk" : row.RowId is >= 16 and <= 18 ? "Sammeln" : "Kampf und Magie";
            result.Add(new(62100 + row.RowId, name, group));
            if (result.Count == 64) break;
        }
        return result.OrderBy(icon => icon.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void Wrapped(string text)
    { ImGui.PushTextWrapPos(0); ImGui.TextUnformatted(text); ImGui.PopTextWrapPos(); }

    private static bool Expected(Exception ex) => ex is IOException or InvalidDataException or ArgumentException or
        InvalidOperationException or OperationCanceledException or UnauthorizedAccessException or NotSupportedException or
        System.Runtime.InteropServices.ExternalException;

    public void Dispose()
    {
        if (disposed) return;
        SetAccess(false);
        disposed = true;
        policy.Changed -= PolicyChanged;
        lifetime.Cancel();
        journalPortrait.Dispose(); editorPortrait.Dispose();
        if (importing is not null) _ = importing.ContinueWith(task => { _ = task.Exception; }, TaskScheduler.Default);
        _ = iconLoading.ContinueWith(task => { _ = task.Exception; lifetime.Dispose(); }, TaskScheduler.Default);
    }

    private sealed record IconOption(uint Id, string Name, string Category);
    private sealed class JournalWindow : Window
    {
        private readonly ProfileUiModule owner;
        public JournalWindow(ProfileUiModule owner) : base(ProfileUiText.Id("Charakterprofil##FRP1Journal"))
        { this.owner = owner; Size = new Vector2(740, 650); SizeCondition = ImGuiCond.FirstUseEver; }
        public override void PreDraw() => WindowName = ProfileUiText.Id("Charakterprofil##FRP1Journal");
        public override bool DrawConditions() => owner.HasAccess && owner.GetSnapshot() is not null;
        public override void Draw() => owner.DrawJournal();
    }
    private sealed class PickerWindow : Window
    {
        private readonly ProfileUiModule owner;
        public PickerWindow(ProfileUiModule owner) : base(ProfileUiText.Id("Symbol auswählen##FRP1Icons"))
        { this.owner = owner; Size = new Vector2(640, 530); SizeCondition = ImGuiCond.FirstUseEver; }
        public override void PreDraw() => WindowName = ProfileUiText.Id("Symbol auswählen##FRP1Icons");
        public override bool DrawConditions() => owner.HasAccess;
        public override void Draw() => owner.DrawPicker();
    }
}
