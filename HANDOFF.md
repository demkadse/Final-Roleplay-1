# Übergabe

Stand: 2026-11-13

## Aktueller Arbeitsfokus

**WP4 am 2026-11-13 als 0.2.0.6 veröffentlicht:** Quelle `6b97c1bd556f98d7d6295954d4a371e5e7c32f41`, [FRP1-IPC v1](docs/frp1-ipc-v1.md) implementiert, lokal 812/812 Funktionstests (27 Sekunden), 287 gezielte Tests, voller Release-Solutionbuild 0 Warnungen/Fehler (2,54 Sekunden), Paket33 und unabhängiges Review ohne kritische/wichtige Restbefunde. [Hosted-Lauf 43416697284](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43416697284) besteht Build/Publish mit 0 Warnungen/Fehlern, 812/812 Funktionstests (32 Sekunden), Paket33 und CI105 (Policy33, Publish24, SDK44, Quellpfade4), alle Ergebnisse ausdrücklich belegt. Registrierung und native Verdrahtung nur statisch geprüft/kompiliert, keine native IPC-/Spielprobe.

[Release v0.2.0.6](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.6) und kanonischer Raw-Index öffentlich; JSON anonym HTTP 200, ZIP und `SHA256SUMS` anonym geladen, Hashvergleich und erneute Paketvalidierung erfolgreich. ZIP 1.110.041 Bytes, SHA-256 `E4DBF184BC5F09F6F54307CEE2227DC374DD206FDF7816C539AB00C2FD8C3450`; JSON SHA-256 `F045E351A03AE6F7A47C38E120343F0155A643F38D92A6BABD401F57BD90C57A`, LastUpdate `1794569012`. Botcommit `7666329` ändert nur `repo.json`; lokaler Branch per Fast-forward abgeglichen.

API 1.0: primitive Capabilities/Readiness/Status/Verfügbarkeit, eigene Queueaktionen und Ticketergebnisse. Globale Lese-/Steuerfreigabe und drei Funktionsfreigaben alle aus; Lesen General-only, Mature-Steuerung nur mit Adult samt Mature-Opt-in. Aktives eigenes Profil maßgeblich, keine Editorwahl oder privaten Exporte. `Persisted` verlangt erfolgreichen eigenen Writer und integrierten gespeicherten Kontext; im Own-Poll-Zeitfenster `Running` halten, echte Konflikte `Stale`. Policy-Epochen invalidieren synchron, Ergebnisse bleiben an Scope/Generation/Epoche gebunden und werden bei Freigabewechsel gelöscht. Native Spielobjekte nie aus IPC-Callbacks lesen. Aktiven IPC-Snapshot nur bei geänderter Own-Session-Version neu erzeugen.

I/O-Fortsetzung: Das MVP-Gate allein löste den sporadischen Windows-`File.Move`-Fehler beim Überschreiben nicht. Jetzt `File.Replace(temp, path, eindeutig.previous)` und Sicherung ausschließlich nach erfolgreichem Replace löschen; bei teilweisem Replacefehler erhalten. Erstanlage Move ohne Überschreiben. Offen-/Blockierleser-Regressionen und synthetische 23.000-Vorgangsdiagnose mit Sicherung erfolgreich; Ursprung der zeitweisen Handles unbelegt. Keine Virenscannerursache, ACLänderung, Retries oder Testskips daraus ableiten. Details in STATUS.

**Fortsetzung:** WP5 ist nach begrenztem Moodles-Quell-/API-/Ratingabgleich in Umsetzung (`moodles_impl`); noch keinen abgeschlossenen Funktions-/Prüf-/Releaseumfang behaupten. Moodles-Manifest 1.1.3.5/API 15 schreibfrei erfasst, nativ nichts aktiviert. Kein automatisches General-Rating für unbekannten Customtext/Icons. Vorheriges verifiziertes Quellbackup: `.release-staging/before-moodles-20261113T112856Z`, Quelle `7666329`, SHA-256 `32CABC658791D3436FC4F8546EB88AD44D8052CEB81786BF8ACFD7072F3A2006`. WP3-Ausbau und WP5–8 bleiben separate Pakete, native Abnahmen gesondert. Der Gesamtauftrag wird paketweise fortgesetzt.

