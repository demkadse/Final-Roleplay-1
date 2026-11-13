using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Integrations;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class MoodlesSessionTests
{
    [Fact]
    public void DisabledIntegrationDoesNoContextCaptureReadApplyOrSettingsWrite()
    {
        using var h = new Harness(enabled: false);
        for (var i = 0; i < 10; i++) { h.Session.RequestRead(); h.Session.Tick(); }
        Assert.Empty(h.Session.VisibleStatuses);
        Assert.Equal(IntegrationActionResult.Disabled, h.Session.RequestImport(h.Source.StatusId));
        Assert.Equal(IntegrationActionResult.Disabled, h.Session.RequestRating(h.Source.StatusId, ContentRating.General));
        Assert.False(h.Session.SetSelected(h.Source.StatusId, true));
        Assert.Equal(IntegrationStatus.Disabled, h.Session.Status);
        Assert.Equal(0, h.Captures);
        Assert.Equal(0, h.Reader.Reads);
        Assert.Equal(0, h.OptionWrites);
        Assert.Empty(h.Applied);
    }

    [Theory]
    [InlineData("cid")]
    [InlineData("object")]
    [InlineData("address")]
    [InlineData("name")]
    [InlineData("world")]
    [InlineData("territory")]
    [InlineData("map")]
    [InlineData("access")]
    [InlineData("unspecified")]
    public void IncompleteLocalActorOrUnselectedAudiencePreventsAnySourceRead(string invalid)
    {
        using var h = new Harness();
        var c = h.Context.Integration;
        h.Context = h.Context with { Integration = invalid switch
        {
            "cid" => c with { Actor = c.Actor with { ContentId = 0 } },
            "object" => c with { Actor = c.Actor with { ObjectId = 0 } },
            "address" => c with { Actor = c.Actor with { Address = 0 } },
            "name" => c with { Actor = c.Actor with { Name = " " } },
            "world" => c with { Actor = c.Actor with { HomeWorldId = 0 } },
            "territory" => c with { Actor = c.Actor with { TerritoryId = 0 } },
            "map" => c with { Actor = c.Actor with { MapId = 0 } },
            _ => Change(h.Context, invalid).Integration,
        } };
        h.Session.Tick();
        Assert.Equal(IntegrationStatus.Blocked, h.Session.Status);
        Assert.Empty(h.Session.VisibleStatuses);
        Assert.Equal(0, h.Reader.Reads);
        Assert.Empty(h.Applied);
    }

    [Theory]
    [InlineData(IntegrationStatus.Missing)]
    [InlineData(IntegrationStatus.Incompatible)]
    [InlineData(IntegrationStatus.Error)]
    [InlineData(IntegrationStatus.Busy)]
    public void UnavailableSourceNeverExposesCachedTextOrQueuesAnAction(IntegrationStatus status)
    {
        using var h = new Harness();
        h.Reader.Result = new(status, [h.Source]);
        h.Session.Tick();
        Assert.Equal(status, h.Session.Status);
        Assert.Empty(h.Session.VisibleStatuses);
        Assert.Equal(IntegrationActionResult.NotReady, h.Session.RequestImport(h.Source.StatusId));
        Assert.Empty(h.Applied);
    }

    [Fact]
    public void InvalidDetachedSnapshotAndThrownReadFailClosedWithoutLeakingSourceText()
    {
        using var h = new Harness();
        h.Reader.Result = new(IntegrationStatus.Ready, [h.Source, h.Source]);
        h.Session.Tick();
        Assert.Equal(IntegrationStatus.Error, h.Session.Status);
        Assert.Empty(h.Session.VisibleStatuses);
        h.Reader.DuringRead = () => throw new InvalidOperationException("Synthetic failure");
        h.Refresh();
        Assert.Equal(IntegrationStatus.Error, h.Session.Status);
        Assert.Empty(h.Session.VisibleStatuses);
        Assert.Empty(h.Applied);
        h.Reader.DuringRead = null;
        h.Reader.Result = new(IntegrationStatus.Ready, [h.Source]);
        h.Refresh();
        Assert.Equal(h.Source, Assert.Single(h.Session.VisibleStatuses));
    }

    [Fact]
    public void ReadAndSelectionNeverAutoClassifyOrApplyAndFiveSelectionsAreTheMaximum()
    {
        using var h = new Harness(selected: false);
        var sources = Enumerable.Range(0, 6).Select(_ => MoodlesRulesTests.Status()).ToArray();
        h.Reader.Result = new(IntegrationStatus.Ready, sources);
        h.Session.Tick();
        Assert.Equal(6, h.Session.VisibleStatuses.Count);
        Assert.Empty(h.Applied);
        for (var i = 0; i < 5; i++)
        {
            Assert.True(h.Session.SetSelected(sources[i].StatusId, true));
            h.Refresh();
        }
        Assert.False(h.Session.SetSelected(sources[0].StatusId, true));
        Assert.False(h.Session.SetSelected(sources[5].StatusId, true));
        Assert.Equal(5, h.Options.Bindings!.Count);
        Assert.All(h.Options.Bindings, binding => Assert.Null(binding.Rating));
        Assert.Empty(h.Applied);
        Assert.Equal(IntegrationActionResult.Blocked, h.Session.RequestImport(sources[0].StatusId));
        Assert.True(h.Session.SetSelected(sources[0].StatusId, false));
        h.Refresh();
        Assert.True(h.Session.SetSelected(sources[5].StatusId, true));
        Assert.Equal(5, h.Options.Bindings!.Count);
    }

    [Fact]
    public void ExplicitGeneralImportReReadsSourceAndAppliesOnlyOneDraftGlanceWithItsOriginalToken()
    {
        using var h = new Harness();
        h.Session.Tick();
        Assert.Empty(h.Applied);
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport(h.Source.StatusId));
        Assert.Equal(IntegrationActionResult.AlreadyPending, h.Session.RequestImport(h.Source.StatusId));
        Assert.Equal(IntegrationActionResult.AlreadyPending, h.Session.RequestRating(h.Source.StatusId, ContentRating.General));
        Assert.True(h.Session.HasPendingImport);
        Assert.Empty(h.Applied);
        h.Session.Tick();
        Assert.Equal(2, h.Reader.Reads);
        Assert.Equal(IntegrationActionResult.Applied, h.Session.LastAction);
        Assert.False(h.Session.HasPendingImport);
        var applied = Assert.Single(h.Applied);
        Assert.Equal(h.Context.Integration.Draft, applied.Token);
        Assert.Equal(h.Source.Label, applied.Glance.Label);
        Assert.Equal(h.Source.Description, applied.Glance.Text);
        Assert.Equal(h.Source.IconId, applied.Glance.IconId);
        Assert.Equal(ContentRating.General, applied.Rating);
        Assert.Equal(0, h.OptionWrites);
        h.Session.Tick();
        Assert.Single(h.Applied);
        Assert.Equal(IntegrationActionResult.RateLimited, h.Session.RequestImport(h.Source.StatusId));
        h.Now += MoodlesSession.ImportIntervalMilliseconds;
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport(h.Source.StatusId));
    }

    [Fact]
    public void EventBurstsCoalesceToOneReadPerSecondAndQuietFramesDoNotPoll()
    {
        using var h = new Harness();
        h.Session.Tick();
        Assert.Equal(1, h.Reader.Reads);
        for (var i = 0; i < 1000; i++) { h.Reader.Version++; h.Session.RequestRead(); }
        Assert.Empty(h.Session.VisibleStatuses);
        h.Session.Tick();
        Assert.Equal(1, h.Reader.Reads);
        h.Now = MoodlesSession.ReadIntervalMilliseconds - 1;
        h.Session.Tick();
        Assert.Equal(1, h.Reader.Reads);
        h.Now++;
        h.Session.Tick();
        Assert.Equal(2, h.Reader.Reads);
        h.Now += MoodlesSession.ReadIntervalMilliseconds * 10;
        for (var i = 0; i < 100; i++) h.Session.Tick();
        Assert.Equal(2, h.Reader.Reads);
        Assert.Empty(h.Applied);
    }

    public static IEnumerable<object[]> ContextChanges => new[] { "cid", "object", "address", "name", "world", "territory", "map",
        "access", "audience", "unspecified", "policy", "draft-null", "draft-id", "draft-revision", "draft-hash",
        "editor-generation", "dirty", "busy", "target-rating", "glance-count" }.Select(change => new object[] { change });

    [Theory]
    [MemberData(nameof(ContextChanges))]
    public void ContextChangeDuringSourceReadDiscardsTheWholeSnapshot(string change)
    {
        using var h = new Harness();
        h.Reader.DuringRead = () => h.Context = Change(h.Context, change);
        h.Session.Tick();
        Assert.Equal(1, h.Reader.Reads);
        Assert.Empty(h.Session.VisibleStatuses);
        Assert.NotEqual(IntegrationStatus.Ready, h.Session.Status);
        Assert.Empty(h.Applied);
    }

    [Theory]
    [MemberData(nameof(ContextChanges))]
    public void QueuedImportRechecksEveryContextFieldAndRetainsNoStalePendingAction(string change)
    {
        using var h = new Harness();
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport(h.Source.StatusId));
        h.Context = Change(h.Context, change);
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Stale, h.Session.LastAction);
        Assert.False(h.Session.HasPendingImport);
        Assert.Empty(h.Applied);
    }

    [Theory]
    [MemberData(nameof(ContextChanges))]
    public void ContextChangeInsideFreshImportReadCannotReachTheDraft(string change)
    {
        using var h = new Harness();
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport(h.Source.StatusId));
        h.Reader.DuringRead = () => h.Context = Change(h.Context, change);
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Stale, h.Session.LastAction);
        Assert.Empty(h.Applied);
        Assert.Empty(h.Session.VisibleStatuses);
    }

    [Theory]
    [MemberData(nameof(ContextChanges))]
    public void ContextChangeAtImmediatePreapplyCaptureCannotReachTheDraft(string change)
    {
        using var h = new Harness();
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport(h.Source.StatusId));
        var captures = 0;
        h.BeforeCapture = () => { if (++captures == 2) h.Context = Change(h.Context, change); };
        h.Session.Tick();
        Assert.True(captures >= 2);
        Assert.Equal(IntegrationActionResult.Stale, h.Session.LastAction);
        Assert.Empty(h.Applied);
    }

    [Theory]
    [InlineData("dirty")]
    [InlineData("busy")]
    [InlineData("draft-null")]
    [InlineData("full")]
    [InlineData("negative-count")]
    public void DraftMustBeCleanIdleAndHaveCapacityBeforeImportCanQueue(string change)
    {
        using var h = new Harness();
        h.Context = change switch
        {
            "full" => h.Context with { GlanceCount = 5 },
            "negative-count" => h.Context with { GlanceCount = -1 },
            _ => Change(h.Context, change),
        };
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Blocked, h.Session.RequestImport(h.Source.StatusId));
        h.Session.Tick();
        Assert.Empty(h.Applied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SilentSourceChangeInvalidatesQueuedImportAndRatingEvenWithoutVersionEvent(bool ratingAction)
    {
        using var h = new Harness();
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Queued, ratingAction
            ? h.Session.RequestRating(h.Source.StatusId, ContentRating.General) : h.Session.RequestImport(h.Source.StatusId));
        h.Reader.Result = new(IntegrationStatus.Ready, [MoodlesRulesTests.Status("Changed") with { StatusId = h.Source.StatusId }]);
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Stale, h.Session.LastAction);
        Assert.False(h.Session.HasPendingImport);
        Assert.Empty(h.Applied);
        Assert.Equal(0, h.OptionWrites);
        h.Refresh();
        Assert.Null(MoodlesRules.ReviewedRating(Assert.Single(h.Session.VisibleStatuses), h.Options));
        Assert.Equal(IntegrationActionResult.Blocked, h.Session.RequestImport(h.Source.StatusId));
    }

    [Fact]
    public void RatingIsAnExplicitFreshReadActionAndDoesNotAutomaticallyCopyTheGlance()
    {
        using var h = new Harness(rating: null);
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Blocked, h.Session.RequestImport(h.Source.StatusId));
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestRating(h.Source.StatusId, ContentRating.General));
        Assert.Null(Assert.Single(h.Options.Bindings!).Rating);
        h.Session.Tick();
        Assert.Equal(2, h.Reader.Reads);
        Assert.Equal(IntegrationActionResult.Applied, h.Session.LastAction);
        Assert.Equal(ContentRating.General, Assert.Single(h.Options.Bindings!).Rating);
        Assert.Empty(h.Applied);
        Assert.Equal(1, h.OptionWrites);
        h.Refresh();
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport(h.Source.StatusId));
        h.Session.Tick();
        Assert.Single(h.Applied);
    }

    [Theory]
    [InlineData(AudienceSelection.Under18, false, null, false)]
    [InlineData(AudienceSelection.Under18, true, null, false)]
    [InlineData(AudienceSelection.Adult, false, null, false)]
    [InlineData(AudienceSelection.Adult, true, null, true)]
    [InlineData(AudienceSelection.Under18, false, ContentRating.General, true)]
    [InlineData(AudienceSelection.Under18, false, ContentRating.Mature18, false)]
    [InlineData(AudienceSelection.Adult, false, ContentRating.Mature18, false)]
    public void UnclassifiedTextAndIconsStayHiddenWithoutAdultOptInAndOnlyExactReviewedGeneralCanPassForMinors(
        AudienceSelection selection, bool optIn, ContentRating? rating, bool visible)
    {
        using var h = new Harness(rating: rating);
        h.Context = h.Context with { Integration = h.Context.Integration with { Audience = new(selection, optIn) } };
        h.Session.Tick();
        Assert.Equal(visible ? 1 : 0, h.Session.VisibleStatuses.Count);
        if (!visible)
        {
            Assert.False(h.Session.SetSelected(h.Source.StatusId, true));
            Assert.Equal(IntegrationActionResult.NotReady, h.Session.RequestRating(h.Source.StatusId, ContentRating.General));
        }
        else if (selection != AudienceSelection.Adult || !optIn)
            Assert.Equal(IntegrationActionResult.Blocked, h.Session.RequestRating(h.Source.StatusId, ContentRating.Mature18));
        Assert.Empty(h.Applied);
    }

    [Theory]
    [InlineData(ContentRating.General, false)]
    [InlineData(ContentRating.Mature18, true)]
    public void MatureCopyRequiresMatureTargetEvenForAnOptedInAdult(ContentRating target, bool allowed)
    {
        using var h = new Harness(rating: ContentRating.Mature18);
        h.Context = h.Context with { TargetRating = target };
        h.Session.Tick();
        Assert.Equal(allowed ? IntegrationActionResult.Queued : IntegrationActionResult.Blocked, h.Session.RequestImport(h.Source.StatusId));
        h.Session.Tick();
        Assert.Equal(allowed ? 1 : 0, h.Applied.Count);
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("disable-reenable")]
    [InlineData("options")]
    [InlineData("reader-version")]
    [InlineData("dispose")]
    public void ConfigurationChangeUnloadAndSourceVersionRevokePendingActions(string change)
    {
        using var h = new Harness();
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport(h.Source.StatusId));
        switch (change)
        {
            case "disable": h.Options = h.Options with { Enabled = false }; break;
            case "disable-reenable":
                h.Options = h.Options with { Enabled = false }; h.Session.OptionsChanged(h.Options);
                h.Options = h.Options with { Enabled = true }; h.Session.OptionsChanged(h.Options); break;
            case "options": h.Options = h.Options with { Bindings = [] }; break;
            case "reader-version": h.Reader.Version++; break;
            case "dispose": h.Session.Dispose(); break;
        }
        Assert.Empty(h.Session.VisibleStatuses);
        h.Session.Tick();
        Assert.False(h.Session.HasPendingImport);
        Assert.Equal(IntegrationActionResult.Stale, h.Session.LastAction);
        Assert.Empty(h.Applied);
    }

    [Fact]
    public void ValueEquivalentOptionsCopiesDoNotInvalidateEveryFrame()
    {
        using var h = new Harness();
        h.CopyOptionsOnRead = true;
        h.Session.Tick();
        for (var i = 0; i < 10; i++)
        {
            h.Now += 1000;
            h.Session.Tick();
            Assert.Single(h.Session.VisibleStatuses);
        }
        Assert.Equal(1, h.Reader.Reads);
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport(h.Source.StatusId));
        h.Session.Tick();
        Assert.Single(h.Applied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedDraftCompareAndSwapAndThrowingApplyAreNotReportedAsApplied(bool throws)
    {
        using var h = new Harness();
        h.ApplyResult = false;
        h.ThrowApply = throws;
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport(h.Source.StatusId));
        h.Session.Tick();
        Assert.Single(h.Applied);
        Assert.Equal(throws ? IntegrationActionResult.Blocked : IntegrationActionResult.Stale, h.Session.LastAction);
        Assert.False(h.Session.HasPendingImport);
    }

    [Fact]
    public void DisposalIsIdempotentAndAllEntryPointsRemainInactive()
    {
        var h = new Harness();
        h.Session.Tick();
        var reads = h.Reader.Reads;
        h.Session.Dispose();
        h.Session.Dispose();
        h.Session.Tick();
        h.Session.RequestRead();
        h.Session.OptionsChanged(new(true));
        Assert.False(h.Session.SetSelected(h.Source.StatusId, true));
        Assert.Equal(IntegrationActionResult.Disabled, h.Session.RequestImport(h.Source.StatusId));
        Assert.Equal(IntegrationActionResult.Disabled, h.Session.RequestRating(h.Source.StatusId, ContentRating.General));
        Assert.Equal(reads, h.Reader.Reads);
        Assert.Equal(1, h.Reader.Disposals);
        Assert.Empty(h.Session.VisibleStatuses);
        Assert.Empty(h.Applied);
    }

    private static MoodlesContext Change(MoodlesContext context, string change)
    {
        var c = context.Integration;
        return change switch
        {
            "target-rating" => context with { TargetRating = ContentRating.Mature18 },
            "glance-count" => context with { GlanceCount = context.GlanceCount + 1 },
            _ => context with { Integration = change switch
            {
                "cid" => c with { Actor = c.Actor with { ContentId = 99 } },
                "object" => c with { Actor = c.Actor with { ObjectId = 99 } },
                "address" => c with { Actor = c.Actor with { Address = 99 } },
                "name" => c with { Actor = c.Actor with { Name = "Other Synthetic Actor" } },
                "world" => c with { Actor = c.Actor with { HomeWorldId = 99 } },
                "territory" => c with { Actor = c.Actor with { TerritoryId = 99 } },
                "map" => c with { Actor = c.Actor with { MapId = 99 } },
                "access" => c with { AccessReady = false },
                "audience" => c with { Audience = new(AudienceSelection.Under18) },
                "unspecified" => c with { Audience = new() },
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

    private sealed class FakeReader : IMoodlesReader
    {
        public long Version { get; set; }
        public int Reads { get; private set; }
        public int Disposals { get; private set; }
        public Action? DuringRead { get; set; }
        public MoodlesReadResult Result { get; set; } = new(IntegrationStatus.Ready, []);
        public MoodlesReadResult ReadLocal() { Reads++; DuringRead?.Invoke(); return Result; }
        public void Dispose() => Disposals++;
    }

    private sealed class Harness : IDisposable
    {
        public MoodlesStatus Source { get; } = MoodlesRulesTests.Status();
        public FakeReader Reader { get; } = new();
        public MoodlesContext Context { get; set; } = new(new(new(1, 2, 3, "Synthetic Actor", 4, 5, 6), true,
            new(AudienceSelection.Adult, true), 1, new(Guid.NewGuid(), 1, "Synthetic hash", 1), false, false), ContentRating.General, 0);
        public MoodlesOptions Options { get; set; }
        public MoodlesSession Session { get; }
        public long Now { get; set; }
        public int Captures { get; private set; }
        public int OptionWrites { get; private set; }
        public bool ApplyResult { get; set; } = true;
        public bool ThrowApply { get; set; }
        public bool CopyOptionsOnRead { get; set; }
        public Action? BeforeCapture { get; set; }
        public List<(IntegrationDraftToken Token, GlanceEntry Glance, ContentRating Rating)> Applied { get; } = [];

        public Harness(bool enabled = true, bool selected = true, ContentRating? rating = ContentRating.General)
        {
            Options = new(enabled, selected ? [new(Source.StatusId, Source.ContentHash, rating)] : []);
            Reader.Result = new(IntegrationStatus.Ready, [Source]);
            Session = new(Reader, () => { Captures++; BeforeCapture?.Invoke(); return Context; },
                (token, glance, contentRating) =>
                {
                    Applied.Add((token, glance, contentRating));
                    if (ThrowApply) throw new InvalidOperationException("Synthetic apply failure");
                    return ApplyResult;
                }, () => CopyOptionsOnRead ? Options with { Bindings = Options.Bindings?.ToArray() } : Options,
                value => { Options = value; OptionWrites++; }, () => Now);
        }

        public void Refresh() { Now += MoodlesSession.ReadIntervalMilliseconds; Session.RequestRead(); Session.Tick(); }
        public void Dispose() => Session.Dispose();
    }
}
