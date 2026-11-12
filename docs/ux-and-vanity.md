# UX und Profilgestaltung

Stand: 2026-11-12. **Die neue Oberfläche ist als 0.2.0.4 veröffentlicht.** [Hosted-Lauf 43296113100](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43296113100) besteht Build und Publish: 0 Warnungen/Fehler, 510/510 Funktionstests, 33/33 Paketprüfungen und alle 105/105 CI-Prüfungen ausdrücklich belegt. Lokal bestanden zuvor 42 gezielte und 510/510 vollständige Tests sowie unabhängiges Review. Öffentliche JSON-/ZIP-/Prüfsummendownloads sind validiert. Native Spiel-/Skalierungsabnahme bleibt offen.

## Aktuelle Gestaltungsvorgaben

Der Nutzer hat die früheren Vorgaben für fünf Profilbereiche, zielabhängiges eigenes HUD und elf sichtbare Volksthemes ausdrücklich durch freie Neugestaltung mit möglichst kompakter Bedienung ersetzt. Maßgeblich ist die folgende Oberfläche. Frühere Beschreibungen in der Veröffentlichungshistorie sind keine weiter geltenden Umsetzungsvorgaben.

Die Hauptnavigation hat drei Einstiege: **Mein Charakter**, **Kontakte**, **Einstellungen**. Ein kleineres Hauptfenster mit neuer Fensterkennung beginnt im Quelllayout bei 560 × 430 Pixeln und bleibt an den verfügbaren Viewport begrenzt. Das sind Layoutwerte vor Skalierung, keine im Spiel gemessenen Abmessungen. Ruhige Flächen, schmale Abstände und bedarfsweise sichtbare Inhalte reduzieren die ständig belegte Fläche.

## Mein Charakter: lesen, dann gezielt bearbeiten

Die eigene Profilkarte öffnet standardmäßig lesend. **Bearbeiten** wechselt in einen getrennten Entwurf mit **Speichern**, **Verwerfen** und **Vorschau**. Die eigene Lesekarte und das Journal für geöffnete Profile haben getrennten Zustand; ein fremdes geöffnetes Profil darf den eigenen Leser nicht ersetzen.

Editor und Leser verwenden drei Bereiche:

| Bereich | Inhalt |
|---|---|
| Überblick | Profilkopf, Status, Kurzbeschreibung und erste Eindrücke. |
| Charakter | Weitere Charakterangaben und bereits vorhandene zusätzliche Inhalte. |
| OOC | Bewusst öffentliche Spielerinformationen, RP-Erfahrung und Absprachen. |

Optionale Angaben erscheinen, wenn sie befüllt sind, oder werden über **+ Angabe** ergänzt. Profilvarianten, JSON-Import/-Export und lokale Werkzeuge liegen im Menü. Bestehende Hooks, freie Zusatzfelder, Traits, Begleiter und Sprachen werden durch die neue Anordnung nicht gelöscht. Bereits vorhandene Zusatzinhalte bleiben lesbar und bewusst entfernbar; Entwurf-/Revision-/Hashschutz bleibt erhalten.

Der kompakte Leser verwendet ein 40-Pixel-Portrait im Kopf und kleine horizontal angeordnete Glance-Icons. Hover, Tastaturfokus oder angeheftete Klickkarte liefern den vollständigen Text mit Scrollmöglichkeit. Eigentümer-, Kontext- und aktuelle Freigabeprüfungen verwerfen überholte Inhalte. Ausblenden oder Einklappen macht Profilinhalte nicht privat; private Informationen gehören ins Register.

## Eigene RP-Leiste und fremde Informationen

Die eigene RP-Leiste zeigt immer das eigene aktive Profil, unabhängig vom aktuellen Ziel. Sie ist im Quelllayout einzeilig mit 210 Pixeln Basisbreite; eingeklappt bleibt ein 32-Pixel-Wiederöffner. Bewegungssperre, Skalierung, Viewportbegrenzung und rückwärtslesbarer lokaler Klappzustand bleiben erhalten. Ein erstes Profil wird nicht automatisch aktiviert.

Die gespeicherte Sichtbarkeit ist weiterhin maßgeblich. Ein vorübergehend geschlossenes Fenster darf sie nicht wieder ausschalten; der Fix aus 0.1.1.3 bleibt erhalten. Zielwechsel verändert weder die eigenen Leisteninhalte noch Entwurf, aktive Zuordnung oder Sharing.

Fremde Angaben erscheinen getrennt in kompakter Ziel-/Mouseoveranzeige. Vollständige Glances sind dort bei Bedarf zugänglich. Ausschließlich aktuell erlaubte Daten bestätigter Bindungen dürfen erscheinen; Audience-, Block-, Mute-, Szene- und Zuordnungsprüfungen bleiben erhalten. Die getrennte große Zusatz-Zielkarte ist bei neuen Einstellungen zunächst aus; vorhandene Entscheidungen bleiben erhalten.

