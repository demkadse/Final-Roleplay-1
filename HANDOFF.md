# Übergabe

Stand: 2026-11-12

## Aktueller Arbeitsfokus

**Releaseautomatik lokal eingerichtet und geprüft.** UX ist bereits im Nutzercommit `62a91a7` („rework“) enthalten. Der Nutzer übernimmt den Commit/Push der neuen Automatik; ein gehosteter Lauf wurde nicht gestartet. Öffentliches ZIP/Index 0.1.0.0 bleiben bis zum erfolgreichen neuen Workflowlauf unverändert.

Ausgangs-HEAD ist `62a91a7`; der Arbeitsbaum war vor dieser Einrichtung sauber. Neue Workflow-/CI-/Testdateien und geänderte Build-/Pakethilfen beim späteren Nutzercommit vollständig berücksichtigen. Nicht aus einem Sourcecommit auf eine neue Installerverfügbarkeit schließen.

Abschlussprüfung des UX-Quellstands: Release-Build 0 Warnungen/Fehler, Gesamtlauf 500/500 bestanden ohne Fehler/Skips, unabhängige Reviewfreigabe. Ein erster Gesamtlauf hatte den einmaligen bestehenden Theme-I/O-Testfehler; Einzeltest und komplette Wiederholung bestanden. Ursache unbelegt, kein Produktiv-I/O-Fix/Retrymechanismus; Details in [STATUS.md](STATUS.md). Die gesonderte Prüfung der Releaseautomatik folgt weiter unten.

Der lokale Editor und Leser haben dieselben fünf Bereiche aus `ProfileAreas`. Keine sechste Gestaltungs-/JSON-Hauptebene hinzufügen: Gestaltung bleibt Kopf-Popup, JSON in Profilaktionen. Vorhandene Zusatzinhalte sind unter Weitere Charakterdaten über `ExistingAdditionalContent` gezielt entfernbar; Snapshot-/CAS-Schutz bewahren. Ausgeblendete oder deaktivierte Inhalte bleiben öffentliches Schema, keine privaten Notizen. Unbearbeitete `RpLanguages` nicht versehentlich leeren.

MiniHUD-Kontext kommt über `RpBarContextResolver` aus dem tatsächlichen Raw-Target, nicht der gepinnten Social-Zielkarte. Ohne Ziel/bei Selbstziel eigenes Profil, erkannter erlaubter Fremder read-only, sonst neutral ohne Own-Fallback. Zielwechsel darf Entwurf, aktive eigene Zuordnung und Sharing nicht verändern. Hover-/Fokus-Glances müssen trotz abgeschalteter Hilfe vollständig lesbar sein; Kontext-/Policywechsel invalidiert sie sofort.

`Collapsed` ist ein eigener lokaler Leistenwert im rückwärtslesbaren Schema 1. Wiederöffner, Lock/Move/Scale und Viewportgrenzen erhalten. Große Zusatz-Zielkarte nur bei neuen Einstellungen standardmäßig aus; vorhandene gespeicherte Wahl bewahren. Hilfen direkt am Feld/Titel beschriften, keine losen Fragezeichengruppen.

Für den nächsten Installerstand erzeugt die vorbereitete Automatik nach ihrer Einführung neue Version, Paket und Index. **Das veröffentlichte v0.1.0.0 nicht überschreiben und repo.json nicht von Hand versionieren.** Reale Spielabnahme bleibt offen. UX: [Benutzerhandbuch, Abschnitt 17](docs/benutzerhandbuch.md); Pipeline: [Releaseautomatik](docs/auto-release.md).

Die Pipelineversion verwendet Basis `0.1.1` und `GITHUB_RUN_NUMBER - 1` als vierte Komponente; `Frp1Version` überschreibt nur die Pluginversion, FRP1.Core bleibt bei 1.0.0.0. Windows/.NET-SDK 10.0.401 und offizielles API-15.0.4-`DALAMUD_HOME` sind festzuhalten, kein stiller Wechsel auf „latest“/API 17. Zwei Jobs: Build nur lesend ohne persistierte Checkout-Credentials; Publish erst nach Erfolg mit `contents: write` über eingebautes `GITHUB_TOKEN`, kein PAT.

