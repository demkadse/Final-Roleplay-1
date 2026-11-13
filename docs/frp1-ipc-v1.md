# Geplanter Vertrag: FRP1-IPC v1

Stand: 2026-11-13 · **Festgelegter Vertrag für WP4, Umsetzung läuft; noch kein abgeschlossener Funktions-/Registrierungsnachweis.** Dieses Dokument beschreibt die geplante Schnittstelle nach dem als 0.2.0.5 veröffentlichten Honorific-MVP. FRP1-IPC v1 gehört noch nicht zum veröffentlichten Umfang. Dieses Dokument ist kein Nachweis verfügbarer IPC-Channels oder nativer Laufzeitkompatibilität. Paketplan und Abnahme: [Integrationsplan, WP4](integrationen-plan.md#wp4--eigene-frp1-ipc).

## Freigaben und Datenumfang

Alle Freigaben beginnen ausgeschaltet: globale Lesefreigabe, globale Steuerfreigabe und drei Funktionsfreigaben für Status, Verfügbarkeit und Öffnen des eigenen Profils. Die neuen optionalen Felder bleiben zu `integrations.json`-Schema 1 kompatibel; fehlende Felder bedeuten `false`. Die lokale Datei bleibt auf 1 KiB begrenzt. Die genaue Benennung der Einstellungsfelder wird bei Umsetzung mit dem Store abgeglichen.

Der eigene Zustand bezieht sich auf `OwnProfileSession.ActiveProfile`, unabhängig vom gerade im Hauptfenster ausgewählten Entwurf. Lesbare Profildaten beschränken sich auf General-Status und General-Verfügbarkeit. Namen, Content-IDs, Profil-IDs, Notizen, Kontakte, Peerangaben und Schlüssel werden nicht exportiert. Adult-Opt-in erteilt keine Exportfreigabe. IPC-Aufrufer sind keine authentisierten Identitäten; die Freigaben ersetzen keine Caller-Authentisierung.

## Channels und Rückgaben

| Channel | Signatur / Ergebnis | Bedeutung |
|---|---|---|
| `FRP1.ApiVersion` | `() → (uint 1, uint 0)` | Version des geplanten Vertrags. |
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

Höchstens ein wartender oder laufender Auftrag und ein letztes Ergebnis für 30 Sekunden bleiben gespeichert. Zwischen angenommenen Aktionen liegen mindestens 500 ms. Identische doppelte Anfragen erhalten dasselbe Ticket. Ein Ticket bestätigt keine Persistenz: Erst `Persisted` bestätigt erfolgreich gespeicherte Status-/Verfügbarkeitsänderungen; `Opened` bestätigt das Öffnen, `AlreadySatisfied` einen bereits erfüllten Zielzustand.

Aufträge laufen auf dem Framework-/Frame-Thread. Globale und funktionsbezogene Freigaben, aktuelle Policy, Actor-/Profilidentität, Kontextgeneration sowie Busy-/Draft-/CAS-Zustand werden vor Einreihen und unmittelbar vor Ausführung geprüft. Änderungen zwischen diesen Zeitpunkten verwerfen den Auftrag ohne Nebenwirkung. Status-/Verfügbarkeitsänderungen verwenden den vorhandenen eigenen Profilschreiber; keine direkten Store-Schreibwege oder Entwurfsüberschreibungen. Widerruf und Unload invalidieren ausstehende Arbeit; sensible Payloads werden auch bei Fehlern nicht protokolliert.

Die Abnahme muss Standard aus, ungültige Argumente, General-only-Lesen, aktive Profilzuordnung unabhängig von der UI-Auswahl, Busy/Dirty/CAS, Queue-Wechsel, Deduplizierung, Rategrenze, Ticketablauf, Widerruf und Unload nachweisen. Konkrete Tests, Registrierung und native Spielprobe sind für dieses Paket noch offen.
