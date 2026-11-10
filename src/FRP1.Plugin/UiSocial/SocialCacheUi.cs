using Dalamud.Bindings.ImGui;
using FRP1.Core.Persistence;

namespace FRP1.Plugin.UiSocial;

public sealed partial class SocialUiModule
{
    private bool cacheManagementRequested;
    private IReadOnlyList<CachedPeerSummary> cachePeers = [];
    private Task<IReadOnlyList<CachedPeerSummary>>? cachePeersLoad;
    private long cachePeersGeneration;
    private long cachePeersReadEpoch;
    private long cachePeersReadDue;
    private string? cacheRemovalChoice;
    private bool cacheRemovalConfirmed;

    private void PollCacheManagement()
    {
        if (cachePeersLoad is { IsCompleted: true } completed)
        {
            cachePeersLoad = null;
            try
            {
                var loaded = completed.GetAwaiter().GetResult();
                if (cachePeersGeneration == generation && cachePeersReadEpoch == cacheEpoch) cachePeers = loaded;
            }
            catch (Exception ex) when (Expected(ex)) { feedback = T("Cacheübersicht konnte nicht gelesen werden.", "The cache overview could not be read."); }
        }
        if (!cacheManagementRequested || !AccessReady || cachePeersLoad is not null || Environment.TickCount64 < cachePeersReadDue) return;
        cachePeersGeneration = generation; cachePeersReadEpoch = cacheEpoch;
        var token = lifetime.Token;
        cachePeersLoad = Task.Run(() => profiles.ListCachedPeersAsync(token), token);
        cachePeersReadDue = Environment.TickCount64 + 3000;
    }

    private void DrawCacheManagement()
    {
        if (!ImGui.CollapsingHeader(T("Lokalen Fremdprofilcache verwalten", "Manage local foreign-profile cache"))) return;
        cacheManagementRequested = true;
        Text(T("Entfernt ausschließlich lokal zwischengespeicherte Profile einer Gegenstelle. Private Notizen, Beziehungen, Sperren und Kopplungen bleiben erhalten. " +
            "Die Verbindung wird vorher getrennt. Erneuter Empfang kann den Cache wieder füllen; die Gegenstelle wird dadurch nicht gesperrt.",
            "Removes only a peer's locally cached profiles. Private notes, relationships, blocks and pairings remain. " +
            "The connection is disconnected first. A later reception may refill the cache; the peer is not blocked."));
        if (cachePeers.Count == 0) Text(T("Keine Cachegegenstellen geladen.", "No cached peers loaded."));
        var index = 0;
        foreach (var peer in cachePeers)
        {
            ImGui.PushID(index++);
            Text(T("Öffentlicher Fingerprint: ", "Public fingerprint: ") + peer.Fingerprint);
            Text(T("Gespeicherte Profile: ", "Cached profiles: ") + peer.ProfileCount);
            ImGui.BeginDisabled(actions.Count != 0 || register.IsBusy || register.IsDirty || contactDirty || characterNoteDirty);
            if (ImGui.Button(T("Cacheentfernung vorbereiten", "Prepare cache removal")))
            { cacheRemovalChoice = peer.Fingerprint; cacheRemovalConfirmed = false; }
            ImGui.EndDisabled();
            ImGui.PopID();
        }
        if (cacheRemovalChoice is not { } pin) return;
        ImGui.Separator();
        Text(T("Ausgewählte Gegenstelle: ", "Selected peer: ") + pin);
        ImGui.Checkbox(T("Ich möchte deren lokale Profilkopien ausdrücklich entfernen.##confirmCacheRemoval",
            "I explicitly want to remove this peer's local profile copies.##confirmCacheRemoval"), ref cacheRemovalConfirmed);
        ImGui.BeginDisabled(!cacheRemovalConfirmed || actions.Count != 0 || register.IsBusy || register.IsDirty || contactDirty || characterNoteDirty);
        if (ImGui.Button(T("Verbindung trennen und Cache entfernen", "Disconnect and remove cache")))
        {
            CacheRemovalResult? result = null;
            if (Queue(async () =>
                {
                    await network.DisconnectAsync(pin, lifetime.Token).ConfigureAwait(false);
                    result = await profiles.RemoveCachedPeerAsync(pin, lifetime.Token).ConfigureAwait(false);
                }, () =>
                {
                    cacheEpoch++;
                    cachedEntries = cachedEntries.Where(entry => !string.Equals(entry.Fingerprint, pin, StringComparison.OrdinalIgnoreCase)).ToArray();
                    RefreshCatalog(); nextCacheRead = 0; cachePeersReadDue = 0;
                    cachePeers = cachePeers.Where(peer => !string.Equals(peer.Fingerprint, pin, StringComparison.OrdinalIgnoreCase)).ToArray();
                    feedback = T("Lokale Profilkopien entfernt: ", "Local profile copies removed: ") + (result?.RemovedProfiles ?? 0) +
                        T(". Erneuter Empfang kann den Cache wieder füllen.", ". A later reception may refill the cache.");
                })) { cacheRemovalChoice = null; cacheRemovalConfirmed = false; }
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button(T("Abbrechen##cacheRemoval", "Cancel##cacheRemoval"))) { cacheRemovalChoice = null; cacheRemovalConfirmed = false; }
    }
}
