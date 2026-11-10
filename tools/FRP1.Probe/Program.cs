using FRP1.Core.Identity;
using FRP1.Core.Networking;
using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;

if (args.Length != 1 || args[0] != "--self-test")
{
    Console.WriteLine("FRP1 Direktverbindungsprobe: --self-test startet zwei lokale TLS-Clients ohne externe Dienste.");
    return 0;
}

var directory = Path.Combine(Path.GetTempPath(), "FRP1-Probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var token = timeout.Token;
    using var firstIdentity = PeerIdentity.CreateEphemeral();
    using var secondIdentity = PeerIdentity.CreateEphemeral();
    var firstStore = new ProfileStore(Path.Combine(directory, "first"));
    var secondStore = new ProfileStore(Path.Combine(directory, "second"));
    await using var first = new PeerNode(firstIdentity, firstStore, new PeerNodeOptions { NetworkEnabled = true });
    await using var second = new PeerNode(secondIdentity, secondStore, new PeerNodeOptions { NetworkEnabled = true });
    var firstProfile = new ProfileDocument
    {
        DisplayName = "Lokaler Testcharakter A",
        Background = "Nur synthetische Testdaten.",
        Status = RpStatus.Ic,
        Availability = RpAvailability.Open,
        Summary = "Ein synthetischer Reisender mit RP-Anknüpfungspunkten.",
        Pronouns = "er/ihm",
        OocInformation = "Nur Testdaten für die lokale Direktverbindungsprobe.",
        CurrentLocation = "Testtaverne",
        FreeCompany = "Testgesellschaft",
        Glances = [new GlanceEntry("Erster Eindruck", "Ein abgewetzter Mantel.", 42)],
        RpHooks = [new RpHook("Orientierung", "Fragt nach einem Wegweiser."), new RpHook("Landkarte", "Sucht eine verschollene Landkarte.")],
    };
    var secondProfile = new ProfileDocument { DisplayName = "Lokaler Testcharakter B" };
    await firstStore.SaveLocalAsync(firstProfile, token);
    await secondStore.SaveLocalAsync(secondProfile, token);
    first.SetLocalProfile(firstProfile);
    second.SetLocalProfile(secondProfile);
    await first.StartAsync(token);
    await second.StartAsync(token);
    var firstInvitation = PeerInvitation.Parse(first.CreateInvitation().Encode());
    var secondInvitation = PeerInvitation.Parse(second.CreateInvitation().Encode());
    first.Pair(secondInvitation);
    second.Pair(firstInvitation);
    await using (var session = await first.ConnectAsync(secondInvitation, token))
    {
        await Task.Delay(400, token);
        if (await secondStore.GetCachedAsync(firstIdentity.Fingerprint, firstProfile.Id, token) is not null ||
            await firstStore.GetCachedAsync(secondIdentity.Fingerprint, secondProfile.Id, token) is not null)
            throw new InvalidOperationException("Profile were shared without consent.");
        Console.WriteLine("PASS: Gekoppelte Clients veröffentlichen ohne gesonderte Freigabe keine Profile.");
        first.SetSharing(secondIdentity.Fingerprint, true);
        second.SetSharing(firstIdentity.Fingerprint, true);
        await WaitForAsync(firstStore, secondIdentity.Fingerprint, secondProfile, token);
        await WaitForAsync(secondStore, firstIdentity.Fingerprint, firstProfile, token);
        Console.WriteLine("PASS: Zwei echte TLS-Clients haben Profile gegenseitig verschlüsselt übertragen.");
        Console.WriteLine("PASS: RP-Status, Verfügbarkeit, Glances und RP-Anknüpfungspunkte bleiben beim Transfer unverändert.");
        firstProfile = firstProfile with
        {
            Revision = 2,
            Title = "Automatisch synchronisiert",
            Status = RpStatus.Afk,
            Availability = RpAvailability.Busy,
            Glances = [new GlanceEntry("Erster Eindruck", "Ein Mantel und eine neue Reisetasche.", 43)],
            RpHooks = [new RpHook("Reisebegleitung", "Sucht jetzt eine Reisebegleitung.")],
        };
        first.SetLocalProfile(firstProfile);
        await WaitForAsync(secondStore, firstIdentity.Fingerprint, firstProfile, token);
        Console.WriteLine("PASS: Eine Profiländerung wurde automatisch anhand Version und Hash synchronisiert.");
    }
    var offline = new ProfileStore(Path.Combine(directory, "second"));
    var cached = await offline.GetCachedAsync(firstIdentity.Fingerprint, firstProfile.Id, token);
    if (cached?.Title != firstProfile.Title) throw new InvalidOperationException("Offline cache failed.");
    if ((await firstStore.LoadLocalAsync(firstProfile.Id, token))?.DisplayName != firstProfile.DisplayName)
        throw new InvalidOperationException("Local persistence failed.");
    Console.WriteLine("PASS: Lokale Profile und empfangener Offline-Cache sind erneut lesbar.");

    var matureProfile = firstProfile with { Revision = 3, ContentRating = ContentRating.Mature18, Title = "Synthetisches Ab-18-Testprofil" };
    try
    {
        ProfileJson.Import(ProfileJson.Export(matureProfile), firstStore.Policy);
        throw new InvalidOperationException("Mature import was accepted without an adult opt-in.");
    }
    catch (InvalidDataException) { }
    firstStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Adult, true));
    secondStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18, true));
    if (secondStore.Policy.Snapshot.MatureContentOptIn) throw new InvalidOperationException("Underage opt-in was not normalized.");
    first.SetLocalProfile(matureProfile);
    await firstStore.SaveLocalAsync(matureProfile, token);
    await using (var session = await first.ConnectAsync(secondInvitation, token))
    {
        await Task.Delay(600, token);
        if ((await secondStore.GetCachedAsync(firstIdentity.Fingerprint, firstProfile.Id, token))?.Revision != 2)
            throw new InvalidOperationException("Mature profile reached an underage recipient.");
        Console.WriteLine("PASS: Ab-18-Import ohne Zustimmung und Übertragung an Minderjährige sind gesperrt.");
        secondStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Adult));
        await Task.Delay(400, token);
        if ((await secondStore.GetCachedAsync(firstIdentity.Fingerprint, firstProfile.Id, token))?.Revision != 2)
            throw new InvalidOperationException("Mature profile reached an adult without opt-in.");
        secondStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Adult, true));
        await WaitForAsync(secondStore, firstIdentity.Fingerprint, matureProfile, token);
        var secondMature = secondProfile with { Revision = 2, ContentRating = ContentRating.Mature18 };
        second.SetLocalProfile(secondMature);
        await secondStore.SaveLocalAsync(secondMature, token);
        await WaitForAsync(firstStore, secondIdentity.Fingerprint, secondMature, token);
        Console.WriteLine("PASS: Ab-18-Profile werden nach beidseitiger Erwachsenen-Selbstauskunft und Zustimmung ausgetauscht.");
        secondStore.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18));
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(2), token);
        try
        {
            await secondStore.GetCachedAsync(firstIdentity.Fingerprint, matureProfile.Id, token);
            throw new InvalidOperationException("Mature cache remained available after withdrawal.");
        }
        catch (InvalidDataException) { }
        if ((await secondStore.ListLocalAsync(token)).Any(p => p.ContentRating == ContentRating.Mature18))
            throw new InvalidOperationException("Mature local profile remained listed after withdrawal.");
        Console.WriteLine("PASS: Widerruf beendet die Verbindung und sperrt bereits gespeicherte Ab-18-Profile.");
    }
    Console.WriteLine("GRENZE: Nur Loopback geprüft; keine Aussage über LAN-Firewalls, NAT oder weltweite Erreichbarkeit.");
    return 0;
}
catch (Exception ex) when (ex is not OutOfMemoryException)
{
    Console.Error.WriteLine("FAIL: " + ex.GetType().Name + " (keine Verbindungsdaten protokolliert).");
    return 1;
}
finally
{
    // This uniquely created test directory contains synthetic public profiles only.
    var resolved = Path.GetFullPath(directory);
    var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
    if (resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("FRP1-Probe-", StringComparison.Ordinal))
        Directory.Delete(resolved, recursive: true);
}

static async Task WaitForAsync(ProfileStore store, string fingerprint, ProfileDocument expected, CancellationToken token)
{
    while (true)
    {
        token.ThrowIfCancellationRequested();
        var cached = await store.GetCachedAsync(fingerprint, expected.Id, token);
        if (cached is not null && ProfileJson.Hash(cached) == ProfileJson.Hash(expected)) return;
        await Task.Delay(50, token);
    }
}
