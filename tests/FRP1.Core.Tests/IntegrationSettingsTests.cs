using System.Text.Json;
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
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(json.RootElement.GetProperty("honorificEnabled").GetBoolean());
        foreach (var name in IpcFields) Assert.False(json.RootElement.GetProperty(name).GetBoolean());
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

    [Fact]
    public async Task AtomicSaveWithOpenDeleteSharingReaderPreservesOldSnapshotAndPublishesNewSettings()
    {
        using var directory = new TestDirectory();
        var store = new IntegrationSettingsStore(directory.Path);
        var original = new IntegrationSettings { HonorificEnabled = true };
        await store.SaveAsync(original);
        var path = Path.Combine(directory.Path, "integrations.json");
        var originalBytes = await File.ReadAllBytesAsync(path);
        await using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous))
        {
            for (var index = 0; index < 8; index++)
            {
                var next = new IntegrationSettings { HonorificEnabled = index % 2 == 0,
                    IpcReadEnabled = index % 2 != 0, IpcControlEnabled = true,
                    IpcSetStatusEnabled = index % 2 == 0, IpcSetAvailabilityEnabled = index % 2 != 0 };
                await store.SaveAsync(next);
                Assert.Equal(next, await store.LoadAsync());
                reader.Position = 0;
                var oldBytes = new byte[originalBytes.Length];
                await reader.ReadExactlyAsync(oldBytes);
                Assert.Equal(originalBytes, oldBytes);
                Assert.Equal(-1, reader.ReadByte());
            }
        }
        Assert.Equal("integrations.json", Path.GetFileName(Assert.Single(Directory.EnumerateFiles(directory.Path))));
    }

    [Fact]
    public async Task FailedAtomicSaveWithBlockingReaderPreservesPreviousSettingsAndLeavesNoArtifacts()
    {
        using var directory = new TestDirectory();
        var store = new IntegrationSettingsStore(directory.Path);
        var original = new IntegrationSettings { HonorificEnabled = true, IpcReadEnabled = true };
        await store.SaveAsync(original);
        var path = Path.Combine(directory.Path, "integrations.json");
        var originalBytes = await File.ReadAllBytesAsync(path);
        await using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failure = await Record.ExceptionAsync(() => store.SaveAsync(new()));
            Assert.True(failure is IOException or UnauthorizedAccessException,
                $"Expected an IO access failure; received {failure?.GetType().Name ?? "no exception"}.");
            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));
        }
        Assert.Equal(original, await store.LoadAsync());
        Assert.Equal("integrations.json", Path.GetFileName(Assert.Single(Directory.EnumerateFiles(directory.Path))));
    }

    private static readonly string[] IpcFields = ["ipcReadEnabled", "ipcControlEnabled", "ipcSetStatusEnabled",
        "ipcSetAvailabilityEnabled", "ipcOpenOwnProfileEnabled"];

    [Fact]
    public async Task ExistingHonorificOnlySchemaRemainsReadableWithoutEnablingAnyIpcCapability()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "integrations.json");
        const string original = """{"schemaVersion":1,"honorificEnabled":true}""";
        await File.WriteAllTextAsync(path, original);
        var settings = await new IntegrationSettingsStore(directory.Path).LoadAsync();
        Assert.True(settings.HonorificEnabled);
        Assert.False(settings.IpcReadEnabled || settings.IpcControlEnabled || settings.IpcSetStatusEnabled ||
            settings.IpcSetAvailabilityEnabled || settings.IpcOpenOwnProfileEnabled);
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task PartialIpcOptionsDefaultMissingFlagsOffAndRoundTripWithoutChangingHonorific()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "integrations.json");
        await File.WriteAllTextAsync(path, """{"schemaVersion":1,"honorificEnabled":true,"ipcSetStatusEnabled":true}""");
        var store = new IntegrationSettingsStore(directory.Path);
        var partial = await store.LoadAsync();
        Assert.True(partial.IpcSetStatusEnabled);
        Assert.False(partial.IpcReadEnabled || partial.IpcControlEnabled || partial.IpcSetAvailabilityEnabled ||
            partial.IpcOpenOwnProfileEnabled);
        var chosen = partial with { IpcReadEnabled = true, IpcControlEnabled = true,
            IpcSetAvailabilityEnabled = false, IpcOpenOwnProfileEnabled = true };
        await store.SaveAsync(chosen);
        Assert.Equal(chosen, await store.LoadAsync());
        Assert.True(new FileInfo(path).Length <= IntegrationSettingsStore.MaximumBytes);
    }

    public static IEnumerable<object[]> InvalidIpcFlags => IpcFields.SelectMany(setting =>
        new[] { "null", "1", "\"true\"" }.Select(value => new object[] { setting, value }));

    [Theory]
    [MemberData(nameof(InvalidIpcFlags))]
    public async Task OptionalIpcFlagsStillRequireStrictBooleanValues(string field, string value)
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "integrations.json");
        var original = "{\"schemaVersion\":1,\"honorificEnabled\":false,\"" + field + "\":" + value + "}";
        await File.WriteAllTextAsync(path, original);
        await Assert.ThrowsAsync<InvalidDataException>(() => new IntegrationSettingsStore(directory.Path).LoadAsync());
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("ipcReadEnabled")]
    [InlineData("ipcControlEnabled")]
    [InlineData("ipcSetStatusEnabled")]
    [InlineData("ipcSetAvailabilityEnabled")]
    [InlineData("ipcOpenOwnProfileEnabled")]
    public async Task DuplicateIpcFlagsCannotOverrideAnEarlierDecision(string field)
    {
        using var directory = new TestDirectory();
        var json = "{\"schemaVersion\":1,\"honorificEnabled\":false,\"" + field + "\":false,\"" + field + "\":true}";
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "integrations.json"), json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new IntegrationSettingsStore(directory.Path).LoadAsync());
    }
}