Release-Assets nicht überschreiben: identische Wiederholung wiederverwenden/no-op, abweichender Hash stoppen. Source-Head-/CAS-Prüfung muss veraltete Läufe vom Index fernhalten. `repo.json` erst nach öffentlichem ZIP-Download und Hashprüfung aktualisieren; Fehler dürfen den bisherigen Installerindex nicht unbrauchbar machen. Botcommit darf ausschließlich `repo.json` ändern. Dokumentations-/Indexpushes dürfen keine Releaseschleife auslösen.

Nach einem Botcommit den lokalen Branch vor dem nächsten Push abgleichen, etwa `git pull --ff-only`; bei Abweichungen prüfen statt Force-Push. Lokal bestehen der frische API-15-Build ohne Warnungen/Fehler, 500/500 Funktionstests, 33/33 Paketprüfungen und 101/101 CI-Prüfungen (Policy 33, Publishabläufe 24, SDK 44). Kandidatenprüfung und actionlint sind erfolgreich, zwei Erstellungen erzeugen identische ZIP-/Index-/Prüfsummendateien. Das unabhängige Automatikreview ist ohne kritische oder wichtige Restbefunde freigegeben. Lokale Simulations-/Pakettests sind kein Hosted-Nachweis; der erste gehostete Lauf bleibt bis zum Nutzercommit/-push offen.

## Historisch: veröffentlichter Stand 0.1.0.0

Alle wesentlichen Module sind im nativen Plugin verdrahtet; die frühere Beschreibung als lokaler Editor mit separatem Transport ist überholt. Social-/Nameplate-Icon-Ergänzungen und letzte Anzeige-/Performancekorrekturen sind enthalten. Danach bestehen der vollständige Release-Build mit null Warnungen/Fehlern und die Gesamtsuite mit **467/467 Tests**, ohne Fehler/übersprungene Tests. Das endgültige unabhängige Review ist freigegeben; keine kritischen oder wichtigen Befunde verbleiben. Die separate Probe besteht mit acht echten Loopback-TLS-Prüfgruppen. Reale Spiel-/Netzabnahme ist weiterhin offen.

Das bestehende Repository `demkadse/Final-Roleplay-1`, Branch `master`, ist öffentlich und unter MIT veröffentlicht. Release `v0.1.0.0`, ZIP, Prüfsummen und Installerindex sind anonym erreichbar und verifiziert. Eine lokale Spielinstallation ist nicht erfolgt. Projektanweisungen beachten und fremde/uncommittete Änderungen erhalten. Keine bereits angebundenen Funktionen oder abgeschlossene Veröffentlichung erneut als offen planen. [STATUS.md](STATUS.md) und [TRP3-Funktionsmatrix](docs/trp3-feature-map.md) sind der Einstieg.

## Abgeschlossene Veröffentlichung

Veröffentlichtes Ziel: `demkadse/Final-Roleplay-1`, Branch `master`, Tag `v0.1.0.0`, Asset `FRP1-0.1.0.0.zip` und `SHA256SUMS`. Installeradresse: `https://raw.githubusercontent.com/demkadse/Final-Roleplay-1/master/repo.json`. Das Release ist kein Entwurf und als Prerelease markiert; kein weiteres Repository anlegen.

Root hat die öffentliche Verteilung bestätigt: anonyme JSON-/ZIP-GETs mit HTTP 200, gültiger Eintrag für 0.1.0.0/API 15 und ZIP-SHA-256 identisch zum geprüften Kandidaten. Zusätzlich bestehen 33 Offline-Paketprüfungen ohne Fehler, der Paketbuilder ohne Warnungen/Fehler und das Veröffentlichungsreview. Diese Prüfung ist getrennt von den zuvor bestätigten 467 Funktionstests und acht Loopbackgruppen.

