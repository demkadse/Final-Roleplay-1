namespace FRP1.Plugin.Integrations;

// Native reads and the only mutation run on the framework thread. No travel ownership is claimed.
public sealed class LifestreamSession : IDisposable
{
    public const int ActionIntervalMilliseconds = 500;
    public const int ReadIntervalMilliseconds = 1000;
    private readonly ILifestreamProvider provider;
    private readonly Func<LifestreamContext> capture;
    private readonly Func<IReadOnlyList<HousingWorld>> getWorlds;
    private readonly Func<LifestreamOptions> getOptions;
    private readonly Action<LifestreamOptions> setOptions;
    private readonly Func<long> now;
    private readonly Func<bool> onFrameworkThread;
    private LifestreamOptions options = new();
    private LifestreamContext? observed;
    private long sceneEpoch;
    private long generation;
    private long policyEpoch;
    private long nextAction;
    private long nextRead;
    private bool disposed;
    private PendingVisit? pending;
    private sealed record PendingVisit(LifestreamContext Context, long SceneEpoch, long Generation,
        long PolicyEpoch, long ProviderVersion, HousingAddress Address);

    public LifestreamSession(ILifestreamProvider provider, Func<LifestreamContext> capture,
        Func<IReadOnlyList<HousingWorld>> getWorlds, Func<LifestreamOptions> getOptions,
        Action<LifestreamOptions> setOptions, Func<long>? milliseconds = null, Func<bool>? onFrameworkThread = null)
    {
        this.provider = provider; this.capture = capture; this.getWorlds = getWorlds;
        this.getOptions = getOptions; this.setOptions = setOptions;
        now = milliseconds ?? (() => Environment.TickCount64);
        this.onFrameworkThread = onFrameworkThread ?? (() => true);
    }

    public IntegrationStatus Status { get; private set; } = IntegrationStatus.Disabled;
    public LifestreamActionResult LastAction { get; private set; }
    public bool HasPendingVisit => pending is not null;
    public LifestreamOptions Options => options;

    public void OptionsChanged(LifestreamOptions value)
    {
        if (disposed) return;
        options = LifestreamRules.ValidateOptions(value);
        Invalidate(); nextRead = 0;
        Status = options.Enabled ? IntegrationStatus.Blocked : IntegrationStatus.Disabled;
    }

    public void AddressSelectionChanged() { if (!disposed) Invalidate(); }
    public void InvalidatePolicy() => Interlocked.Increment(ref policyEpoch);

    public bool SaveAddress(HousingAddress address)
    {
        if (disposed || !options.Enabled || options != getOptions() || !LifestreamRules.IsValidAddress(address) ||
            !WorldExists(address)) return false;
        Invalidate();
        setOptions(options with { Address = LifestreamRules.Canonicalize(address) });
        SynchronizeOptions();
        return options.Address == LifestreamRules.Canonicalize(address);
    }

    // Called every framework update, including when the UI is closed. Detects A -> B -> A scenes.
    public void ObserveScene()
    {
        if (disposed || !onFrameworkThread()) return;
        SynchronizeOptions();
        if (!options.Enabled) return;
        Observe(capture());
    }

    private void Observe(LifestreamContext context)
    {
        if (observed == context) return;
        observed = context; sceneEpoch++; Invalidate(); nextRead = 0;
    }

    public LifestreamActionResult RequestVisit()
    {
        if (disposed) return LastAction = LifestreamActionResult.Disabled;
        SynchronizeOptions();
        if (!options.Enabled) return LastAction = LifestreamActionResult.Disabled;
        if (!onFrameworkThread()) return LastAction = LifestreamActionResult.Blocked;
        if (pending is not null) return LastAction = LifestreamActionResult.AlreadyPending;
        var context = capture(); Observe(context);
        if (!CanVisit(context) || options.Address is not { } address || !LifestreamRules.IsValidAddress(address) ||
            !WorldExists(address)) return LastAction = LifestreamActionResult.Blocked;
        if (now() < nextAction) return LastAction = LifestreamActionResult.RateLimited;
        var version = provider.Version;
        var epoch = Interlocked.Read(ref policyEpoch);
        var expectedGeneration = generation;
        var expectedScene = sceneEpoch;
        Status = CheckAvailability();
        if (Status != IntegrationStatus.Ready) return LastAction = LifestreamActionResult.NotReady;
        if (context != capture() || options != getOptions() || version != provider.Version || epoch != Interlocked.Read(ref policyEpoch) ||
            expectedGeneration != generation || expectedScene != sceneEpoch)
            return LastAction = LifestreamActionResult.Stale;
        pending = new(context, sceneEpoch, generation, epoch, version, address);
        nextAction = now() + ActionIntervalMilliseconds;
        return LastAction = LifestreamActionResult.Queued;
    }

    public void Tick()
    {
        if (disposed || !onFrameworkThread()) return;
        SynchronizeOptions();
        if (!options.Enabled) { Status = IntegrationStatus.Disabled; return; }
        var current = capture(); Observe(current);
        if (pending is { } action)
        {
            pending = null;
            if (!IsCurrent(action, current) || !WorldExists(action.Address))
            { LastAction = LifestreamActionResult.Stale; return; }
            Status = CheckAvailability();
            // Recheck after the final native read: callbacks may invalidate policy, scene or settings.
            if (Status != IntegrationStatus.Ready) { LastAction = LifestreamActionResult.NotReady; return; }
            if (!IsCurrent(action, capture()) || !WorldExists(action.Address) || !IsCurrent(action, capture()))
            { LastAction = LifestreamActionResult.Stale; return; }
            try
            {
                provider.GoToHousingAddress(action.Address);
                LastAction = LifestreamActionResult.HandedOff;
            }
            catch (Exception) { Status = IntegrationStatus.Error; LastAction = LifestreamActionResult.NotReady; }
            return;
        }
        if (!CanVisit(current)) { Status = IntegrationStatus.Blocked; return; }
        if (now() < nextRead) return;
        nextRead = now() + ReadIntervalMilliseconds;
        Status = CheckAvailability();
    }

    private bool IsCurrent(PendingVisit action, LifestreamContext current) => !disposed && options.Enabled &&
        options == getOptions() && options.Address == action.Address && action.Generation == generation &&
        action.SceneEpoch == sceneEpoch && action.PolicyEpoch == Interlocked.Read(ref policyEpoch) &&
        action.ProviderVersion == provider.Version && action.Context == current && CanVisit(current);

    private static bool CanVisit(LifestreamContext context) => context.CurrentWorldId != 0 &&
        context.Integration.CanRead && !context.Integration.Dirty && !context.Integration.Busy;

    private IntegrationStatus CheckAvailability()
    {
        try { return provider.CheckAvailability(); }
        catch (Exception) { return IntegrationStatus.Error; }
    }

    private bool WorldExists(HousingAddress address)
    {
        try { return LifestreamRules.WorldExists(address, getWorlds()); }
        catch (Exception) { return false; }
    }

    private void SynchronizeOptions()
    {
        var value = getOptions();
        if (options != value) OptionsChanged(value);
    }

    private void Invalidate()
    {
        generation++;
        if (pending is not null) LastAction = LifestreamActionResult.Stale;
        pending = null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Invalidate(); Status = IntegrationStatus.Disabled;
        try { provider.Dispose(); } catch (Exception) { }
    }
}
