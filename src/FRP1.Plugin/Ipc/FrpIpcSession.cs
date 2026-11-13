using FRP1.Core.Profiles;
using FRP1.Plugin.Integrations;
using System.Security.Cryptography;

namespace FRP1.Plugin.Ipc;

// IPC callbacks only inspect detached snapshots or fill one bounded queue slot.
// Capture, open and writer dispatch are exclusively advanced by the UI/frame thread.
public sealed class FrpIpcSession : IDisposable
{
    private readonly object gate = new();
    private readonly Func<FrpIpcContext> capture;
    private readonly Func<FrpIpcAction, FrpIpcProfile, CancellationToken, Task<FrpIpcProfile>> write;
    private readonly Func<FrpIpcProfile, bool> open;
    private readonly Func<long> now;
    private IntegrationIpcOptions options = new();
    private Published? published;
    private FrpIpcContext? observed;
    private Work? pending;
    private Completion? completion;
    private long externalEpoch;
    private long generation;
    // A fresh positive sequence origin avoids ticket aliases after a plugin reload.
    private long ticket = BitConverter.ToInt64(RandomNumberGenerator.GetBytes(sizeof(long))) & long.MaxValue;
    private long nextAction;
    private bool disposed;

    private sealed record Published(FrpIpcContext Context, long Generation, long Epoch);
    private sealed record Completion(long Ticket, FrpIpcResult Result, long Expires, long Generation, long Epoch);
    private sealed class Work(long ticket, FrpIpcAction action, Published expected)
    {
        public long Ticket { get; } = ticket;
        public FrpIpcAction Action { get; } = action;
        public Published Expected { get; } = expected;
        public FrpIpcResult Result { get; set; } = FrpIpcResult.Queued;
        public Task<FrpIpcProfile>? Task { get; set; }
        public CancellationTokenSource? Cancellation { get; set; }
        public bool Invalidated { get; set; }
    }

    public FrpIpcSession(Func<FrpIpcContext> capture,
        Func<FrpIpcAction, FrpIpcProfile, CancellationToken, Task<FrpIpcProfile>> write,
        Func<FrpIpcProfile, bool> open, Func<long>? milliseconds = null)
    {
        this.capture = capture; this.write = write; this.open = open;
        now = milliseconds ?? (() => Environment.TickCount64);
    }

    public Task DisposalCompletion { get; private set; } = Task.CompletedTask;
    public bool HasCapabilities => FrpIpcRules.Capabilities(Volatile.Read(ref options)) != 0;

    public void SetOptions(IntegrationIpcOptions value)
    {
        lock (gate)
        {
            if (disposed || options == value) return;
            Volatile.Write(ref options, value);
            generation++;
            Volatile.Write(ref published, null);
            InvalidateWork();
            completion = null;
        }
    }

    // Called from policy notifications without acquiring gate: policy events run under their own lock.
    public void InvalidatePolicy()
    {
        Interlocked.Increment(ref externalEpoch);
        Volatile.Write(ref published, null);
    }

    public int GetCapabilities()
    {
        lock (gate) return disposed ? 0 : FrpIpcRules.Capabilities(options);
    }

    public int GetReadiness()
    {
        lock (gate)
        {
            if (disposed || FrpIpcRules.Capabilities(options) == 0) return (int)FrpIpcReadiness.Disabled;
            var snapshot = Current();
            if (snapshot is null) return (int)FrpIpcReadiness.Blocked;
            if ((FrpIpcRules.Capabilities(options) & ~FrpIpcRules.ReadCapability) == 0 &&
                snapshot.Context.Active?.Rating != ContentRating.General) return (int)FrpIpcReadiness.Blocked;
            var ready = FrpIpcRules.Readiness(snapshot.Context);
            return (int)(ready == FrpIpcReadiness.Ready && pending is not null ? FrpIpcReadiness.Busy : ready);
        }
    }