GitHub ist ausschließlich Softwareverteilung, kein FRP1-Profilbackend. Öffentliche JSON-/ZIP-Dateien benötigen kein GitHub-Konto; der Pluginbetrieb ebenfalls keinen Account. Öffentliche Profile gehen nur direkt an ausdrücklich gekoppelte/freigegebene FRP1-Clients. Private Notizen, Altersauswahl, Content-IDs, Zuordnungen und Einstellungen bleiben lokal. LICENSE, Index und Fremdkomponentenunterlagen sind vorhanden; die Dokumentationsrolle ändert diese Dateien nicht.

Projektcode und Dokumentation stehen unter [MIT](LICENSE), einschließlich kommerzieller Weiterverwendung unter Erhalt der Hinweise. Nutzerprofile/Bilder und fremde Inhalte sind davon nicht automatisch erfasst; keine Rechteübertragung dieser Inhalte behaupten.

Neue Nutzerdokumentation: [ausführliches Handbuch](docs/benutzerhandbuch.md), [Release-Notes 0.1.0.0](docs/release-notes-v0.1.0.0.md).

## Zuständigkeiten und Datenwege

- `Core/Profiles`: öffentliches, begrenztes Schema mit Traits/Präferenzen/Sprachen/Begleitern und normalisiertem Portrait. Profile behalten zufällige GUID, Revision und Inhaltskennzeichnung.
- `PeerIdentityStore` schützt die dauerhafte Identität mit Windows-DPAPI CurrentUser. `PeerTrustStore` hält private Kopplung/Freigaben; Fehler nicht durch stillen Identitätsersatz oder Übergehen der Pins umgehen.
- `Networking/NetworkSession` ist der UI-Lebenszyklus für Opt-in-Netzwerk, Kopplung, Freigabe, Trennung, Block und eingehende Profile. Transport wird im Plugin tatsächlich verwendet.
- `OwnProfileSession` bleibt der gemeinsame Schreiber für Editor/Leiste und Automation. Erwartete Revision/Hash prüfen, Änderungen serialisieren und keine UI-Schreibwege am Koordinator vorbei einführen.
- `RegisterSession`, `ContactStore` und `PresentationResolver` liefern private Notizen/Beziehungen, bestätigte Zuordnung und aktuell erlaubte Anzeigen.
- `UiProfiles` verbindet Journal, grafischen Iconpicker, sichere Textdarstellung und lokalen Portraitzuschnitt; `UiSocial` verbindet Netzwerk, Register/Cache, Mouseover und Zielkarte.
- `GamePresentationModule` verwendet aktuelle lokale Actor-/Szenensnapshots für Nameplates, Chatnamen und Kartenaktion. Keine Spielpakete verändern.
- `Automation`, `Localization` und `UiTools` enthalten begrenzte lokale Regeln, Sprachauswahl, Textentwürfe und interne Profilverweise.

Der vorhandene Verlauf wird beim Öffnen eines Profils geschrieben (`ProfileOpened`). `Seen` wird vom Dienstmodell unterstützt, ist aber keine automatische Host-Protokollierung vorbeilaufender Akteure.

## Verbindliche Schutzgrenzen

Keine Accounts, Cloud, zentrale Profile/Moderation, Relay-/STUN-/Discoverydienste oder Telemetrie. Router/Firewall/DNS bleiben unverändert. Quellenlinks in Dokumenten sind keine Runtimeabhängigkeit. Direkte IP-Sichtbarkeit und Netzwerkzugriff bleiben bewusste Zustimmung.

Dauerhafte Peer-ID beweist Schlüsselbesitz, keinen Spielcharakterbesitz. Eigene Content-ID bleibt lokal; fremde Zuordnung Name/Homeworld→Peer/Profil wird ausdrücklich lokal bestätigt. Notizen, Beziehungen, Verlauf und private Konfiguration nie in öffentliche Profile oder Wire-Payloads aufnehmen.

Profilregeln aus `UsageGuidelines` bleiben kanonisch für Erststart und Einstellungen. Versionierte Hinweisbestätigung ist lokal persistent; bei materieller Textänderung Version erhöhen. `AudienceSettings.LoadForSessionAsync` startet jede Sitzung dagegen wieder mit `Unspecified/false`. Hinweise, Themes, Profilpräferenzen und Beziehungen erteilen keine Altersfreigabe oder Zustimmung zu Handlungen.

