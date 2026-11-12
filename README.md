# Final Roleplay 1 (FRP1)

**Rollenspielprofile für Final Fantasy XIV, die auf deinem Rechner bleiben und nach deiner Freigabe direkt mit anderen FRP1-Clients ausgetauscht werden. Ohne FRP1-Profilserver, zentrale Profildatenbank oder Registrierung.**

FRP1 ist ein Dalamud-Plugin für Charakterbeschreibungen, erste Eindrücke, RP-Status und Kontakte. Es orientiert sich beim Bedienkonzept an Total RP 3 und übernimmt die lokal beziehungsweise direkt zwischen Clients umsetzbaren Funktionen von Glance in einer eigenen FFXIV-Oberfläche.

**Stand: 2026-11-12 – veröffentlicht als früher öffentlicher Teststand 0.2.0.4 (Prerelease).** Repository, MIT-Lizenz, Installerindex und Releasepaket sind öffentlich verfügbar. Der erfolgreiche GitHub-Lauf besteht Build mit 0 Warnungen/Fehlern, 510/510 Funktionstests, 33/33 Paketprüfungen und 105/105 CI-Prüfungen. Native Bedienungs-/Skalierungsabnahme und reale LAN-/Internet-/NAT-Abnahme stehen noch aus.

**Im aktuellen Installerpaket:** Drei Hauptwege **Mein Charakter**, **Kontakte**, **Einstellungen**; eigene Lesekarte mit getrenntem Bearbeiten, drei Profilbereiche **Überblick**, **Charakter**, **OOC**, einzeilige zielunabhängige eigene RP-Leiste und drei primäre Themes. Fremde Angaben bleiben in kompakter Ziel-/Mouseoveranzeige; vollständige Glances sind per Hover/Fokus/Klickpin zugänglich.

**RP-Leiste wieder einblenden:** 0.1.1.3 korrigiert ein sofortiges erneutes Ausschalten nach dem Aktivieren. Nach dem Update die Leistensichtbarkeit erneut aktivieren oder **Anzeigen und Position zurücksetzen** wählen; alternativ `/frpbar` beziehungsweise `/frp bar show` verwenden. Die gespeicherte ausgeschaltete Wahl wird durch das Update nicht automatisch verändert.

