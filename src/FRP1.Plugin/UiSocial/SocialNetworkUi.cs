using System.Net;
using Dalamud.Bindings.ImGui;
using FRP1.Core.Networking;
using FRP1.Core.Profiles;
using FRP1.Plugin.Networking;

namespace FRP1.Plugin.UiSocial;

public sealed partial class SocialUiModule
{
    private IReadOnlyList<LocalNetworkAddress> addresses = [];
    private Task<IReadOnlyList<LocalNetworkAddress>>? addressLoad;
    private int selectedAddress;
    private bool ipConsent;
    private string invitationInput = "";
    private string peerName = "";
    private string ownInvitation = "";
    private bool manualInvitationAddress;
    private string advertisedAddress = "";
    private int advertisedPort;

    public void DrawNetworkSettings()
    {
        if (!AccessReady) { Text(T("Direktaustausch benötigt bestätigte Hinweise, Altersauswahl und einen angemeldeten Spielcharakter.", "Direct exchange requires acknowledged guidance, an age selection and a logged-in character.")); return; }
        if (feedback is not null) Text(feedback);
        Text(T("Keine Registrierung oder Vermittlungsdienste. Direkte Verbindungen machen deine IP-Adresse für den Kontakt sichtbar. Router und Firewalls können Verbindungen verhindern.", "No registration or intermediary services. Direct connections reveal your IP address to the peer. Routers and firewalls may prevent connections."));
        if (addressLoad is null && addresses.Count == 0)
            addressLoad = Task.Run(NetworkAdapters.EnumerateLocalAddresses, lifetime.Token);
        if (addressLoad is { IsCompleted: true } loaded)
        {
            addressLoad = null;
            try { addresses = loaded.GetAwaiter().GetResult(); }
            catch (Exception ex) when (Expected(ex)) { feedback = T("Lokale Netzwerkadapter konnten nicht gelesen werden.", "Local network adapters could not be read."); }
        }
        ImGui.BeginDisabled(network.IsRunning);
        ImGui.Checkbox(T("Direkte IP-Sichtbarkeit für diese Sitzung erlauben", "Allow direct IP visibility for this session"), ref ipConsent);
        var addressLabel = addresses.Count == 0 ? T("Wird geladen …", "Loading …") : addresses[Math.Clamp(selectedAddress, 0, addresses.Count - 1)].AdapterName;
        if (ImGui.BeginCombo(T("Lokaler Netzwerkadapter", "Local network adapter"), addressLabel))
        {
            for (var index = 0; index < addresses.Count; index++)
            {
                ImGui.PushID(index);
                var item = addresses[index];
                if (ImGui.Selectable(item.AdapterName + (IPAddress.IsLoopback(item.Address) ? T(" · nur dieser Rechner", " · this computer only") : T(" · Direktverbindung", " · direct connection")), selectedAddress == index))
                    selectedAddress = index;
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }
        var listenPort = options.ListenPort;
        if (ImGui.InputInt(T("Lokaler Listenport (0 = automatisch)", "Local listening port (0 = automatic)"), ref listenPort))
        {
            if (listenPort is >= 0 and <= 65535) SetOptions(options with { ListenPort = listenPort });
            else feedback = T("Der lokale Listenport muss zwischen 0 und 65535 liegen.", "The local listening port must be between 0 and 65535.");
        }
        ImGui.BeginDisabled(!ipConsent || addresses.Count == 0 || actions.Count != 0);
        if (ImGui.Button(T("Direktverbindung ausdrücklich aktivieren", "Explicitly enable direct networking")))
        {
            var address = addresses[Math.Clamp(selectedAddress, 0, addresses.Count - 1)].Address;
            Queue(() => network.StartAsync(new NetworkStartOptions { NetworkEnabled = true, DirectIpConsent = true, BindAddress = address, ListenPort = options.ListenPort }, lifetime.Token),
                () => { ownInvitation = ""; nextCacheRead = 0; advertisedPort = network.CreateInvitation().Port; });
        }
        ImGui.EndDisabled();
        ImGui.EndDisabled();
        if ((network.IsRunning || actions.Count != 0) && ImGui.Button(T("Direktverbindungen stoppen", "Stop direct networking")))
            Queue(() => network.StopAsync(lifetime.Token), () => { publication = null; publishedHash = null; ownInvitation = ""; });
        if (network.IsRunning)
        {
            Text(T("Direktverbindung aktiv. Das veröffentlicht noch kein Profil.", "Direct networking is active. This does not publish a profile."));
            ImGui.Checkbox(T("Abweichende externe Einladungsadresse manuell angeben", "Manually specify a different external invitation address"), ref manualInvitationAddress);
            if (manualInvitationAddress)
            {
                ImGui.InputText(T("Numerische externe IP-Adresse", "Numeric external IP address"), ref advertisedAddress, 128);
                ImGui.InputInt(T("Externer Einladungsport", "External invitation port"), ref advertisedPort);
                Text(T("Die Adresse wird nur in die Einladung geschrieben. Keine Adressermittlung, DNS-, STUN-, Router- oder Firewalländerung. " +
                    "Adress- und Portweiterleitung müssen bereits funktionieren; NAT/CGNAT kann den Zugang verhindern.",
                    "The address is only written into the invitation. No address lookup, DNS, STUN, router or firewall changes. " +
                    "Address and port forwarding must already work; NAT/CGNAT may prevent access."));
            }
            if (ImGui.Button(T("Eigene Einladung kopieren", "Copy own invitation")))
            {
                try
                {
                    var invitation = network.CreateInvitation();
                    if (manualInvitationAddress) invitation = SocialUiRules.CreateAdvertisedInvitation(invitation, advertisedAddress, advertisedPort);
                    ownInvitation = invitation.Encode(); ImGui.SetClipboardText(ownInvitation);
                }
                catch (Exception ex) when (Expected(ex)) { feedback = T("Einladung momentan nicht verfügbar.", "Invitation unavailable. Check the numeric address and port if you use a manual endpoint."); }
            }
            if (ownInvitation.Length != 0) Text(T("Die Einladung enthält Verbindungsadresse und öffentlichen Fingerprint. Nur vertrauenswürdig austauschen.", "The invitation contains the connection address and public fingerprint. Exchange it through a trusted channel."));
            var active = own.ActiveProfile;
            if (active is not null)
            {
                Text(publication == active.Id ? T("Dieses aktive Profil ist ausdrücklich zum Teilen ausgewählt.", "This active profile is explicitly selected for sharing.") : T("Das aktive Profil ist bisher nur lokal ausgewählt.", "The active profile is currently selected only locally."));
                if (publication != active.Id && ImGui.Button(T("Dieses aktive Profil zum Teilen verwenden", "Use this active profile for sharing")))
                {
                    try { network.SetPublishedProfile(active); publication = active.Id; publishedHash = ProfileJson.Hash(active); }
                    catch (Exception ex) when (Expected(ex)) { feedback = T("Das Profil konnte nicht freigegeben werden.", "The profile could not be published."); }
                }
                if (publication.HasValue && ImGui.Button(T("Profilveröffentlichung zurücknehmen und pausieren", "Withdraw profile publication and pause")))
                { network.SetPublishedProfile(null); publication = null; publishedHash = null; }
            }
            else Text(T("Bitte zuerst in der eigenen RP-Leiste ein zulässiges aktives Profil auswählen.", "First select an allowed active profile in your own RP bar."));
        }
        else Text(T("Direktverbindung ausgeschaltet. Profile und Notizen bleiben lokal verfügbar.", "Direct networking is off. Profiles and notes remain available locally."));
        ImGui.Separator();
        ImGui.InputText(T("Einladung des Kontakts", "Peer invitation"), ref invitationInput, 2048);
        ImGui.InputText(T("Lokaler Kontaktname (optional)", "Local peer name (optional)"), ref peerName, 640);
        ImGui.BeginDisabled(actions.Count != 0 || !network.IsInitialized);
        if (ImGui.Button(T("Einladung koppeln – ohne Profilfreigabe", "Pair invitation without sharing a profile")))
        {
            try
            {
                var invitation = PeerInvitation.Parse(invitationInput);
                if (register.IsPeerBlocked(invitation.Fingerprint)) feedback = T("Dieser Kontakt ist lokal gesperrt. Sperre zuerst ausdrücklich aufheben.", "This peer is locally blocked. Explicitly remove the block first.");
                else Queue(() => network.PairAsync(invitation, peerName, false, lifetime.Token), () => invitationInput = "");
            }
            catch (Exception ex) when (Expected(ex)) { feedback = T("Ungültige Einladung. Version, IP-Adresse und Fingerprint prüfen.", "Invalid invitation. Check the version, IP address and fingerprint."); }
        }
        ImGui.EndDisabled();
        Text(T("Beide Seiten müssen die Einladungen bewusst austauschen und koppeln. Kopplung, Verbindung und Profilfreigabe sind getrennte Entscheidungen.", "Both sides must deliberately exchange invitations and pair. Pairing, connecting and sharing a profile are separate decisions."));
        foreach (var peer in network.Peers)
        {
            var pin = peer.Invitation.Fingerprint;
            ImGui.PushID(pin);
            ImGui.Separator();
            ImGui.TextUnformatted(peer.DisplayName.Length == 0 ? T("Gekoppelter Kontakt", "Paired peer") : peer.DisplayName);
            Text(T("Öffentlicher Fingerprint: ", "Public fingerprint: ") + pin);
            ImGui.TextUnformatted(peer.Blocked ? T("Gesperrt", "Blocked") : network.ConnectedPeerFingerprints.Contains(pin, StringComparer.OrdinalIgnoreCase)
                ? T("Direkt verbunden", "Directly connected") : T("Gekoppelt; keine aktive Direktverbindung", "Paired; no active direct connection"));
            ImGui.BeginDisabled(peer.Blocked || !network.IsRunning || actions.Count != 0);
            if (ImGui.Button(T("Direkt verbinden", "Connect directly"))) Queue(() => network.ConnectAsync(pin, lifetime.Token));
            var shared = peer.ShareProfile;
            if (ImGui.Checkbox(T("Profil für diesen Kontakt freigeben", "Share profile with this peer"), ref shared))
                Queue(() => network.SetSharingAsync(pin, shared, lifetime.Token));
            ImGui.EndDisabled();
            if (ImGui.Button(T("Verbindung trennen", "Disconnect"))) Queue(() => network.DisconnectAsync(pin, lifetime.Token));
            ImGui.BeginDisabled(register.IsBusy || !register.IsLoaded || actions.Count != 0);
            if (ImGui.Button(peer.Blocked || register.IsPeerBlocked(pin) ? T("Lokale Sperre aufheben", "Remove local block") : T("Kontakt sperren", "Block peer")))
            {
                var blocked = !(peer.Blocked || register.IsPeerBlocked(pin));
                Queue(() => register.SetPeerBlockedAsync(pin, blocked), () => pendingBlocks[pin] = blocked);
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(actions.Count != 0);
            if (ImGui.Button(T("Kopplung entfernen", "Remove pairing"))) Queue(() => network.UnpairAsync(pin, lifetime.Token), () => nextCacheRead = 0);
            ImGui.EndDisabled();
            ImGui.PopID();
        }
        DrawCacheManagement();
        DrawSocialOptions();
    }

    private void DrawSocialOptions()
    {
        ImGui.Separator();
        var tooltip = options.MouseoverTooltip;
        if (ImGui.Checkbox(T("Mouseover-RP-Tooltip anzeigen", "Show mouseover RP tooltip"), ref tooltip)) SetOptions(options with { MouseoverTooltip = tooltip });
        if (ImGui.BeginCombo(T("Mouseover-Umfang", "Mouseover detail"), options.MouseoverDetail == MouseoverDetail.Compact ? T("Kompakt", "Compact") : T("Ausführlich", "Detailed")))
        {
            if (ImGui.Selectable(T("Kompakt", "Compact"), options.MouseoverDetail == MouseoverDetail.Compact)) SetOptions(options with { MouseoverDetail = MouseoverDetail.Compact });
            if (ImGui.Selectable(T("Ausführlich", "Detailed"), options.MouseoverDetail == MouseoverDetail.Detailed)) SetOptions(options with { MouseoverDetail = MouseoverDetail.Detailed });
            ImGui.EndCombo();
        }
        var target = options.TargetVisible;
        if (ImGui.Checkbox(T("Frei verschiebbare fremde Zielkarte anzeigen", "Show movable foreign target card"), ref target)) SetOptions(options with { TargetVisible = target });
        var locked = options.TargetLocked;
        if (ImGui.Checkbox(T("Position der Zielkarte sperren", "Lock target-card position"), ref locked)) SetOptions(options with { TargetLocked = locked });
        if (ImGui.Button(T("Zielkartenposition zurücksetzen", "Reset target-card position")))
        { SetOptions(options with { TargetX = 32, TargetY = 180, TargetVisible = true }); targetWindow.ResetPosition(); }
        Text(T("RP-Inhaltstooltips sind unabhängig von den Tutorialhinweisen. Unbekannte Spieler lösen weder Verbindungen noch Freigaben aus.", "RP-content tooltips are independent of tutorial hints. Unknown players never trigger connections or sharing."));
        if (optionsSaveFailed)
        {
            Text(T("Darstellung nur für diese Sitzung geändert. Die letzte Auswahl ist noch nicht gespeichert.", "Presentation changed only for this session. The latest choice has not been saved."));
            if (ImGui.Button(T("Darstellung erneut speichern", "Retry saving presentation")))
            { optionsSaveFailed = false; optionsDirty = true; optionsSaveDue = 0; }
        }
        if (optionsLoadFailed)
        {
            Text(T("Darstellungseinstellungen konnten nicht geladen werden; sichere Vorgaben bleiben aktiv.", "Presentation settings could not load; safe defaults remain active."));
            if (optionsStore is not null && optionsLoad is null && !optionsDirty && optionsSave is null &&
                ImGui.Button(T("Darstellung erneut laden", "Retry loading presentation")))
            {
                optionsLoadFailed = false; optionsLoadVersion = optionsVersion;
                var token = lifetime.Token; optionsLoad = Task.Run(() => optionsStore.LoadAsync(token), token);
            }
        }
    }
}
