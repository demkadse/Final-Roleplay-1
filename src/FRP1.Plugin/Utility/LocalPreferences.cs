namespace FRP1.Plugin.Utility;

// UI-thread state for a bounded local preference file. Workers never mutate Value.
internal sealed class LocalPreferences<T> : IDisposable
{
    private readonly Func<CancellationToken, Task<T>> load;
    private readonly Func<T, CancellationToken, Task> save;
    private readonly Func<T, T> snapshot;
    private readonly Func<T, T, bool> same;
    private readonly CancellationTokenSource lifetime = new();
    private Task<T>? loading;
    private Task? saving;
    private long choice;
    private long loadChoice;
    private long saveChoice;
    private bool dirty;
    private bool disposed;

    public LocalPreferences(T initial, Func<CancellationToken, Task<T>> load, Func<T, CancellationToken, Task> save,
        Func<T, T> snapshot, Func<T, T, bool>? same = null)
    {
        this.load = load; this.save = save; this.snapshot = snapshot;
        this.same = same ?? EqualityComparer<T>.Default.Equals;
        Value = snapshot(initial);
        BeginLoad();
    }

    public T Value { get; private set; }
    public long Version { get; private set; }
    public bool Loaded { get; private set; }
    public bool IsBusy => loading is not null || saving is not null;
    public bool HasUnsavedChanges => dirty;
    public string? ErrorKey { get; private set; }
    public Task DisposalCompletion { get; private set; } = Task.CompletedTask;

    public void Set(T value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var copy = snapshot(value);
        if (Loaded && same(Value, copy) && ErrorKey is null) return;
        Value = copy;
        choice++;
        Version++;
        dirty = true;
        ErrorKey = null;
    }

    public void Poll()
    {
        if (disposed) return;
        if (loading is { IsCompleted: true } completedLoad)
        {
            loading = null;
            try
            {
                var loaded = snapshot(completedLoad.GetAwaiter().GetResult());
                if (choice == loadChoice) { Value = loaded; Version++; ErrorKey = null; }
            }
            catch (Exception ex) when (Expected(ex)) { if (choice == loadChoice) ErrorKey = "Error.Load"; }
            Loaded = true;
        }
        if (saving is { IsCompleted: true } completedSave)
        {
            saving = null;
            try
            {
                completedSave.GetAwaiter().GetResult();
                if (saveChoice == choice) { dirty = false; ErrorKey = null; }
            }
            catch (Exception ex) when (Expected(ex)) { if (saveChoice == choice) ErrorKey = "Error.Save"; }
        }
        if (Loaded && dirty && saving is null && loading is null && ErrorKey != "Error.Save")
        {
            var value = snapshot(Value);
            saveChoice = choice;
            var token = lifetime.Token;
            saving = Task.Run(() => save(value, token), token);
        }
    }

    public void Retry()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsBusy) return;
        if (dirty) ErrorKey = null;
        else BeginLoad();
    }

    private void BeginLoad()
    {
        loadChoice = choice;
        ErrorKey = null;
        var token = lifetime.Token;
        loading = Task.Run(() => load(token), token);
    }

    internal static bool Expected(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or
        ArgumentException or InvalidOperationException or OperationCanceledException;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        DisposalCompletion = ObserveCompletionAsync(new Task?[] { loading, saving }.OfType<Task>());
    }

    private async Task ObserveCompletionAsync(IEnumerable<Task> tasks)
    {
        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch (Exception ex) when (Expected(ex)) { /* No paths or preference contents are logged. */ }
        finally { lifetime.Dispose(); }
    }
}
