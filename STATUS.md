# Status

Stand: 2026-11-11

## Aktueller lokaler UX-Stand – nicht veröffentlicht

Alle neuen UX-Änderungen liegen nur im Arbeitsverzeichnis, **uncommitted und unpushed**. Commit und Push übernimmt ausdrücklich der Nutzer. Versionsnummer und öffentliches Release-ZIP 0.1.0.0 sowie Root-`repo.json` sind unverändert; die Veröffentlichungshistorie weiter unten beschreibt nicht diesen neuen Code.

- Editor und Leser teilen genau fünf gleichrangige Bereiche: RP-Status und Kurzprofil; OOC Informationen; Auf den ersten Blick; Weitere Charakterdaten; RP-Erfahrung und Absprachen.
- Kleiner Profilkopf, ein Inhaltsbereich, Gestaltung im Kopf-Popup und JSON unter Profilaktionen. Keine zweispaltige Vorschau oder Neuanlagebaukästen für Hooks/Extras/Traits/Begleiter.
- Bestehende Zusatzinhalte bleiben lesbar und gezielt aus dem Entwurf entfernbar; sie bleiben bis zur Entfernung öffentlich und werden vor Export/Teilen kenntlich gemacht. Deaktivierte Glances sind standardmäßig unsichtbar; unbearbeitete Sprachen bleiben unverändert.
- Zweizeiliges MiniHUD mit Ich-/Ziel-/Neutral-Kontext, einklappbarem lokalem Zustand, Wiederöffner und Kontextmenü. Fremde Inhalte sind nur lesend; ungültige Ziele verwenden keinen eigenen Profilfallback. Die Kontextquelle ist das tatsächliche Ziel, nicht eine gepinnte Zusatzkarte.
- Vollständige scrollbare Glance-Inhalte bei Hover/Fokus, unabhängig von Hilfshinweisen; rund 500 ms Mausübergang und optionales Anheften. Kontext-/Policywechsel invalidiert offene Inhalte.
- Feldnahe beschriftete Hilfen und sechs neue Hilfethemen. Neue Einstellungen lassen die große Zusatz-Zielkarte zunächst geschlossen; vorhandene Entscheidungen bleiben bestehen.

### Prüfung des lokalen UX-Stands

Release-Build: **0 Warnungen, 0 Fehler** (1,08 Sekunden). Abschließender Gesamtlauf: **500/500 bestanden**, 0 Fehler, 0 übersprungen (26 Sekunden); die 33 neuen UX-Fälle sind enthalten. Unabhängiges Review: Freigabe ohne kritische oder wichtige Restbefunde. Keine Abnahme im laufenden Spiel erfolgt.

Testhistorie: Der erste Gesamtlauf bestand 499/500; im bereits vorhandenen unveränderten Test `ThemeTests.ConcurrentChangesPublishOneCompleteChoiceWithoutLeavingTemporaryFiles` trat einmalig eine `UnauthorizedAccessException` bei `File.Move` mit Überschreiben (`ThemeSettingsStore`, Zeile 71) auf. Der Einzeltest mit identischen Artefakten bestand anschließend 1/1 (55 ms), danach der vollständige Wiederholungslauf 500/500. Die Ursache ist nicht belegt; es wurden weder ein Produktiv-I/O-Fix noch automatische Test-Retries oder übersprungene Tests eingeführt.

Die früheren 33 Offline-Paketprüfungen und öffentlichen Download-/Hashnachweise gelten für das unveränderte Release 0.1.0.0, nicht als Paketabnahme dieser lokalen UX-Version. Vor späterem Installerupdate neue Version, Paket und Index erstellen; 0.1.0.0 nicht ersetzen.

## Öffentliche Veröffentlichung 0.1.0.0