## Drei primäre Themes

Die normale Auswahl bietet **Obsidian**, **Pergament** und **Nachtblau**. Die bisherigen elf Theme-IDs bleiben lesbar; es findet keine Datenmigration statt. Die internen Volkzuordnungen bleiben für gespeicherte Einstellungen kompatibel, verwenden jedoch eine gemeinsame ruhige Standardpalette. Der bisherige Schlüssel `mondlicht` bezeichnet nun das sichtbare Theme Nachtblau.

Themes gestalten ausschließlich FRP1-Flächen. Styles werden nach dem Zeichnen zurückgenommen und verändern keine anderen Plugins. Fremde Profile bestimmen das lokale Empfängertheme nicht. Standarddarstellung neutralisiert fremde Gestaltung für Lesbarkeit. Keine externen Fonts, URL-Bilder oder WoW-Assets werden geladen.

## Kontakte, Umgebung und Verbindung

**Kontakte** verbindet die Liste mit Aktionen für den ausgewählten Eintrag. Die Umgebung ist dort untergeordnet erreichbar. Private Notizen, Beziehungen, Favoriten, Mute, Netzblock, bestätigte Charakterbindung, Cacheverwaltung und Verlauf geöffneter Profile bleiben vorhanden. Vorbeilaufende Figuren werden nicht automatisch protokolliert.

Technische Netzwerkdetails stehen unter **Erweitert**. Start/Stop, Einladung, Kopplung, Vertrauen und Profilfreigabe bleiben ausdrücklich getrennte Entscheidungen. Dauerhafte DPAPI-Identität, private Registerdaten und bestehende Kopplungen werden weiterverwendet. Keine Registrierung, Cloud, zentrale Profildatenbank, Relay-, STUN- oder Discoverydienste kommen hinzu. GitHub verteilt ausschließlich Software und Installerindex.

## Hilfe und lokale Werkzeuge

Dauerhaft sichtbare Fragezeichen-/Hilfebadges entfallen. Kontextuelle Hover-/Fokushinweise erläutern die jeweilige Bedienung; die zentrale Hilfe bleibt erreichbar. Abschaltbare Bedienhilfen und eigentliche Profil-/Glance-Inhalte sind getrennt. Verpflichtende Profilregeln und Alterswahl bleiben unabhängig davon bestehen.

Werkzeuge liegen im Menü. Automationen bleiben standardmäßig aus und auf höchstens 16 deklarative Regeln begrenzt; keine Skripte oder Spielchatbefehle. Rede-/Emote-Werkzeuge erzeugen lokale Textentwürfe. Interne Verweise öffnen nur vorhandene erlaubte Daten, ohne Fremdabruf.

## Daten- und Schutzgrenzen

Das öffentliche Profilschema und lokale Speicherformate bleiben erhalten. Speichern, Aktivieren und Teilen sind getrennt; Speichern erteilt keine neue Peerfreigabe. Bereits aktive freigegebene Änderungen können weiterhin synchronisieren. Der gemeinsame eigene Profilschreiber serialisiert konkurrierende Änderungen und prüft Revision/Hash.

Profilregeln gelten unverändert: allgemein gekennzeichnete Inhalte einschließlich Name, Glances und Bildern müssen jugendfrei sein. Aktive Alterswahl pro Sitzung und zusätzliches Mature-Opt-in bleiben notwendig; Selbstauskunft ist keine Altersverifikation. Widerruf sperrt Anzeigen und verspätete Ergebnisse. Themes, Beziehungen und Hilfebestätigungen erteilen keine Freigabe.

Private Notizen, Content-IDs, Zuordnungen und Einstellungen bleiben lokal. Profilbilder werden weiterhin zu begrenzten metadatenfreien PNG-Miniaturen verarbeitet. Chatnamensersetzung verwendet ausschließlich General-Profile; Nameplates und Karte ändern keine Spielpakete oder Questmarker.

## Noch offene Abnahme

510 bestandene Funktionstests ersetzen keine Bedienprüfung im Spiel. Offen bleiben Darstellung bei verschiedenen Skalierungen und Viewports, Fokus und Tastaturbedienung, Leser-/Editorwechsel, Glance-Karten, Ein-/Ausklappen und Entladen. Reale Zwei-Spieler-/LAN-/Internet-/NAT-Abnahme bleibt ebenfalls offen. Bestätigte Build-/Review-/Releaseergebnisse stehen in [STATUS.md](../STATUS.md).

Taschen-/Inventarsysteme, WoW-/MSP-Import und Musik sind nicht implementiert. Eigene TRP3-orientierte Bedienung bedeutet keine Übernahme fremden Codes oder fremder Assets. Weitere Zuordnung: [TRP3-Matrix](trp3-feature-map.md), [Glance-Abgleich](glance-feature-map.md).
