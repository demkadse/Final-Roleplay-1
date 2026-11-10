# FRP1 0.1.0.0 – früher öffentlicher Teststand

Stand: 2026-11-10 · **Veröffentlichung in Vorbereitung, noch nicht als erfolgreich bestätigt.**

Geplant: [demkadse/FinalRoleplay1](https://github.com/demkadse/FinalRoleplay1), Tag `v0.1.0.0`, Asset `FRP1-0.1.0.0.zip`, Installerindex:

```text
https://raw.githubusercontent.com/demkadse/FinalRoleplay1/main/repo.json
```

## Was FRP1 macht

FRP1 ist ein Windows-/Dalamud-API-15-Plugin für Charakterprofile in Final Fantasy XIV. Profile bleiben lokal und werden nach ausdrücklicher Kopplung/Verbindung/Freigabe direkt zwischen FRP1-Clients ausgetauscht.

**Keine FRP1-Profilserver, zentrale Datenbank, Registrierung, Cloudprofile, Telemetrie, Relays, STUN oder Discoverydienste.** GitHub liefert ausschließlich Software und Installerindex; Profildaten gehen nicht zu GitHub. Für Installation/Benutzung ist kein GitHub-Konto nötig.

Private Notizen, Beziehungen, Zuordnungen, Altersauswahl und eigene Einstellungen bleiben lokal. Sie sind nicht Bestandteil des geteilten Profils. Für Mature-Empfang wird lediglich das notwendige Empfangsflag ausgetauscht, keine vollständige Altersangabe.

## Enthalten

- Erweiterte Profile, fünf Glances, zehn Hooks, Traits, Gesprächspräferenzen, beschriebene Sprachen und Begleiter.
- Drei Journallayouts, begrenztes Rich Text, grafische Spielicons und lokale Portraitnormalisierung.
- Dauerhafte DPAPI-Identität, gegenseitiges TLS-Pinning, direkte Kopplung/Peerfreigabe und automatische Profilaktualisierung.
- Private Notizen, Favoriten, Beziehungen, Mute/Netzblock, Cacheverwaltung und Verlauf geöffneter Profile.
- Mouseover, bewegliche/pinnbare Zielkarte, gezielter Kontextmenüeintrag, lokale Spielanzeigen und optionales kleines Nameplate-Profilicon.
- Eigene RP-Leiste, elf native Themes, globale Hilfen und DE/EN in neuen Modulen.
- Begrenzte standardmäßig ausgeschaltete Automationen, lokale Textentwürfe und interne vorhandene Profilverweise.

## Wichtige Grenzen

Die direkte Verbindung legt den Peers ihre IP-Adressen gegenseitig offen. FRP1 konfiguriert keine Router/Firewalls, fragt keine öffentliche IP ab und garantiert keine Erreichbarkeit hinter NAT/CGNAT. Beide Clients müssen erreichbar und korrekt gekoppelt sein.

Altersauswahl ist aktive Selbstauskunft pro Pluginsitzung, keine Altersverifikation. Mature-Inhalte brauchen zusätzliches Opt-in. Die gesamte Profilkennzeichnung muss korrekt sein; absichtlich falsche Angaben werden nicht zuverlässig erkannt. Chatnamen stammen ausschließlich aus General-Profilen, weil Chat nicht rückholbar ist.

Es fehlen eine tatsächliche Spiel-/LAN-/Internetabnahme und umfangreiche Belastung. Einzelne Host-/Hilfetexte bleiben deutsch. Musik, lokale UI-Klick-/Bestätigungstöne und WoW-/MSP-Import sind nicht implementiert; Taschen/Inventar und TRP3 Extended sind ausgeschlossen.

## Prüfung

- Vollständiger Release-Build: 0 Warnungen, 0 Fehler.
- Gesamtsuite: 467/467 Tests bestanden, 0 fehlgeschlagen, 0 übersprungen.
- Unabhängiges Review: Freigabe ohne kritische oder wichtige Restbefunde.
- Zusätzliche reale Loopback-Probe: acht Gruppen bestanden.

Die Ergebnisse ersetzen keine tatsächliche Zwei-Spieler-Abnahme im laufenden Spiel. [Installation und Bedienung](benutzerhandbuch.md), [Projektbeschreibung](../README.md), [Status](../STATUS.md).

