# Status

Stand: 2026-11-12

**Planungsrunde am 2026-11-12 abgeschlossen:** [Integrationsplan](docs/integrationen-plan.md) für Honorific, Moodles, Glamourer, Customize+, Lifestream und eigene FRP1-IPC fertig. Planinhalt vollständig, alle vier Reviewanforderungen eingearbeitet; abschließender Reviewer-Abgleich der zuletzt ergänzten Queue-Klarstellung noch offen. Kontrollierter Checkpoint am 2026-11-12; am 2026-11-13 zuerst diesen einen Abgleich abschließen, danach WP1/Honorific lesen beginnen. Keine Integration implementiert, keine Builds/Tests/Runtimeänderungen. Produktstand `89b5e42` / öffentlich 0.2.0.4 unverändert. Nächster Schritt am 2026-11-13: zuerst den offenen Reviewer-Abgleich zur Queue-Klarstellung abschließen; anschließend Gitstatus/HEAD prüfen, WP1 und Honorific-Read-API-Spike; erstes MVP einschließlich bewusstem Entwurfsimport und minimalem Opt-in-/Importknopf. Bei Budgetstopp mit diesem Wiedereinstieg fortsetzen.

## Aktuell: kompakter UI-Neubau als 0.2.0.4 veröffentlicht

Die neue Oberfläche ist als 0.2.0.4 öffentlich veröffentlicht und über dieselbe Installeradresse verfügbar. Quellcommit ist `6c2e49115d764ae391922d5c5b3bc4f8fc70413e`; die Pluginversionsbasis bleibt 0.2.0.0, FRP1.Core bleibt 1.0.0.0. Bestehende Installationen über Dalamud aktualisieren und bei Bedarf neu laden. Keine Installation durch den Agenten durchgeführt.

Drei Haupteinstiege **Mein Charakter**, **Kontakte**, **Einstellungen** ersetzen die bisherige Navigation. Das eigene Profil startet als Lesekarte; Bearbeiten besitzt getrennte Aktionen für Speichern, Verwerfen und Vorschau. Editor und Leser haben **Überblick**, **Charakter**, **OOC**; optionale Angaben erscheinen gefüllt oder über **+ Angabe**. Profilvarianten, JSON und Werkzeuge liegen im Menü. Kontakte bündeln Liste, Auswahlaktionen und Umgebung; Netzwerkdetails liegen unter **Erweitert**. Sichtbare Hilfebadges entfallen zugunsten von Hoverhinweisen und zentraler Hilfe.

Die eigene RP-Leiste bleibt immer beim eigenen Profil, unabhängig vom Ziel: eine Zeile mit 210 Pixeln Basisbreite, eingeklappt 32 Pixel. Fremde Informationen bleiben in separater kompakter Ziel-/Mouseoveranzeige. Das Hauptfenster verwendet eine neue Kennung und beginnt bei 560 × 430 Pixeln vor Skalierung und Viewportbegrenzung. Primäre Themes sind Obsidian, Pergament und Nachtblau; alte elf Theme-IDs bleiben ohne Datenmigration lesbar.

Profilschema, private Registerdaten, DPAPI-Identität, Kopplungen und direkte Clientfreigaben bleiben erhalten. Audience-, Block-, Trust-, Revisions-/Hash- und Sichtbarkeitsschutz bleiben maßgeblich. Die früheren Vorgaben für fünf Profilbereiche, zielabhängiges eigenes HUD und elf sichtbare Volksthemes wurden vom Nutzer ausdrücklich aufgehoben; historische Beschreibungen unten sind keine aktuellen Umsetzungsvorgaben.

Lokaler Vorabnachweis: 42 gezielte Tests und 510/510 vollständige Funktionstests (27 Sekunden, keine Fehler/Skips), voller Release-Solutionbuild gegen gepinntes API 15/.NET-SDK 10.0.401 mit 0 Warnungen/Fehlern (2,35 Sekunden) und 33/33 lokale Paketprüfungen für 0.2.0.0. Unabhängiges Review: Freigabe ohne kritische oder wichtige Restbefunde. Der erfolgreiche [Hosted-Lauf 43296113100](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43296113100) bestätigt zusätzlich Build 0 Warnungen/Fehler, 510/510 Funktionstests (35 Sekunden), 33/33 Paketprüfungen und alle 105/105 CI-Prüfungen (Policy33, Publish24, SDK44, Compiler4). Build und Publish sind erfolgreich. Native Spiel-/Skalierungs- und reale Netzabnahme bleiben offen. Datiertes Quellbackup vor Neubau: `.release-staging/ui-rebuild-backup-20261112T194250Z`, Ausgangscommit `b9d2bb7`, SHA-256 `378594B3D74ABA2A49CE85E11CDA83F6A281621CBFBF0F188AAD4B4172F46885`.

