using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Contacts;
using FRP1.Plugin.Presentation;
using FRP1.Plugin.RpBar;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class RpBarContextTests
{
    private static readonly ActorSnapshot Local = new("Local Name", 1, 100);
    private static readonly ActorSnapshot Foreign = new("Other Name", 2, 200);

    [Theory]
    [InlineData("no-target")]
    [InlineData("self")]
    [InlineData("npc")]
    [InlineData("invalid-target")]
    [InlineData("contradictory-target")]
    [InlineData("unknown-player")]
    [InlineData("same-name-other-world")]
    public async Task EveryTargetContextKeepsTheValidLocalActor(string scenario)
    {
        await using var harness = await BarContextHarness.CreateAsync();
        var scene = scenario switch
        {
            "no-target" => new RpBarSceneSnapshot(true, false, null, Local),
            "self" => new(true, true, new(" local name ", 1, 999), Local),
            "npc" => new(true, true, null, Local),
            "invalid-target" => new(true, true, new("Bad\0Name", 2), Local),
            "contradictory-target" => new(true, false, Foreign, Local),
            "same-name-other-world" => new(true, true, new(Local.Name, 2), Local),
            _ => new(true, true, Foreign, Local),
        };
        var context = RpBarContextResolver.Resolve(scene, true, harness.Resolver);
        Assert.Equal(RpBarContextKind.Own, context.Kind);
        Assert.Equal(Local, context.Actor);
        Assert.Null(context.Profile);
    }

    [Theory]
    [InlineData("invalid-scene")]
    [InlineData("missing-local")]
    [InlineData("access-denied")]
    public async Task MissingLocalSceneOrAccessAlwaysProducesAnEmptyNeutralContext(string scenario)
    {
        await using var harness = await BarContextHarness.CreateAsync();
        var scene = scenario switch
        {
            "invalid-scene" => new RpBarSceneSnapshot(false, false, null, Local),
            "missing-local" => new(true, true, Foreign, null),
            _ => new(true, true, Foreign, Local),
        };
        var result = RpBarContextResolver.Resolve(scene, scenario != "access-denied", harness.Resolver);
        Assert.Equal(RpBarContextKind.Neutral, result.Kind);
        Assert.Null(result.Actor);
        Assert.Null(result.Profile);
    }

    [Theory]
    [InlineData("", 1u)]
    [InlineData("   ", 1u)]
    [InlineData("Bad\0Name", 1u)]
    [InlineData("Bad\nName", 1u)]
    [InlineData("Local Name", 0u)]
    [InlineData("Local Name", 65536u)]
    [InlineData("Local Name", uint.MaxValue)]
    public async Task InvalidLocalIdentityNeverBorrowsAReadableForeignTarget(string name, uint homeWorldId)
    {
        await using var harness = await BarContextHarness.CreateAsync();
        var profile = ProfileTests.Valid();
        var key = new ProfileKey(new string('A', 64), profile.Id);
        await harness.BindAsync(key, Foreign);
        harness.Resolver.SetCatalog([new(key.Fingerprint, profile, DateTimeOffset.UtcNow, true)]);
        Assert.True(harness.Resolver.Resolve(Foreign).IsReadable);
        var result = RpBarContextResolver.Resolve(new(true, true, Foreign, new(name, homeWorldId)), true, harness.Resolver);
        Assert.Equal(new RpBarDisplayContext(RpBarContextKind.Neutral), result);
    }

    [Theory]
    [InlineData(80, 65535u, RpBarContextKind.Own)]
    [InlineData(81, 65535u, RpBarContextKind.Neutral)]
    public async Task LocalIdentityBoundariesAreCheckedWithoutRequiringAnEntityId(int length, uint world, RpBarContextKind expected)
    {
        await using var harness = await BarContextHarness.CreateAsync();
        var local = new ActorSnapshot(new string('L', length), world);
        var result = RpBarContextResolver.Resolve(new(true, true, Foreign, local), true, harness.Resolver);
        Assert.Equal(expected, result.Kind);
        Assert.Equal(expected == RpBarContextKind.Own ? local : (ActorSnapshot?)null, result.Actor);
        Assert.Null(result.Profile);
    }

    [Fact]
    public async Task ConfirmedForeignAndSameNameDifferentWorldStayInSocialViewsAndNeverReplaceOwnHud()
    {
        await using var harness = await BarContextHarness.CreateAsync();
        var profile = ProfileTests.Valid();
        var key = new ProfileKey(new string('A', 64), profile.Id);
        await harness.BindAsync(key, Foreign);
        harness.Resolver.SetCatalog([new(key.Fingerprint, profile, DateTimeOffset.UtcNow, false)]);
        var result = RpBarContextResolver.Resolve(new(true, true, Foreign, Local), true, harness.Resolver);
        Assert.Equal(new RpBarDisplayContext(RpBarContextKind.Own, Local), result);
        Assert.Equal(key, harness.Resolver.Resolve(Foreign).Key);
        var sameNameOtherWorld = new ActorSnapshot(Local.Name, 2);
        await harness.BindAsync(key, sameNameOtherWorld);
        var otherWorld = RpBarContextResolver.Resolve(new(true, true, sameNameOtherWorld, Local), true, harness.Resolver);
        Assert.Equal(result, otherWorld);
        Assert.Equal(key, harness.Resolver.Resolve(sameNameOtherWorld).Key);
        Assert.Equal(ProfilePresentationState.Unknown, harness.Resolver.Resolve(Local).State);
    }

    [Theory]
    [InlineData("under18")]
    [InlineData("unknown-age")]
    [InlineData("resolver-access")]
    [InlineData("block")]
    [InlineData("mute")]
    [InlineData("ambiguous")]
    [InlineData("binding-cleared")]
    public async Task ForeignPolicyOrBindingRevocationHidesSocialDataAndKeepsHudLocal(string reason)
    {
        await using var harness = await BarContextHarness.CreateAsync();
        var profile = ProfileTests.Valid() with { ContentRating = ContentRating.Mature18 };
        var key = new ProfileKey(new string('A', 64), profile.Id);
        await harness.BindAsync(key, Foreign);
        harness.Resolver.SetCatalog([new(key.Fingerprint, profile, DateTimeOffset.UtcNow, true)]);
        var scene = new RpBarSceneSnapshot(true, true, Foreign, Local);
        var heldForeignView = harness.Resolver.Resolve(Foreign);
        Assert.Equal(key, heldForeignView.Key);
        Assert.NotNull(heldForeignView.Profile);
        var initial = RpBarContextResolver.Resolve(scene, true, harness.Resolver);
        Assert.Equal(new RpBarDisplayContext(RpBarContextKind.Own, Local), initial);
        switch (reason)
        {
            case "under18": harness.Policy.SetAudience(new(AudienceSelection.Under18)); break;
            case "unknown-age": harness.Policy.SetAudience(new()); break;
            case "resolver-access": harness.Resolver.SetAccess(false); break;
            case "block": await harness.Register.SetPeerBlockedAsync(key.Fingerprint, true); break;
            case "mute": await harness.Register.SetContactAsync(harness.Register.GetContact(key)! with { Muted = true }); break;
            case "ambiguous": await harness.BindAsync(new(new string('B', 64), Guid.NewGuid()), Foreign); break;
            case "binding-cleared": await harness.Register.ClearBindingAsync(key); break;
        }
        await ContactRegisterTests.UntilAsync(harness.Register, () => !harness.Register.IsBusy);
        var result = RpBarContextResolver.Resolve(scene, true, harness.Resolver);
        Assert.Equal(initial, result);
        Assert.Null(result.Profile);
        Assert.False(heldForeignView.IsReadable);
        Assert.Null(heldForeignView.Key);
        Assert.Null(heldForeignView.Profile);
        Assert.Null(heldForeignView.DisplayMetadata);
        Assert.Equal("", heldForeignView.DisplayName);
    }

    [Fact]
    public async Task Under18CanReadGeneralSocialProfileWhileHudRemainsOwn()
    {
        await using var harness = await BarContextHarness.CreateAsync();
        var profile = ProfileTests.Valid();
        var key = new ProfileKey(new string('A', 64), profile.Id);
        await harness.BindAsync(key, Foreign);
        harness.Resolver.SetCatalog([new(key.Fingerprint, profile, DateTimeOffset.UtcNow, false)]);
        harness.Policy.SetAudience(new(AudienceSelection.Under18));
        Assert.Equal(key, harness.Resolver.Resolve(Foreign).Key);
        Assert.NotNull(harness.Resolver.Resolve(Foreign).Profile);
        Assert.Equal(new RpBarDisplayContext(RpBarContextKind.Own, Local),
            RpBarContextResolver.Resolve(new(true, true, Foreign, Local), true, harness.Resolver));
    }

    [Fact]
    public async Task EqualProfileGuidsFromDifferentPeersChangeSocialIdentityWhileHudRemainsOwn()
    {
        await using var harness = await BarContextHarness.CreateAsync();
        var profile = ProfileTests.Valid();
        var first = new ProfileKey(new string('A', 64), profile.Id);
        var second = new ProfileKey(new string('B', 64), profile.Id);
        harness.Resolver.SetCatalog([new(first.Fingerprint, profile, DateTimeOffset.UtcNow, false), new(second.Fingerprint, profile, DateTimeOffset.UtcNow, false)]);
        await harness.BindAsync(first, Foreign);
        var scene = new RpBarSceneSnapshot(true, true, Foreign, Local);
        var initial = RpBarContextResolver.Resolve(scene, true, harness.Resolver);
        var heldForeignView = harness.Resolver.Resolve(Foreign);
        Assert.Equal(first, heldForeignView.Key);
        await harness.Register.ClearBindingAsync(first);
        await ContactRegisterTests.UntilAsync(harness.Register, () => !harness.Register.IsBusy);
        await harness.BindAsync(second, Foreign);
        var changed = RpBarContextResolver.Resolve(scene, true, harness.Resolver);
        Assert.Equal(second, heldForeignView.Key);
        Assert.Equal(first.ProfileId, heldForeignView.Key!.Value.ProfileId);
        Assert.Equal(new RpBarDisplayContext(RpBarContextKind.Own, Local), initial);
        Assert.Equal(initial, changed);
    }

    [Fact]
    public async Task ContextReevaluationClearsLostAccessAndFollowsOnlyLocalActorChanges()
    {
        await using var harness = await BarContextHarness.CreateAsync();
        var scene = new RpBarSceneSnapshot(true, true, Foreign, Local);
        var own = RpBarContextResolver.Resolve(scene, true, harness.Resolver);
        var neutral = new RpBarDisplayContext(RpBarContextKind.Neutral);
        Assert.Equal(neutral, RpBarContextResolver.Resolve(scene, false, harness.Resolver));
        Assert.Equal(neutral, RpBarContextResolver.Resolve(scene with { SceneValid = false }, true, harness.Resolver));
        Assert.Equal(neutral, RpBarContextResolver.Resolve(scene with { LocalActor = null }, true, harness.Resolver));
        Assert.Equal(own, RpBarContextResolver.Resolve(scene, true, harness.Resolver));
        var changedLocal = new ActorSnapshot("Next Local", 3, 300);
        var changed = RpBarContextResolver.Resolve(scene with { LocalActor = changedLocal }, true, harness.Resolver);
        Assert.Equal(new RpBarDisplayContext(RpBarContextKind.Own, changedLocal), changed);
        Assert.NotEqual(own, changed);
    }

    [Fact]
    public async Task TargetSwitchesNeverChangeOwnEditorSnapshotOrWriterData()
    {
        await using var contextHarness = await BarContextHarness.CreateAsync();
        var ownProfile = ProfileTests.Valid();
        await using var own = await OwnProfileHarness.CreateAsync([ownProfile]);
        await own.Session.SelectActiveAsync(ownProfile.Id);
        await own.ReadyAsync();
        var ownVersion = own.Session.Version;
        var ownHash = ProfileJson.Hash(own.Session.ActiveProfile!);
        var file = Path.Combine(own.Directory.Path, "profiles", ownProfile.Id.ToString("N") + ".json");
        var writtenAt = File.GetLastWriteTimeUtc(file);
        var foreignProfile = ProfileTests.Valid();
        var key = new ProfileKey(new string('A', 64), foreignProfile.Id);
        await contextHarness.BindAsync(key, Foreign);
        contextHarness.Resolver.SetCatalog([new(key.Fingerprint, foreignProfile, DateTimeOffset.UtcNow, false)]);
        foreach (var scene in new[] { new RpBarSceneSnapshot(true, false, null, Local), new(true, true, Foreign, Local), new(true, true, null, Local), new(false, false, null, Local) })
        {
            var context = RpBarContextResolver.Resolve(scene, true, contextHarness.Resolver);
            Assert.Equal(scene.SceneValid ? RpBarContextKind.Own : RpBarContextKind.Neutral, context.Kind);
            Assert.Null(context.Profile);
        }
        Assert.Equal(ownVersion, own.Session.Version);
        Assert.Equal(ownHash, ProfileJson.Hash(own.Session.ActiveProfile!));
        Assert.Equal(ownHash, ProfileJson.Hash((await own.Store.LoadLocalAsync(ownProfile.Id))!));
        Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(file));
    }
}

internal sealed class BarContextHarness : IAsyncDisposable
{
    public AudiencePolicy Policy { get; } = ProfileModuleTests.Adult();
    public RegisterSession Register { get; } = new(_ => Task.FromResult(new ContactRegister()), (_, _) => Task.CompletedTask);
    public PresentationResolver Resolver { get; }
    private BarContextHarness() { Resolver = new(Policy, Register); Resolver.SetAccess(true); }
    public static async Task<BarContextHarness> CreateAsync()
    {
        var harness = new BarContextHarness();
        await ContactRegisterTests.UntilAsync(harness.Register, () => harness.Register.IsLoaded);
        return harness;
    }
    public async Task BindAsync(ProfileKey key, ActorSnapshot actor)
    {
        await Register.ConfirmBindingAsync(key, new(actor.Name, actor.HomeWorldId));
        await ContactRegisterTests.UntilAsync(Register, () => !Register.IsBusy);
    }
    public async ValueTask DisposeAsync()
    {
        Resolver.Dispose(); Register.Dispose();
        await Register.DisposalCompletion;
    }
}