Ein gemeinsames aktuelles Audience-Gate muss vor Namen, Titel, Icons, Portraitdecodierung, Tooltip, Zielkarte, Suche und Journal greifen. Widerruf schließt Verbindungen und verwirft gesperrte Ansichten sowie verspätete asynchrone Ergebnisse. Picker-Callbacks werden bereits bei Savebeginn invalidiert; keinen alten Callback wieder auf einen anderen Entwurf anwenden.

**Chat ist nicht rückholbar:** Namensersetzung ausschließlich aus General-Profilen, auch wenn Erwachsene Mature-Inhalte freigegeben haben. Nicht durch eine bloße Prüfung der aktuellen Altersauswahl ersetzen. Mature-Profilnamen nur in widerrufbaren Anzeigen verwenden.

Netzblock beendet/verhindert Verkehr; Mute unterdrückt Darstellung. Cachelöschen ist keine Entkopplung oder Netzsperre; eine weiter erlaubte Verbindung kann ein Profil erneut liefern. Standarddarstellung behält das eigene FRP1-Grundtheme und überstimmt fremde Gestaltung.

## Lokale Speicherung

`profiles/`, `cache/`, `peer-identity.dpapi`, `peer-trust.json`, `contacts.json`, `rp-bar.json`, `themes/<ContentId:X16>.json`, `usage-notices.json`, `audience.json`, `social-ui.json`, `game-presentation.json`, `automation.json` und `language.json` liegen im Plugin-Konfigurationsverzeichnis. Der Theme-Dateiname ist symbolisch; keine echten IDs/privaten Schlüssel oder Dateiinhalte protokollieren.

Alle Stores strikt, begrenzt und asynchron halten. Änderungen mit Generationen gegen veraltete Load-/Save-Ergebnisse absichern. Native Style-Pushes nach dem FRP1-Draw zurücknehmen; fremde Plugins nicht umgestalten. Die eigene Leiste bleibt viewportbegrenzt, lokal skalierbar und nach Logout nur vorübergehend versteckt.

## Verbleibende Abnahme und Fortsetzung

1. Native Spielabnahme einschließlich Social-/Icondarstellung, Fensterbedienung, Kontextmenü, Skalierung und Entladen durchführen, sobald entsprechend beauftragt. Codeprüfung und Review nicht damit gleichsetzen.
2. Echte Zwei-Spieler-/Netzabnahme als offen führen, solange nicht tatsächlich erfolgt. GitHub-Veröffentlichung ist ausdrücklich beauftragt; Spielstart, lokale Installation und Serverdeployment sind davon nicht automatisch umfasst.
3. Commit und Push der lokalen UX ausdrücklich dem Nutzer überlassen. Vor späterer Veröffentlichung die Versions-/Paket-/Indexentscheidung getrennt treffen; keinen alten Releaseasset ersetzen.
4. Restliche Sprachlücken und nachgewiesene Fehler gezielt bearbeiten; keine unbeauftragten neuen Produktideen oder Taschen-/Inventarfunktionen hinzufügen. Builds/Tests nur bei neuen Änderungen oder offenen Fragen erneut ausführen.

## Reproduzierbare lokale Prüfung

```powershell
dotnet build FRP1.slnx -c Release --nologo
dotnet test FRP1.slnx -c Release --nologo -p:BuildInParallel=false
dotnet run --project tools/FRP1.Probe -c Release -- --self-test
```

Loopback belegt keine NAT-/CGNAT-Erreichbarkeit. Weitere Grenzen: [Machbarkeit](docs/feasibility.md), [Architektur](docs/architecture.md), [UX](docs/ux-and-vanity.md). TRP3-/Glance-Funktionen sind eigenständig implementiert; kein fremder Code/keine WoW-Assets übernommen. Die öffentliche Softwareverteilung ist verifiziert; tatsächliche Spiel-/Netzabnahme bleibt offen.

