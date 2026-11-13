using FRP1.Core.Profiles;

namespace FRP1.Plugin.Integrations;

// All reads and mutable session state belong to the frame thread. Provider events only change reader.Version.
public sealed class MoodlesSession : IDisposable
{
    public const int ReadIntervalMilliseconds = 1000;
    public const int ImportIntervalMilliseconds = 500;
    private readonly IMoodlesReader reader;
    private readonly Func<MoodlesContext> capture;
    private readonly Func<IntegrationDraftToken, GlanceEntry, ContentRating, bool> apply;
    private readonly Func<MoodlesOptions> getOptions;
    private readonly Action<MoodlesOptions> setOptions;
    private readonly Func<long> now;
    private MoodlesOptions options = new();
    private MoodlesContext? observed;
    private MoodlesContext? confirmed;
    private IReadOnlyList<MoodlesStatus> statuses = Array.Empty<MoodlesStatus>();
    private long readerVersion = -1;
    private long generation;
    private long nextRead;
    private long nextAction;
    private bool readPending;
    private bool disposed;
    private PendingAction? pending;
    private sealed record PendingAction(MoodlesContext Context, long Generation, long ReaderVersion,
        MoodlesStatus Status, ContentRating? Rating, bool Import);

    public MoodlesSession(IMoodlesReader reader, Func<MoodlesContext> capture,
        Func<IntegrationDraftToken, GlanceEntry, ContentRating, bool> apply, Func<MoodlesOptions> getOptions,
        Action<MoodlesOptions> setOptions, Func<long>? milliseconds = null)
    {
        this.reader = reader; this.capture = capture; this.apply = apply;
        this.getOptions = getOptions; this.setOptions = setOptions;
        now = milliseconds ?? (() => Environment.TickCount64);
    }

    public IntegrationStatus Status { get; private set; } = IntegrationStatus.Disabled;
    public IntegrationActionResult LastAction { get; private set; }
    public bool HasPendingImport => pending is not null;
    public MoodlesOptions Options => options;
    public IReadOnlyList<MoodlesStatus> VisibleStatuses
    {
        get
        {
            if (disposed || !options.Enabled || !SameOptions(options, getOptions()) || readerVersion != reader.Version ||
                confirmed is null || confirmed != capture() || !confirmed.Integration.CanRead) return Array.Empty<MoodlesStatus>();
            return Array.AsReadOnly(statuses.Where(status => MoodlesRules.CanPreview(status, options, confirmed.Integration.Audience)).ToArray());
        }
    }

    public void OptionsChanged(MoodlesOptions value)
    {
        if (disposed) return;
        options = MoodlesRules.ValidateOptions(value);
        Invalidate();
        readPending = options.Enabled;
        Status = options.Enabled ? IntegrationStatus.Blocked : IntegrationStatus.Disabled;
    }

    public void RequestRead()
    {
        if (!disposed && options.Enabled) readPending = true;
    }

    public bool SetSelected(Guid id, bool selected)
    {
        if (disposed || !options.Enabled || !SameOptions(options, getOptions())) return false;
        var bindings = (options.Bindings ?? Array.Empty<MoodlesBinding>()).ToList();
        if (!selected)
        {
            if (bindings.RemoveAll(binding => binding.StatusId == id) == 0) return false;
        }
        else
        {
            var source = VisibleStatuses.FirstOrDefault(status => status.StatusId == id);
            if (source is null || bindings.Count >= MoodlesRules.MaximumBindings || bindings.Any(binding => binding.StatusId == id)) return false;
            bindings.Add(new(id, source.ContentHash));
        }
        setOptions(options with { Bindings = bindings.AsReadOnly() });
        SynchronizeOptions();
        return true;
    }

    public IntegrationActionResult RequestRating(Guid id, ContentRating rating) => RequestAction(id, rating, false);
    public IntegrationActionResult RequestImport(Guid id) => RequestAction(id, null, true);

    private IntegrationActionResult RequestAction(Guid id, ContentRating? rating, bool import)
    {
        if (disposed || !options.Enabled) return LastAction = IntegrationActionResult.Disabled;
        if (pending is not null) return LastAction = IntegrationActionResult.AlreadyPending;
        var context = capture();
        var source = VisibleStatuses.FirstOrDefault(status => status.StatusId == id);
        if (source is null || !context.Integration.CanRead ||
            options.Bindings?.Any(binding => binding.StatusId == id) != true) return LastAction = IntegrationActionResult.NotReady;
        if (import && (!context.Integration.CanImport || context.GlanceCount >= 5 || context.GlanceCount < 0 ||
                !MoodlesRules.CanCopy(source, MoodlesRules.ReviewedRating(source, options), context.TargetRating, context.Integration.Audience)) ||
            !import && (rating is null || !Enum.IsDefined(rating.Value) || !MoodlesRules.AdultOptIn(context.Integration.Audience)))
            return LastAction = IntegrationActionResult.Blocked;
        if (now() < nextAction) return LastAction = IntegrationActionResult.RateLimited;
        pending = new(context, generation, reader.Version, source, rating, import);
        return LastAction = IntegrationActionResult.Queued;
    }

