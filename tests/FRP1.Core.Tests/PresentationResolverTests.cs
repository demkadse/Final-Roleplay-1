using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Contacts;
using FRP1.Plugin.Presentation;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class PresentationResolverTests
{
    [Fact]
    public async Task ExplicitAuthenticatedCacheReadDoesNotAssertCharacterBinding()
    {
        using var register = await ReadyRegisterAsync(new());
        var policy = ProfileModuleTests.Adult();
        using var resolver = new PresentationResolver(policy, register);
        resolver.SetAccess(true);
        var profile = ProfileTests.Valid() with { DisplayName = "Same Name", Title = "Synthetic title" };
        var key = new ProfileKey(new string('A', 64), profile.Id);
        var received = DateTimeOffset.UtcNow.AddHours(-1);
        resolver.SetCatalog([new(key.Fingerprint, profile, received, false)]);
        var explicitView = resolver.GetProfile(key);
        Assert.Equal(ProfilePresentationState.Offline, explicitView.State);
        Assert.Equal(profile.DisplayName, explicitView.DisplayName);
        Assert.Equal(profile.DisplayName, explicitView.DisplayMetadata!.DisplayName);
        Assert.False(explicitView.Connected);
        Assert.Equal(received, explicitView.ReceivedAt);
        Assert.Null(explicitView.Character);
        Assert.Single(resolver.Search("Same Name"));
        Assert.Empty(resolver.Search(includeOffline: false));
        Assert.Equal(ProfilePresentationState.Unknown, resolver.Resolve(new("Same Name", 1)).State);
        Assert.Null(resolver.Resolve(new("Same Name", 1)).DisplayMetadata);
        Assert.Equal(ProfilePresentationState.NoTarget, resolver.Resolve(null).State);
        await register.ConfirmBindingAsync(key, new("Same Name", 1));
        await ContactRegisterTests.UntilAsync(register, () => !register.IsBusy);
        Assert.Equal(ProfilePresentationState.Offline, resolver.Resolve(new("Same Name", 1)).State);
        Assert.Equal(ProfilePresentationState.Unknown, resolver.Resolve(new("Same Name", 2)).State);
        policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18));
        Assert.NotNull(explicitView.DisplayMetadata);
        policy.SetAudience(new AudiencePreferences());
        Assert.Null(explicitView.DisplayMetadata);
    }

    [Fact]
    public async Task DuplicateConfirmedBindingsProduceNeutralAmbiguousActorView()
    {
        var first = ProfileTests.Valid();
        var second = ProfileTests.Valid();
        var character = new CharacterKey("Same Name", 1);
        var firstKey = new ProfileKey(new string('A', 64), first.Id);
        var secondKey = new ProfileKey(new string('B', 64), second.Id);
        using var register = await ReadyRegisterAsync(new()
        {
            Contacts = [new() { Profile = firstKey, Binding = new(character, true) }, new() { Profile = secondKey, Binding = new(character, true) }],
        });
        using var resolver = new PresentationResolver(ProfileModuleTests.Adult(), register);
        resolver.SetAccess(true);
        resolver.SetCatalog([new(firstKey.Fingerprint, first, DateTimeOffset.UtcNow, true), new(secondKey.Fingerprint, second, DateTimeOffset.UtcNow, true)]);
        var view = resolver.Resolve(new(character.Name, character.HomeWorldId));
        Assert.Equal(ProfilePresentationState.Ambiguous, view.State);
        Assert.Null(view.Key);
        Assert.Null(view.Profile);
        Assert.Null(view.DisplayMetadata);
        Assert.Equal("", view.DisplayName);
        Assert.Equal(2, resolver.Search().Count);
    }

    [Theory]
    [InlineData("under18")]
    [InlineData("unknown")]
    [InlineData("access")]
    [InlineData("block")]
    [InlineData("mute")]
    public async Task AlreadyResolvedViewsAndHistoryCannotLeakAfterLiveWithdrawal(string reason)
    {
        var policy = ProfileModuleTests.Adult();
        var profile = ProfileModuleTests.Expanded() with { ContentRating = ContentRating.Mature18, Title = "Restricted synthetic title", Portrait = new("image/png", PngFixture.Create()) };
        var key = new ProfileKey(new string('A', 64), profile.Id);
        var contact = new LocalContact { Profile = key, Alias = "Private alias", Binding = new(new("Name", 1), true) };
        using var register = await ReadyRegisterAsync(new()
        {
            Contacts = [contact], Encounters = [new(key, new("Name", 1), EncounterKind.ProfileOpened, DateTimeOffset.UtcNow.AddMinutes(-1))],
        });
        using var resolver = new PresentationResolver(policy, register);
        resolver.SetAccess(true);
        resolver.SetCatalog([new(key.Fingerprint, profile, DateTimeOffset.UtcNow, true)]);
        var held = resolver.Resolve(new("Name", 1));
        var heldHistory = Assert.Single(resolver.History());
        Assert.Equal(profile.DisplayName, held.DisplayName);
        Assert.Equal(profile.Title, held.Title);
        Assert.NotNull(held.Profile!.Portrait);
        Assert.Equal(profile.DisplayName, held.DisplayMetadata!.DisplayName);
        Assert.Equal(ContentRating.Mature18, held.DisplayMetadata.ContentRating);
        switch (reason)
        {
            case "under18": policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18)); break;
            case "unknown": policy.SetAudience(new AudiencePreferences()); break;
            case "access": resolver.SetAccess(false); break;
            case "block": await register.SetPeerBlockedAsync(key.Fingerprint, true); break;
            case "mute": await register.SetContactAsync(contact with { Muted = true }); break;
        }
        Assert.False(held.IsReadable);
        Assert.Equal("", held.DisplayName);
        Assert.Equal("", held.Title);
        Assert.Null(held.Key);
        Assert.Null(held.Profile);
        Assert.Null(held.DisplayMetadata);
        Assert.Null(held.Contact);
        Assert.Null(held.Character);
        Assert.Null(held.ReceivedAt);
        Assert.False(held.Connected);
        Assert.Equal("", heldHistory.Profile.DisplayName);
        Assert.Null(heldHistory.Profile.Profile);
        Assert.Null(heldHistory.Profile.DisplayMetadata);
        Assert.Empty(resolver.Search());
        Assert.Empty(resolver.History());
        await ContactRegisterTests.UntilAsync(register, () => !register.IsBusy);
    }

    [Fact]
    public async Task CatalogAndReturnedProfileCopiesCannotBeMutatedByCaller()
    {
        using var register = await ReadyRegisterAsync(new());
        using var resolver = new PresentationResolver(ProfileModuleTests.Adult(), register);
        resolver.SetAccess(true);
        var profile = ProfileModuleTests.Expanded() with { Portrait = new("image/png", PngFixture.Create()), Glances = [new("Original", "Text")] };
        var key = new ProfileKey(new string('A', 64), profile.Id);
        resolver.SetCatalog([new(key.Fingerprint, profile, DateTimeOffset.UtcNow, false)]);
        profile.Glances.Clear();
        profile.Portrait!.Data[0] = 0;
        var view = resolver.GetProfile(key);
        var first = view.Profile!;
        Assert.Single(first.Glances);
        Assert.Equal(137, first.Portrait!.Data[0]);
        first.Glances.Clear();
        first.Portrait.Data[0] = 0;
        Assert.Single(view.Profile!.Glances);
        Assert.Equal(137, view.Profile!.Portrait!.Data[0]);
    }

    [Fact]
    public async Task SearchUsesPrivateFiltersWithoutGuessingBindingsOrOnlinePresence()
    {
        var profile = ProfileTests.Valid();
        var key = new ProfileKey(new string('A', 64), profile.Id);
        using var register = await ReadyRegisterAsync(new()
        {
            Contacts = [new() { Profile = key, Alias = "Trusted alias", Favorite = true, Relationship = RelationshipKind.Friend }],
        });
        using var resolver = new PresentationResolver(ProfileModuleTests.Adult(), register);
        resolver.SetAccess(true);
        resolver.SetCatalog([new(key.Fingerprint, profile, DateTimeOffset.UtcNow, false)]);
        Assert.Single(resolver.Search("trusted", favoritesOnly: true, relationship: RelationshipKind.Friend));
        Assert.Empty(resolver.Search(homeWorldId: 1));
        Assert.Empty(resolver.Search(relationship: RelationshipKind.Rival));
        Assert.Empty(resolver.Search(includeOffline: false));
        resolver.Dispose();
        Assert.Equal("", resolver.GetProfile(key).DisplayName);
    }

    [Fact]
    public async Task LightweightMetadataIsDetachedAndRefreshesWithCatalogWithoutReturningPortraits()
    {
        var policy = ProfileModuleTests.Adult();
        using var register = await ReadyRegisterAsync(new());
        using var resolver = new PresentationResolver(policy, register);
        resolver.SetAccess(true);
        var source = ProfileModuleTests.Expanded() with
        {
            Title = "First title", Portrait = new("image/png", PngFixture.Create(256, 256)),
            Background = new string('x', 8192),
        };
        var key = new ProfileKey(new string('A', 64), source.Id);
        resolver.SetCatalog([new(key.Fingerprint, source, DateTimeOffset.UtcNow, false)]);
        var held = resolver.GetProfile(key);
        var first = held.DisplayMetadata;
        Assert.NotNull(first);
        Assert.Equal(source.DisplayName, first.DisplayName);
        Assert.Equal(source.Title, first.Title);
        Assert.Equal(source.ContentRating, first.ContentRating);
        Assert.Equal(source.AppearanceStyle.NameColor, first.NameColor);
        Assert.Equal(source.AppearanceStyle.IconId, first.IconId);
        source.Portrait!.Data[0] = 0;
        source.Companions.Clear();
        Assert.Equal(first, held.DisplayMetadata);

        var updated = ProfileTests.Valid() with
        {
            Id = source.Id, Revision = 2, DisplayName = "Updated name", Title = "Updated title",
            AppearanceStyle = new() { NameColor = "#112233", IconId = 456 },
        };
        resolver.SetCatalog([new(key.Fingerprint, updated, DateTimeOffset.UtcNow, true)]);
        var latest = held.DisplayMetadata;
        Assert.NotNull(latest);
        Assert.Equal("Updated name", latest.DisplayName);
        Assert.Equal("Updated title", latest.Title);
        Assert.Equal("#112233", latest.NameColor);
        Assert.Equal(456u, latest.IconId);
        Assert.Equal("First title", first.Title);
        resolver.SetCatalog([]);
        Assert.Null(held.DisplayMetadata);
    }

    [Fact]
    public async Task HeldActorMetadataFollowsConfirmedBindingChangesAndBecomesNeutralWhenAmbiguous()
    {
        var first = ProfileTests.Valid() with { DisplayName = "First bound profile" };
        var second = ProfileTests.Valid() with { DisplayName = "Second bound profile" };
        var firstKey = new ProfileKey(new string('A', 64), first.Id);
        var secondKey = new ProfileKey(new string('B', 64), second.Id);
        var character = new CharacterKey("Same Name", 1);
        using var register = await ReadyRegisterAsync(new()
        {
            Contacts = [new() { Profile = firstKey, Binding = new(character, true) }],
        });
        using var resolver = new PresentationResolver(ProfileModuleTests.Adult(), register);
        resolver.SetAccess(true);
        resolver.SetCatalog([new(firstKey.Fingerprint, first, DateTimeOffset.UtcNow, true), new(secondKey.Fingerprint, second, DateTimeOffset.UtcNow, true)]);
        var held = resolver.Resolve(new(character.Name, character.HomeWorldId));
        Assert.Equal(first.DisplayName, held.DisplayMetadata!.DisplayName);
        await register.ClearBindingAsync(firstKey);
        await ContactRegisterTests.UntilAsync(register, () => !register.IsBusy);
        Assert.Null(held.DisplayMetadata);
        await register.ConfirmBindingAsync(secondKey, character);
        await ContactRegisterTests.UntilAsync(register, () => !register.IsBusy);
        Assert.Equal(second.DisplayName, held.DisplayMetadata!.DisplayName);
        await register.ConfirmBindingAsync(firstKey, character);
        await ContactRegisterTests.UntilAsync(register, () => !register.IsBusy);
        Assert.Equal(ProfilePresentationState.Ambiguous, held.State);
        Assert.Null(held.DisplayMetadata);
        resolver.SetAccess(false);
        Assert.Null(held.DisplayMetadata);
    }

    private static async Task<RegisterSession> ReadyRegisterAsync(ContactRegister data)
    {
        var register = new RegisterSession(_ => Task.FromResult(data), (_, _) => Task.CompletedTask);
        await ContactRegisterTests.UntilAsync(register, () => register.IsLoaded);
        return register;
    }
}
