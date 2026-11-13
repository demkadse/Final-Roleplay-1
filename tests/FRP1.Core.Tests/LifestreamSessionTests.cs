using FRP1.Core.Safety;
using FRP1.Plugin.Integrations;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class LifestreamSessionTests
{
    [Fact]
    public void DefaultDisabledDoesNotReadProviderContextWorldsOrWriteSettings()
    {
        using var h = new Harness(enabled: false);
        for (var i = 0; i < 5; i++)
        {
            h.Session.ObserveScene();
            h.Session.Tick();
            Assert.Equal(LifestreamActionResult.Disabled, h.Session.RequestVisit());
            Assert.False(h.Session.SaveAddress(HousingAddressTests.House));
        }
        Assert.Equal(IntegrationStatus.Disabled, h.Session.Status);
        Assert.Equal(0, h.Captures);
        Assert.Equal(0, h.WorldReads);
        Assert.Equal(0, h.Provider.Checks);
        Assert.Equal(0, h.Provider.VersionReads);
        Assert.Equal(0, h.OptionWrites);
        Assert.Empty(h.Provider.Visits);
    }

    [Fact]
    public void SavingAndReplacingOneLocalDestinationNeverStartsTravel()
    {
        using var h = new Harness();
        h.Options = new(true);
        h.Session.Tick();
        Assert.Equal(LifestreamActionResult.Blocked, h.Session.RequestVisit());
        var address = HousingAddressTests.House with { Plot = 60, Apartment = 90, ApartmentSubdivision = true };
        Assert.True(h.Session.SaveAddress(address));
        Assert.Equal(address with { Apartment = 1, ApartmentSubdivision = false }, h.Options.Address);
        Assert.True(h.Session.SaveAddress(HousingAddressTests.House with { Ward = 30 }));
        Assert.Equal(30, h.Session.Options.Address!.Ward);
        Assert.Equal(2, h.OptionWrites);
        for (var i = 0; i < 5; i++) { h.Now += 1000; h.Session.Tick(); }
        Assert.Empty(h.Provider.Visits);
        Assert.False(h.Session.HasPendingVisit);
    }

    [Fact]
    public void InvalidOrUnknownDestinationDoesNotOverwriteTheSavedAddress()
    {
        using var h = new Harness();
        h.Session.Tick();
        var before = h.Options;
        Assert.False(h.Session.SaveAddress(HousingAddressTests.House with { Ward = 31 }));
        Assert.False(h.Session.SaveAddress(HousingAddressTests.House with { WorldId = 22 }));
        h.Worlds = [new(21, "Synthetic", 1, false)];
        Assert.False(h.Session.SaveAddress(HousingAddressTests.House));
        Assert.Equal(before, h.Options);
        Assert.Equal(0, h.OptionWrites);
        Assert.Empty(h.Provider.Visits);
    }

    [Fact]
    public void ExplicitClickQueuesOnceThenHandsOffExactlyOnceWithoutClaimingTravelCompletion()
    {
        using var h = new Harness();
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        Assert.True(h.Session.HasPendingVisit);
        Assert.Equal(LifestreamActionResult.AlreadyPending, h.Session.RequestVisit());
        Assert.Empty(h.Provider.Visits);
        h.Session.Tick();
        Assert.Equal(HousingAddressTests.House, Assert.Single(h.Provider.Visits));
        Assert.Equal(LifestreamActionResult.HandedOff, h.Session.LastAction);
        Assert.False(h.Session.HasPendingVisit);
        Assert.Equal(0, h.OptionWrites);
        for (var i = 0; i < 5; i++) { h.Now += 1000; h.Session.Tick(); }
        Assert.Single(h.Provider.Visits);
    }

    [Fact]
    public void RepeatedClicksRespectTheActionIntervalAtItsExactBoundary()
    {
        using var h = new Harness();
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        h.Session.Tick();
        h.Now = LifestreamSession.ActionIntervalMilliseconds - 1;
        Assert.Equal(LifestreamActionResult.RateLimited, h.Session.RequestVisit());
        Assert.Single(h.Provider.Visits);
        h.Now++;
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        h.Session.Tick();
        Assert.Equal(2, h.Provider.Visits.Count);
    }

    [Theory]
    [InlineData(IntegrationStatus.Missing)]
    [InlineData(IntegrationStatus.Incompatible)]
    [InlineData(IntegrationStatus.Busy)]
    [InlineData(IntegrationStatus.Error)]
    public void UnavailableOrAlreadyBusyProviderCannotQueueOrDispatch(IntegrationStatus status)
    {
        using var h = new Harness();
        h.Provider.Availability = status;
        Assert.Equal(LifestreamActionResult.NotReady, h.Session.RequestVisit());
        Assert.Equal(status, h.Session.Status);
        Assert.False(h.Session.HasPendingVisit);
        Assert.Empty(h.Provider.Visits);

        h.Provider.Availability = IntegrationStatus.Ready;
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        h.Provider.Availability = status;
        h.Session.Tick();
        Assert.Equal(LifestreamActionResult.NotReady, h.Session.LastAction);
        Assert.False(h.Session.HasPendingVisit);
        h.Provider.Availability = IntegrationStatus.Ready;
        h.Now += 1000;
        h.Session.Tick();
        Assert.Empty(h.Provider.Visits);
    }

    [Theory]
    [InlineData("current-world")]
    [InlineData("cid")]
    [InlineData("object")]
    [InlineData("actor-address")]
    [InlineData("name")]
    [InlineData("home-world")]
    [InlineData("territory")]
    [InlineData("map")]
    [InlineData("access")]
    [InlineData("unspecified")]
    [InlineData("dirty")]
    [InlineData("busy")]
    public void MissingLocalIdentityConsentOrCleanIdleStateBlocksBeforeReadingTheProvider(string invalid)
    {
        using var h = new Harness();
        var c = h.Context.Integration;
        h.Context = invalid switch
        {
            "current-world" => h.Context with { CurrentWorldId = 0 },
            _ => h.Context with { Integration = invalid switch
            {
                "cid" => c with { Actor = c.Actor with { ContentId = 0 } },
                "object" => c with { Actor = c.Actor with { ObjectId = 0 } },
                "actor-address" => c with { Actor = c.Actor with { Address = 0 } },
                "name" => c with { Actor = c.Actor with { Name = " " } },
                "home-world" => c with { Actor = c.Actor with { HomeWorldId = 0 } },
                "territory" => c with { Actor = c.Actor with { TerritoryId = 0 } },
                "map" => c with { Actor = c.Actor with { MapId = 0 } },
                _ => Change(h.Context, invalid).Integration,
            } },
        };
        h.Session.Tick();
        Assert.Equal(IntegrationStatus.Blocked, h.Session.Status);
        Assert.Equal(LifestreamActionResult.Blocked, h.Session.RequestVisit());
        Assert.Equal(0, h.Provider.Checks);
        Assert.Empty(h.Provider.Visits);
    }

    public static IEnumerable<object[]> ContextChanges => new[]
    {
        "cid", "object", "actor-address", "name", "home-world", "territory", "map", "current-world",
        "scene-epoch", "settings-generation", "access", "unspecified", "audience", "policy",
        "draft-null", "draft-id", "draft-revision", "draft-hash", "editor-generation", "dirty", "busy",
    }.Select(change => new object[] { change });

    [Theory]
    [MemberData(nameof(ContextChanges))]
    public void ContextChangedBetweenClickAndDispatchRevokesPendingVisit(string change)
    {
        using var h = new Harness();
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        h.Context = Change(h.Context, change);
        h.Session.Tick();
        Assert.Equal(LifestreamActionResult.Stale, h.Session.LastAction);
        Assert.False(h.Session.HasPendingVisit);
        Assert.Empty(h.Provider.Visits);
    }

    [Theory]
    [MemberData(nameof(ContextChanges))]
    public void ContextChangedInsideFinalProviderReadCannotReachTheTravelCall(string change)
    {
        using var h = new Harness();
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        h.Provider.DuringCheck = () => h.Context = Change(h.Context, change);
        h.Session.Tick();
        Assert.Equal(LifestreamActionResult.Stale, h.Session.LastAction);
        Assert.False(h.Session.HasPendingVisit);
        Assert.Empty(h.Provider.Visits);
    }

    [Theory]
    [MemberData(nameof(ContextChanges))]
    public void ObservedRoundTripFromAToBAndBackCannotRestoreAnEarlierClick(string change)
    {
        using var h = new Harness();
        var original = h.Context;
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        h.Context = Change(original, change);
        h.Session.ObserveScene();
        h.Context = original;
        h.Session.ObserveScene();
        h.Session.Tick();
        Assert.Equal(LifestreamActionResult.Stale, h.Session.LastAction);
        Assert.False(h.Session.HasPendingVisit);
        Assert.Empty(h.Provider.Visits);
    }

    [Theory]
    [InlineData("options")]
    [InlineData("disable-reenable")]
    [InlineData("ui-selection")]
    [InlineData("policy-epoch")]
    [InlineData("provider-version")]
    [InlineData("world-removed")]
    [InlineData("dispose")]
    public void SettingsUiPolicySourceWorldOrUnloadChangesRevokePendingVisits(string change)
    {
        using var h = new Harness();
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        switch (change)
        {
            case "options": h.Options = h.Options with { Address = HousingAddressTests.House with { Ward = 2 } }; break;
            case "disable-reenable":
                h.Options = h.Options with { Enabled = false }; h.Session.OptionsChanged(h.Options);
                h.Options = h.Options with { Enabled = true }; h.Session.OptionsChanged(h.Options); break;
            case "ui-selection": h.Session.AddressSelectionChanged(); break;
            case "policy-epoch": h.Session.InvalidatePolicy(); break;
            case "provider-version": h.Provider.Version++; break;
            case "world-removed": h.Worlds = []; break;
            case "dispose": h.Session.Dispose(); break;
        }
        h.Session.Tick();
        Assert.Equal(LifestreamActionResult.Stale, h.Session.LastAction);
        Assert.False(h.Session.HasPendingVisit);
        Assert.Empty(h.Provider.Visits);
    }

    [Theory]
    [InlineData("options")]
    [InlineData("policy-epoch")]
    [InlineData("ui-selection")]
    [InlineData("provider-version")]
    [InlineData("dispose")]
    public void ReentrantInvalidationDuringFinalAvailabilityReadIsRechecked(string change)
    {
        using var h = new Harness();
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        h.Provider.DuringCheck = () =>
        {
            switch (change)
            {
                case "options": h.Options = h.Options with { Enabled = false }; break;
                case "policy-epoch": h.Session.InvalidatePolicy(); break;
                case "ui-selection": h.Session.AddressSelectionChanged(); break;
                case "provider-version": h.Provider.Version++; break;
                case "dispose": h.Session.Dispose(); break;
            }
        };
        h.Session.Tick();
        Assert.Equal(LifestreamActionResult.Stale, h.Session.LastAction);
        Assert.False(h.Session.HasPendingVisit);
        Assert.Empty(h.Provider.Visits);
    }

    [Fact]
    public void LastContextCaptureAndWorldValidationCannotDispatchAChangedScene()
    {
        using var h = new Harness();
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        var captures = 0;
        h.BeforeCapture = () => { if (++captures == 2) h.Context = Change(h.Context, "current-world"); };
        h.Session.Tick();
        Assert.True(captures >= 2);
        Assert.Equal(LifestreamActionResult.Stale, h.Session.LastAction);
        Assert.Empty(h.Provider.Visits);
    }

    [Fact]
    public void ContextChangedWhileTheClickChecksAvailabilityIsNeverQueued()
    {
        using var h = new Harness();
        h.Provider.DuringCheck = () => h.Context = Change(h.Context, "cid");
        Assert.Equal(LifestreamActionResult.Stale, h.Session.RequestVisit());
        Assert.False(h.Session.HasPendingVisit);
        Assert.Empty(h.Provider.Visits);
    }

    [Fact]
    public void ProviderReadExceptionsAndThrowingTravelFailClosedWithoutRetry()
    {
        using var h = new Harness();
        h.Provider.DuringCheck = () => throw new InvalidOperationException("Synthetic availability failure");
        Assert.Equal(LifestreamActionResult.NotReady, h.Session.RequestVisit());
        Assert.Equal(IntegrationStatus.Error, h.Session.Status);
        Assert.Empty(h.Provider.Visits);
        h.Provider.DuringCheck = null;
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        h.Provider.ThrowVisit = true;
        h.Session.Tick();
        Assert.Equal(IntegrationStatus.Error, h.Session.Status);
        Assert.Equal(LifestreamActionResult.NotReady, h.Session.LastAction);
        Assert.False(h.Session.HasPendingVisit);
        Assert.Single(h.Provider.Visits);
        for (var i = 0; i < 5; i++) { h.Now += 1000; h.Session.Tick(); }
        Assert.Single(h.Provider.Visits);
    }

    [Fact]
    public void MissingOrThrowingWorldCatalogBlocksSavingAndTravelWithoutCallingProvider()
    {
        using var h = new Harness();
        h.Session.OptionsChanged(h.Options);
        h.DuringWorldRead = () => throw new InvalidOperationException("Synthetic world failure");
        Assert.False(h.Session.SaveAddress(HousingAddressTests.House));
        Assert.Equal(LifestreamActionResult.Blocked, h.Session.RequestVisit());
        Assert.Equal(0, h.Provider.Checks);
        Assert.Equal(0, h.OptionWrites);
        Assert.Empty(h.Provider.Visits);
    }

    [Fact]
    public void StatusPollingIsLimitedAndNeverTurnsIntoAutomaticTravel()
    {
        using var h = new Harness();
        h.Session.Tick();
        Assert.Equal(1, h.Provider.Checks);
        for (var i = 0; i < 10; i++) { h.Session.ObserveScene(); h.Session.Tick(); }
        Assert.Equal(1, h.Provider.Checks);
        h.Now = LifestreamSession.ReadIntervalMilliseconds - 1;
        h.Session.Tick();
        Assert.Equal(1, h.Provider.Checks);
        h.Now++;
        h.Session.Tick();
        Assert.Equal(2, h.Provider.Checks);
        Assert.Empty(h.Provider.Visits);
    }

    [Fact]
    public void EqualDetachedOptionsCopiesDoNotCancelAQueuedExplicitVisit()
    {
        using var h = new Harness();
        h.CopyOptionsOnRead = true;
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        h.Session.Tick();
        Assert.Single(h.Provider.Visits);
        Assert.Equal(LifestreamActionResult.HandedOff, h.Session.LastAction);
    }

    [Fact]
    public void OffFrameworkThreadCannotReadProviderOrQueueAndCannotDispatchPendingVisit()
    {
        using var h = new Harness();
        h.OnFrameworkThread = false;
        h.Session.Tick();
        h.Session.ObserveScene();
        Assert.Equal(LifestreamActionResult.Blocked, h.Session.RequestVisit());
        Assert.Equal(0, h.Provider.Checks);
        h.OnFrameworkThread = true;
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        h.OnFrameworkThread = false;
        h.Session.Tick();
        Assert.Empty(h.Provider.Visits);
        h.Session.Dispose();
        h.OnFrameworkThread = true;
        h.Session.Tick();
        Assert.Empty(h.Provider.Visits);
    }

    [Fact]
    public void DisposalIsIdempotentAndNoEntryPointCanRestartWorkEvenIfProviderDisposalThrows()
    {
        using var h = new Harness();
        Assert.Equal(LifestreamActionResult.Queued, h.Session.RequestVisit());
        h.Provider.ThrowDispose = true;
        h.Session.Dispose();
        h.Session.Dispose();
        var checks = h.Provider.Checks;
        h.Session.ObserveScene();
        h.Session.Tick();
        h.Session.OptionsChanged(new(true, HousingAddressTests.House));
        Assert.Equal(LifestreamActionResult.Disabled, h.Session.RequestVisit());
        Assert.False(h.Session.SaveAddress(HousingAddressTests.House));
        Assert.False(h.Session.HasPendingVisit);
        Assert.Equal(IntegrationStatus.Disabled, h.Session.Status);
        Assert.Equal(checks, h.Provider.Checks);
        Assert.Equal(1, h.Provider.Disposals);
        Assert.Empty(h.Provider.Visits);
    }

    private static LifestreamContext Change(LifestreamContext context, string change)
    {
        var c = context.Integration;
        return change switch
        {
            "current-world" => context with { CurrentWorldId = context.CurrentWorldId + 1 },
            "scene-epoch" => context with { SceneEpoch = context.SceneEpoch + 1 },
            "settings-generation" => context with { SettingsGeneration = context.SettingsGeneration + 1 },
            _ => context with { Integration = change switch
            {
                "cid" => c with { Actor = c.Actor with { ContentId = 99 } },
                "object" => c with { Actor = c.Actor with { ObjectId = 99 } },
                "actor-address" => c with { Actor = c.Actor with { Address = 99 } },
                "name" => c with { Actor = c.Actor with { Name = "Other Synthetic Actor" } },
                "home-world" => c with { Actor = c.Actor with { HomeWorldId = 99 } },
                "territory" => c with { Actor = c.Actor with { TerritoryId = 99 } },
                "map" => c with { Actor = c.Actor with { MapId = 99 } },
                "access" => c with { AccessReady = false },
                "unspecified" => c with { Audience = new() },
                "audience" => c with { Audience = new(AudienceSelection.Under18) },
                "policy" => c with { PolicyGeneration = c.PolicyGeneration + 1 },
                "draft-null" => c with { Draft = null },
                "draft-id" => c with { Draft = c.Draft! with { ProfileId = Guid.NewGuid() } },
                "draft-revision" => c with { Draft = c.Draft! with { Revision = c.Draft.Revision + 1 } },
                "draft-hash" => c with { Draft = c.Draft! with { Hash = "Other synthetic hash" } },
                "editor-generation" => c with { Draft = c.Draft! with { EditorGeneration = c.Draft.EditorGeneration + 1 } },
                "dirty" => c with { Dirty = true },
                "busy" => c with { Busy = true },
                _ => throw new ArgumentOutOfRangeException(nameof(change)),
            } },
        };
    }

    private sealed class FakeProvider : ILifestreamProvider
    {
        private long version;
        public long Version { get { VersionReads++; return version; } set => version = value; }
        public int VersionReads { get; private set; }
        public int Checks { get; private set; }
        public int Disposals { get; private set; }
        public IntegrationStatus Availability { get; set; } = IntegrationStatus.Ready;
        public Action? DuringCheck { get; set; }
        public bool ThrowVisit { get; set; }
        public bool ThrowDispose { get; set; }
        public List<HousingAddress> Visits { get; } = [];
        public IntegrationStatus CheckAvailability() { Checks++; DuringCheck?.Invoke(); return Availability; }
        public void GoToHousingAddress(HousingAddress address)
        {
            Visits.Add(address);
            if (ThrowVisit) throw new InvalidOperationException("Synthetic travel failure");
        }
        public void Dispose()
        {
            Disposals++;
            if (ThrowDispose) throw new InvalidOperationException("Synthetic dispose failure");
        }
    }

    private sealed class Harness : IDisposable
    {
        public FakeProvider Provider { get; } = new();
        public LifestreamContext Context { get; set; } = new(new(new(1, 2, 3, "Synthetic Actor", 4, 5, 6), true,
            new(AudienceSelection.Adult, true), 1, new(Guid.NewGuid(), 1, "Synthetic hash", 1), false, false), 21, 1);
        public LifestreamOptions Options { get; set; }
        public IReadOnlyList<HousingWorld> Worlds { get; set; } = [new(21, "Synthetic World", 1)];
        public LifestreamSession Session { get; }
        public long Now { get; set; }
        public bool OnFrameworkThread { get; set; } = true;
        public bool CopyOptionsOnRead { get; set; }
        public int Captures { get; private set; }
        public int WorldReads { get; private set; }
        public int OptionWrites { get; private set; }
        public Action? BeforeCapture { get; set; }
        public Action? DuringWorldRead { get; set; }

        public Harness(bool enabled = true)
        {
            Options = new(enabled, HousingAddressTests.House);
            Session = new(Provider, () => { Captures++; BeforeCapture?.Invoke(); return Context; },
                () => { WorldReads++; DuringWorldRead?.Invoke(); return Worlds; },
                () => CopyOptionsOnRead ? Options with { Address = Options.Address is { } address ? address with { } : null } : Options,
                value => { Options = value; OptionWrites++; }, () => Now, () => OnFrameworkThread);
        }
        public void Dispose() => Session.Dispose();
    }
}
