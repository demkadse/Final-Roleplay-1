# FRP1 – Benutzerhandbuch

Stand: 2026-11-11 · veröffentlichter früher öffentlicher Teststand 0.1.0.0 (Prerelease).

**Veröffentlicht:** Das bestehende Repository `demkadse/Final-Roleplay-1`, Branch `master`, ist öffentlich und MIT-lizenziert. Installerindex und Releasepaket sind ohne Anmeldung erreichbar und geprüft. Der zuvor geprüfte Codestand besteht 467 Tests und unabhängiges Review; zusätzlich bestehen 33 Offline-Paketprüfungen ohne Fehler. Eine echte Spiel-/LAN-/Internetabnahme steht noch aus.

**Zwei Bedienungsstände:** Kapitel 1–16 beziehen sich auf das veröffentlichte 0.1.0.0. Die neuere UX ist inzwischen im Nutzercommit `62a91a7` enthalten, aber noch nicht als Installerupdate ausgeliefert; [Abschnitt 17](#17-neue-lokale-ux--noch-nicht-veröffentlicht) erklärt fünf Bereiche, MiniHUD und Hilfen. Ihr 500er-Teststand ersetzt kein veröffentlichtes Downloadpaket. Die neue Automatik wird erst nach Nutzercommit/-push ihrer Einrichtung wirksam; siehe Abschnitt 18.

## 1. Was FRP1 ist

Final Roleplay 1 ist ein Dalamud-Plugin für Rollenspiel in Final Fantasy XIV. Du beschreibst deinen Charakter, pflegst RP-Status und erste Eindrücke und kannst Profile anderer ausdrücklich gekoppelter FRP1-Nutzer lesen.

FRP1 speichert Profile auf den beteiligten Rechnern. Es gibt **keinen FRP1-Profilserver, keine zentrale Profildatenbank, keine Registrierung und keine Cloudprofile**. Öffentliche Profile werden nur direkt zwischen den von dir ausdrücklich gekoppelten und freigegebenen Clients ausgetauscht. „Öffentlich“ bezeichnet das zum Teilen vorgesehene Dokument, nicht eine automatisch öffentliche Internetseite.

Private Notizen, Beziehungen, Favoriten, lokale Content-IDs, eigene Zuordnungen, Altersauswahl, Besuchsverlauf und Einstellungen bleiben lokal und werden gar nicht mit anderen Clients geteilt. Die Profildarstellung einer anderen Person kommt aus dem direkten Austausch oder deinem lokalen Cache, nicht aus einer Website. Es gibt keinen FRP1-IP-Broker oder automatischen Vermittlungsdienst.

GitHub liefert ausschließlich Plugin-Dateien, Quelltext und den Installerindex. **Für Installation und Benutzung brauchst du kein GitHub-Konto.** Die öffentlichen JSON-/ZIP-Downloads sind ohne Anmeldung verfügbar. FFXIV und Softwaredownloads nutzen ihre üblichen Dienste; sie werden nicht zu FRP1-Profildatenservern.

## 2. Voraussetzungen und Installation

Du brauchst einen Windows-FFXIV-Client mit XIVLauncher/Dalamud und kompatibler Dalamud API 15. FRP1 ist kein eigenständig startbares Programm und kein Plugin für Konsolen ohne Dalamud-Client. Für die normale Installation musst du keinen Quellcode bauen oder .NET-SDK einrichten.

Installeradresse:

```text
https://raw.githubusercontent.com/demkadse/Final-Roleplay-1/master/repo.json
```

1. Öffne im Spiel die Dalamud-Einstellungen mit `/xlsettings`.
2. Öffne **Experimental / Experimentell**.
3. Suche **Custom Plugin Repositories / Benutzerdefinierte Plugin-Repositories**.
4. Füge die vollständige JSON-Adresse ein, füge den Eintrag hinzu, aktiviere ihn und speichere.
5. Öffne den Plugin-Installer mit `/xlplugins` und aktualisiere bei Bedarf seine Liste.
6. Suche **Final Roleplay 1** und wähle **Installieren**.
7. Öffne FRP1 mit `/frp`.

In das Repositoryfeld gehört die `repo.json`-Adresse, nicht die GitHub-Startseite und nicht das ZIP. Das normale Verfahren beschreibt die [offizielle Dalamud-Dokumentation](https://dalamud.dev/plugin-publishing/custom-repositories/).

Das veröffentlichte [Release v0.1.0.0](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.1.0.0) enthält [FRP1-0.1.0.0.zip](https://github.com/demkadse/Final-Roleplay-1/releases/download/v0.1.0.0/FRP1-0.1.0.0.zip) und [SHA256SUMS](https://github.com/demkadse/Final-Roleplay-1/releases/download/v0.1.0.0/SHA256SUMS) im bestehenden [Projekt](https://github.com/demkadse/Final-Roleplay-1), Branch `master`. Es ist als früher Teststand/Prerelease markiert. Normale Installation erfolgt über den JSON-Index; das ZIP muss dafür nicht manuell entpackt werden.

## 3. Erststart: Regeln und tatsächliches Alter

Beim ersten Pluginstart ohne gültige Bestätigung der aktuellen Profilregeln öffnet FRP1 sein Hinweisfenster einmal automatisch. Lies die Hinweise, setze die zunächst leere Checkbox und bestätige. Erst eine erfolgreich gespeicherte Bestätigung öffnet den nächsten Schritt. Du kannst denselben Text später unter **Einstellungen → Profilregeln und Hinweise** nachlesen.

Die Kennzeichnung gilt für das gesamte Profil. Allgemein/jugendfrei darf keine sexuellen oder sonstigen expliziten Inhalte ab 18 enthalten – auch nicht im Namen, Titel, in Glances, Freitexten oder Bildern. Nicht jugendfreie Profile sind vollständig entsprechend zu kennzeichnen.

Die Regeln untersagen sexualisierte Darstellungen Minderjähriger, unerlaubte private Daten Dritter, Belästigung und Inhalte ohne nötige Nutzungsrechte. Es gibt keine zentrale Profilprüfung oder Sanktionsinstanz.

Danach wählst du aktiv **unter 18** oder **18 oder älter**. Gemeint ist deine reale Person, nicht das Alter des RP-Charakters. Erwachsene müssen Inhalte ab 18 zusätzlich erlauben, wenn sie solche Profile sehen möchten.

Wichtig:
- Hinweisbestätigung bleibt für die jeweilige Regelversion gespeichert; nach materiellen Änderungen kann erneute Bestätigung nötig sein.
- Alterswahl und zusätzliche Mature-Freigabe sind **nach jedem Pluginstart erneut** erforderlich.
- Minderjährige, Nutzer ohne Alterswahl und Erwachsene ohne zusätzliches Opt-in sehen keine entsprechend markierten Profile.
- Es werden keine Ausweise, Geburtstage oder Accounts verlangt. Die Angabe ist Selbstauskunft, keine Altersverifikation.
- Falsche Altersangaben und absichtlich falsch gekennzeichnete Profile können nicht zuverlässig erkannt werden.

## 4. Das Hauptfenster

`/frp` öffnet vier Haupttabs:

| Tab | Zweck |
|---|---|
| Mein Profil | Profile erstellen, bearbeiten, speichern, exportieren/importieren und im Journal lesen. |
| Umgebung | Aktuell sichtbare Akteure, bekannte erlaubte Profile, Filter und lokale Profil-/Zielaktionen. |
| Kontakte | Empfangene Profile und privates Register mit Notizen, Bindungen und Sperren. |
| Einstellungen | Netzwerk, Profilregeln, Altersfreigabe, Darstellung, Sprache, Hilfe, lokale Spielanzeigen und Werkzeuge. |

Die neuen Module bieten deutsche und englische Bedienung. Manche älteren Host- und Hilfetexte sind noch deutsch. Profiltexte selbst werden nicht übersetzt.

## 5. Profile erstellen, speichern und lesen

1. Öffne **Mein Profil** und lege ein neues Profil an.
2. Fülle RP-Namen, Titel, Grunddaten und gewünschte Beschreibungen aus.
3. Kennzeichne das **gesamte** Profil korrekt als allgemein oder ab 18.
4. Speichere den Entwurf lokal.
5. Öffne **Gespeichertes Editorprofil lesen**, um genau diese gespeicherte Fassung im Journal zu prüfen.

Editor und Journal haben verschiedene Aufgaben: Der Editor enthält deinen Entwurf. Das Journal liest gespeicherte Profildaten. Ein ungespeicherter Entwurf wird nicht allein durch Öffnen des Journals zur gespeicherten Fassung.

Für deinen angemeldeten Spielcharakter aktivierst du ein gespeichertes Profil ausdrücklich in der eigenen RP-Leiste. Es gibt keine stille Aktivierung des ersten Profils. **Aktives eigenes Profil lesen** öffnet dessen gespeicherte Ansicht.

Unterstützte Inhalte sind unter anderem Kurzbeschreibung, Pronomen, Herkunft/Beruf, Aussehen, Persönlichkeit, Hintergrund, freie Felder, OOC-Angaben, Traits, Gesprächspräferenzen, RP-Sprachen und begrenzte Begleiterprofile. RP-Präferenzen beschreiben Spielwünsche und sind kein Einverständnis zu Handlungen.

Die Gesamtgröße eines öffentlichen Profils ist auf 48 KiB begrenzt. Auch einzelne Texte und Listen haben Grenzen. Wenn Speichern/Import abgelehnt wird, prüfe Pflichtfelder, Inhaltsfreigabe, Textlängen und Gesamtumfang.

Im JSON-Import-/Exportbereich kannst du das öffentliche Profil als Text exportieren und bewusst kopieren beziehungsweise lokal sichern. Private Notizen, Altersauswahl und eigene Einstellungen sind darin nicht enthalten. Ein Export ist trotzdem eine vollständige Kopie der öffentlichen Profilinhalte und sollte entsprechend behandelt werden.

Für einen Import fügst du gültigen FRP1-JSON-Text ein und verwendest die Importfunktion. FRP1 validiert Schema, Größe und aktuelle Inhaltsfreigabe und öffnet eine **neue lokale Kopie**. Sie überschreibt nicht still ein vorhandenes Profil und wird weder automatisch aktiv noch für Peers freigegeben. Speichere und aktiviere sie anschließend nur, wenn du das möchtest.

## 6. Glances, Anknüpfungspunkte und eigene RP-Leiste

Glances sind bis zu fünf kurze „Auf den ersten Blick“-Einträge mit Bezeichnung, Text und optionalem Spielicon. Sie lassen sich bearbeiten und umordnen; vorbereitete Vorlagen sind Ausgangspunkte für eigene Texte. Zusätzlich sind bis zu zehn RP-Anknüpfungspunkte mit Titel/Text möglich.

Ein deaktivierter Glance wird dadurch nicht zur Privatnotiz: Er bleibt im Profildokument gespeichert. Wirklich private Informationen gehören in das getrennte private Register.

Die eigene Leiste öffnest du so:

| Befehl | Wirkung |
|---|---|
| `/frpbar` oder `/frp bar show` | Eigene RP-Leiste anzeigen. |
| `/frpbar toggle` oder `/frp bar toggle` | Sichtbarkeit umschalten. |
| `/frp` | Hauptfenster öffnen. |

Wähle das gespeicherte aktive Profil, IC/OOC/AFK und die RP-Verfügbarkeit. Eigene Glances bleiben per Klick lesbar, auch wenn kontextuelle Hilfen ausgeschaltet sind.

Position, Sichtbarkeit, Bewegungssperre und fensterlokale Skalierung von 0,75 bis 1,75 werden lokal gespeichert. Eine Bewegungssperre sperrt nicht die Buttons. Die Position bleibt auf die sichtbare Arbeitsfläche begrenzt. Logout blendet die Leiste nur vorübergehend aus.

Bei verändertem oder beschäftigtem Editor sind konkurrierende Leistenänderungen gesperrt. Öffne den Editor, speichere oder kläre den Entwurf und versuche es erneut. Ein veralteter Entwurf darf eine zwischenzeitliche Profiländerung nicht überschreiben.

## 7. Portraits, Icons, Journal und Themes

Der grafische Spielicon-Picker verwendet lokale Spielressourcen mit Suche/Kategorien. Du musst normale Icons nicht über technische IDs auswählen.

Für ein Portrait wählst du eine lokale Bilddatei und passt den Zuschnitt an. FRP1 erzeugt eine metadatenfreie PNG-Miniatur bis 256 × 256 und 12 KiB. Es wird kein externes Bildhosting verwendet und keine Bild-URL abgerufen. Nicht jedes beliebige Bild passt unverändert in diese Grenzen.

Drei Journal-Layouts – Abschnitte, Chronik und Kompakt – sowie begrenztes Rich Text ermöglichen unterschiedliche lesbare Darstellungen. Unterstützt werden Überschriften/Listen und unter anderem `[b]Text[/b]`, `[i]Text[/i]`, `[color=#RRGGBB]Text[/color]`. Es ist kein HTML- oder Skriptfeld.

Die **Standarddarstellung** neutralisiert fremde Gestaltung für bessere Lesbarkeit. Sie bleibt innerhalb des eigenen FRP1-Grundthemes und wechselt nicht zum Standard-Dalamud-Look.

Unter **Einstellungen → Darstellung** stehen acht Volkspaletten sowie Obsidian, Pergament und Mondlicht bereit. Automatik liest das Volk deines lokalen Spielcharakters; du kannst alle elf Themes unabhängig davon manuell wählen. Eine manuelle Wahl bleibt bis zum erneuten Aktivieren der Automatik bestehen und wird je lokalem Charakter gespeichert.

Ohne Charakter, während des anfänglichen Ladens oder bei Ladefehler bleibt Obsidian aktiv. Eine bewusste, noch nicht gespeicherte Auswahl wird während der Sitzung nicht still verworfen; bei Fehlern gibt es Wiederholungsmöglichkeiten.

## 8. Zwei Clients direkt koppeln

### Vor dem Verbindungsaufbau

Beide Personen benötigen FRP1, erfüllte Hinweise-/Altersauswahl und einen angemeldeten Charakter. Einander im Spiel sehen bedeutet noch nicht, den FRP1-Client automatisch zu finden.

Direkte Verbindung macht die IP-Adresse der beteiligten Peers gegenseitig sichtbar. NAT, CGNAT und Firewalls können Erreichbarkeit verhindern. FRP1 hat keinen Relay-/STUN-/Discoverydienst und richtet kein Portforwarding ein.

### Schritt 1: Auf beiden Rechnern den Listener starten

Öffnet jeweils **Einstellungen → Direktverbindungen**. Wählt einen passenden **lokalen Netzwerkadapter** und erlaubt die direkte IP-Sichtbarkeit für diese Sitzung. Startet dann ausdrücklich die Direktverbindung.

Beide Clients müssen ihren eigenen Listener starten, damit beide ihre eigene Einladung erstellen können. „Listener“ bedeutet nur den eingehenden Teil des aktiven Clients, keinen separat gehosteten Server.

Ein Listenport von 0 lässt automatisch einen lokalen Port wählen. Ein ausdrücklich fester Listenport ist optional. Die zusätzliche numerische Einladungsadresse ändert nur die Adresse im Ticket; sie ermittelt keine öffentliche IP und verändert weder Router noch Firewall oder DNS. Nutze diese Option nur mit tatsächlich bekannten passenden Verbindungsdaten.

### Schritt 2: Einladungen vertrauenswürdig austauschen

Jede Person wählt **Eigene Einladung kopieren** und übergibt ihre Einladung der anderen über einen vertrauenswürdigen Weg. Eine Einladung enthält Adresse, Port und öffentlichen Fingerprint, keinen privaten Schlüssel. Die IP-Sichtbarkeit gilt bereits für die darin enthaltenen Adressinformationen.

### Schritt 3: Gegenseitig koppeln

Beide fügen die Einladung der anderen Person in **Einladung des Kontakts** ein und wählen **Einladung koppeln – ohne Profilfreigabe**. Der Fingerprint wird dadurch ausdrücklich der Gegenstelle zugeordnet.

Kopplung allein veröffentlicht noch kein Profil. Die gesicherte Verbindung authentifiziert den Schlüsselbesitz; sie bestätigt nicht, welcher Spielcharakter einer Person gehört.

### Schritt 4: Verbindung herstellen

Nach beidseitiger Kopplung startet eine Person die Verbindung beim gekoppelten Kontakt. Die Clients authentifizieren einander automatisch anhand der bestätigten Pins. Beide müssen über den angegebenen Netzpfad erreichbar sein; FRP1 überspringt keine Firewalls.

### Schritt 5: Aktives Profil ausdrücklich zum Teilen wählen

Wähle zunächst in der eigenen Leiste dein gespeichertes aktives Profil. Im Netzwerkbereich wählst du zusätzlich **Dieses aktive Profil zum Teilen verwenden**.

Das ist von der lokalen Aktivierung getrennt. Ein nur lokal aktives Profil ist noch kein automatisch ausgewähltes Teilprofil.

### Schritt 6: Freigabe je Peer setzen

Jede Person setzt beim gewünschten gekoppelten Kontakt **Profil für diesen Kontakt freigeben**. Nur das eigene ausgehende Profil wird dadurch für diesen Peer freigegeben. Die andere Person entscheidet unabhängig über ihr eigenes Profil.

Danach können die Clients zulässige Profile austauschen. Kopplung, Teilprofilwahl und Peerfreigabe müssen auf beiden Seiten jeweils passend gesetzt sein, damit beide Personen ein Profil erhalten.

### Änderungen und Neustart

Speicherst du Änderungen am bereits zum Teilen gewählten aktiven Profil, synchronisiert FRP1 sie automatisch anhand Revision/Hash. Unveränderte Profile werden nicht ständig erneut als vollständiger Inhalt geschrieben.

Die lokale DPAPI-Identität und Kopplungen sind dauerhaft. Nach Neustart sind die Altersauswahl und bewusste Aktivierung der Sitzung erneut nötig; prüfe Teilprofil und Peerfreigaben. Ein anderes aktives Profil muss erneut bewusst zum Teilen gewählt werden.

## 9. Fremde Charaktere zuordnen und Profile lesen

Ein empfangenes Profil ist zunächst an Fingerprint und Profil-ID gebunden. Für die passende Figur im Spiel bestätigst du im privaten Register ausdrücklich die Zuordnung von Name/Homeworld zu dieser Peer-/Profilidentität.

Prüfe sorgfältig, ob die Zuordnung stimmt. Ein Zertifikat beweist keinen FFXIV-Charakterbesitz. Ähnliche Namen oder eine bloße Anwesenheit in derselben Zone sind kein Nachweis.

Bei bekannter bestätigter Bindung und erlaubtem Profil stehen Mouseover, Zielkarte und der native Kontextmenüeintrag **FRP1-Profil öffnen** zur Verfügung. Die Zielkarte kann bewegt/gepinnt werden; Mouseover kann kompakt oder ausführlich sein. Die eigene RP-Leiste ist davon getrennt.

Die Umgebung zeigt aktuelle lokale Akteure und kann nach RP-Status, Verfügbarkeit oder leeren Profilen gefiltert werden. Eine Kartenaktion bezieht sich auf einen bekannten aktuell lokalen Akteur, nicht auf alle FRP1-Nutzer weltweit.

## 10. Privates Register, Cache und Sperren

Profilnotizen und Charakternotizen, Favoriten und Beziehungen bleiben auf deinem Rechner. Sie werden nicht in das öffentliche Profil exportiert oder an den Kontakt geschickt.

Der Verlauf erfasst tatsächlich geöffnete Profile. FRP1 behauptet keine automatische Protokollierung jeder vorbeilaufenden Figur.

| Aktion | Bedeutung |
|---|---|
| Mute/Stummschaltung | Unterdrückt die lokale Darstellung; keine Gleichsetzung mit tatsächlichem Netzblock. |
| Netzblock/Kontakt sperren | Beendet beziehungsweise verhindert Austausch mit dem Peer. |
| Profilfreigabe ausschalten | Stoppt die eigene Freigabe für diesen Kontakt. |
| Verbindung trennen | Beendet die aktuelle Verbindung. |
| Kopplung entfernen | Entfernt die gespeicherte Peer-Kopplung. |
| Cacheeintrag löschen | Entfernt den lokalen gespeicherten Profilstand; entzieht keine Freigabe und blockiert keinen Peer. |

Solange eine Verbindung und Freigabe weiter bestehen, kann ein gelöschter Cacheeintrag erneut ankommen. Um weiteren Austausch zu verhindern, nutze die passende Freigabe-/Verbindungs-/Blockfunktion.

Bereits an eine Gegenstelle übertragene Daten lassen sich nicht von deren Rechner zurückrufen. Das gilt unabhängig davon, ob es einen zentralen Server gibt.

## 11. Spielanzeigen bewusst einschalten

Nameplates, Titel, Profilfarben, Chatnamen und kleine Profilicons sind lokale Anzeigeoptionen. Kleine Profilicons sind standardmäßig aus und lassen Questmarker unverändert. Die echte Spiel-/Farb-/Iconabnahme steht noch aus.

Mature-Profile erscheinen nur in widerrufbaren Anzeigen nach aktueller Freigabe. **Chatnamensersetzung verwendet immer ausschließlich General-Profile**, weil bereits ausgegebener Chat nicht zuverlässig gelöscht werden kann. Sie ändert keine ausgehenden Nachrichten und transportiert keine Profile über den Spielchat.

Bei fehlender bestätigter Bindung, gesperrtem Profil oder Mute/Block darf die entsprechende RP-Anzeige nicht verwendet werden. Deaktivieren/Entladen beendet die FRP1-Hooks; fremde Plugins werden nicht global umgestaltet.

## 12. Hilfe, Sprache, Automation und lokale Werkzeuge

**Hilfshinweise anzeigen** unter **Bedienung und Hilfe** steuert die kontextuellen Hover-/Fokushinweise und Fragezeichen global. **Hilfe / Erste Schritte** bleibt auch bei ausgeschaltetem Schalter erreichbar. Eigentliche Glance-Inhalte sowie Pflichtregeln/Altersauswahl sind keine abschaltbaren Hilfetexte.

Neue Bereiche können DE/EN darstellen; einige ältere Texte bleiben deutsch. Das ändert keine Profilinhalte anderer Personen.

Lokale Automation ist global standardmäßig ausgeschaltet und auf höchstens 16 deklarative Regeln beschränkt. Regeln können bei Login, Kampf-/Duty- oder Gebietswechsel Status, Verfügbarkeit oder das aktive Profil ändern. Sie respektieren Sperren/laufende Entwürfe und erteilen keine Netz- oder Altersfreigabe. Es gibt keine Skripte, Chatbefehle oder Nachrichtenautomation.

Rede-/Emote-Werkzeuge erzeugen ausschließlich lokale Vorschau-/Kopiertexte. Sie imitieren keine NPC-Pakete und senden nichts selbstständig. Interne Profilverweise öffnen nur bereits vorhandene erlaubte Profildaten; sie lösen keine Suche oder Downloads fremder Profile aus.

Musik, lokale UI-Klick-/Bestätigungstöne und WoW-/MSP-Import sind nicht implementiert. Taschen/Inventar und TRP3 Extended sind ausdrücklich ausgeschlossen.

## 13. Updates und Datenspeicherung

Softwareupdates werden über den üblichen Dalamud-Installer und die öffentliche `repo.json` angeboten. FRP1 nutzt keinen eigenen Updateprozess außerhalb dieses Mechanismus. Dafür können öffentliche Dateien von GitHub heruntergeladen werden; Profildaten werden nicht dorthin hochgeladen.

Automatische Änderungen zwischen schon freigegebenen verbundenen Profilen sind ein anderer Vorgang und brauchen kein GitHub.

Eigene Profile, empfangener Cache und private Einstellungen liegen im Plugin-Konfigurationsverzeichnis. Der private Peer-Schlüssel ist mit Windows-DPAPI an den aktuellen Benutzerkontext gebunden. Profil-/Cachedateien werden durch einen Alterswechsel nicht automatisch gelöscht oder verschlüsselt.

Hinweisbestätigung bleibt versionsgebunden lokal gespeichert. Altersauswahl ist nach jedem Pluginstart neu erforderlich. Deine Themes, Hilfeoptionen, private Zuordnungen und Notizen erteilen keine darüber hinausgehende Freigabe.

## 14. Fehlersuche

| Problem | Prüfen |
|---|---|
| Repository/Plugin erscheint nicht | Vollständige raw-`repo.json`-Adresse statt GitHub-Seite/ZIP, Eintrag aktiviert und gespeichert, Liste neu geladen, API 15 kompatibel? Die öffentliche JSON-/ZIP-Erreichbarkeit wurde zur Veröffentlichung geprüft; spätere Netz-/GitHub-Störungen sind davon getrennt. |
| Hinweise/Alterswahl blockieren | Checkbox aktiv gesetzt, Bestätigung erfolgreich gespeichert, Alterswahl für diese neue Sitzung getroffen? Fehlerhafte Bestätigung gibt keinen Zugriff frei. |
| Profil kann nicht gespeichert werden | Pflichtname, richtige Inhaltsfreigabe, Text-/Bild-/Gesamtgrenzen; alte Entwurfsversion oder laufender Vorgang? Entwurf klären und Fehlermeldung beachten. |
| Kein aktives Profil | Zuerst lokal speichern, dann ausdrücklich in der eigenen Leiste für den aktuellen Charakter auswählen. |
| Einladung fehlt | Eigener Listener gestartet? Beide Seiten brauchen eine eigene gestartete Einladung. |
| Keine Verbindung | Gegenseitig richtige Tickets gekoppelt, vertrauenswürdige Fingerprints, passende numerische Adresse/Port, Gegenstelle aktiv? NAT/CGNAT/Firewall können sie trotz korrekter Bedienung verhindern. |
| Verbindung, aber kein Profil | Aktives Profil zusätzlich zum Teilen gewählt, Peerfreigabe gesetzt, andere Seite ebenfalls passend freigegeben, Alters-/Inhaltsfilter und Blockzustände geprüft? |
| Profil nicht über der Figur | Richtige bestätigte Name-/Homeworldbindung zum richtigen Fingerprint/Profil, aktuelle Szene, zulässiges Profil und gewünschte Anzeigeoption? |
| Gecachtes Profil kommt wieder | Cachelöschen ist kein Netzblock. Weiter bestehende Freigaben/Verbindungen getrennt ausschalten. |
| Position/Option nicht gespeichert | Sichtbare Lade-/Speicherfehler beachten und angebotene Wiederholung nutzen; alte asynchrone Ergebnisse dürfen neue Wahl nicht ersetzen. |
| Schlüssel-/Identitätsfehler | Windows-Benutzerkontext und Fehlermeldung prüfen. Nicht durch ungeprüftes Löschen/Ersetzen der Identität oder Abschalten von Prüfungen umgehen; private Identitätsdatei niemals öffentlich weitergeben. |

Für eine Fehlermeldung reichen ein klarer Ablauf und die sichtbare Fehlermeldung. Keine privaten Notizen, Profile, Einladungscodes oder Identitätsdateien öffentlich anhängen.

## 15. Stoppen, Freigaben zurücknehmen und abschalten

- Entferne die Profilfreigabe beim Peer, wenn du das eigene Profil nicht weiter übertragen möchtest.
- Nutze **Verbindung trennen** für die aktuelle Sitzung oder **Direktverbindungen stoppen** für den Netzwerkbetrieb.
- Nutze Netzblock/Kopplung entfernen für die entsprechende dauerhaftere lokale Entscheidung.
- Widerrufe Mature-Freigabe, um entsprechende Ansichten sofort zu sperren; das entfernt keine bereits fremd gespeicherten Daten.
- Schalte optionale Spielanzeigen aus oder deaktiviere FRP1 über den Dalamud-Installer. Entladen beendet die Hooks/Verbindungen; eigene lokale Daten werden nicht dadurch zu einem Cloudkonto.

Deaktivieren und lokale Daten löschen sind getrennte Entscheidungen. Eine Deinstallation oder Konfigurationslöschung im Installer sollte entsprechend der tatsächlich gewünschten Datenbehandlung gewählt werden.

## 16. Was geprüft ist und was noch offen bleibt

Der Codestand besteht 467 automatisierte Tests, den vollständigen Release-Build ohne Warnungen/Fehler und unabhängiges Review ohne kritische oder wichtige Restbefunde. Die zusätzliche Probe besteht acht Gruppen mit echten Loopback-TLS-Clients.

Für die Veröffentlichung bestehen separat 33 Offline-Paketprüfungen ohne Fehler und ein Paketbuilder ohne Warnungen/Fehler. JSON und ZIP wurden anonym mit HTTP 200 geprüft; der ZIP-Hash stimmt mit dem geprüften Kandidaten überein. Das war keine erneute Ausführung der 467 Funktionstests und keine reale Spiel-/Netzabnahme.

Reale Spielbedienung, Darstellung, LAN/Internet/NAT/CGNAT und umfangreiche Belastung sind noch nicht abgenommen. Es gibt keine Garantie, dass zwei beliebige Netze ohne weitere Infrastruktur eine direkte Verbindung erlauben.

Projektcode und Dokumentation stehen unter der [MIT-Lizenz](../LICENSE). Das erlaubt auch kommerzielle Nutzung unter Erhalt der Copyright-/Lizenzhinweise. Deine Profile und Bilder werden dadurch nicht automatisch unter MIT gestellt; maßgeblich sind die Rechte der jeweiligen Inhalte.

Technische Details: [Architektur](architecture.md), [Machbarkeit](feasibility.md), [TRP3-Abgleich](trp3-feature-map.md), [Glance-Abgleich](glance-feature-map.md). [Projektstatus](../STATUS.md) und [Release-Notes](release-notes-v0.1.0.0.md) halten Veröffentlichung und Abnahmestand fest.

## 17. Neue lokale UX – noch nicht veröffentlicht

Die folgenden Änderungen sind im Nutzercommit `62a91a7` vom 2026-11-11 enthalten, aber noch nicht im Installer. Öffentliches ZIP und Index 0.1.0.0 bleiben bis zur erfolgreichen neuen Veröffentlichung unverändert. Die zunächst lokal vorbereitete Releaseautomatik benötigt erst ihren Nutzercommit/-push und einen erfolgreichen Lauf; ein Sourcecommit allein reicht nicht.

### Profil bearbeiten und lesen

1. Öffne **Mein Profil** beziehungsweise den Profil-Leser. Beide verwenden dieselben fünf gleichrangigen Bereiche:

| Bereich | Zweck |
|---|---|
| RP-Status und Kurzprofil | Status, Verfügbarkeit und kompakte Charakterangaben. |
| OOC Informationen | Bewusst öffentliche OOC-Angaben. |
| Auf den ersten Blick | Die aktiven Glance-Eindrücke. Deaktivierte Einträge sind zunächst unsichtbar. |
| Weitere Charakterdaten | Zusätzliche Charakterbeschreibungen und erhaltene ältere Zusatzinhalte. |
| RP-Erfahrung und Absprachen | Gesprächs-/Spielpräferenzen, weiterhin ohne automatische Zustimmung zu Handlungen. |

2. Der kleine Profilkopf enthält Name, Titel, Portrait, Icon und Inhaltskennzeichnung. Darunter gibt es einen Inhaltsbereich statt einer zweispaltigen Bearbeiten/Vorschau-Anordnung.
3. Öffne die Gestaltung am Profilkopf. JSON-Import/-Export befindet sich unter den Profilaktionen; beides ist kein sechster Hauptbereich.
4. Speichere wie bisher lokal. Änderungen am bereits aktiven und freigegebenen Profil können weiterhin automatisch synchronisieren; Speichern erteilt keine neue Freigabe.

Neue Baukästen für Hooks, freie Zusatzfelder, Traits und Begleiter werden nicht mehr angeboten. Bereits vorhandene Einträge bleiben unter **Weitere Charakterdaten** lesbar. Du kannst einzelne Einträge bewusst aus dem Entwurf entfernen und die Änderung anschließend speichern. Unbearbeitete Sprachen bleiben unverändert erhalten.

**Ausblenden bedeutet nicht privat:** Solange Zusatzinhalte oder deaktivierte Glances im Profildokument stehen, gehören sie weiterhin zu den öffentlichen Profilinhalten und damit zu Export/Teilen. Beachte die sichtbaren Hinweise vor diesen Aktionen. Wirklich private Informationen gehören in das Register.

### Mein RP als MiniHUD

Das lokale **Mein RP** ist jetzt eine kompakte Leiste aus zwei Zeilen ohne Titelbalken. Der kleine Kopf zeigt Icon/Name sowie **Ich**, **Ziel** oder einen neutralen Zustand. Status-, Aktions- und Glance-Icons ersetzen die bisher große eigene Leiste.

- Ohne Ziel oder bei Selbstziel zeigt es das eigene Profil. Profilwahl und Änderungen bleiben ausdrückliche eigene Aktionen.
- Ein erkannter, erlaubter fremder Charakter wird nur lesend angezeigt.
- Unbekannte/NPC-, gesperrte, stummgeschaltete, nicht erlaubte Mature-, mehrdeutige oder szenenfremde Ziele zeigen neutralen Inhalt. Es gibt keinen stillen Rückfall auf das eigene Profil.
- Maßgeblich ist das echte aktuelle Spielziel. Eine separat gepinnte große Zielkarte steuert den HUD-Kontext nicht.
- Ein Zielwechsel verändert weder deinen Entwurf noch aktives eigenes Profil oder Sharing.

Per Kontextmenü erreichst du Profilwahl, Einstellungen, Hilfe und bewusst angeforderte Details. Die zusätzliche große Zielkarte ist bei neuen Einstellungen standardmäßig aus; vorher gespeicherte Sichtbarkeitsentscheidungen werden beibehalten.

Du kannst das HUD einklappen; der kleine Wiederöffner bleibt erreichbar und der Klappzustand wird getrennt lokal gespeichert. Bewegungssperre, Verschieben, Skalierung und Begrenzung auf die sichtbare Arbeitsfläche bleiben erhalten. Vollständig ausblenden und einklappen sind unterschiedliche Entscheidungen; die bekannten Show-/Toggle-Befehle bleiben verfügbar.

### Vollständige Glances und verständliche Hilfe

Fahre mit der Maus über einen Glance oder fokussiere ihn per Tastatur. Der vollständige Text erscheint ungekürzt in einer scrollbaren Karte, auch bei ausgeschalteten Hilfshinweisen. Ein kurzer Übergang von rund 500 ms erlaubt den Weg mit der Maus zur Karte; ein Klick kann sie anheften. Ziel-/Kontext- oder Freigabewechsel verwirft den Inhalt.

Hilfen stehen jetzt mit **? Hilfe: Titel** oder dem kompakten **? Hilfe** direkt am betroffenen Titel/Feld. Tooltip und geöffneter Hilfetext erklären das konkrete Thema; lose Fragezeichengruppen entfallen. Der globale Hilfeschalter wirkt weiterhin auf diese Hilfen, nicht auf eigentliche Glance-Inhalte oder **Erste Schritte**.

Der lokale Abschluss besteht den Release-Build mit 0 Warnungen/Fehlern, 500/500 Tests und unabhängige Reviewfreigabe. Native Spielabnahme fehlt weiterhin. Die transparente Testhistorie steht in [STATUS.md](../STATUS.md); historische Paket-/Downloadprüfungen des öffentlichen 0.1.0.0 gelten nicht als Veröffentlichung dieser neuen UX.

## 18. Vorbereitete automatische Softwareupdates nach einem Master-Push

Die neue GitHub-Actions-Einrichtung liegt zunächst nur lokal. Erst wenn der Nutzer Workflow/CI-Hilfen selbst auf `master` committet und pusht, kann ein relevanter Push Build, Tests, Paket und Prerelease auslösen. Ein grüner Sourcepush ohne erfolgreichen Actionslauf liefert kein neues Installerpaket.

Maintainer prüfen den Lauf im [Actions-Bereich](https://github.com/demkadse/Final-Roleplay-1/actions). Nach erfolgreicher Veröffentlichung und öffentlicher Hashprüfung schreibt ein Bot nur den erzeugten `repo.json`-Index zurück. Die Installeradresse bleibt gleich; normale Nutzer verwenden danach das übliche Dalamud-Update und benötigen weiterhin kein GitHub-Konto.

Nach einem solchen Botcommit muss der Maintainer seinen lokalen Branch vor dem nächsten Push abgleichen, beispielsweise mit `git pull --ff-only`; bei Abweichungen erst prüfen, nicht Force-Push verwenden. Die lokale Einrichtung besteht Build, 500 Funktionstests, 33 Paketprüfungen und 101 zusätzliche CI-Prüfungen; das unabhängige Review ist ohne kritische oder wichtige Restbefunde freigegeben. Ein erster gehosteter Lauf ist erst nach Nutzercommit/-push prüfbar. Versions-/Sicherheitsdetails und Fehlersuche: [Releaseautomatik](auto-release.md).

