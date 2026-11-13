using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FRP1.Plugin.Integrations;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class LifestreamSettingsTests
{
    [Theory]
    [InlineData("""{"schemaVersion":1,"honorificEnabled":true}""")]
    [InlineData("""{"schemaVersion":1,"honorificEnabled":false,"ipcReadEnabled":true,"moodlesEnabled":true,"moodlesBindings":[]}""")]
    public async Task OldSchemaOneFilesRemainUntouchedWithLifestreamDisabledAndNoDestination(string original)
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "integrations.json");
        await File.WriteAllTextAsync(path, original);
        var settings = await new IntegrationSettingsStore(directory.Path).LoadAsync();
        Assert.False(settings.LifestreamEnabled);
        Assert.Null(settings.LifestreamAddress);
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task MissingFileDefaultsOffWithoutCreatingAnyLocalFile()
    {
        using var directory = new TestDirectory();
        var settings = await new IntegrationSettingsStore(directory.Path).LoadAsync();
        Assert.False(settings.LifestreamEnabled);
        Assert.Null(settings.LifestreamAddress);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
    }

    [Theory]
    [InlineData(HousingPropertyType.House)]
    [InlineData(HousingPropertyType.Apartment)]
    public async Task ExactlyOneTypedDestinationRoundTripsLocallyAlongsideExistingIntegrationChoices(HousingPropertyType type)
    {
        using var directory = new TestDirectory();
        var store = new IntegrationSettingsStore(directory.Path);
        var address = HousingAddressTests.House with { PropertyType = type, Ward = 30, Plot = 60, Apartment = 90, ApartmentSubdivision = true };
        var settings = new IntegrationSettings { HonorificEnabled = true, IpcReadEnabled = true, MoodlesEnabled = true,
            LifestreamEnabled = true, LifestreamAddress = address };
        await store.SaveAsync(settings);
        var restored = await store.LoadAsync();
        Assert.True(restored.LifestreamEnabled && restored.HonorificEnabled && restored.IpcReadEnabled && restored.MoodlesEnabled);
        Assert.Equal(LifestreamRules.Canonicalize(address), restored.LifestreamAddress);
        var replacement = address with { Ward = 1 };
        await store.SaveAsync(restored with { LifestreamAddress = replacement });
        Assert.Equal(LifestreamRules.Canonicalize(replacement), (await store.LoadAsync()).LifestreamAddress);
        var path = Assert.Single(Directory.EnumerateFiles(directory.Path));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        var target = json.RootElement.GetProperty("lifestreamAddress");
        Assert.Equal(JsonValueKind.Object, target.ValueKind);
        Assert.Equal(new[] { "apartment", "apartmentSubdivision", "district", "plot", "propertyType", "ward", "worldId" },
            target.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.InRange(new FileInfo(path).Length, 1, 8192);
    }

    public static IEnumerable<object[]> InvalidOptionalFields => new[]
    {
        "\"lifestreamEnabled\":null", "\"lifestreamEnabled\":1", "\"lifestreamEnabled\":\"true\"",
        "\"lifestreamEnabled\":false,\"lifestreamEnabled\":true", "\"lifestreamAddress\":[]",
        "\"lifestreamAddress\":{}", "\"lifestreamAddress\":\"/li travel\"",
        "\"lifestreamAddress\":null,\"lifestreamAddress\":null",
    }.Select(fragment => new object[] { fragment });

    [Theory]
    [MemberData(nameof(InvalidOptionalFields))]
    public async Task MalformedAndDuplicateOptionalFieldsFailClosedWithoutRewritingTheInput(string fragment)
    {
        using var directory = new TestDirectory();
        var json = "{\"schemaVersion\":1,\"honorificEnabled\":false," + fragment + "}";
        var path = Path.Combine(directory.Path, "integrations.json");
        await File.WriteAllTextAsync(path, json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new IntegrationSettingsStore(directory.Path).LoadAsync());
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("worldId", "0")]
    [InlineData("worldId", "2147483648")]
    [InlineData("worldId", "null")]
    [InlineData("worldId", "\"21\"")]
    [InlineData("district", "0")]
    [InlineData("district", "\"Mist\"")]
    [InlineData("ward", "31")]
    [InlineData("propertyType", "2")]
    [InlineData("plot", "61")]
    [InlineData("apartment", "91")]
    [InlineData("apartmentSubdivision", "1")]
    [InlineData("alias", "\"/li command\"")]
    public async Task InvalidTypedAddressFieldsAndCommandLikeExtraDataAreRejected(string field, string value)
    {
        using var directory = new TestDirectory();
        var address = AddressJson();
        address[field] = JsonNode.Parse(value);
        var json = Document(address).ToJsonString();
        var path = Path.Combine(directory.Path, "integrations.json");
        await File.WriteAllTextAsync(path, json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new IntegrationSettingsStore(directory.Path).LoadAsync());
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("worldId")]
    [InlineData("district")]
    [InlineData("ward")]
    [InlineData("propertyType")]
    public async Task MissingRequiredAddressFieldsCannotSilentlyBecomeDefaults(string field)
    {
        using var directory = new TestDirectory();
        var address = AddressJson();
        address.Remove(field);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "integrations.json"), Document(address).ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => new IntegrationSettingsStore(directory.Path).LoadAsync());
    }

    [Fact]
    public async Task OptionalInactiveAddressFieldsRetainSafeDefaultsAndNullMeansNoSavedDestination()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "integrations.json");
        await File.WriteAllTextAsync(path, Document(AddressJson()).ToJsonString());
        var store = new IntegrationSettingsStore(directory.Path);
        Assert.Equal(HousingAddressTests.House, (await store.LoadAsync()).LifestreamAddress);
        await File.WriteAllTextAsync(path, Document(null).ToJsonString());
        Assert.Null((await store.LoadAsync()).LifestreamAddress);
    }

    [Fact]
    public async Task ExistingEightKiBBoundaryStillAppliesToHousingSettingsAndOversizeIsNotRewritten()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "integrations.json");
        var store = new IntegrationSettingsStore(directory.Path);
        var boundary = Document(AddressJson()).ToJsonString().PadRight(8192);
        Assert.Equal(8192, IntegrationSettingsStore.MaximumBytes);
        Assert.Equal(8192, Encoding.UTF8.GetByteCount(boundary));
        await File.WriteAllTextAsync(path, boundary);
        Assert.Equal(HousingAddressTests.House, (await store.LoadAsync()).LifestreamAddress);
        var oversized = boundary + " ";
        await File.WriteAllTextAsync(path, oversized);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
        Assert.Equal(oversized, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task InvalidSaveKeepsPreviousValidDestinationAndNoTemporaryArtifacts()
    {
        using var directory = new TestDirectory();
        var store = new IntegrationSettingsStore(directory.Path);
        await store.SaveAsync(new() { LifestreamEnabled = true, LifestreamAddress = HousingAddressTests.House });
        var path = Path.Combine(directory.Path, "integrations.json");
        var before = await File.ReadAllBytesAsync(path);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new() { LifestreamEnabled = true,
            LifestreamAddress = HousingAddressTests.House with { Apartment = 91 } }));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }

    private static JsonObject AddressJson() => new() { ["worldId"] = 21, ["district"] = 8, ["ward"] = 1, ["propertyType"] = 0 };
    private static JsonObject Document(JsonNode? address) => new() { ["schemaVersion"] = 1, ["honorificEnabled"] = false,
        ["lifestreamEnabled"] = true, ["lifestreamAddress"] = address };
}
