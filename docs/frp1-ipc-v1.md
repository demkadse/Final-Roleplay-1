# Vertrag: FRP1-IPC v1

Stand: 2026-11-13 · **WP4 als [0.2.0.6](https://github.com/demkadse/Final-Roleplay-1/releases/tag/v0.2.0.6) veröffentlicht**, Quelle `6b97c1b`, automatisiert geprüft und unabhängig ohne kritische/wichtige Restbefunde reviewfreigegeben. Der [Lauf 43416697284](https://github.com/demkadse/Final-Roleplay-1/actions/runs/43416697284) besteht Build/Publish mit 812/812 Funktionstests, Paket33 und CI105; anonyme JSON-/ZIP-/Hash-/Paketprüfung erfolgreich. Die Registrierung und native Verdrahtung sind statisch geprüft und kompiliert, eine native IPC-/Spielprobe steht aus. Paketplan und Abnahme: [Integrationsplan, WP4](integrationen-plan.md#wp4--eigene-frp1-ipc).

## Freigaben und Datenumfang

Alle Freigaben beginnen ausgeschaltet: globale Lesefreigabe, globale Steuerfreigabe und drei Funktionsfreigaben für Status, Verfügbarkeit und Öffnen des eigenen Profils. Die optionalen Felder `ipcReadEnabled`, `ipcControlEnabled`, `ipcSetStatusEnabled`, `ipcSetAvailabilityEnabled` und `ipcOpenOwnProfileEnabled` bleiben zu `integrations.json`-Schema 1 kompatibel; fehlende Felder bedeuten `false`. Die lokale Datei bleibt auf 1 KiB begrenzt. Die Freigaben stehen unter Einstellungen → Integrationen → Lokale FRP1-Schnittstelle.

Der eigene Zustand bezieht sich auf `OwnProfileSession.ActiveProfile`, unabhängig vom gerade im Hauptfenster ausgewählten Entwurf. Lesbare Profildaten beschränken sich auf General-Status und General-Verfügbarkeit. Namen, Content-IDs, Profil-IDs, Notizen, Kontakte, Peerangaben und Schlüssel werden nicht exportiert. Adult-Opt-in erteilt keine Exportfreigabe: `GetOwnState` bleibt auch dann General-only. Steueraktionen auf einem Mature-Profil benötigen die aktuelle Auswahl Adult samt Mature-Opt-in und die jeweiligen IPC-Freigaben. IPC-Aufrufer sind keine authentisierten Identitäten; die Freigaben gelten für alle lokal geladenen Plugins und ersetzen keine Caller-Authentisierung.

## Channels und Rückgaben

| Channel | Signatur / Ergebnis | Bedeutung |
|---|---|---|
| `FRP1.ApiVersion` | `() → (uint 1, uint 0)` | Vertragsversion 1.0. |
| `FRP1.v1.GetCapabilities` | `() → int` | Bitmaske: Read `1`, Status `2`, Availability `4`, Open `8`. |
| `FRP1.v1.GetReadiness` | `() → int` | `0` Disabled, `1` Blocked, `2` Busy, `3` Ready. |
| `FRP1.v1.GetOwnState` | `() → (int result, int status, int availability)` | Nur mit Lesefreigabe und zulässigem General-Profil; `result = 0` bedeutet Erfolg, negative Werte entsprechen den Fehlercodes. Im Fehlerfall Datenfelder `-1, -1`. |
| `FRP1.v1.SetStatus` | `(int status) → long` | Positives Ticket bedeutet ausschließlich eingereiht; negativer Wert ist ein Fehler. |
| `FRP1.v1.SetAvailability` | `(int availability) → long` | Positives Ticket bedeutet ausschließlich eingereiht; negativer Wert ist ein Fehler. |
| `FRP1.v1.OpenOwnProfile` | `() → long` | Öffnen über die begrenzte Queue; positives Ticket ist keine Öffnungsbestätigung. |
| `FRP1.v1.GetActionResult` | `(long ticket) → int` | Begrenzte Abfrage des eigenen Auftragszustands, siehe unten. |

Statuswerte entsprechen `RpStatus`: `0` Ic, `1` Ooc, `2` Afk. Verfügbarkeitswerte entsprechen `RpAvailability`: `0` Open, `1` Busy, `2` Unavailable. Andere Eingaben werden abgewiesen.

| Fehlerwert | Bedeutung |
|---|---|
| `-1` | Disabled |
| `-2` | InvalidArg |
| `-3` | Blocked |
| `-4` | Busy |
| `-5` | Stale |
| `-6` | RateLimit |
| `-7` | Error |

| `GetActionResult` | Bedeutung |
|---|---|
| `0` | Unknown |
| `1` | Queued |
| `2` | Running |
| `3` | Persisted |
| `4` | Opened |
| `5` | AlreadySatisfied |
| Negativer Fehlerwert | Auftrag abgewiesen oder fehlgeschlagen. |

## Queue, Persistenz und Lebenszyklus

Höchstens ein wartender oder laufender Auftrag und ein letztes Ergebnis für 30 Sekunden bleiben gespeichert. Zwischen angenommenen Aktionen liegen mindestens 500 ms. Identische Anfragen zum noch ausstehenden Auftrag im selben gültigen Kontext erhalten dasselbe Ticket. Tickets sind positive `long`-Werte aus einem pro Instanz zufälligen 63-Bit-Ausgangspunkt mit anschließender Sequenz; sie sind keine Callerkennung oder Berechtigung.

Ein Ticket bestätigt ausschließlich die Einreihung. Erst `Persisted` bestätigt eine Status-/Verfügbarkeitsänderung, nachdem der eigene Schreibauftrag erfolgreich beendet und genau dessen gespeicherter Profilstand in den aktuellen `OwnProfileSession`-Kontext übernommen wurde. Endet der Schreibauftrag kurz nach dessen Poll, bleibt das Ergebnis während des zugehörigen Busy-Zustands `Running`, bis der nächste Poll den alten beziehungsweise gespeicherten Stand integriert hat. Ein tatsächlicher Kontext-/Revisionskonflikt wird `Stale`. `Opened` bestätigt das Öffnen, `AlreadySatisfied` einen bereits erfüllten Zielzustand ohne zusätzlichen Save.

IPC-Callbacks lesen nur abgetrennte Snapshots oder befüllen den begrenzten Queueplatz; sie greifen nicht auf native Spielobjekte zu. Erfassung, Öffnen und Übergabe an den Profilschreiber laufen auf dem Framework-/Frame-Thread. Globale und funktionsbezogene Freigaben, aktuelle Policy, Actor-/Profilidentität, Kontextgeneration sowie Busy-/Draft-/CAS-Zustand werden vor Einreihen und unmittelbar vor Ausführung geprüft. Änderungen zwischen diesen Zeitpunkten verwerfen den Auftrag ohne Nebenwirkung. Status-/Verfügbarkeitsänderungen verwenden den vorhandenen eigenen Profilschreiber; keine direkten Store-Schreibwege oder Entwurfsüberschreibungen. Regulär gespeicherte Änderungen können über bereits bestehende Profilfreigaben synchronisiert werden.

Policy-/Opt-out-Wechsel invalidieren den veröffentlichten Snapshot synchron über eine Epochennummer; auch letzte Ergebnisse sind an Generation und Epoche gebunden. Änderungen der IPC-Freigaben löschen das letzte Ergebnis. Widerruf und Unload invalidieren ausstehende Arbeit und beobachten bereits gestartete Schreibaufträge bis zu deren Abschluss, ohne alte Erfolge als aktuell auszugeben. Sensible Payloads werden auch bei Fehlern nicht protokolliert.

Automatisiert geprüft sind Standard aus, ungültige Argumente, General-only-Lesen, Alters-/Policygruppen, aktive Profilzuordnung unabhängig von der UI-Auswahl, Busy/Dirty/CAS, Queue-/Kontextwechsel, Deduplizierung, Rategrenze, Ticketablauf, Widerruf, Unload und die Übernahme des eigenen Speicherergebnisses. Lokaler Gesamtnachweis zu `6b97c1b`: 812/812 Funktionstests (27 Sekunden), 287 gezielte Tests, voller Release-Solutionbuild mit 0 Warnungen/Fehlern (2,54 Sekunden), 33/33 Paketprüfungen und unabhängige Reviewfreigabe. Diese Nachweise ersetzen keine native IPC-/Spielprobe.
