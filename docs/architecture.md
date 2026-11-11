# Architektur

Stand: 2026-11-11. Native Module und Direkttransport sind verbunden; der vollständige Release-Build, 467 Tests und das unabhängige Review sind erfolgreich abgeschlossen. Dies ist keine Spiel-/Netzabnahme.

## Bausteine

| Baustein | Verantwortung |
|---|---|
| `Core/Profiles` | Öffentliches Schema, Pflichtmetadaten, Validierung, kanonischer Export/Hash, erweiterte Profile und Portraitvalidierung. |
| `Core/Safety` | Gemeinsame aktuelle Alters-/Inhaltspolicy. |
| `Core/Identity` | Dauerhafte, an Windows-DPAPI CurrentUser gebundene private Identität. |
| `Core/Networking` | Einladungen, lokaler Peer-Trust, gegenseitiges TLS-Pinning, Freigaben und Synchronisierung. |
| `Core/Persistence` | Lokale Profile, begrenzter Offline-Cache, Verwaltung und Versionskonflikte. |
| `Plugin/Networking` | Tatsächlich angebundener Netzwerklebenszyklus und empfangene Profilbenachrichtigungen. |
| `Plugin/RpBar` | Gemeinsamer Schreiber eigener Profile, private aktive Zuordnung, Leistenlayout. |
| `Plugin/Contacts`, `Presentation` | Privates Register und zentraler Resolver für aktuell erlaubte, bestätigte Profile. |
| `UiProfiles`, `Profiles` | Journal, Layouts, Rich Text, Spielicon-Picker, lokaler Portraitzuschnitt und Texturen. |
| `UiSocial` | Netzwerkoberfläche, Kontakte/Cache, Umgebung, Mouseover und bewegliche/pinnbare Zielkarte. |
| `GameIntegration` | Lokale Nameplates/Titel/Farben/optionale Profilicons, General-Chatnamen und Kartenaktion. |
| `Automation`, `UiTools`, `Utility` | Deklarative Regeln, lokale Textentwürfe, interne vorhandene Profilverweise und robuste lokale Optionen. |
| `Theming`, `Localization`, Hilfe | Elf native Paletten, lokale Sprachauswahl und gemeinsame kontextuelle Hilfe. |

Asynchrone Ergebnisse werden im UI-Lebenszyklus übernommen; Dateiarbeit und Netzwerk blockieren den Game-Thread nicht. Style-Scope und Hooks gelten nur für FRP1 und werden beim Entladen abgemeldet/zurückgenommen.

Die vorbereitete GitHub-Veröffentlichung ergänzt ausschließlich die Softwareverteilung: öffentlicher `repo.json`-Installerindex und Release-ZIP für Dalamud. GitHub erhält dadurch keine FRP1-Profile oder privaten Registerdaten. Für Installation/Benutzung ist kein GitHub-Konto erforderlich; ein Softwaredownloadserver ist kein Profilbackend. Stand der tatsächlichen Veröffentlichung: [STATUS.md](../STATUS.md).

## Öffentliche und private Daten

Das öffentliche Profil besitzt zufällige GUID, Revision, Inhaltskennzeichnung und begrenzte Texte/Listen. Es umfasst Status/Verfügbarkeit, Glances/Hooks, freie Felder, Persönlichkeitseigenschaften, Gesprächspräferenzen, beschriebene RP-Sprachen, Begleiter, Darstellungswünsche und optionales normalisiertes Portrait. Gesprächspräferenzen sind keine Zustimmung zu realen oder RP-Handlungen und kein Altersnachweis.

Eigene Content-ID, aktive lokale Zuordnung, fremde bestätigte Charakterbindung, Notizen, Beziehungen, Favoriten, Mutes, Netzblock, Besuchsverlauf und Optionen bleiben privat. Ein Peer-Fingerprint beweist Schlüsselbesitz, nicht FFXIV-Charakterbesitz. Fremde Zuordnung Name/Homeworld→Peer/Profil muss ausdrücklich bestätigt werden.

Der Host protokolliert aktuell geöffnete Profile als `ProfileOpened`. Ein `Seen`-Datenmodell bedeutet keine automatische Protokollierung aller sichtbaren Akteure.

## Grenzen und Medien

- Öffentliches Gesamtprofil: höchstens 48 KiB UTF-8; TLS-Frame höchstens 64 KiB.
- Fünf Glances, zehn Hooks, acht Traits, zwölf RP-Sprachen und acht Begleiter; genaue Feldgrenzen stehen in `ProfileJson`.
- Portrait: ausschließlich validiertes metadatenfreies PNG, höchstens 256 × 256 und 12 KiB, im begrenzten Profil eingebettet.
- Lokale Bildquelle: begrenztes Lesen/Decodieren und Zuschnitt; keine URL-Abrufe. Externe EXIF-/Textmetadaten werden nicht weitergegeben.
- Rich Text: begrenzte Überschriften/Listen und Inline-Markierungen für Fett, Kursiv und Farben; Token-/Tiefe-/Textgrenzen, kein HTML oder ausführbarer Inhalt.
- Cache: maximal 16 Peers mit je 32 Profilen; Transport höchstens vier Verbindungen und 16 gekoppelte Peers.
- Register: begrenzte Kontakte, Charakter-/Profilnotizen, Netzblöcke und gespeicherte Profilbesuche.
- Automation: höchstens 16 deklarative Regeln, global standardmäßig ausgeschaltet, keine Skriptausführung.

Unbekannte/doppelte JSON-Felder, fehlende Pflichtdaten, ungültige Enumwerte und übergroße Inhalte werden abgewiesen. Hash-/Versionsprüfung verhindert Rückschritte und widersprüchlichen Inhalt bei gleicher Revision. Bei Änderungen wird ein vollständiger begrenzter Snapshot übertragen, keine Felddelta-Synchronisierung.

