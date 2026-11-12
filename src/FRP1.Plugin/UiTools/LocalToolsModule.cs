using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Automation;
using FRP1.Plugin.Localization;
using FRP1.Plugin.RpBar;
using FRP1.Plugin.Utility;

namespace FRP1.Plugin.UiTools;

public sealed class LocalToolsModule : IDisposable
{
    private readonly OwnProfileSession own;
    private readonly Func<ProfileReference, bool>? openExistingProfile;
    private readonly AudiencePolicy? audience;
    private readonly LanguageSession language;
    private readonly LocalPreferences<AutomationPreferences> preferences;
    private readonly AutomationEngine engine = new();
    private readonly ToolsWindow window;
    private AutomationContext context = new();
    private IReadOnlyList<ProfileDocument> profileChoices = [];
    private long ownVersion = -1;
    private long preferencesVersion = -1;
    private Task? automationTask;
    private CancellationTokenSource? actionCancellation;
    private long actionGeneration;
    private bool disposed;
    private string? feedbackKey;
    private string referenceText = "";
    private string draftName = "";
    private string draftText = "";
    private RpTextTemplate draftTemplate;
    private AutomationRule ruleDraft = new();
    private bool editingRule;

    public LocalToolsModule(OwnProfileSession ownProfiles, string configurationDirectory, Func<ProfileReference, bool>? openExistingProfile = null,
        AudiencePolicy? audiencePolicy = null)
    {
        own = ownProfiles;
        this.openExistingProfile = openExistingProfile;
        audience = audiencePolicy;
        language = new LanguageSession(new LanguageSettingsStore(configurationDirectory));
        var settingsStore = new AutomationSettingsStore(configurationDirectory);
        preferences = new(new AutomationPreferences(), settingsStore.LoadAsync, settingsStore.SaveAsync, AutomationPreferences.Snapshot,
            (first, second) => first.Enabled == second.Enabled && first.HideOverlaysInCombat == second.HideOverlaysInCombat &&
                first.HideOverlaysInDuty == second.HideOverlaysInDuty && first.Rules.SequenceEqual(second.Rules));
        window = new ToolsWindow(this);
        Windows = Array.AsReadOnly<Window>([window]);
    }

    public IReadOnlyList<Window> Windows { get; }
    public UiLanguage Language => language.Language;
    public AutomationPreferences Preferences => AutomationPreferences.Snapshot(preferences.Value);
    public bool HasAccess => !disposed && context.AccessReady && context.LoggedIn && context.ContentId != 0 && own.AccessReady && own.HasCharacter;
    public bool OverlaysSuppressed => HasAccess && engine.OverlaysSuppressed;
    public string? Feedback => feedbackKey is { } key ? T(key) : null;
    public Task DisposalCompletion { get; private set; } = Task.CompletedTask;

    public void SetLanguage(UiLanguage value) => language.SetLanguage(value);

    public void SetPreferences(AutomationPreferences value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        preferences.Set(value);
        ApplyPreferences();
    }

    public void SetContext(AutomationContext value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(value);
        if (context == value) return;
        context = value;
        if (preferences.Loaded) engine.ObserveContext(value);
        CancelPendingAction();
        if (!HasAccess)
        {
            window.IsOpen = false;
            profileChoices = [];
            ownVersion = -1;
            referenceText = draftName = draftText = "";
            editingRule = false;
            feedbackKey = null;
        }
    }

