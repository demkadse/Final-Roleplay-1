# TRP3-Funktionsabgleich

Stand: 2026-11-11. Die FRP1-Module sind im echten Plugin angebunden. Release-Build ohne Warnungen/Fehler, 467 bestandene Tests und unabhängige Reviewfreigabe belegen den Codeprüfstand; die native Spiel-/Netzabnahme ist ein weiterhin offener eigener Schritt.

Die Matrix hält den Funktionsumfang des **veröffentlichten 0.1.0.0** fest. Die neuere Bedienung besteht 500 Tests und ist im Nutzercommit 62a91a7 enthalten, aber noch nicht als Installerupdate veröffentlicht: fünf gleiche Bereiche, Kontext-MiniHUD und feldnahe Hilfen ohne neue Zusatzdaten-Baukästen. Releaseautomatik zunächst lokal vorbereitet; öffentlicher Installer bis zum erfolgreichen Lauf unverändert. Details: [UX](ux-and-vanity.md), [Status](../STATUS.md).

## Quelle und Lesart

Grundlagen sind die [offizielle TRP3-Beschreibung](https://github.com/Total-RP/Total-RP-3/blob/main/README.md) und der [offizielle Modulbaum](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules). Die Modulnamen sind die Strukturreferenz; die Einordnung unten ist der eigene Abgleich gegen den FRP1-Quellstand, keine Behauptung identischer WoW-Funktionen.

**Umgesetzt** bezeichnet eine vorhandene integrierte FRP1-Funktion. **Angepasst** bedeutet eine bewusst lokale/FFXIV-spezifische Entsprechung mit den genannten Grenzen. **Nicht übertragen** kennzeichnet fehlende beziehungsweise plattformbedingt ausgeschlossene Funktionen. Diese Bewertung ist keine Spielabnahme.

## Grundfunktionen

| TRP3-Funktionsbereich | FRP1-Stand | Grenze |
|---|---|---|
| Charakterprofile und schneller Profilwechsel | Umgesetzt: mehrere lokale Profile, aktive eigene Charakterzuordnung, Status/Verfügbarkeit, Glances/Hooks und freie Felder. | Keine automatische fremde Charakterverifikation. |
| Individuelle Charakterbeschreibung | Umgesetzt: Traits, Gesprächspräferenzen, beschriebene RP-Sprachen, drei Journallayouts und begrenztes Rich Text. | Präferenzen sind kein Consent-/Altersnachweis. |
| Begleiter und visuelle Darstellung | Umgesetzt: begrenzte Begleiterprofile, grafische Spielicons und lokaler Portraitzuschnitt. | Keine Änderung tatsächlicher Pets/Mounts oder ihrer Spieldaten. |
| Mature-Filter | Angepasst: aktive reale Alters-Selbstauskunft pro Sitzung plus zusätzliches Opt-in, Sperren vor Daten-/Medien-/Anzeigezugriff. | Keine Altersverifikation; richtige Inhaltskennzeichnung bleibt erforderlich. |
| Profilaustausch | Angepasst: direktes gepinntes mTLS, lokale DPAPI-Identität, Kopplung und Freigabe. | Kein WoW-Addonkanal/MSP, keine Infrastruktur- oder NAT-Garantie. |

## Alle offiziellen Kernmodule

| Offizieller Bezug | Einordnung und vorhandene FRP1-Funktion | Bewusste Abweichung |
|---|---|---|
| [Automation](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/Automation) | Angepasst: höchstens 16 deklarative Regeln für Login/Kampf/Duty/Gebiet und Status/Verfügbarkeit/Profilwahl. | Global standardmäßig aus; keine Skripte, Chatbefehle oder automatische Freigaben. |
| [ChatFrame](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/ChatFrame) | Angepasst: lokale RP-Namensdarstellung vorhandener Chatnachrichten. | Nur General-Namen; keine rückwirkend unentfernbaren Mature-Namen, keine Behauptung sämtlicher WoW-Chatfarben/-rahmen. |
| [ChatLinks](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/ChatLinks) | Angepasst: interne Referenzen öffnen vorhandene erlaubte lokale Profile. | Kein Profiltransport über Spielchat, keine externen URL-Abrufe und keine automatische Beschaffung unbekannter Profile. |
| [Dashboard](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/Dashboard) | Angepasst: Hauptfenster, Profil-/Journalansicht, Kontakte und lokale Werkzeuge. | Eigenständige Dalamud-UI, keine WoW-Oberflächenkopie. |
| [Importer](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/Importer) | Angepasst: streng validierter FRP1-JSON-Import/-Export; Import als neue lokale Kopie. | TRP2-/MyRolePlay-/XRP-/MSP-Import nicht übertragen. |
| [Languages](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/Languages) | Angepasst: öffentliche RP-Sprachenbeschreibung und lokale DE-/EN-Oberflächenauswahl. | Keine Manipulation/Simulation der Spielsprache; ältere Host-/Hilfetexte teils deutsch. |
| [Launcher](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/Launcher) | Angepasst: `/frp`, `/frpbar`, Pluginmenü und eigene RP-Leiste. | Kein WoW-Minimapbutton erforderlich oder übernommen. |
| [Map](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/Map) | Angepasst: Karte für einen bekannten aktuell lokal vorhandenen Akteur. | Kein globales RP-Positionsverzeichnis, Zonenscan oder weltweite Suche. |
| [NamePlates](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/NamePlates) | Angepasst: optionale lokale Namen/Titel/Farben und kleine Profilicons über Figuren. | Icons standardmäßig aus; Questmarker unangetastet. Nur aktuelle erlaubte bestätigte Bindungen; Spielabnahme offen. |
| [Register](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/Register) | Umgesetzt: privates Register, Profil-/Charakternotizen, Favoriten, Beziehungen, Mute/Netzblock, Suche und gespeicherte Profilbesuche. | Kein automatisches Protokoll aller vorbeilaufenden Figuren; keine zentrale Datenbank. |
| [TargetFrame](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/TargetFrame) | Angepasst: eigene bewegliche Zielkarte, Zielpin, zulässiges Profil und lokale Aktionen. | Keine Musik und keine umfassende Modifikation aller FFXIV-Unitframes. |
| [Toolbar](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/Toolbar) | Umgesetzt: eigene bewegliche RP-Leiste für Profil, IC/OOC/AFK, Verfügbarkeit und eigene Glances. | Lokale explizite Zuordnung; kein erstes Profil still aktivieren. |
| [TooltipSkins](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/TooltipSkins) | Angepasst: elf FRP1-Themes sowie kompakte/ausführliche zulässige Mouseoveransicht. | Keine WoW-Skins/-Assets; Standarddarstellung bleibt das eigene lesbare FRP1-Theme. |
| [UnitPopups](https://github.com/Total-RP/Total-RP-3/tree/main/totalRP3/Modules/UnitPopups) | Angepasst: nativer Kontextmenüeintrag **FRP1-Profil öffnen** für bekannte bestätigte Charakterbindungen; weitere Profil-/Kontaktaktionen in Zielkarte, Umgebung und Register. | Kein globaler Umbau aller FFXIV-Kontextmenüs. |

## Nicht als vorhanden ausgeben

- NPC-Sprache: nur lokale Rede-/Emote-Entwürfe mit Vorschau/Kopiertext; keine NPC-Imitation, Flüster-/Rufautomation oder Packet-Manipulation.
- Fremdaddon-Interoperabilität: keine MSP-/WoW-Addonkommunikation oder automatische TRP3-Profilsuche.
- Weltkarte: kein serverfreier weltweiter Präsenzindex; nur bekannte lokale Akteure.
- Medien: keine importierten WoW-Assets, externe Medienabrufe oder Musik-/Autoplayfunktion.
- Taschen/Inventar, Handel, Quests und andere TRP3-Extended-Systeme sind ausdrücklich nicht beauftragt.

FRP1 übernimmt keine TRP3-Quelltexte oder Assets. Die Quellen sind Referenzen der Entwicklung, keine im Produkt verwendeten Server. [STATUS.md](../STATUS.md) hält die tatsächlich abgeschlossenen Prüfungen fest; reale Spiel-/LAN-/Internetabnahme bleibt offen.

