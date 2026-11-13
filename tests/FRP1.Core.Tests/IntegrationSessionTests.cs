using FRP1.Core.Safety;
using FRP1.Plugin.Integrations;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class IntegrationSessionTests
{
    [Fact]
    public async Task DefaultOffNeverReadsImportsOrWritesAndPreloadChoiceCannotOverrideDisk()
    {
        await using var h = new Harness();
        h.Session.SetHonorificEnabled(true);
        await h.LoadAsync();
        h.Session.RequestRead();
        Assert.Equal(IntegrationActionResult.Disabled, h.Session.RequestImport());
        for (var i = 0; i < 20; i++) h.Session.Tick();
        Assert.False(h.Session.Settings.HonorificEnabled);
        Assert.Equal(IntegrationStatus.Disabled, h.Session.Status);
        Assert.Equal(0, h.Reader.Reads);
        Assert.Equal(0, h.Captures);
        Assert.Empty(h.Applied);
        Assert.Empty(Directory.EnumerateFiles(h.Directory.Path));
    }

    [Theory]
    [InlineData(IntegrationStatus.Missing)]
    [InlineData(IntegrationStatus.Incompatible)]
    [InlineData(IntegrationStatus.Error)]
    [InlineData(IntegrationStatus.Busy)]
    public async Task UnavailableReaderNeverConfirmsImportOrNativeYield(IntegrationStatus status)
    {
        await using var h = new Harness();
        h.Reader.Result = new(status);
        await h.EnableAsync();
        Assert.Equal(status, h.Session.Status);
        Assert.Empty(h.Session.Title);
        Assert.Equal(IntegrationActionResult.NotReady, h.Session.RequestImport());
        Assert.False(h.Yields());
        Assert.Empty(h.Applied);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"Title\":null}")]
    [InlineData("{\"Title\":\"\"}")]
    [InlineData("{\"Title\":\"   \"}")]
    public async Task ReadyWithoutUsableTitleYieldsOwnSlotButCannotImport(string json)
    {
        await using var h = new Harness();
        h.Reader.Result = new(IntegrationStatus.Ready, json);
        await h.EnableAsync();
        Assert.Equal(IntegrationStatus.Ready, h.Session.Status);
        Assert.True(h.Yields());
        Assert.Equal(IntegrationActionResult.NotReady, h.Session.RequestImport());
        Assert.Empty(h.Applied);
    }

    [Fact]
    public async Task MalformedReadAndThrownReadFailClosedAndCanRecoverOnNextExplicitRead()
    {
        await using var h = new Harness();
        h.Reader.Result = new(IntegrationStatus.Ready, "{\"Title\":7}");
        await h.EnableAsync();
        Assert.Equal(IntegrationStatus.Error, h.Session.Status);
        Assert.False(h.Yields());
        h.Reader.DuringRead = () => throw new InvalidOperationException("Synthetic reader failure");
        h.Now += IntegrationSession.ReadIntervalMilliseconds;
        h.Session.RequestRead();
        h.Session.Tick();
        Assert.Equal(IntegrationStatus.Error, h.Session.Status);
        Assert.Empty(h.Session.Title);
        h.Reader.DuringRead = null;
        h.Reader.Result = new(IntegrationStatus.Ready, "{\"Title\":\"Recovered\"}");
        h.Now += IntegrationSession.ReadIntervalMilliseconds;
        h.Session.RequestRead();
        h.Session.Tick();
        Assert.Equal("Recovered", h.Session.Title);
        Assert.True(h.Yields());
        Assert.Empty(h.Applied);
    }

    [Fact]
    public async Task ReadsNeverAutoApplyAndDuplicateQueuedImportAppliesExactlyOnceWithDraftToken()
    {
        await using var h = new Harness();
        await h.EnableAsync();
        Assert.Equal("Synthetic title", h.Session.Title);
        Assert.Empty(h.Applied);
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport());
        Assert.Equal(IntegrationActionResult.AlreadyPending, h.Session.RequestImport());
        Assert.True(h.Session.HasPendingImport);
        Assert.Empty(h.Applied);
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Applied, h.Session.LastAction);
        Assert.False(h.Session.HasPendingImport);
        var applied = Assert.Single(h.Applied);
        Assert.Equal(h.Context.Draft, applied.Token);
        Assert.Equal("Synthetic title", applied.Title);
        h.Session.Tick();
        Assert.Single(h.Applied);
        Assert.Equal(IntegrationActionResult.RateLimited, h.Session.RequestImport());
        h.Now += IntegrationSession.ImportIntervalMilliseconds;
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport());
    }

    [Fact]
    public async Task BurstsCoalesceToBoundedFrameReadsWithoutPeriodicPolling()
    {
        await using var h = new Harness();
        await h.EnableAsync();
        Assert.Equal(1, h.Reader.Reads);
        for (var i = 0; i < 1000; i++) { h.Reader.Version++; h.Session.RequestRead(); }
        Assert.Equal(1, h.Reader.Reads);
        h.Session.Tick();
        Assert.Equal(1, h.Reader.Reads);
        Assert.Empty(h.Session.Title);
        Assert.False(h.Yields());
        h.Now = IntegrationSession.ReadIntervalMilliseconds - 1;
        h.Session.Tick();
        Assert.Equal(1, h.Reader.Reads);
        h.Now++;
        h.Session.Tick();
        Assert.Equal(2, h.Reader.Reads);
        h.Now += 10 * IntegrationSession.ReadIntervalMilliseconds;
        for (var i = 0; i < 100; i++) h.Session.Tick();
        Assert.Equal(2, h.Reader.Reads);
        Assert.Empty(h.Applied);
    }

    [Theory]
    [InlineData("dirty")]
    [InlineData("busy")]
    [InlineData("draft-null")]
    [InlineData("draft-id-empty")]
    [InlineData("draft-revision-zero")]
    [InlineData("access")]
    [InlineData("audience-unspecified")]
    public async Task UnsafeCurrentDraftOrPolicyBlocksExplicitImport(string change)
    {
        await using var h = new Harness();
        await h.EnableAsync();
        h.Context = Change(h.Context, change);
        Assert.Equal(IntegrationActionResult.Blocked, h.Session.RequestImport());
        h.Session.Tick();
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
    [InlineData("audience")]
    public async Task IncompleteLocalIdentityOrUnselectedPolicyPreventsAnyReaderCall(string invalid)
    {
        await using var h = new Harness();
        await h.LoadAsync();
        var actor = h.Context.Actor;
        h.Context = invalid switch
        {
            "cid" => h.Context with { Actor = actor with { ContentId = 0 } },
            "object" => h.Context with { Actor = actor with { ObjectId = 0 } },
            "address" => h.Context with { Actor = actor with { Address = 0 } },
            "name" => h.Context with { Actor = actor with { Name = " " } },
            "world" => h.Context with { Actor = actor with { HomeWorldId = 0 } },
            "territory" => h.Context with { Actor = actor with { TerritoryId = 0 } },
            "map" => h.Context with { Actor = actor with { MapId = 0 } },
            "access" => h.Context with { AccessReady = false },
            _ => h.Context with { Audience = new() },
        };
        h.Session.SetHonorificEnabled(true);
        h.Session.RequestRead();
        h.Session.Tick();
        Assert.Equal(0, h.Reader.Reads);
        Assert.Equal(IntegrationStatus.Blocked, h.Session.Status);
        Assert.Equal(IntegrationActionResult.Blocked, h.Session.RequestImport());
        Assert.False(h.Yields());
    }

    public static IEnumerable<object[]> ContextChanges => new[] { "cid", "object", "address", "name", "world",
        "territory", "map", "access", "audience", "policy", "draft-id", "draft-revision", "draft-hash",
        "editor-generation", "dirty", "busy" }.Select(change => new object[] { change });

    [Theory]
    [MemberData(nameof(ContextChanges))]
    public async Task ContextChangeDuringReaderCallDiscardsResult(string change)
    {
        await using var h = new Harness();
        await h.LoadAsync();
        h.Reader.DuringRead = () => h.Context = Change(h.Context, change);
        h.Session.SetHonorificEnabled(true);
        var generation = h.Session.Generation;
        h.Session.Tick();
        Assert.Equal(1, h.Reader.Reads);
        Assert.True(h.Session.Generation > generation);
        Assert.Empty(h.Session.Title);
        Assert.NotEqual(IntegrationStatus.Ready, h.Session.Status);
        Assert.False(h.Yields());
        Assert.Empty(h.Applied);
    }

    [Theory]
    [MemberData(nameof(ContextChanges))]
    public async Task QueuedImportRechecksEntireContextBeforeApplying(string change)
    {
        await using var h = new Harness();
        await h.EnableAsync();
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport());
        h.Context = Change(h.Context, change);
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Stale, h.Session.LastAction);
        Assert.False(h.Session.HasPendingImport);
        Assert.Empty(h.Applied);
    }

    [Theory]
    [MemberData(nameof(ContextChanges))]
    public async Task ContextChangingBetweenTickObservationAndImmediatePreapplyIsRejected(string change)
    {
        await using var h = new Harness();
        await h.EnableAsync();
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport());
        var captures = 0;
        h.BeforeCapture = () => { if (++captures == 2) h.Context = Change(h.Context, change); };
        h.Session.Tick();
        Assert.True(captures >= 2);
        Assert.Equal(IntegrationActionResult.Stale, h.Session.LastAction);
        Assert.Empty(h.Applied);
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("disable-reenable")]
    [InlineData("dispose")]
    [InlineData("reader-version")]
    public async Task ReentrantInvalidationDuringReadCannotPublishOldResult(string invalidation)
    {
        await using var h = new Harness();
        await h.LoadAsync();
        h.Reader.DuringRead = () =>
        {
            if (invalidation is "disable" or "disable-reenable")
            {
                h.Session.SetHonorificEnabled(false);
                if (invalidation == "disable-reenable") h.Session.SetHonorificEnabled(true);
            }
            else if (invalidation == "dispose") h.Session.Dispose();
            else h.Reader.Version++;
        };
        h.Session.SetHonorificEnabled(true);
        h.Session.Tick();
        Assert.Empty(h.Session.Title);
        Assert.NotEqual(IntegrationStatus.Ready, h.Session.Status);
        Assert.False(h.Yields());
        Assert.Empty(h.Applied);
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("disable-reenable")]
    [InlineData("dispose")]
    [InlineData("reader-version")]
    public async Task QueuedImportCannotSurviveInvalidation(string invalidation)
    {
        await using var h = new Harness();
        await h.EnableAsync();
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport());
        if (invalidation is "disable" or "disable-reenable")
        {
            h.Session.SetHonorificEnabled(false);
            if (invalidation == "disable-reenable") h.Session.SetHonorificEnabled(true);
        }
        else if (invalidation == "dispose") h.Session.Dispose();
        else h.Reader.Version++;
        h.Session.Tick();
        Assert.Empty(h.Applied);
        Assert.Equal(IntegrationActionResult.Stale, h.Session.LastAction);
        Assert.False(h.Session.HasPendingImport);
        Assert.False(h.Yields());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplyCasFailureOrExceptionCannotReportSuccess(bool throws)
    {
        await using var h = new Harness();
        await h.EnableAsync();
        h.ApplyResult = false;
        h.ThrowApply = throws;
        Assert.Equal(IntegrationActionResult.Queued, h.Session.RequestImport());
        h.Session.Tick();
        Assert.Equal(throws ? IntegrationActionResult.Blocked : IntegrationActionResult.Stale, h.Session.LastAction);
        Assert.Single(h.Applied);
        h.Session.Tick();
        Assert.Single(h.Applied);
    }

    [Fact]
    public async Task NativeYieldRequiresLiveReaderVersionAccessAudiencePolicyAndExactOwnSlot()
    {
        await using var h = new Harness();
        await h.EnableAsync();
        var context = h.Context;
        var actor = context.Actor;
        Assert.True(h.Yields());
        Assert.False(h.Session.CanYieldOwnTitle(actor, context.Audience, context.PolicyGeneration, false, actor.ObjectId, actor.Address));
        Assert.False(h.Session.CanYieldOwnTitle(actor, new(AudienceSelection.Under18), context.PolicyGeneration, true, actor.ObjectId, actor.Address));
        Assert.False(h.Session.CanYieldOwnTitle(actor, context.Audience, context.PolicyGeneration + 1, true, actor.ObjectId, actor.Address));
        Assert.False(h.Session.CanYieldOwnTitle(actor, context.Audience, context.PolicyGeneration, true, actor.ObjectId + 1, actor.Address));
        Assert.False(h.Session.CanYieldOwnTitle(actor, context.Audience, context.PolicyGeneration, true, actor.ObjectId, actor.Address + 1));
        Assert.False(h.Session.CanYieldOwnTitle(actor with { ContentId = 99 }, context.Audience, context.PolicyGeneration, true, actor.ObjectId, actor.Address));
        h.Reader.Version++;
        Assert.False(h.Yields());
        h.Session.SetHonorificEnabled(false);
        Assert.False(h.Yields());
    }

    [Fact]
    public async Task InvalidLocalLoadFallsBackOffWithoutOverwritingUnreadSettings()
    {
        await using var h = new Harness("invalid local json");
        await h.LoadAsync();
        Assert.True(h.Session.LoadFailed);
        Assert.False(h.Session.Settings.HonorificEnabled);
        Assert.Equal(0, h.Reader.Reads);
        Assert.Equal("invalid local json", await File.ReadAllTextAsync(h.SettingsPath));
    }

    [Fact]
    public async Task PersistedOptInLoadsBeforeReadingAndLatestRapidChoiceWinsOnDisk()
    {
        await using var h = new Harness("{\"schemaVersion\":1,\"honorificEnabled\":true}");
        Assert.Equal(0, h.Reader.Reads);
        await h.LoadAsync();
        Assert.Equal(1, h.Reader.Reads);
        h.Session.SetHonorificEnabled(false);
        h.Session.Tick();
        h.Session.SetHonorificEnabled(true);
        h.Session.SetHonorificEnabled(false);
        h.Session.SetHonorificEnabled(true);
        // A distinct final value proves the save queue persisted its newest snapshot.
        h.Session.SetHonorificEnabled(false);
        await h.PumpUntilAsync(async () =>
        {
            if (!File.Exists(h.SettingsPath)) return false;
            try { return !(await new IntegrationSettingsStore(h.Directory.Path).LoadAsync()).HonorificEnabled; }
            catch (IOException) { return false; }
        });
        h.Session.Dispose();
        await h.Session.DisposalCompletion;
        Assert.False((await new IntegrationSettingsStore(h.Directory.Path).LoadAsync()).HonorificEnabled);
    }

    [Fact]
    public async Task FailedSaveDoesNotRollbackChoiceAndLaterExplicitChangeRecovers()
    {
        await using var h = new Harness();
        await h.LoadAsync();
        Directory.CreateDirectory(h.SettingsPath);
        h.Session.SetHonorificEnabled(true);
        await h.PumpUntilAsync(() => Task.FromResult(h.Session.SaveFailed));
        Assert.True(h.Session.Settings.HonorificEnabled);
        Directory.Delete(h.SettingsPath);
        h.Session.SetHonorificEnabled(false);
        await h.PumpUntilAsync(async () => File.Exists(h.SettingsPath) &&
            !(await new IntegrationSettingsStore(h.Directory.Path).LoadAsync()).HonorificEnabled && !h.Session.SaveFailed);
        Assert.False(h.Session.SaveFailed);
    }

    [Fact]
    public async Task DisposeIsIdempotentAndPreventsFurtherReaderOrApplyCalls()
    {
        await using var h = new Harness();
        await h.EnableAsync();
        var reads = h.Reader.Reads;
        h.Session.Dispose();
        h.Session.Dispose();
        await h.Session.DisposalCompletion;
        h.Session.RequestRead();
        h.Session.SetHonorificEnabled(true);
        h.Now += IntegrationSession.ReadIntervalMilliseconds;
        h.Session.Tick();
        Assert.Equal(IntegrationActionResult.Disabled, h.Session.RequestImport());
        Assert.Equal(reads, h.Reader.Reads);
        Assert.Equal(1, h.Reader.Disposals);
        Assert.Empty(h.Applied);
    }

    private static IntegrationContext Change(IntegrationContext c, string change) => change switch
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
        "audience-unspecified" => c with { Audience = new() },
        "policy" => c with { PolicyGeneration = c.PolicyGeneration + 1 },
        "draft-null" => c with { Draft = null },
        "draft-id-empty" => c with { Draft = c.Draft! with { ProfileId = Guid.Empty } },
        "draft-revision-zero" => c with { Draft = c.Draft! with { Revision = 0 } },
        "draft-id" => c with { Draft = c.Draft! with { ProfileId = Guid.NewGuid() } },
        "draft-revision" => c with { Draft = c.Draft! with { Revision = c.Draft.Revision + 1 } },
        "draft-hash" => c with { Draft = c.Draft! with { Hash = "changed synthetic hash" } },
        "editor-generation" => c with { Draft = c.Draft! with { EditorGeneration = c.Draft.EditorGeneration + 1 } },
        "dirty" => c with { Dirty = true },
        "busy" => c with { Busy = true },
        _ => throw new ArgumentOutOfRangeException(nameof(change)),
    };

    private sealed class FakeReader : IHonorificReader
    {
        public long Version { get; set; }
        public int Reads { get; private set; }
        public int Disposals { get; private set; }
        public Action? DuringRead { get; set; }
        public HonorificReadResult Result { get; set; } = new(IntegrationStatus.Ready, "{\"Title\":\"Synthetic title\"}");
        public HonorificReadResult Read() { Reads++; DuringRead?.Invoke(); return Result; }
        public void Dispose() => Disposals++;
    }

    private sealed class Harness : IAsyncDisposable
    {
        public TestDirectory Directory { get; } = new();
        public FakeReader Reader { get; } = new();
        public IntegrationContext Context { get; set; } = new(new(1, 2, 3, "Synthetic Actor", 4, 5, 6),
            true, new(AudienceSelection.Adult), 1, new(Guid.NewGuid(), 1, "synthetic hash", 1), false, false);
        public IntegrationSession Session { get; }
        public long Now { get; set; }
        public bool ApplyResult { get; set; } = true;
        public bool ThrowApply { get; set; }
        public Action? BeforeCapture { get; set; }
        public int Captures { get; private set; }
        public List<(IntegrationDraftToken Token, string Title)> Applied { get; } = [];
        public string SettingsPath => Path.Combine(Directory.Path, "integrations.json");

        public Harness(string? initialJson = null)
        {
            if (initialJson is not null) File.WriteAllText(SettingsPath, initialJson);
            Session = new(new(Directory.Path), Reader,
                () => { Captures++; BeforeCapture?.Invoke(); return Context; },
                (token, title) =>
                {
                    Applied.Add((token, title));
                    if (ThrowApply) throw new InvalidOperationException("Synthetic CAS failure");
                    return ApplyResult;
                }, () => Now);
        }

        public Task LoadAsync() => PumpUntilAsync(() => Task.FromResult(Session.SettingsLoaded));
        public async Task EnableAsync()
        {
            await LoadAsync();
            Session.SetHonorificEnabled(true);
            Session.Tick();
        }

        public bool Yields() => Session.CanYieldOwnTitle(Context.Actor, Context.Audience, Context.PolicyGeneration,
            Context.AccessReady, Context.Actor.ObjectId, Context.Actor.Address);

        public async Task PumpUntilAsync(Func<Task<bool>> done)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            do
            {
                Session.Tick();
                if (await done()) return;
                await Task.Delay(5, timeout.Token);
            } while (true);
        }

        public async ValueTask DisposeAsync()
        {
            Session.Dispose();
            await Session.DisposalCompletion;
            Directory.Dispose();
        }
    }
}
