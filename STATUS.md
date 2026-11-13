# Status

Stand: 2026-11-13

## Aktueller Checkpoint: WP5 Moodles als 0.2.0.7 veröffentlicht

**Quellstand `1d5f7749cabfad47ad4636398ef524a71605d331`, veröffentlicht am 2026-11-13:** Moodles ist unter **Einstellungen → Integrationen** standardmäßig ausgeschaltet. Der Adapter liest ausschließlich eigene aktive Zustände (API 4, Quellformat 2). Höchstens fünf lokale Bindungen halten Status-ID, Inhalts-Hash und ein ausdrücklich bestätigtes General-/Mature-Rating. Titel, Text und Icon sind an den Hash gebunden; geänderte Inhalte gelten wieder als ungeprüft. Ungeprüfte und Mature-Vorschauen erfordern aktuelle Adult-Auswahl samt Mature-Opt-in; ein passendes geprüftes General-Rating erlaubt die Vorschau nach erfolgter Alterswahl. Bewertung selbst verlangt Adult samt Mature-Opt-in. Kein automatisches General-Rating.

Die ausdrückliche Aktion **Als Eindruck übernehmen** hängt einen Glance an den unveränderten gespeicherten eigenen Entwurf an; insgesamt höchstens fünf Glances. Mature-Import ist nur in einen Mature-Entwurf mit aktueller Adult-/Mature-Freigabe erlaubt. Titel höchstens 80, Beschreibung höchstens 1024 Zeichen; zu große Inhalte werden abgewiesen, nicht gekürzt. Anschließend regulär prüfen und speichern. Bestehende Profilfreigaben gelten für gespeicherte Inhalte weiter; der Import erteilt keine neue Netzwerkfreigabe. Keine Moodles-Apply-/Set-Aufrufe, Fremdreads oder neuen Profil-/Wirefelder.

Lokale Einstellungen bleiben in `integrations.json`, Schema 1 mit optionalen Moodles-Feldern, **8 KiB** Gesamtgrenze und Lesbarkeit älterer Dateien ohne diese Felder. Hintergrundaktualisierungen sind auf höchstens **1 Hz** begrenzt. Bewusste Bewertung und Import lesen zusätzlich frisch; beide Aktionen teilen sich einen **500-ms-Abstand**. Actor-/Profil-/Policy-/Entwurfswechsel, Busy, Quelländerung und Unload verwerfen veraltete Arbeit.

Lokaler WP5-Nachweis laut abgeschlossener Teamprüfung: voller Release-Solutionbuild **0 Warnungen/Fehler**, **248 gezielte Tests**, **1019/1019 vollständige Funktionstests** in 28 Sekunden ohne Fehler/Skips, **33/33 Paketprüfungen**, unabhängige Reviewfreigabe ohne wichtige Restbefunde. Die synthetische SDK-/Newtonsoft-Probe für das 16-Felder-Tuple einschließlich Enum-Konvertierung besteht; dies ist ausschließlich ein synthetischer Nachweis. Native Moodles-/IPC-/Spiel-/UI-QA bleibt offen, keine Aktivierung durch den Agenten.

[Hosted-Lauf 43419504299](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43419504299) besteht Build und Publish: **0 Warnungen/Fehler**, **1019/1019 Funktionstests** in 32 Sekunden, **33/33 Paketprüfungen**, **105/105 CI-Prüfungen** (Policy33, Publish24, SDK44, Quellpfade4); alle Ergebnisse ausdrücklich in den Logs bestätigt. [Release v0.2.0.7](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.7) und kanonischer Raw-Index sind öffentlich; Index anonym HTTP 200 mit Version 0.2.0.7, ZIP und `SHA256SUMS` anonym geladen, Hashvergleich und vollständige erneute Paketvalidierung bestanden. Botcommit `de8a29d` ändert ausschließlich `repo.json`; lokaler Branch per Fast-forward abgeglichen. Das ist ein Distributionsnachweis, keine native Spielabnahme.

| Datei / Feld | Nachweis für 0.2.0.7 |
|---|---|
| ZIP-Größe | 1.123.824 Bytes |
| ZIP SHA-256 | `E6AE80825161D6C3DC0CC61874A042EF473F664BAD933E26B0C9D2403ACF504D` |
| JSON SHA-256 | `ED2E67B29314896F55509FCA77A4AAFBD2864E623234CE3083C95D6F74D3E123` |
| LastUpdate | `1794570287` |