    // Host calls this every UI tick, including when every window is closed.
    public void Tick()
    {
        if (disposed) return;
        language.Poll();
        preferences.Poll();
        ApplyPreferences();
        if (preferences.Loaded)
        {
            var before = engine.Generation;
            engine.ObserveContext(HasAccess ? context : context with { AccessReady = false });
            if (before != engine.Generation) CancelPendingAction();
        }
        if (automationTask is { IsCompleted: true } completed)
        {
            automationTask = null;
            try
            {
                completed.GetAwaiter().GetResult();
                if (actionGeneration == engine.Generation && HasAccess) feedbackKey = "Automation.Complete";
            }
            catch (Exception ex) when (LocalPreferences<int>.Expected(ex))
            { if (actionGeneration == engine.Generation && HasAccess) feedbackKey = "Automation.Blocked"; }
            finally { actionCancellation?.Dispose(); actionCancellation = null; }
        }
        if (!HasAccess)
        {
            window.IsOpen = false;
            CancelPendingAction();
            profileChoices = [];
            ownVersion = -1;
            referenceText = draftName = draftText = "";
            return;
        }
        if (ownVersion != own.Version) { ownVersion = own.Version; profileChoices = own.Profiles; }
        if (automationTask is not null || own.IsBusy || own.IsLoading || !preferences.Loaded) return;
        if (!engine.TryDequeue(out var intent) || intent is null || !engine.IsCurrent(intent)) return;
        actionCancellation = new CancellationTokenSource();
        actionGeneration = engine.Generation;
        try
        {
            // All mutations go through the same writer and dirty/busy guards as manual actions.
            automationTask = intent.Rule.Action switch
            {
                AutomationActionKind.SetStatus => own.UpdateStatusAsync(intent.Rule.Status, actionCancellation.Token),
                AutomationActionKind.SetAvailability => own.UpdateAvailabilityAsync(intent.Rule.Availability, actionCancellation.Token),
                AutomationActionKind.SelectProfile => own.SelectActiveAsync(intent.Rule.ProfileId!.Value, actionCancellation.Token),
                _ => throw new InvalidOperationException("Unsupported local action."),
            };
        }
        catch (Exception ex) when (LocalPreferences<int>.Expected(ex))
        { feedbackKey = "Automation.Blocked"; actionCancellation.Dispose(); actionCancellation = null; }
    }

    public void ShowTools()
    {
        if (HasAccess) window.IsOpen = true;
        else feedbackKey = "Safety.Required";
    }

    public void DrawSettings()
    {
        DrawLanguageSettings();
        if (ImGui.CollapsingHeader(T("Automation.Title"))) DrawAutomationSettings();
        if (ImGui.Button(T("Tools.Title"))) ShowTools();
        DrawFeedback();
    }

    public void DrawLanguageSettings()
    {
        ImGui.TextUnformatted(T("Language.Title"));
        ItemHelp(T("Language.Help"));
        if (ImGui.RadioButton("Deutsch##LocalLanguage", Language == UiLanguage.German)) SetLanguage(UiLanguage.German);
        ImGui.SameLine();
        if (ImGui.RadioButton("English##LocalLanguage", Language == UiLanguage.English)) SetLanguage(UiLanguage.English);
        if (language.ErrorMessage is { } languageError)
        {
            ImGui.TextWrapped(languageError);
            if (ImGui.Button(T("Common.Retry") + "##Language")) language.Retry();
        }
    }

    private void DrawAutomationSettings()
    {
        if (!HasAccess) { ImGui.TextWrapped(T("Safety.Required")); return; }
        var current = preferences.Value;
        var enabled = current.Enabled;
        if (ImGui.Checkbox(T("Automation.Enable"), ref enabled)) SetPreferences(current with { Enabled = enabled });
        ItemHelp(T("Automation.Help"));
        var combat = preferences.Value.HideOverlaysInCombat;
        if (ImGui.Checkbox(T("Automation.HideCombat"), ref combat)) SetPreferences(preferences.Value with { HideOverlaysInCombat = combat });
        var duty = preferences.Value.HideOverlaysInDuty;
        if (ImGui.Checkbox(T("Automation.HideDuty"), ref duty)) SetPreferences(preferences.Value with { HideOverlaysInDuty = duty });
        ItemHelp(T("Automation.HideHelp"));
        if (preferences.ErrorKey is { } key)
        {
            ImGui.TextWrapped(T(key));
            if (ImGui.Button(T("Common.Retry") + "##Automation")) preferences.Retry();
        }
        DrawRules();
    }

    public void DrawToolsPanel()
    {
        if (!HasAccess) { ImGui.TextWrapped(T("Safety.Required")); return; }
        if (!ImGui.BeginTabBar("##local-tool-areas")) return;
        try
        {
            if (ImGui.BeginTabItem(Language == UiLanguage.English ? "Draft" : "Entwurf"))
            {
                try { DrawTextDraft(); }
                finally { ImGui.EndTabItem(); }
            }
            if (ImGui.BeginTabItem(Language == UiLanguage.English ? "Profile link" : "Profilverweis"))
            {
                try { DrawProfileReference(); }
                finally { ImGui.EndTabItem(); }
            }
            if (ImGui.BeginTabItem(Language == UiLanguage.English ? "Automation" : "Automationen"))
            {
                try { DrawAutomationSettings(); }
                finally { ImGui.EndTabItem(); }
            }
        }
        finally { ImGui.EndTabBar(); }
        DrawFeedback();
    }

