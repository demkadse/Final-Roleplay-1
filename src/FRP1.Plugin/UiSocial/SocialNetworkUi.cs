using System.Net;
using System.Numerics;
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
        if (!AccessReady) { Text(T("Verbindungen nach Hinweis- und Altersauswahl verfügbar.", "Connections available after guidance and age selection.")); return; }
        if (feedback is not null) Text(feedback);
        if (addressLoad is null && addresses.Count == 0)
            addressLoad = Task.Run(NetworkAdapters.EnumerateLocalAddresses, lifetime.Token);
        if (addressLoad is { IsCompleted: true } loaded)
        {
            addressLoad = null;
            try { addresses = loaded.GetAwaiter().GetResult(); }
            catch (Exception ex) when (Expected(ex)) { feedback = T("Netzwerkadapter konnten nicht gelesen werden.", "Network adapters could not be read."); }
        }
        Text(network.IsRunning
            ? T("Aktiv · ", "Active · ") + network.ConnectedPeerFingerprints.Count + T(" verbunden", " connected")
            : T("Aus · Profile bleiben lokal verfügbar", "Off · profiles remain available locally"));
        ImGui.BeginDisabled(network.IsRunning);
        DrawNetworkAdapter();
        ImGui.Checkbox(T("IP-Adresse für direkte Kontakte sichtbar machen", "Expose IP address to direct contacts"), ref ipConsent);
        ImGui.BeginDisabled(!ipConsent || addresses.Count == 0 || actions.Count != 0);
        if (ImGui.Button(T("Verbindungen aktivieren", "Enable connections")))
        {
            var address = addresses[Math.Clamp(selectedAddress, 0, addresses.Count - 1)].Address;
            Queue(() => network.StartAsync(new NetworkStartOptions { NetworkEnabled = true, DirectIpConsent = true, BindAddress = address, ListenPort = options.ListenPort }, lifetime.Token),
                () => { ownInvitation = ""; nextCacheRead = 0; advertisedPort = network.CreateInvitation().Port; });
        }
        ImGui.EndDisabled(); ImGui.EndDisabled();
        if ((network.IsRunning || actions.Count != 0) && ImGui.Button(T("Verbindungen stoppen", "Stop connections")))
            Queue(() => network.StopAsync(lifetime.Token), () => { publication = null; publishedHash = null; ownInvitation = ""; });
        if (network.IsRunning)
        {
            DrawProfileSharing();
            if (ImGui.Button(T("Eigene Einladung kopieren", "Copy own invitation")))
            {
                try
                {
                    var invitation = network.CreateInvitation();
                    if (manualInvitationAddress) invitation = SocialUiRules.CreateAdvertisedInvitation(invitation, advertisedAddress, advertisedPort);
                    ownInvitation = invitation.Encode(); ImGui.SetClipboardText(ownInvitation);
                }
                catch (Exception ex) when (Expected(ex)) { feedback = T("Einladung nicht verfügbar. Adresse und Port unter Erweitert prüfen.", "Invitation unavailable. Check address and port under Advanced."); }
            }
            if (ownInvitation.Length != 0) Text(T("Einladung kopiert · nur vertrauenswürdig weitergeben", "Invitation copied · share through a trusted channel"));
        }
        if (ImGui.CollapsingHeader(T("Kontakt koppeln", "Pair a contact"))) DrawPairing();
        ImGui.Separator();
        if (network.Peers.Count == 0) Text(T("Noch keine gekoppelten Kontakte", "No paired contacts yet"));
        foreach (var peer in network.Peers) DrawNetworkPeer(peer);
        if (ImGui.CollapsingHeader(T("Erweitert##network", "Advanced##network")))
        {
            DrawNetworkAdvanced();
            DrawCacheManagement();
        }
        if (ImGui.CollapsingHeader(T("Mouseover und Zielkarte", "Mouseover and target card"))) DrawSocialOptions();
    }

    private void DrawNetworkAdapter()
    {
        var addressLabel = addresses.Count == 0 ? T("Wird geladen …", "Loading …")
            : NetworkAddressLabel(addresses[Math.Clamp(selectedAddress, 0, addresses.Count - 1)]);
        if (!ImGui.BeginCombo(T("Netzwerk", "Network"), addressLabel)) return;
        try
        {
            for (var index = 0; index < addresses.Count; index++)
            {
                ImGui.PushID(index);
                var item = addresses[index];
                if (ImGui.Selectable(NetworkAddressLabel(item), selectedAddress == index))
                    selectedAddress = index;
                ImGui.PopID();
            }
        }
        finally { ImGui.EndCombo(); }
    }

    private static string NetworkAddressLabel(LocalNetworkAddress address) => IPAddress.IsLoopback(address.Address)
        ? T("Nur dieser Rechner", "This computer only") + (address.Address.Equals(IPAddress.IPv6Loopback) ? " (IPv6)" : "")
        : address.AdapterName;

    private void DrawProfileSharing()
    {
        var active = own.ActiveProfile;
        if (active is null) { Text(T("Eigenes Profil zuerst aktivieren.", "Activate your own profile first.")); return; }
        var sharing = publication == active.Id;
        Text((sharing ? T("Zum Teilen gewählt: ", "Selected for sharing: ") : T("Nur lokal: ", "Local only: ")) + active.DisplayName);
        if (!sharing)
        {
            var additional = FRP1.Plugin.UiProfiles.ExistingAdditionalContent.Read(active).Count;
            if (additional != 0) Text(additional + T(" Zusatzinhalte werden mitgeteilt. Vorher im Profil prüfen.", " additional entries will be shared. Review the profile first."));
            if (ImGui.Button(T("Aktives Profil zum Teilen wählen", "Select active profile for sharing")))
            {
                try { network.SetPublishedProfile(active); publication = active.Id; publishedHash = ProfileJson.Hash(active); }
                catch (Exception ex) when (Expected(ex)) { feedback = T("Profil konnte nicht zum Teilen gewählt werden.", "Profile could not be selected for sharing."); }
            }
        }
        if (publication.HasValue && ImGui.Button(T("Teilen pausieren", "Pause sharing")))
        { network.SetPublishedProfile(null); publication = null; publishedHash = null; }
    }

    private void DrawPairing()
    {
        Text(T("Einladungen beidseitig austauschen. Koppeln gibt kein Profil frei.", "Exchange invitations on both sides. Pairing does not share a profile."));
        ImGui.InputText(T("Einladung", "Invitation"), ref invitationInput, 2048);
        ImGui.InputText(T("Kontaktname (optional)", "Contact name (optional)"), ref peerName, 640);
        ImGui.BeginDisabled(actions.Count != 0 || !network.IsInitialized);
        if (ImGui.Button(T("Koppeln ohne Profilfreigabe", "Pair without profile sharing")))
        {
            try
            {
                var invitation = PeerInvitation.Parse(invitationInput);
                if (register.IsPeerBlocked(invitation.Fingerprint)) feedback = T("Kontakt gesperrt. Sperre zuerst ausdrücklich aufheben.", "Contact blocked. Explicitly remove the block first.");
                else Queue(() => network.PairAsync(invitation, peerName, false, lifetime.Token), () => invitationInput = "");
            }
            catch (Exception ex) when (Expected(ex)) { feedback = T("Ungültige Einladung. Version, Adresse und Fingerprint prüfen.", "Invalid invitation. Check version, address and fingerprint."); }
        }
        ImGui.EndDisabled();
    }

    private void DrawNetworkPeer(PeerTrust peer)
    {
        var pin = peer.Invitation.Fingerprint;
        var connected = network.ConnectedPeerFingerprints.Contains(pin, StringComparer.OrdinalIgnoreCase);
        var blocked = peer.Blocked || register.IsPeerBlocked(pin);
        var state = blocked ? T("gesperrt", "blocked") : connected ? T("verbunden", "connected") : T("getrennt", "disconnected");
        var sharing = peer.ShareProfile ? T("freigegeben", "sharing allowed") : T("keine Freigabe", "sharing off");
        ImGui.PushID(pin);
        try
        {
            var before = ImGui.GetCursorPos();
            var expanded = ImGui.CollapsingHeader("##peer-header");
            var after = ImGui.GetCursorPos();
            ImGui.SetCursorPos(before + new Vector2(ImGui.GetTreeNodeToLabelSpacing(), ImGui.GetStyle().FramePadding.Y));
            ImGui.TextUnformatted((peer.DisplayName.Length == 0 ? T("Kontakt", "Contact") : peer.DisplayName) + " · " + state + (blocked ? "" : " · " + sharing));
            ImGui.SetCursorPos(after);
            if (!expanded) return;
            ImGui.BeginDisabled(blocked || !network.IsRunning || actions.Count != 0);
            if (ImGui.Button(T("Verbinden", "Connect"))) Queue(() => network.ConnectAsync(pin, lifetime.Token));
            var shared = peer.ShareProfile;
            if (ImGui.Checkbox(T("Profil für diesen Kontakt freigeben", "Share profile with this contact"), ref shared))
                Queue(() => network.SetSharingAsync(pin, shared, lifetime.Token));
            ImGui.EndDisabled();
            if (ImGui.Button(T("Trennen", "Disconnect"))) Queue(() => network.DisconnectAsync(pin, lifetime.Token));
            ImGui.SameLine();
            ImGui.BeginDisabled(register.IsBusy || !register.IsLoaded || actions.Count != 0);
            if (ImGui.Button(blocked ? T("Sperre aufheben", "Unblock") : T("Kontakt sperren", "Block contact")))
                Queue(() => register.SetPeerBlockedAsync(pin, !blocked), () => pendingBlocks[pin] = !blocked);
            ImGui.EndDisabled();
            if (ImGui.TreeNode(T("Erweitert##peer", "Advanced##peer")))
            {
                Text(T("Öffentlicher Fingerprint: ", "Public fingerprint: ") + pin);
                Text(T("Adresse: ", "Address: ") + peer.Invitation.Address + ":" + peer.Invitation.Port);
                Text(T("Kopplung bestätigt diesen Schlüssel, keine Spielidentität.", "Pairing trusts this key, not an in-game identity."));
                ImGui.BeginDisabled(actions.Count != 0);
                if (ImGui.Button(T("Kopplung entfernen", "Remove pairing"))) Queue(() => network.UnpairAsync(pin, lifetime.Token), () => nextCacheRead = 0);
                ImGui.EndDisabled(); ImGui.TreePop();
            }
        }
        finally { ImGui.PopID(); }
    }

    private void DrawNetworkAdvanced()
    {
        Text(T("Direkt zwischen Clients, ohne Vermittlungsdienst. Router oder NAT können Verbindungen verhindern.", "Directly between clients, without an intermediary. Routers or NAT may prevent connections."));
        if (network.ErrorCode is { } error) Text(T("Verbindungsdiagnose: ", "Connection diagnostics: ") + error);
        if (addresses.Count != 0) Text(T("Lokale Adresse: ", "Local address: ") + addresses[Math.Clamp(selectedAddress, 0, addresses.Count - 1)].Address);
        ImGui.BeginDisabled(network.IsRunning);
        var listenPort = options.ListenPort;
        if (ImGui.InputInt(T("Listenport (0 = automatisch)", "Listening port (0 = automatic)"), ref listenPort))
        {
            if (listenPort is >= 0 and <= 65535) SetOptions(options with { ListenPort = listenPort });
            else feedback = T("Port muss zwischen 0 und 65535 liegen.", "Port must be between 0 and 65535.");
        }
        ImGui.EndDisabled();
        ImGui.Checkbox(T("Externe Einladungsadresse manuell", "Manual external invitation address"), ref manualInvitationAddress);
        if (manualInvitationAddress)
        {
            ImGui.InputText(T("Numerische IP-Adresse", "Numeric IP address"), ref advertisedAddress, 128);
            ImGui.InputInt(T("Externer Port", "External port"), ref advertisedPort);
            Text(T("Nur für die Einladung. Adress- und Portweiterleitung müssen bereits funktionieren; keine Router- oder Firewalländerung durch FRP1.", "Used only for the invitation. Address and port forwarding must already work; FRP1 does not change routers or firewalls."));
        }
    }

    private void DrawSocialOptions()
    {
        var tooltip = options.MouseoverTooltip;
        if (ImGui.Checkbox(T("Mouseover-RP-Tooltip anzeigen", "Show mouseover RP tooltip"), ref tooltip)) SetOptions(options with { MouseoverTooltip = tooltip });
        if (ImGui.BeginCombo(T("Mouseover-Umfang", "Mouseover detail"), options.MouseoverDetail == MouseoverDetail.Compact ? T("Kompakt", "Compact") : T("Ausführlich", "Detailed")))
        {
            if (ImGui.Selectable(T("Kompakt", "Compact"), options.MouseoverDetail == MouseoverDetail.Compact)) SetOptions(options with { MouseoverDetail = MouseoverDetail.Compact });
            if (ImGui.Selectable(T("Ausführlich", "Detailed"), options.MouseoverDetail == MouseoverDetail.Detailed)) SetOptions(options with { MouseoverDetail = MouseoverDetail.Detailed });
            ImGui.EndCombo();
        }
        var target = options.TargetVisible;
        if (ImGui.Checkbox(T("RP-Zielkarte anzeigen", "Show RP target card"), ref target)) SetOptions(options with { TargetVisible = target });
        var locked = options.TargetLocked;
        if (ImGui.Checkbox(T("Position der Zielkarte sperren", "Lock target-card position"), ref locked)) SetOptions(options with { TargetLocked = locked });
        if (ImGui.Button(T("Zielkartenposition zurücksetzen", "Reset target-card position")))
        { SetOptions(options with { TargetX = 32, TargetY = 180, TargetVisible = true }); targetWindow.ResetPosition(); }

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