Öffentlicher Nachweis: [Release v0.2.0.4](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.4) ist öffentlich, als Prerelease markiert und kein Entwurf. Botcommit `f2071ac` aktualisiert ausschließlich `repo.json`; der lokale Branch ist abgeglichen. Die kanonische Raw-JSON-Adresse liefert anonym HTTP 200 und Version 0.2.0.4. ZIP und `SHA256SUMS` sind ebenfalls anonym geladen, gemeinsam per SHA-256 geprüft und mit `Test-ReleasePackage` erneut vollständig validiert.

| Datei / Feld | Nachweis für 0.2.0.4 |
|---|---|
| ZIP-Größe | 1.079.993 Bytes |
| ZIP SHA-256 | `00F3974603E8442B4574EBFCD116B7A02A8F21A74B965B3D575BD11FE9A00AE2` |
| JSON SHA-256 | `0C77B7CB19AE8259E2344BD3BD6009924C439CED6410AEC0DE2F02B1BDDD11D5` |
| LastUpdate | `1794513688` |

## Vorheriger Stand: RP-Leistenfix als 0.1.1.3 veröffentlicht

Quellcommit `809d9961929e38257386f0138015834a59c15726` korrigiert einen Rückkopplungsfehler: Nach Aktivierung der RP-Leiste oder Positionsreset konnte das noch geschlossene Fenster die gespeicherte Sichtbarkeit sofort wieder ausschalten. Die ausdrücklich gewählte Sichtbarkeit ist jetzt die einzige Vorgabe; der vorübergehende Fensterzustand überschreibt sie nicht mehr. Bestehende Profil-/Alters-/Szenenschutzprüfungen bleiben erhalten.

Lokal bestehen Release-Build mit 0 Warnungen/Fehlern und 76/76 gezielte Tests für Profilsitzung, Leisteneinstellungen und Kontext. Unabhängiges Review: Freigabe ohne kritische oder wichtige Restbefunde. Der erfolgreiche [Actions-Lauf 43284625889](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43284625889) bestätigt zusätzlich Build 0 Warnungen/Fehler, 500/500 Funktionstests (34 Sekunden), 33/33 Paketprüfungen und alle 105/105 CI-Prüfungen (Policy33, Publish24, SDK44, Compiler4). Build und Publish sind erfolgreich. Die native Bedienung im laufenden Spiel ist weiterhin nicht geprüft.

Eine schreibfreie Prüfung zeigt inzwischen eine vorhandene lokale Installation 0.1.1.2 und ausgeschaltete Leistensichtbarkeit. Konfiguration und Installation wurden durch diese Prüfung nicht verändert.

[Release v0.1.1.3](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.1.1.3) ist öffentlich, als Prerelease markiert und kein Entwurf; Quelle ist `809d9961929e38257386f0138015834a59c15726`. Botcommit `8400b2d` aktualisiert ausschließlich `repo.json`; der lokale Branch ist abgeglichen. Kanonischer Raw-Index, ZIP und `SHA256SUMS` wurden anonym per HTTP 200 geladen, gemeinsam per SHA-256 geprüft und erneut vollständig paketvalidiert. ZIP: 1.072.632 Bytes, SHA-256 `74B8C6A87C0C4B718C38B85CDF7E276DEEB312A19FF4A6716550CB7F13AB6B6B`. JSON SHA-256: `934DC9A00CA5120051925753A50107E32BFACC5B0E043292EEFECD1949536FF8`; LastUpdate `1794509650`.

Nach dem Update auf 0.1.1.3 die Leistensichtbarkeit erneut aktivieren oder **Anzeigen und Position zurücksetzen** verwenden. `/frpbar` beziehungsweise `/frp bar show` zeigt die Leiste ebenfalls an. Die vor dem Fix ausgeschaltete gespeicherte Wahl wird nicht automatisch geändert.

