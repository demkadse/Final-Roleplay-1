using System.Net;
using System.Net.Sockets;
using FRP1.Core.Identity;
using FRP1.Core.Networking;
using FRP1.Core.Persistence;
using FRP1.Plugin.Networking;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class NetworkIntentTests
{
    [Fact]
    public async Task AuthenticatedConnectionsAppearOnBothSidesAndDisconnectPreservesTrustAndCache()
    {
        await using var first = new NetworkHarness();
        await using var second = new NetworkHarness();
        first.Session.ObserveContext(1, true);
        second.Session.ObserveContext(2, true);
        await first.Session.StartAsync(Options());
        await second.Session.StartAsync(Options());
        var firstInvitation = first.Session.CreateInvitation();
        var secondInvitation = second.Session.CreateInvitation();
        await first.Session.PairAsync(secondInvitation, shareProfile: true);
        await second.Session.PairAsync(firstInvitation, shareProfile: true);
        Assert.Empty(first.Session.ConnectedPeerFingerprints);
        Assert.Empty(second.Session.ConnectedPeerFingerprints);
        using (var unauthenticated = new TcpClient())
        {
            await unauthenticated.ConnectAsync(IPAddress.Parse(secondInvitation.Address), secondInvitation.Port);
            await Task.Delay(100);
            Assert.Empty(second.Session.ConnectedPeerFingerprints);
        }
        var profile = ProfileTests.Valid();
        first.Session.SetPublishedProfile(profile);
        await first.Session.ConnectAsync(secondInvitation.Fingerprint);
        await UntilAsync(() => first.Session.ConnectedPeerFingerprints.Count == 1 && second.Session.ConnectedPeerFingerprints.Count == 1);
        Assert.Equal(secondInvitation.Fingerprint, Assert.Single(first.Session.ConnectedPeerFingerprints));
        Assert.Equal(firstInvitation.Fingerprint, Assert.Single(second.Session.ConnectedPeerFingerprints));
        await NetworkSessionTests.WaitCachedAsync(second.Store, firstInvitation.Fingerprint, profile.Id, 1);
        var firstTrust = first.Session.Peers.ToArray();
        var secondTrust = second.Session.Peers.ToArray();
        await second.Session.DisconnectAsync(firstInvitation.Fingerprint);
        await UntilAsync(() => first.Session.ConnectedPeerFingerprints.Count == 0 && second.Session.ConnectedPeerFingerprints.Count == 0);
        Assert.True(first.Session.IsRunning);
        Assert.True(second.Session.IsRunning);
        Assert.Equal(firstTrust, first.Session.Peers);
        Assert.Equal(secondTrust, second.Session.Peers);
        Assert.NotNull(await second.Store.GetCachedAsync(firstInvitation.Fingerprint, profile.Id));
        first.Session.SetPublishedProfile(profile with { Revision = 2, Title = "Retained while disconnected" });
        Assert.Equal(1, (await second.Store.GetCachedAsync(firstInvitation.Fingerprint, profile.Id))!.Revision);
        await first.Session.ConnectAsync(secondInvitation.Fingerprint);
        await NetworkSessionTests.WaitCachedAsync(second.Store, firstInvitation.Fingerprint, profile.Id, 2);
        await UntilAsync(() => second.Session.ConnectedPeerFingerprints.Count == 1);
    }

    [Fact]
    public async Task QueuedStartCannotResurrectListenerAfterNewerStop()
    {
        await using var harness = new QueueBarrierHarness();
        await harness.Session.InitializeAsync();
        harness.Session.ObserveContext(1, true);
        var blocker = await harness.HoldQueueAsync(SyntheticPeer());
        var oldStart = harness.Session.StartAsync(Options());
        var newStop = harness.Session.StopAsync();
        harness.Release();
        await Task.WhenAll(blocker, oldStart, newStop).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(harness.Session.IsRunning);
        Assert.Empty(harness.Session.ConnectedPeerFingerprints);
        Assert.Throws<InvalidOperationException>(() => harness.Session.CreateInvitation());
    }

    [Fact]
    public async Task NewerExplicitStartWinsOverQueuedOlderStop()
    {
        await using var harness = new QueueBarrierHarness();
        harness.Session.ObserveContext(1, true);
        await harness.Session.StartAsync(Options());
        var blocker = await harness.HoldQueueAsync(SyntheticPeer());
        var oldStop = harness.Session.StopAsync();
        Assert.False(harness.Session.IsRunning);
        var newStart = harness.Session.StartAsync(Options());
        harness.Release();
        await Task.WhenAll(blocker, oldStop, newStart).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(harness.Session.IsRunning);
        Assert.True(IPAddress.IsLoopback(IPAddress.Parse(harness.Session.CreateInvitation().Address)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedOldConnectCannotOutliveNewerDisconnectOrNetworkRestart(bool restartNetwork)
    {
        await using var first = new QueueBarrierHarness();
        await using var second = new NetworkHarness();
        first.Session.ObserveContext(1, true);
        second.Session.ObserveContext(2, true);
        await first.Session.StartAsync(Options());
        await second.Session.StartAsync(Options());
        var firstInvitation = first.Session.CreateInvitation();
        var secondInvitation = second.Session.CreateInvitation();
        await first.Session.PairAsync(secondInvitation);
        await second.Session.PairAsync(firstInvitation);
        var blocker = await first.HoldQueueAsync(secondInvitation);
        var oldConnect = first.Session.ConnectAsync(secondInvitation.Fingerprint);
        var newerOperations = new List<Task>();
        if (restartNetwork)
        {
            newerOperations.Add(first.Session.StopAsync());
            newerOperations.Add(first.Session.StartAsync(Options()));
        }
        else newerOperations.Add(first.Session.DisconnectAsync(secondInvitation.Fingerprint));
        first.Release();
        await Task.WhenAll(newerOperations.Append(oldConnect).Append(blocker)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(first.Session.IsRunning);
        Assert.Empty(first.Session.ConnectedPeerFingerprints);
        Assert.Empty(second.Session.ConnectedPeerFingerprints);
        Assert.Single(first.Session.Peers);
    }

    [Fact]
    public async Task NewerConnectWinsOverQueuedOlderDisconnect()
    {
        await using var first = new QueueBarrierHarness();
        await using var second = new NetworkHarness();
        first.Session.ObserveContext(1, true);
        second.Session.ObserveContext(2, true);
        await first.Session.StartAsync(Options());
        await second.Session.StartAsync(Options());
        var firstInvitation = first.Session.CreateInvitation();
        var secondInvitation = second.Session.CreateInvitation();
        await first.Session.PairAsync(secondInvitation);
        await second.Session.PairAsync(firstInvitation);
        var blocker = await first.HoldQueueAsync(secondInvitation);
        var oldDisconnect = first.Session.DisconnectAsync(secondInvitation.Fingerprint);
        var newConnect = first.Session.ConnectAsync(secondInvitation.Fingerprint);
        first.Release();
        await Task.WhenAll(blocker, oldDisconnect, newConnect).WaitAsync(TimeSpan.FromSeconds(5));
        await UntilAsync(() => first.Session.ConnectedPeerFingerprints.Count == 1 && second.Session.ConnectedPeerFingerprints.Count == 1);
    }

    [Fact]
    public async Task DisconnectClosesAnInProgressTlsHandshakeWithoutRemovingTrust()
    {
        await using var harness = new NetworkHarness();
        harness.Session.ObserveContext(1, true);
        await harness.Session.StartAsync(Options());
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var invitation = new PeerInvitation(1, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, new string('A', 64));
        await harness.Session.PairAsync(invitation);
        var connecting = harness.Session.ConnectAsync(invitation.Fingerprint);
        using var silentPeer = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Empty(harness.Session.ConnectedPeerFingerprints);
        var disconnect = harness.Session.DisconnectAsync(invitation.Fingerprint);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connecting.WaitAsync(TimeSpan.FromSeconds(3)));
        await disconnect.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(harness.Session.IsRunning);
        Assert.Empty(harness.Session.ConnectedPeerFingerprints);
        Assert.Equal(invitation, Assert.Single(harness.Session.Peers).Invitation);
    }

    private static NetworkStartOptions Options() => new() { NetworkEnabled = true, DirectIpConsent = true };
    private static PeerInvitation SyntheticPeer() => new(1, "127.0.0.1", 1234, new string('A', 64));
    private static async Task UntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }
}

internal sealed class QueueBarrierHarness : IAsyncDisposable
{
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int armed;
    public TestDirectory Directory { get; } = new();
    public NetworkSession Session { get; }

    public QueueBarrierHarness()
    {
        var store = new ProfileStore(Directory.Path, ProfileModuleTests.Adult());
        var trustStore = new PeerTrustStore(Directory.Path);
        Session = new NetworkSession(store, new PeerIdentityStore(Directory.Path), trustStore, async (peers, token) =>
        {
            if (Interlocked.Exchange(ref armed, 0) == 1)
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            }
            await trustStore.SaveAsync(peers, token);
        });
    }

    public async Task<Task> HoldQueueAsync(PeerInvitation peer)
    {
        Interlocked.Exchange(ref armed, 1);
        var pending = Session.PairAsync(peer, "Synthetic queue barrier");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        return pending;
    }

    public void Release() => release.TrySetResult();

    public async ValueTask DisposeAsync()
    {
        release.TrySetResult();
        await Session.DisposeAsync();
        Directory.Dispose();
    }
}