## Identität und tatsächlicher UI-Direktaustausch

`PeerIdentityStore` speichert den privaten Schlüssel mit Windows-DPAPI im aktuellen Benutzerkontext. `PeerTrustStore` bindet Kopplungen an die lokale Identität. Neustart erfordert damit keine grundsätzlich neue Identität/Kopplung mehr. Fehler werden nicht durch stillen Schlüsselersatz oder ungeprüftes Vertrauen umgangen.

Der Nutzer startet Netzwerkzugriff ausdrücklich, bestätigt direkte IP-Sichtbarkeit, wählt eine lokale Schnittstelle und tauscht Einladungen mit der Gegenstelle aus. Beide pinnen die öffentlichen Fingerprints; TLS 1.2/1.3 authentifiziert beide Richtungen. Profilfreigabe ist eine zusätzliche lokale Entscheidung je Peer. NetworkSession übernimmt aktive freigegebene Profile und empfangene Snapshots in den tatsächlichen Pluginablauf.

Ein fester lokaler Listenport und eine abweichende numerische Einladungsadresse sind ausdrückliche Optionen. Sie ermitteln keine öffentliche IP und konfigurieren weder Router, Firewall noch DNS. NAT-/CGNAT-Erreichbarkeit wird nicht zugesichert.

## Anzeige- und Altersgrenzen

Versionierte Profilregeln stehen vor Profilzugriff; jede Pluginsitzung verlangt danach erneut aktive Alterswahl. Unbekannt/unter 18 sowie Erwachsene ohne zusätzliches Opt-in erhalten keine Mature-Inhalte. Die eigene Policy und aktuelle Peerempfangsfreigabe werden getrennt geprüft; jede neue Verbindung beginnt ohne frühere Remotezustimmung.

Der Resolver prüft Altersfreigabe, Binding, Mute/Block und zulässigen Cachezustand vor Namen, Medien und Profilansichten. Widerruf schließt Verbindungen und invalidiert Ansichten, Texturen sowie verspätete Ergebnisse. Picker-Callbacks werden bereits bei Savebeginn ungültig, damit sie nicht auf einen anderen Entwurf wirken.

Nameplates, Mouseover und Zielkarte sind widerrufbare lokale Anzeigen. Der aktuelle Anzeige-/Umgebungspfad nutzt unveränderliche, unmittelbar gegatete Metadatenprojektionen und verarbeitet nicht für jede Namensanzeige erneut vollständige Profile/Portraits. Chatnachrichten lassen sich nach Ausgabe nicht zuverlässig entfernen: deshalb sind dort ausschließlich Namen aus General-Profilen zulässig, unabhängig von Adult-Opt-in. Mature-Chatersetzung ist gesperrt.

Netzblock unterbindet Austausch; Mute unterdrückt Darstellung. Cachelöschen entkoppelt keinen Peer und beendet keine Freigabe. Eine bekannte Karte/Umgebung verwendet aktuelle lokale Actors/Szene, keine globale Discovery oder Standortveröffentlichung. Optionale Nameplate-Profilicons verändern nur das kleine Namensicon; Questmarker bleiben unangetastet.

## Private Konfiguration

| Datei/Bereich | Inhalt |
|---|---|
| `profiles/`, `cache/` | Lokale öffentliche Profile und empfangene Snapshots. |
| `peer-identity.dpapi`, `peer-trust.json` | Geschützte Identität und private Kopplung/Freigabe/Blockzustände. |
| `contacts.json` | Notizen, Beziehungen, bestätigte Bindungen und Profilbesuchsverlauf. |
| `usage-notices.json`, `audience.json` | Bestätigte Regelversion beziehungsweise lokale Alterspräferenzen. Die Regelbestätigung bleibt versionsgebunden gespeichert; daraus wird keine Altersfreigabe übernommen. |
| `rp-bar.json`, `themes/<ContentId:X16>.json` | Private aktive Zuordnung, Leiste, globale Hilfen und charakterbezogenes Theme. Der Dateiname ist symbolisch, keine reale ID. |
| `social-ui.json`, `game-presentation.json` | Mouseover/Ziel/Filter/Port und explizite lokale Spielanzeigen. |
| `automation.json`, `language.json` | Deklarative lokale Regeln und Oberflächensprache. |

Die Regeldatei enthält nur die aktuelle bestätigte Version (höchstens 128 Bytes); materielle Änderungen erhöhen die Version. Themes verwenden strikt vier Pflichtfelder und höchstens 1024 Bytes. `rp-bar.json` ist auf 32 KiB und 128 aktive Zuordnungen begrenzt. Alle Stores bleiben asynchron, begrenzt und gegen veraltete Lade-/Schreibergebnisse abgesichert.

## Lokale Werkzeuge und Abnahme

Automation reagiert auf Login, Kampf-/Dutywechsel und Gebietswechsel mit begrenzten Status-/Verfügbarkeits-/Profilaktionen. Keine Alters-/Netzfreigabe wird dadurch erteilt. Lokale Textentwürfe erzeugen Vorschau/Kopiertext; interne Profilverweise lösen nur vorhandene erlaubte Daten auf, keinen Fremdabruf.

Neue Module sind DE/EN lokalisiert; ältere Host-/Hilfetexte sind teilweise deutsch. Build und Tests ersetzen weder Spielabnahme noch reale LAN-/Internettests. Siehe [Status](../STATUS.md), [Machbarkeit](feasibility.md) und [Funktionsmatrix](trp3-feature-map.md).

