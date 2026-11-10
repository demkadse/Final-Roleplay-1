using FRP1.Core.Identity;
using FRP1.Core.Networking;
using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using FRP1.Plugin.Networking;
using FRP1.Plugin.UiSocial;
using System.Text.Json.Nodes;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class SocialFilterPortTests
{
    [Fact]
    public void StatusAvailabilityAndEmptyProfileFiltersRemainIndependent()
    {
        var empty = ProfileTests.Valid() with { Status = RpStatus.Ic, Availability = RpAvailability.Open };
        Assert.True(SocialUiRules.IsEmptyProfile(empty));
        Assert.False(SocialUiRules.MatchesEnvironment(empty, new(HideEmpty: true)));
        var meaningful = empty with { Summary = "Synthetic RP introduction" };
        Assert.True(SocialUiRules.MatchesEnvironment(meaningful, new(RpStatus.Ic, RpAvailability.Open, true)));
        Assert.False(SocialUiRules.MatchesEnvironment(meaningful, new(RpStatus.Afk)));
        Assert.False(SocialUiRules.MatchesEnvironment(meaningful, new(Availability: RpAvailability.Unavailable)));
        Assert.True(SocialUiRules.MatchesEnvironment(null, new()));
        Assert.False(SocialUiRules.MatchesEnvironment(null, new(RpStatus.Ic)));
        Assert.False(SocialUiRules.MatchesEnvironment(null, new(HideEmpty: true)));
    }

    [Fact]
    public void DecorationAndDisabledGlancesDoNotPretendToBeWrittenRoleplayInformation()
    {
        var profile = ProfileTests.Valid() with
        {
            Portrait = new("image/png", PngFixture.Create()), AppearanceStyle = new() { IconId = 123, NameColor = "#AABBCC" },
            Glances = [new("Disabled", "Hidden text", Enabled: false)],
        };
        Assert.True(SocialUiRules.IsEmptyProfile(profile));
        Assert.False(SocialUiRules.IsEmptyProfile(profile with { Glances = [new("Shown", "Visible RP description")] }));
        Assert.False(SocialUiRules.IsEmptyProfile(profile with { Preferences = new() { Experience = RpExperience.Experienced } }));
    }

    [Fact]
    public async Task EarlierSocialPreferencesRemainReadableWithoutNewFiltersOrFixedPort()
    {
        using var directory = new TestDirectory();
        var store = new SocialUiOptionsStore(directory.Path);
        await store.SaveAsync(new() { MouseoverTooltip = false, TargetLocked = true });
        var path = Path.Combine(directory.Path, "social-ui.json");
        var json = JsonNode.Parse(await File.ReadAllBytesAsync(path))!.AsObject();
        foreach (var key in new[] { "mouseoverDetail", "environmentStatus", "environmentAvailability", "hideEmptyEnvironment", "listenPort" }) json.Remove(key);
        var earlier = json.ToJsonString();
        await File.WriteAllTextAsync(path, earlier);
        var loaded = await store.LoadAsync();
        Assert.False(loaded.MouseoverTooltip);
        Assert.True(loaded.TargetLocked);
        Assert.Equal(MouseoverDetail.Compact, loaded.MouseoverDetail);
        Assert.Null(loaded.EnvironmentStatus);
        Assert.Null(loaded.EnvironmentAvailability);
        Assert.False(loaded.HideEmptyEnvironment);
        Assert.Equal(0, loaded.ListenPort);
        Assert.Equal(earlier, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task NewSocialFiltersAndListenPortRoundtripLocally()
    {
        using var directory = new TestDirectory();
        var options = new SocialUiOptions { MouseoverDetail = MouseoverDetail.Detailed, EnvironmentStatus = RpStatus.Ic,
            EnvironmentAvailability = RpAvailability.Open, HideEmptyEnvironment = true, ListenPort = 49321 };
        await new SocialUiOptionsStore(directory.Path).SaveAsync(options);
        Assert.Equal(options, await new SocialUiOptionsStore(directory.Path).LoadAsync());
    }

    [Theory]
    [InlineData("mouseoverDetail", 999)]
    [InlineData("environmentStatus", 999)]
    [InlineData("environmentAvailability", 999)]
    [InlineData("listenPort", -1)]
    [InlineData("listenPort", 65536)]
    public async Task InvalidNewFilterAndPortValuesAreRejected(string field, int value)
    {
        using var directory = new TestDirectory();
        var store = new SocialUiOptionsStore(directory.Path);
        await store.SaveAsync(new());
        var path = Path.Combine(directory.Path, "social-ui.json");
        var json = JsonNode.Parse(await File.ReadAllBytesAsync(path))!.AsObject();
        json[field] = value;
        await File.WriteAllTextAsync(path, json.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(65535)]
    public void ListenPortAllowsEphemeralZeroAndAllValidExplicitPorts(int port) => SocialUiRules.ValidateListenPort(port);

    [Theory]
    [InlineData(-1)] [InlineData(65536)]
    public async Task InvalidPortsAreRejectedByUiCoreAndNetworkSession(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SocialUiRules.ValidateListenPort(port));
        using var directory = new TestDirectory();
        using var identity = PeerIdentity.CreateEphemeral();
        await using var node = new PeerNode(identity, new ProfileStore(directory.Path), new() { NetworkEnabled = true, ListenPort = port });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => node.StartAsync());
        await using var network = new NetworkHarness();
        network.Session.ObserveContext(1, true);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => network.Session.StartAsync(new() { NetworkEnabled = true, DirectIpConsent = true, ListenPort = port }));
    }

    [Theory]
    [InlineData("127.0.0.1", 12345)]
    [InlineData("::1", 65535)]
    public void AdvertisedInvitationChangesOnlyExplicitNumericEndpoint(string address, int port)
    {
        var original = new PeerInvitation(1, "127.0.0.1", 2000, new string('A', 64));
        var advertised = SocialUiRules.CreateAdvertisedInvitation(original, address, port);
        Assert.Equal(address, advertised.Address);
        Assert.Equal(port, advertised.Port);
        Assert.Equal(original.Fingerprint, advertised.Fingerprint);
        Assert.Equal(original.ProtocolVersion, advertised.ProtocolVersion);
        Assert.Equal(2000, original.Port);
        Assert.Equal(advertised, PeerInvitation.Parse(advertised.Encode()));
    }

    [Theory]
    [InlineData("example.invalid", 1234)]
    [InlineData("https://127.0.0.1", 1234)]
    [InlineData("0.0.0.0", 1234)]
    [InlineData("127.0.0.1", 0)]
    [InlineData("127.0.0.1", 65536)]
    public void AdvertisedEndpointsRejectDnsUrlsUnspecifiedAddressAndNonListeningPorts(string address, int port) =>
        Assert.Throws<InvalidDataException>(() => SocialUiRules.CreateAdvertisedInvitation(new(1, "127.0.0.1", 2000, new string('A', 64)), address, port));

    [Fact]
    public async Task ExplicitOccupiedPortIsNotSilentlyReplacedWithAnotherListener()
    {
        await using var occupied = new NetworkHarness();
        occupied.Session.ObserveContext(1, true);
        await occupied.Session.StartAsync(new() { NetworkEnabled = true, DirectIpConsent = true });
        var port = occupied.Session.CreateInvitation().Port;
        await using var contender = new NetworkHarness();
        contender.Session.ObserveContext(2, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => contender.Session.StartAsync(new() { NetworkEnabled = true, DirectIpConsent = true, ListenPort = port }));
        Assert.False(contender.Session.IsRunning);
        Assert.True(occupied.Session.IsRunning);
        Assert.Equal(port, occupied.Session.CreateInvitation().Port);
    }
}
