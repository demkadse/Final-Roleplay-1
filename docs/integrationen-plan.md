# Plan für lokale Pluginintegrationen und FRP1-IPC

Stand: 2026-11-12 · **Planung abgeschlossen; keine Integration implementiert.** Planinhalt vollständig, alle vier Reviewanforderungen eingearbeitet; abschließender Reviewer-Abgleich der zuletzt ergänzten Queue-Klarstellung noch offen. Kontrollierter Checkpoint am 2026-11-12; am 2026-11-13 zuerst diesen einen Abgleich abschließen, danach WP1/Honorific lesen beginnen.

Die Planungsrunde am **2026-11-12** beschränkt sich auf Planung und Dokumentation. Umsetzung frühestens am **2026-11-13**. Ausgangsstand `89b5e42`, öffentliches FRP1 0.2.0.4; Produktcode, Runtime und Veröffentlichung bleiben in der Planungsrunde unverändert. Die Architektur ist entschieden. Offene API-/Runtimefragen sind begrenzte Prüfaufträge vor dem jeweiligen Adapter, keine noch ausstehende Produktentscheidung.

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

**Eigentum:** `Integrations/IntegrationUi.cs`, Integrationsteil in `MainWindow.cs`, notwendige `HelpContent.cs`-/`Localization/UiText.cs`-Texte. Abhängigkeit: WP1–2.

Unter Einstellungen/Erweitert je Plugin Status, lokale Freigabe und verfügbare Aktion anzeigen. Technische IDs/Pfade nicht in Profilflächen. Unterstützte Version und fehlende Capability verständlich erklären; keine wirkungslosen Buttons oder neue Hauptnavigation.

**Tests/DoD:** Zustands-/Aktionsregeln gezielt prüfen. Native UI-Abnahme für aus/fehlend/bereit/beschäftigt, kleine Viewports, Fokus, Widerruf und Pluginentladen gesondert protokollieren. Bedienung von Speichern, Aktivieren und Teilen bleibt getrennt.

### WP4 – Eigene FRP1-IPC

**Eigentum:** `Ipc/FrpIpcContracts.cs`, `FrpIpcRules.cs`, `FrpIpcProvider.cs`; Lebenszyklusverdrahtung in `Plugin.cs`. Abhängigkeit: WP1, vorhandene eigene Profilzustände; nach WP3 integrieren.

Versionierter kleiner Vertrag für Version/Readiness und begrenzte eigene Status-/Verfügbarkeitsaktionen. Globale sowie funktionsbezogene Freigaben, standardmäßig aus. Keine Vollprofil-/Register-/Peer-/Schlüsselexporte. Genaue Channelnamen, DTOs und Fehlercodes vor Registrierung festschreiben; Ergebnisse unterscheiden deaktiviert, inkompatibel, busy, veraltet und abgelehnt. Dieselben Policy-/CAS-/Queuegrenzen wie die UI.

**Tests/DoD:** `FrpIpcRulesTests` und Provider-Lebenszyklusfälle: Standard aus, ungültige Eingaben, Dirty/Busy/CAS, Rategrenze, Policywechsel und Unload. Keine Caller-Authentisierung behaupten; keine verborgenen Schreibwege.

### WP5 – Moodles lokal auswählen

**Eigentum:** `Integrations/MoodlesAdapter.cs`, `MoodlesRules.cs`, eigener Teil von `IntegrationUi.cs`. Abhängigkeit: WP1/3 plus Moodles-Spike.

Nur ausgewählte lokale Angaben lesen. Kopieren erfordert bewusste Auswahl, Prüfung und bestätigten eigenen Entwurfsweg. Kein automatisches Kopieren oder Teilen von Adult-/Privatinhalten, GUIDs oder fremden Statuslisten. Fremder Customtext und Icons haben keine automatisch vertrauenswürdige Altersklassifikation: ausgewählt bedeutet nicht jugendfrei. Unbekannte Klassifikation konservativ sperren, insbesondere für Minderjährige. Kein automatisches General-Rating; explizite Übernahme in einen FRP1-Entwurf verlangt dessen bewusste Inhaltskennzeichnung. Rating-/UI-Verhalten vor Beginn dieser Phase konkret festlegen und als failclosed Abnahmekriterium prüfen.

**Tests/DoD:** `MoodlesIntegrationTests`: Auswahlbegrenzung, fehlende API, sensible Inhalte, Validierung, Profil-/Policywechsel und Busy. Lokales Anzeigen ist keine Veröffentlichung.

### WP6 – Eigener Glamourer-Equipment-Look

**Eigentum:** `Integrations/GlamourerAdapter.cs`, `GlamourerRules.cs`, zugehörige Auswahlaktion. Abhängigkeit: WP1/3 plus Scope-Spike.

Ausschließlich ausdrücklich ausgewählten lokalen Look auf den aktuellen eigenen Actor anwenden. Vor Umsetzung Equipment-Scope, Flags, Keys, Ownership, Rückgabecodes, unterstützte API-Version und tatsächliche Parameterwirkung belegen. Keine automatische Anwendung aus fremden Profilen, keine vollständigen Designs oder Actor-IDs exportieren. Ist eine dieser sicherheitsrelevanten Semantiken ungeklärt, bei Read-only bleiben und die Anwendung deaktiviert lassen.

**Tests/DoD:** `GlamourerIntegrationTests`: eigene Actoridentität, Scene-/Generationwechsel, bestätigter Scope, Ratelimit und Unload; native Probe separat. Kein ungeprüftes Restore-/Owner-Versprechen.

