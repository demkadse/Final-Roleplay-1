using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Theming;
using FRP1.Plugin.RpBar;
using FRP1.Plugin.UiProfiles;
using FRP1.Plugin.UiSocial;
using FRP1.Plugin.Profiles;
using FRP1.Plugin.Localization;
using Dalamud.Plugin.Services;
using FRP1.Plugin.UiTools;
using FRP1.Plugin.GameIntegration;

namespace FRP1.Plugin;

internal sealed class MainWindow : Window, IDisposable
{
    private static readonly (string Key, string Label, int Limit)[] ShortFields =
    [
        ("DisplayName", "RP-Name", 640), ("FirstName", "Vorname", 1024),
        ("LastName", "Nachname", 1024), ("Title", "Titel", 1024),
        ("Race", "Volk", 1024), ("Clan", "Clan", 1024), ("Age", "Alter", 1024),
        ("Gender", "Geschlecht", 1024), ("Origin", "Herkunft", 1024),
        ("Occupation", "Beruf", 1024),
    ];
    private static readonly (string Key, string Label)[] LongFields =
    [
        ("Appearance", "Aussehen"), ("Personality", "Persönlichkeit"),
        ("Background", "Hintergrundgeschichte"),
    ];

    private readonly ProfileStore store;
    private readonly AudienceSettings audienceSettings;
    private readonly UsageAcknowledgementStore usageAcknowledgements;
    private readonly ThemeSession themeSession;
    private readonly OwnProfileSession ownProfiles;
    private readonly ProfileUiModule profileUi;
    private readonly SocialUiModule socialUi;
    private readonly ITextureProvider textures;
    public Func<UiLanguage> LanguageProvider { get; set; } = () => UiLanguage.German;
    public string T(string key) => UiText.Get(key, LanguageProvider());
    private string L(string german) => UiText.Translate(german, LanguageProvider());
    public HelpHints Help { get; }
    public RpBarWindow? RpBarWindow { get; set; }
    public LocalToolsModule? LocalTools { get; set; }
    public GamePresentationModule? GamePresentation { get; set; }
    public Func<Guid, string?>? OwnProfileReferenceProvider { get; set; }
    private HelpTopicId? helpTopic;
    private long catalogVersion = -1;
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<ProfileDocument> profiles = [];
    private readonly Dictionary<string, string> fields = [];
    private readonly SortedDictionary<string, string> extraFields = new(StringComparer.Ordinal);
    private ProfileDocument original = new();
    private ProfileDetailsDraft details = new(new ProfileDocument());
    private ExtendedProfileDraft extended = ExtendedProfileDraft.From(new ProfileDocument());
    private GlanceTemplateSet? glanceTemplate;
    private long editorGeneration;
    private Task<ProfileDocument>? saving;
    private Task<AudiencePreferences>? preferencesOperation;
    private Task<bool>? noticesLoading;
    private Task? noticesSaving;
    private bool noticesLoaded;
    private bool noticesAcknowledged;
    private bool noticesCheckbox;
    private bool initialPreferencesLoaded;
    private int policyGeneration;
    private int saveGeneration;
    private ContentRating contentRating;
    private bool dirty;
    private bool disposed;
    private string message = "Lokale Einstellungen werden geladen …";
    private string jsonText = "";
    private string extraKey = "";
    private string extraValue = "";

    public MainWindow(ProfileStore store, AudienceSettings audienceSettings, UsageAcknowledgementStore usageAcknowledgements,
        ThemeSession themeSession, OwnProfileSession ownProfiles, ProfileUiModule profileUi,
        SocialUiModule socialUi, ITextureProvider textures)
        : base("Final Roleplay 1##FRP1")
    {
        this.store = store;
        this.audienceSettings = audienceSettings;
        this.usageAcknowledgements = usageAcknowledgements;
        this.themeSession = themeSession;
        this.ownProfiles = ownProfiles;
        this.profileUi = profileUi;
        this.socialUi = socialUi;
        this.textures = textures;
        Help = new HelpHints(() => !ownProfiles.SettingsLoaded || ownProfiles.Settings.ShowHints, OpenHelp);
        Size = new Vector2(1000, 700);
        SizeCondition = ImGuiCond.FirstUseEver;
        SetDraft(new ProfileDocument());
        var token = lifetime.Token;
        preferencesOperation = Task.Run(() => audienceSettings.LoadForSessionAsync(token), token);
        noticesLoading = Task.Run(() => usageAcknowledgements.IsAcknowledgedAsync(token), token);
    }

    private bool Busy => ownProfiles.IsLoading || ownProfiles.IsBusy || saving is not null || preferencesOperation is not null ||
        noticesLoading is not null || noticesSaving is not null;

    public bool ProfileAccessReady => noticesLoaded && noticesAcknowledged && initialPreferencesLoaded &&
        store.Policy.Snapshot.Selection != AudienceSelection.Unspecified;
    public bool EditorBlocked => dirty || Busy;
    public ProfileDocument? GetSavedOwnProfile(Guid id)
    {
        if (!ProfileAccessReady || !ownProfiles.AccessReady) return null;
        var profile = profiles.FirstOrDefault(profile => profile.Id == id);
        return profile is not null && CanDisplay(profile) ? profile : null;
    }

    public void OpenHelp(HelpTopicId topic)
    {
        helpTopic = topic;
        IsOpen = true;
    }