    public void Tick()
    {
        if (disposed) return;
        SynchronizeOptions();
        if (!options.Enabled) { Status = IntegrationStatus.Disabled; return; }
        var current = capture();
        if (observed != current || readerVersion != reader.Version)
        {
            Invalidate(); observed = current; readerVersion = reader.Version; readPending = true;
        }
        if (!current.Integration.CanRead) { Status = IntegrationStatus.Blocked; return; }
        if (pending is { } action)
        {
            pending = null;
            Execute(action, current);
            return;
        }
        if (!readPending || now() < nextRead) return;
        readPending = false; nextRead = now() + ReadIntervalMilliseconds;
        var expectedVersion = reader.Version;
        var expectedGeneration = generation;
        var result = ReadValidated();
        if (!StillCurrent(current, expectedVersion, expectedGeneration))
        {
            Invalidate(); readPending = options.Enabled; Status = IntegrationStatus.Blocked; return;
        }
        Status = result.Status;
        statuses = result.Statuses ?? Array.Empty<MoodlesStatus>();
        confirmed = Status == IntegrationStatus.Ready ? current : null;
    }

    private void Execute(PendingAction action, MoodlesContext current)
    {
        if (action.Generation != generation || action.ReaderVersion != reader.Version || action.Context != current ||
            confirmed != current || !SameOptions(options, getOptions()))
        { LastAction = IntegrationActionResult.Stale; return; }
        nextAction = now() + ImportIntervalMilliseconds;
        // Read the live own status again: an unchanged event epoch alone is not proof of unchanged source data.
        var fresh = ReadValidated();
        var source = fresh.Statuses?.FirstOrDefault(status => status.StatusId == action.Status.StatusId);
        if (fresh.Status != IntegrationStatus.Ready || source != action.Status ||
            !StillCurrent(current, action.ReaderVersion, action.Generation))
        { Invalidate(); readPending = true; LastAction = IntegrationActionResult.Stale; return; }
        if (!action.Import)
        {
            if (!MoodlesRules.AdultOptIn(current.Integration.Audience)) { LastAction = IntegrationActionResult.Blocked; return; }
            var bindings = options.Bindings!.Select(binding => binding.StatusId == source.StatusId
                ? new MoodlesBinding(source.StatusId, source.ContentHash, action.Rating) : binding).ToArray();
            setOptions(options with { Bindings = Array.AsReadOnly(bindings) });
            SynchronizeOptions();
            LastAction = IntegrationActionResult.Applied;
            return;
        }
        var rating = MoodlesRules.ReviewedRating(source, options);
        if (!current.Integration.CanImport || current.GlanceCount is < 0 or >= 5 ||
            !MoodlesRules.CanCopy(source, rating, current.TargetRating, current.Integration.Audience))
        { LastAction = IntegrationActionResult.Blocked; return; }
        try
        {
            LastAction = apply(current.Integration.Draft!, new GlanceEntry(source.Label, source.Description) { IconId = source.IconId }, rating!.Value)
                ? IntegrationActionResult.Applied : IntegrationActionResult.Stale;
        }
        catch (Exception) { LastAction = IntegrationActionResult.Blocked; }
    }

    private MoodlesReadResult ReadValidated()
    {
        try
        {
            var result = reader.ReadLocal();
            if (result.Status != IntegrationStatus.Ready) return new(result.Status);
            return MoodlesRules.TryValidateStatuses(result.Statuses, out var detached)
                ? new(IntegrationStatus.Ready, detached) : new(IntegrationStatus.Error);
        }
        catch (Exception) { return new(IntegrationStatus.Error); }
    }

    private bool StillCurrent(MoodlesContext context, long version, long expectedGeneration) => !disposed &&
        options.Enabled && SameOptions(options, getOptions()) && reader.Version == version && generation == expectedGeneration &&
        capture() == context && context.Integration.CanRead;

    private static bool SameOptions(MoodlesOptions left, MoodlesOptions right) => left.Enabled == right.Enabled &&
        (left.Bindings ?? Array.Empty<MoodlesBinding>()).SequenceEqual(right.Bindings ?? Array.Empty<MoodlesBinding>());

    private void SynchronizeOptions()
    {
        var value = getOptions();
        if (!SameOptions(options, value)) OptionsChanged(value);
    }

    private void Invalidate()
    {
        generation++; confirmed = null; statuses = Array.Empty<MoodlesStatus>();
        if (pending is not null) LastAction = IntegrationActionResult.Stale;
        pending = null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Invalidate(); Status = IntegrationStatus.Disabled;
        try { reader.Dispose(); } catch (Exception) { }
    }
}