## Vorheriger Stand: 0.1.1.2 vollständig geprüft und automatisch veröffentlicht

Die UX aus Nutzercommit `62a91a7` ist veröffentlicht. Der öffentliche Installerindex enthält 0.1.1.2/API 15. Beim damaligen Veröffentlichungsnachweis stand die unveränderte lokale Benutzerinstallation noch auf 0.1.0.0; die aktuelle schreibfreie Prüfung oben zeigt inzwischen 0.1.1.2.

Der erfolgreiche [Actions-Lauf 43243803893](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43243803893) baut Quellcommit `66cc76829aa185400a0ad136ea631309939574e9`: Build 0 Warnungen/Fehler, 500/500 Funktionstests (35 Sekunden), 33/33 Paketprüfungen und 105/105 CI-Prüfungen. Alle Gruppen sind ausdrücklich im Hosted-Protokoll belegt: 33 Policy, 24 Publishabläufe, 44 SDK und vier Compilerfälle im Vorbereitungsschritt. Buildjob und Publishjob sind erfolgreich (2:19 beziehungsweise 1:26 Minuten).

Historie: Lauf 43239620147 zu `ffe036c` scheiterte nach Build/Tests an `Unmapped embedded PDB source path.`. SourceRoot-Fix `44c2263` korrigierte PDB und SourceLink auf `/_/FRP1/*`; vier Compilerregressionen und ihre Negativkontrollen bestätigten den Fix. Lauf 43242243046 veröffentlichte danach 0.1.1.1 mit der neuen UX, führte aber wegen eines vorzeitigen erfolgreichen Testskriptendes die 101 Helferprüfungen nicht aus. Fix `66cc768` setzt den erfolgreichen Skriptausgang ausdrücklich auf 0. Frische Shellproben bestätigten danach Paket33 plus Helfer101 und eine nachfolgende Kontrollmarkierung; ein absichtlich falscher Versionsfall stoppt mit Exit 1 vor den Helfern. Beide Fixes sind ohne kritische oder wichtige Restbefunde reviewfreigegeben. Der neue Lauf prüft alle Gruppen und veröffentlicht 0.1.1.2.

Nachgewiesen, ausschließlich lokal: frisch geladenes offizielles Dalamud 15.0.4/API 15 mit SHA-/Metadatenprüfung, .NET-SDK 10.0.401, Locked-Restore und ganzer Release-Build gegen dieses `DALAMUD_HOME` mit 0 Warnungen/Fehlern; 500/500 Funktionstests bestanden (26 Sekunden). FRP1.Core bleibt 1.0.0.0. Zusätzlich bestehen 33/33 Paketprüfungen und 101/101 Offline-CI-Prüfungen: 33 Versions-/Releasepolicyfälle, 24 Veröffentlichungsabläufe und 44 SDK-/Pfad-/Metadatenfälle. Die unabhängige statische Workflowprüfung mit actionlint 1.7.12 besteht ohne Meldungen. Das abschließende unabhängige Review ist ohne kritische oder wichtige Restbefunde freigegeben; daraus wird kein gehosteter Erfolg abgeleitet.

Kandidat 0.1.1.0 ist vollständig paketvalidiert. Sein ZIP umfasst 1.072.715 Bytes mit SHA-256 `3D8631338B00A89B065EF3BB305D5EA3DB3D9FA5F9289640B331F9DCE940216D`. Zwei lokale Erstellungen ergeben byteidentische ZIP-, Index- und Prüfsummendateien. Die Prüfung belegt die lokale Erstellung und Wiederholbarkeit; der öffentliche 0.1.0.0-Index wurde nicht verändert.

Öffentlicher Nachweis: [Release v0.1.1.2](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.1.1.2) ist ein öffentliches Prerelease, kein Entwurf, und verweist auf Quellcommit `66cc768`. Botcommit `14f92bb` ändert ausschließlich `repo.json`; der lokale Branch ist abgeglichen. Die kanonische Raw-JSON-Adresse liefert anonym HTTP 200 und 0.1.1.2. ZIP und `SHA256SUMS` wurden ebenfalls anonym geladen, gemeinsam per SHA-256 geprüft und mit `Test-ReleasePackage` erneut erfolgreich validiert.