## Vorheriger Integrationscheckpoint: Honorific-MVP

**Integrationen, MVP am 2026-11-13 als 0.2.0.5 veröffentlicht:** [Integrationsplan](docs/integrationen-plan.md) ohne Planrestbefunde freigegeben; WP1/WP2 auf Basis `e63b238` umgesetzt und unabhängig ohne wichtige Restbefunde reviewfreigegeben. [Release v0.2.0.5](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.5) öffentliches Prerelease, kein Entwurf; Quelle `bae4ef86c6e812c45536f73db7ea356fc5ce1e31`. Lokal 152 neue Headless-Tests einschließlich elf Unicodefällen plus 510 bisherige Tests: **662/662 bestanden**, 27 Sekunden, keine Fehler/Skips. Voller Release-Solutionbuild gegen API 15/.NET-SDK 10: **0 Warnungen/Fehler**, 1,03 Sekunden; **33/33 Paketprüfungen** bestanden. [Hosted-Lauf 43354035033](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43354035033) besteht Build/Publish, 0 Warnungen/Fehler, 662/662 Funktionstests (33 Sekunden), 33/33 Paketprüfungen und 105/105 CI-Prüfungen (Policy33, Publish24, SDK44, Quellpfade4). Botcommit `a655c30` ändert nur `repo.json`, lokaler Branch per Fast-forward abgeglichen. Native IPC-/Spielprobe fehlt; keine automatische Aktivierung. Historische Planungsrunde am 2026-11-12 zu `89b5e42` erfolgte ohne Code-/Runtime-/Releaseänderungen oder Builds/Tests.

Öffentlicher Downloadnachweis: kanonischer Raw-Index anonym HTTP 200, Version 0.2.0.5; ZIP und `SHA256SUMS` anonym geladen, Hashvergleich und vollständige Paketvalidierung bestanden. ZIP 1.094.651 Bytes, SHA-256 `432AA0E6177B8753462397AF392F987086E008D4D2D42C7EAD340E0C7CD3659D`; JSON SHA-256 `F3BA5D2E5495DF8B2EAB2F55DF9267FE1790771D5A36CF482FB6F6041ABF49BE`, LastUpdate `1794537751`.

Historischer Folgeauftrag des MVP: WP4 fertigstellen, testen und reviewen. Das ist im oben genannten Quellstand abgeschlossen; Veröffentlichung und nächste Pakete richten sich nach dem aktuellen Checkpoint. Eigentum des bisherigen MVP-Entwicklers: `Ipc/**`, Integrationsfreigaben sowie nötige Plugin-/MainWindow-/Lokalisierungsverdrahtung. Nicht nach dem MVP stoppen; paketweise bis zum beauftragten Ergebnis beziehungsweise mit dauerhaftem Checkpoint fortsetzen.

Honorific-Quell-/Manifestabgleich am 2026-11-13: `ApiVersion` 3.2, `GetLocalCharacterTitle`, `LocalCharacterTitleChanged`, `Ready`, `Disposing`; Quellprojekt 1.7.5.1 mit SDK 15 und `net10.0-windows`, lokales Manifest 1.7.5.1/API 15. Das belegt keine native IPC-Probe. Lokales MVP standardmäßig aus; Opt-in, Status, Vorschau und bewusste Übernahme in unveränderten gespeicherten eigenen Entwurf, erst normales Speichern persistiert den Titel. `OwnProfileSession`, Draft-CAS, Kontextgeneration, begrenztes JSON und Frame-Queue sichern den Weg. Keine Honorific-Set-/Clear-/SetIdentity-Aufrufe und kein automatischer Export privater/Mature-Inhalte. Titelkollisionsregel ausschließlich für den eigenen nativen Titelslot bei bestätigtem Kontext. Parser verwirft doppelte Schlüssel im gesamten JSON und ungültiges UTF-16 ohne Ersatzzeichen. Ausgeschaltet kein Draft-Capture; frische Import-CAS neben Draft-Cache. `LocalJsonFile` nutzt für Load/Save das bestehende Instanzgate gegen Windows-Rename-/Leserennen; keine instanzübergreifende Garantie oder Retries.

