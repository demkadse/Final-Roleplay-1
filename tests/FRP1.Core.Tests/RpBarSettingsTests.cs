using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FRP1.Plugin.RpBar;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class RpBarSettingsTests
{
    [Fact]
    public async Task MissingSettingsUseVisibleUnlockedDefaultAndNoImplicitActiveProfile()
    {
        using var directory = new TestDirectory();
        var preferences = await new RpBarSettingsStore(directory.Path).LoadAsync();
        Assert.True(preferences.Visible);
        Assert.False(preferences.Locked);
        Assert.True(preferences.ShowHints);
        Assert.Equal(1f, preferences.Scale);
        Assert.Empty(preferences.ActiveProfiles);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
    }

    [Fact]
    public async Task LayoutAndExplicitCharacterAssociationsRoundtripAsIndependentSnapshots()
    {
        using var directory = new TestDirectory();
        var profileId = Guid.NewGuid();
        var externalMap = new Dictionary<ulong, Guid> { [1] = profileId, [ulong.MaxValue] = Guid.NewGuid() };
        var original = new RpBarPreferences { Visible = false, Locked = true, ShowHints = false, Scale = 1.5f, PositionX = -100, PositionY = 800, ActiveProfiles = externalMap };
        var snapshot = RpBarPreferences.Snapshot(original);
        var store = new RpBarSettingsStore(directory.Path);
        var pending = store.SaveAsync(original);
        externalMap[1] = Guid.NewGuid();
        externalMap.Clear();
        await pending;
        var restored = await new RpBarSettingsStore(directory.Path).LoadAsync();
        Assert.False(restored.Visible);
        Assert.True(restored.Locked);
        Assert.False(restored.ShowHints);
        Assert.Equal(1.5f, restored.Scale);
        Assert.Equal(-100, restored.PositionX);
        Assert.Equal(800, restored.PositionY);
        Assert.Equal(profileId, restored.ActiveProfiles[1]);
        Assert.Equal(snapshot.ActiveProfiles, restored.ActiveProfiles);
        var map = Assert.IsAssignableFrom<IDictionary<ulong, Guid>>(restored.ActiveProfiles);
        Assert.True(map.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => map.Clear());
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }

    [Theory]
    [InlineData("schemaVersion")] [InlineData("visible")] [InlineData("locked")]
    [InlineData("scale")] [InlineData("positionX")] [InlineData("positionY")] [InlineData("activeProfiles")] [InlineData("showHints")]
    public async Task MissingRequiredSettingsAreRejected(string property)
    {
        using var directory = new TestDirectory();
        var store = new RpBarSettingsStore(directory.Path);
        await store.SaveAsync(new RpBarPreferences());
        var path = Path.Combine(directory.Path, "rp-bar.json");
        var json = JsonNode.Parse(await File.ReadAllBytesAsync(path))!.AsObject();
        json.Remove(property);
        await File.WriteAllTextAsync(path, json.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Theory]
    [InlineData("schemaVersion", "2")]
    [InlineData("scale", "0.74")]
    [InlineData("scale", "1.76")]
    [InlineData("scale", "null")]
    [InlineData("scale", "\"NaN\"")]
    [InlineData("positionX", "100001")]
    [InlineData("positionY", "-100001")]
    [InlineData("activeProfiles", "null")]
    [InlineData("activeProfiles", "{\"0\":\"11111111-1111-1111-1111-111111111111\"}")]
    [InlineData("activeProfiles", "{\"1\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("activeProfiles", "{\"1\":\"not-a-guid\"}")]
    [InlineData("visible", "null")]
    public async Task InvalidSettingsCannotBecomeActive(string property, string value)
    {
        using var directory = new TestDirectory();
        var store = new RpBarSettingsStore(directory.Path);
        await store.SaveAsync(new RpBarPreferences());
        var path = Path.Combine(directory.Path, "rp-bar.json");
        var json = JsonNode.Parse(await File.ReadAllBytesAsync(path))!.AsObject();
        json[property] = JsonNode.Parse(value);
        await File.WriteAllTextAsync(path, json.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Theory]
    [InlineData("null")] [InlineData("[]")] [InlineData("{")] [InlineData("")]
    public async Task MalformedSettingsAreRejected(string content)
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "rp-bar.json"), content);
        await Assert.ThrowsAsync<InvalidDataException>(() => new RpBarSettingsStore(directory.Path).LoadAsync());
    }

    [Fact]
    public async Task UnknownDuplicateAndOversizedSettingsAreRejected()
    {
        using var directory = new TestDirectory();
        var store = new RpBarSettingsStore(directory.Path);
        await store.SaveAsync(new RpBarPreferences());
        var path = Path.Combine(directory.Path, "rp-bar.json");
        var json = await File.ReadAllTextAsync(path);
        foreach (var suffix in new[] { ",\"unknown\":1}", ",\"scale\":1}" })
        {
            await File.WriteAllTextAsync(path, json[..^1] + suffix);
            await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
        }
        await File.WriteAllTextAsync(path, json.PadRight(RpBarSettingsStore.MaximumBytes));
        Assert.NotNull(await store.LoadAsync());
        await File.AppendAllTextAsync(path, " ");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Fact]
    public async Task AssociationLimitAndFiniteCoordinatesAreEnforced()
    {
        using var directory = new TestDirectory();
        var store = new RpBarSettingsStore(directory.Path);
        var map = Enumerable.Range(1, 128).ToDictionary(value => (ulong)value, _ => Guid.NewGuid());
        await store.SaveAsync(new RpBarPreferences { ActiveProfiles = map });
        Assert.Equal(128, (await store.LoadAsync()).ActiveProfiles.Count);
        map[129] = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new RpBarPreferences { ActiveProfiles = map }));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new RpBarPreferences { PositionX = float.NaN }));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new RpBarPreferences { PositionY = float.PositiveInfinity }));
    }

    [Fact]
    public async Task CancelledWritePreservesOldFileAndLeavesNoTemporaryFile()
    {
        using var directory = new TestDirectory();
        var store = new RpBarSettingsStore(directory.Path);
        await store.SaveAsync(new RpBarPreferences { Locked = true });
        var before = await File.ReadAllBytesAsync(Path.Combine(directory.Path, "rp-bar.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(new RpBarPreferences(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.LoadAsync(cancellation.Token));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(directory.Path, "rp-bar.json")));
        Assert.True((await store.LoadAsync()).Locked);
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }

    [Theory]
    [InlineData(0.75f)] [InlineData(1f)] [InlineData(1.75f)]
    public void ScaleSupportsDocumentedRange(float scale) => Assert.Equal(scale, RpBarGeometry.ValidateScale(scale));

    [Theory]
    [InlineData(0f)] [InlineData(0.749f)] [InlineData(1.751f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void UnsupportedScaleIsRejected(float scale) => Assert.Throws<ArgumentOutOfRangeException>(() => RpBarGeometry.ValidateScale(scale));

    [Fact]
    public void BarIsClampedInsideViewportAndResetIsReachable()
    {
        var size = new Vector2(400, 50);
        var viewport = new Vector2(1920, 1080);
        Assert.Equal(new Vector2(8, 8), RpBarGeometry.Clamp(new(-100, -200), size, viewport));
        Assert.Equal(new Vector2(1512, 1022), RpBarGeometry.Clamp(new(5000, 5000), size, viewport));
        Assert.Equal(RpBarGeometry.ResetPosition, RpBarGeometry.Clamp(RpBarGeometry.ResetPosition, size, viewport));
        Assert.Equal(Vector2.Zero, RpBarGeometry.Clamp(new(200, 200), size, new(100, 20)));
    }

    [Fact]
    public void InvalidGeometryProducesFiniteFallbackAndRelativeCoordinatesIgnoreMonitorOrigin()
    {
        var invalid = RpBarGeometry.Clamp(new(float.NaN, float.PositiveInfinity), new(float.NaN, -10), new(100, 100), float.NaN);
        Assert.Equal(new Vector2(8, 8), invalid);
        Assert.Equal(Vector2.Zero, RpBarGeometry.Clamp(new(20, 20), new(10, 10), new(float.NaN, -1)));
        var firstMonitorOrigin = new Vector2(-1920, 0);
        var secondMonitorOrigin = new Vector2(1920, -1080);
        var relative = new Vector2(20, 120);
        var first = RpBarGeometry.Clamp((firstMonitorOrigin + relative) - firstMonitorOrigin, new(400, 50), new(1920, 1080));
        var second = RpBarGeometry.Clamp((secondMonitorOrigin + relative) - secondMonitorOrigin, new(400, 50), new(1920, 1080));
        Assert.Equal(relative, first);
        Assert.Equal(first, second);
    }
}
