using System.Diagnostics;
using FRP1.Core.Persistence;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class CacheRemovalTests
{
    [Fact]
    public async Task RemovingOnePeerPreservesOtherCacheAndOwnProfilesAndMissingPeerIsHarmless()
    {
        using var directory = new TestDirectory();
        var store = new ProfileStore(directory.Path);
        var first = ProfileTests.Valid();
        var other = ProfileTests.Valid();
        var pin = new string('A', 64);
        var otherPin = new string('B', 64);
        await store.SaveLocalAsync(first);
        await store.SaveCachedAsync(pin, first);
        await store.SaveCachedAsync(otherPin, other);
        var removed = await store.RemoveCachedPeerAsync(pin.ToLowerInvariant());
        Assert.Equal(1, removed.RemovedProfiles);
        Assert.True(removed.PeerDirectoryRemoved);
        Assert.Null(await store.GetCachedAsync(pin, first.Id));
        Assert.NotNull(await store.GetCachedAsync(otherPin, other.Id));
        Assert.NotNull(await store.LoadLocalAsync(first.Id));
        var summary = Assert.Single(await store.ListCachedPeersAsync());
        Assert.Equal(otherPin, summary.Fingerprint);
        Assert.Equal(1, summary.ProfileCount);
        var missing = await store.RemoveCachedPeerAsync(pin);
        Assert.Equal(0, missing.RemovedProfiles);
        Assert.False(missing.PeerDirectoryRemoved);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("")]
    [InlineData("not-a-fingerprint")]
    public async Task InvalidPeerReferencesCannotTouchUnrelatedFiles(string pin)
    {
        using var directory = new TestDirectory();
        var sentinel = Path.Combine(directory.Path, "unrelated.txt");
        await File.WriteAllTextAsync(sentinel, "Synthetic sentinel");
        await Assert.ThrowsAsync<InvalidDataException>(() => new ProfileStore(directory.Path).RemoveCachedPeerAsync(pin));
        Assert.Equal("Synthetic sentinel", await File.ReadAllTextAsync(sentinel));
    }

    [Theory]
    [InlineData("unexpected-file")]
    [InlineData("invalid-json-name")]
    [InlineData("subdirectory")]
    public async Task UnexpectedChildrenRefuseEntireRemovalBeforeDeletingValidProfiles(string kind)
    {
        using var directory = new TestDirectory();
        var store = new ProfileStore(directory.Path);
        var profile = ProfileTests.Valid();
        var pin = new string('A', 64);
        await store.SaveCachedAsync(pin, profile);
        var peerDirectory = Path.Combine(directory.Path, "cache", pin);
        var unexpected = Path.Combine(peerDirectory, kind == "invalid-json-name" ? "not-a-guid.json" : "unrelated");
        if (kind == "subdirectory") Directory.CreateDirectory(unexpected);
        else await File.WriteAllTextAsync(unexpected, "Synthetic unexpected content");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.RemoveCachedPeerAsync(pin));
        Assert.NotNull(await store.GetCachedAsync(pin, profile.Id));
        Assert.True(File.Exists(unexpected) || Directory.Exists(unexpected));
    }

    [Fact]
    public async Task CancelledRemovalPreservesAllDataAndMetadataListingHonorsCancellation()
    {
        using var directory = new TestDirectory();
        var store = new ProfileStore(directory.Path);
        var profile = ProfileTests.Valid();
        var pin = new string('A', 64);
        await store.SaveCachedAsync(pin, profile);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.RemoveCachedPeerAsync(pin, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ListCachedPeersAsync(cancellation.Token));
        Assert.NotNull(await store.GetCachedAsync(pin, profile.Id));
        Assert.Single(await store.ListCachedPeersAsync());
    }

    [Fact]
    public async Task ExplicitRemovalReleasesSixteenthCacheSlot()
    {
        using var directory = new TestDirectory();
        var store = new ProfileStore(directory.Path);
        for (var i = 1; i <= 16; i++) await store.SaveCachedAsync(i.ToString("X64"), ProfileTests.Valid());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveCachedAsync(17.ToString("X64"), ProfileTests.Valid()));
        await store.RemoveCachedPeerAsync(1.ToString("X64"));
        await store.SaveCachedAsync(17.ToString("X64"), ProfileTests.Valid());
        Assert.Equal(16, (await store.ListCachedPeersAsync()).Count);
        Assert.DoesNotContain(await store.ListCachedPeersAsync(), peer => peer.Fingerprint == 1.ToString("X64"));
    }

    [Fact]
    public async Task ConcurrentSaveAndRemoveSerializeIntoOneCompleteOutcome()
    {
        using var directory = new TestDirectory();
        var store = new ProfileStore(directory.Path);
        var pin = new string('A', 64);
        var old = ProfileTests.Valid();
        var next = ProfileTests.Valid();
        await store.SaveCachedAsync(pin, old);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var removing = Task.Run(async () => { await start.Task; return await store.RemoveCachedPeerAsync(pin); });
        var saving = Task.Run(async () => { await start.Task; await store.SaveCachedAsync(pin, next); });
        start.SetResult();
        await Task.WhenAll(removing, saving);
        Assert.Null(await store.GetCachedAsync(pin, old.Id));
        var entries = await store.ListCachedAsync();
        Assert.InRange(entries.Count, 0, 1);
        if (entries.Count == 1) Assert.Equal(next.Id, entries[0].Profile.Id);
    }

    [Fact]
    public async Task DirectoryJunctionIsRefusedAndItsTargetRemainsUntouched()
    {
        using var directory = new TestDirectory();
        var pin = new string('A', 64);
        var cacheRoot = Path.Combine(directory.Path, "cache");
        var junction = Path.Combine(cacheRoot, pin);
        var target = Path.Combine(directory.Path, "unrelated-target");
        Directory.CreateDirectory(cacheRoot);
        Directory.CreateDirectory(target);
        var sentinel = Path.Combine(target, Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(sentinel, "Synthetic untouched target");
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var part in new[] { "/c", "mklink", "/J", junction, target }) start.ArgumentList.Add(part);
        using (var process = Process.Start(start)!)
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, process.ExitCode);
        }
        try
        {
            Assert.True((File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0);
            var store = new ProfileStore(directory.Path);
            await Assert.ThrowsAsync<InvalidDataException>(() => store.RemoveCachedPeerAsync(pin));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.ListCachedPeersAsync());
            Assert.Equal("Synthetic untouched target", await File.ReadAllTextAsync(sentinel));
        }
        finally
        {
            var resolved = Path.GetFullPath(junction);
            Assert.StartsWith(Path.GetFullPath(directory.Path) + Path.DirectorySeparatorChar, resolved, StringComparison.OrdinalIgnoreCase);
            // Delete only this test-created junction, never recurse through its target.
            Directory.Delete(resolved, recursive: false);
        }
        Assert.True(File.Exists(sentinel));
    }
}