### WP7 – Lifestream-Housingbesuch

**Eigentum:** `Integrations/LifestreamAdapter.cs`, `HousingAddress.cs`, `LifestreamRules.cs`, lokale Besuchsaktion. Abhängigkeit: WP1/3 plus Adress-/Abbruch-Spike.

Welt, Gebiet, Bezirk und Wohnadresse als typisierte lokale Daten validieren. Besuchen nur nach Nutzerklick; keine Befehlsstrings aus Profiltexten. Bereits laufende fremde Reise nicht übernehmen. Eigenen Abbruch nur anbieten, wenn der eigene Vorgang API-seitig zuverlässig erkennbar ist.

**Tests/DoD:** `LifestreamIntegrationTests`: ungültige Adresse, laufende Reise, fehlende API, Nutzerklickpflicht, Actor-/Policywechsel und fehlende Abbruchzuordnung. Ohne belastbaren Abbruchnachweis kein allgemeines Abort anbieten.

### WP8 – Customize+ lesen und prüfen

**Eigentum:** `Integrations/CustomizePlusAdapter.cs`, `CustomizePlusRules.cs`, lokale Statusdarstellung. Abhängigkeit: WP1/3 plus Customize+-Spike.

Zunächst Readiness, lesbare eigene Auswahl und Wirkungsbereich prüfen. Schreiben bleibt gesperrt, bis Eigentümerwirkungen, Actorbezug und sichere Aufhebung belegbar sind. Keine automatische Profil-/Skalenzuordnung oder Rücknahme fremder Änderungen.

**Tests/DoD:** `CustomizePlusIntegrationTests`: fehlend/unsupported, eigener Actor, Auswahl-/Policywechsel und Unload. Ein Read-Adapter gilt nicht als Freigabe für spätere Schreibfunktionen.

## Quellen und begrenzte Spikes

Die Quellen belegen vorhandene APIs, **nicht** bereits geprüfte installierte Versionen oder API-15-Runtimekompatibilität:

- [Honorific: IpcProvider](https://github.com/Caraxi/Honorific/blob/master/IpcProvider.cs)
- [Glamourer: IGlamourerApiDesigns](https://github.com/Ottermandias/Glamourer.Api/blob/main/Api/IGlamourerApiDesigns.cs)
- [Moodles: IPCProcessor](https://github.com/kawaii/Moodles/blob/main/Moodles/IPCProcessor.cs)
- [Customize+: Profile-IPC](https://github.com/Aether-Tools/CustomizePlus/blob/main/CustomizePlus/Api/CustomizePlusIpc.Profile.cs)
- [Lifestream: IPCProvider](https://github.com/NightmareXIV/Lifestream/blob/main/Lifestream/IPC/IPCProvider.cs)

Vor jedem Adapter einmal Zielversion, exakte Channels/Signaturen/DTOs, Threadanforderung und wirksamen Scope abgleichen. Eine kleine native Probe gesondert vom Quell-/Buildnachweis protokollieren. Bei fehlendem Nachweis Adapter deaktiviert belassen und den konkreten offenen Punkt dokumentieren, statt unbegrenzt zu recherchieren oder Plugins automatisch zu installieren. Honorific-Ownership, Glamourer-Equipmentwirkung, Lifestream-Adress-/Abbruchsemantik und Customize+-Eigentümerwirkung bleiben explizite Spikefragen.

## Prüfung und Einstieg am 2026-11-13

**Bei sauberem Budgetstopp nächster Schritt:** Diese Datei sowie die obersten Abschnitte in STATUS/HANDOFF lesen. Planinhalt und dokumentierte Reviewnacharbeit sind vollständig; der letzte Reviewer-Abgleich zur Queue-Klarstellung ist noch offen. Am **2026-11-13** zuerst ausschließlich diesen Abgleich abschließen, danach Gitstatus/HEAD prüfen und mit WP1 sowie dem Honorific-Read-API-Spike beginnen. Bewusste Entwurfsübernahme und minimaler Opt-in-/Import-UI-Weg gehören zum ersten MVP. Das Verbot von Implementierung, Builds/Testläufen und Releases gilt für die Planungsrunde am **2026-11-12**; es verbietet nicht die ausdrücklich für 2026-11-13 geplanten Implementierungsprüfungen.

Nach diesem Reviewer-Abgleich am 2026-11-13 Gitstatus, HEAD und fremde Änderungen sichern/prüfen; dokumentierte API-Spikes gezielt beginnen. Das erste MVP beschränkt sich auf die kleine notwendige Grundlage und Honorific-Read mit bewusster sicherer Entwurfsübernahme sowie eigener Titelkollisionsregel. WP3–8 bleiben getrennte spätere Arbeitspakete; keine zusätzlichen Integrationen oder vorsorgliche Frameworkausweitung in dieses MVP ziehen. Keine festen zukünftigen Testzahlen versprechen.

Pro Implementierung gezielte Tests, Build und unabhängiges Review. Vollständigen Solutionbuild und Funktionstests nach tatsächlichen Änderungen durchführen, nicht für diesen Plan wiederholen. Reale Plugin-/Spielabnahme, Unload und Skalierung bleiben eigene Schritte. Dokumentationsabschluss oder Planreview bedeuten weder bereits implementierte Integrationen noch eine neue Pluginveröffentlichung.
