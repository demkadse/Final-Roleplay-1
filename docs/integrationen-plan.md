# Plan für lokale Pluginintegrationen und FRP1-IPC

Stand: 2026-11-13 · **WP1/WP2 als 0.2.0.5 und WP4 als 0.2.0.6 veröffentlicht.** WP4: 812/812 lokale Funktionstests (27 Sekunden; 150 zusätzlich zum MVP), 287 gezielte Tests, voller Release-Solutionbuild gegen API 15/.NET-SDK 10 mit 0 Warnungen/Fehlern (2,54 Sekunden), 33/33 Paketprüfungen und unabhängige Reviewfreigabe ohne kritische/wichtige Restbefunde. [Hosted-Lauf 43416697284](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43416697284) zu `6b97c1b` besteht Build/Publish mit 0 Warnungen/Fehlern, 812/812 Funktionstests (32 Sekunden), Paket33 und CI105. Anonyme JSON-/ZIP-/Hash-/Paketprüfung erfolgreich, Botcommit `7666329` nur für `repo.json` per Fast-forward abgeglichen. Registrierung/native Verdrahtung statisch geprüft und kompiliert, native IPC-/Spielabnahme offen. WP5 ist im Quellstand `1d5f774` lokal abgeschlossen und reviewfreigegeben: Build 0 Warnungen/Fehler, 248 gezielte und 1019/1019 vollständige Funktionstests (28 Sekunden, keine Fehler/Skips), Paket33. Als 0.2.0.7 veröffentlicht; [Hosted-Lauf 43419504299](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43419504299) besteht Build/Publish mit 0 Warnungen/Fehlern und 1019/1019 Funktionstests (32 Sekunden), Paket33 und CI105 (Policy33, Publish24, SDK44, Quellpfade4). Anonyme Index-/ZIP-/Hash-/Paketprüfung bestanden; native Moodles-QA offen. WP3-Ausbau und WP6–8 bleiben separate Pakete.

Bestätigter öffentlicher Stand: [Release v0.2.0.8](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.8), Quelle `5c01d545de0330ac6231175c617f8801453def33`, Botcommit `5cc6ab3` nur für `repo.json` per Fast-forward abgeglichen. [Hosted-Lauf 43462921783](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43462921783) besteht Build/Publish mit **0 Warnungen/Fehlern**, **1201/1201 Funktionstests** (36 Sekunden), **33/33 Paketprüfungen** und **105/105 CI-Prüfungen** (Policy33, Publication24, SDK44, Quellpfade4). [Release v0.2.0.8](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.8) und kanonischer Raw-Index öffentlich; Index anonym HTTP 200 mit Version 0.2.0.8, ZIP und `SHA256SUMS` anonym geladen, Hashvergleich und vollständige Paketvalidierung bestanden. Botcommit `5cc6ab3` ändert nur `repo.json`; per Fast-forward abgeglichen. Vorheriger MVP-Nachweis: [0.2.0.5](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.5), Quelle `bae4ef86c6e812c45536f73db7ea356fc5ce1e31`, [Hosted-Lauf 43354035033](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43354035033) mit 662/662 Funktionstests (33 Sekunden), Paket33 und CI105. Details und Hashes in [STATUS](../STATUS.md). Abschließender Planreview und beide Code-Reviews freigegeben; keine automatische Aktivierung.

Die Planungsrunde am **2026-11-12** beschränkte sich auf Planung und Dokumentation zu `89b5e42`, ohne damalige Produktcode-/Runtime-/Releaseänderung oder Builds/Tests. Die Umsetzung am **2026-11-13** begann auf Quellbasis `e63b238` bei öffentlichem Stand 0.2.0.4; inzwischen ist das Honorific-MVP als 0.2.0.5 veröffentlicht. Verifiziertes Quellbackup: `.release-staging/integrations-backup-20261113T022114Z/source-before-integrations.zip`, Quelle `e63b2387f266be6b4f411e8eb49b7fe1b3925348`, SHA-256 `C34A4EE5D471D763B6F8E9808839A64E4B619F31AA1FE27DE4ABDA7CBD049A7C`. Die Architektur ist entschieden. Offene API-/Runtimefragen sind begrenzte Prüfaufträge vor dem jeweiligen Adapter, keine noch ausstehende Produktentscheidung.

## Umfang und Reihenfolge

Alle Integrationen sind optional, lokal und standardmäßig ausgeschaltet. Ein fehlendes oder inkompatibles Fremdplugin darf keine Installation oder automatische Ersatzaktion auslösen.

