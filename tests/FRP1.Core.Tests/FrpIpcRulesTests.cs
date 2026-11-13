using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Integrations;
using FRP1.Plugin.Ipc;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class FrpIpcRulesTests
{
    [Theory]
    [InlineData(false, false, false, false, false, 0)]
    [InlineData(false, false, true, true, true, 0)]
    [InlineData(false, true, false, false, false, 0)]
    [InlineData(true, false, true, true, true, 1)]
    [InlineData(false, true, true, false, false, 2)]
    [InlineData(false, true, false, true, false, 4)]
    [InlineData(false, true, false, false, true, 8)]
    [InlineData(true, true, true, true, true, 15)]
    public void CapabilitiesRequireIndependentReadAndGlobalPlusIndividualControlOptIns(
        bool read, bool control, bool status, bool availability, bool open, int expected)
    {
        Assert.Equal(expected, FrpIpcRules.Capabilities(new(read, control, status, availability, open)));
    }

    [Theory]
    [InlineData(FrpIpcActionKind.SetStatus, -1, false)]
    [InlineData(FrpIpcActionKind.SetStatus, 0, true)]
    [InlineData(FrpIpcActionKind.SetStatus, 2, true)]
    [InlineData(FrpIpcActionKind.SetStatus, 3, false)]
    [InlineData(FrpIpcActionKind.SetAvailability, -1, false)]
    [InlineData(FrpIpcActionKind.SetAvailability, 0, true)]
    [InlineData(FrpIpcActionKind.SetAvailability, 2, true)]
    [InlineData(FrpIpcActionKind.SetAvailability, int.MaxValue, false)]
    [InlineData(FrpIpcActionKind.OpenOwnProfile, 0, true)]
    [InlineData(FrpIpcActionKind.OpenOwnProfile, 1, false)]
    [InlineData((FrpIpcActionKind)99, 0, false)]
    public void InvalidActionKindsAndOutOfRangeValuesAreRejected(FrpIpcActionKind kind, int value, bool valid)
    {
        Assert.Equal(valid, FrpIpcRules.ValidAction(new(kind, value)));
    }

    [Fact]
    public void PrivateSnapshotConversionKeepsCanonicalRevisionHashWithoutReturningFullProfile()
    {
        var profile = new ProfileDocument { DisplayName = "Synthetic Profile", Status = RpStatus.Afk,
            Availability = RpAvailability.Unavailable };
        var snapshot = FrpIpcProfile.From(profile);
        Assert.Equal(profile.Id, snapshot.Id);
        Assert.Equal(profile.Revision, snapshot.Revision);
        Assert.Equal(ProfileJson.Hash(profile), snapshot.Hash);
        Assert.Equal(RpStatus.Afk, snapshot.Status);
        Assert.Equal(RpAvailability.Unavailable, snapshot.Availability);
    }
}