Verifiziertes Quellbackup: `.release-staging/integrations-backup-20261113T022114Z/source-before-integrations.zip`, Quelle `e63b2387f266be6b4f411e8eb49b7fe1b3925348`, SHA-256 `C34A4EE5D471D763B6F8E9808839A64E4B619F31AA1FE27DE4ABDA7CBD049A7C`. Bei Unterbrechung diesen Checkpoint und den tatsächlichen Git-/Teststand abgleichen; keine Fertigstellung aus dem Planstatus ableiten.

**Kompakter UI-Neubau als 0.2.0.4 veröffentlicht.** Der Nutzer hat die früheren Layoutvorgaben ausdrücklich aufgehoben und freie Neugestaltung bei maximaler Kompaktheit beauftragt. Maßgebliche aktuelle Vorgaben stehen weiter unten und in [UX](docs/ux-and-vanity.md): drei Hauptwege, drei Profilbereiche, eigene Lesekarte mit getrenntem Editmodus, einzeilige immer eigene RP-Leiste und drei primäre Themes. Profil-/Privatdaten, DPAPI-Identität, Trust, Freigaben und Schutzgates bleiben erhalten. Quelle `6c2e491`, Versionsbasis 0.2.0.0, FRP1.Core 1.0.0.0.

Lokaler Vorabnachweis: voller Release-Solutionbuild mit 0 Warnungen/Fehlern (2,35 Sekunden), 42 gezielte und 510/510 vollständige Tests (27 Sekunden), 33/33 Paketprüfungen; unabhängige Reviewfreigabe. [Hosted-Lauf 43296113100](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43296113100) besteht Build/Publish, 0 Warnungen/Fehler, 510/510 Funktionstests (35 Sekunden), 33/33 Paketprüfungen und alle 105/105 CI-Prüfungen ausdrücklich belegt. [Release v0.2.0.4](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.4) ist öffentliches Prerelease, kein Entwurf. Botcommit `f2071ac` ändert nur `repo.json`; lokaler Branch abgeglichen. Kanonischer Raw-Index, ZIP und SHA256SUMS anonym geladen, Hashes passend und vollständige erneute Paketvalidierung erfolgreich. Native Spiel-/Skalierungs-/Netzabnahme bleibt offen; Nutzer aktualisiert über die bestehende JSON-Adresse und lädt das Plugin bei Bedarf neu. Vorheriges Sourcebackup zu `b9d2bb7`: `.release-staging/ui-rebuild-backup-20261112T194250Z`, SHA-256 `378594B3D74ABA2A49CE85E11CDA83F6A281621CBFBF0F188AAD4B4172F46885`.