| Reihenfolge | Integration | Erster Umfang |
|---|---|---|
| 1 | Gemeinsame Grundlage | Lokale Einstellungen, Capability-/Readinesszustand, begrenzte Aktionen und Lebenszyklus. |
| 2 | Honorific | Titel lesen; ausdrücklich in eigenen Entwurf übernehmen. Schreiben zunächst gesperrt. |
| 3 | Integrationsoberfläche | Kompakte Einstellungen, Status und bewusst ausgelöste Aktionen; keine neue Hauptnavigation. |
| 4 | Eigene FRP1-IPC | Version, Readiness und kleine freigegebene Aktionen am eigenen Profil. |
| 5 | Moodles | Ausgewählte Angaben lokal lesen; ausdrücklich geprüften Inhalt kopieren. |
| 6 | Glamourer | Bewusst ausgewählten eigenen Equipment-Look anwenden. |
| 7 | Lifestream | Lokal gespeicherte typisierte Housingadresse, ausdrückliche Aktion Besuchen. |
| 8 | Customize+ | Lesen und Scope prüfen; Schreiben zunächst gesperrt. |

## Gemeinsame Daten- und Sicherheitsgrenzen

`integrations.json` enthält ausschließlich lokale Einstellungen und Zuordnungen. Striktes begrenztes Schema, Größen-/Anzahlgrenzen und atomisches Schreiben nach dem bestehenden Storemuster. Konkrete technische Caps im Grundlagenpaket als Konstanten festlegen und testen. Keine Erweiterung des öffentlichen Profilschemas oder Wireformats. GUIDs, Pfade, Peers, Content-IDs, private Notizen und Schlüssel werden nicht automatisch exportiert. Explizites Kopieren geprüften Texts in den eigenen Entwurf bleibt eine bewusste Inhaltsentscheidung und erteilt keine Netzwerkfreigabe.

Jeder Zugriff prüft aktuelle Audience-/Profilpolicy, Profil-ID, Actoridentität und Zustandsgeneration vor sowie nach einem asynchronen Ergebnis. Veraltete Ergebnisse nach Profil-/Szenen-/Policywechsel verwerfen. Änderungen am eigenen Profil ausschließlich über den vorhandenen `OwnProfileSession`-Schreiber mit erwarteter Revision/Hash; kein direkter Storezugriff oder Überschreiben eines veränderten/beschäftigten Entwurfs. UI-Übernahmen verwenden den vorhandenen Bearbeiten-/Speichern-/Verwerfen-Ablauf.

Native Aktionen auf dem Framework-/Frame-Thread ausführen. Begrenzte Warteschlange, Ratelimit und zusammengefasste Zustandsereignisse statt IPC-Polling in jedem Frame. Fehlende/unsupported APIs, Widerruf und Unload sperren Aktionen, brechen eigene ausstehende Arbeit ab und entsorgen Abonnements. Keine fremden Zustände ungefragt zurücksetzen; Abbruch nur dort anbieten, wo der eigene Vorgang nachweisbar abgrenzbar ist.

Unmittelbar vor der Queue-Ausführung werden Busy-/Draftzustand atomar geprüft und für denselben Auftrag aktuelle Policy, Actoridentität und Generation erneut validiert; zwischen Einreihen und Ausführen geänderte Bedingungen verwerfen den Auftrag ohne Nebenwirkung.

Gleiche Ereignisse und Read-/Writeanforderungen für denselben Zielzustand zusammenfassen. Doppelte Anfragen dürfen keinen doppelten Save, Lookwechsel oder Reisebeginn auslösen; bei Szenenwechsel alte Aufträge verwerfen. Diese Idempotenz ist Abnahmekriterium der Session-/Adaptertests.

Eigene IPC-Aufrufer sind **keine authentifizierten Identitäten**. Globale und funktionsbezogene Freigaben schützen den lokalen Nutzer, ersetzen aber keine nachgewiesene Calleridentität. Keine Caller-Whitelist als Sicherheitsversprechen. Alterswahl und Adult-Opt-in können weder Fremdplugins noch IPC erteilen. Keine automatische Übernahme privater/erwachsener Inhalte und keine Ausführung von Formatstrings, Profiltexten oder Fremdprofilbefehlen.

Für nicht rückholbare externe Textsenken einschließlich bleibender Chatausgaben und Protokolle gilt General-only; Mature-Opt-in ist kein Exportfreibrief. Sensible Payloads, private Angaben und Schlüssel werden auch bei Fehlern nicht protokolliert. Bestehende native Frame-Thread- und Legacy-General-Grenzen bleiben bestehen.

Der Betrieb bleibt ohne Registrierung, zentralen FRP1-Profilserver, Discovery, Relay oder Telemetrie. GitHub verteilt ausschließlich Software und Installerindex.

## Arbeitspakete mit Eigentum und Abnahme

Die folgenden neuen Dateinamen sind geplante Zuständigkeiten. Vor Umsetzung tatsächliche Struktur abgleichen. Ein Entwickler besitzt jeweils genau das angegebene Produktpaket; Tester ändern nur zugehörige Tests, Reviewer prüfen ohne Änderungen. Gemeinsame Verdrahtungsdateien nacheinander bearbeiten.