    public (int Result, int Status, int Availability) GetOwnState()
    {
        lock (gate)
        {
            if (disposed || !options.ReadEnabled) return ((int)FrpIpcResult.Disabled, -1, -1);
            var snapshot = Current();
            if (snapshot is null || !FrpIpcRules.Allowed(snapshot.Context) || snapshot.Context.Active!.Rating != ContentRating.General)
                return ((int)FrpIpcResult.Blocked, -1, -1);
            if (snapshot.Context.Dirty || snapshot.Context.Busy || pending is not null) return ((int)FrpIpcResult.Busy, -1, -1);
            var result = (0, (int)snapshot.Context.Active.Status, (int)snapshot.Context.Active.Availability);
            return ReferenceEquals(snapshot, Current()) ? result : ((int)FrpIpcResult.Stale, -1, -1);
        }
    }

    public long Request(FrpIpcAction action)
    {
        lock (gate)
        {
            if (disposed || !FrpIpcRules.ActionEnabled(options, action)) return (long)FrpIpcResult.Disabled;
            if (!FrpIpcRules.ValidAction(action)) return (long)FrpIpcResult.InvalidArgument;
            var snapshot = Current();
            if (snapshot is null || !FrpIpcRules.Allowed(snapshot.Context)) return (long)FrpIpcResult.Blocked;
            if (pending is { } work)
                return !work.Invalidated && work.Action == action && work.Expected.Generation == snapshot.Generation &&
                    work.Expected.Epoch == snapshot.Epoch ? work.Ticket : (long)FrpIpcResult.Busy;
            if (snapshot.Context.Dirty || snapshot.Context.Busy) return (long)FrpIpcResult.Busy;
            if (now() < nextAction) return (long)FrpIpcResult.RateLimited;
            if (ticket == long.MaxValue) return (long)FrpIpcResult.Error;
            var accepted = ++ticket;
            nextAction = now() + FrpIpcRules.ActionIntervalMilliseconds;
            pending = new(accepted, action, snapshot);
            return accepted;
        }
    }

    public int GetActionResult(long requestedTicket)
    {
        lock (gate)
        {
            if (disposed || !options.ControlEnabled) return (int)FrpIpcResult.Disabled;
            if (requestedTicket <= 0) return (int)FrpIpcResult.InvalidArgument;
            if (pending is { } work && work.Ticket == requestedTicket)
                return (int)(work.Invalidated || work.Expected.Epoch != Interlocked.Read(ref externalEpoch)
                    ? FrpIpcResult.Stale : work.Result);
            if (completion is { } done && done.Expires > now() && done.Ticket == requestedTicket)
                return (int)(done.Generation == generation && done.Epoch == Interlocked.Read(ref externalEpoch)
                    ? done.Result : FrpIpcResult.Stale);
            return (int)FrpIpcResult.Unknown;
        }
    }

    public void Tick() => Advance(execute: true);
    public void Refresh() => Advance(execute: false);

    private void Advance(bool execute)
    {
        lock (gate)
        {
            if (disposed) return;
            if (FrpIpcRules.Capabilities(options) == 0)
            {
                Volatile.Write(ref published, null);
                InvalidateWork();
                ObserveInvalidatedCompletion();
                return;
            }
            var epoch = Interlocked.Read(ref externalEpoch);
            FrpIpcContext current;
            try { current = capture(); }
            catch (Exception)
            {
                Volatile.Write(ref published, null); InvalidateWork(); ObserveInvalidatedCompletion(); return;
            }
            if (epoch != Interlocked.Read(ref externalEpoch))
            {
                Volatile.Write(ref published, null); InvalidateWork(); ObserveInvalidatedCompletion(); return;
            }
            if (observed is null || !FrpIpcRules.SameScope(observed, current))
            {
                generation++; InvalidateWork();
            }
            observed = current;
            if (pending is { } running && (running.Expected.Epoch != epoch || !FrpIpcRules.ActionEnabled(options, running.Action)))
                InvalidateWork();
            if (pending is { Task: null } waiting && waiting.Expected.Context != current ||
                pending is { Task: not null } && current.Dirty) InvalidateWork();
            if (pending is { Task: { IsCompleted: true } } completed) FinishWrite(completed, current, epoch);
            Volatile.Write(ref published, new(current, generation, epoch));
            if (!execute || pending is not { Task: null, Invalidated: false } queued) return;

            // One UI-thread critical section covers the live context recheck and dispatch.
            FrpIpcContext execution;
            try { execution = capture(); }
            catch (Exception) { Volatile.Write(ref published, null); Complete(queued, FrpIpcResult.Error); return; }
            if (queued.Expected.Generation != generation || queued.Expected.Epoch != Interlocked.Read(ref externalEpoch) ||
                queued.Expected.Context != execution || FrpIpcRules.Readiness(execution) != FrpIpcReadiness.Ready ||
                !FrpIpcRules.ActionEnabled(options, queued.Action))
            {
                Volatile.Write(ref published, null); Complete(queued, FrpIpcResult.Stale); return;
            }
            var active = execution.Active!;
            if (FrpIpcRules.AlreadySatisfied(queued.Action, active)) { Complete(queued, FrpIpcResult.AlreadySatisfied); return; }
            queued.Result = FrpIpcResult.Running;
            try
            {
                if (queued.Action.Kind == FrpIpcActionKind.OpenOwnProfile)
                    Complete(queued, open(active) ? FrpIpcResult.Opened : FrpIpcResult.Stale);
                else
                {
                    queued.Cancellation = new();
                    queued.Task = write(queued.Action, active, queued.Cancellation.Token);
                }
            }
            catch (Exception) { Complete(queued, FrpIpcResult.Error); }
        }
    }