**RP-Leistenfix als 0.1.1.3 veröffentlicht und vollständig geprüft.** Commit `809d996` verhindert, dass ein vorübergehend geschlossenes Fenster die gerade aktivierte Leistensichtbarkeit wieder ausschaltet. Gespeicherte Sichtbarkeit bleibt maßgeblich; Schließen bereinigt nur offene Glance-Inhalte. Schutzprüfungen unverändert. Lokal Build 0 Warnungen/Fehler, 76/76 gezielte Tests und unabhängige Reviewfreigabe. [Lauf 43284625889](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43284625889): Build/Publish erfolgreich, Build 0 Warnungen/Fehler, 500/500 Funktionstests, 33/33 Paketprüfungen und 105/105 CI-Prüfungen ausdrücklich belegt. Release ist öffentliches Prerelease, kein Entwurf. Botcommit `8400b2d` ändert nur `repo.json`; lokaler Branch abgeglichen. Anonyme JSON-/ZIP-/Prüfsummendownloads und erneute vollständige Paketvalidierung erfolgreich. Schreibfrei festgestellt: lokale Installation noch 0.1.1.2, Leistensichtbarkeit aus. Nach Nutzerupdate Leiste erneut aktivieren oder **Anzeigen und Position zurücksetzen** verwenden. Keine Konfiguration geändert; native Spielprüfung offen.

**Historischer Automatiknachweis: 0.1.1.2 veröffentlicht.** [Lauf 43243803893](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43243803893) zu `66cc76829aa185400a0ad136ea631309939574e9` besteht Build und Publish. Explizit belegt: 0 Warnungen/Fehler, 500/500 Funktionstests, 33/33 Paketprüfungen und 105/105 CI-Prüfungen (Policy33, Publish24, SDK44, Compiler4). Botcommit `14f92bb` aktualisiert ausschließlich `repo.json`; der lokale Branch wurde bereits per Fast-forward abgeglichen. Zum damaligen Nachweis war die öffentliche Version 0.1.1.2 und die unveränderte lokale Benutzerinstallation noch 0.1.0.0.

Quellrelease `v0.1.1.2` basiert auf `66cc768`; `14f92bb` ist der anschließende Indexcommit. Der Nutzer hat Commit und Push der Fixes ausdrücklich autorisiert; diese Arbeit ist erfolgt. Historische Fehler und die unabhängigen Fixprüfungen stehen in [STATUS.md](STATUS.md). Keine neue lokale Installation oder Spielabnahme durchgeführt.

Abschlussprüfung des UX-Quellstands: Release-Build 0 Warnungen/Fehler, Gesamtlauf 500/500 bestanden ohne Fehler/Skips, unabhängige Reviewfreigabe. Ein erster Gesamtlauf hatte den einmaligen bestehenden Theme-I/O-Testfehler; Einzeltest und komplette Wiederholung bestanden. Ursache unbelegt, kein Produktiv-I/O-Fix/Retrymechanismus; Details in [STATUS.md](STATUS.md). Die gesonderte Prüfung der Releaseautomatik folgt weiter unten.

Aktuelle lokale UI-Vorgabe: drei Hauptwege **Mein Charakter**, **Kontakte**, **Einstellungen**; Profilbereiche **Überblick**, **Charakter**, **OOC**. Eigenes Profil startet lesend, Bearbeiten hat Speichern/Verwerfen/Vorschau. Optionale Felder erscheinen gefüllt oder über **+ Angabe**. Profilvarianten, JSON und Werkzeuge liegen im Menü. Vorhandene zusätzliche Inhalte und Sprachen erhalten; Snapshot-/CAS-Schutz bewahren. Frühere Fünf-Bereiche-Vorgaben sind aufgehoben.

Die aktuelle eigene RP-Leiste zeigt ausschließlich das eigene aktive Profil, unabhängig vom Raw-Target oder einer gepinnten Karte. Einzeiliges Quelllayout mit 210 Pixeln Basisbreite, eingeklappt 32 Pixel; fremde Informationen gehören in die separate Ziel-/Mouseoveranzeige. Zielwechsel darf Entwurf, aktive Zuordnung oder Sharing nicht verändern. Vollständige Glance-Inhalte bleiben per Hover/Fokus/Klickpin mit Scrollen zugänglich; Owner-/Policywechsel invalidiert offene Inhalte.