### WP1 – Integrationsgrundlage

**Eigentum:** `src/FRP1.Plugin/Integrations/IntegrationModels.cs`, `IntegrationSettingsStore.cs`, `IntegrationSession.cs`; notwendige Verdrahtung in `Plugin.cs`. Abhängigkeit: keine.

Capabilities mit klaren Zuständen für aus/fehlend/inkompatibel/bereit/beschäftigt/Fehler modellieren. Lokaler Store, Generationen, Aktionsbegrenzung, Frame-Dispatch und Unloadschutz; vorhandenen Profilschreiber und `LocalJsonFile` weiterverwenden. Kein neues DI-/Workflowframework und keine leeren Dummyadapter für spätere Phasen. Die Grundlage nur für den ersten Honorific-Leseumfang bauen.

**Tests/DoD:** `IntegrationSettingsTests` und `IntegrationSessionTests`: ungültige/übergroße Daten, atomisches Speichern, Standard aus, Busy/CAS, veraltete Generation, Actorwechsel, Ratelimit, Widerruf und Unload. Queue-Regression: Auftrag gültig einreihen, danach Busy-/Draft-/Actor-/Policy-/Generationwechsel vor Ausführung; keine Ausführung und kein Write, ausschließlich Status abgewiesen. Ohne Fremdplugins nutzbar; keine Änderung an Profil-/Wireformat oder privatem Datenumfang.

### WP2 – Honorific zunächst lesen

**Eigentum:** `Integrations/HonorificAdapter.cs`, `HonorificRules.cs`; gezielte Ergänzung des eigenen Entwurfswegs in `MainWindow.cs` und Own-Titelslots in `GameIntegration/GamePresentationModule.cs`. Abhängigkeit: WP1 plus Honorific-Spike.

Titel lesen und erst nach ausdrücklicher Übernahme in den ausgewählten eigenen Entwurf eintragen. Erwartete Profil-ID und Entwurfszustand prüfen; bestehenden veränderten Entwurf nicht überschreiben. Titeltext gegen kanonische `ProfileValidator`-Grenzen prüfen, keine Format-/Befehlsinterpretation. Schreiben bleibt gesperrt: Die bekannte API bietet keinen Owner-Token, deshalb keine racefreie automatische Rücknahme versprechen.

Das MVP enthält bereits einen bedienbaren minimalen UI-Weg: lokale Opt-in-Zeile unter den bestehenden Einstellungen/Integrationen sowie **Titel übernehmen** für den ausgewählten eigenen Entwurf. Default aus, Readiness sichtbar und Import nur bei passendem aktuellen Profil und sicherem Entwurfszustand; WP3 ist keine Voraussetzung für diesen Knopf, sondern erweitert später die einheitliche Oberfläche.

**Native Titelkollision:** `GamePresentationModule.OnNameplates` schreibt bisher FRP1-Titel. Nur den eigenen nativen Titelslot bei bestätigter Honorific-Readiness für Read/Set überspringen; fremde FRP1-Titel und andere Anzeigen erhalten. Keine Einstellungen anderer Plugins umschreiben. Fehlt Honorific, gilt das bisherige FRP1-Anzeigeverhalten; kein neuer Honorific-Titelschreibfallback.

**Tests/DoD:** `HonorificIntegrationTests`: fehlende/alte API, Titelgrenzen, bewusster Import, falsche Profil-ID, Dirty/Busy/CAS, verspätete Ergebnisse und exakt eigener nativer Slot. Ohne Opt-in kein Read/Import, keinerlei Honorific-Schreiben.

### WP3 – Kompakte Einstellungen

**Aktueller Arbeitsfokus WP3:** Der kompakte UX-Abschluss ist am 2026-11-13 in Umsetzung (`integration_ux_impl`), noch ohne Funktions-/Prüf-/Releaseabschluss. Umfang ausschließlich `MainWindow.cs`, `Localization/UiText.cs` und `HelpContent.cs`: vier kurze, zunächst geschlossene Integrationsteile; Status und Freigabe bleiben außen sichtbar, Details gezielt öffnen. Keine Daten-/Sessionänderung. Verifiziertes Quellbackup: `.release-staging/before-integration-ux-20261113T163049Z`, Quelle `5c01d54`, SHA-256 `C3FC93FB27B5C3145EFED97E52FF77E7BB89C87395935A4F2E50E6D487D738D6`.

**Eigentum:** `Integrations/IntegrationUi.cs`, Integrationsteil in `MainWindow.cs`, notwendige `HelpContent.cs`-/`Localization/UiText.cs`-Texte. Abhängigkeit: WP1–2.

