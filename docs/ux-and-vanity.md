# UX und Profilgestaltung

Stand: 2026-11-11. **Die folgenden Funktionen sind im nativen Plugin verbunden.** Die abschließende Codeprüfung besteht mit 467 Tests, Release ohne Warnungen/Fehler und unabhängiger Reviewfreigabe. Die Webvorschau bleibt illustrative Referenz, kein Pixelversprechen und keine Spielabnahme.

## Referenz und vorhandene Oberfläche

TRP3 bleibt die primäre Bedienungsreferenz für individuelle lesbare Profile, Profilwechsel und schnellen Zugriff auf RP-Aktionen. Siehe [offizielle Beschreibung](https://github.com/Total-RP/Total-RP-3/blob/main/README.md) und [vollständigen FRP1-Abgleich](trp3-feature-map.md). FRP1 verwendet eigene Implementierung und keine WoW-Oberfläche oder WoW-Assets.

Die Haupttabs **Mein Profil**, **Umgebung**, **Kontakte** und **Einstellungen** bleiben der Einstieg. Integriert sind erweiterter Profileditor, eigenes Journal, drei Layouts, Spielicon-Picker, Portraitzuschnitt, privates Register, Netzwerkbedienung, Mouseover, bewegliche Zielkarte, eigene RP-Leiste, globale Hilfen und lokale Werkzeuge. Die neue DE-/EN-Lokalisierung deckt die neuen Module ab; einige ältere Host- und Hilfetexte sind noch deutsch.

Die weitere große Web-Journalgestaltung und eigene Serif-/UI-Fontintegration werden nicht als pixelgleich übernommen oder fertig abgenommen behauptet. Der aktuelle native Titelrahmen mit Schließen, Ziehen und Größenänderung bleibt erhalten.

## Eigenständiges FRP1-Theme

FRP1 gestaltet seine eigenen Fensterflächen, Panels, Texte, Akzente, Fokusmarkierungen, Eingaben, Buttons, Tabs und Innenrahmen. Diese Styles umfassen auch Hinweise und Einstellungen und werden nach dem FRP1-Draw zurückgenommen; andere Plugins bleiben unberührt. Dezente Ecklinien sind über **Dekoration reduzieren** abschaltbar.

Der Hintergrund liegt innerhalb des FRP1-Fensters. Außerhalb bleibt die Spiellandschaft sichtbar; kein erzwungener Vollbildhintergrund oder Szenen-Overlay. Texturen/Icons kommen lokal, Profilmedien lokal oder nach Freigabe direkt vom Peer. Keine URL-/Fontserverabrufe oder WoW-Assets.

Unter **Einstellungen → Darstellung** ist Automatik oder freie manuelle Wahl möglich. Automatik ist ein Modus, keine zwölfte Palette. Jedes Volkstheme ist auch für andere Charaktere frei wählbar.

| Zuordnung | Palette |
|---|---|
| Hyur | Schiefer und Bronze |
| Elezen | Blaugrau und Silber |
| Lalafell | Honig und Creme |
| Miqo’te | Pflaume und Roségold |
| Roegadyn | Petrol und Kupfer |
| Au Ra | Tinte und Jade |
| Hrothgar | Anthrazit und Bernstein |
| Viera | Moos und Violett |
| Frei: Obsidian | Graphit und Silber |
| Frei: Pergament | Helles Papier und dunkler Text |
| Frei: Mondlicht | Nachtblau und Silber |

Automatik liest das Volk des lokalen Spielcharakters aus `IPlayerState.Race.RowId`, nicht aus dem frei bearbeitbaren RP-Profil. Manuelle Wahl bleibt bis zur Rückkehr zur Automatik aktiv und wird je Content-ID lokal gespeichert. Fremde Profile ändern das Empfängertheme nicht.

Ohne Charakter, während anfänglichen Ladens oder bei Ladefehler verwendet FRP1 Obsidian; eine bereits bewusst gewählte ungespeicherte Auswahl bleibt während der Sitzung erhalten und bietet Speicherwiederholung. Alte asynchrone Ergebnisse dürfen neuere Charakter-/Auswahlzustände nicht ersetzen. Dateien werden nicht still nach Fehler überschrieben.

Rechnerisch geprüfte Farben sind kein Nachweis vollständiger Spielbarrierefreiheit. Reale Schriftwiedergabe, Fokus, Skalierung, Volkzuordnung und Entladen brauchen native Abnahme.

## Profil lesen und gestalten

Das Journal zeigt erlaubte eigene und fremde Profile mit drei Layouts: Abschnitte, Chronik und Kompakt. Es verwendet die aktuelle Inhaltskennzeichnung, zulässige Text-/Bilddaten und Empfänger-Standarddarstellung. Profilkopf, Glances, Hintergrund, OOC und ergänzte RP-Felder sind keine ausschließlich geplanten Platzhalter mehr.

Öffentlich bearbeitbar sind unter anderem:
- Status/Verfügbarkeit, Kurzbeschreibung, Pronomen, Ort/FC und OOC-Informationen;
- fünf Glances, zehn Hooks und freie Felder;
- bis acht Persönlichkeitseigenschaften, beschriebene RP-Sprachen und Gesprächspräferenzen;
- bis acht Begleiterprofile mit Name, Beschreibung und Icon;
- Darstellungswünsche, Spielicon und optionales normalisiertes Portrait.

RP-Präferenzen beschreiben Gesprächsthemen/Spielwünsche; sie sind keine Zustimmung zu Handlungen und kein Altersnachweis. Private Notizen und Beziehungen bleiben im Register, nicht im öffentlichen OOC-Feld.

### Grafische Icons, Portrait und Rich Text

Ein grafischer Picker mit Suche/Kategorien verwendet lokale Spielressourcen; normale Nutzer müssen keine Icon-ID eingeben. Eigene Glancevorlagen dienen als bearbeitbare Ausgangspunkte, nicht als neue Datenquelle anderer Spieler.

Die Portraitwahl liest ein begrenztes lokales Bild, bietet Zuschnitt und erzeugt eine metadatenfreie PNG-Miniatur bis 256 × 256 und 12 KiB. Das gesamte übertragene Profil bleibt höchstens 48 KiB groß. Es gibt weder externes Bildhosting noch automatische URL-Abrufe.

Begrenztes Rich Text unterstützt Überschriften/Listen sowie `[b]…[/b]`, `[i]…[/i]` und `[color=#RRGGBB]…[/color]`. Inhalt bleibt geparster Text mit Größen-/Token-/Tiefegrenzen; kein HTML, Skript, eingebetteter Fremdinhalt oder Autoplay.

Die **Standarddarstellung** neutralisiert fremde Gestaltung für gute Lesbarkeit und verwendet weiterhin das eigene FRP1-Grundtheme, nicht den Standard-Dalamud-Look. Musik und fremde Schriftdateien sind keine vorhandene Funktion.

## Eigene Leiste und Fremdanzeigen

Die bewegliche eigene RP-Leiste bietet ausdrücklich gewähltes aktives Profil, IC/OOC/AFK, Verfügbarkeit, Profilöffnung und fünf eigene Glances per Klick. Position, Sichtbarkeit, Bewegungssperre und Skalierung von 0,75 bis 1,75 sind lokal gespeichert; Logout verbirgt sie nur vorübergehend. Ein erstes Profil wird nicht automatisch aktiv.

Die davon getrennte **Fremdprofilanzeige ist inzwischen implementiert**: Mouseover, kompakte/ausführliche Darstellung und bewegliche/pinnbare Zielkarte verwenden aktuell erlaubte Daten bestätigter Bindungen. Umgebungsfilter für Status, Verfügbarkeit und leere Profile sowie Cache-/Kontaktaktionen gehören zum Socialmodul. Für bekannte bestätigte Charakterbindungen gibt es außerdem den nativen Kontextmenüeintrag **FRP1-Profil öffnen**; kein globaler Umbau der Spielmenüs.

Lokale Nameplates/Titel/Farben und optionale kleine Profilicons sind gesondert einschaltbar. Questmarker bleiben unverändert. Die Kartenaktion bezieht sich auf einen bekannten aktuell lokalen Akteur, nicht auf eine globale Spielersuche.

Chatnamensersetzung verwendet ausschließlich General-Profile, weil einmal ausgegebener Chat nicht verlässlich zurückgenommen werden kann. Mature-Namen bleiben auch mit Erwachsenenfreigabe aus dem bleibenden Chat ausgeschlossen.

## Speicherzustände und Freigaben

Ein gemeinsamer eigener Profilschreiber prüft erwartete Revision/Hash und serialisiert Editor-, Leisten- und Automationsänderungen. Bei verändertem/beschäftigtem Editor verhindern Sperren konkurrierende Leistenänderungen; unveränderter Status erzeugt keine unnötige Speicherung.

Speichern erteilt keine neue Peerfreigabe. Änderungen am bereits aktiven und für einen Kontakt freigegebenen Profil synchronisieren weiterhin automatisch. Import erstellt eine neue lokale Kopie. Spät eintreffende Iconpicker-/Medienergebnisse werden bei Savebeginn beziehungsweise Kontextwechsel ungültig; sie dürfen keinen anderen Entwurf verändern.

Eine vollständig neu gestaltete einheitliche Dreiwege-Navigation wird nicht allein aus diesen Sperren abgeleitet. Maßgeblich ist der vorhandene native Ablauf, nicht jede frühere Webmockup-Interaktion.

## Netzwerk und privates Register

Die Oberfläche kann Netzwerk explizit starten/stoppen, Einladungen erstellen/einfügen, Peers koppeln, verbinden/trennen und Profile gesondert freigeben. Dauerhafte DPAPI-Identität und private Kopplungen ersetzen die frühere flüchtige Demoidentität. Optionaler Listenport und numerische Einladungsadresse konfigurieren keine Router/Firewall und fragen keine öffentliche IP ab.

Kontakte bieten private Profil-/Charakternotizen, Favoriten, Beziehungen, Mute und Netzblock sowie bestätigte fremde Charakterbindung. Suche und Cacheverwaltung sind angebunden. Der Besuchsverlauf erfasst tatsächlich geöffnete Profile; keine automatische Aufzeichnung aller vorbeilaufenden Figuren. Cachelöschen ist kein Netzblock: weiterhin erlaubte Gegenstellen können das Profil erneut liefern.

## Globale Hilfen und lokale Werkzeuge

Kontextuelle Hilfe über Mauszeiger, Tastaturfokus und Fragezeichen stammt aus einem gemeinsamen Katalog. Der globale Schalter **Hilfshinweise anzeigen** betrifft diese Hilfen; **Hilfe / Erste Schritte**, eigentliche Profil-/Glance-Inhalte und verpflichtende Hinweise bleiben davon unabhängig.

Automation ist global standardmäßig aus, auf 16 deklarative Regeln beschränkt und führt keine Skripte oder Spielchatbefehle aus. Lokale Rede-/Emote-Entwürfe bieten Vorschau/Kopiertext. Interne Profilverweise öffnen nur bereits vorhandene erlaubte Daten, ohne Fremdabruf oder Chattransport. RP-Sprachen sind Profilbeschreibung, keine Manipulation der Spielsprache.

## Regeln und Alter vor jeder Darstellung

Kanonische Profilregeln erscheinen vor der getrennten Altersauswahl und bleiben später in den Einstellungen lesbar. Ihre lokale Bestätigung bleibt für die aktuelle Regelversion gespeichert; Alterswahl und zusätzliches Mature-Opt-in werden pro Pluginsitzung neu verlangt.

Allgemein gekennzeichnete Profile müssen einschließlich Name, Titel, Glances und Bild jugendfrei sein. Richtig kennzeichnen, reales Alter wahrheitsgemäß angeben; keine sexualisierten Minderjährigendarstellungen, unerlaubten privaten Daten Dritter, Belästigung oder Inhalte ohne Nutzungsrechte. Es gibt keine zentrale Sanktionierung und keine verifizierte Alterskontrolle.

Audience-Gate und Register-/Binding-/Blockzustand gelten vor Profilnamen, Bildern/Decodierung, Icon, Vorschau, Journal, Tooltip und Zielkarte. Widerruf invalidiert Ansichten und verspätete Ergebnisse. Ein Theme, eine Beziehung, eine Profilpräferenz oder eine Hilfebestätigung kann diese Freigabe nicht erteilen.

## Verbleibende Abnahme

Der nach den Anzeige-/Performancekorrekturen geprüfte Stand und die Reviewfreigabe sind in [STATUS.md](../STATUS.md) festgehalten; ein gemessener FPS-Gewinn wird nicht behauptet. Im laufenden Spiel zu prüfen sind insbesondere:
1. Journal-/Icon-/Portraitwiedergabe, tatsächliche Volkzuordnung, Textumbruch, Skalierung und Tastaturfokus.
2. Eigene Leiste, Fremdmouseover/Zielpin, Nameplate-/Chatdarstellung und Rücknahme beim Deaktivieren/Entladen.
3. Alterswiderruf, Charakterwechsel, verspätete Picker-/Ladeergebnisse und blockierte/gelöschte Kontakte ohne Inhaltsoffenlegung.
4. Reale direkte Verbindung und automatische Änderungen zwischen zwei Spielern in LAN und unterschiedlichen Netzen.

Taschen-/Inventarsysteme und TRP3 Extended sind ausdrücklich vom Auftrag ausgeschlossen. WoW-/MSP-Import und Musik sind nicht implementiert. Lokale UI-Klick-/Bestätigungstöne sind eine davon getrennte, ebenfalls noch nicht implementierte Funktion. Die Funktionszuordnung steht in der [TRP3-Matrix](trp3-feature-map.md) und im [Glance-Abgleich](glance-feature-map.md).

