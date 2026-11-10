using System.Text.Json;
using FRP1.Core.Safety;
using FRP1.Plugin;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class UsageAcknowledgementTests
{
    [Fact]
    public void CanonicalGuidelinesHaveVersionAndNonemptyNotices()
    {
        Assert.True(UsageGuidelines.CurrentVersion > 0);
        Assert.NotEmpty(UsageGuidelines.Notices);
        Assert.All(UsageGuidelines.Notices, notice => Assert.False(string.IsNullOrWhiteSpace(notice)));
    }

    [Fact]
    public async Task MissingRecordIsUnacknowledgedAndReadingCreatesNoFile()
    {
        using var directory = new TestDirectory();
        Assert.False(await new UsageAcknowledgementStore(directory.Path).IsAcknowledgedAsync());
        Assert.Empty(Directory.EnumerateFiles(directory.Path));
    }

    [Fact]
    public async Task AcknowledgementPersistsOnlyCurrentVersionAndReloads()
    {
        using var directory = new TestDirectory();
        await new UsageAcknowledgementStore(directory.Path).AcknowledgeAsync();
        Assert.True(await new UsageAcknowledgementStore(directory.Path).IsAcknowledgedAsync());
        var file = Assert.Single(Directory.EnumerateFiles(directory.Path));
        Assert.Equal("usage-notices.json", Path.GetFileName(file));
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(file));
        var property = Assert.Single(json.RootElement.EnumerateObject());
        Assert.Equal("version", property.Name);
        Assert.Equal(UsageGuidelines.CurrentVersion, property.Value.GetInt32());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"version\":0}")]
    [InlineData("{\"version\":2}")]
    [InlineData("{\"version\":-1}")]
    [InlineData("{\"version\":\"1\"}")]
    [InlineData("{\"version\":1.5}")]
    [InlineData("{\"version\":null}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("{\"version\":1,\"age\":18}")]
    [InlineData("{\"version\":1,\"version\":1}")]
    public async Task InvalidOrDifferentVersionRecordsNeverCountAsAcknowledged(string content)
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "usage-notices.json");
        await File.WriteAllTextAsync(path, content);
        Assert.False(await new UsageAcknowledgementStore(directory.Path).IsAcknowledgedAsync());
        Assert.Equal(content, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task OversizedOtherwiseValidRecordIsNotAccepted()
    {
        using var directory = new TestDirectory();
        var content = JsonSerializer.Serialize(new { version = UsageGuidelines.CurrentVersion }) + new string(' ', 1024 * 1024);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "usage-notices.json"), content);
        Assert.False(await new UsageAcknowledgementStore(directory.Path).IsAcknowledgedAsync());
    }

    [Fact]
    public async Task CancelledFirstAcknowledgementLeavesUnacceptedAndNoTemporaryFile()
    {
        using var directory = new TestDirectory();
        var store = new UsageAcknowledgementStore(directory.Path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.AcknowledgeAsync(cancellation.Token));
        Assert.False(await store.IsAcknowledgedAsync());
        Assert.Empty(Directory.EnumerateFiles(directory.Path));
    }

    [Fact]
    public async Task CancelledWritePreservesExistingValidRecord()
    {
        using var directory = new TestDirectory();
        var store = new UsageAcknowledgementStore(directory.Path);
        await store.AcknowledgeAsync();
        var before = await File.ReadAllBytesAsync(Path.Combine(directory.Path, "usage-notices.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.AcknowledgeAsync(cancellation.Token));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(directory.Path, "usage-notices.json")));
        Assert.True(await new UsageAcknowledgementStore(directory.Path).IsAcknowledgedAsync());
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledReadsHonorCancellation(bool hasValidAcknowledgement)
    {
        using var directory = new TestDirectory();
        var store = new UsageAcknowledgementStore(directory.Path);
        if (hasValidAcknowledgement) await store.AcknowledgeAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.IsAcknowledgedAsync(cancellation.Token));
    }

    [Fact]
    public async Task AcceptedNoticesNeverAuthorizePersistedAdultPreferencesInNewSession()
    {
        using var directory = new TestDirectory();
        await new AudienceSettings(directory.Path).SaveAsync(new AudiencePreferences(AudienceSelection.Adult, true), CancellationToken.None);
        await new UsageAcknowledgementStore(directory.Path).AcknowledgeAsync();
        Assert.True(await new UsageAcknowledgementStore(directory.Path).IsAcknowledgedAsync());
        var session = await new AudienceSettings(directory.Path).LoadForSessionAsync(CancellationToken.None);
        Assert.Equal(new AudiencePreferences(AudienceSelection.Unspecified, false), session);
        Assert.False(new AudiencePolicy(session).AcceptsMatureContent);
    }
}