Unter Einstellungen/Erweitert je Plugin Status, lokale Freigabe und verfügbare Aktion anzeigen. Technische IDs/Pfade nicht in Profilflächen. Unterstützte Version und fehlende Capability verständlich erklären; keine wirkungslosen Buttons oder neue Hauptnavigation.

**Tests/DoD:** Zustands-/Aktionsregeln gezielt prüfen. Native UI-Abnahme für aus/fehlend/bereit/beschäftigt, kleine Viewports, Fokus, Widerruf und Pluginentladen gesondert protokollieren. Bedienung von Speichern, Aktivieren und Teilen bleibt getrennt.

### WP4 – Eigene FRP1-IPC

**Eigentum:** `Ipc/FrpIpcContracts.cs`, `FrpIpcRules.cs`, `FrpIpcProvider.cs`; Lebenszyklusverdrahtung in `Plugin.cs`. Abhängigkeit: WP1, vorhandene eigene Profilzustände und der bereits im MVP verfügbare minimale Integrations-UI-Weg; der breitere WP3-Ausbau ist keine Voraussetzung.

**Im Quellstand `6b97c1b` implementiert und reviewfreigegeben, als 0.2.0.6 veröffentlicht.** Der [FRP1-IPC-v1-Vertrag](frp1-ipc-v1.md) dokumentiert API 1.0, Channels, primitive Rückgaben, Tickets und Fehlercodes. Globale Lese-/Steuerfreigabe und drei Funktionsfreigaben standardmäßig aus. General-only-Lesen von Status/Verfügbarkeit; eigene Steueraktionen auf Mature nur mit aktueller Adult-Auswahl und Mature-Opt-in. Keine Vollprofil-/Register-/Peer-/Schlüsselexporte. Aktives eigenes Profil unabhängig von der UI-Auswahl; dieselben Policy-/CAS-/Queuegrenzen wie die UI. Begrenzte Queue und letztes Ergebnis, 500-ms-Aktionsabstand, 30-s-Ergebnisablauf, synchrone Policy-Epochen und generationengebundene Ergebnisse. `Persisted` erst nach erfolgreichem Writer und Integration des gespeicherten Profils; Own-Poll-Zeitfenster als `Running`, echte Konflikte als `Stale`. Minimale Freigabeoberfläche in Einstellungen/Integrationen vorhanden, breiterer WP3-Ausbau bleibt separat.

**Tests/DoD:** `FrpIpcRulesTests`, `FrpIpcSessionTests` und ergänzte `IntegrationSettingsTests` prüfen Standard aus, ungültige Eingaben, Alters-/Policygruppen, General-only-Lesen, Dirty/Busy/CAS, Rategrenze, Ticketablauf, Deduplizierung, Policywechsel, Writer-/Poll-/Refresh-/Scope-Rennen und Unload. Lokaler/Hosted-Gesamtnachweis oben. Keine Caller-Authentisierung behaupten; keine verborgenen Schreibwege. Native IPC-/Spielprobe steht aus.

Die im Gesamtlauf erneut beobachteten Windows-I/O-Fehler beim überschreibenden Move sind durch `LocalJsonFile`-Replace mit eindeutiger vorheriger Sicherung adressiert, einschließlich Offen-/Blockierleser-Regressionen. Sicherung erst nach Erfolg entfernen und bei teilweisem Replacefehler erhalten; kein Retry oder instanzübergreifendes Schutzversprechen. Diagnose und Grenzen: [STATUS](../STATUS.md).

### WP5 – Moodles lokal auswählen

**Am 2026-11-13 im Quellstand `1d5f774` abgeschlossen, unabhängig reviewfreigegeben und als 0.2.0.7 veröffentlicht.** Moodles-Manifest 1.1.3.5/API 15 schreibfrei erfasst, API 4/Quellformat 2 abgeglichen; keine native Aktivierung oder IPC-Probe. Verifiziertes Quellbackup vor Umsetzung: `.release-staging/before-moodles-20261113T112856Z`, Quelle `7666329`, SHA-256 `32CABC658791D3436FC4F8546EB88AD44D8052CEB81786BF8ACFD7072F3A2006`.

**Umgesetzt:** `Integrations/MoodlesAdapter.cs`, `MoodlesModels.cs`, `MoodlesRules.cs`, `MoodlesSession.cs`; lokale Einstellungen und minimale Oberfläche in `MainWindow.cs`/`Localization/UiText.cs`, Lebenszyklus in `Plugin.cs`. Baut auf WP1 und dem vorhandenen minimalen Integrationsweg auf; breiterer WP3-Ausbau bleibt separat.

