# Serverfreier Glance-Funktionsabgleich

Stand: 2026-11-11. Die bisher beschriebenen Folgepakete sind im nativen Plugin integriert. Abschließender Release-Build, 467 Tests und unabhängiges Review sind bestanden; die Spielabnahme bleibt offen.

Diese Matrix beschreibt die vorhandenen Module. Die neuere UX mit fünf Bereichen, MiniHUD und feldnahen Hilfen ist seit 0.1.1.1 veröffentlicht und im aktuellen Installerpaket 0.1.1.2 enthalten. Die Releaseautomatik ist erfolgreich gehostet geprüft. Zusatzinhalte bleiben erhalten und gezielt entfernbar, ohne neue Baukästen. Unterschiede: [UX](ux-and-vanity.md), [Status](../STATUS.md).

Referenzen: [Glance](https://rphub.co/glance), [offizielles Repository](https://github.com/rphubco/glance), [Profilmodelle](https://raw.githubusercontent.com/rphubco/glance/master/Models/ProfileModels.cs), [Editor](https://raw.githubusercontent.com/rphubco/glance/master/UI/Components/ProfileEditor.cs), [Einstellungen](https://raw.githubusercontent.com/rphubco/glance/master/UI/Windows/Tabs/SettingsTab.cs), [Profiltab](https://raw.githubusercontent.com/rphubco/glance/master/UI/Windows/Tabs/ProfileTab.cs), [Toolbox](https://raw.githubusercontent.com/rphubco/glance/master/UI/Windows/ToolboxWindow.cs). Eigene Implementierung; keine Übernahme des AGPL-3.0-Quellcodes.

| Funktion | FRP1-Umsetzung | Grenze/Abnahme |
|---|---|---|
| RP-Name/Titel, Volk/Clan, Freitext/freie Felder | Im Editor, Journal, JSON und gepinntem Direktaustausch integriert. | Maximal 48 KiB; kein Charakterbesitznachweis aus dem Namen. |
| Kurztext, Pronomen, IC/OOC/AFK, Ort/FC, OOC | Explizite Profilfelder mit Vorschau und Übertragung. | Keine automatische Ortsveröffentlichung. |
| Fünf Glances und zehn Hooks | Geordnete editierbare Einträge, grafische Spielicons und lesbare eigene Glance-Popups. | Medien-/Textgrenzen und Altersgate. |
| Mehrere Profile/aktives Profil je Charakter | Explizite lokale Zuordnung, Wiederherstellung beim Charakterwechsel und eigene RP-Leiste. | Content-ID bleibt lokal; kein erstes Profil automatisch aktiv. |
| Private Profil-/Charakternotizen | Privates Register integriert. | Niemals Export/Wire-Profil. |
| Mute, Favoriten und Block | Lokal integriert; Darstellungsmute und tatsächlicher Netzblock getrennt. | Netzblock beendet/verhindert Austausch. |
| Profiltooltip/Lesefenster/Zielkarte | Mouseover, kompakt/ausführlich, eigenes Journal und bewegliche/pinnbare Zielkarte. | Nur aktuelles erlaubtes Profil mit bestätigter Bindung. |
| Umgebung/Entfernung/Anvisieren | Aktuelle lokale Actoranzeige mit Status-/Verfügbarkeits-/Leerfiltern und Profilaktionen. | Sichtbarkeit entdeckt keinen FRP1-Peer. Kein automatischer Besuchsverlauf für alle Akteure. |
| Nameplates/RP-Name/Titel/Icon/Farbe | Explizite lokale Anzeigeoptionen; kleine Profilicons standardmäßig aus. | Questmarker unverändert; native Spielabnahme offen. |
| Chatnamen | Lokale Ersetzung vorhandener Chatnamen aus General-Profilen. | Keine Mature-Namen im nicht rückholbaren Chat und kein Chattransport. |
| Toolbox | Eigene RP-Leiste für Profil, Status, Verfügbarkeit, Profilöffnung und Glances. | Von fremder Zielkarte getrennt. |
| Cache/Suche/Aktualisierung | Lokaler Cache, Suche/Verwaltung, privates Register und UI-P2P-Synchronisierung. | Löschen entzieht keine bestehende Peerfreigabe. |
| Portraits/Iconauswahl/Profilgestaltung | Lokaler Zuschnitt/Normalisierung, grafischer Iconpicker, drei Layouts und begrenztes Rich Text. | Keine URL-Abrufe; Standarddarstellung überstimmt Fremdgestaltung. |
| Filter/Kampf-/Dutykomfort | Lokale Filter und standardmäßig ausgeschaltete deklarative Automationen mit Overlayoptionen. | Kein Skriptsystem; vollständig konfigurierte Spieleffekte im Spiel abnehmen. |
| Sounds | Nicht implementiert. | Gemeint sind lokale UI-Klick-/Bestätigungstöne; diese sind von ebenfalls nicht implementierter Profilmusik zu unterscheiden. |

Traits, Begleiter, Gesprächspräferenzen und weitere TRP3-Anpassungen stehen in der [TRP3-Matrix](trp3-feature-map.md). Verfügbarkeit, aktive Alters-Selbstauskunft und Favoriten sind zusätzliche FRP1-Anforderungen; nicht als Glance-exklusive Funktionen ausgeben.

## Ausgeschlossen

RPHub-Accounts, Web-/Pluginsynchronisation, zentrale Beacon-Präsenz/Zonensuche, zentral verifizierter Charakterbesitz, Konsolenprofile ohne FRP1-Client und zentrale Moderation werden nicht übernommen. Keine zentrale Galerie, Forum-, Event- oder Communitydatenbank.

Eine lokale Umgebung/Kartenaktion ist kein weltweiter Präsenzdienst. Kryptografische Peer-Authentifizierung ersetzt keine Charakterbesitzprüfung. Alle Profil-/Medien-/Anzeigewege unterliegen aktuellen lokalen Freigaben; die Gegenstelle kann keine Altersauswahl erzwingen oder verifizieren.

Der integrierte Stand ist abschließend auf Code-/Testebene geprüft. Reale FFXIV-Bedienung und netzübergreifende Erreichbarkeit sind noch nicht abgenommen. [Status](../STATUS.md), [Architektur](architecture.md), [UX](ux-and-vanity.md).

