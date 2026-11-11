# UX und Profilgestaltung

Stand: 2026-11-11. **Die neue UX ist ausschließlich lokal, uncommitted und unpushed.** Sie besteht den Release-Build ohne Warnungen/Fehler, 500/500 Tests und unabhängiges Review. Öffentliches ZIP/Installerindex 0.1.0.0 sind unverändert; die historischen 467 Funktionstests und 33 Paketprüfungen dieses Releases sind davon getrennt. Commit und Push übernimmt der Nutzer. Die Webvorschau ist kein Pixelversprechen oder Nachweis einer Spielabnahme.

## Referenz und vorhandene Oberfläche

TRP3 bleibt die primäre Bedienungsreferenz für individuelle lesbare Profile, Profilwechsel und schnellen Zugriff auf RP-Aktionen. Siehe [offizielle Beschreibung](https://github.com/Total-RP/Total-RP-3/blob/main/README.md) und [vollständigen FRP1-Abgleich](trp3-feature-map.md). FRP1 verwendet eigene Implementierung und keine WoW-Oberfläche oder WoW-Assets.

Die Haupttabs **Mein Profil**, **Umgebung**, **Kontakte** und **Einstellungen** bleiben der Einstieg. Die lokale Überarbeitung vereint Editor und Leser auf fünf Inhaltsbereiche, reduziert die eigene Leiste zum Kontext-MiniHUD und beschriftet feldnahe Hilfen. Register, Netzwerk, Medien und ihre Schutzgrenzen bleiben angebunden. Einige ältere Host- und Hilfetexte sind noch deutsch.

Die weitere Web-Journalgestaltung und eigene Serif-/UI-Fontintegration werden nicht als pixelgleich übernommen oder fertig abgenommen behauptet. Normale Hauptfenster behalten native Fensterbedienung; das neue MiniHUD verwendet bewusst keine Titelzeile.

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

## Fünf gleichrangige Bereiche in Editor und Leser

Beide Ansichten verwenden dieselbe Reihenfolge aus `ProfileAreas`: **RP-Status und Kurzprofil**, **OOC Informationen**, **Auf den ersten Blick**, **Weitere Charakterdaten**, **RP-Erfahrung und Absprachen**. Ein kleiner Kopf enthält Name, Titel, Portrait, Icon und Rating. Darunter gibt es einen Inhaltsbereich; die zweispaltige Vorschau entfällt. Gestaltung bleibt Kopf-Popup, JSON liegt unter Profilaktionen und wird kein sechster Hauptbereich.

Status-/Kurzprofil, OOC, Glances, weitere Beschreibungen und RP-Absprachen sind gezielt erreichbar. Neue Baukästen für Hooks, freie Zusatzfelder, Traits und Begleiter werden nicht mehr angeboten. Bereits vorhandene Inhalte bleiben unter Weitere Charakterdaten vollständig lesbar und über einen geprüften Entwurf gezielt entfernbar. Unbearbeitete Sprachen bleiben unverändert erhalten.

Vor Export und Teilen weisen sichtbare Hinweise auf vorhandene Zusatzinhalte hin. Sie bleiben bis zur tatsächlichen Entfernung öffentliche Profilinhalte. Auch deaktivierte, standardmäßig unsichtbare Glances sind nicht privat; verbergen ist keine Zugriffssperre. Private Informationen gehören weiterhin ins Register.

RP-Präferenzen beschreiben Gesprächsthemen/Spielwünsche; sie sind keine Zustimmung zu Handlungen und kein Altersnachweis. Private Notizen und Beziehungen bleiben im Register, nicht im öffentlichen OOC-Feld.

### Grafische Icons, Portrait und Rich Text

Ein grafischer Picker mit Suche/Kategorien verwendet lokale Spielressourcen; normale Nutzer müssen keine Icon-ID eingeben. Eigene Glancevorlagen dienen als bearbeitbare Ausgangspunkte, nicht als neue Datenquelle anderer Spieler.

Die Portraitwahl liest ein begrenztes lokales Bild, bietet Zuschnitt und erzeugt eine metadatenfreie PNG-Miniatur bis 256 × 256 und 12 KiB. Das gesamte übertragene Profil bleibt höchstens 48 KiB groß. Es gibt weder externes Bildhosting noch automatische URL-Abrufe.

Begrenztes Rich Text unterstützt Überschriften/Listen sowie `[b]…[/b]`, `[i]…[/i]` und `[color=#RRGGBB]…[/color]`. Inhalt bleibt geparster Text mit Größen-/Token-/Tiefegrenzen; kein HTML, Skript, eingebetteter Fremdinhalt oder Autoplay.

Die **Standarddarstellung** neutralisiert fremde Gestaltung für gute Lesbarkeit und verwendet weiterhin das eigene FRP1-Grundtheme, nicht den Standard-Dalamud-Look. Musik und fremde Schriftdateien sind keine vorhandene Funktion.

## Kontext-MiniHUD und separate Fremdanzeigen

Das neue Mein-RP-HUD verwendet zwei kompakte Zeilen ohne Titelbalken; das Quelllayout hat 280 Pixel Basisbreite vor Skalierung/Viewportbegrenzung. Das ist keine im Spiel gemessene Pixelgröße. Kopf und Icons zeigen Ich-, Ziel- oder neutralen Kontext. Ohne Ziel/bei Selbstziel erscheint das eigene Profil, ein erkanntes erlaubtes fremdes Ziel nur lesend. Unbekannte/NPC-, blockierte, stummgeschaltete, nicht erlaubte Mature-, mehrdeutige oder szenenfremde Ziele bleiben neutral ohne Own-Fallback.

Quelle ist das tatsächliche Raw-Target, nicht eine gepinnte separate Zielkarte. Zielwechsel verändert weder Entwurf, eigene aktive Zuordnung noch Sharing. Profilwahl, Einstellungen, Hilfe und bewusstes Öffnen von Details liegen im Kontextmenü. Neue Einstellungen lassen die große Zusatz-Zielkarte standardmäßig aus; bestehende gespeicherte Wahl wird beibehalten.

Einklappen hat einen eigenen lokalen `Collapsed`-Wert und einen kleinen Wiederöffner; das alte Schema 1 bleibt lesbar. Bewegungssperre, Verschieben, Skalierung und sichtbare Viewportgrenzen bleiben erhalten. Ein erstes Profil wird nicht automatisch aktiv.

Glances öffnen bei Hover oder Tastaturfokus den vollständigen ungekürzten Text in einer scrollbaren Karte – auch bei deaktivierten Hilfen. Rund 500 ms Mausübergang und optionales Anheften per Klick erlauben den Wechsel in die Karte. Kontext-/Policywechsel invalidiert den Inhalt.

Die getrennte Fremdprofilanzeige mit Mouseover, kompakter/ausführlicher Darstellung und beweglicher/pinnbarer Zielkarte verwendet weiterhin aktuell erlaubte Daten bestätigter Bindungen. Umgebungsfilter und Cache-/Kontaktaktionen gehören zum Socialmodul. Der native Kontextmenüeintrag **FRP1-Profil öffnen** für bekannte bestätigte Bindungen bleibt erhalten.

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

Kontextuelle Hilfe steht als **? Hilfe: Titel** oder kompaktes **? Hilfe** unmittelbar am Titel/Feld. Tooltip und geöffneter Hilfetext erklären das konkrete Thema; lose Fragezeichengruppen entfallen. Sechs neue Themen ergänzen den gemeinsamen Katalog. Der globale Schalter **Hilfshinweise anzeigen** betrifft diese Hilfen; **Hilfe / Erste Schritte**, eigentliche Profil-/Glance-Inhalte und verpflichtende Hinweise bleiben davon unabhängig.

Automation ist global standardmäßig aus, auf 16 deklarative Regeln beschränkt und führt keine Skripte oder Spielchatbefehle aus. Lokale Rede-/Emote-Entwürfe bieten Vorschau/Kopiertext. Interne Profilverweise öffnen nur bereits vorhandene erlaubte Daten, ohne Fremdabruf oder Chattransport. RP-Sprachen sind Profilbeschreibung, keine Manipulation der Spielsprache.

## Regeln und Alter vor jeder Darstellung

Kanonische Profilregeln erscheinen vor der getrennten Altersauswahl und bleiben später in den Einstellungen lesbar. Ihre lokale Bestätigung bleibt für die aktuelle Regelversion gespeichert; Alterswahl und zusätzliches Mature-Opt-in werden pro Pluginsitzung neu verlangt.

Allgemein gekennzeichnete Profile müssen einschließlich Name, Titel, Glances und Bild jugendfrei sein. Richtig kennzeichnen, reales Alter wahrheitsgemäß angeben; keine sexualisierten Minderjährigendarstellungen, unerlaubten privaten Daten Dritter, Belästigung oder Inhalte ohne Nutzungsrechte. Es gibt keine zentrale Sanktionierung und keine verifizierte Alterskontrolle.

Audience-Gate und Register-/Binding-/Blockzustand gelten vor Profilnamen, Bildern/Decodierung, Icon, Vorschau, Journal, Tooltip und Zielkarte. Widerruf invalidiert Ansichten und verspätete Ergebnisse. Ein Theme, eine Beziehung, eine Profilpräferenz oder eine Hilfebestätigung kann diese Freigabe nicht erteilen.

## Verbleibende Abnahme

Der lokale 500er-Teststand, die transparente Testhistorie und Reviewfreigabe stehen in [STATUS.md](../STATUS.md). Er ist noch nicht committed, gepusht oder als neues Installerpaket veröffentlicht. Ein späteres Update benötigt neue Version/Paket/Index und darf 0.1.0.0 nicht überschreiben. Im laufenden Spiel zu prüfen sind insbesondere:
1. Journal-/Icon-/Portraitwiedergabe, tatsächliche Volkzuordnung, Textumbruch, Skalierung und Tastaturfokus.
2. Eigene Leiste, Fremdmouseover/Zielpin, Nameplate-/Chatdarstellung und Rücknahme beim Deaktivieren/Entladen.
3. Alterswiderruf, Charakterwechsel, verspätete Picker-/Ladeergebnisse und blockierte/gelöschte Kontakte ohne Inhaltsoffenlegung.
4. Reale direkte Verbindung und automatische Änderungen zwischen zwei Spielern in LAN und unterschiedlichen Netzen.

Taschen-/Inventarsysteme und TRP3 Extended sind ausdrücklich vom Auftrag ausgeschlossen. WoW-/MSP-Import und Musik sind nicht implementiert. Lokale UI-Klick-/Bestätigungstöne sind eine davon getrennte, ebenfalls noch nicht implementierte Funktion. Die Funktionszuordnung steht in der [TRP3-Matrix](trp3-feature-map.md) und im [Glance-Abgleich](glance-feature-map.md).