    public void OpenOwnProfile()
    {
        if (!ProfileAccessReady) { IsOpen = true; return; }
        if (ownProfiles.ActiveProfile is { } active && CanDisplay(active))
            profileUi.OpenOwn(() => ProfileAccessReady ? ownProfiles.ActiveProfile : null);
        else { IsOpen = true; message = "Bitte zuerst ein gespeichertes Profil ausdrücklich als aktives RP-Profil auswählen."; }
    }

    // Results are applied by the plugin's Draw callback, including while the window is closed.
    public void PollOperation()
    {
        if (disposed) return;
        if (noticesLoading is { IsCompleted: true } completedNoticesLoad)
        {
            noticesLoading = null;
            try { noticesAcknowledged = completedNoticesLoad.GetAwaiter().GetResult(); }
            catch (Exception ex) when (IsExpectedFailure(ex)) { noticesAcknowledged = false; }
            noticesLoaded = true;
            if (!noticesAcknowledged) IsOpen = true;
        }
        if (noticesSaving is { IsCompleted: true } completedNoticesSave)
        {
            noticesSaving = null;
            try
            {
                completedNoticesSave.GetAwaiter().GetResult();
                noticesAcknowledged = true;
                message = "Hinweisbestätigung lokal gespeichert. Bitte die Altersauswahl separat treffen.";
            }
            catch (Exception ex) when (IsExpectedFailure(ex))
            {
                noticesAcknowledged = false;
                message = "Die Hinweisbestätigung konnte nicht gespeichert werden. Bitte erneut bestätigen.";
            }
        }
        if (preferencesOperation is { IsCompleted: true } completedPreferences)
        {
            preferencesOperation = null;
            try
            {
                var preferences = completedPreferences.GetAwaiter().GetResult();
                if (!initialPreferencesLoaded) ApplyAudience(preferences);
                else message = "Altersauswahl lokal gespeichert.";
            }
            catch (Exception ex) when (IsExpectedFailure(ex))
            {
                ApplyAudience(new AudiencePreferences());
                message = "Altersauswahl konnte nicht geladen oder gespeichert werden. Bitte erneut auswählen.";
            }
            initialPreferencesLoaded = true;
        }
        if (saving is { IsCompleted: true } completedSave)
        {
            saving = null;
            try
            {
                var saved = completedSave.GetAwaiter().GetResult();
                if (saveGeneration == policyGeneration && CanDisplay(saved))
                {
                    var index = profiles.FindIndex(profile => profile.Id == saved.Id);
                    if (index < 0) profiles.Add(saved);
                    else profiles[index] = saved;
                    SetDraft(saved);
                    message = "Profil lokal gespeichert.";
                }
            }
            catch (Exception ex) when (IsExpectedFailure(ex))
            {
                if (saveGeneration == policyGeneration)
                    message = "Speichern fehlgeschlagen. Der Entwurf bleibt erhalten.";
            }
        }
    }

    public void SynchronizeProfiles()
    {
        if (catalogVersion == ownProfiles.Version) return;
        catalogVersion = ownProfiles.Version;
        profiles.Clear();
        if (!ProfileAccessReady) return;
        profiles.AddRange(ownProfiles.Profiles.Where(CanDisplay));
        if (dirty || saving is not null || ownProfiles.IsLoading) return;
        var updated = profiles.FirstOrDefault(profile => profile.Id == original.Id);
        if (updated is not null)
        {
            if (ProfileJson.Hash(updated) != ProfileJson.Hash(original)) SetDraft(updated);
        }
        else SetDraft(ownProfiles.ActiveProfile ?? profiles.FirstOrDefault() ?? new ProfileDocument());
    }