Das bestehende Repository [demkadse/Final-Roleplay-1](https://github.com/demkadse/Final-Roleplay-1), Branch `master`, ist öffentlich; GitHub erkennt die MIT-Lizenz. [Release v0.1.0.0](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.1.0.0) ist veröffentlicht, kein Entwurf und als Prerelease markiert. Es enthält `FRP1-0.1.0.0.zip` und `SHA256SUMS`. Es wurde kein separates Repository angelegt.

Verifizierte Installeradresse: `https://raw.githubusercontent.com/demkadse/Final-Roleplay-1/master/repo.json`. JSON und ZIP sind anonym per HTTP 200 erreichbar. Der Index enthält einen Eintrag für Version 0.1.0.0/API 15 und verweist auf das richtige Releasepaket. GitHub verteilt ausschließlich Software und Installerindex; FRP1-Profile oder private Daten werden nicht dorthin übertragen.

Eigener Projektcode und Dokumentation sind unter [MIT](LICENSE) lizenziert. Nutzerprofile/Bilder und Fremdkomponenten sind dadurch nicht automatisch mitlizenziert. Zusätzlich zum bestätigten Codestand mit 467 Funktionstests und acht Probegruppen bestehen **33 Offline-Paketprüfungen ohne Fehler**. Das ist keine erneute Ausführung der Funktionstests.

Die anonymen Downloads des kanonischen JSON-Index (1.373 Bytes) und des ZIP (1.058.298 Bytes) stimmen jeweils per SHA-256 exakt mit dem geprüften Veröffentlichungskandidaten überein. Die öffentliche Datei `SHA256SUMS` ist ebenfalls per HTTP 200 erreichbar; beide Einträge passen. Die tatsächlich öffentlich heruntergeladenen JSON-/ZIP-Dateien wurden gemeinsam erneut mit `Test-ReleasePackage` erfolgreich validiert. Paketbuilder: null Warnungen/Fehler; Veröffentlichungsreview freigegeben. Reale Spiel-/LAN-/Internetabnahme wird daraus nicht abgeleitet.

Ausführliche Beschreibung und konkrete Installations-/Bedienungsschritte stehen in [README](README.md), [Benutzerhandbuch](docs/benutzerhandbuch.md) und [Release-Notes](docs/release-notes-v0.1.0.0.md). Dies ist ein früher Teststand; reale Spiel-/Netzabnahme bleibt offen.

## Integrierter Stand des veröffentlichten 0.1.0.0

Der frühere Stand „lokaler Editor plus separater Transport“ ist überholt. Die folgenden Module sind im echten Plugin verbunden und abschließend auf Code-/Testebene geprüft; die native Spiel-/Netzabnahme bleibt offen:

| Bereich | Vorhandener Umfang |
|---|---|
| Profile | Mehrere Profile, explizite lokale Charakterzuordnung, Status/Verfügbarkeit, Glances/Hooks, freie Felder, Traits, RP-Präferenzen, Sprachenbeschreibung und Begleiter. |
| Journal/Medien | Drei Leselayouts, sicheres begrenztes Rich Text, Standarddarstellung, grafischer Spielicon-Picker und lokaler Portraitzuschnitt mit begrenztem metadatenfreiem PNG. |
| Netzwerk | Dauerhafte DPAPI-Identität, gespeicherte Kopplung/Peerfreigabe, gegenseitiges TLS-Pinning und tatsächlicher UI-Profiltransfer mit Versions-/Hash-Synchronisierung. |
| Register | Private Notizen, Favoriten, Beziehungen, Mute/Netzblock, Cache-/Kontaktsuche, bestätigte fremde Bindungen und Verlauf geöffneter Profile. Keine automatische Aufzeichnung aller vorbeilaufenden Figuren. |
| Spielanzeigen | Mouseoverprofil, bewegliche Zielkarte, nativer Eintrag **FRP1-Profil öffnen** für bestätigte bekannte Bindungen, lokale Nameplates/Titel/Farben, General-Chatnamen und Karte für einen bekannten aktuellen Akteur. |
| Werkzeuge | Höchstens 16 deklarative Automationen, standardmäßig aus; lokale Textentwürfe und interne Verweise auf vorhandene erlaubte Profile. |
| Bedienung | Eigene RP-Leiste, globale Hilfen, elf native Themes, DE/EN in neuen Modulen. Einige Host-/Hilfetexte bleiben deutsch. |
| Schutz | Versionierte Profilregeln, aktive Alterswahl je Sitzung, zusätzliches Adult-Opt-in und aktuelles Audience-Gate vor Profil-/Medien-/Anzeigezugriffen. |

Die letzte Social-UI-Welle ist angebunden: Detailgrad, Zielpin, Umgebungsfilter, Cacheverwaltung, Optionswiederholung und expliziter Listenport beziehungsweise numerische Einladungsadresse. Kleine Nameplate-Profilicons sind optional und standardmäßig aus; Questmarker bleiben unangetastet.

## Historische Codeprüfung des veröffentlichten 0.1.0.0

Der endgültige vollständige Release-Build besteht mit **null Warnungen und null Fehlern** (2,30 Sekunden). Der anschließende vollständige Testlauf besteht mit **467/467 Tests**, ohne Fehler und ohne übersprungene Tests (34 Sekunden). Beide Prüfungen erfolgten nach den letzten Anzeige-/Performancekorrekturen. Das abschließende unabhängige Review erteilt **Freigabe ohne verbleibende kritische oder wichtige Befunde**.

Die separate Release-Probe besteht mit acht Gruppen über zwei echte lokale TLS-Clients: kein Profil ohne Freigabe, gegenseitiger Transfer, automatische Versions-/Hashaktualisierung, erneutes Lesen lokaler Daten und Alters-/Opt-in-/Widerrufssperren.

Der wichtige Chatbefund ist korrigiert und unabhängig regressionsgeprüft: nur General-Profilnamen dürfen in den nicht rückholbaren Chat. Mature-Inhalte bleiben auf widerrufbare Anzeigen beschränkt. Ein verspätetes Iconpicker-Ergebnis wird bereits beim Beginn des Speicherns invalidiert. Die bereinigten Anzeige-/Umgebungspfade verwenden aktuell gegatete unveränderliche Metadaten statt wiederholter vollständiger Profil-/Portraitverarbeitung je Actor. Die Gates bleiben erhalten; ein gemessener FPS-Gewinn wird nicht behauptet.

## Fachliche Grenzen

- Kein FRP1-Profilserver, Account, zentrale Profildatenbank, Relay, STUN oder Discoverydienst; keine DNS-/Router-/Firewalländerung und keine Telemetrie. GitHub-Softwaredownloads sind davon getrennt. Netzwerk und Profilfreigabe bleiben explizit.
- Peer-Schlüsselbesitz ist kein FFXIV-Charakterbesitznachweis. Content-ID und private Registerdaten bleiben lokal.
- Automationen sind begrenzte deklarative Regeln, keine Skripte oder Chatbefehle.
- NPC-/Rede-/Emote-Werkzeug erzeugt lokale Textentwürfe; keine NPC-Imitation im Spiel.
- Karte zeigt einen bekannten aktuell vorhandenen Akteur; keine weltweite RP-Spielersuche.
- RP-Sprachen sind Profilangaben, keine Manipulation der Spielsprache.
- Import/Export betrifft FRP1-JSON; WoW-/MSP-Kompatibilität wird nicht behauptet.
- Keine Musik, WoW-Assets, Taschen-/Inventarsysteme oder TRP3 Extended.

## Offene Abnahmen

Native Bedienung, Skalierung, Fokus, Entladen, reale Volkzuordnung, Nameplate-/Chatdarstellung und Portrait-/Iconwiedergabe müssen im laufenden Spiel geprüft werden. Reale LAN-/IPv6-/Internet-/NAT-/CGNAT-Erreichbarkeit und Belastungsprüfung bleiben offen. Die End-to-End-Definition of Done mit zwei Spielern ist dadurch noch nicht abgenommen.

Eine Installation beziehungsweise reale Spiel-/Netzabnahme wurde nicht durchgeführt. Quellcode, MIT-Lizenz und Installerverteilung des bisherigen 0.1.0.0 sind veröffentlicht und auf Erreichbarkeit geprüft; die neue lokale UX ist weder committed noch gepusht oder als Release ausgeliefert. Vollständige Zuordnung der vorhandenen und angepassten Funktionen: [TRP3-Matrix](docs/trp3-feature-map.md), [Glance-Matrix](docs/glance-feature-map.md).