Standardmäßig aus unter **Einstellungen → Integrationen**. Ausschließlich eigene aktive Zustände über `Moodles.GetClientStatusManagerInfoV2`, keine Apply-/Set-Channels oder Fremdreads. Höchstens fünf gewählte lokale Bindungen aus ID, Inhalts-Hash und ausdrücklich bestätigtem General-/Mature-Rating. Änderungen an Titel/Text/Icon machen die Bewertung ungültig. Ungeprüfte oder Mature-Vorschau nur mit Adult samt Mature-Opt-in; passendes General-Rating erlaubt Vorschau nach erfolgter Alterswahl. Bewertung selbst erfordert Adult samt Mature-Opt-in. Kein automatisches General-Rating für fremden Customtext oder Icons.

Bewusster Import hängt einen Glance an einen sauberen gespeicherten eigenen Entwurf mit weniger als fünf Glances an. Mature nur in Mature-Ziel bei Adult-/Mature-Freigabe; Titel bis 80, Beschreibung bis 1024 Zeichen, keine Kürzung. Anschließend regulär speichern/verwerfen. Keine neuen Profil-/Wirefelder oder automatische Netzwerkfreigabe; bestehende Freigaben gelten für gespeicherte Inhalte. `integrations.json` bleibt Schema 1 mit optionalen Moodles-Feldern, 8-KiB-Grenze und Lesbarkeit älterer Dateien. Hintergrundreads höchstens 1 Hz; ausdrückliche Bewertung/Import lesen zusätzlich frisch und teilen 500 ms Mindestabstand. Kontext-, Policy-, Actor-, Entwurfs- und Quelländerungen sowie Busy/Unload verwerfen veraltete Ergebnisse.

**Prüfstand:** `MoodlesRulesTests`, `MoodlesSessionTests`, `MoodlesSettingsTests` und Integrationsregressionen decken Auswahl, fehlende/inkompatible API, ungültige Quellen, Hash-/Ratingwechsel, Policy, Draft-CAS, Busy, Actorwechsel, frische Aktionsreads, Ratelimits und Unload ab. 248 gezielte Tests und 1019/1019 vollständige Funktionstests (28 Sekunden, keine Fehler/Skips), voller Release-Solutionbuild mit 0 Warnungen/Fehlern, Paket33 und unabhängige Reviewfreigabe ohne wichtige Restbefunde. Synthetische SDK-/Newtonsoft-Probe für 16-Felder-Tuple/Enum-Konvertierung bestanden, ausschließlich synthetisch. Native Moodles-/IPC-/Spiel-/UI-QA offen. Lokales Anzeigen ist keine Veröffentlichung; Hosted-/Downloadnachweise für 0.2.0.7 erfolgreich; konkrete Hashes in [STATUS](../STATUS.md).

### WP6 – Eigener Glamourer-Equipment-Look