    private void DrawProfileReference()
    {
        ImGui.InputText(T("Reference.Input"), ref referenceText, 2048);
        ItemHelp(T("Reference.Help"));
        if (ImGui.Button(T("Common.Open") + "##Reference"))
        {
            if (!ProfileReference.TryParse(referenceText, out var reference)) feedbackKey = "Reference.Invalid";
            else
            {
                try { if (openExistingProfile?.Invoke(reference!) != true) feedbackKey = "Reference.Missing"; else feedbackKey = null; }
                catch (Exception ex) when (LocalPreferences<int>.Expected(ex)) { feedbackKey = "Reference.Missing"; }
            }
        }
        ImGui.SameLine();
        if (ImGui.Button(T("Common.Copy") + "##Reference"))
        {
            if (ProfileReference.TryParse(referenceText, out var reference))
            { ImGui.SetClipboardText(reference!.Format()); feedbackKey = "Reference.Copied"; }
            else feedbackKey = "Reference.Invalid";
        }
    }

    private void DrawTextDraft()
    {
        ImGui.InputText(T("Common.Name") + "##Draft", ref draftName, 321);
        ImGui.InputTextMultiline(T("Draft.Text"), ref draftText, 8193, new Vector2(-1, 140));
        ItemHelp(T("Draft.Help"));
        if (ImGui.RadioButton(T("Draft.Speech"), draftTemplate == RpTextTemplate.Speech)) draftTemplate = RpTextTemplate.Speech;
        ImGui.SameLine();
        if (ImGui.RadioButton(T("Draft.Emote"), draftTemplate == RpTextTemplate.Emote)) draftTemplate = RpTextTemplate.Emote;
        try
        {
            var preview = new RpTextDraft(draftName, draftText, draftTemplate).Preview();
            if (ImGui.CollapsingHeader(T("Common.Preview"))) ImGui.TextWrapped(preview);
            if (ImGui.Button(T("Common.Copy") + "##Draft")) { ImGui.SetClipboardText(preview); feedbackKey = "Draft.Copied"; }
        }
        catch (InvalidDataException) { ImGui.TextWrapped(T("Draft.Invalid")); }
    }

