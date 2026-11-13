using System.Text.Json;
using System.Text.Json.Nodes;
using FRP1.Core.Profiles;
using FRP1.Plugin.Integrations;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class MoodlesSettingsTests
{
    [Theory]
    [InlineData("""{"schemaVersion":1,"honorificEnabled":true}""")]
    [InlineData("""{"schemaVersion":1,"honorificEnabled":false,"ipcReadEnabled":true,"ipcControlEnabled":true,"ipcSetStatusEnabled":true,"ipcSetAvailabilityEnabled":true,"ipcOpenOwnProfileEnabled":true}""")]
    public async Task OldSchemaOneDocumentsRemainReadableWithoutEnablingOrWritingMoodles(string original)
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "integrations.json");
        await File.WriteAllTextAsync(path, original);
        var settings = await new IntegrationSettingsStore(directory.Path).LoadAsync();
        Assert.False(settings.MoodlesEnabled);
        Assert.Empty(settings.MoodlesBindings);
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task FiveSelectionsRoundTripWithNullableRatingsAndOnlyLocalIdentityHashRatingData()
    {
        using var directory = new TestDirectory();
        var store = new IntegrationSettingsStore(directory.Path);
        var bindings = Enumerable.Range(0, 5).Select(index => new MoodlesBinding(Guid.NewGuid(), new('A', 64),
            index == 0 ? null : index % 2 == 0 ? ContentRating.General : ContentRating.Mature18)).ToArray();
        var original = new IntegrationSettings { HonorificEnabled = true, IpcReadEnabled = true,
            IpcControlEnabled = true, IpcSetStatusEnabled = true, IpcSetAvailabilityEnabled = true,
            IpcOpenOwnProfileEnabled = true, MoodlesEnabled = true, MoodlesBindings = bindings };
        await store.SaveAsync(original);
        var restored = await store.LoadAsync();
        Assert.True(restored.MoodlesEnabled && restored.HonorificEnabled && restored.IpcReadEnabled && restored.IpcControlEnabled &&
            restored.IpcSetStatusEnabled && restored.IpcSetAvailabilityEnabled && restored.IpcOpenOwnProfileEnabled);
        Assert.Equal(bindings, restored.MoodlesBindings);
        var path = Assert.Single(Directory.EnumerateFiles(directory.Path));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        var stored = json.RootElement.GetProperty("moodlesBindings").EnumerateArray().ToArray();
        Assert.Equal(5, stored.Length);
        Assert.Equal(JsonValueKind.Null, stored[0].GetProperty("rating").ValueKind);
        foreach (var binding in stored)
            Assert.Equal(new[] { "contentHash", "rating", "statusId" }, binding.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.InRange(new FileInfo(path).Length, 1, 8192);
        Assert.Equal(8192, IntegrationSettingsStore.MaximumBytes);
    }

    public static IEnumerable<object[]> MalformedOptions => new[]
    {
        "\"moodlesEnabled\":null", "\"moodlesEnabled\":1", "\"moodlesEnabled\":\"true\"",
        "\"moodlesEnabled\":false,\"moodlesEnabled\":true", "\"moodlesBindings\":null",
        "\"moodlesBindings\":{}", "\"moodlesBindings\":[null]", "\"moodlesBindings\":[{}]",
        "\"moodlesBindings\":[],\"moodlesBindings\":[]",
    }.Select(json => new object[] { json });

    [Theory]
    [MemberData(nameof(MalformedOptions))]
    public async Task InvalidOptionalSettingsFailClosedAndLeaveOriginalBytesUntouched(string fragment)
    {
        using var directory = new TestDirectory();
        var json = "{\"schemaVersion\":1,\"honorificEnabled\":false," + fragment + "}";
        var path = Path.Combine(directory.Path, "integrations.json");
        await File.WriteAllTextAsync(path, json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new IntegrationSettingsStore(directory.Path).LoadAsync());
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("statusId", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("statusId", "null")]
    [InlineData("statusId", "\"bad-id\"")]
    [InlineData("contentHash", "null")]
    [InlineData("contentHash", "\"ABC\"")]
    [InlineData("rating", "999")]
    [InlineData("rating", "\"General\"")]
    [InlineData("sourceTitle", "\"Do not persist\"")]
    public async Task MalformedBindingFieldsAndUnexpectedSourceDataAreRejected(string field, string value)
    {
        using var directory = new TestDirectory();
        var binding = new JsonObject { ["statusId"] = Guid.NewGuid().ToString(), ["contentHash"] = new string('A', 64), ["rating"] = null };
        binding[field] = JsonNode.Parse(value);
        var document = new JsonObject { ["schemaVersion"] = 1, ["honorificEnabled"] = false,
            ["moodlesEnabled"] = true, ["moodlesBindings"] = new JsonArray(binding) };
        var json = document.ToJsonString();
        var path = Path.Combine(directory.Path, "integrations.json");
        await File.WriteAllTextAsync(path, json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new IntegrationSettingsStore(directory.Path).LoadAsync());
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task DuplicateIdsAndSixBindingsAreRejectedOnSaveWithoutReplacingThePreviousSettings()
    {
        using var directory = new TestDirectory();
        var store = new IntegrationSettingsStore(directory.Path);
        await store.SaveAsync(new() { HonorificEnabled = true });
        var path = Path.Combine(directory.Path, "integrations.json");
        var previous = await File.ReadAllBytesAsync(path);
        var binding = new MoodlesBinding(Guid.NewGuid(), new('A', 64));
        foreach (var bindings in new IReadOnlyList<MoodlesBinding>[]
        {
            [binding, binding], Enumerable.Range(0, 6).Select(_ => binding with { StatusId = Guid.NewGuid() }).ToArray(),
        })
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new() { MoodlesEnabled = true, MoodlesBindings = bindings }));
            Assert.Equal(previous, await File.ReadAllBytesAsync(path));
        }
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }
}
