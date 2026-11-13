using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Integrations;
using FRP1.Plugin.Ipc;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class FrpIpcSessionTests
{
    private static readonly FrpIpcAction StatusAction = new(FrpIpcActionKind.SetStatus, (int)RpStatus.Afk);
    private static readonly FrpIpcAction AvailabilityAction = new(FrpIpcActionKind.SetAvailability, (int)RpAvailability.Unavailable);
    private static readonly FrpIpcAction OpenAction = new(FrpIpcActionKind.OpenOwnProfile);
    private static readonly IntegrationIpcOptions AllOptions = new(true, true, true, true, true);

    [Fact]
    public async Task DefaultOffNeverCapturesOrDispatchesAndReturnsNoStateValues()
    {
        await using var h = new Harness();
        for (var i = 0; i < 10; i++) { h.Session.Tick(); h.Session.Refresh(); }
        Assert.Equal(0, h.Captures);
        Assert.Equal(0, h.Session.GetCapabilities());
        Assert.Equal((int)FrpIpcReadiness.Disabled, h.Session.GetReadiness());
        Assert.Equal(((int)FrpIpcResult.Disabled, -1, -1), h.Session.GetOwnState());
        Assert.Equal((long)FrpIpcResult.Disabled, h.Session.Request(AvailabilityAction));
        Assert.Equal((long)FrpIpcResult.Disabled, h.Session.Request(OpenAction));
        Assert.Equal((long)FrpIpcResult.Disabled, h.Session.Request(StatusAction));
        Assert.Empty(h.Writes);
        Assert.Empty(h.Opened);
    }

    [Fact]
    public async Task OptInRequiresFrameSnapshotAndCallbacksNeverCaptureOrDispatch()
    {
        await using var h = new Harness();
        h.Session.SetOptions(AllOptions);
        Assert.Equal(15, h.Session.GetCapabilities());
        Assert.Equal((int)FrpIpcReadiness.Blocked, h.Session.GetReadiness());
        Assert.Equal((long)FrpIpcResult.Blocked, h.Session.Request(StatusAction));
        Assert.Equal(0, h.Captures);
        h.Session.Refresh();
        var captures = h.Captures;
        Assert.Equal((int)FrpIpcReadiness.Ready, h.Session.GetReadiness());
        Assert.Equal((0, (int)RpStatus.Ic, (int)RpAvailability.Open), h.Session.GetOwnState());
        var ticket = h.Session.Request(StatusAction);
        Assert.True(ticket > 0);
        Assert.Equal((int)FrpIpcResult.Queued, h.Session.GetActionResult(ticket));
        Assert.Equal(captures, h.Captures);
        Assert.Empty(h.Writes);
        Assert.Empty(h.Opened);
    }

    [Fact]
    public async Task ControlsOnlyCannotExposeValuesAndReadOnlyCannotEnqueueActions()
    {
        await using var h = new Harness();
        h.Ready(new(false, true, true, false, false));
        Assert.Equal(2, h.Session.GetCapabilities());
        Assert.Equal(((int)FrpIpcResult.Disabled, -1, -1), h.Session.GetOwnState());
        Assert.Equal((long)FrpIpcResult.Disabled, h.Session.Request(AvailabilityAction));
        Assert.Equal((long)FrpIpcResult.Disabled, h.Session.Request(OpenAction));
        Assert.True(h.Session.Request(StatusAction) > 0);
        h.Session.SetOptions(new(true));
        h.Session.Refresh();
        Assert.Equal(1, h.Session.GetCapabilities());
        Assert.Equal((long)FrpIpcResult.Disabled, h.Session.Request(StatusAction));
        Assert.Equal((0, 0, 0), h.Session.GetOwnState());
        Assert.Empty(h.Writes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MatureStateNeverExportsValuesEvenWhenAdultControlsAreAllowed(bool adult, bool optIn)
    {
        await using var h = new Harness();
        h.Context = h.Context with { Active = h.Context.Active! with { Rating = ContentRating.Mature18 },
            Audience = new(adult ? AudienceSelection.Adult : AudienceSelection.Under18, optIn) };
        h.Ready();
        Assert.Equal(((int)FrpIpcResult.Blocked, -1, -1), h.Session.GetOwnState());
        var ticket = h.Session.Request(StatusAction);
        if (adult && optIn) Assert.True(ticket > 0);
        else Assert.Equal((long)FrpIpcResult.Blocked, ticket);
        h.Session.SetOptions(new(true));
        h.Session.Refresh();
        Assert.Equal((int)FrpIpcReadiness.Blocked, h.Session.GetReadiness());
        Assert.Equal(((int)FrpIpcResult.Blocked, -1, -1), h.Session.GetOwnState());
    }

    [Theory]
    [InlineData("dirty", FrpIpcResult.Busy)]
    [InlineData("busy", FrpIpcResult.Busy)]
    [InlineData("access", FrpIpcResult.Blocked)]
    [InlineData("audience-unspecified", FrpIpcResult.Blocked)]
    [InlineData("actor-invalid", FrpIpcResult.Blocked)]
    [InlineData("profile-null", FrpIpcResult.Blocked)]
    [InlineData("profile-id-empty", FrpIpcResult.Blocked)]
    [InlineData("revision-zero", FrpIpcResult.Blocked)]
    [InlineData("rating-invalid", FrpIpcResult.Blocked)]
    [InlineData("status-invalid", FrpIpcResult.Blocked)]
    [InlineData("availability-invalid", FrpIpcResult.Blocked)]
    public async Task UnsafeSnapshotsBlockReadValuesAndQueueAdmission(string change, FrpIpcResult result)
    {
        await using var h = new Harness();
        h.Context = Change(h.Context, change);
        h.Ready();
        Assert.Equal(((int)result, -1, -1), h.Session.GetOwnState());
        Assert.Equal((long)result, h.Session.Request(StatusAction));
        Assert.Empty(h.Writes);
    }

    [Theory]
    [InlineData(FrpIpcActionKind.SetStatus, -1)]
    [InlineData(FrpIpcActionKind.SetStatus, 3)]
    [InlineData(FrpIpcActionKind.SetAvailability, int.MaxValue)]
    [InlineData(FrpIpcActionKind.OpenOwnProfile, 1)]
    public async Task EnabledChannelsRejectInvalidArgumentsWithoutEnqueuing(FrpIpcActionKind kind, int value)
    {
        await using var h = new Harness();
        h.Ready();
        Assert.Equal((long)FrpIpcResult.InvalidArgument, h.Session.Request(new(kind, value)));
        h.Session.Tick();
        Assert.Empty(h.Writes);
        Assert.Empty(h.Opened);
    }

    [Fact]
    public async Task AdmissionIsQueuedThenRunningAndOnlyWriterCompletionCanBecomePersisted()
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        Assert.True(ticket > 0);
        Assert.Equal((int)FrpIpcResult.Queued, h.Session.GetActionResult(ticket));
        Assert.Equal(((int)FrpIpcResult.Busy, -1, -1), h.Session.GetOwnState());
        h.Session.Tick();
        var write = Assert.Single(h.Writes);
        Assert.Equal(h.Context.Active, write.Expected);
        Assert.Equal(StatusAction, write.Action);
        Assert.Equal((int)FrpIpcResult.Running, h.Session.GetActionResult(ticket));
        var saved = h.Saved(StatusAction);
        h.Context = h.Context with { Active = saved, CatalogVersion = h.Context.CatalogVersion + 1 };
        h.Session.Refresh();
        Assert.Equal((int)FrpIpcResult.Running, h.Session.GetActionResult(ticket));
        write.Source.SetResult(saved);
        Assert.Equal((int)FrpIpcResult.Running, h.Session.GetActionResult(ticket));
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Persisted, h.Session.GetActionResult(ticket));
        Assert.Equal((0, (int)RpStatus.Afk, (int)RpAvailability.Open), h.Session.GetOwnState());
        Assert.Single(h.Writes);
    }

    [Fact]
    public async Task QueueAndRunningDeduplicateIdenticalRequestsAndRejectConflictingActions()
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        Assert.Equal(ticket, h.Session.Request(StatusAction));
        Assert.Equal((long)FrpIpcResult.Busy, h.Session.Request(AvailabilityAction));
        h.Session.Tick();
        Assert.Equal(ticket, h.Session.Request(StatusAction));
        Assert.Equal((long)FrpIpcResult.Busy, h.Session.Request(OpenAction));
        Assert.Single(h.Writes);
        Assert.Empty(h.Opened);
    }

    [Fact]
    public async Task AlreadySatisfiedStillUsesExplicitQueueButDoesNotWriteAgain()
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(new(FrpIpcActionKind.SetStatus, (int)RpStatus.Ic));
        Assert.True(ticket > 0);
        Assert.Equal((int)FrpIpcResult.Queued, h.Session.GetActionResult(ticket));
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.AlreadySatisfied, h.Session.GetActionResult(ticket));
        Assert.Empty(h.Writes);
        Assert.Equal((long)FrpIpcResult.RateLimited, h.Session.Request(AvailabilityAction));
        h.Now = FrpIpcRules.ActionIntervalMilliseconds - 1;
        Assert.Equal((long)FrpIpcResult.RateLimited, h.Session.Request(AvailabilityAction));
        h.Now++;
        Assert.True(h.Session.Request(AvailabilityAction) > 0);
    }

    [Fact]
    public async Task OpenRunsOnlyFromExecutingFrameAndPreservesSnapshotDraftState()
    {
        await using var h = new Harness();
        h.Ready();
        var initial = h.Context;
        var ticket = h.Session.Request(OpenAction);
        Assert.True(ticket > 0);
        h.Session.Refresh();
        Assert.Empty(h.Opened);
        Assert.Empty(h.Writes);
        Assert.Equal((int)FrpIpcResult.Queued, h.Session.GetActionResult(ticket));
        h.Session.Tick();
        Assert.Equal(initial.Active, Assert.Single(h.Opened));
        Assert.Equal(initial, h.Context);
        Assert.Equal((int)FrpIpcResult.Opened, h.Session.GetActionResult(ticket));
        Assert.Empty(h.Writes);
        Assert.Equal((long)FrpIpcResult.RateLimited, h.Session.Request(OpenAction));
    }

    [Fact]
    public async Task ReadSnapshotRefreshesWhenActiveProfileRevisionAndCatalogChange()
    {
        await using var h = new Harness();
        h.Ready();
        h.Context = h.Context with { Active = h.Saved(AvailabilityAction), CatalogVersion = h.Context.CatalogVersion + 1 };
        Assert.Equal((0, 0, 0), h.Session.GetOwnState());
        h.Session.Refresh();
        Assert.Equal((0, 0, (int)RpAvailability.Unavailable), h.Session.GetOwnState());
        Assert.Empty(h.Writes);
    }

    public static IEnumerable<object[]> ScopeChanges => new[] { "cid", "object", "address", "name", "world",
        "territory", "map", "access", "audience", "policy", "catalog", "editor", "dirty", "busy", "profile-id",
        "revision", "hash", "rating", "status", "availability" }.Select(change => new object[] { change });

    [Theory]
    [MemberData(nameof(ScopeChanges))]
    public async Task QueuedWorkRechecksWholeContextBeforeDispatch(string change)
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        h.Context = Change(h.Context, change);
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Stale, h.Session.GetActionResult(ticket));
        Assert.Empty(h.Writes);
        Assert.Empty(h.Opened);
    }

    [Theory]
    [MemberData(nameof(ScopeChanges))]
    public async Task ImmediatePreexecutionCaptureCannotApplyToChangedContext(string change)
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        var captures = 0;
        h.BeforeCapture = () => { if (++captures == 2) h.Context = Change(h.Context, change); };
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Stale, h.Session.GetActionResult(ticket));
        Assert.Empty(h.Writes);
    }

    [Theory]
    [InlineData("policy-epoch")]
    [InlineData("read-permission")]
    [InlineData("control-permission")]
    [InlineData("feature-permission")]
    [InlineData("disable-reenable")]
    public async Task PermissionAndEpochChangesInvalidateQueuedWorkImmediately(string invalidation)
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        if (invalidation == "policy-epoch") h.Session.InvalidatePolicy();
        else if (invalidation == "read-permission") h.Session.SetOptions(AllOptions with { ReadEnabled = false });
        else if (invalidation == "control-permission") h.Session.SetOptions(AllOptions with { ControlEnabled = false });
        else if (invalidation == "feature-permission") h.Session.SetOptions(AllOptions with { SetStatusEnabled = false });
        else { h.Session.SetOptions(new()); h.Session.SetOptions(AllOptions); }
        Assert.Equal(((int)(invalidation == "read-permission" ? FrpIpcResult.Disabled : FrpIpcResult.Blocked), -1, -1),
            h.Session.GetOwnState());
        h.Session.Tick();
        Assert.Empty(h.Writes);
        if (invalidation != "control-permission")
            Assert.NotEqual((int)FrpIpcResult.Persisted, h.Session.GetActionResult(ticket));
        else Assert.Equal((int)FrpIpcResult.Disabled, h.Session.GetActionResult(ticket));
    }

    [Fact]
    public async Task PolicyEpochInvalidationPreventsReadImmediatelyBeforeNextFrame()
    {
        await using var h = new Harness();
        h.Ready();
        var captures = h.Captures;
        h.Session.InvalidatePolicy();
        Assert.Equal(((int)FrpIpcResult.Blocked, -1, -1), h.Session.GetOwnState());
        Assert.Equal((long)FrpIpcResult.Blocked, h.Session.Request(StatusAction));
        Assert.Equal(captures, h.Captures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PolicyRevocationAlsoInvalidatesPreviouslyCompletedActionReceipts(bool persisted)
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(persisted ? StatusAction : OpenAction);
        h.Session.Tick();
        if (persisted)
        {
            var saved = h.Saved(StatusAction);
            h.Context = h.Context with { Active = saved };
            Assert.Single(h.Writes).Source.SetResult(saved);
            h.Session.Tick();
        }
        Assert.Equal((int)(persisted ? FrpIpcResult.Persisted : FrpIpcResult.Opened), h.Session.GetActionResult(ticket));
        var captures = h.Captures;
        h.Session.InvalidatePolicy();
        Assert.Equal((int)FrpIpcResult.Stale, h.Session.GetActionResult(ticket));
        Assert.Equal(captures, h.Captures);
        h.Session.Refresh();
        Assert.Equal((int)FrpIpcResult.Stale, h.Session.GetActionResult(ticket));
    }

    [Fact]
    public async Task ExplicitPermissionChangeClearsPreviousCompletionReceipt()
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(OpenAction);
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Opened, h.Session.GetActionResult(ticket));
        h.Session.SetOptions(AllOptions with { ReadEnabled = false });
        Assert.Equal((int)FrpIpcResult.Unknown, h.Session.GetActionResult(ticket));
        h.Session.Refresh();
        Assert.Equal((int)FrpIpcResult.Unknown, h.Session.GetActionResult(ticket));
    }

    [Theory]
    [InlineData("cid")]
    [InlineData("policy")]
    [InlineData("profile-id")]
    public async Task LaterActorPolicyOrActiveProfileScopeCannotReuseAnOldCompletion(string change)
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(OpenAction);
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Opened, h.Session.GetActionResult(ticket));
        h.Context = Change(h.Context, change);
        h.Session.Refresh();
        Assert.Equal((int)FrpIpcResult.Stale, h.Session.GetActionResult(ticket));
    }

    [Theory]
    [InlineData("policy-epoch")]
    [InlineData("control-permission")]
    [InlineData("feature-permission")]
    public async Task RevokingPermissionDuringWriteCancelsAndCannotBecomePersisted(string invalidation)
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        h.Session.Tick();
        var write = Assert.Single(h.Writes);
        var saved = h.Saved(StatusAction);
        if (invalidation == "policy-epoch") h.Session.InvalidatePolicy();
        else if (invalidation == "control-permission") h.Session.SetOptions(AllOptions with { ControlEnabled = false });
        else h.Session.SetOptions(AllOptions with { SetStatusEnabled = false });
        h.Session.Tick();
        Assert.True(write.Token.IsCancellationRequested);
        h.Context = h.Context with { Active = saved };
        write.Source.SetResult(saved);
        h.Session.Tick();
        Assert.Equal((int)(invalidation == "control-permission" ? FrpIpcResult.Disabled : FrpIpcResult.Stale),
            h.Session.GetActionResult(ticket));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedExplicitOpenCannotReportOpened(bool throws)
    {
        await using var h = new Harness();
        h.Ready();
        h.OpenResult = false;
        h.ThrowOpen = throws;
        var ticket = h.Session.Request(OpenAction);
        h.Session.Tick();
        Assert.Single(h.Opened);
        Assert.Equal((int)(throws ? FrpIpcResult.Error : FrpIpcResult.Stale), h.Session.GetActionResult(ticket));
        Assert.Empty(h.Writes);
    }

    [Theory]
    [InlineData("cid")]
    [InlineData("profile-id")]
    [InlineData("policy")]
    [InlineData("dirty")]
    public async Task LateWriterSuccessAfterScopeChangeCannotReportPersistenceForCurrentActor(string change)
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        h.Session.Tick();
        var write = Assert.Single(h.Writes);
        var saved = h.Saved(StatusAction);
        h.Context = Change(h.Context, change);
        h.Session.Refresh();
        Assert.True(write.Token.IsCancellationRequested);
        Assert.Equal((int)FrpIpcResult.Stale, h.Session.GetActionResult(ticket));
        write.Source.SetResult(saved);
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Stale, h.Session.GetActionResult(ticket));
        Assert.Single(h.Writes);
    }

    [Theory]
    [InlineData("fault")]
    [InlineData("cancel")]
    [InlineData("dispatch-throw")]
    public async Task WriterFailureOrCancellationNeverReportsPersisted(string failure)
    {
        await using var h = new Harness();
        h.Ready();
        h.ThrowWrite = failure == "dispatch-throw";
        var ticket = h.Session.Request(StatusAction);
        h.Session.Tick();
        var write = Assert.Single(h.Writes);
        if (failure == "fault") write.Source.SetException(new IOException("Synthetic writer failure"));
        if (failure == "cancel") write.Source.SetCanceled();
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Error, h.Session.GetActionResult(ticket));
        Assert.Equal(RpStatus.Ic, h.Context.Active!.Status);
    }

    [Fact]
    public async Task CompletedWriterWithConflictingIdleActiveHashIsStale()
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        h.Session.Tick();
        var saved = h.Saved(StatusAction);
        h.Context = h.Context with { Active = h.Context.Active! with { Hash = "conflicting synthetic hash" } };
        Assert.Single(h.Writes).Source.SetResult(saved);
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Stale, h.Session.GetActionResult(ticket));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SuccessfulWriterWaitsForOwnProfilePollBeforeClaimingPersistence(bool savedAlreadyVisible, bool refreshOnly)
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        h.Session.Tick();
        var saved = h.Saved(StatusAction);
        h.Context = h.Context with { Busy = true, Active = savedAlreadyVisible ? saved : h.Context.Active };
        Assert.Single(h.Writes).Source.SetResult(saved);
        if (refreshOnly) h.Session.Refresh();
        else h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Running, h.Session.GetActionResult(ticket));
        Assert.Equal(((int)FrpIpcResult.Busy, -1, -1), h.Session.GetOwnState());
        Assert.Single(h.Writes);
        h.Context = h.Context with { Busy = false, Active = saved, CatalogVersion = h.Context.CatalogVersion + 1 };
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Persisted, h.Session.GetActionResult(ticket));
        Assert.Equal((0, (int)RpStatus.Afk, (int)RpAvailability.Open), h.Session.GetOwnState());
    }

    [Theory]
    [InlineData("cid")]
    [InlineData("policy")]
    [InlineData("profile-id")]
    [InlineData("dirty")]
    [InlineData("hash")]
    public async Task CompletedWriterWaitingForOwnPollCannotIgnoreInvalidatedContext(string change)
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        h.Session.Tick();
        var saved = h.Saved(StatusAction);
        var write = Assert.Single(h.Writes);
        h.Context = Change(h.Context with { Busy = true }, change);
        write.Source.SetResult(saved);
        h.Session.Refresh();
        Assert.Equal((int)FrpIpcResult.Stale, h.Session.GetActionResult(ticket));
        Assert.Single(h.Writes);
        h.Context = h.Context with { Busy = false, Dirty = false, Active = saved };
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Stale, h.Session.GetActionResult(ticket));
    }

    [Fact]
    public async Task CompletionCanObserveTheWritersAutomaticCleanEditorRefresh()
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        h.Session.Tick();
        var saved = h.Saved(StatusAction);
        h.Context = h.Context with { Active = saved, CatalogVersion = h.Context.CatalogVersion + 1,
            EditorGeneration = h.Context.EditorGeneration + 1 };
        Assert.Single(h.Writes).Source.SetResult(saved);
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Persisted, h.Session.GetActionResult(ticket));
    }

    [Fact]
    public async Task ExpiredAndSupersededTicketsCannotBeConfusedWithCurrentSuccess()
    {
        await using var h = new Harness();
        h.Ready();
        Assert.Equal((int)FrpIpcResult.InvalidArgument, h.Session.GetActionResult(0));
        Assert.Equal((int)FrpIpcResult.InvalidArgument, h.Session.GetActionResult(-1));
        Assert.Equal((int)FrpIpcResult.Unknown, h.Session.GetActionResult(long.MaxValue));
        var first = h.Session.Request(OpenAction);
        h.Session.Tick();
        h.Now = FrpIpcRules.CompletionLifetimeMilliseconds - 1;
        Assert.Equal((int)FrpIpcResult.Opened, h.Session.GetActionResult(first));
        h.Now++;
        Assert.Equal((int)FrpIpcResult.Unknown, h.Session.GetActionResult(first));
        var second = h.Session.Request(OpenAction);
        Assert.True(second > 0);
        Assert.NotEqual(first, second);
        h.Session.Tick();
        Assert.Equal((int)FrpIpcResult.Unknown, h.Session.GetActionResult(first));
        Assert.Equal((int)FrpIpcResult.Opened, h.Session.GetActionResult(second));
    }

    [Fact]
    public async Task ReloadedSessionNeverReusesAnOldPositiveTicket()
    {
        await using var first = new Harness();
        first.Ready();
        var oldTicket = first.Session.Request(OpenAction);
        first.Session.Tick();
        first.Session.Dispose();
        await using var second = new Harness();
        second.Ready();
        var newTicket = second.Session.Request(OpenAction);
        second.Session.Tick();
        Assert.True(newTicket > 0);
        Assert.NotEqual(oldTicket, newTicket);
        Assert.Equal((int)FrpIpcResult.Unknown, second.Session.GetActionResult(oldTicket));
        Assert.Equal((int)FrpIpcResult.Disabled, first.Session.GetActionResult(oldTicket));
    }

    [Fact]
    public async Task ConcurrentCallbacksShareOneBoundedQueueAndNeverRunFrameDependencies()
    {
        await using var h = new Harness();
        h.Ready();
        var captures = h.Captures;
        var accepted = await Task.WhenAll(Enumerable.Range(0, 64).Select(_ =>
            Task.Run(() => h.Session.Request(StatusAction))));
        var ticket = Assert.Single(accepted.Distinct());
        Assert.True(ticket > 0);
        var conflicts = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
            Task.Run(() => h.Session.Request(AvailabilityAction))));
        Assert.All(conflicts, result => Assert.Equal((long)FrpIpcResult.Busy, result));
        Assert.Equal(captures, h.Captures);
        Assert.Empty(h.Writes);
        Assert.Empty(h.Opened);
        h.Session.Tick();
        Assert.Single(h.Writes);
    }

    [Fact]
    public async Task CaptureExceptionsFailClosedIncludingTheImmediateExecutionRecheck()
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        var captures = 0;
        h.BeforeCapture = () =>
        {
            if (++captures == 2) throw new InvalidOperationException("Synthetic context failure");
        };
        h.Session.Tick();
        Assert.Empty(h.Writes);
        Assert.NotEqual((int)FrpIpcResult.Persisted, h.Session.GetActionResult(ticket));
        Assert.NotEqual((int)FrpIpcReadiness.Ready, h.Session.GetReadiness());
    }

    [Fact]
    public async Task DisposeCancelsPendingWriterAndObservesLateFailureWithoutRevivingState()
    {
        await using var h = new Harness();
        h.Ready();
        var ticket = h.Session.Request(StatusAction);
        h.Session.Tick();
        var write = Assert.Single(h.Writes);
        h.Session.Dispose();
        Assert.True(write.Token.IsCancellationRequested);
        Assert.False(h.Session.DisposalCompletion.IsCompleted);
        write.Source.SetException(new IOException("Synthetic late failure"));
        await h.Session.DisposalCompletion;
        h.Session.Tick();
        h.Session.Refresh();
        Assert.Equal(0, h.Session.GetCapabilities());
        Assert.Equal(((int)FrpIpcResult.Disabled, -1, -1), h.Session.GetOwnState());
        Assert.Equal((int)FrpIpcResult.Disabled, h.Session.GetActionResult(ticket));
        Assert.Single(h.Writes);
    }

    private static FrpIpcContext Change(FrpIpcContext c, string change) => change switch
    {
        "cid" => c with { Actor = c.Actor with { ContentId = 99 } },
        "object" => c with { Actor = c.Actor with { ObjectId = 99 } },
        "address" => c with { Actor = c.Actor with { Address = 99 } },
        "name" => c with { Actor = c.Actor with { Name = "Other Synthetic Actor" } },
        "world" => c with { Actor = c.Actor with { HomeWorldId = 99 } },
        "territory" => c with { Actor = c.Actor with { TerritoryId = 99 } },
        "map" => c with { Actor = c.Actor with { MapId = 99 } },
        "actor-invalid" => c with { Actor = c.Actor with { ContentId = 0 } },
        "access" => c with { AccessReady = false },
        "audience" => c with { Audience = new(AudienceSelection.Under18) },
        "audience-unspecified" => c with { Audience = new() },
        "policy" => c with { PolicyGeneration = c.PolicyGeneration + 1 },
        "catalog" => c with { CatalogVersion = c.CatalogVersion + 1 },
        "editor" => c with { EditorGeneration = c.EditorGeneration + 1 },
        "dirty" => c with { Dirty = true },
        "busy" => c with { Busy = true },
        "profile-null" => c with { Active = null },
        "profile-id-empty" => c with { Active = c.Active! with { Id = Guid.Empty } },
        "revision-zero" => c with { Active = c.Active! with { Revision = 0 } },
        "rating-invalid" => c with { Active = c.Active! with { Rating = (ContentRating)99 } },
        "status-invalid" => c with { Active = c.Active! with { Status = (RpStatus)99 } },
        "availability-invalid" => c with { Active = c.Active! with { Availability = (RpAvailability)99 } },
        "profile-id" => c with { Active = c.Active! with { Id = Guid.NewGuid() } },
        "revision" => c with { Active = c.Active! with { Revision = c.Active.Revision + 1 } },
        "hash" => c with { Active = c.Active! with { Hash = "changed synthetic hash" } },
        "rating" => c with { Active = c.Active! with { Rating = ContentRating.Mature18 } },
        "status" => c with { Active = c.Active! with { Status = RpStatus.Ooc } },
        "availability" => c with { Active = c.Active! with { Availability = RpAvailability.Busy } },
        _ => throw new ArgumentOutOfRangeException(nameof(change)),
    };

    private sealed record WriteRequest(FrpIpcAction Action, FrpIpcProfile Expected, CancellationToken Token,
        TaskCompletionSource<FrpIpcProfile> Source);

    private sealed class Harness : IAsyncDisposable
    {
        public FrpIpcContext Context { get; set; } = new(new(1, 2, 3, "Synthetic Actor", 4, 5, 6), true,
            new(AudienceSelection.Adult), 1, 1, 1, false, false,
            new(Guid.NewGuid(), 1, "synthetic hash", ContentRating.General, RpStatus.Ic, RpAvailability.Open));
        public FrpIpcSession Session { get; }
        public long Now { get; set; }
        public int Captures { get; private set; }
        public Action? BeforeCapture { get; set; }
        public bool ThrowWrite { get; set; }
        public bool ThrowOpen { get; set; }
        public bool OpenResult { get; set; } = true;
        public List<WriteRequest> Writes { get; } = [];
        public List<FrpIpcProfile> Opened { get; } = [];

        public Harness()
        {
            Session = new(() => { Captures++; BeforeCapture?.Invoke(); return Context; },
                (action, expected, token) =>
                {
                    var source = new TaskCompletionSource<FrpIpcProfile>(TaskCreationOptions.RunContinuationsAsynchronously);
                    Writes.Add(new(action, expected, token, source));
                    if (ThrowWrite) throw new InvalidOperationException("Synthetic dispatch failure");
                    return source.Task;
                },
                profile =>
                {
                    Opened.Add(profile);
                    if (ThrowOpen) throw new InvalidOperationException("Synthetic open failure");
                    return OpenResult;
                }, () => Now);
        }

        public void Ready(IntegrationIpcOptions? options = null)
        {
            Session.SetOptions(options ?? AllOptions);
            Session.Refresh();
        }

        public FrpIpcProfile Saved(FrpIpcAction action)
        {
            var active = Context.Active!;
            return active with { Revision = active.Revision + 1, Hash = "saved synthetic hash",
                Status = action.Kind == FrpIpcActionKind.SetStatus ? (RpStatus)action.Value : active.Status,
                Availability = action.Kind == FrpIpcActionKind.SetAvailability ? (RpAvailability)action.Value : active.Availability };
        }

        public async ValueTask DisposeAsync()
        {
            Session.Dispose();
            foreach (var request in Writes) request.Source.TrySetCanceled();
            await Session.DisposalCompletion;
        }
    }
}