    private void DrawRules()
    {
        ImGui.TextUnformatted(T("Automation.RuleLimit"));
        ItemHelp(T("Automation.Help"));
        foreach (var rule in preferences.Value.Rules.ToArray())
        {
            ImGui.PushID(rule.Id.ToString("N"));
            try
            {
                if (ImGui.SmallButton(T("Common.Edit"))) { ruleDraft = rule; editingRule = true; }
                ImGui.SameLine();
                if (ImGui.SmallButton(T("Common.Remove")))
                    SetPreferences(preferences.Value with { Rules = Array.AsReadOnly(preferences.Value.Rules.Where(item => item.Id != rule.Id).ToArray()) });
                ImGui.SameLine(); ImGui.TextUnformatted(rule.Name + " · " + T("Trigger." + rule.Trigger));
            }
            finally { ImGui.PopID(); }
        }
        if (preferences.Value.Rules.Count < 16 && ImGui.Button(T("Automation.NewRule")))
        { ruleDraft = new AutomationRule { Name = T("Automation.NewRule") }; editingRule = true; }
        if (!editingRule) return;
        var name = ruleDraft.Name;
        if (ImGui.InputText(T("Automation.RuleName"), ref name, 321)) ruleDraft = ruleDraft with { Name = name };
        var enabled = ruleDraft.Enabled;
        if (ImGui.Checkbox(T("Common.Enabled") + "##Rule", ref enabled)) ruleDraft = ruleDraft with { Enabled = enabled };
        if (ImGui.BeginCombo(T("Automation.Trigger"), T("Trigger." + ruleDraft.Trigger)))
        {
            foreach (var trigger in Enum.GetValues<AutomationTrigger>())
                if (ImGui.Selectable(T("Trigger." + trigger), ruleDraft.Trigger == trigger)) ruleDraft = ruleDraft with { Trigger = trigger };
            ImGui.EndCombo();
        }
        if (ImGui.BeginCombo(T("Automation.Action"), T("Action." + ruleDraft.Action)))
        {
            foreach (var action in Enum.GetValues<AutomationActionKind>())
                if (ImGui.Selectable(T("Action." + action), ruleDraft.Action == action)) ruleDraft = ruleDraft with { Action = action, ProfileId = null };
            ImGui.EndCombo();
        }
        if (ruleDraft.Action == AutomationActionKind.SetStatus && ImGui.BeginCombo(T("Status.Label"), T("Status." + ruleDraft.Status)))
        {
            foreach (var status in Enum.GetValues<RpStatus>())
                if (ImGui.Selectable(T("Status." + status), ruleDraft.Status == status)) ruleDraft = ruleDraft with { Status = status };
            ImGui.EndCombo();
        }
        if (ruleDraft.Action == AutomationActionKind.SetAvailability && ImGui.BeginCombo(T("Availability.Label"), T("Availability." + ruleDraft.Availability)))
        {
            foreach (var availability in Enum.GetValues<RpAvailability>())
                if (ImGui.Selectable(T("Availability." + availability), ruleDraft.Availability == availability)) ruleDraft = ruleDraft with { Availability = availability };
            ImGui.EndCombo();
        }
        if (ruleDraft.Action == AutomationActionKind.SelectProfile && ImGui.BeginCombo(T("Automation.ChooseProfile"),
                profileChoices.FirstOrDefault(profile => profile.Id == ruleDraft.ProfileId && AllowedProfile(profile))?.DisplayName ?? T("Profile.Select")))
        {
            foreach (var profile in profileChoices.Where(AllowedProfile))
                if (ImGui.Selectable(profile.DisplayName + "##" + profile.Id, ruleDraft.ProfileId == profile.Id)) ruleDraft = ruleDraft with { ProfileId = profile.Id };
            ImGui.EndCombo();
        }
        var filter = ruleDraft.TerritoryId.HasValue;
        if (ImGui.Checkbox(T("Automation.TerritoryFilter"), ref filter)) ruleDraft = ruleDraft with { TerritoryId = filter ? context.TerritoryId : null };
        if (filter)
        {
            var territory = (int)Math.Min(ruleDraft.TerritoryId ?? 0, int.MaxValue);
            if (ImGui.InputInt(T("Automation.Territory"), ref territory)) ruleDraft = ruleDraft with { TerritoryId = territory > 0 ? (uint)territory : 0 };
        }
        if (ImGui.Button(T("Common.Apply") + "##Rule"))
        {
            try
            {
                SetPreferences(preferences.Value with
                {
                    Rules = Array.AsReadOnly(preferences.Value.Rules.Where(rule => rule.Id != ruleDraft.Id).Append(ruleDraft).ToArray()),
                });
                editingRule = false;
                feedbackKey = null;
            }
            catch (Exception ex) when (LocalPreferences<int>.Expected(ex)) { feedbackKey = "Automation.Invalid"; }
        }
        ImGui.SameLine();
        if (ImGui.Button(T("Common.Cancel") + "##Rule")) editingRule = false;
    }

    private void ApplyPreferences()
    {
        if (preferencesVersion == preferences.Version) return;
        preferencesVersion = preferences.Version;
        engine.SetPreferences(preferences.Value);
        CancelPendingAction();
    }

    private void CancelPendingAction() => actionCancellation?.Cancel();
    private bool AllowedProfile(ProfileDocument profile) => own.AccessReady &&
        (profile.ContentRating == ContentRating.General || audience?.AcceptsMatureContent == true);
    private string T(string key) => UiText.Get(key, Language);
    private void DrawFeedback() { if (Feedback is { } message) ImGui.TextWrapped(message); }
    private static void ItemHelp(string text)
    {
        if (!ImGui.IsItemHovered() && !ImGui.IsItemFocused()) return;
        ImGui.BeginTooltip();
        try { ImGui.PushTextWrapPos(ImGui.GetFontSize() * 26); ImGui.TextUnformatted(text); ImGui.PopTextWrapPos(); }
        finally { ImGui.EndTooltip(); }
    }


    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        window.IsOpen = false;
        CancelPendingAction();
        preferences.Dispose();
        language.Dispose();
        referenceText = draftName = draftText = "";
        DisposalCompletion = ObserveDisposalAsync();
    }

    private async Task ObserveDisposalAsync()
    {
        try
        {
            await Task.WhenAll(automationTask ?? Task.CompletedTask, preferences.DisposalCompletion, language.DisposalCompletion).ConfigureAwait(false);
        }
        catch (Exception ex) when (LocalPreferences<int>.Expected(ex)) { /* Observe cancellation without logging draft text. */ }
        finally { actionCancellation?.Dispose(); }
    }

    private sealed class ToolsWindow(LocalToolsModule owner) : Window("FRP1 · RP-Werkzeuge / RP tools##FRP1.LocalTools")
    {
        public override void Draw() => owner.DrawToolsPanel();
    }
}
