using System.Collections.ObjectModel;
using FRP1.Plugin.Localization;

namespace FRP1.Plugin.UiProfiles;

// UI-only strings. Never pass profile text or user-defined field names through this catalog.
internal static class ProfileUiText
{
    private static readonly IReadOnlyDictionary<string, string> English = new ReadOnlyDictionary<string, string>(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Die Profilbearbeitung ist für die aktuelle Freigabe gesperrt."] = "Profile editing is restricted by the current access settings.",
            ["Weitere Charakterdaten"] = "Additional character details",
            ["RP-Status und Kurzprofil"] = "RP status and summary",
            ["OOC Informationen"] = "OOC information",
            ["Portrait und Profilgestaltung"] = "Portrait and profile appearance",
            ["Gesamtes Profil: ab 18"] = "Entire profile: adults only",
            ["Gesamtes Profil: allgemein / jugendfrei"] = "Entire profile: general audiences",
            ["Vorhandene zusätzliche Inhalte"] = "Existing additional contents",
            ["Vorhandene zusätzliche Inhalte: "] = "Existing additional contents: ",
            [" – unter Weitere Charakterdaten vollständig lesbar."] = " – available in full under Additional character details.",
            ["Keine OOC-Informationen."] = "No OOC information.",
            ["Aktueller Ort"] = "Current location",
            ["Schließen"] = "Close",
            ["Inhalt"] = "Contents", ["Persönlichkeitsmerkmal"] = "Personality trait",
            ["Augenfarbe"] = "Eye color", ["Größe"] = "Height", ["Gewicht"] = "Weight",
            ["Geburtsort"] = "Birthplace", ["Wohnort"] = "Residence",
            ["RP-Sprachen: eine frei beschriebene Sprache je Zeile, höchstens zwölf."] = "RP languages: enter one freely described language per line, up to twelve.",
            ["Persönlichkeitsmerkmale"] = "Personality traits",
            ["Linker Pol"] = "Left trait", ["Rechter Pol"] = "Right trait", ["Gewichtung"] = "Balance",
            ["Merkmal entfernen"] = "Remove trait", ["Merkmal hinzufügen"] = "Add trait",
            ["RP-Erfahrung und Absprachen"] = "RP experience and preferences",
            ["Diese Angaben sind Gesprächshinweise. Sie erteilen keine Einwilligung und keine Altersfreigabe."] = "These details are conversation guidance. They do not grant consent or mature-content access.",
            ["RP-Erfahrung"] = "RP experience", ["Bevorzugter RP-Stil"] = "Preferred RP style",
            ["Verletzungen"] = "Injuries", ["Charaktertod"] = "Character death", ["Romantik"] = "Romance",
            ["Kriminalität"] = "Crime", ["Kontrollverlust"] = "Loss of control",
            ["Öffentliche Profilgestaltung"] = "Public profile appearance",
            ["Diese Gestaltung wird mit deinem Profil geteilt. Das persönliche FRP1-Fensterdesign bleibt davon getrennt."] = "This appearance is shared with your profile. Your personal FRP1 window theme remains separate.",
            ["Profilicon auswählen"] = "Choose profile icon", ["Namensfarbe"] = "Name color", ["Akzentfarbe"] = "Accent color",
            ["Profilvorlage"] = "Profile layout",
            ["Textgestaltung: # Überschrift, - Liste, [b]fett[/b], [i]kursiv[/i] und [color=#RRGGBB]Farbe[/color]. Unbekannte Markierungen bleiben Text."] = "Text formatting: # heading, - list, [b]bold[/b], [i]italic[/i] and [color=#RRGGBB]color[/color]. Unknown markup stays plain text.",
            ["Begleiterprofile"] = "Companion profiles",
            ["Dies sind selbst beschriebene Begleiter. Eine automatische Zuordnung zu einem Spielobjekt wird nicht behauptet."] = "These are self-described companions. They are not automatically linked to game objects.",
            ["Name"] = "Name", ["Art / Volk"] = "Species / race", ["Beschreibung"] = "Description", ["Begleitertyp"] = "Companion type",
            ["Begleitericon auswählen"] = "Choose companion icon", ["Begleiter entfernen"] = "Remove companion", ["Begleiter hinzufügen"] = "Add companion",
            ["Portrait: lokales PNG oder JPEG wählen, quadratischen Ausschnitt bestätigen. Das geteilte Vorschaubild enthält keine Bildmetadaten."] = "Portrait: choose a local PNG or JPEG and confirm a square crop. The shared thumbnail contains no image metadata.",
            ["Portraitdatei auswählen"] = "Choose portrait file", ["Lokales Portrait auswählen"] = "Choose local portrait",
            ["Bilder"] = "Images", ["Portrait entfernen"] = "Remove portrait",
            ["Ausschnitt horizontal"] = "Horizontal crop position", ["Ausschnitt vertikal"] = "Vertical crop position", ["Ausschnitt vergrößern"] = "Crop zoom",
            ["Ausschnitt als Vorschau aktualisieren"] = "Update crop preview", ["Portrait übernehmen"] = "Apply portrait",
            ["Bitte die geänderte Vorschau zuerst aktualisieren."] = "Update the changed preview first.",
            ["Das Vorschaubild wird lokal vorbereitet …"] = "Preparing the thumbnail locally …",
            ["Vorschau bereit. Übernehmen speichert sie zunächst nur im Entwurf."] = "Preview ready. Applying it only updates the draft until you save.",
            ["Das Bild konnte nicht verwendet werden. Bitte ein gültiges lokales PNG/JPEG innerhalb der Grenzen wählen."] = "This image could not be used. Choose a valid local PNG or JPEG within the limits.",
            ["Standardfarbe"] = "Default color", ["Ja"] = "Yes", ["Nein"] = "No", ["Nur nach Absprache"] = "Ask first", ["Keine Angabe"] = "Not specified",
            ["Neu im RP"] = "New to RP", ["Erfahren"] = "Experienced", ["Ansprechpartner"] = "RP guide",
            ["Chronik"] = "Chronicle", ["Kompakt"] = "Compact", ["Abschnitte"] = "Sections",
            ["Reittier"] = "Mount", ["Sonstiger Begleiter"] = "Other companion", ["Haustier / Gefährte"] = "Pet / companion",
            ["Offen für RP"] = "Open for RP", ["Beschäftigt"] = "Busy", ["Kein RP"] = "Unavailable for RP",
            ["Vorname"] = "First name", ["Nachname"] = "Last name", ["Volk"] = "Race", ["Clan"] = "Clan",
            ["Charakteralter"] = "Character age", ["Geschlecht"] = "Gender", ["Herkunft"] = "Origin", ["Beruf"] = "Occupation",
            ["Freie Gesellschaft"] = "Free company", ["Aussehen"] = "Appearance", ["Persönlichkeit"] = "Personality", ["Hintergrund"] = "Background",
            ["Die lokale Symbolauswahl konnte nicht geladen werden."] = "The local icon picker could not be loaded.",
            ["Dieses Profil ist derzeit nicht verfügbar."] = "This profile is currently unavailable.",
            ["Standarddarstellung verwenden"] = "Use standard appearance", ["Ohne Portrait"] = "No portrait",
            ["Charakter"] = "Character", ["RP-Sprachen: "] = "RP languages: ", ["Über mich"] = "About",
            ["RP-Anknüpfungspunkte"] = "RP hooks", ["Auf den ersten Blick"] = "At first glance", ["Keine aktiven Einträge."] = "No active entries.",
            ["Erfahrung: "] = "Experience: ",
            ["Die folgenden Angaben sind Gesprächshinweise und ersetzen keine Absprache oder Einwilligung."] = "The following details are conversation guidance and do not replace discussion or consent.",
            ["Begleiter"] = "Companions", ["Keine Begleiterprofile."] = "No companion profiles.",
            ["Symbole suchen"] = "Search icons", ["Kategorie"] = "Category", ["Alle"] = "All",
            ["Kampf und Magie"] = "Combat and magic", ["Handwerk"] = "Crafting", ["Sammeln"] = "Gathering",
            ["Kein Symbol"] = "No icon", ["Auswählen"] = "Select", ["Zurück"] = "Previous", ["Weiter"] = "Next",
            ["Charakterprofil"] = "Character profile", ["Symbol auswählen"] = "Choose icon",
        });

    public static string Get(string text) => UiText.CurrentLanguage == UiLanguage.English && English.TryGetValue(text, out var translated)
        ? translated : text;

    public static string Id(string originalLabel)
    {
        var hidden = originalLabel.IndexOf("##", StringComparison.Ordinal);
        var visible = hidden < 0 ? originalLabel : originalLabel[..hidden];
        // ### preserves the original ID across languages, including any previous ## suffix.
        return Get(visible) + "###" + originalLabel;
    }
}
