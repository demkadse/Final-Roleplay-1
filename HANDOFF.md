# Übergabe

Stand: 2026-11-11

## Aktueller Arbeitsfokus

Alle wesentlichen Module sind im nativen Plugin verdrahtet; die frühere Beschreibung als lokaler Editor mit separatem Transport ist überholt. Social-/Nameplate-Icon-Ergänzungen und letzte Anzeige-/Performancekorrekturen sind enthalten. Danach bestehen der vollständige Release-Build mit null Warnungen/Fehlern und die Gesamtsuite mit **467/467 Tests**, ohne Fehler/übersprungene Tests. Das endgültige unabhängige Review ist freigegeben; keine kritischen oder wichtigen Befunde verbleiben. Die separate Probe besteht mit acht echten Loopback-TLS-Prüfgruppen. Reale Spiel-/Netzabnahme ist weiterhin offen.

Der Quellcode ist im bestehenden privaten Repository `demkadse/Final-Roleplay-1`, Branch `master`, Ausgangsstand `46ad78e` hochgeladen. Die ausdrückliche Folgefreigabe umfasst öffentliche Sichtbarkeit und MIT im selben Repository; die öffentliche Installerverteilung ist noch nicht bestätigt. Eine lokale Spielinstallation ist nicht erfolgt. Projektanweisungen beachten und fremde/uncommittete Änderungen erhalten. Keine bereits angebundenen Funktionen erneut als offen planen. [STATUS.md](STATUS.md) und [TRP3-Funktionsmatrix](docs/trp3-feature-map.md) sind der Einstieg.

## Beauftragte Veröffentlichung

Bestehendes Ziel: `demkadse/Final-Roleplay-1`, Branch `master`, Tag `v0.1.0.0`, Asset `FRP1-0.1.0.0.zip`. Installeradresse: `https://raw.githubusercontent.com/demkadse/Final-Roleplay-1/master/repo.json`. Öffentliche Sichtbarkeit und MIT sind bereits autorisiert; kein neues Repository und keine erneute Nutzerfreigabe als routinemäßigen Zwischenstopp einführen.

Root führt Repository-/Releaseaktionen aus. Erst nach bestätigtem Erfolg Veröffentlichungsvorbereitung in README/STATUS/Handbuch/Release-Notes auf tatsächliche Verfügbarkeit umstellen. Die konkreten Texte sind vorbereitet; weder einen Upload noch einen getesteten öffentlichen Download allein aus lokalen Dateien ableiten.

GitHub ist ausschließlich Softwareverteilung, kein FRP1-Profilbackend. Öffentliche JSON-/ZIP-Dateien benötigen nach Veröffentlichung kein GitHub-Konto; der Pluginbetrieb ebenfalls keinen Account. Öffentliche Profile gehen nur direkt an ausdrücklich gekoppelte/freigegebene FRP1-Clients. Private Notizen, Altersauswahl, Content-IDs, Zuordnungen und Einstellungen bleiben lokal. Der Publisher ergänzt parallel LICENSE, Paket-/Indexziele und Fremdkomponentenunterlagen; die Dokumentationsrolle ändert diese Dateien nicht.

Projektcode und Dokumentation stehen unter [MIT](LICENSE), einschließlich kommerzieller Weiterverwendung unter Erhalt der Hinweise. Nutzerprofile/Bilder und fremde Inhalte sind davon nicht automatisch erfasst; keine Rechteübertragung dieser Inhalte behaupten. Die Release-Paketprüfung ist zusätzlich zum bestehenden Funktionsteststand auszuwerten.

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
3. Restliche Sprachlücken und nachgewiesene Fehler gezielt bearbeiten; keine unbeauftragten neuen Produktideen oder Taschen-/Inventarfunktionen hinzufügen. Builds/Tests nur bei neuen Änderungen oder offenen Fragen erneut ausführen.

## Reproduzierbare lokale Prüfung

```powershell
dotnet build FRP1.slnx -c Release --nologo
dotnet test FRP1.slnx -c Release --nologo -p:BuildInParallel=false
dotnet run --project tools/FRP1.Probe -c Release -- --self-test
```

Loopback belegt keine NAT-/CGNAT-Erreichbarkeit. Weitere Grenzen: [Machbarkeit](docs/feasibility.md), [Architektur](docs/architecture.md), [UX](docs/ux-and-vanity.md). TRP3-/Glance-Funktionen sind eigenständig implementiert; kein fremder Code/keine WoW-Assets übernommen. MIT ist beschlossen; öffentliche Erreichbarkeit erst nach tatsächlicher Verifikation bestätigen.

