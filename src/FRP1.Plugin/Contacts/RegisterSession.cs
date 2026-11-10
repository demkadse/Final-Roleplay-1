namespace FRP1.Plugin.Contacts;

public readonly record struct PeerBlockChange(string Fingerprint, bool Blocked);

// The caller owns the UI thread; workers receive detached immutable snapshots only.
public sealed class RegisterSession : IDisposable
{
    private readonly Func<CancellationToken, Task<ContactRegister>> load;
    private readonly Func<ContactRegister, CancellationToken, Task> save;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<string, bool> blockChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> runtimeBlocks = new(StringComparer.OrdinalIgnoreCase);
    private ContactRegister committed = ContactData.Copy(new());
    private ContactRegister desired = ContactData.Copy(new());
    private Task<ContactRegister>? loading;
    private Task? saving;
    private ContactRegister? savingSnapshot;
    private bool disposed;

    public RegisterSession(ContactStore store) : this(store.LoadAsync, store.SaveAsync) { }

    public RegisterSession(Func<CancellationToken, Task<ContactRegister>> load,
        Func<ContactRegister, CancellationToken, Task> save)
    {
        this.load = load ?? throw new ArgumentNullException(nameof(load));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        Reload();
    }

    public long Version { get; private set; }
    public bool IsLoaded { get; private set; }
    public bool IsBusy => loading is not null || saving is not null;
    public bool IsDirty { get; private set; }
    public bool LoadFailed { get; private set; }
    public bool SaveFailed { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Task DisposalCompletion { get; private set; } = Task.CompletedTask;
    public ContactRegister Snapshot => ContactData.Copy(desired);

    public void Reload()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsBusy || IsDirty) throw new InvalidOperationException("Finish the current local edit before reloading.");
        IsLoaded = false;
        LoadFailed = false;
        ErrorMessage = null;
        var token = lifetime.Token;
        loading = Task.Run(() => load(token), token);
        Version++;
    }

    public void Poll()
    {
        if (disposed) return;
        if (loading is { IsCompleted: true } completedLoad)
        {
            loading = null;
            try
            {
                committed = ContactData.Copy(completedLoad.GetAwaiter().GetResult());
                desired = ContactData.Copy(committed);
                IsLoaded = true;
                LoadFailed = false;
                foreach (var peer in committed.BlockedPeers) blockChanges[peer] = true;
                ErrorMessage = null;
            }
            catch (Exception ex) when (Expected(ex))
            {
                IsLoaded = false;
                LoadFailed = true;
                ErrorMessage = "Das lokale Kontaktregister konnte nicht geladen werden. Bitte erneut versuchen.";
            }
            Version++;
        }
        if (saving is { IsCompleted: true } completedSave)
        {
            saving = null;
            var snapshot = savingSnapshot!;
            savingSnapshot = null;
            try
            {
                completedSave.GetAwaiter().GetResult();
                var previous = committed.BlockedPeers.Concat(runtimeBlocks).ToHashSet(StringComparer.OrdinalIgnoreCase);
                committed = snapshot;
                desired = ContactData.Copy(snapshot);
                foreach (var peer in previous.Except(committed.BlockedPeers, StringComparer.OrdinalIgnoreCase))
                    blockChanges[peer] = false;
                runtimeBlocks.Clear();
                IsDirty = SaveFailed = false;
                ErrorMessage = null;
            }
            catch (Exception ex) when (Expected(ex))
            {
                SaveFailed = IsDirty = true;
                ErrorMessage = "Die Kontaktänderung ist nur für diese Sitzung aktiv. Speichern erneut versuchen.";
            }
            Version++;
        }
    }

    public Task SetContactAsync(LocalContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        ContactData.ValidateKey(contact.Profile);
        return Change(data => data with
        {
            Contacts = data.Contacts.Where(c => c.Profile.Canonical() != contact.Profile.Canonical()).Append(contact).ToArray(),
        });
    }

    public Task SetCharacterNoteAsync(CharacterKey character, string note)
    {
        ContactData.ValidateCharacter(character);
        return Change(data => data with
        {
            CharacterNotes = data.CharacterNotes.Where(n => !ContactData.SameCharacter(n.Character, character))
                .Append(new CharacterNote(character, note)).ToArray(),
        });
    }

    public Task ConfirmBindingAsync(ProfileKey profile, CharacterKey character)
    {
        ContactData.ValidateKey(profile);
        ContactData.ValidateCharacter(character);
        var contact = GetContact(profile) ?? new LocalContact { Profile = profile };
        return SetContactAsync(contact with { Binding = new CharacterBinding(character, true) });
    }

    public Task ClearBindingAsync(ProfileKey profile)
    {
        var contact = GetContact(profile) ?? throw new InvalidOperationException("Unknown local contact.");
        return SetContactAsync(contact with { Binding = null });
    }

    public Task SetPeerBlockedAsync(string fingerprint, bool blocked)
    {
        EnsureEditable();
        ContactData.ValidateKey(new ProfileKey(fingerprint, Guid.NewGuid()));
        var peer = fingerprint.ToUpperInvariant();
        if (blockChanges.Count >= 512 && !blockChanges.ContainsKey(peer))
            throw new InvalidOperationException("Apply pending network changes before changing more blocks.");
        var candidate = ContactData.Copy(desired with
        {
            BlockedPeers = desired.BlockedPeers.Where(p => !string.Equals(p, peer, StringComparison.OrdinalIgnoreCase))
                .Concat(blocked ? new[] { peer } : Array.Empty<string>()).ToArray(),
        });
        if (blocked)
        {
            // Blocking takes effect before storage; a failed save must never re-expose the peer.
            runtimeBlocks.Add(peer);
            blockChanges[peer] = true;
        }
        return Change(_ => candidate);
    }

    public Task RecordEncounterAsync(ProfileKey profile, CharacterKey? character, EncounterKind kind)
    {
        EnsureEditable();
        ContactData.ValidateKey(profile);
        if (character is { } key) ContactData.ValidateCharacter(key);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var now = DateTimeOffset.UtcNow;
        var delay = kind == EncounterKind.Seen ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(10);
        if (desired.Encounters.Any(e => e.Profile.Canonical() == profile.Canonical() && e.Kind == kind && now - e.At < delay))
            return Task.CompletedTask;
        return Change(data => data with
        {
            Encounters = data.Encounters.Append(new EncounterEntry(profile, character, kind, now))
                .OrderByDescending(e => e.At).Take(ContactData.MaximumEncounters).ToArray(),
        });
    }

    public LocalContact? GetContact(ProfileKey profile)
    {
        ContactData.ValidateKey(profile);
        return desired.Contacts.FirstOrDefault(c => c.Profile.Canonical() == profile.Canonical()) is { } contact
            ? contact with { Binding = contact.Binding is { } binding ? binding with { } : null } : null;
    }

    public string GetCharacterNote(CharacterKey character)
    {
        ContactData.ValidateCharacter(character);
        return desired.CharacterNotes.FirstOrDefault(n => ContactData.SameCharacter(n.Character, character))?.Note ?? "";
    }

    public IReadOnlyList<ProfileKey> FindBindings(CharacterKey character)
    {
        ContactData.ValidateCharacter(character);
        return Array.AsReadOnly(desired.Contacts.Where(c => c.Binding is { Confirmed: true } binding &&
            ContactData.SameCharacter(binding.Character, character)).Select(c => c.Profile.Canonical()).ToArray());
    }

    public bool IsPeerBlocked(string fingerprint) => runtimeBlocks.Contains(fingerprint) ||
        committed.BlockedPeers.Contains(fingerprint, StringComparer.OrdinalIgnoreCase) ||
        desired.BlockedPeers.Contains(fingerprint, StringComparer.OrdinalIgnoreCase);

    public bool TryDequeueNetworkBlockChange(out PeerBlockChange change)
    {
        if (blockChanges.Count == 0) { change = default; return false; }
        var first = blockChanges.First();
        blockChanges.Remove(first.Key);
        change = new PeerBlockChange(first.Key, first.Value);
        return true;
    }

    public void Retry()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsBusy) return;
        if (LoadFailed) Reload();
        else if (SaveFailed && IsDirty) _ = StartSave();
    }

    private Task Change(Func<ContactRegister, ContactRegister> mutation)
    {
        EnsureEditable();
        var candidate = ContactData.Copy(mutation(desired));
        desired = candidate;
        IsDirty = true;
        SaveFailed = false;
        ErrorMessage = null;
        Version++;
        return StartSave();
    }

    private Task StartSave()
    {
        savingSnapshot = ContactData.Copy(desired);
        var snapshot = savingSnapshot;
        var token = lifetime.Token;
        saving = Task.Run(() => save(snapshot, token), token);
        return saving;
    }

    private void EnsureEditable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!IsLoaded || IsBusy) throw new InvalidOperationException("The local register is not ready.");
    }

    private static bool Expected(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidDataException or
        ArgumentException or InvalidOperationException or OperationCanceledException;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        IsLoaded = false;
        Version++;
        lifetime.Cancel();
        var pending = new List<Task>();
        if (loading is not null) pending.Add(loading);
        if (saving is not null) pending.Add(saving);
        DisposalCompletion = Task.WhenAll(pending).ContinueWith(task =>
        {
            _ = task.Exception;
            lifetime.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