Verifiziertes Quellbackup vor WP5: `.release-staging/before-moodles-20261113T112856Z`, Quelle `7666329`, SHA-256 `32CABC658791D3436FC4F8546EB88AD44D8052CEB81786BF8ACFD7072F3A2006`. WP3-Ausbau und WP6–8 bleiben separate Pakete. Bedienung: [Handbuch, Abschnitt 22](docs/benutzerhandbuch.md#22-moodles-lokal-auswählen-wp5).

**WP7 Lifestream ist am 2026-11-13 nach dem begrenzten Adress-/Abbruchabgleich in Umsetzung (`lifestream_impl`); noch kein abgeschlossener Funktions-/Prüf-/Releaseumfang.** Verifiziertes lokales Quellbackup vor WP7: `.release-staging/before-lifestream-20261113T114933Z`, Quelle `de8a29d`, SHA-256 `B1EEBDB6E65E8941DD3D8732EF111AF6124796F76C2CF06B4B16DC52FAA23A82`. WP6-Glamourer-Schreiben ist nach begrenztem API-1.8-/Quellabgleich blockiert: `ApplyDesign(Once | Equipment, key: 0)` kann über verknüpfte AssociatedMods Penumbra ändern; ActorIndex-/Identitäts-/Kopienscope ist ebenfalls ungeklärt. Kein WP6-Produktcode, keine Dummyoberfläche oder native Abnahme. Belegte Quellkette und Grenzen: [Integrationsplan, WP6](docs/integrationen-plan.md#wp6--eigener-glamourer-equipment-look).

## Vorheriger Stand: WP4 als 0.2.0.6 veröffentlicht

**WP4 als 0.2.0.6 veröffentlicht am 2026-11-13:** `6b97c1bd556f98d7d6295954d4a371e5e7c32f41` implementiert [FRP1-IPC v1](docs/frp1-ipc-v1.md) mit API 1.0, Capabilities/Readiness, General-only-Status/Verfügbarkeit und separat freizugebenden eigenen Aktionen samt Ticketabfrage. Alle fünf Freigaben standardmäßig aus. Aktives eigenes Profil unabhängig von der Editorauswahl; keine privaten Datenexporte. Mature-Steuerung nur mit aktueller Adult-Auswahl und zusätzlichem Mature-Opt-in, lesbarer Export bleibt General-only. Native-freie IPC-Callbacks, begrenzte Frame-Queue, synchroner Policy-Epochenwechsel und generationengebundene Ergebnisse sichern Kontext und Widerruf. `Persisted` erst nach erfolgreichem eigenen Schreibauftrag und Übernahme genau dieses gespeicherten Profilstands; das Poll-Zeitfenster bleibt `Running`, echte Konflikte werden `Stale`.

Lokaler WP4-Nachweis: **812/812 Funktionstests** (27 Sekunden; 150 zusätzlich zum 662er-MVP), **287 gezielte Tests**, voller Release-Solutionbuild mit **0 Warnungen/Fehlern** (2,54 Sekunden) und **33/33 Paketprüfungen**. Unabhängiges abschließendes Review ohne kritische/wichtige Restbefunde freigegeben. Registrierung und Pluginverdrahtung sind statisch geprüft und kompiliert; native IPC-/Spielprobe steht aus.

Der [Hosted-Lauf 43416697284](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43416697284) besteht beide Jobs Build/Publish: 0 Warnungen/Fehler, **812/812 Funktionstests** (32 Sekunden), **33/33 Paketprüfungen** und **105/105 CI-Prüfungen** (Policy33, Publish24, SDK44, Quellpfade4), alle Ergebnisse ausdrücklich in den Logs bestätigt. [Release v0.2.0.6](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.6) und kanonischer Raw-Index sind öffentlich; JSON anonym HTTP 200, ZIP und `SHA256SUMS` anonym geladen, Hashvergleich und erneute vollständige Paketvalidierung erfolgreich. Botcommit `7666329` ändert nur `repo.json`; lokaler Branch per Fast-forward abgeglichen.

| Datei / Feld | Nachweis für 0.2.0.6 |
|---|---|
| ZIP-Größe | 1.110.041 Bytes |
| ZIP SHA-256 | `E4DBF184BC5F09F6F54307CEE2227DC374DD206FDF7816C539AB00C2FD8C3450` |
| JSON SHA-256 | `F045E351A03AE6F7A47C38E120343F0155A643F38D92A6BABD401F57BD90C57A` |
| LastUpdate | `1794569012` |

**Lokale I/O-Korrektur im selben Quellstand:** Das MVP-Instanzgate verhinderte den sporadischen Windows-Fehler bei `File.Move` mit Überschreiben nicht vollständig; die Ursache der zeitweise offenen Handles ist unbelegt. `LocalJsonFile` ersetzt bestehende Zieldateien jetzt mit `File.Replace` und eindeutig benannter `.previous`-Sicherung. Erst nach erfolgreichem Replace wird die Sicherung entfernt; nach einem teilweisen Replacefehler bleibt sie zur Wiederherstellung erhalten. Erstanlage verwendet `File.Move` ohne Überschreiben. Instanzgate erhalten, keine instanzübergreifende Garantie oder Retries. Synthetische Diagnose mit 23.000 Vorgängen und Sicherung erfolgreich; Regressionen mit offenem bzw. blockierendem Leser und fünf Own-Poll-/Refresh-/Scope-Fälle sind im Prüfstand enthalten. Keine belegte Aussage zu Virenscannerursachen.

Historischer Folgeauftrag von WP4 war WP5 nach begrenztem Moodles-Quell-/API-/Ratingabgleich; das Moodles-Manifest 1.1.3.5/API 15 wurde schreibfrei erfasst. Der abgeschlossene lokale WP5-Nachweis und die erfolgreiche Veröffentlichung stehen im obersten Checkpoint. Native Spiel-/Netzabnahme bleibt offen.

## Vorheriger Stand: Honorific-MVP als 0.2.0.5 veröffentlicht

**Integrationen, MVP als 0.2.0.5 veröffentlicht am 2026-11-13:** WP1/WP2 des [freigegebenen Integrationsplans](docs/integrationen-plan.md) sind umgesetzt und unabhängig ohne wichtige Restbefunde reviewfreigegeben. [Release v0.2.0.5](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.5) ist öffentlich, als Prerelease markiert und kein Entwurf; Quelle `bae4ef86c6e812c45536f73db7ea356fc5ce1e31`. Die standardmäßig ausgeschaltete Honorific-Integration liest ausschließlich den eigenen Titel, zeigt Status/Vorschau und übernimmt ihn nur bewusst in einen unveränderten gespeicherten eigenen Entwurf. Erst normales Speichern persistiert den Profilinhalt. Aktuelle Actor-/Policy-/Kontextgeneration, Busy/Draft-CAS und Frame-Queue sichern die Übernahme; bei bestätigtem Kontext überlässt FRP1 ausschließlich den eigenen nativen Titelslot Honorific. Keine Honorific-Set-/Clear-/SetIdentity-Aufrufe oder automatischen Privat-/Mature-Exporte. Native IPC-/Spielabnahme bleibt offen; keine Aktivierung durch den Agenten.

Lokaler Nachweis: 152 neue Headless-Tests einschließlich elf Unicodefällen plus 510 bisherige Tests, insgesamt **662/662 bestanden** in 27 Sekunden, ohne Fehler/Skips. Voller Release-Solutionbuild gegen API 15/.NET-SDK 10: **0 Warnungen/Fehler** (1,03 Sekunden); **33/33 Paketprüfungen** bestanden. Der Titelparser verwirft doppelte JSON-Schlüssel auch in ignorierten Feldern sowie ungültiges UTF-16 ohne Ersatzzeichen; elf Unicodefälle decken die Korrektur der zunächst acht fehlgeschlagenen Exceptionfälle ab. Bei ausgeschalteter Integration wird kein Entwurf erfasst; regulärer Draft-Cache und frische CAS-Prüfung bei Import erhalten die Gültigkeit. `LocalJsonFile` serialisiert Laden/Speichern über das bestehende Instanzgate gegen das beobachtete Windows-Rename-/Leserennen; keine Garantie über mehrere Instanzen und keine Retries. Diese Prüfungen belegen keine native IPC-/Spielausführung.

Der erfolgreiche [Hosted-Lauf 43354035033](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43354035033) bestätigt Build/Publish erfolgreich, Build mit 0 Warnungen/Fehlern, 662/662 Funktionstests (33 Sekunden), 33/33 Paketprüfungen und 105/105 CI-Prüfungen (33 Policy, 24 Publishabläufe, 44 SDK, vier Quellpfadfälle). Botcommit `a655c30` ändert ausschließlich `repo.json`; der lokale Branch ist per Fast-forward abgeglichen. Die kanonische Raw-JSON-Adresse liefert anonym HTTP 200 und 0.2.0.5. ZIP und `SHA256SUMS` sind ebenfalls anonym geladen, Hashes passend und vollständige erneute Paketvalidierung bestanden.

| Datei / Feld | Nachweis für 0.2.0.5 |
|---|---|
| ZIP-Größe | 1.094.651 Bytes |
| ZIP SHA-256 | `432AA0E6177B8753462397AF392F987086E008D4D2D42C7EAD340E0C7CD3659D` |
| JSON SHA-256 | `F3BA5D2E5495DF8B2EAB2F55DF9267FE1790771D5A36CF482FB6F6041ABF49BE` |
| LastUpdate | `1794537751` |

Historischer Folgeauftrag des MVP: WP4 umsetzen und prüfen. Der aktuelle abgeschlossene Quell-/Prüf-/Veröffentlichungsstand steht im obersten Checkpoint. Die Arbeit geht paketweise mit belastbaren Checkpoints weiter; das MVP beendet den Gesamtauftrag nicht.

Verifiziertes Quellbackup vor Integrationen: `.release-staging/integrations-backup-20261113T022114Z/source-before-integrations.zip`, Quelle `e63b2387f266be6b4f411e8eb49b7fe1b3925348`, SHA-256 `C34A4EE5D471D763B6F8E9808839A64E4B619F31AA1FE27DE4ABDA7CBD049A7C`. Historischer Planungsstand vom 2026-11-12: `89b5e42`, ohne damalige Code-/Runtime-/Releaseänderungen oder Builds/Tests.

## Vorheriger Stand: kompakter UI-Neubau als 0.2.0.4 veröffentlicht

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