| Datei / Feld | Nachweis für 0.1.1.2 |
|---|---|
| ZIP-Größe | 1.072.587 Bytes |
| ZIP SHA-256 | `7B9C95D6977F100C14116CB5B2F567D7BA950D1431FA34CDDE179EBD2E910728` |
| JSON SHA-256 | `5446762ED99309BDFEF603BB803F6AB5A4B1918B6A80BDA78598B9921F31DE99` |
| LastUpdate | `1794495856` |

## UX seit veröffentlichtem 0.1.1.1, vollständig geprüfter Stand 0.1.1.2

Fünf Bereiche, kompaktes HUD und feldnahe Hilfen sind im öffentlichen Installerpaket 0.1.1.2 enthalten; erstmals ausgeliefert wurden sie mit 0.1.1.1. Die historische 0.1.0.0-Veröffentlichung bleibt weiter unten getrennt dokumentiert.

- Editor und Leser teilen genau fünf gleichrangige Bereiche: RP-Status und Kurzprofil; OOC Informationen; Auf den ersten Blick; Weitere Charakterdaten; RP-Erfahrung und Absprachen.
- Kleiner Profilkopf, ein Inhaltsbereich, Gestaltung im Kopf-Popup und JSON unter Profilaktionen. Keine zweispaltige Vorschau oder Neuanlagebaukästen für Hooks/Extras/Traits/Begleiter.
- Bestehende Zusatzinhalte bleiben lesbar und gezielt aus dem Entwurf entfernbar; sie bleiben bis zur Entfernung öffentlich und werden vor Export/Teilen kenntlich gemacht. Deaktivierte Glances sind standardmäßig unsichtbar; unbearbeitete Sprachen bleiben unverändert.
- Zweizeiliges MiniHUD mit Ich-/Ziel-/Neutral-Kontext, einklappbarem lokalem Zustand, Wiederöffner und Kontextmenü. Fremde Inhalte sind nur lesend; ungültige Ziele verwenden keinen eigenen Profilfallback. Die Kontextquelle ist das tatsächliche Ziel, nicht eine gepinnte Zusatzkarte.
- Vollständige scrollbare Glance-Inhalte bei Hover/Fokus, unabhängig von Hilfshinweisen; rund 500 ms Mausübergang und optionales Anheften. Kontext-/Policywechsel invalidiert offene Inhalte.
- Feldnahe beschriftete Hilfen und sechs neue Hilfethemen. Neue Einstellungen lassen die große Zusatz-Zielkarte zunächst geschlossen; vorhandene Entscheidungen bleiben bestehen.

### Prüfung des lokalen UX-Stands

Release-Build: **0 Warnungen, 0 Fehler** (1,08 Sekunden). Abschließender Gesamtlauf: **500/500 bestanden**, 0 Fehler, 0 übersprungen (26 Sekunden); die 33 neuen UX-Fälle sind enthalten. Unabhängiges Review: Freigabe ohne kritische oder wichtige Restbefunde. Keine Abnahme im laufenden Spiel erfolgt.

Testhistorie: Der erste Gesamtlauf bestand 499/500; im bereits vorhandenen unveränderten Test `ThemeTests.ConcurrentChangesPublishOneCompleteChoiceWithoutLeavingTemporaryFiles` trat einmalig eine `UnauthorizedAccessException` bei `File.Move` mit Überschreiben (`ThemeSettingsStore`, Zeile 71) auf. Der Einzeltest mit identischen Artefakten bestand anschließend 1/1 (55 ms), danach der vollständige Wiederholungslauf 500/500. Die Ursache ist nicht belegt; es wurden weder ein Produktiv-I/O-Fix noch automatische Test-Retries oder übersprungene Tests eingeführt.

Die früheren 33 Offline-Paketprüfungen und öffentlichen Download-/Hashnachweise gelten für das unveränderte Release 0.1.0.0, nicht als Abnahme der neuen Automatik oder eines neueren Installerpakets. Nach Einführung soll die Automatik neue Version/Paket/Index erzeugen; 0.1.0.0 nicht ersetzen.

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

Eine Installation beziehungsweise reale Spiel-/Netzabnahme wurde nicht durchgeführt. Quellcode, MIT-Lizenz und Installerverteilung 0.1.1.2 sind veröffentlicht und auf Erreichbarkeit und Paketintegrität geprüft. Vollständige Zuordnung: [TRP3-Matrix](docs/trp3-feature-map.md), [Glance-Matrix](docs/glance-feature-map.md).

