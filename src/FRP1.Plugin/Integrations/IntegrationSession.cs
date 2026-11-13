using FRP1.Core.Safety;

namespace FRP1.Plugin.Integrations;

// One coalesced read and one explicit draft action. All mutable state is UI-thread-only.
public sealed class IntegrationSession : IDisposable
{
    public const int ReadIntervalMilliseconds = 1000;
    public const int ImportIntervalMilliseconds = 500;
    private readonly IntegrationSettingsStore store;
    private readonly IHonorificReader reader;
    private readonly Func<IntegrationContext> capture;
    private readonly Func<IntegrationDraftToken, string, bool> apply;
    private readonly Func<long> now;
    private readonly CancellationTokenSource lifetime = new();
    private Task<IntegrationSettings>? load;
    private Task? save;
    private bool savePending;
    private bool readPending;
    private bool disposed;
    private long generation;
    private long readerVersion = -1;
    private long nextRead;
    private long nextImport;
    private IntegrationContext? observed;
    private IntegrationContext? confirmed;
    private TitleConfirmation? titleConfirmation;
    private ImportRequest? pendingImport;
    private sealed record ImportRequest(IntegrationContext Context, long Generation, long ReaderVersion, string Title);
    private sealed record TitleConfirmation(IntegrationActor Actor, AudiencePreferences Audience, long PolicyGeneration, long ReaderVersion);

    public IntegrationSession(IntegrationSettingsStore store, IHonorificReader reader, Func<IntegrationContext> capture,
        Func<IntegrationDraftToken, string, bool> apply, Func<long>? milliseconds = null)
    {
        this.store = store; this.reader = reader; this.capture = capture; this.apply = apply;
        now = milliseconds ?? (() => Environment.TickCount64);
        load = Task.Run(() => store.LoadAsync(lifetime.Token));
    }

    public IntegrationSettings Settings { get; private set; } = new();
    public bool SettingsLoaded { get; private set; }
    public bool LoadFailed { get; private set; }
    public bool SaveFailed { get; private set; }
    public IntegrationStatus Status { get; private set; } = IntegrationStatus.Disabled;
    public IntegrationActionResult LastAction { get; private set; }
    public string Title { get; private set; } = "";
    public long Generation => generation;
    public bool HasPendingImport => pendingImport is not null;
    public bool HasConfirmedReadiness => Volatile.Read(ref titleConfirmation) is { } snapshot && snapshot.ReaderVersion == reader.Version;
    public Task DisposalCompletion { get; private set; } = Task.CompletedTask;
    public IntegrationIpcOptions IpcOptions => new(Settings.IpcReadEnabled, Settings.IpcControlEnabled,
        Settings.IpcSetStatusEnabled, Settings.IpcSetAvailabilityEnabled, Settings.IpcOpenOwnProfileEnabled);
    public event Action<IntegrationIpcOptions>? IpcOptionsChanged;
    public MoodlesOptions MoodlesOptions => new(Settings.MoodlesEnabled, Settings.MoodlesBindings);
    public event Action<MoodlesOptions>? MoodlesOptionsChanged;

    public void SetMoodlesOptions(MoodlesOptions options)
    {
        if (disposed || !SettingsLoaded) return;
        options = MoodlesRules.ValidateOptions(options);
        if (Settings.MoodlesEnabled == options.Enabled && Settings.MoodlesBindings.SequenceEqual(options.Bindings!)) return;
        Settings = Settings with { MoodlesEnabled = options.Enabled, MoodlesBindings = options.Bindings! };
        savePending = true;
        MoodlesOptionsChanged?.Invoke(MoodlesOptions);
    }

    public void SetIpcOptions(IntegrationIpcOptions options)
    {
        if (disposed || !SettingsLoaded || IpcOptions == options) return;
        Settings = Settings with
        {
            IpcReadEnabled = options.ReadEnabled, IpcControlEnabled = options.ControlEnabled,
            IpcSetStatusEnabled = options.SetStatusEnabled, IpcSetAvailabilityEnabled = options.SetAvailabilityEnabled,
            IpcOpenOwnProfileEnabled = options.OpenOwnProfileEnabled,
        };
        savePending = true;
        IpcOptionsChanged?.Invoke(options);
    }

    public void SetHonorificEnabled(bool enabled)
    {
        if (disposed || !SettingsLoaded || Settings.HonorificEnabled == enabled) return;
        Settings = Settings with { HonorificEnabled = enabled };
        savePending = true;
        Invalidate();
        Status = enabled ? IntegrationStatus.Blocked : IntegrationStatus.Disabled;
        readPending = enabled;
    }

    public void RequestRead()
    {
        if (!disposed && SettingsLoaded && Settings.HonorificEnabled) readPending = true;
    }

    public IntegrationActionResult RequestImport()
    {
        if (disposed || !Settings.HonorificEnabled) return LastAction = IntegrationActionResult.Disabled;
        if (pendingImport is not null) return LastAction = IntegrationActionResult.AlreadyPending;
        var context = capture();
        if (!context.CanImport) return LastAction = IntegrationActionResult.Blocked;
        if (!ReadIsCurrent(context) || string.IsNullOrWhiteSpace(Title)) return LastAction = IntegrationActionResult.NotReady;
        if (now() < nextImport) return LastAction = IntegrationActionResult.RateLimited;
        pendingImport = new(context, generation, reader.Version, Title);
        return LastAction = IntegrationActionResult.Queued;
    }

