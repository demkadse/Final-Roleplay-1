using System.Collections.ObjectModel;
using System.Globalization;

namespace FRP1.Plugin.Localization;

public static class UiText
{
    private static int currentLanguage;
    public static UiLanguage CurrentLanguage
    {
        get => (UiLanguage)Volatile.Read(ref currentLanguage);
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            Volatile.Write(ref currentLanguage, (int)value);
        }
    }
    private static readonly IReadOnlyDictionary<string, (string De, string En)> Catalog =
        new ReadOnlyDictionary<string, (string, string)>(new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["Main.Title"] = ("Final Roleplay 1", "Final Roleplay 1"),
            ["Integration.Title"] = ("Integrationen", "Integrations"),
            ["Ipc.Title"] = ("Lokale FRP1-Schnittstelle", "Local FRP1 interface"),
            ["Ipc.Explanation"] = ("Diese Freigaben gelten für alle lokal geladenen Plugins. Lesen liefert nur Status und Verfügbarkeit allgemein gekennzeichneter aktiver Profile. Erlaubte Änderungen werden normal gespeichert und können über bestehende Profilfreigaben synchronisiert werden.",
                "These permissions apply to all locally loaded plugins. Reading returns only the status and availability of general-rated active profiles. Allowed changes are saved normally and may sync through existing profile sharing permissions."),
            ["Ipc.Read"] = ("Lokalen Status lesen erlauben", "Allow reading local status"),
            ["Ipc.Control"] = ("Lokale Aktionen erlauben", "Allow local actions"),
            ["Ipc.Status"] = ("RP-Status ändern erlauben", "Allow changing RP status"),
            ["Ipc.Availability"] = ("Verfügbarkeit ändern erlauben", "Allow changing availability"),
            ["Ipc.Open"] = ("Eigenes aktives Profil öffnen erlauben", "Allow opening own active profile"),
            ["Ipc.Unavailable"] = ("Die lokale Schnittstelle konnte nicht registriert werden. Bitte FRP1 neu laden.", "The local interface could not be registered. Please reload FRP1."),
            ["Ipc.Readiness.0"] = ("FRP1-Schnittstelle: ausgeschaltet", "FRP1 interface: disabled"),
            ["Ipc.Readiness.1"] = ("FRP1-Schnittstelle: Profil oder Zugriff nicht bereit", "FRP1 interface: profile or access is not ready"),
            ["Ipc.Readiness.2"] = ("FRP1-Schnittstelle: Entwurf oder Vorgang offen", "FRP1 interface: draft or operation in progress"),
            ["Ipc.Readiness.3"] = ("FRP1-Schnittstelle: bereit", "FRP1 interface: ready"),
            ["Integration.EnableHonorific"] = ("Honorific-Titel lokal lesen", "Read Honorific title locally"),
            ["Integration.Disabled"] = ("Honorific: ausgeschaltet", "Honorific: disabled"),
            ["Integration.Missing"] = ("Honorific: nicht verfügbar", "Honorific: unavailable"),
            ["Integration.Incompatible"] = ("Honorific: API nicht unterstützt (benötigt 3.2)", "Honorific: unsupported API (requires 3.2)"),
            ["Integration.Ready"] = ("Honorific: bereit", "Honorific: ready"),
            ["Integration.Busy"] = ("Honorific: wird gelesen …", "Honorific: reading …"),
            ["Integration.Error"] = ("Honorific: Lesen fehlgeschlagen oder ungültiger Titel", "Honorific: read failed or invalid title"),
            ["Integration.Blocked"] = ("Honorific: Charakter oder Profilzugriff noch nicht bereit", "Honorific: character or profile access is not ready"),
            ["Integration.Explanation"] = ("Nur lokales Lesen. Prüfe den Titel und die Inhaltskennzeichnung vor der Übernahme. Der Titel wird zum Entwurf; danach normal speichern. Honorific wird nicht verändert und keine Freigabe erteilt.",
                "Local reading only. Check the title and content rating before importing. The title becomes a draft; save it normally afterwards. Honorific is unchanged and no sharing permission is granted."),
            ["Integration.SettingsError"] = ("Integrationseinstellungen konnten nicht geladen oder gespeichert werden. Bitte die lokale Auswahl erneut setzen.", "Integration settings could not be loaded or saved. Please set the local choice again."),
            ["Integration.Refresh"] = ("Titel erneut lesen", "Read title again"),
            ["Integration.Preview"] = ("Honorific-Titel (Vorschau):", "Honorific title (preview):"),
            ["Integration.SelectedProfile"] = ("Ausgewähltes eigenes Profil: ", "Selected own profile: "),
            ["Integration.Import"] = ("Titel in Entwurf übernehmen", "Import title into draft"),
            ["Integration.Dirty"] = ("Zuerst den offenen Entwurf speichern oder verwerfen.", "Save or discard the open draft first."),
            ["Integration.EditorBusy"] = ("Bitte den laufenden Profilvorgang abwarten.", "Wait for the current profile operation."),
            ["Integration.Rejected"] = ("Übernahme verworfen: Der Zustand hat sich geändert oder die Aktion war zu schnell. Bitte erneut prüfen.", "Import rejected: the state changed or the action was too fast. Please check again."),
            ["Integration.Imported"] = ("Honorific-Titel im Entwurf. Inhalt prüfen und normal speichern.", "Honorific title is in the draft. Check its content and save normally."),
            ["Tab.MyProfile"] = ("Mein Charakter", "My character"), ["Tab.Nearby"] = ("Umgebung", "Nearby"),
            ["Tab.Contacts"] = ("Kontakte", "Contacts"), ["Tab.Settings"] = ("Einstellungen", "Settings"),
            ["Tab.Tools"] = ("Lokale Werkzeuge", "Local tools"),
            ["Common.Save"] = ("Speichern", "Save"), ["Common.Cancel"] = ("Abbrechen", "Cancel"),
            ["Common.Discard"] = ("Änderungen verwerfen", "Discard changes"), ["Common.Close"] = ("Schließen", "Close"),
            ["Common.Retry"] = ("Erneut versuchen", "Retry"), ["Common.Copy"] = ("Kopieren", "Copy"),
            ["Common.Open"] = ("Öffnen", "Open"), ["Common.Remove"] = ("Entfernen", "Remove"),
            ["Common.Add"] = ("Hinzufügen", "Add"), ["Common.Name"] = ("Name", "Name"),
            ["Common.Edit"] = ("Bearbeiten", "Edit"), ["Common.Apply"] = ("Übernehmen", "Apply"),
            ["Common.Enabled"] = ("Aktiv", "Enabled"), ["Common.Preview"] = ("Vorschau", "Preview"),
            ["Compact.Menu"] = ("Menü", "Menu"), ["Compact.Help"] = ("Hilfe", "Help"),
            ["Compact.Discard"] = ("Verwerfen", "Discard"), ["Compact.Draft"] = ("Entwurf", "Draft"),
            ["Compact.Add"] = ("+ Angabe", "+ Detail"), ["Compact.Variants"] = ("Profilvarianten", "Profiles"),
            ["Compact.Tools"] = ("Werkzeuge", "Tools"), ["Compact.Bar"] = ("RP-Leiste", "RP bar"),
            ["Compact.Game"] = ("Spielanzeige", "Game display"), ["Compact.Network"] = ("Verbindungen", "Connections"),
            ["Compact.Language"] = ("Sprache", "Language"), ["Compact.Safety"] = ("Alter & Inhalte", "Age & content"),
            ["Compact.Map"] = ("Karte", "Map"), ["Compact.Legacy"] = ("Bisherige Auswahl", "Previous selection"),
            ["Compact.Existing"] = ("Vorhandene Zusatzinhalte", "Existing details"),
            ["Compact.Templates"] = ("Vorlagen", "Templates"), ["Compact.Inactive"] = ("Inaktiv", "Inactive"),
            ["Compact.Start"] = ("Dein Charakter beginnt mit einem Namen.", "Your character begins with a name."),
            ["Compact.Hints"] = ("Hinweise erscheinen beim Darüberfahren. Ausführliche Hilfe öffnest du oben über Hilfe.",
                "Hover for hints. Open Help above for detailed guidance."),
            ["Common.Loading"] = ("Wird geladen …", "Loading …"), ["Common.Unavailable"] = ("Nicht verfügbar", "Unavailable"),
            ["Profile.New"] = ("Neues Profil", "New profile"), ["Profile.Select"] = ("Profil auswählen", "Select profile"),
            ["Profile.Active"] = ("Aktives RP-Profil", "Active RP profile"), ["Profile.Name"] = ("RP-Name", "RP name"),
            ["Profile.Title"] = ("Titel", "Title"), ["Profile.Summary"] = ("Kurzbeschreibung", "Summary"),
            ["Profile.Pronouns"] = ("Pronomen", "Pronouns"), ["Profile.Appearance"] = ("Aussehen", "Appearance"),
            ["Profile.Personality"] = ("Persönlichkeit", "Personality"), ["Profile.Background"] = ("Hintergrundgeschichte", "Background"),
            ["Profile.Ooc"] = ("OOC-Informationen", "OOC information"), ["Profile.Location"] = ("Aktueller Ort", "Current location"),
            ["Profile.FreeCompany"] = ("Freie Gesellschaft", "Free company"), ["Profile.Glances"] = ("Auf den ersten Blick", "At first glance"),
            ["Profile.Hooks"] = ("RP-Anknüpfungspunkte", "RP hooks"), ["Profile.Import"] = ("JSON importieren", "Import JSON"),
            ["Profile.Export"] = ("JSON exportieren", "Export JSON"), ["Profile.Mature"] = ("Profil ab 18", "Mature profile (18+)"),
            ["Status.Label"] = ("RP-Status", "RP status"), ["Status.Ic"] = ("IC – in der Rolle", "IC – in character"),
            ["Status.Ooc"] = ("OOC – außerhalb der Rolle", "OOC – out of character"), ["Status.Afk"] = ("AFK – abwesend", "AFK – away"),
            ["Availability.Label"] = ("RP-Verfügbarkeit", "RP availability"), ["Availability.Open"] = ("Offen für RP", "Open for RP"),
            ["Availability.Busy"] = ("Beschäftigt", "Busy"), ["Availability.Unavailable"] = ("Kein RP", "Unavailable for RP"),
            ["Safety.Rules"] = ("Profilregeln und Hinweise", "Profile rules and guidance"),
            ["Safety.Age"] = ("Dein tatsächliches Alter", "Your actual age"), ["Safety.Under18"] = ("Unter 18", "Under 18"),
            ["Safety.Adult"] = ("18 oder älter", "18 or older"), ["Safety.OptIn"] = ("Inhalte ab 18 ausdrücklich freigeben", "Explicitly allow mature content"),
            ["Safety.Required"] = ("Bitte zuerst Hinweise bestätigen und die Altersauswahl treffen.", "Please acknowledge the guidance and choose your age group first."),
            ["Network.Title"] = ("Direktverbindungen", "Direct connections"), ["Network.Start"] = ("Netzwerk starten", "Start networking"),
            ["Network.Stop"] = ("Netzwerk stoppen", "Stop networking"), ["Network.Pair"] = ("Gegenstelle koppeln", "Pair peer"),
            ["Network.Connect"] = ("Verbinden", "Connect"), ["Network.Disconnect"] = ("Trennen", "Disconnect"),
            ["Network.Share"] = ("Profil für diesen Kontakt freigeben", "Share profile with this peer"),
            ["Network.IpNotice"] = ("Direkte Verbindungen machen eure IP-Adressen gegenseitig sichtbar.", "Direct connections reveal your IP addresses to each other."),
            ["Contacts.Favorite"] = ("Favorit", "Favorite"), ["Contacts.Notes"] = ("Private Notizen", "Private notes"),
            ["Contacts.Block"] = ("Sperren", "Block"), ["Contacts.Unblock"] = ("Sperre aufheben", "Unblock"),
            ["Theme.Title"] = ("Darstellung", "Appearance"), ["Theme.Automatic"] = ("Automatisch nach Volk", "Automatic by race"),
            ["Theme.Manual"] = ("Manuell auswählen", "Choose manually"), ["Theme.Decoration"] = ("Verzierungen reduzieren", "Reduce decoration"),
            ["Bar.Visible"] = ("RP-Leiste anzeigen", "Show RP bar"), ["Bar.Locked"] = ("Position sperren", "Lock position"),
            ["Bar.Scale"] = ("Leistengröße", "Bar scale"), ["Bar.Reset"] = ("Position zurücksetzen", "Reset position"),
            ["Language.Title"] = ("Oberflächensprache", "Interface language"),
            ["Language.Help"] = ("Nur lokale Bedienoberflächen. Profiltexte werden nicht übersetzt; noch nicht angebundene Bereiche behalten ihre bisherige Sprache.",
                "Local interface text only. Profile content is never translated; areas not yet connected keep their previous language."),
            ["Tools.Title"] = ("Lokale RP-Werkzeuge", "Local RP tools"),
            ["Tools.Help"] = ("Diese Werkzeuge bleiben lokal. Sie senden keine Spielnachrichten und stellen keine Verbindung her.",
                "These tools stay local. They never send game messages or establish a connection."),
            ["Reference.Title"] = ("Vorhandenes Profil öffnen", "Open an existing profile"),
            ["Reference.Help"] = ("Füge einen internen FRP1-Profilverweis ein. Nur bereits vorhandene und erlaubte Profile können geöffnet werden; es erfolgt kein Verbindungsaufbau.",
                "Paste an internal FRP1 profile reference. Only existing, permitted profiles can open; this never establishes a connection."),
            ["Reference.Input"] = ("Profilverweis", "Profile reference"), ["Reference.Invalid"] = ("Der Profilverweis ist ungültig.", "The profile reference is invalid."),
            ["Reference.Missing"] = ("Dieses Profil ist nicht lokal vorhanden oder derzeit nicht erlaubt.", "This profile is not available locally or is currently restricted."),
            ["Reference.Copied"] = ("Profilverweis kopiert.", "Profile reference copied."),
            ["Draft.Title"] = ("RP-Text- und NPC-Entwurf", "RP text and NPC draft"),
            ["Draft.Help"] = ("Bereite einen Text für deine Szene vor. Kopieren steuert keine NPCs und sendet nichts an FFXIV. Prüfe den Text, bevor du ihn selbst verwendest.",
                "Prepare text for your scene. Copying never controls NPCs or sends anything to FFXIV. Review the text before using it yourself."),
            ["Draft.Text"] = ("Text", "Text"), ["Draft.Template"] = ("Vorlage", "Template"),
            ["Draft.Speech"] = ("Gesprochen", "Speech"), ["Draft.Emote"] = ("Emote", "Emote"),
            ["Draft.Copied"] = ("Textentwurf kopiert; nichts gesendet.", "Text draft copied; nothing was sent."),
            ["Draft.Invalid"] = ("Entwurf zu lang oder ungültig: Name höchstens 80, Text höchstens 2048 Zeichen.", "Draft too long or invalid: name up to 80, text up to 2048 characters."),
            ["Automation.Title"] = ("Lokale Automatisierung", "Local automation"),
            ["Automation.Enable"] = ("Deklarative Regeln bewusst aktivieren", "Explicitly enable declarative rules"),
            ["Automation.Help"] = ("Standardmäßig aus. Regeln ändern nur dein eigenes Profil über die normale Profilsperre. Offene Entwürfe, laufende Vorgänge und fehlende Freigaben werden respektiert. Keine Skripte, Nachrichten oder Netzwerkfreigaben.",
                "Off by default. Rules only change your own profile through the normal profile guards. Unsaved drafts, pending operations and access restrictions are respected. No scripts, messages or networking permissions."),
            ["Automation.HideCombat"] = ("RP-Overlays während Kampf vorübergehend ausblenden", "Temporarily hide RP overlays in combat"),
            ["Automation.HideDuty"] = ("RP-Overlays während Duty vorübergehend ausblenden", "Temporarily hide RP overlays during duty"),
            ["Automation.HideHelp"] = ("Die gespeicherte Sichtbarkeit der RP-Leiste bleibt unverändert. Die Anzeige kehrt nach dem Zustand zurück.",
                "The RP bar's saved visibility stays unchanged. Its display returns after the condition ends."),
            ["Automation.Trigger"] = ("Auslöser", "Trigger"), ["Automation.Action"] = ("Eigene Aktion", "Own-profile action"),
            ["Automation.NewRule"] = ("Neue Regel", "New rule"), ["Automation.RuleName"] = ("Regelbezeichnung", "Rule name"),
            ["Automation.TerritoryFilter"] = ("Nur in einem bestimmten Gebiet", "Only in a specific territory"),
            ["Automation.Territory"] = ("Gebiets-ID", "Territory ID"),
            ["Automation.ChooseProfile"] = ("Eigenes gespeichertes Profil ausdrücklich wählen", "Explicitly select a saved own profile"),
            ["Automation.Invalid"] = ("Regel unvollständig oder ungültig. Prüfe Profilwahl, Bezeichnung und Gebiets-ID.", "Rule incomplete or invalid. Check the profile selection, name and territory ID."),
            ["Automation.RuleLimit"] = ("Bis zu 16 lokale Regeln.", "Up to 16 local rules."),
            ["Automation.Complete"] = ("Lokale Profilregel angewendet.", "Local profile rule applied."),
            ["Automation.Blocked"] = ("Regel nicht angewendet. Bitte Entwurf speichern oder verwerfen, Vorgänge abwarten und Profilfreigabe prüfen.",
                "Rule not applied. Save or discard your draft, wait for pending operations and check profile access."),
            ["Trigger.Login"] = ("Anmeldung / Charakter bereit", "Login / character ready"),
            ["Trigger.CombatEntered"] = ("Kampf beginnt", "Enter combat"), ["Trigger.CombatLeft"] = ("Kampf endet", "Leave combat"),
            ["Trigger.DutyEntered"] = ("Duty beginnt", "Enter duty"), ["Trigger.DutyLeft"] = ("Duty endet", "Leave duty"),
            ["Trigger.TerritoryChanged"] = ("Gebietswechsel", "Territory change"),
            ["Action.SetStatus"] = ("RP-Status ändern", "Change RP status"),
            ["Action.SetAvailability"] = ("Verfügbarkeit ändern", "Change availability"), ["Action.SelectProfile"] = ("Gespeichertes eigenes Profil aktivieren", "Activate a saved own profile"),
            ["Error.Load"] = ("Lokale Einstellungen konnten nicht geladen werden. Standardwerte bleiben aktiv; erneut versuchen oder bewusst ändern.",
                "Local settings could not be loaded. Defaults remain active; retry or explicitly change the settings."),
            ["Error.Save"] = ("Lokale Einstellungen konnten nicht gespeichert werden. Deine Auswahl bleibt in dieser Sitzung erhalten; bitte erneut versuchen.",
                "Local settings could not be saved. Your choice remains active for this session; please retry."),
            ["Help.FirstSteps"] = ("Bestätige die Hinweise, triff die Altersauswahl und speichere ein lokales Profil. Aktiviere das gewünschte eigene Profil ausdrücklich für den angemeldeten Charakter.",
                "Acknowledge the guidance, choose your age group and save a local profile. Explicitly activate the desired own profile for your logged-in character."),
            ["Help.Age"] = ("Die Angabe betrifft die reale Person und wird pro Sitzung erneut getroffen. Inhalte ab 18 erfordern zusätzliches Opt-in. Dies ist eine Selbstauskunft, keine Altersverifikation.",
                "This describes the real person and must be chosen again each session. Mature content also requires explicit opt-in. This is self-declaration, not age verification."),
            ["Help.Sharing"] = ("Speichern, Aktivieren, Koppeln und Teilen sind getrennte Aktionen. Weder Sprachwahl noch Regeln erteilen eine neue Profil- oder Netzwerkfreigabe.",
                "Saving, activating, pairing and sharing are separate actions. Language choices and rules never grant new profile or network permission."),
            ["Help.Privacy"] = ("Profile, Notizen und lokale Einstellungen bleiben bei dir. Direkter Austausch braucht Zustimmung und macht den Kontakten eure IP-Adressen sichtbar. Ein Profilverweis verbindet niemanden automatisch.",
                "Profiles, notes and local settings remain with you. Direct exchange requires consent and reveals IP addresses to the peers. A profile reference never connects automatically."),
        });

    public static IReadOnlyList<string> Keys { get; } = Array.AsReadOnly(Catalog.Keys.ToArray());

    public static string Get(string? key) => Get(key, CurrentLanguage);

    public static string Get(string? key, UiLanguage language)
    {
        if (string.IsNullOrWhiteSpace(key)) return language == UiLanguage.English ? "Unavailable" : "Nicht verfügbar";
        return Catalog.TryGetValue(key, out var text) ? language == UiLanguage.English ? text.En : text.De : key.Replace('.', ' ');
    }

    public static string Translate(string? germanText, UiLanguage language)
    {
        if (string.IsNullOrWhiteSpace(germanText)) return Get(null, language);
        foreach (var entry in Catalog.Values)
            if (string.Equals(entry.De, germanText, StringComparison.Ordinal)) return language == UiLanguage.English ? entry.En : entry.De;
        return germanText;
    }

    public static string Translate(string? germanText) => Translate(germanText, CurrentLanguage);

    public static string Format(string key, params object?[] arguments) => Format(key, CurrentLanguage, arguments);

    public static string Format(string key, UiLanguage language, params object?[] arguments)
    {
        var text = Get(key, language);
        try { return string.Format(language == UiLanguage.English ? CultureInfo.GetCultureInfo("en-GB") : CultureInfo.GetCultureInfo("de-DE"), text, arguments); }
        catch (FormatException) { return text; }
        catch (ArgumentNullException) { return text; }
    }
}