**Am 2026-11-13 blockiert, WP7 wird vorgezogen:** Der begrenzte Quellabgleich zur Glamourer-API 1.8 (`ea569211a7c3500f2ce0b1b9223df85d8cb85f1a`) und Implementierung `e165910345b17b99d9aa35990005b5e0f73ae65b` zeigt keinen belegten ausschließlich auf Equipment begrenzten Schreibweg. `ApplyDesign(Once | Equipment, key: 0)` kann verknüpfte Modzuordnungen in Penumbra anwenden: [DesignsApi.cs, Zeile 41](https://github.com/Ottermandias/Glamourer/blob/e165910345b17b99d9aa35990005b5e0f73ae65b/Glamourer/Api/DesignsApi.cs#L41) setzt `MergeLinks: true`; [StateEditor.cs, Zeile 476](https://github.com/Ottermandias/Glamourer/blob/e165910345b17b99d9aa35990005b5e0f73ae65b/Glamourer/State/StateEditor.cs#L476) verwendet bei fehlender expliziter Vorgabe `AlwaysApplyAssociatedMods`; [DesignMerger.cs, Zeile 45](https://github.com/Ottermandias/Glamourer/blob/e165910345b17b99d9aa35990005b5e0f73ae65b/Glamourer/Designs/Links/DesignMerger.cs#L45) sammelt AssociatedMods unabhängig von der Equipmentmaske; [ModSettingApplier.cs, Zeile 20](https://github.com/Ottermandias/Glamourer/blob/e165910345b17b99d9aa35990005b5e0f73ae65b/Glamourer/Interop/Penumbra/ModSettingApplier.cs#L20) schreibt diese nach Penumbra. Der genaue Scope von ActorIndex gegenüber identitätsgebundenem Zustand und Kopien ist zusätzlich ungeklärt. Kein WP6-Produktcode, keine Dummyoberfläche und keine native Abnahme; Schreiben bleibt gesperrt. WP7 ist nach dem eigenen begrenzten Adress-/Abbruchabgleich implementiert und automatisiert geprüft; unabhängige Reviewfreigabe ohne kritische/wichtige Restbefunde erteilt; als 0.2.0.8 veröffentlicht.

**Eigentum:** `Integrations/GlamourerAdapter.cs`, `GlamourerRules.cs`, zugehörige Auswahlaktion. Abhängigkeit: WP1/3 plus Scope-Spike.

Ausschließlich ausdrücklich ausgewählten lokalen Look auf den aktuellen eigenen Actor anwenden. Vor Umsetzung Equipment-Scope, Flags, Keys, Ownership, Rückgabecodes, unterstützte API-Version und tatsächliche Parameterwirkung belegen. Keine automatische Anwendung aus fremden Profilen, keine vollständigen Designs oder Actor-IDs exportieren. Ist eine dieser sicherheitsrelevanten Semantiken ungeklärt, bei Read-only bleiben und die Anwendung deaktiviert lassen.

**Tests/DoD:** `GlamourerIntegrationTests`: eigene Actoridentität, Scene-/Generationwechsel, bestätigter Scope, Ratelimit und Unload; native Probe separat. Kein ungeprüftes Restore-/Owner-Versprechen.

### WP7 – Lifestream-Housingbesuch

**Am 2026-11-13 im Produktcode implementiert; neue Tests bestanden, unabhängig ohne kritische/wichtige Restbefunde reviewfreigegeben; als 0.2.0.8 veröffentlicht, Quelle `5c01d545de0330ac6231175c617f8801453def33`.** Lokal 182/182 gezielte und 1201/1201 vollständige Funktionstests (28 Sekunden, keine Fehler/Skips), Testbuild ohne Warnungen/Fehler, voller API-15-Release-Solutionbuild 0 Warnungen/Fehler (1,25 Sekunden), Paket33/33. 0.2.0.8 ist öffentlich bestätigt. Native QA offen; durch den Agenten weder installiert noch aktiviert. Verifiziertes lokales Quellbackup vor WP7: `.release-staging/before-lifestream-20261113T114933Z`, Quelle `de8a29d`, SHA-256 `B1EEBDB6E65E8941DD3D8732EF111AF6124796F76C2CF06B4B16DC52FAA23A82`.

**Umgesetzt:** `Integrations/LifestreamAdapter.cs`, `HousingAddress.cs`, `LifestreamRules.cs`, `LifestreamSession.cs`, lokale Einstellungen, MainWindow-/Pluginverdrahtung und DE-/EN-Texte. Grundlage WP1 und vorhandener minimaler Integrationsweg; breiterer WP3-Ausbau bleibt separat.

Standardmäßig aus unter **Einstellungen → Integrationen**. Genau ein lokales Housingziel mit öffentlicher Welt, fünf typisierten Gebieten, Bezirk 1–30, Haus 1–60 oder Apartment 1–90 (FRP1-Produktgrenze), Apartment-Unterbezirk optional. **Ziel speichern** und **Besuchen** sind getrennte Aktionen; keine automatische Reise. Nur lokale Einstellungen in Schema 1/8 KiB, keine Profil-/Wirefelder, Befehlsstrings, Namens-/Aliasinterpretation oder Server. Die öffentliche Zielwelt mit gültiger World-/DataCenter-ID wird vor Dispatch erneut gegen das World-Sheet geprüft.

Exaktes Laufzeitgate: geladene Installation **2.6.0.13/API 15** aus Pluginmetadaten, kein IPC-Versionshandshake. Grundlage [Release 2.6.0.13](https://github.com/NightmareXIV/Lifestream/releases/tag/2.6.0.13) und [Quellstand `b36320170faafc8315f7585edb45cfbcd34e7046`](https://github.com/NightmareXIV/Lifestream/tree/b36320170faafc8315f7585edb45cfbcd34e7046); die veraltete csproj-Version 2.5.4.18 ist kein Gate. Genau zwei Channels: `Lifestream.IsBusy` und Action `Lifestream.GoToHousingAddress`. Typisiertes 10-Felder-Tuple, Name/Alias leer und Alias deaktiviert. QuickTravel wird nicht abgefragt und ist bei false kein Blocker. Keine Adressbuch-, Command- oder Abort-Endpunkte.

Die Reise kann den eigenen Charakter bewegen, teleportieren, Welt/Datenzentrum wechseln und ein Apartment betreten. UI-Ergebnis ausschließlich **Auftrag übergeben**, kein Ticket, Ankunfts-/Erfolgs-/Zutrittsnachweis oder Ownershipversprechen. FRP1 bietet keinen Abort; Ausschalten und Kontextwechsel stoppen bereits übergebene Reisen nicht. Laufende Reise in Lifestream selbst bedienen. Pending-Aufträge verfallen bei Dirty/Busy sowie Actor-, CurrentWorld-, Szenen-, Einstellungs-, Policy- oder Providerwechsel. 500 ms Aktionsabstand und höchstens 1 Hz Hintergrundprüfungen; frische Busy-/Kontext-/Weltprüfung vor Dispatch auf dem Framework-Thread.

[Hosted-Lauf 43462921783](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43462921783) besteht Build/Publish mit **0 Warnungen/Fehlern**, **1201/1201 Funktionstests** (36 Sekunden), **33/33 Paketprüfungen** und **105/105 CI-Prüfungen** (Policy33, Publication24, SDK44, Quellpfade4). [Release v0.2.0.8](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.8) und kanonischer Raw-Index öffentlich; Index anonym HTTP 200 mit Version 0.2.0.8, ZIP und `SHA256SUMS` anonym geladen, Hashvergleich und vollständige Paketvalidierung bestanden. Botcommit `5cc6ab3` ändert nur `repo.json`; per Fast-forward abgeglichen. Native QA bleibt offen.

**Prüfstand:** `HousingAddressTests`, `LifestreamSessionTests` und `LifestreamSettingsTests` samt Integrationsregressionen: 182/182 gezielte Tests, 1201/1201 vollständige Funktionstests (28 Sekunden, keine Fehler/Skips), voller API-15-Solutionbuild 0 Warnungen/Fehler und Paket33/33 bestanden. Unabhängiges Review ohne kritische/wichtige Restbefunde freigegeben, einschließlich der gekürzten UI-Texte. Der Threadverdacht wurde anhand der gebundenen Dalamud-Threadidentität und UI auf dem Hauptthread nicht bestätigt; dies ersetzt keine native Bedienabnahme. Keine native Reise-/UI-Abnahme. [Bedienung, Handbuch Abschnitt 23](benutzerhandbuch.md#23-lifestream-housingziel-bewusst-besuchen-wp7).

### WP8 – Customize+ lesen und prüfen

**Scopeprüfung am 2026-11-13 abgeschlossen, Produktmodul zurückgestellt:** Der begrenzte Abgleich von [Customize+ API 6.4 im Quellstand `44d25415b961e0bbba759d1508b6d06acef88d90`](https://github.com/Aether-Tools/CustomizePlus/tree/44d25415b961e0bbba759d1508b6d06acef88d90) zeigt: `GetActiveProfileIdOnCharacter(ushort)` liefert `(int, Guid?)` für die gespeicherte Zuordnung, keinen Nachweis des tatsächlich wirksamen Presets; Editor- und temporäre Zustände bleiben unberücksichtigt. `IsValid` kann auch bei global ausgeschaltetem Plugin wahr sein; ein primitiver Enabled-Getter fehlt. `GetList` würde sämtliche Namen, Pfade und Zuordnungen lesen. Eine begrenzte eigene Zuordnungsabfrage existiert; sie belegt jedoch weder das tatsächlich wirksame Preset noch den globalen Aktivierungszustand. Der Nutzen einer entsprechenden Statusoberfläche ist damit gering. Belege: [IsValid, General-API Zeile 20](https://github.com/Aether-Tools/CustomizePlus/blob/44d25415b961e0bbba759d1508b6d06acef88d90/CustomizePlus/Api/CustomizePlusIpc.General.cs#L20), [eigene Zuordnung, Profile-API Zeile 285](https://github.com/Aether-Tools/CustomizePlus/blob/44d25415b961e0bbba759d1508b6d06acef88d90/CustomizePlus/Api/CustomizePlusIpc.Profile.cs#L285), [Resolver und Grenzen, ProfileManager ab Zeile 494](https://github.com/Aether-Tools/CustomizePlus/blob/44d25415b961e0bbba759d1508b6d06acef88d90/CustomizePlus/Profiles/ProfileManager.cs#L494). Kein Füllmodul und keine Oberfläche; Schreiben bleibt gesperrt. Keine native Abnahme.

**Eigentum:** `Integrations/CustomizePlusAdapter.cs`, `CustomizePlusRules.cs`, lokale Statusdarstellung. Abhängigkeit: WP1/3 plus Customize+-Spike.

Zunächst Readiness, lesbare eigene Auswahl und Wirkungsbereich prüfen. Schreiben bleibt gesperrt, bis Eigentümerwirkungen, Actorbezug und sichere Aufhebung belegbar sind. Keine automatische Profil-/Skalenzuordnung oder Rücknahme fremder Änderungen.

**Tests/DoD:** `CustomizePlusIntegrationTests`: fehlend/unsupported, eigener Actor, Auswahl-/Policywechsel und Unload. Ein Read-Adapter gilt nicht als Freigabe für spätere Schreibfunktionen.

## Quellen und begrenzte Spikes

Die Quellen belegen vorhandene APIs, **keine** native IPC-/Spielabnahme oder API-15-Runtimekompatibilität. Für Honorific wurde am 2026-11-13 zusätzlich das lokale Manifest schreibfrei abgeglichen:

- [Honorific: IpcProvider](https://github.com/Caraxi/Honorific/blob/master/IpcProvider.cs)
- [Glamourer: IGlamourerApiDesigns](https://github.com/Ottermandias/Glamourer.Api/blob/main/Api/IGlamourerApiDesigns.cs)
- [Moodles: IPCProcessor](https://github.com/kawaii/Moodles/blob/main/Moodles/IPCProcessor.cs)
- [Customize+: Profile-IPC](https://github.com/Aether-Tools/CustomizePlus/blob/main/CustomizePlus/Api/CustomizePlusIpc.Profile.cs)
- [Lifestream: IPCProvider](https://github.com/NightmareXIV/Lifestream/blob/main/Lifestream/IPC/IPCProvider.cs)

Honorific-Abgleich am **2026-11-13**: `Caraxi/Honorific`-`IpcProvider` mit `ApiVersion` 3.2, `GetLocalCharacterTitle`, `LocalCharacterTitleChanged`, `Ready` und `Disposing`; Quellprojektversion 1.7.5.1, SDK 15, `net10.0-windows`; lokales Honorific-Manifest 1.7.5.1/API 15. Native IPC-Probe bleibt offen. Das erste MVP bleibt standardmäßig aus und liest ausschließlich; sämtliche Honorific-Set-/Clear-/SetIdentity-Aufrufe bleiben verboten. Titelübernahme erfolgt bewusst über Draft-CAS und Kontextgeneration in den eigenen Entwurf, gespeichert wird regulär. Begrenztes JSON, Frame-Queue, ausschließlich eigener nativer Titelslot und kein automatischer Export privater/Mature-Inhalte sind MVP-Grenzen.

Vor jedem Adapter einmal Zielversion, exakte Channels/Signaturen/DTOs, Threadanforderung und wirksamen Scope abgleichen. Eine kleine native Probe gesondert vom Quell-/Buildnachweis protokollieren. Bei fehlendem Nachweis Adapter deaktiviert belassen und den konkreten offenen Punkt dokumentieren, statt unbegrenzt zu recherchieren oder Plugins automatisch zu installieren. Honorific-Ownership, Glamourer-Equipmentwirkung, Lifestream-Adress-/Abbruchsemantik und Customize+-Eigentümerwirkung bleiben explizite Spikefragen.

## Prüfung und Einstieg am 2026-11-13

**Bei Unterbrechung nächster Schritt:** Diese Datei sowie die obersten Abschnitte in STATUS/HANDOFF und den tatsächlichen Git-/Teststand abgleichen. WP1/WP2 sind als 0.2.0.5 veröffentlicht; WP4 ist am **2026-11-13** im Quellstand `6b97c1b` umgesetzt, getestet, reviewfreigegeben und als 0.2.0.6 veröffentlicht. Hosted-/Index-/Download-/Hash-/Paketnachweise und Fast-forward auf Botcommit `7666329` abgeschlossen. WP5 ist im Quellstand `1d5f774` abgeschlossen, reviewfreigegeben und als 0.2.0.7 veröffentlicht; Hosted-/Index-/Download-/Hash-/Paketnachweise erfolgreich, Fast-forward auf Botcommit `de8a29d` abgeschlossen. WP7 ist als 0.2.0.8 veröffentlicht, unabhängig reviewfreigegeben und durch Hosted-/Index-/Download-/Hash-/Paketprüfungen belegt. WP3-UX-Abschluss ist in Umsetzung, noch ohne Funktionsnachweis; WP6-Schreiben bleibt wegen des unten belegten Scopes gesperrt. Paketweise belastbare Checkpoints hinterlassen; native Abnahme bleibt gesondert offen.

Das erste MVP beschränkt sich auf die kleine notwendige Grundlage und Honorific-Read mit bewusster sicherer Entwurfsübernahme sowie eigener Titelkollisionsregel. WP4 ergänzt jetzt die kleine eigene IPC im geprüften Quellstand; WP5 ergänzt die begrenzte lokale Moodles-Auswahl samt geprüftem Glance-Import. WP3-Ausbau und WP6–8 bleiben getrennte Arbeitspakete. Keine zusätzlichen Integrationen oder vorsorgliche Frameworkausweitung in dieses MVP ziehen. Keine festen zukünftigen Testzahlen versprechen.

Pro Implementierung gezielte Tests, Build und unabhängiges Review. Vollständigen Solutionbuild und Funktionstests nach tatsächlichen Änderungen durchführen, nicht für diesen Plan wiederholen. Reale Plugin-/Spielabnahme, Unload und Skalierung bleiben eigene Schritte. Dokumentationsabschluss oder Planreview allein belegen keine Implementierung oder Veröffentlichung; für das Honorific-MVP liegen die gesonderten Nachweise oben vor.
