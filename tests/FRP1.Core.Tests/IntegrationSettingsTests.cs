using FRP1.Plugin.Integrations;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class IntegrationSettingsTests
{
    [Fact]
    public async Task MissingSettingsDefaultOffWithoutWritingAndExplicitChoiceRoundTripsLocally()
    {
        using var directory = new TestDirectory();
        var store = new IntegrationSettingsStore(directory.Path);
        Assert.Equal(new IntegrationSettings(), await store.LoadAsync());
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
        await store.SaveAsync(new() { HonorificEnabled = true });
        Assert.True((await new IntegrationSettingsStore(directory.Path).LoadAsync()).HonorificEnabled);
        var path = Assert.Single(Directory.EnumerateFiles(directory.Path));
        Assert.Equal("integrations.json", Path.GetFileName(path));
        Assert.Equal("{\"schemaVersion\":1,\"honorificEnabled\":true}", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"honorificEnabled\":false}")]
    [InlineData("{\"schemaVersion\":2,\"honorificEnabled\":false}")]
    [InlineData("{\"schemaVersion\":1,\"honorificEnabled\":1}")]
    [InlineData("{\"schemaVersion\":1,\"honorificEnabled\":null}")]
    [InlineData("{\"schemaVersion\":1,\"honorificEnabled\":true,\"extra\":0}")]
    [InlineData("{\"schemaVersion\":1,\"honorificEnabled\":true,\"honorificEnabled\":false}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"honorificEnabled\":true}")]
    [InlineData("{\"schemaVersion\":1,\"honorificEnabled\":false,}")]
    public async Task InvalidLocalSettingsAreRejectedWithoutOverwritingFile(string json)
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "integrations.json");
        await File.WriteAllTextAsync(path, json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new IntegrationSettingsStore(directory.Path).LoadAsync());
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ReadSizeBoundaryIsBoundedAndOversizedUtf8IsRejected()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "integrations.json");
        var store = new IntegrationSettingsStore(directory.Path);
        var boundary = "{\"schemaVersion\":1,\"honorificEnabled\":false}".PadRight(IntegrationSettingsStore.MaximumBytes);
        await File.WriteAllTextAsync(path, boundary);
        Assert.False((await store.LoadAsync()).HonorificEnabled);
        await File.WriteAllTextAsync(path, boundary + " ");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Fact]
    public async Task CancelledAndInvalidSavesPreservePreviousAtomicDocumentAndLeaveNoTemporaryFiles()
    {
        using var directory = new TestDirectory();
        var store = new IntegrationSettingsStore(directory.Path);
        await store.SaveAsync(new() { HonorificEnabled = true });
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(new(), cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.LoadAsync(cancelled.Token));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new() { SchemaVersion = 2 }));
        Assert.True((await store.LoadAsync()).HonorificEnabled);
        Assert.Equal("integrations.json", Path.GetFileName(Assert.Single(Directory.EnumerateFiles(directory.Path))));
    }

    [Fact]
    public async Task ConcurrentSavesAndReadsNeverExposePartialJsonAndFinalExplicitWriteWins()
    {
        using var directory = new TestDirectory();
        var store = new IntegrationSettingsStore(directory.Path);
        await store.SaveAsync(new());
        await Task.WhenAll(Enumerable.Range(0, 20).Select(async index =>
        {
            await store.SaveAsync(new() { HonorificEnabled = index % 2 == 0 });
            Assert.Equal(1, (await store.LoadAsync()).SchemaVersion);
        }));
        await store.SaveAsync(new() { HonorificEnabled = true });
        Assert.True((await store.LoadAsync()).HonorificEnabled);
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }
}
