# Machbarkeit und Werkzeugwahl

Stand: 2026-11-11

## Umgesetzt: Clients unter sich

FRP1 speichert Profile lokal und tauscht sie nach Zustimmung direkt zwischen aktiven Clients aus. Der Transport ist inzwischen in der Pluginoberfläche integriert; die separate Loopback-Probe bleibt ein zusätzliches Diagnose-/Prüfwerkzeug. Dauerhafte DPAPI-Identität und gespeicherte Kopplungen ersetzen die frühere rein flüchtige Prototypidentität.

Es gibt keine Registrierung, Cloudprofile, zentrale Profildatenbank, Telemetrie, Relays, STUN oder automatische externe Discovery. Der eingehende Verbindungsteil läuft im aktiven Client, kein separat betriebener Dienst. Quellenlinks dienen der Entwicklung. Der öffentlich erreichbare GitHub-Installerindex und das ZIP dienen ausschließlich Softwareinstallation/-updates, nicht Profiltransport oder Profilspeicherung; dafür ist kein GitHub-Konto nötig.

## Unvermeidbare Netzwerkgrenzen

Zwei Clients brauchen einen erreichbaren Netzpfad und bestätigte Gegenstelleninformationen. Einladungen transportieren IP-Literal, Port und öffentlichen Fingerprint. Ein optional fester Listenport und eine manuell angegebene numerische Einladungsadresse stellen Informationen bereit; sie öffnen keinen Router und überwinden keine Firewall.

FRP1 fragt keine öffentliche IP bei Dritten ab und ändert weder Router noch Firewall/DNS. NAT, CGNAT und restriktive Netze können daher Verbindungen verhindern. Zuverlässige weltweite Erkennung und Erreichbarkeit unter beliebigen Bedingungen werden nicht zugesichert.

Direkte Verbindung legt die IP-Adressen den beiden Peers gegenseitig offen; Verschlüsselung verbirgt diesen Netzmetadatenweg nicht. Das bestätigt auch die [iroh-Datenschutzbeschreibung](https://docs.iroh.computer/concepts/security-privacy).

Die Umgebung/Karte des Plugins verwendet lokal sichtbare bekannte Actors. Ein sichtbarer Spielcharakter ist dadurch noch kein entdeckter FRP1-Peer, und eine bestätigte Peer-ID ist kein serverseitiger Charakterbesitznachweis.

## Werkzeugentscheidung

| Weg | Einordnung |
|---|---|
| .NET TCP/`SslStream` | Gewählter direkter Plattformtransport mit gegenseitigem Zertifikatspinning; keine eigene Kryptografie und keine verpflichtenden Laufzeitdienste. |
| iroh | Etablierter QUIC-P2P-Ansatz; komfortable NAT-Koordination nutzt laut geprüfter Dokumentation Relays. Offizielle Sprachübersicht nennt kein C#-Binding. Löst die strikte Infrastrukturgrenze nicht automatisch. |
| dotnet-libp2p | C#-Ansatz mit mehreren Protokollen; die geprüfte Roadmap führte Hole Punching als offen und Circuit Relay als in Arbeit. Zusätzliche Komplexität löst keine prinzipielle Erreichbarkeitsgarantie. |
| Dalamud.NET.Sdk 15.0.0 | Offizielles lokal verfügbares Plugin-SDK, mit dem gegen vorhandene API-15-Bibliotheken gebaut wird. |
| Windows-DPAPI CurrentUser | Dauerhafter lokaler Schutz des privaten Peer-Schlüssels, an den Windows-Benutzerkontext gebunden. Kein Account-/Hostingdienst. |

Primärquellen: [Microsoft SslStream](https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream?view=net-10.0), [iroh NAT](https://docs.iroh.computer/concepts/nat-traversal), [iroh-Sprachen](https://docs.iroh.computer/languages/), [dotnet-libp2p](https://github.com/NethermindEth/dotnet-libp2p#roadmap), [Dalamud-Sample](https://github.com/goatcorp/SamplePlugin/blob/master/SamplePlugin/SamplePlugin.csproj). Der Werkzeugvergleich stammt aus der Projektprüfung vom 2026-11-09; diese externen Projekte können sich ändern.

## Nachgewiesen und noch nicht abgenommen

Die Release-Probe besteht mit acht Gruppen über zwei echte Loopback-TLS-Clients, einschließlich Freigabe, gegenseitigem Austausch, automatischer Aktualisierung, Persistenz sowie Ab-18-/Widerrufssperren. Der integrierte Modulstand besteht den abschließenden Release-Build ohne Warnungen/Fehler und 467 automatisierte Tests. Das unabhängige Review ist ohne verbleibende kritische oder wichtige Befunde freigegeben; [STATUS.md](../STATUS.md) hält die Ergebnisse fest.

Weiterhin nicht abgenommen: tatsächliche Bedienung/Renderdarstellung im Spiel, reale LAN-/IPv6-/Internetverbindungen, NAT/CGNAT und viele gleichzeitige Angreifer. Ein Test auf diesem Rechner beweist keine Verbindung zwischen zwei beliebigen Spielern.

TRP3-Funktionen werden eigenständig für FFXIV angepasst. WoW-Addonkanal/MSP, globale Weltkartensuche, importierte WoW-Assets, Spielsprachmanipulation, automatische NPC-Imitation und Musik werden nicht als vorhanden versprochen. Taschen-/Inventarsysteme und TRP3 Extended sind ausgeschlossen. Die vollständige Zwei-Spieler-Definition of Done bleibt bis zur tatsächlichen Abnahme offen.