`Collapsed` bleibt lokal rückwärtslesbar im Schema 1; Wiederöffner, Lock/Move/Scale und Viewportgrenzen erhalten. `Settings.Visible` bleibt alleinige Sichtbarkeitsvorgabe; den Fix aus 0.1.1.3 nicht zurückbauen. Dauerhaft sichtbare Hilfebadges entfallen, kontextuelle Hoverhinweise und zentrale Hilfe bleiben. Primäre Themes Obsidian/Pergament/Nachtblau; alle elf früheren IDs bleiben lesbar, ohne Datenmigration.

Die Automatik erzeugt nach relevanten Master-Pushes neue Version, Paket und Index. Vorhandene Releaseassets nicht überschreiben und `repo.json` nicht von Hand versionieren. Reale Spielabnahme bleibt offen. UX: [Benutzerhandbuch, Abschnitt 17](docs/benutzerhandbuch.md); Pipeline: [Releaseautomatik](docs/auto-release.md).

Die Pluginversionsbasis ist `0.2.0.0`; die CI verwendet `GITHUB_RUN_NUMBER - 1` als vierte Komponente. Öffentlich belegt ist jetzt 0.2.0.6. `Frp1Version` überschreibt nur die Pluginversion, FRP1.Core bleibt 1.0.0.0. .NET-SDK 10.0.401 und offizielles API-15.0.4-`DALAMUD_HOME` bleiben gepinnt. Buildjob nur lesend, Publish erst nach Erfolg mit integriertem `GITHUB_TOKEN`, kein PAT.

Release-Assets nicht überschreiben: identische Wiederholung wiederverwenden/no-op, abweichender Hash stoppen. Source-Head-/CAS-Prüfung muss veraltete Läufe vom Index fernhalten. `repo.json` erst nach öffentlichem ZIP-Download und Hashprüfung aktualisieren; Fehler dürfen den bisherigen Installerindex nicht unbrauchbar machen. Botcommit darf ausschließlich `repo.json` ändern. Dokumentations-/Indexpushes dürfen keine Releaseschleife auslösen.

Nach einem Botcommit den lokalen Branch vor dem nächsten Push abgleichen, etwa `git pull --ff-only`; bei Abweichungen prüfen statt Force-Push. Der erfolgreiche Hosted-Lauf und anonyme JSON-/ZIP-/Prüfsummendownloads sind jetzt nachgewiesen. ZIP umfasst 1.072.587 Bytes, SHA-256 `7B9C95D6977F100C14116CB5B2F567D7BA950D1431FA34CDDE179EBD2E910728`. Gemeinsame erneute Paketvalidierung erfolgreich. Die früheren lokalen Prüfungen und Fixregressionen sind in STATUS dokumentiert; reale Spiel-/Netzabnahme bleibt offen.

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
3. Eine vorhandene lokale Installation 0.1.0.0 durch den Nutzer im Dalamud-Installer auf den veröffentlichten Stand aktualisieren lassen. Kein manuelles Ersetzen alter Releaseassets; Updateverteilung ist bereits verifiziert.
4. Restliche Sprachlücken und nachgewiesene Fehler gezielt bearbeiten; keine unbeauftragten neuen Produktideen oder Taschen-/Inventarfunktionen hinzufügen. Builds/Tests nur bei neuen Änderungen oder offenen Fragen erneut ausführen.

## Reproduzierbare lokale Prüfung

```powershell
dotnet build FRP1.slnx -c Release --nologo
dotnet test FRP1.slnx -c Release --nologo -p:BuildInParallel=false
dotnet run --project tools/FRP1.Probe -c Release -- --self-test
```

Loopback belegt keine NAT-/CGNAT-Erreichbarkeit. Weitere Grenzen: [Machbarkeit](docs/feasibility.md), [Architektur](docs/architecture.md), [UX](docs/ux-and-vanity.md). TRP3-/Glance-Funktionen sind eigenständig implementiert; kein fremder Code/keine WoW-Assets übernommen. Die öffentliche Softwareverteilung ist verifiziert; tatsächliche Spiel-/Netzabnahme bleibt offen.

