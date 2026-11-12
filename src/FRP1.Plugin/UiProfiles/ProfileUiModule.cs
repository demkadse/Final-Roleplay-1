using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Presentation;
using Lumina.Excel.Sheets;

namespace FRP1.Plugin.UiProfiles;

public sealed partial class ProfileUiModule : IDisposable
{
    private readonly AudiencePolicy policy;
    private readonly ITextureProvider textures;
    private readonly JournalWindow journal;
    private readonly PickerWindow picker;
    private readonly ReaderContext journalReader;
    private readonly ReaderContext ownReader;
    private readonly PortraitTexture editorPortrait;
    private readonly FileDialogManager dialogs = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task<IconOption[]> iconLoading;
    private IReadOnlyList<IconOption> icons = [];
    private Action<uint>? iconCallback;
    private long iconGeneration;
    private ContentRating iconRating;
    private long editorGeneration;
    private int policyVersion;
    private int observedPolicyVersion;
    private bool access;
    private bool disposed;
    private string search = "";
    private string category = "Alle";
    private int page;
    private string? feedback;

    public ProfileUiModule(AudiencePolicy policy, ITextureProvider textures, IDataManager dataManager)
    {
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        ArgumentNullException.ThrowIfNull(dataManager);
        journalReader = new ReaderContext(textures, "Journal");
        ownReader = new ReaderContext(textures, "Own");
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
            journalReader.Clear();
            ownReader.Clear();
            ResetEditorContext();
        }
    }

    public void OpenOwn(Func<ProfileDocument?> liveProfile)
    {
        ArgumentNullException.ThrowIfNull(liveProfile);
        if (!HasAccess) return;
        journalReader.Clear();
        journalReader.Provider = liveProfile;
        journal.IsOpen = GetSnapshot(journalReader) is not null;
    }

    public void OpenForeign(Func<ResolvedProfile?> liveProfile)
    {
        ArgumentNullException.ThrowIfNull(liveProfile);
        if (!HasAccess) return;
        journalReader.Clear();
        journalReader.Provider = () => liveProfile()?.Profile;
        journal.IsOpen = GetSnapshot(journalReader) is not null;
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
            journalReader.Clear();
            ownReader.Clear();
            ResetEditorContext();
        }
        if (iconLoading.IsCompleted && icons.Count == 0)
        {
            try { icons = Array.AsReadOnly(iconLoading.GetAwaiter().GetResult()); }
            catch (Exception ex) when (Expected(ex)) { feedback = "Die lokale Symbolauswahl konnte nicht geladen werden."; }
        }
        if (!HasAccess)
        {
            journalReader.Clear(); ownReader.Clear(); editorPortrait.Clear();
            journal.IsOpen = picker.IsOpen = false;
        }
        else if (journal.IsOpen) _ = GetSnapshot(journalReader);
        PollPortraitImport();
        journalReader.Portrait.Poll(); ownReader.Portrait.Poll(); editorPortrait.Poll();
    }

    public void DrawOverlays()
    { if (HasAccess) dialogs.Draw(); }

    private bool HasAccess => !disposed && access && policy.Snapshot.Selection != AudienceSelection.Unspecified;
    private void PolicyChanged(object? sender, EventArgs args) => Interlocked.Increment(ref policyVersion);

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
        journalReader.Dispose(); ownReader.Dispose(); editorPortrait.Dispose();
        if (importing is not null) _ = importing.ContinueWith(task => { _ = task.Exception; }, TaskScheduler.Default);
        _ = iconLoading.ContinueWith(task => { _ = task.Exception; lifetime.Dispose(); }, TaskScheduler.Default);
    }

    private sealed record IconOption(uint Id, string Name, string Category);
    private sealed class JournalWindow : Window
    {
        private readonly ProfileUiModule owner;
        public JournalWindow(ProfileUiModule owner) : base(ProfileUiText.Id("Charakterprofil##FRP1CompactJournal"))
        { this.owner = owner; Size = new Vector2(480, 480); SizeCondition = ImGuiCond.FirstUseEver; }
        public override void PreDraw()
        {
            WindowName = ProfileUiText.Id("Charakterprofil##FRP1CompactJournal");
            var available = Vector2.Max(new Vector2(1), ImGui.GetMainViewport().WorkSize - new Vector2(16));
            ImGui.SetNextWindowSizeConstraints(Vector2.Min(new Vector2(300, 240), available), available);
        }
        public override bool DrawConditions() => owner.HasAccess && owner.GetSnapshot(owner.journalReader) is not null;
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