    public void Tick()
    {
        if (disposed) return;
        PollSettings();
        if (!SettingsLoaded || !Settings.HonorificEnabled) { Status = IntegrationStatus.Disabled; return; }
        var current = capture();
        if (!SameReadContext(observed, current) || readerVersion != reader.Version)
        {
            Invalidate();
            observed = current;
            readerVersion = reader.Version;
            readPending = Settings.HonorificEnabled;
        }
        if (!current.CanRead)
        {
            if (confirmed is not null || pendingImport is not null) Invalidate();
            Status = IntegrationStatus.Blocked;
            return;
        }

        // The queued action never relies on the conditions that held at enqueue time.
        if (pendingImport is { } request)
        {
            pendingImport = null;
            var execution = capture();
            if (request.Generation != generation || request.ReaderVersion != reader.Version ||
                request.Context != execution || !execution.CanImport || !ReadIsCurrent(execution))
                LastAction = IntegrationActionResult.Stale;
            else
            {
                nextImport = now() + ImportIntervalMilliseconds;
                try { LastAction = apply(execution.Draft!, request.Title) ? IntegrationActionResult.Applied : IntegrationActionResult.Stale; }
                catch (Exception) { LastAction = IntegrationActionResult.Blocked; }
            }
        }
        if (!readPending || now() < nextRead) return;
        readPending = false;
        nextRead = now() + ReadIntervalMilliseconds;
        var expectedGeneration = generation;
        var expectedReaderVersion = reader.Version;
        Volatile.Write(ref titleConfirmation, null);
        Status = IntegrationStatus.Busy;
        HonorificReadResult result;
        try { result = reader.Read(); }
        catch (Exception) { result = new(IntegrationStatus.Error); }
        var after = capture();
        if (disposed || !Settings.HonorificEnabled || generation != expectedGeneration || reader.Version != expectedReaderVersion ||
            current != after || !after.CanRead)
        {
            Invalidate(); Status = Settings.HonorificEnabled ? IntegrationStatus.Blocked : IntegrationStatus.Disabled;
            readPending = Settings.HonorificEnabled;
            return;
        }
        Title = ""; confirmed = null;
        Status = result.Status;
        if (Status != IntegrationStatus.Ready) return;
        if (!HonorificRules.TryReadTitle(result.Json, out var text)) { Status = IntegrationStatus.Error; return; }
        Title = text; confirmed = after;
        Volatile.Write(ref titleConfirmation, new(after.Actor, after.Audience, after.PolicyGeneration, expectedReaderVersion));
    }

    public bool CanYieldOwnTitle(IntegrationActor current, AudiencePreferences audience, long policyGeneration,
        bool accessReady, ulong plateObjectId, long plateAddress)
    {
        var snapshot = Volatile.Read(ref titleConfirmation);
        if (snapshot is null || !accessReady || snapshot.ReaderVersion != reader.Version || snapshot.Audience != audience ||
            snapshot.PolicyGeneration != policyGeneration || audience.Selection == AudienceSelection.Unspecified) return false;
        return HonorificRules.YieldOwnTitle(true, IntegrationStatus.Ready, snapshot.Actor, current, plateObjectId, plateAddress) &&
            ReferenceEquals(snapshot, Volatile.Read(ref titleConfirmation)) && snapshot.ReaderVersion == reader.Version;
    }

    private bool ReadIsCurrent(IntegrationContext current) => Status == IntegrationStatus.Ready &&
        readerVersion == reader.Version && current.CanRead && SameReadContext(confirmed, current);

    private static bool SameReadContext(IntegrationContext? left, IntegrationContext right) => left is not null &&
        left.Actor == right.Actor && left.AccessReady == right.AccessReady && left.Audience == right.Audience &&
        left.PolicyGeneration == right.PolicyGeneration;

    private void Invalidate()
    {
        generation++; confirmed = null; Title = "";
        Volatile.Write(ref titleConfirmation, null);
        if (pendingImport is not null) LastAction = IntegrationActionResult.Stale;
        pendingImport = null;
    }

    private void PollSettings()
    {
        if (load is { IsCompleted: true } completed)
        {
            load = null;
            try { Settings = completed.GetAwaiter().GetResult(); }
            catch (Exception) { LoadFailed = true; Settings = new(); }
            SettingsLoaded = true;
            readPending = Settings.HonorificEnabled;
            IpcOptionsChanged?.Invoke(IpcOptions);
            MoodlesOptionsChanged?.Invoke(MoodlesOptions);
        }
        if (save is { IsCompleted: true } written)
        {
            save = null;
            try { written.GetAwaiter().GetResult(); SaveFailed = false; }
            catch (Exception) { SaveFailed = true; }
        }
        if (savePending && save is null)
        {
            savePending = false;
            var snapshot = Settings;
            save = Task.Run(() => store.SaveAsync(snapshot, lifetime.Token));
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Invalidate(); Status = IntegrationStatus.Disabled;
        lifetime.Cancel();
        try { reader.Dispose(); } catch (Exception) { }
        DisposalCompletion = ObserveDisposalAsync(load, save);
    }

    private async Task ObserveDisposalAsync(Task? pendingLoad, Task? pendingSave)
    {
        try
        {
            if (pendingLoad is not null) try { await pendingLoad.ConfigureAwait(false); } catch (Exception) { }
            if (pendingSave is not null) try { await pendingSave.ConfigureAwait(false); } catch (Exception) { }
        }
        finally { lifetime.Dispose(); }
    }
}