    private Published? Current()
    {
        var snapshot = Volatile.Read(ref published);
        return snapshot is not null && snapshot.Epoch == Interlocked.Read(ref externalEpoch) ? snapshot : null;
    }

    private void FinishWrite(Work work, FrpIpcContext current, long epoch)
    {
        FrpIpcProfile saved;
        try { saved = work.Task!.GetAwaiter().GetResult(); }
        catch (Exception) { Complete(work, work.Invalidated ? FrpIpcResult.Stale : FrpIpcResult.Error); return; }
        var expected = work.Expected.Context;
        var scopeMatches = !work.Invalidated && work.Expected.Generation == generation && work.Expected.Epoch == epoch &&
            FrpIpcRules.SameScope(expected, current) && FrpIpcRules.Allowed(current) && !current.Dirty;
        // The task may finish just after OwnProfileSession.Poll. Wait for its next
        // poll to integrate the saved snapshot before deciding persistence.
        if (scopeMatches && current.Busy && (current.Active == expected.Active || current.Active == saved)) return;
        var valid = scopeMatches && FrpIpcRules.Readiness(current) == FrpIpcReadiness.Ready &&
            saved == current.Active && saved.Id == expected.Active!.Id && saved.Revision > expected.Active.Revision &&
            FrpIpcRules.AlreadySatisfied(work.Action, saved);
        Complete(work, valid ? FrpIpcResult.Persisted : FrpIpcResult.Stale);
    }

    private void InvalidateWork()
    {
        if (pending is not { } work) return;
        work.Invalidated = true;
        if (work.Task is null) { Complete(work, FrpIpcResult.Stale); return; }
        try { work.Cancellation?.Cancel(); } catch (AggregateException) { }
    }

    private void ObserveInvalidatedCompletion()
    {
        if (pending is not { Task: { IsCompleted: true } } work) return;
        try { _ = work.Task.GetAwaiter().GetResult(); } catch (Exception) { }
        Complete(work, FrpIpcResult.Stale);
    }

    private void Complete(Work work, FrpIpcResult result)
    {
        completion = new(work.Ticket, result, now() + FrpIpcRules.CompletionLifetimeMilliseconds,
            work.Expected.Generation, work.Expected.Epoch);
        work.Cancellation?.Dispose();
        if (ReferenceEquals(work, pending)) pending = null;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true; Volatile.Write(ref published, null); Interlocked.Increment(ref externalEpoch);
            InvalidateWork();
            if (pending is { Task: { } task } work)
            {
                pending = null;
                DisposalCompletion = ObserveDisposalAsync(task, work.Cancellation);
            }
            completion = null;
        }
    }

    private static async Task ObserveDisposalAsync(Task task, CancellationTokenSource? cancellation)
    {
        try { await task.ConfigureAwait(false); } catch (Exception) { }
        finally { cancellation?.Dispose(); }
    }
}