    public override void Draw()
    {
        NativeThemeRenderer.DrawInterior(themeSession.Theme, themeSession.Preferences.ReduceDecoration);
        if (ImGui.Button("Hilfe / Erste Schritte")) OpenHelp(HelpTopicId.FirstSteps);
        DrawHelpPanel();
        Wrapped("Profile, Kontakte und Einstellungen bleiben lokal. Direktaustausch startet nur nach ausdrücklicher Aktivierung.");
        Wrapped(message);
        if (!noticesLoaded)
        {
            Wrapped("Die lokale Hinweisbestätigung wird geprüft …");
            return;
        }
        if (!noticesAcknowledged)
        {
            DrawUsageOnboarding();
            return;
        }
        if (!initialPreferencesLoaded)
        {
            Wrapped("Lokale Altersauswahl wird geladen …");
            return;
        }
        if (store.Policy.Snapshot.Selection == AudienceSelection.Unspecified)
        {
            DrawAudienceChoice();
            return;
        }
        if (ownProfiles.ErrorMessage is { } profileError)
        {
            Wrapped(profileError);
            if (ImGui.Button("Profile / Leiste erneut versuchen")) ownProfiles.Retry();
        }
        ImGui.Separator();
        if (!ImGui.BeginTabBar("FRP1Tabs")) return;
        if (ImGui.BeginTabItem(T("Tab.MyProfile") + "###OwnProfileTab"))
        {
            ImGui.BeginDisabled(Busy);
            DrawProfile();
            ImGui.EndDisabled();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(T("Tab.Nearby") + "###NearbyTab"))
        {
            Help.Badge(HelpTopicId.Environment);
            Help.Badge(HelpTopicId.Binding);
            Help.Badge(HelpTopicId.Reader);
            socialUi.DrawEnvironmentTab();
            if (ImGui.CollapsingHeader("Bekannte Charaktere auf der Karte"))
            { Help.Badge(HelpTopicId.Map); GamePresentation?.DrawMapPanel(); }
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(T("Tab.Contacts") + "###ContactsTab"))
        {
            Help.Badge(HelpTopicId.Contacts);
            Help.Badge(HelpTopicId.Notes);
            Help.Badge(HelpTopicId.Block);
            socialUi.DrawContactsTab();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(T("Tab.Settings") + "###SettingsTab"))
        {
            Help.Badge(HelpTopicId.Language);
            Help.Badge(HelpTopicId.Automation);
            LocalTools?.DrawSettings();
            ImGui.Separator();
            DrawHelpSettings();
            RpBarWindow?.DrawSettings();
            DrawAppearanceSettings();
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Lokale Spielanzeige"))
            { Help.Badge(HelpTopicId.GamePresentation); GamePresentation?.DrawSettings(); }
            ImGui.Separator();
            if (ImGui.CollapsingHeader(T("Network.Title")))
            {
                Help.Badge(HelpTopicId.Network);
                Help.Badge(HelpTopicId.Trust);
                Help.Badge(HelpTopicId.IpConsent);
                Help.Badge(HelpTopicId.Sharing);
                Help.Badge(HelpTopicId.Block);
                socialUi.DrawNetworkSettings();
            }
            ImGui.Separator();
            if (ImGui.CollapsingHeader(T("Safety.Rules") + "###UsageRules")) DrawUsageNotices();
            ImGui.Separator();
            DrawAudienceChoice();
            ImGui.Separator();
            Wrapped("Kein Konto, keine Registrierung, kein Hosting und keine Telemetrie. Es werden keine Relay-, Such- oder Registrierungsdienste angesprochen.");
            Wrapped("Profile werden im Dalamud-Konfigurationsverzeichnis dieses Plugins gespeichert. JSON-Export gibt ausschließlich die öffentlichen Profilfelder aus.");
            Wrapped("Bestätigte Charakterzuordnung, Kopplung und Freigabe sind getrennte Entscheidungen. Eine sichere Verbindung beweist keinen Spielcharakterbesitz.");
            ImGui.EndTabItem();
        }
        if (LocalTools is not null && ImGui.BeginTabItem(T("Tab.Tools") + "###ToolsTab"))
        {
            Help.Badge(HelpTopicId.LocalTools);
            Help.Badge(HelpTopicId.ProfileLinks);
            LocalTools.DrawToolsPanel();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private void DrawHelpPanel()
    {
        if (helpTopic is not { } topic) return;
        ImGui.Separator();
        if (ImGui.BeginCombo("Hilfethema", HelpContent.Get(topic).Title))
        {
            foreach (var entry in HelpContent.All)
                if (ImGui.Selectable(entry.Value.Title, entry.Key == topic)) helpTopic = entry.Key;
            ImGui.EndCombo();
        }
        var content = HelpContent.Get(helpTopic.Value);
        Wrapped(content.Summary);
        Wrapped(content.Details);
        if (ImGui.Button("Hilfe schließen")) helpTopic = null;
        ImGui.Separator();
    }

    private void DrawHelpSettings()
    {
        if (!ImGui.CollapsingHeader("Bedienung und Hilfe", ImGuiTreeNodeFlags.DefaultOpen)) return;
        ImGui.BeginDisabled(!ownProfiles.SettingsLoaded);
        var show = ownProfiles.Settings.ShowHints;
        if (ImGui.Checkbox("Hilfshinweise anzeigen", ref show))
            ownProfiles.SetPreferences(ownProfiles.Settings with { ShowHints = show });
        ImGui.EndDisabled();
        Wrapped("Kurze Hinweise erscheinen beim Darüberfahren oder beim Tastaturfokus. Fragezeichen öffnen eine bleibende Erklärung.");
        if (ImGui.Button("Erste Schritte erneut anzeigen")) OpenHelp(HelpTopicId.FirstSteps);
    }

    private void DrawAppearanceSettings()
    {
        if (!ImGui.CollapsingHeader(T("Theme.Title") + "###Appearance", ImGuiTreeNodeFlags.DefaultOpen)) return;
        Help.Badge(HelpTopicId.ThemeAuto);
        Wrapped("Aktive Darstellung: " + themeSession.Theme.Name);
        if (!themeSession.HasCharacter)
            Wrapped("Zur Auswahl und lokalen Speicherung einer Darstellung bitte mit einem Charakter anmelden. Ohne Charakter bleibt Obsidian aktiv.");
        else if (themeSession.IsLoading)
            Wrapped("Die Darstellung dieses Charakters wird geladen …");
        else if (themeSession.HasPendingSave)
            Wrapped("Darstellung wird lokal gespeichert …");
        if (themeSession.ErrorMessage is { } error)
        {
            Wrapped(error);
            if (ImGui.Button("Darstellung erneut versuchen")) themeSession.Retry();
        }
        ImGui.BeginDisabled(!themeSession.HasCharacter);
        var preferences = themeSession.Preferences;
        var automatic = preferences.Mode == ThemeSelectionMode.Automatic;
        if (ImGui.Checkbox(T("Theme.Automatic") + "###AutoTheme", ref automatic))
            themeSession.SetPreferences(preferences with
            { Mode = automatic ? ThemeSelectionMode.Automatic : ThemeSelectionMode.Manual });
        Help.AfterItem(HelpTopicId.ThemeAuto);
        preferences = themeSession.Preferences;
        if (ImGui.BeginCombo(T("Theme.Manual") + "###ThemeChoice", ThemeCatalog.Get(preferences.ManualThemeId).Name))
        {
            foreach (var theme in ThemeCatalog.All)
                if (ImGui.Selectable(theme.Name, preferences.Mode == ThemeSelectionMode.Manual && preferences.ManualThemeId == theme.Id))
                    themeSession.SetPreferences(themeSession.Preferences with
                    { Mode = ThemeSelectionMode.Manual, ManualThemeId = theme.Id });
            ImGui.EndCombo();
        }
        Help.AfterItem(HelpTopicId.ThemeManual);
        var reduce = themeSession.Preferences.ReduceDecoration;
        if (ImGui.Checkbox(T("Theme.Decoration") + "###ReduceDecoration", ref reduce))
            themeSession.SetPreferences(themeSession.Preferences with { ReduceDecoration = reduce });
        Help.AfterItem(HelpTopicId.Decorations);
        ImGui.EndDisabled();
    }

    private void DrawProfile()
    {
        Help.Badge(HelpTopicId.ProfileSelect);
        if (ImGui.Button("Aktives eigenes Profil lesen")) OpenOwnProfile();
        Help.AfterItem(HelpTopicId.Reader);
        if (ImGui.Button("Gespeichertes Editorprofil lesen"))
        {
            var selectedId = original.Id;
            if (GetSavedOwnProfile(selectedId) is not null)
                profileUi.OpenOwn(() => GetSavedOwnProfile(selectedId));
            else message = "Bitte den Entwurf zuerst lokal speichern. Das Journal zeigt ausschließlich gespeicherte Profile.";
        }
        if (ImGui.Button("Profilverweis kopieren"))
        {
            var reference = OwnProfileReferenceProvider?.Invoke(original.Id);
            if (reference is null) message = "Ein eigener Profilverweis benötigt ein gespeichertes Profil und eine ausdrücklich aktive Direktverbindung.";
            else { ImGui.SetClipboardText(reference); message = T("Reference.Copied"); }
        }
        Help.AfterItem(HelpTopicId.ProfileLinks);
        ImGui.BeginDisabled(dirty);
        Wrapped("Editorprofil: " + (string.IsNullOrEmpty(original.DisplayName) ? "Neues Profil" : ProfileLabel(original)));
        if (ImGui.BeginCombo("Profil auswählen", "Gespeichertes Profil öffnen"))
        {
            foreach (var profile in profiles)
            {
                ImGui.PushID(profile.Id.ToString("N"));
                if (ProfileSelectable(profile, original.Id == profile.Id)) SetDraft(profile);
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }
        Help.AfterItem(HelpTopicId.ProfileSelect);
        if (profiles.Count >= 128) Wrapped("Die Oberfläche verwaltet höchstens 128 lokale Profile.");
        ImGui.BeginDisabled(profiles.Count >= 128);
        if (ImGui.Button(T("Profile.New") + "###NewProfile")) SetDraft(new ProfileDocument());
        Help.AfterItem(HelpTopicId.NewProfile);
        ImGui.EndDisabled();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!dirty);
        if (ImGui.Button(T("Common.Save") + "###SaveProfile")) SaveDraft();
        Help.AfterItem(HelpTopicId.SaveProfile);
        ImGui.SameLine();
        if (ImGui.Button(T("Common.Discard") + "###DiscardProfile")) SetDraft(original);
        Help.AfterItem(HelpTopicId.Discard);
        ImGui.EndDisabled();
        if (dirty) Wrapped("Ungespeicherte Änderungen: vor einem Profilwechsel speichern oder verwerfen.");

        if (ImGui.BeginTable("EditorPreview", 2, ImGuiTableFlags.Resizable))
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Editor");
            if (store.Policy.AcceptsMatureContent)
            {
                var mature = contentRating == ContentRating.Mature18;
                if (ImGui.Checkbox("Dieses Profil ist ab 18", ref mature))
                {
                    contentRating = mature ? ContentRating.Mature18 : ContentRating.General;
                    dirty = true;
                }
                Help.AfterItem(HelpTopicId.Mature);
            }
            else Wrapped("Inhalte ab 18 sind gesperrt. Eigene Profile können hier nur allgemein zugänglich erstellt werden.");
            DrawDetails();
            ImGui.Separator();
            ImGui.TextUnformatted("Charakterdaten");
            Help.Badge(HelpTopicId.CharacterFields);
            foreach (var field in ShortFields)
            {
                var value = fields[field.Key];
                if (ImGui.InputText(L(field.Label) + "###" + field.Key, ref value, field.Limit))
                {
                    fields[field.Key] = value;
                    dirty = true;
                }
            }
            foreach (var field in LongFields)
            {
                if (!ImGui.CollapsingHeader(L(field.Label) + "###Section" + field.Key)) continue;
                var value = fields[field.Key];
                if (ImGui.InputTextMultiline("##" + field.Key, ref value, 32768, new Vector2(-1, 130)))
                {
                    fields[field.Key] = value;
                    dirty = true;
                }
            }
            DrawExtras();
            DrawGlances();
            DrawRpHooks();
            Help.Badge(HelpTopicId.Portrait);
            Help.Badge(HelpTopicId.Formatting);
            Help.Badge(HelpTopicId.Companions);
            Help.Badge(HelpTopicId.Preferences);
            extended.ContentRating = contentRating;
            var generation = editorGeneration;
            var policy = policyGeneration;
            profileUi.DrawEditor(extended, () =>
            {
                if (generation == editorGeneration && policy == policyGeneration && ProfileAccessReady && !Busy) dirty = true;
            }, contentRating);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Live-Vorschau");
            if (contentRating == ContentRating.Mature18)
                ImGui.TextUnformatted("AB 18 – nicht jugendfreies Profil");
            ImGui.Separator();
            DrawDetailsPreview();
            foreach (var field in ShortFields)
                if (fields[field.Key].Length != 0) Wrapped(field.Label + ": " + fields[field.Key]);
            foreach (var field in LongFields)
                if (fields[field.Key].Length != 0)
                {
                    ImGui.Separator();
                    ImGui.TextUnformatted(field.Label);
                    Wrapped(fields[field.Key]);
                }
            foreach (var pair in extraFields) Wrapped(pair.Key + ": " + pair.Value);
            ImGui.EndTable();
        }
        DrawImportExport();
    }

    private static void DrawUsageNotices()
    {
        foreach (var notice in UsageGuidelines.Notices)
        {
            Wrapped(notice);
            ImGui.Spacing();
        }
    }

    private void DrawUsageOnboarding()
    {
        ImGui.TextUnformatted("Profilregeln und Hinweise");
        Help.Badge(HelpTopicId.Rules);
        ImGui.Separator();
        DrawUsageNotices();
        ImGui.Separator();
        ImGui.BeginDisabled(noticesSaving is not null);
        ImGui.Checkbox("Ich habe die Hinweise gelesen und beachte die Profilkennzeichnung.", ref noticesCheckbox);
        ImGui.BeginDisabled(!noticesCheckbox);
        if (ImGui.Button("Hinweise bestätigen"))
        {
            var token = lifetime.Token;
            noticesSaving = Task.Run(() => usageAcknowledgements.AcknowledgeAsync(token), token);
            message = "Hinweisbestätigung wird lokal gespeichert …";
        }
        ImGui.EndDisabled();
        ImGui.EndDisabled();
    }

    private void DrawDetails()
    {
        if (!ImGui.CollapsingHeader("RP-Status und Kurzprofil", ImGuiTreeNodeFlags.DefaultOpen)) return;
        Help.Badge(HelpTopicId.Status);
        if (ImGui.BeginCombo(T("Status.Label") + "###RpStatus", StatusLabel(details.Status)))
        {
            foreach (var status in Enum.GetValues<RpStatus>())
                if (ImGui.Selectable(StatusLabel(status), details.Status == status))
                {
                    details.Status = status;
                    dirty = true;
                }
            ImGui.EndCombo();
        }
        Help.AfterItem(HelpTopicId.Status);
        if (ImGui.BeginCombo(T("Availability.Label") + "###RpAvailability", AvailabilityLabel(details.Availability)))
        {
            foreach (var availability in Enum.GetValues<RpAvailability>())
                if (ImGui.Selectable(AvailabilityLabel(availability), details.Availability == availability))
                {
                    details.Availability = availability;
                    dirty = true;
                }
            ImGui.EndCombo();
        }
        Help.AfterItem(HelpTopicId.Availability);
        dirty |= ImGui.InputTextMultiline(T("Profile.Summary") + "###Summary", ref details.Summary, 2048, new Vector2(-1, 75));
        dirty |= ImGui.InputText(T("Profile.Pronouns") + "###Pronouns", ref details.Pronouns, 320);
        dirty |= ImGui.InputText(T("Profile.Location") + "###CurrentLocation", ref details.CurrentLocation, 1024);
        dirty |= ImGui.InputText(T("Profile.FreeCompany") + "###FreeCompany", ref details.FreeCompany, 640);
        if (ImGui.CollapsingHeader(T("Profile.Ooc") + "###OocInformation"))
        {
            Wrapped("Diese Angaben gehören zum öffentlichen Profil. Keine privaten Kontaktdaten eintragen, die du nicht teilen möchtest.");
            dirty |= ImGui.InputTextMultiline("##OocInformation", ref details.OocInformation, 16384, new Vector2(-1, 100));
        }
    }

    private void DrawGlances()
    {
        if (!ImGui.CollapsingHeader(T("Profile.Glances"))) return;
        Help.Badge(HelpTopicId.Glances);
        Wrapped("Bis zu fünf Eindrücke. Aktive Einträge erscheinen im Journal und in der eigenen RP-Leiste; deaktivierte bleiben im Profil gespeichert.");
        DrawGlanceTemplates();
        for (var index = 0; index < details.Glances.Count; index++)
        {
            ImGui.PushID("glance" + index);
            var entry = details.Glances[index];
            var label = entry.Label;
            var text = entry.Text;
            var enabled = entry.Enabled;
            var changed = ImGui.Checkbox(T("Common.Enabled"), ref enabled);
            changed |= ImGui.InputText("Bezeichnung", ref label, 320);
            changed |= ImGui.InputTextMultiline("Beschreibung", ref text, 4096, new Vector2(-1, 85));
            if (changed)
            {
                details.Glances[index] = entry = entry with { Label = label, Text = text, Enabled = enabled };
                dirty = true;
            }
            DrawLocalIcon(entry.IconId, new Vector2(28));
            if (ImGui.Button("Symbol auswählen")) ChooseGlanceIcon(entry);
            var reordered = DrawEntryActions(details.Glances, index);
            ImGui.PopID();
            ImGui.Separator();
            if (reordered) break;
        }
        ImGui.BeginDisabled(details.Glances.Count >= 5);
        if (ImGui.Button(UiText.Format("Common.Add", LanguageProvider()) + $" ({details.Glances.Count}/5)###AddGlance"))
        {
            details.Glances.Add(new GlanceEntry("Ein Blick", ""));
            dirty = true;
        }
        ImGui.EndDisabled();
    }

    private void ChooseGlanceIcon(GlanceEntry entry)
    {
        var generation = editorGeneration;
        var policy = policyGeneration;
        var rating = contentRating;
        var draft = details;
        profileUi.OpenIconPicker(entry.IconId, icon =>
        {
            if (generation != editorGeneration || policy != policyGeneration || !ProfileAccessReady ||
                rating != contentRating || !ReferenceEquals(details, draft) || Busy) return;
            var index = details.Glances.FindIndex(current => ReferenceEquals(current, entry));
            if (index < 0) return;
            details.Glances[index] = details.Glances[index] with { IconId = icon };
            dirty = true;
        }, rating);
    }

    private void DrawGlanceTemplates()
    {
        if (ImGui.Button("Einblicke als lokale Vorlage merken"))
        {
            try { glanceTemplate = GlanceTemplates.Copy(BuildDraft(), store.Policy); message = "Einblick-Vorlage lokal gemerkt."; }
            catch (Exception ex) when (IsExpectedFailure(ex)) { message = "Vorlage nicht verfügbar. Bitte Profil und Freigabe prüfen."; }
        }
        ImGui.BeginDisabled(glanceTemplate is null);
        if (ImGui.Button("Gemerkte Vorlage übernehmen")) ApplyGlanceTemplate(glanceTemplate!);
        ImGui.EndDisabled();
        if (ImGui.Button("Fünf leere Standard-Einblicke übernehmen"))
            ApplyGlanceTemplate(new GlanceTemplateSet(ContentRating.General, GlanceTemplates.Defaults));
    }

    private void ApplyGlanceTemplate(GlanceTemplateSet template)
    {
        try
        {
            var next = GlanceTemplates.Apply(BuildDraft(), template, store.Policy);
            profileUi.ResetEditorContext();
            editorGeneration++;
            details.Glances.Clear();
            details.Glances.AddRange(next.Glances.Select(entry => entry with { }));
            dirty = true;
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        { message = "Vorlage abgelehnt. Ab-18-Inhalte dürfen nicht in allgemeine Profile übernommen werden."; }
    }

    internal void DrawLocalIcon(uint id, Vector2 size)
    {
        if (id == 0 || !ProfileAccessReady) return;
        try
        {
            var texture = textures.GetFromGameIcon(id).GetWrapOrDefault();
            if (texture is not null) ImGui.Image(texture.Handle, size);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { }
    }

    private void DrawRpHooks()
    {
        if (!ImGui.CollapsingHeader(T("Profile.Hooks") + "###RpHooks")) return;
        Help.Badge(HelpTopicId.Hooks);
        Wrapped("Bis zu zehn Ideen für Begegnungen und gemeinsames Rollenspiel.");
        for (var index = 0; index < details.RpHooks.Count; index++)
        {
            ImGui.PushID("hook" + index);
            var entry = details.RpHooks[index];
            var title = entry.Title;
            var text = entry.Text;
            var changed = ImGui.InputText("Überschrift", ref title, 320);
            changed |= ImGui.InputTextMultiline("Anknüpfungspunkt", ref text, 4096, new Vector2(-1, 85));
            if (changed)
            {
                details.RpHooks[index] = new RpHook(title, text);
                dirty = true;
            }
            var reordered = DrawEntryActions(details.RpHooks, index);
            ImGui.PopID();
            ImGui.Separator();
            if (reordered) break;
        }
        ImGui.BeginDisabled(details.RpHooks.Count >= 10);
        if (ImGui.Button(UiText.Format("Common.Add", LanguageProvider()) + $" ({details.RpHooks.Count}/10)###AddRpHook"))
        {
            details.RpHooks.Add(new RpHook("RP-Anknüpfungspunkt", ""));
            dirty = true;
        }
        ImGui.EndDisabled();
    }

    private bool DrawEntryActions<T>(List<T> entries, int index)
    {
        ImGui.BeginDisabled(index == 0);
        var moveUp = ImGui.Button("Nach oben");
        Help.AfterItem(HelpTopicId.Reorder);
        ImGui.EndDisabled();
        if (moveUp)
        {
            (entries[index - 1], entries[index]) = (entries[index], entries[index - 1]);
            dirty = true;
            return true;
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(index == entries.Count - 1);
        var moveDown = ImGui.Button("Nach unten");
        Help.AfterItem(HelpTopicId.Reorder);
        ImGui.EndDisabled();
        if (moveDown)
        {
            (entries[index + 1], entries[index]) = (entries[index], entries[index + 1]);
            dirty = true;
            return true;
        }
        ImGui.SameLine();
        if (!ImGui.Button(this.T("Common.Remove") + "###RemoveEntry")) return false;
        entries.RemoveAt(index);
        dirty = true;
        return true;
    }

    private void DrawDetailsPreview()
    {
        Wrapped("Status: " + StatusLabel(details.Status));
        Wrapped("Verfügbarkeit: " + AvailabilityLabel(details.Availability));
        if (details.Summary.Length != 0) Wrapped(details.Summary);
        if (details.Pronouns.Length != 0) Wrapped("Pronomen: " + details.Pronouns);
        if (details.CurrentLocation.Length != 0) Wrapped("Aktueller Ort: " + details.CurrentLocation);
        if (details.FreeCompany.Length != 0) Wrapped("Freie Gesellschaft: " + details.FreeCompany);
        if (details.Glances.Count != 0)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Auf den ersten Blick");
        foreach (var entry in details.Glances.Where(entry => entry.Enabled))
            {
                DrawLocalIcon(entry.IconId, new Vector2(24));
                Wrapped(entry.Label);
                Wrapped(entry.Text);
            }
        }
        if (details.RpHooks.Count != 0)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("RP-Anknüpfungspunkte");
            foreach (var entry in details.RpHooks)
            {
                Wrapped(entry.Title);
                Wrapped(entry.Text);
            }
        }
        if (details.OocInformation.Length != 0)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("OOC-Informationen");
            Wrapped(details.OocInformation);
        }
        ImGui.Separator();
    }

    private string StatusLabel(RpStatus status) => T("Status." + status);
    private string AvailabilityLabel(RpAvailability availability) => T("Availability." + availability);

    private void DrawExtras()
    {
        if (!ImGui.CollapsingHeader("Zusatzfelder")) return;
        Help.Badge(HelpTopicId.ExtraFields);
        var index = 0;
        foreach (var pair in extraFields.ToArray())
        {
            ImGui.PushID(index++);
            ImGui.TextUnformatted(pair.Key);
            var value = pair.Value;
            if (ImGui.InputTextMultiline("##value", ref value, 8192, new Vector2(-1, 65)))
            {
                extraFields[pair.Key] = value;
                dirty = true;
            }
            if (ImGui.Button("Feld entfernen"))
            {
                extraFields.Remove(pair.Key);
                dirty = true;
            }
            ImGui.PopID();
        }
        ImGui.BeginDisabled(extraFields.Count >= 24);
        ImGui.InputText("Feldname", ref extraKey, 320);
        ImGui.InputText("Feldinhalt", ref extraValue, 8192);
        if (ImGui.Button("Feld hinzufügen"))
        {
            if (string.IsNullOrWhiteSpace(extraKey) || extraFields.ContainsKey(extraKey))
                message = "Bitte einen eindeutigen Feldnamen eingeben.";
            else
            {
                extraFields.Add(extraKey, extraValue);
                extraKey = extraValue = "";
                dirty = true;
            }
        }
        ImGui.EndDisabled();
    }

    private void DrawImportExport()
    {
        if (!ImGui.CollapsingHeader("JSON importieren / exportieren")) return;
        Help.Badge(HelpTopicId.Import);
        Wrapped("JSON hier einfügen oder den aktuellen Entwurf exportieren. Ein Import wird als neues lokales Profil angelegt und überschreibt kein bestehendes Profil.");
        ImGui.InputTextMultiline("##ProfileJson", ref jsonText, 196608, new Vector2(-1, 160));
        if (ImGui.Button(T("Profile.Export") + "###ExportProfile"))
        {
            try
            {
                var snapshot = BuildDraft();
                store.Policy.EnsureAllowed(snapshot);
                jsonText = Encoding.UTF8.GetString(ProfileJson.Export(snapshot));
                message = "Öffentliche Profildaten exportiert. Der Entwurf wurde dadurch nicht gespeichert.";
            }
            catch (Exception ex) when (IsExpectedFailure(ex))
            { message = "Export fehlgeschlagen: RP-Name, Feldlängen und Profilgröße prüfen."; }
        }
        Help.AfterItem(HelpTopicId.Export);
        ImGui.SameLine();
        if (ImGui.Button(T("Common.Copy") + "###CopyJson")) ImGui.SetClipboardText(jsonText);
        Help.AfterItem(HelpTopicId.Clipboard);
        ImGui.BeginDisabled(dirty || profiles.Count >= 128);
        if (ImGui.Button(T("Profile.Import") + "###ImportProfile"))
        {
            try
            {
                if (Encoding.UTF8.GetByteCount(jsonText) > ProfileJson.MaximumBytes)
                    throw new InvalidDataException("Profile exceeds size limit.");
                var imported = ProfileJson.Import(Encoding.UTF8.GetBytes(jsonText), store.Policy);
                SetDraft(imported with { Id = Guid.NewGuid(), Revision = 1 });
                dirty = true;
                message = "Profil als neue Kopie importiert. Zum Übernehmen speichern.";
            }
            catch (Exception ex) when (IsExpectedFailure(ex))
            { message = "Import abgelehnt: ungültiges, zu großes oder nicht unterstütztes Profil."; }
        }
        Help.AfterItem(HelpTopicId.Import);
        ImGui.EndDisabled();
    }

    private void SaveDraft()
    {
        try
        {
            var exists = profiles.Any(profile => profile.Id == original.Id);
            var snapshot = BuildDraft();
            store.Policy.EnsureAllowed(snapshot);
            ProfileJson.Validate(snapshot);
            profileUi.ResetEditorContext();
            editorGeneration++;
            var token = lifetime.Token;
            saveGeneration = policyGeneration;
            saving = ownProfiles.SaveEditorAsync(snapshot, exists ? original.Revision : 0,
                exists ? ProfileJson.Hash(original) : null, token);
            message = "Profil wird gespeichert …";
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        { message = "Speichern nicht möglich: RP-Name, Feldlängen und Profilgröße prüfen."; }
    }

    private ProfileDocument BuildDraft() => extended.ApplyTo(details.ApplyTo(original with
    {
        DisplayName = fields["DisplayName"], FirstName = fields["FirstName"], LastName = fields["LastName"],
        ContentRating = contentRating,
        Title = fields["Title"], Race = fields["Race"], Clan = fields["Clan"], Age = fields["Age"],
        Gender = fields["Gender"], Origin = fields["Origin"], Occupation = fields["Occupation"],
        Appearance = fields["Appearance"], Personality = fields["Personality"], Background = fields["Background"],
        ExtraFields = new SortedDictionary<string, string>(extraFields, StringComparer.Ordinal),
    }));

    private void SetDraft(ProfileDocument profile)
    {
        profileUi.ResetEditorContext();
        editorGeneration++;
        original = profile;
        details = new ProfileDetailsDraft(profile);
        extended = ExtendedProfileDraft.From(profile);
        contentRating = profile.ContentRating;
        fields["DisplayName"] = profile.DisplayName; fields["FirstName"] = profile.FirstName;
        fields["LastName"] = profile.LastName; fields["Title"] = profile.Title;
        fields["Race"] = profile.Race; fields["Clan"] = profile.Clan; fields["Age"] = profile.Age;
        fields["Gender"] = profile.Gender; fields["Origin"] = profile.Origin; fields["Occupation"] = profile.Occupation;
        fields["Appearance"] = profile.Appearance; fields["Personality"] = profile.Personality;
        fields["Background"] = profile.Background;
        extraFields.Clear();
        foreach (var pair in profile.ExtraFields) extraFields.Add(pair.Key, pair.Value);
        dirty = false;
        extraKey = extraValue = "";
    }

    private static void Wrapped(string text)
    {
        ImGui.PushTextWrapPos(0);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }

    private static bool IsExpectedFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or
        InvalidDataException or OperationCanceledException or ArgumentException or OverflowException or InvalidOperationException;

    internal bool CanDisplay(ProfileDocument profile)
    {
        try { store.Policy.EnsureAllowed(profile); return true; }
        catch (InvalidDataException) { return false; }
    }

    private static string ProfileLabel(ProfileDocument profile) =>
        (profile.ContentRating == ContentRating.Mature18 ? "[AB 18] " : "") + profile.DisplayName;

    internal static bool ProfileSelectable(ProfileDocument profile, bool selected)
    {
        var before = ImGui.GetCursorPos();
        var clicked = ImGui.Selectable("##profile-choice", selected);
        var after = ImGui.GetCursorPos();
        ImGui.SetCursorPos(before);
        ImGui.TextUnformatted(ProfileLabel(profile).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' '));
        ImGui.SetCursorPos(after);
        return clicked;
    }

    private void ApplyAudience(AudiencePreferences preferences)
    {
        store.Policy.SetAudience(preferences);
        policyGeneration++;
        profiles.Clear();
        SetDraft(new ProfileDocument());
        jsonText = "";
        glanceTemplate = null;
        catalogVersion = -1;
    }

    private void ChangeAudience(AudiencePreferences preferences)
    {
        ApplyAudience(preferences);
        var token = lifetime.Token;
        preferencesOperation = Task.Run(async () =>
        {
            await audienceSettings.SaveAsync(preferences, token).ConfigureAwait(false);
            return preferences;
        }, token);
        message = "Altersauswahl wird lokal gespeichert …";
    }

    private void DrawAudienceChoice()
    {
        Help.Badge(HelpTopicId.Age);
        Wrapped("Bitte für diese Pluginsitzung aktiv auswählen: Bist du mindestens 18 Jahre alt? Die Angabe wird nur lokal gespeichert. Dies ist eine Selbstauskunft und keine Altersprüfung.");
        Wrapped("Nach jedem Pluginstart ist eine neue Auswahl erforderlich. Inhalte ab 18 bleiben außerdem bis zu einer erneuten ausdrücklichen Freigabe gesperrt.");
        Wrapped("Eine Änderung der Altersauswahl verwirft offene Entwürfe und den JSON-Inhalt, damit gesperrte Inhalte sofort verschwinden.");
        var audience = store.Policy.Snapshot;
        ImGui.TextUnformatted(audience.Selection switch
        {
            AudienceSelection.Adult => "Aktuelle Auswahl: 18 Jahre oder älter",
            AudienceSelection.Under18 => "Aktuelle Auswahl: unter 18 Jahre",
            _ => "Noch keine Auswahl getroffen",
        });
        ImGui.BeginDisabled(preferencesOperation is not null);
        if (ImGui.Button("Ich bin unter 18"))
            ChangeAudience(new AudiencePreferences(AudienceSelection.Under18));
        ImGui.SameLine();
        if (ImGui.Button("Ich bin 18 oder älter"))
            ChangeAudience(new AudiencePreferences(AudienceSelection.Adult));
        if (audience.Selection == AudienceSelection.Adult)
        {
            var optedIn = audience.MatureContentOptIn;
            if (ImGui.Checkbox("Inhalte ab 18 anzeigen und erstellen", ref optedIn))
                ChangeAudience(new AudiencePreferences(AudienceSelection.Adult, optedIn));
        }
        ImGui.EndDisabled();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        var pending = new List<Task>();
        if (saving is not null) pending.Add(saving);
        if (preferencesOperation is not null) pending.Add(preferencesOperation);
        if (noticesLoading is not null) pending.Add(noticesLoading);
        if (noticesSaving is not null) pending.Add(noticesSaving);
        // Unloading never blocks the game thread; observe failures and release cancellation after I/O ends.
        _ = Task.WhenAll(pending).ContinueWith(task =>
        {
            _ = task.Exception;
            lifetime.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
