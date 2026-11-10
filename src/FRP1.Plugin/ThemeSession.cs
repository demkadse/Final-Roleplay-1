using FRP1.Plugin.Theming;

namespace FRP1.Plugin;

// Called only from the UI thread. Background operations return immutable snapshots.
internal sealed class ThemeSession : IDisposable
{
    private readonly Func<ulong, CancellationToken, Task<ThemePreferences>> load;
    private readonly Func<ulong, ThemePreferences, CancellationToken, Task> save;
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<LoadOperation> loads = [];
    private readonly Dictionary<ulong, SaveRequest> queuedSaves = [];
    private readonly Dictionary<ulong, SaveRequest> unsavedChoices = [];
    private SaveOperation? activeSave;
    private ulong contentId;
    private uint? raceId;
    private long contextVersion;
    private long choiceVersion;
    private bool ready;
    private bool disposed;

    public ThemeSession(ThemeSettingsStore store) : this(store.LoadAsync, store.SaveAsync) { }

    public ThemeSession(Func<ulong, CancellationToken, Task<ThemePreferences>> load,
        Func<ulong, ThemePreferences, CancellationToken, Task> save)
    {
        this.load = load;
        this.save = save;
    }

    public ThemePreferences Preferences { get; private set; } = new();
    public ThemeDefinition Theme => ready && HasCharacter ? ThemeCatalog.Resolve(Preferences, raceId) : ThemeCatalog.Get("obsidian");
    public bool HasCharacter => contentId != 0;
    public bool IsLoading => loads.Any(operation => operation.ContextVersion == contextVersion && operation.ChoiceVersion == choiceVersion);
    public bool HasPendingSave => queuedSaves.ContainsKey(contentId) || activeSave?.Request.ContentId == contentId;
    public bool LoadFailed { get; private set; }
    public bool SaveFailed { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Task DisposalCompletion { get; private set; } = Task.CompletedTask;

    public void ObserveCharacter(ulong currentContentId, uint? currentRaceId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        currentRaceId = currentRaceId is >= 1 and <= 8 ? currentRaceId : null;
        if (contentId == currentContentId)
        {
            raceId = currentRaceId;
            return;
        }
        foreach (var operation in loads) operation.Cancellation.Cancel();
        contentId = currentContentId;
        raceId = currentRaceId;
        contextVersion++;
        choiceVersion = 0;
        ready = false;
        Preferences = new();
        LoadFailed = SaveFailed = false;
        ErrorMessage = null;
        if (HasCharacter && unsavedChoices.TryGetValue(contentId, out var unsaved))
        {
            // Returning to this character must not load an older disk snapshot
            // while its last explicit choice is still being saved or needs retry.
            Preferences = unsaved.Preferences;
            ready = true;
            SaveFailed = !HasPendingSave;
            if (SaveFailed) ErrorMessage = "Darstellung konnte nicht gespeichert werden. Die Auswahl bleibt für diese Sitzung aktiv.";
        }
        else if (HasCharacter) StartLoad();
    }

    private void StartLoad()
    {
        LoadFailed = false;
        ErrorMessage = null;
        var source = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var id = contentId;
        var token = source.Token;
        loads.Add(new LoadOperation(contextVersion, choiceVersion, source,
            Task.Run(() => load(id, token), token)));
    }

    public void SetPreferences(ThemePreferences preferences)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ThemeCatalog.ValidatePreferences(preferences);
        if (!HasCharacter) return;
        if (ready && Preferences == preferences) return;
        Preferences = preferences;
        ready = true;
        choiceVersion++;
        LoadFailed = SaveFailed = false;
        ErrorMessage = null;
        QueueSave();
    }

    private void QueueSave()
    {
        var request = new SaveRequest(contentId, contextVersion, choiceVersion, Preferences);
        queuedSaves[contentId] = request;
        unsavedChoices[contentId] = request;
        StartNextSave();
    }

    private void StartNextSave()
    {
        if (activeSave is not null || queuedSaves.Count == 0) return;
        var next = queuedSaves.First();
        queuedSaves.Remove(next.Key);
        var request = next.Value;
        var token = lifetime.Token;
        activeSave = new SaveOperation(request, Task.Run(() => save(request.ContentId, request.Preferences, token), token));
    }

    public void Poll()
    {
        if (disposed) return;
        foreach (var operation in loads.Where(operation => operation.Task.IsCompleted).ToArray())
        {
            loads.Remove(operation);
            var current = operation.ContextVersion == contextVersion && operation.ChoiceVersion == choiceVersion;
            try
            {
                var loaded = operation.Task.GetAwaiter().GetResult();
                ThemeCatalog.ValidatePreferences(loaded);
                if (current)
                {
                    Preferences = loaded;
                    ready = true;
                    LoadFailed = false;
                    ErrorMessage = null;
                }
            }
            catch (Exception ex) when (IsExpectedFailure(ex))
            {
                if (current)
                {
                    LoadFailed = true;
                    ErrorMessage = "Darstellung konnte nicht geladen werden. Obsidian bleibt aktiv; erneut versuchen oder eine Darstellung wählen.";
                }
            }
            finally { operation.Cancellation.Dispose(); }
        }
        if (activeSave is { Task.IsCompleted: true } completedSave)
        {
            activeSave = null;
            var request = completedSave.Request;
            var latest = unsavedChoices.TryGetValue(request.ContentId, out var unsaved) && unsaved == request;
            var current = latest && request.ContentId == contentId;
            try
            {
                completedSave.Task.GetAwaiter().GetResult();
                if (latest) unsavedChoices.Remove(request.ContentId);
                if (current) { SaveFailed = false; ErrorMessage = null; }
            }
            catch (Exception ex) when (IsExpectedFailure(ex))
            {
                if (current)
                {
                    SaveFailed = true;
                    ErrorMessage = "Darstellung konnte nicht gespeichert werden. Die Auswahl bleibt für diese Sitzung aktiv.";
                }
            }
        }
        StartNextSave();
    }

    public void Retry()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!HasCharacter) return;
        if (SaveFailed)
        {
            SaveFailed = false;
            ErrorMessage = null;
            QueueSave();
        }
        else if (LoadFailed && !IsLoading) StartLoad();
    }

    private static bool IsExpectedFailure(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or
        ArgumentException or InvalidOperationException or OperationCanceledException;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        foreach (var operation in loads) operation.Cancellation.Cancel();
        var sources = loads.Select(operation => operation.Cancellation).ToArray();
        var pending = loads.Select(operation => (Task)operation.Task).ToList();
        if (activeSave is not null) pending.Add(activeSave.Task);
        queuedSaves.Clear();
        DisposalCompletion = ObserveCompletionAsync(pending, sources);
    }

    private async Task ObserveCompletionAsync(IEnumerable<Task> pending, IEnumerable<CancellationTokenSource> sources)
    {
        try { await Task.WhenAll(pending).ConfigureAwait(false); }
        catch (Exception) { /* Unload observes pending failures without logging character data. */ }
        finally
        {
            foreach (var source in sources) source.Dispose();
            lifetime.Dispose();
        }
    }

    private sealed record LoadOperation(long ContextVersion, long ChoiceVersion, CancellationTokenSource Cancellation,
        Task<ThemePreferences> Task);
    private sealed record SaveRequest(ulong ContentId, long ContextVersion, long ChoiceVersion, ThemePreferences Preferences);
    private sealed record SaveOperation(SaveRequest Request, Task Task);
}
