using System.Net;
using FRP1.Core.Identity;
using FRP1.Core.Networking;
using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Networking;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class NetworkSessionTests
{
    [Fact]
    public async Task SessionStartsOfflineAndRequiresBothNetworkAndIpConsentPlusAllowedContext()
    {
        await using var harness = new NetworkHarness();
        await harness.Session.InitializeAsync();
        Assert.True(harness.Session.IsInitialized);
        Assert.False(harness.Session.IsRunning);
        Assert.Throws<InvalidOperationException>(() => harness.Session.CreateInvitation());
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.StartAsync(new()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.StartAsync(new() { NetworkEnabled = true }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.StartAsync(AllowedOptions()));
        harness.Session.ObserveContext(1, true);
        harness.Policy.SetAudience(new AudiencePreferences());
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.StartAsync(AllowedOptions()));
        Assert.False(harness.Session.IsRunning);
        harness.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18));
        await harness.Session.StartAsync(AllowedOptions());
        Assert.True(harness.Session.IsRunning);
        Assert.True(IPAddress.IsLoopback(IPAddress.Parse(harness.Session.CreateInvitation().Address)));
        await harness.Session.StopAsync();
        Assert.False(harness.Session.IsRunning);
    }

    [Fact]
    public async Task TrustWithoutItsOriginalIdentityCannotCreateSilentReplacement()
    {
        await using var harness = new NetworkHarness();
        var trust = new PeerTrustStore(harness.Directory.Path);
        trust.BindLocalIdentity(new string('A', 64));
        await trust.SaveAsync([new PeerTrust(new(1, "127.0.0.1", 1234, new string('B', 64)))]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.InitializeAsync());
        Assert.False(harness.Session.IsInitialized);
        Assert.False(File.Exists(Path.Combine(harness.Directory.Path, "peer-identity.dpapi")));
    }

    [Fact]
    public async Task TrustBoundToAnotherIdentityRejectsInitializationWithoutReplacingExistingKey()
    {
        await using var harness = new NetworkHarness();
        using var original = await new PeerIdentityStore(harness.Directory.Path).LoadOrCreateAsync();
        var trust = new PeerTrustStore(harness.Directory.Path);
        trust.BindLocalIdentity(new string('B', 64));
        await trust.SaveAsync([]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.InitializeAsync());
        Assert.False(harness.Session.IsInitialized);
        using var unchanged = await new PeerIdentityStore(harness.Directory.Path).LoadExistingAsync();
        Assert.Equal(original.Fingerprint, unchanged.Fingerprint);
    }

    [Theory]
    [InlineData("share")]
    [InlineData("pair-share")]
    [InlineData("unblock")]
    public async Task NewerPeerDenialSurvivesOlderEnableCompletionEvenWhenDenialCannotBeSaved(string olderAction)
    {
        using var directory = new TestDirectory();
        var profileStore = new ProfileStore(directory.Path, ProfileModuleTests.Adult());
        var trustStore = new PeerTrustStore(directory.Path);
        var oldWriteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = false;
        var controlledWrites = 0;
        async Task SaveTrustAsync(IReadOnlyList<PeerTrust> snapshot, CancellationToken token)
        {
            if (armed)
            {
                var index = Interlocked.Increment(ref controlledWrites);
                if (index == 1)
                {
                    oldWriteStarted.SetResult();
                    await releaseOldWrite.Task.WaitAsync(token);
                }
                else if (index == 2) throw new IOException("Synthetic failure persisting the newer denial");
            }
            await trustStore.SaveAsync(snapshot, token);
        }
        await using var first = new NetworkSession(profileStore, new PeerIdentityStore(directory.Path), trustStore, SaveTrustAsync);
        await using var second = new NetworkHarness();
        first.ObserveContext(1, true);
        second.Session.ObserveContext(2, true);
        await first.StartAsync(AllowedOptions());
        await second.Session.StartAsync(AllowedOptions());
        var firstInvitation = first.CreateInvitation();
        var secondInvitation = second.Session.CreateInvitation();
        await first.PairAsync(secondInvitation);
        await second.Session.PairAsync(firstInvitation);
        if (olderAction == "unblock") await first.SetBlockedAsync(secondInvitation.Fingerprint, true);
        var profile = ProfileTests.Valid();
        first.SetPublishedProfile(profile);
        armed = true;
        var oldOperation = olderAction switch
        {
            "share" => first.SetSharingAsync(secondInvitation.Fingerprint, true),
            "pair-share" => first.PairAsync(secondInvitation, shareProfile: true),
            _ => first.SetBlockedAsync(secondInvitation.Fingerprint, false),
        };
        await oldWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var newDenial = olderAction == "unblock"
            ? first.SetBlockedAsync(secondInvitation.Fingerprint, true)
            : first.SetSharingAsync(secondInvitation.Fingerprint, false);
        releaseOldWrite.SetResult();
        var oldFailure = await Record.ExceptionAsync(() => oldOperation);
        Assert.True(oldFailure is null or InvalidOperationException);
        await Assert.ThrowsAsync<InvalidOperationException>(() => newDenial);
        Assert.Equal(2, controlledWrites);
        var peer = Assert.Single(first.Peers);
        Assert.False(peer.ShareProfile);
        if (olderAction == "unblock")
        {
            Assert.True(peer.Blocked);
            await Assert.ThrowsAsync<InvalidOperationException>(() => first.ConnectAsync(secondInvitation.Fingerprint));
        }
        else
        {
            await first.ConnectAsync(secondInvitation.Fingerprint);
            await Task.Delay(450);
            Assert.Empty(await second.Store.ListCachedAsync());
        }
    }

    [Fact]
    public async Task PairedSessionsDoNotShareUntilExplicitlyAllowedThenSyncAndNotify()
    {
        await using var first = new NetworkHarness();
        await using var second = new NetworkHarness();
        var firstProfile = ProfileModuleTests.Expanded();
        var secondProfile = ProfileTests.Valid();
        var pins = await StartAndPairAsync(first, second);
        first.Session.SetPublishedProfile(firstProfile);
        second.Session.SetPublishedProfile(secondProfile);
        await first.Session.ConnectAsync(pins.Second.Fingerprint);
        await Task.Delay(550);
        Assert.Empty(await first.Store.ListCachedAsync());
        Assert.Empty(await second.Store.ListCachedAsync());
        await first.Session.SetSharingAsync(pins.Second.Fingerprint, true);
        await second.Session.SetSharingAsync(pins.First.Fingerprint, true);
        await WaitCachedAsync(second.Store, pins.First.Fingerprint, firstProfile.Id, 1);
        await WaitCachedAsync(first.Store, pins.Second.Fingerprint, secondProfile.Id, 1);
        Assert.True(second.Session.TryDequeueReceived(out var notification));
        Assert.NotNull(notification);
        Assert.Equal(firstProfile.Id, notification.ProfileId);
        Assert.Equal(pins.First.Fingerprint, notification.Fingerprint);
        var update = firstProfile with { Revision = 2, Summary = "Automatic module update" };
        first.Session.SetPublishedProfile(update);
        await WaitCachedAsync(second.Store, pins.First.Fingerprint, firstProfile.Id, 2);
        await first.Session.SetSharingAsync(pins.Second.Fingerprint, false);
        first.Session.SetPublishedProfile(update with { Revision = 3, Summary = "Private revision" });
        await Task.Delay(550);
        Assert.Equal(2, (await second.Store.GetCachedAsync(pins.First.Fingerprint, firstProfile.Id))!.Revision);
    }

    [Theory]
    [InlineData("character")]
    [InlineData("access")]
    [InlineData("age")]
    [InlineData("published-null")]
    public async Task ContextWithdrawalStopsConnectionAndRestartCannotRepublishOldProfile(string reason)
    {
        await using var first = new NetworkHarness();
        await using var second = new NetworkHarness();
        var profile = ProfileTests.Valid();
        var pins = await StartAndPairAsync(first, second, share: true);
        first.Session.SetPublishedProfile(profile);
        await first.Session.ConnectAsync(pins.Second.Fingerprint);
        await WaitCachedAsync(second.Store, pins.First.Fingerprint, profile.Id, 1);
        switch (reason)
        {
            case "character": first.Session.ObserveContext(2, true); break;
            case "access": first.Session.ObserveContext(1, false); break;
            case "age": first.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18)); break;
            case "published-null": first.Session.SetPublishedProfile(null); break;
        }
        Assert.False(first.Session.IsRunning);
        Assert.Throws<InvalidOperationException>(() => first.Session.SetPublishedProfile(profile with { Revision = 2 }));
        first.Session.ObserveContext(2, true);
        await first.Session.StartAsync(AllowedOptions());
        await first.Session.ConnectAsync(pins.Second.Fingerprint);
        await Task.Delay(450);
        Assert.Equal(1, (await second.Store.GetCachedAsync(pins.First.Fingerprint, profile.Id))!.Revision);
        first.Session.SetPublishedProfile(profile with { Revision = 2, Title = "New explicit publication" });
        await WaitCachedAsync(second.Store, pins.First.Fingerprint, profile.Id, 2);
    }

    [Fact]
    public async Task BlockAndUnpairImmediatelyDenyCommunicationEvenWhenStorageFails()
    {
        await using var first = new NetworkHarness();
        await using var second = new NetworkHarness();
        var profile = ProfileTests.Valid();
        var pins = await StartAndPairAsync(first, second, share: true);
        first.Session.SetPublishedProfile(profile);
        await first.Session.ConnectAsync(pins.Second.Fingerprint);
        await WaitCachedAsync(second.Store, pins.First.Fingerprint, profile.Id, 1);
        var trustPath = Path.Combine(first.Directory.Path, "peer-trust.json");
        using (var fileLock = new FileStream(trustPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => first.Session.SetBlockedAsync(pins.Second.Fingerprint, true));
            var peer = Assert.Single(first.Session.Peers);
            Assert.True(peer.Blocked);
            Assert.False(peer.ShareProfile);
            first.Session.SetPublishedProfile(profile with { Revision = 2 });
            await Task.Delay(450);
            Assert.Equal(1, (await second.Store.GetCachedAsync(pins.First.Fingerprint, profile.Id))!.Revision);
            await Assert.ThrowsAsync<InvalidOperationException>(() => first.Session.ConnectAsync(pins.Second.Fingerprint));
        }
        await first.Session.SetBlockedAsync(pins.Second.Fingerprint, true);
        await first.Session.UnpairAsync(pins.Second.Fingerprint);
        Assert.Empty(first.Session.Peers);
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.Session.ConnectAsync(pins.Second.Fingerprint));
    }

    [Fact]
    public async Task AuthenticatedCacheListingUsesLocalFileTimeAndFiltersMatureAfterWithdrawal()
    {
        await using var harness = new NetworkHarness();
        var general = ProfileTests.Valid();
        var mature = ProfileTests.Valid() with { ContentRating = ContentRating.Mature18 };
        var pin = new string('A', 64);
        await harness.Store.SaveCachedAsync(pin, general);
        await harness.Store.SaveCachedAsync(pin, mature);
        var localTime = DateTime.UtcNow.AddHours(-2);
        File.SetLastWriteTimeUtc(Path.Combine(harness.Directory.Path, "cache", pin, general.Id.ToString("N") + ".json"), localTime);
        var listing = await harness.Store.ListCachedAsync();
        Assert.Equal(2, listing.Count);
        Assert.Equal(localTime, listing.Single(entry => entry.Profile.Id == general.Id).StoredAtUtc.UtcDateTime);
        harness.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18));
        Assert.Equal(general.Id, Assert.Single(await harness.Store.ListCachedAsync()).Profile.Id);
    }

    [Fact]
    public void AdapterEnumerationUsesBoundedLocalAddressesWithoutLookupInputs()
    {
        var addresses = NetworkAdapters.EnumerateLocalAddresses();
        Assert.InRange(addresses.Count, 2, 64);
        Assert.Contains(addresses, item => item.Address.Equals(IPAddress.Loopback));
        Assert.Contains(addresses, item => item.Address.Equals(IPAddress.IPv6Loopback));
        Assert.DoesNotContain(addresses, item => item.Address.Equals(IPAddress.Any) || item.Address.Equals(IPAddress.IPv6Any) || item.Address.IsIPv6Multicast);
        Assert.Equal(addresses.Count, addresses.Select(item => item.Address).Distinct().Count());
    }

    private static async Task<(PeerInvitation First, PeerInvitation Second)> StartAndPairAsync(NetworkHarness first, NetworkHarness second, bool share = false)
    {
        first.Session.ObserveContext(1, true);
        second.Session.ObserveContext(2, true);
        await first.Session.StartAsync(AllowedOptions());
        await second.Session.StartAsync(AllowedOptions());
        var firstInvitation = first.Session.CreateInvitation();
        var secondInvitation = second.Session.CreateInvitation();
        await first.Session.PairAsync(secondInvitation, shareProfile: share);
        await second.Session.PairAsync(firstInvitation, shareProfile: share);
        return (firstInvitation, secondInvitation);
    }

    internal static async Task WaitCachedAsync(ProfileStore store, string pin, Guid id, long revision)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(7));
        while ((await store.GetCachedAsync(pin, id))?.Revision != revision) await Task.Delay(25, timeout.Token);
    }

    private static NetworkStartOptions AllowedOptions() => new() { NetworkEnabled = true, DirectIpConsent = true };
}

internal sealed class NetworkHarness : IAsyncDisposable
{
    public TestDirectory Directory { get; } = new();
    public AudiencePolicy Policy { get; } = ProfileModuleTests.Adult();
    public ProfileStore Store { get; }
    public NetworkSession Session { get; }
    public NetworkHarness()
    {
        Store = new ProfileStore(Directory.Path, Policy);
        Session = new NetworkSession(Store, new PeerIdentityStore(Directory.Path), new PeerTrustStore(Directory.Path));
    }
    public async ValueTask DisposeAsync()
    {
        await Session.DisposeAsync();
        Directory.Dispose();
    }
}
