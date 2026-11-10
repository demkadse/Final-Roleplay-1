using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using FRP1.Plugin.Contacts;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class ContactRegisterTests
{
    [Fact]
    public async Task PrivateNotesRelationshipsAndBindingsPersistAsDetachedLocalData()
    {
        using var directory = new TestDirectory();
        var contacts = new List<LocalContact> { Contact() with { Favorite = true, ProfileNote = "Private note", Relationship = RelationshipKind.Friend,
            Binding = new(new("Same Name", 1), true) } };
        var register = new ContactRegister
        {
            Contacts = contacts, CharacterNotes = [new(new("Same Name", 2), "Other world note")],
            BlockedPeers = [new string('B', 64)], Encounters = [new(contacts[0].Profile, new("Same Name", 1), EncounterKind.Seen, DateTimeOffset.UtcNow.AddMinutes(-2))],
        };
        var store = new ContactStore(directory.Path);
        var saving = store.SaveAsync(register);
        contacts.Clear();
        await saving;
        var reopened = await new ContactStore(directory.Path).LoadAsync();
        Assert.Equal("Private note", Assert.Single(reopened.Contacts).ProfileNote);
        Assert.Equal(RelationshipKind.Friend, reopened.Contacts[0].Relationship);
        Assert.Equal("Other world note", Assert.Single(reopened.CharacterNotes).Note);
        var collection = Assert.IsAssignableFrom<ICollection<LocalContact>>(reopened.Contacts);
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
        Assert.Equal("contacts.json", Path.GetFileName(Assert.Single(Directory.EnumerateFiles(directory.Path))));
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("contacts")]
    [InlineData("characterNotes")]
    [InlineData("blockedPeers")]
    [InlineData("encounters")]
    public async Task RegisterFieldsAreRequired(string property)
    {
        using var directory = new TestDirectory();
        var store = new ContactStore(directory.Path);
        await store.SaveAsync(new());
        var path = Path.Combine(directory.Path, "contacts.json");
        var json = JsonNode.Parse(await File.ReadAllBytesAsync(path))!.AsObject();
        json.Remove(property);
        await File.WriteAllTextAsync(path, json.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":1,\"contacts\":null,\"characterNotes\":[],\"blockedPeers\":[],\"encounters\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"contacts\":[],\"characterNotes\":[],\"blockedPeers\":[],\"encounters\":[],\"unknown\":true}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"contacts\":[],\"characterNotes\":[],\"blockedPeers\":[],\"encounters\":[]}")]
    public async Task MalformedRegistersAreRejected(string json)
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "contacts.json"), json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new ContactStore(directory.Path).LoadAsync());
    }

    [Fact]
    public void InvalidKeysBindingsNotesRelationshipsAndTimesCannotEnterRegister()
    {
        var contact = Contact();
        Assert.Throws<InvalidDataException>(() => ContactData.Copy(new() { Contacts = [contact with { Profile = new("not-a-pin", Guid.NewGuid()) }] }));
        Assert.Throws<InvalidDataException>(() => ContactData.Copy(new() { Contacts = [contact with { Binding = new(new("Name", 1), false) }] }));
        Assert.Throws<InvalidDataException>(() => ContactData.Copy(new() { Contacts = [contact with { Relationship = RelationshipKind.Custom }] }));
        Assert.Throws<InvalidDataException>(() => ContactData.Copy(new() { Contacts = [contact with { Relationship = (RelationshipKind)999 }] }));
        Assert.Throws<InvalidDataException>(() => ContactData.Copy(new() { Contacts = [contact with { ProfileNote = new string('x', 8193) }] }));
        Assert.Throws<InvalidDataException>(() => ContactData.Copy(new() { Contacts = [contact, contact with { Profile = contact.Profile with { Fingerprint = contact.Profile.Fingerprint.ToLowerInvariant() } }] }));
        Assert.Throws<InvalidDataException>(() => ContactData.Copy(new() { CharacterNotes = [new(new("Name", 0), "Note")] }));
        Assert.Throws<InvalidDataException>(() => ContactData.Copy(new() { Encounters = [new(contact.Profile, null, EncounterKind.Seen, DateTimeOffset.UtcNow.AddDays(1))] }));
        Assert.False(ContactData.SameCharacter(new("Same Name", 1), new("Same Name", 2)));
        Assert.True(ContactData.SameCharacter(new(" same Name ", 1), new("Same Name", 1)));
    }

    [Fact]
    public void RegisterCollectionsRemainBounded()
    {
        var contacts = Enumerable.Range(0, ContactData.MaximumContacts).Select(_ => Contact()).ToArray();
        Assert.Equal(ContactData.MaximumContacts, ContactData.Copy(new() { Contacts = contacts }).Contacts.Count);
        Assert.Throws<InvalidDataException>(() => ContactData.Copy(new() { Contacts = contacts.Append(Contact()).ToArray() }));
        var blocks = Enumerable.Range(1, ContactData.MaximumBlockedPeers).Select(i => i.ToString("X64")).ToArray();
        Assert.Equal(ContactData.MaximumBlockedPeers, ContactData.Copy(new() { BlockedPeers = blocks }).BlockedPeers.Count);
        Assert.Throws<InvalidDataException>(() => ContactData.Copy(new() { BlockedPeers = blocks.Append(new string('F', 64)).ToArray() }));
    }

    [Fact]
    public async Task CancelledAndOversizedRegisterWritesPreserveExistingData()
    {
        using var directory = new TestDirectory();
        var store = new ContactStore(directory.Path);
        await store.SaveAsync(new() { Contacts = [Contact()] });
        var path = Path.Combine(directory.Path, "contacts.json");
        var before = await File.ReadAllBytesAsync(path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(new(), cancellation.Token));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Single(Directory.EnumerateFiles(directory.Path));
        await File.WriteAllTextAsync(path, new string(' ', ContactStore.MaximumBytes + 1));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Fact]
    public async Task RegisterLoadFailureDisallowsEditsAndRetryPublishesOnlyValidatedSnapshot()
    {
        var attempts = new ConcurrentQueue<TaskCompletionSource<ContactRegister>>();
        using var session = new RegisterSession(_ =>
        {
            var completion = new TaskCompletionSource<ContactRegister>(TaskCreationOptions.RunContinuationsAsynchronously);
            attempts.Enqueue(completion);
            return completion.Task;
        }, (_, _) => Task.CompletedTask);
        await UntilAsync(session, () => attempts.Count == 1);
        Assert.Throws<InvalidOperationException>(() => { _ = session.SetContactAsync(Contact()); });
        attempts.ToArray()[0].SetException(new InvalidDataException("Synthetic corrupt register"));
        await UntilAsync(session, () => session.LoadFailed);
        Assert.False(session.IsLoaded);
        Assert.Throws<InvalidOperationException>(() => { _ = session.SetCharacterNoteAsync(new("Name", 1), "Note"); });
        session.Retry();
        await UntilAsync(session, () => attempts.Count == 2);
        Assert.Throws<InvalidOperationException>(() => { _ = session.SetContactAsync(Contact()); });
        attempts.ToArray()[1].SetResult(new() { Contacts = [Contact()] });
        await UntilAsync(session, () => session.IsLoaded && !session.IsBusy);
        Assert.Single(session.Snapshot.Contacts);
        session.Dispose();
        await session.DisposalCompletion;
    }

    [Fact]
    public async Task FailedBlockIsImmediateButUnblockingWaitsForSuccessfulCommit()
    {
        var writes = new ConcurrentQueue<RegisterWrite>();
        using var session = new RegisterSession(_ => Task.FromResult(new ContactRegister()), (snapshot, _) =>
        {
            var write = new RegisterWrite(snapshot);
            writes.Enqueue(write);
            return write.Completion.Task;
        });
        await UntilAsync(session, () => session.IsLoaded);
        var pin = new string('A', 64);
        var blocked = session.SetPeerBlockedAsync(pin, true);
        Assert.True(session.IsPeerBlocked(pin));
        Assert.True(session.TryDequeueNetworkBlockChange(out var immediate));
        Assert.True(immediate.Blocked);
        await UntilAsync(session, () => writes.Count == 1);
        writes.ToArray()[0].Completion.SetException(new IOException("Synthetic failed save"));
        await Assert.ThrowsAsync<IOException>(() => blocked);
        await UntilAsync(session, () => session.SaveFailed);
        Assert.True(session.IsPeerBlocked(pin));
        var unblock = session.SetPeerBlockedAsync(pin, false);
        Assert.True(session.IsPeerBlocked(pin));
        Assert.False(session.TryDequeueNetworkBlockChange(out _));
        await UntilAsync(session, () => writes.Count == 2);
        writes.ToArray()[1].Completion.SetResult();
        await unblock;
        await UntilAsync(session, () => !session.IsBusy);
        Assert.False(session.IsPeerBlocked(pin));
        Assert.True(session.TryDequeueNetworkBlockChange(out var committed));
        Assert.False(committed.Blocked);
        session.Dispose();
        await session.DisposalCompletion;
    }

    [Fact]
    public async Task DirtyNotesSurviveFailedSaveAndSubsequentEditAndRetry()
    {
        var writes = new ConcurrentQueue<RegisterWrite>();
        using var session = new RegisterSession(_ => Task.FromResult(new ContactRegister()), (snapshot, _) =>
        {
            var write = new RegisterWrite(snapshot);
            writes.Enqueue(write);
            return write.Completion.Task;
        });
        await UntilAsync(session, () => session.IsLoaded);
        var contact = Contact() with { ProfileNote = "First unsaved note" };
        var first = session.SetContactAsync(contact);
        await UntilAsync(session, () => writes.Count == 1);
        writes.ToArray()[0].Completion.SetException(new IOException("Synthetic failed save"));
        await Assert.ThrowsAsync<IOException>(() => first);
        await UntilAsync(session, () => session.SaveFailed);
        Assert.Throws<InvalidOperationException>(() => session.Reload());
        var second = session.SetCharacterNoteAsync(new("Name", 1), "Second note");
        await UntilAsync(session, () => writes.Count == 2);
        var pending = writes.ToArray()[1];
        Assert.Equal("First unsaved note", Assert.Single(pending.Snapshot.Contacts).ProfileNote);
        Assert.Equal("Second note", Assert.Single(pending.Snapshot.CharacterNotes).Note);
        pending.Completion.SetException(new IOException("Synthetic second failed save"));
        await Assert.ThrowsAsync<IOException>(() => second);
        await UntilAsync(session, () => session.SaveFailed);
        session.Retry();
        await UntilAsync(session, () => writes.Count == 3);
        var retry = writes.ToArray()[2];
        Assert.Equal("First unsaved note", Assert.Single(retry.Snapshot.Contacts).ProfileNote);
        Assert.Equal("Second note", Assert.Single(retry.Snapshot.CharacterNotes).Note);
        Assert.Throws<InvalidOperationException>(() => { _ = session.SetContactAsync(Contact()); });
        retry.Completion.SetResult();
        await UntilAsync(session, () => !session.IsBusy);
        Assert.False(session.IsDirty);
        Assert.Equal("First unsaved note", session.GetContact(contact.Profile)!.ProfileNote);
        Assert.Equal("Second note", session.GetCharacterNote(new("Name", 1)));
        session.Dispose();
        await session.DisposalCompletion;
    }

    [Fact]
    public async Task EncountersAreThrottledAndOldestEntriesAreBounded()
    {
        var key = Contact().Profile;
        var initial = new ContactRegister { Encounters = Enumerable.Range(0, ContactData.MaximumEncounters)
            .Select(i => new EncounterEntry(new(new string('A', 64), Guid.NewGuid()), null, EncounterKind.Seen, DateTimeOffset.UtcNow.AddHours(-1).AddMilliseconds(i))).ToArray() };
        using var session = new RegisterSession(_ => Task.FromResult(initial), (_, _) => Task.CompletedTask);
        await UntilAsync(session, () => session.IsLoaded);
        await session.RecordEncounterAsync(key, null, EncounterKind.ProfileOpened);
        await UntilAsync(session, () => !session.IsBusy);
        Assert.Equal(ContactData.MaximumEncounters, session.Snapshot.Encounters.Count);
        Assert.Equal(key, session.Snapshot.Encounters[0].Profile);
        var version = session.Version;
        await session.RecordEncounterAsync(key, null, EncounterKind.ProfileOpened);
        Assert.Equal(version, session.Version);
        session.Dispose();
        await session.DisposalCompletion;
    }

    internal static LocalContact Contact(char pin = 'A') => new() { Profile = new(new string(pin, 64), Guid.NewGuid()) };
    internal static async Task UntilAsync(RegisterSession session, Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            session.Poll();
            if (condition()) return;
            await Task.Delay(5, timeout.Token);
        }
    }
    private sealed record RegisterWrite(ContactRegister Snapshot)
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