**Auf 0.2.0.4 aktualisieren:** Verwende dieselbe untenstehende JSON-Adresse im Dalamud-Installer. Aktualisiere das Plugin und lade es bei Bedarf neu. Profildaten, alte Theme-IDs, private Registerdaten, Kopplungen und Freigaben bleiben erhalten; eine Konfigurationslöschung ist nicht erforderlich. Aktuelle Anleitung: [Handbuch, Abschnitt 19](docs/benutzerhandbuch.md#19-kompakte-bedienung-seit-0204), [UX](docs/ux-and-vanity.md). Eine Installation oder native Spielabnahme wurde durch uns nicht durchgeführt.

## Kein zentraler Server für deine Profile

FRP1 lädt deine Charakterprofile nicht auf eine Website, in eine Cloud oder in eine zentrale Datenbank hoch. Ein öffentliches Profil bleibt zunächst eine lokale Datei. Nur wenn du einen anderen FRP1-Client ausdrücklich koppelst, eine erreichbare direkte Verbindung herstellst und dein Profil für diesen Kontakt freigibst, wird es direkt dorthin übertragen.

**„Öffentliches Profil“ bedeutet hier ein zum Teilen vorgesehenes Profildokument, keine Veröffentlichung im Internet.** Es wird nicht automatisch auffindbar oder für alle Spieler sichtbar.

Private Profil- und Charakternotizen, Beziehungen, Favoriten, Besuchsverlauf, lokale Content-IDs, eigene Charakterzuordnungen, Altersauswahl und persönliche Einstellungen bleiben ausschließlich lokal. Sie sind kein Bestandteil des geteilten Profils und werden nicht an andere Clients geschickt. Für den Inhaltsfilter wird nur das notwendige Empfangsflag übermittelt, ob ein Client Inhalte ab 18 akzeptiert; kein Geburtsdatum und keine vollständige Altersangabe.

Es gibt keine FRP1-Accounts, Telemetrie, Relay-, STUN-, Discoverydienste oder IP-Broker. Ein aktiver Client nimmt direkte Verbindungen anderer ausdrücklich gekoppelter Clients entgegen. Du musst dafür keinen separaten Serverdienst oder Profildatenspeicher betreiben.

**GitHub hat eine andere Aufgabe:** Dort werden ausschließlich Plugin-Dateien, Quelltext und der Installerindex für Installation und Updates bereitgestellt. FRP1-Profile werden nicht zu GitHub übertragen. Die Softwaredownloads und FFXIV selbst verwenden selbstverständlich ihre jeweiligen Dienste; „serverfrei“ bezieht sich auf den Austausch und die Speicherung deiner FRP1-Profildaten.

```mermaid
flowchart LR
    G["GitHub: Plugin-Dateien und repo.json"] -->|"Nur Installation / Softwareupdates"| A["FRP1-Client A"]
    G -->|"Nur Installation / Softwareupdates"| B["FRP1-Client B"]
    A <-->|"Nur ausdrücklich gekoppelte und freigegebene Profile"| B
    A -->|"Nur lokal speichern"| PA["Private Notizen, Alterswahl und Einstellungen bei A"]
    B -->|"Nur lokal speichern"| PB["Private Notizen, Alterswahl und Einstellungen bei B"]
```

Das Diagramm zeigt die FRP1-Datenwege. FFXIV-Spielverkehr ist kein FRP1-Profiltransport.

## Installation über die .json-Adresse

**FRP1 ist veröffentlicht:** Das bestehende Repository ist öffentlich, und Installerindex sowie Releasepaket sind ohne Anmeldung erreichbar.

Für Installation und Benutzung ist **kein GitHub-Konto** erforderlich. Die öffentliche JSON-Datei und das ZIP-Paket können ohne Anmeldung heruntergeladen werden; FRP1 verlangt ebenfalls kein Konto.

Installeradresse für **Custom Plugin Repositories**:

```text
https://raw.githubusercontent.com/demkadse/Final-Roleplay-1/master/repo.json
```

1. Nutze XIVLauncher/Dalamud auf Windows mit kompatibler Dalamud API 15. FRP1 ist kein eigenständiges Programm und kein Konsolenplugin.
2. Öffne die Dalamud-Einstellungen mit `/xlsettings`.
3. Gehe zu **Experimental / Experimentell** und **Custom Plugin Repositories / Benutzerdefinierte Plugin-Repositories**.
4. Füge die vollständige obenstehende **.json-Adresse** hinzu, aktiviere den Eintrag und speichere.
5. Öffne `/xlplugins`, aktualisiere bei Bedarf die Liste und suche **Final Roleplay 1**.
6. Installiere das Plugin und öffne es mit `/frp`.

Die GitHub-Projektseite oder eine ZIP-Adresse gehört nicht in das Repositoryfeld. Der Index ist eine JSON-Liste, aus der Dalamud die eigentlichen Installations-/Updatepakete lädt. Grundlage ist das [offizielle Dalamud-Verfahren für eigene Repositories](https://dalamud.dev/plugin-publishing/custom-repositories/).

Projekt: [demkadse/Final-Roleplay-1](https://github.com/demkadse/Final-Roleplay-1), Branch `master`. Das [Release v0.2.0.4](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.4) enthält [FRP1-0.2.0.4.zip](https://github.com/demkadse/Final-Roleplay-1/releases/download/v0.2.0.4/FRP1-0.2.0.4.zip) und [SHA256-Prüfsummen](https://github.com/demkadse/Final-Roleplay-1/releases/download/v0.2.0.4/SHA256SUMS). Das Release ist ein öffentlicher früher Teststand/Prerelease und kein Entwurf.

## Die ersten Minuten

1. Lies und bestätige die Profilregeln. Sie bleiben später unter **Einstellungen → Profilregeln und Hinweise** erreichbar.
2. Gib für diese Pluginsitzung aktiv an, ob du mindestens 18 Jahre alt bist. Erwachsene müssen Inhalte ab 18 zusätzlich freigeben, wenn sie diese sehen möchten.
3. Erstelle unter **Mein Profil** ein Profil und speichere es lokal.
4. Öffne `/frpbar` und wähle dein gespeichertes Profil ausdrücklich als aktives RP-Profil für den angemeldeten Charakter. Das erste vorhandene Profil wird nicht automatisch aktiviert.
5. Lies das gespeicherte Profil im Journal und richte bei Bedarf Darstellung und Hilfen ein.
6. Nutze es vollständig lokal oder kopple einen Kontakt für den direkten Austausch.

**Speichern, aktivieren und teilen sind verschiedene Entscheidungen.** Speichern legt die lokale Profilversion ab. Aktivieren ordnet sie deinem lokalen Spielcharakter zu. Zum Teilen bestimmst du dieses aktive Profil zusätzlich ausdrücklich im Netzwerkbereich und erteilst dem gewünschten Peer eine eigene Freigabe.

Die ausführliche Schritt-für-Schritt-Anleitung einschließlich Fehlersuche steht im [deutschen Benutzerhandbuch](docs/benutzerhandbuch.md).

## Was du gestalten und verwalten kannst

Die Übersicht nennt die vorhandenen Module. Seit 0.1.1.1 gilt die im nächsten Abschnitt erklärte vereinfachte Bedienung: Vorhandene zusätzliche Profilinhalte bleiben erhalten und gezielt entfernbar; neue Baukästen dafür werden nicht angeboten.

| Bereich | Funktionen |
|---|---|
| Charakter | RP-Name/Titel, Volk/Clan, Kurzbeschreibung, Pronomen, Herkunft, Beruf, Aussehen, Persönlichkeit, Hintergrund, freie Felder und OOC-Informationen. |
| RP-Informationen | IC/OOC/AFK, offen/beschäftigt/kein RP, fünf Glances, zehn Anknüpfungspunkte, Traits, Gesprächspräferenzen und beschriebene RP-Sprachen. |
| Journal und Medien | Drei Leselayouts, begrenztes Rich Text, Standarddarstellung, grafischer Spielicon-Picker und lokaler Portraitzuschnitt. |
| Begleiter | Begrenzte Beschreibungen eigener Begleiter; keine Veränderung tatsächlicher Pets oder Mounts. |
| Eigene RP-Leiste | Profilwechsel, Status, Verfügbarkeit, Journalzugriff und anklickbare eigene Glances; beweglich, sperrbar und lokal skalierbar. |
| Gestaltung | Drei primäre Themes Obsidian, Pergament und Nachtblau; bisherige elf Theme-IDs bleiben ohne Datenmigration lesbar. |
| Privates Register | Profil-/Charakternotizen, Favoriten, Beziehungen, Stummschaltung, Netzblock, Suche und Verlauf geöffneter Profile. |
| Spielanzeigen | Mouseover, bewegliche/pinnbare Zielkarte, nativer Kontextmenüeintrag, optionale lokale Nameplates/Titel/Farben/Profilicons und General-Chatnamen. |
| Umgebung/Karte | Aktuell sichtbare bekannte Akteure, Filter, Anvisieren und Karte für einen bekannten lokalen Akteur. Keine weltweite Suche. |
| Werkzeuge | Höchstens 16 deklarative Automationsregeln, standardmäßig aus; lokale Rede-/Emote-Entwürfe und interne Verweise auf vorhandene erlaubte Profile. |
| Hilfe/Sprache | Globale kontextuelle Hilfen und jederzeit erreichbare Erste Schritte. Neue Module sind DE/EN; ältere Host-/Hilfetexte teilweise deutsch. |

Portraits werden lokal zu metadatenfreien PNG-Miniaturen bis 256 × 256 und 12 KiB normalisiert. Das gesamte öffentliche Profil bleibt höchstens 48 KiB groß. Es gibt keine externen Bildabrufe, eingebetteten Skripte oder automatisches Abspielen fremder Medien.

Die RP-Präferenzen beschreiben Gesprächs- und Spielwünsche. Sie sind keine Zustimmung zu Handlungen und kein Altersnachweis.

## Aktuelle Bedienung

**Mein Charakter** öffnet die eigene Lesekarte. **Bearbeiten** bietet Speichern, Verwerfen und Vorschau. **Überblick**, **Charakter** und **OOC** bündeln die Inhalte; weitere optionale Felder ergänzt **+ Angabe**. Profilvarianten, JSON und lokale Werkzeuge liegen im Menü.

Die eigene RP-Leiste ist einzeilig, einklappbar und bleibt beim eigenen aktiven Profil. Fremde Informationen erscheinen separat als kompakte Ziel-/Mouseoveranzeige. Kleine Glance-Icons liefern bei Hover, Fokus oder Klickpin vollständige scrollbare Texte.

**Kontakte** bündelt Liste, Auswahlaktionen und Umgebung. Netzwerkdetails liegen unter **Erweitert**. Die primäre Gestaltung bietet Obsidian, Pergament und Nachtblau; ältere Theme-IDs bleiben lesbar. Kontextuelle Hoverhinweise und zentrale Hilfe ersetzen sichtbare Hilfebadges. Vorhandene Daten, Kopplungen und Freigaben bleiben erhalten.

Die genaue Bedienung steht in [Handbuch, Abschnitt 19](docs/benutzerhandbuch.md#19-kompakte-bedienung-seit-0204). Native Bedienung, Fokus und Skalierung im Spiel sind noch nicht abgenommen.

## Wie zwei Spieler Profile austauschen

1. **Beide** starten unter **Einstellungen → Direktverbindungen** ihren Clientlistener nach Bestätigung der direkten IP-Sichtbarkeit. Eine Einladung kann erst für einen gestarteten Listener erstellt werden.
2. Beide kopieren ihre eigene Einladung und tauschen diese über einen vertrauenswürdigen Weg aus.
3. Beide fügen die Einladung des anderen ein und wählen **Einladung koppeln – ohne Profilfreigabe**.
4. Eine Person startet die Verbindung zum gekoppelten Kontakt. Die Clients authentifizieren einander anhand der bestätigten Fingerprints.
5. Jede Person wählt das eigene aktive Profil über **Dieses aktive Profil zum Teilen verwenden** ausdrücklich zum Austausch.
6. Jede Person setzt für den gewünschten Kontakt **Profil für diesen Kontakt freigeben**.

Erst die Kombination aus erlaubtem Netzbetrieb, beidseitiger Kopplung, gültiger Verbindung, ausdrücklich gewähltem Teilprofil und jeweiliger Peerfreigabe erlaubt den Austausch. Änderungen eines bereits aktiven und freigegebenen Profils werden nach dem Speichern automatisch anhand Revision und Hash synchronisiert.

Die dauerhafte Identität wird unter Windows mit DPAPI im lokalen Benutzerkontext geschützt. Gespeicherte Kopplung beweist den Besitz eines Schlüssels, nicht den Besitz eines FFXIV-Charakters. Für Anzeigen fremder Profile muss Name/Homeworld zusätzlich ausdrücklich lokal der richtigen Peer-/Profilidentität zugeordnet werden.

## Grenzen direkter Verbindungen

Die Gegenstelle sieht bei einer direkten Verbindung deine IP-Adresse. Ohne vermittelnde Dienste lässt sich nicht garantieren, dass zwei beliebige Heim-/Mobilfunknetze erreichbar sind: Firewalls, NAT und CGNAT können Verbindungen verhindern.

Ein fester lokaler Listenport und eine manuell angegebene numerische Einladungsadresse sind optionale Hilfsmittel. Sie ermitteln keine öffentliche IP, öffnen keine Ports und ändern weder Router, Firewall noch DNS. Eine Einladung umgeht diese Netzgrenzen nicht.

Das Plugin benötigt keine manuelle Einrichtung eines FRP1-Servers; es kann trotzdem sein, dass eine Direktverbindung in einem bestimmten Netz nicht möglich ist. Das ist keine fehlende Registrierung oder erforderliche Cloudfreischaltung.

## Profilregeln, Alter und Widerruf

Allgemein/jugendfrei gekennzeichnete Profile dürfen keine sexuellen oder sonstigen expliziten Inhalte ab 18 enthalten – auch nicht in Namen, Titeln, Glances, Freitexten oder Bildern. Die Kennzeichnung gilt für das gesamte Profil. Gib das Alter deiner realen Person wahrheitsgemäß an.

Sexualisierte Minderjährigendarstellungen, unerlaubte private Daten anderer, Belästigung und Inhalte ohne nötige Nutzungsrechte sind untersagt. Die Regeln sind lokal lesbar und versionsgebunden zu bestätigen; es gibt keine zentrale Profilprüfung oder Moderationsinstanz.

Nach jedem Pluginstart verlangt FRP1 erneut die aktive Alterswahl. Erwachsene müssen Mature-Inhalte zusätzlich erlauben. Gespeicherte Regelbestätigung, Theme oder Profilpräferenz erteilen keine Altersfreigabe. **Die Angabe bleibt Selbstauskunft, keine Altersverifikation.** Falsche Angaben oder bewusst falsche Inhaltskennzeichnungen können nicht zuverlässig erkannt werden.

Bei Widerruf werden Verbindungen geschlossen und gesperrte Ansichten entfernt. Bereits empfangene Dateien auf einem anderen Rechner können dadurch nicht zurückgeholt werden. Auch der eigene bestehende Cache wird nicht allein durch einen Alterswechsel gelöscht oder verschlüsselt.

Chatnamensersetzung verwendet ausschließlich allgemein gekennzeichnete Profile, weil bereits ausgegebener Chat nicht zuverlässig zurückgenommen werden kann. Mature-Namen bleiben auch bei Erwachsenenfreigabe aus diesem bleibenden Chat ausgeschlossen.

## Updates, Prüfung und Entwicklungsstand

### Automatische Releases nach Master-Pushes

Die GitHub-Actions-Einrichtung ist mit Nutzercommit `ffe036c` eingeführt. Relevante Quellcode-, Build-, Test-, Workflow- oder Lizenzänderungen auf `master` starten Build, Tests, Paketprüfung und Veröffentlichung. Reine Dokumentations-/Indexänderungen erzeugen keine Releaseschleife; ein bewusster manueller Start ist auf `master` möglich.

Die CI-Version entsteht aus Basis `0.1.1` und Laufnummer minus eins als vierter Komponente: Lauf 1 verwendet `0.1.1.0`, Lauf 2 `0.1.1.1`. Erst nach erfolgreicher Veröffentlichung und anonymer Paket-/Hashprüfung schreibt ein Bot ausschließlich den erzeugten `repo.json`-Index zurück. Die Installeradresse bleibt unverändert. Es wird nur das integrierte `GITHUB_TOKEN` benötigt, kein zusätzliches PAT, Profilserver oder Nutzerkonto.

Die Automatik veröffentlicht Quellcommit `6c2e491` erfolgreich: [Lauf 43296113100](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43296113100) besteht Build und Publish. Die Protokolle bestätigen ausdrücklich 510/510 Funktionstests, 33/33 Paketfälle, 33 Policyfälle, 24 Publishabläufe, 44 SDK-Fälle und vier Compilerregressionen. Anonyme JSON-/ZIP-/Prüfsummendownloads und erneute vollständige Paketvalidierung sind erfolgreich. Details: [Status](STATUS.md), [Releaseautomatik](docs/auto-release.md).

Softwareupdates laufen über den normalen Dalamud-Installer und die Repositoryadresse; FRP1 betreibt keinen eigenen automatischen Updater und keinen Profilserver. Profiländerungen zwischen bereits verbundenen freigegebenen Peers sind davon unabhängig.

Historischer Stand 0.1.0.0: 467/467 Funktionstests, Build mit 0 Warnungen/Fehlern, unabhängige Reviewfreigabe und acht Probegruppen mit zwei echten Loopback-TLS-Clients. Dieser Nachweis bleibt getrennt vom neuen 500er-Prüfstand.

Die öffentliche JSON-Adresse liefert Version 0.2.0.4/API 15. JSON, ZIP und Prüfsummen wurden anonym geladen, gemeinsam per SHA-256 geprüft und erneut paketvalidiert. Das ist eine Distributionsprüfung, keine Spiel-/Netzabnahme.

**Nicht tatsächlich abgenommen:** Bedienung/Renderdarstellung im laufenden Spiel, reales LAN/Internet, NAT/CGNAT und umfangreiche Belastung. Dies ist ein früher Teststand, keine Zusicherung reibungsloser Verbindung zwischen beliebigen zwei Spielern.

Taschen/Inventar und TRP3 Extended sind ausgeschlossen. WoW-/MSP-Import, Musik und lokale UI-Klick-/Bestätigungstöne sind nicht implementiert. RP-Sprachen sind Profilangaben, kein Eingriff in Spielsprache. NPC-Werkzeuge sind ausschließlich lokale Textentwürfe, keine NPC-Imitation oder Chatautomation.

## Für Entwickler

Windows, .NET-10-SDK, lokale Dalamud-API-15-Entwicklungsbibliotheken und `Dalamud.NET.Sdk/15.0.0` sind die Buildgrundlage. Normale Installation über Dalamud benötigt keinen eigenen Quellcodebuild.

```powershell
dotnet build FRP1.slnx -c Release --nologo
dotnet test FRP1.slnx -c Release --nologo -p:BuildInParallel=false
dotnet run --project tools/FRP1.Probe -c Release -- --self-test
```

Release-Ausgaben aus dem Entwicklungsbuild: `src/FRP1.Plugin/bin/Release/FRP1.Plugin.dll` und `src/FRP1.Plugin/bin/Release/FRP1.Plugin/latest.zip`. Das aktuelle veröffentlichte Installerpaket heißt `FRP1-0.2.0.4.zip`.

Weitere Dokumentation: [Benutzerhandbuch](docs/benutzerhandbuch.md), [Release-Notes 0.1.0.0](docs/release-notes-v0.1.0.0.md), [Status](STATUS.md), [Übergabe](HANDOFF.md), [Architektur](docs/architecture.md), [Machbarkeit](docs/feasibility.md), [TRP3-Abgleich](docs/trp3-feature-map.md), [Glance-Abgleich](docs/glance-feature-map.md), [UX/Gestaltung](docs/ux-and-vanity.md).

## Lizenz

FRP1-Projektcode und Dokumentation stehen unter der [MIT-Lizenz](LICENSE). Sie erlaubt auch kommerzielle Nutzung, Änderungen und Weitergabe unter Beibehaltung der Copyright- und Lizenzhinweise. Maßgeblich ist der vollständige Lizenztext.

Diese Projektlizenz gilt **nicht automatisch für Nutzerprofile, Profilbilder oder andere fremde Inhalte**. Über deren Rechte und Freigaben entscheiden die jeweiligen Rechteinhaber. Fremdkomponenten behalten ihre eigenen Bedingungen; siehe [mitgelieferte Hinweise](THIRD-PARTY-NOTICES.txt).

FRP1 ist eigenständig implementiert; keine Übernahme von TRP3-/Glance-Code oder WoW-Assets.

