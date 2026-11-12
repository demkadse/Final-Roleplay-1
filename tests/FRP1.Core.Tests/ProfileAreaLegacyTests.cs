using FRP1.Core.Profiles;
using FRP1.Plugin;
using FRP1.Plugin.UiProfiles;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class ProfileAreaLegacyTests
{
    [Fact]
    public void NavigationHasExactlyTheThreeRequestedImmutableProfileAreas()
    {
        Assert.Equal(new[] { ProfileArea.Overview, ProfileArea.Character, ProfileArea.Ooc }, ProfileAreas.All);
        Assert.Equal(3, Enum.GetValues<ProfileArea>().Length);
        Assert.Equal(new[] { "Überblick", "Charakter", "OOC" }, ProfileAreas.All.Select(ProfileAreas.Title));
        var areas = Assert.IsAssignableFrom<ICollection<ProfileArea>>(ProfileAreas.All);
        Assert.True(areas.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => areas.Clear());
        Assert.Throws<ArgumentOutOfRangeException>(() => ProfileAreas.Title((ProfileArea)999));
    }

    [Fact]
    public void EveryExistingLegacyValueRemainsReadableAndDetachedFromTheSource()
    {
        var source = LegacyProfile();
        var entries = ExistingAdditionalContent.Read(source);
        Assert.Equal(8, entries.Count);
        foreach (var kind in Enum.GetValues<AdditionalContentKind>()) Assert.Equal(2, entries.Count(entry => entry.Kind == kind));
        Assert.Contains(entries, entry => entry.Kind == AdditionalContentKind.RpHook && entry.Title == "Hook A" && entry.Values.Any(value => value.Value == "Full hook text A"));
        Assert.Contains(entries, entry => entry.Kind == AdditionalContentKind.ExtraField && entry.Key == "Extra A" && entry.Values.Any(value => value.Value == "Full extra value A"));
        Assert.Contains(entries, entry => entry.Kind == AdditionalContentKind.PersonalityTrait &&
            entry.Values.Any(value => value.Value == "Quiet") && entry.Values.Any(value => value.Value == "Talkative") && entry.Values.Any(value => value.Value == "73%"));
        Assert.Contains(entries, entry => entry.Kind == AdditionalContentKind.Companion && entry.Title == "Companion A" && entry.IconId == 123 &&
            entry.Values.Any(value => value.Value == "Chocobo") && entry.Values.Any(value => value.Value == "Full companion description A"));
        Assert.True(Assert.IsAssignableFrom<ICollection<AdditionalContentEntry>>(entries).IsReadOnly);
        Assert.All(entries, entry => Assert.True(Assert.IsAssignableFrom<ICollection<AdditionalContentValue>>(entry.Values).IsReadOnly));
        source.RpHooks.Clear();
        source.ExtraFields.Clear();
        source.PersonalityTraits.Clear();
        source.Companions.Clear();
        Assert.Equal(8, entries.Count);
        Assert.Contains(entries, entry => entry.Title == "Hook A");
    }

    [Theory]
    [InlineData(AdditionalContentKind.RpHook)]
    [InlineData(AdditionalContentKind.ExtraField)]
    [InlineData(AdditionalContentKind.PersonalityTrait)]
    [InlineData(AdditionalContentKind.Companion)]
    public void ExplicitRemovalDropsExactlyOneLegacyEntryWithoutChangingAnyOtherPublicData(AdditionalContentKind kind)
    {
        var source = LegacyProfile();
        var originalHash = ProfileJson.Hash(source);
        var selected = ExistingAdditionalContent.Read(source).First(entry => entry.Kind == kind);
        var result = ExistingAdditionalContent.Remove(source, selected);
        var expected = kind switch
        {
            AdditionalContentKind.RpHook => source with { RpHooks = [source.RpHooks[1]] },
            AdditionalContentKind.ExtraField => source with { ExtraFields = new() { ["Extra B"] = "Full extra value B" } },
            AdditionalContentKind.PersonalityTrait => source with { PersonalityTraits = [source.PersonalityTraits[1]] },
            _ => source with { Companions = [source.Companions[1]] },
        };
        Assert.Equal(ProfileJson.Hash(expected), ProfileJson.Hash(result));
        Assert.Equal(originalHash, ProfileJson.Hash(source));
        Assert.Equal(7, ExistingAdditionalContent.Read(result).Count);
    }

    [Fact]
    public void ForeignOrChangedEntryCannotRemoveAnUnrelatedCurrentValue()
    {
        var source = LegacyProfile();
        var selected = ExistingAdditionalContent.Read(source).First(entry => entry.Kind == AdditionalContentKind.RpHook);
        Assert.Throws<InvalidOperationException>(() => ExistingAdditionalContent.Remove(source with { Id = Guid.NewGuid() }, selected));
        var changed = source with { RpHooks = [new("Hook A", "Changed since selection"), source.RpHooks[1]] };
        var before = ProfileJson.Hash(changed);
        Assert.Throws<InvalidOperationException>(() => ExistingAdditionalContent.Remove(changed, selected));
        Assert.Equal(before, ProfileJson.Hash(changed));
        Assert.Throws<InvalidOperationException>(() => ExistingAdditionalContent.Remove(source, selected with { Index = 1 }));
        Assert.Throws<InvalidOperationException>(() => ExistingAdditionalContent.Remove(source, selected with { Kind = AdditionalContentKind.Companion }));
    }

    [Fact]
    public void LegacyRemovalCopiesEveryRemainingMutableCollectionAndPortraitBuffer()
    {
        var source = LegacyProfile();
        var originalHash = ProfileJson.Hash(source);
        var selected = ExistingAdditionalContent.Read(source).First(entry => entry.Kind == AdditionalContentKind.ExtraField);
        var result = ExistingAdditionalContent.Remove(source, selected);
        result.Glances.Clear(); result.RpHooks.Clear(); result.RpLanguages.Clear();
        result.PersonalityTraits.Clear(); result.Companions.Clear(); result.ExtraFields.Clear();
        result.Portrait!.Data[0] = 0;
        Assert.Equal(originalHash, ProfileJson.Hash(source));
        Assert.Equal(137, source.Portrait!.Data[0]);
    }

    [Fact]
    public void UneditedLanguageListPreservesWhitespaceLineBreaksAndOrderingExactly()
    {
        var source = LegacyProfile() with { RpLanguages = [" Deutsch ", "Common\r\nDialect", "Doman"] };
        var before = ProfileJson.Hash(source);
        var draft = ExtendedProfileDraft.From(source);
        var result = draft.ApplyTo(source);
        Assert.Equal(before, ProfileJson.Hash(result));
        var originalLanguages = source.RpLanguages.ToArray();
        source.RpLanguages.Clear();
        Assert.Equal(originalLanguages, draft.ApplyTo(source).RpLanguages);
    }

    [Fact]
    public void EditingSummaryPreservesUneditedOptionalAndLegacyFieldsAcrossBothDrafts()
    {
        var source = LegacyProfile() with
        {
            EyeColor = "Amber", Height = "175 cm", Weight = "68 kg", Birthplace = "Gridania", Residence = "Doman village",
            RpLanguages = [" Deutsch ", "Common\r\nDialect", "Doman"],
            Glances = [new("Disabled but retained", "Existing optional content", Enabled: false)],
        };
        var before = ProfileJson.Hash(source);
        var details = new ProfileDetailsDraft(source) { Summary = "Changed introduction" };
        var extended = ExtendedProfileDraft.From(source);
        var result = extended.ApplyTo(details.ApplyTo(source));
        Assert.Equal(ProfileJson.Hash(source with { Summary = details.Summary }), ProfileJson.Hash(result));
        Assert.Equal(before, ProfileJson.Hash(source));
        Assert.False(Assert.Single(result.Glances).Enabled);
        Assert.Equal(8, ExistingAdditionalContent.Read(result).Count);
    }

    [Fact]
    public async Task LegacyRemovalUsesNormalWriterCasAndCannotOverwriteNewerPublicData()
    {
        var source = LegacyProfile();
        await using var harness = await OwnProfileHarness.CreateAsync([source]);
        var selected = ExistingAdditionalContent.Read(source).First(entry => entry.Kind == AdditionalContentKind.RpHook);
        var pending = ExistingAdditionalContent.Remove(source, selected);
        var saved = await harness.Session.SaveEditorAsync(pending, source.Revision, ProfileJson.Hash(source));
        await harness.ReadyAsync();
        Assert.Equal(2, saved.Revision);
        Assert.Equal("Hook B", Assert.Single(saved.RpHooks).Title);
        Assert.Equal(ProfileJson.Hash(pending with { Revision = 2 }), ProfileJson.Hash((await harness.Store.LoadLocalAsync(source.Id))!));
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.SaveEditorAsync(pending, source.Revision, ProfileJson.Hash(source)));
        Assert.Equal(2, (await harness.Store.LoadLocalAsync(source.Id))!.Revision);
    }

    private static ProfileDocument LegacyProfile() => ProfileTests.Valid() with
    {
        Summary = "Unchanged summary", OocInformation = "Unchanged OOC", Background = "Unchanged history",
        RpHooks = [new("Hook A", "Full hook text A"), new("Hook B", "Full hook text B")],
        ExtraFields = new() { ["Extra A"] = "Full extra value A", ["Extra B"] = "Full extra value B" },
        PersonalityTraits = [new("Quiet", "Talkative", 73), new("Calm", "Restless", 25)],
        Companions = [new() { Name = "Companion A", Species = "Chocobo", Description = "Full companion description A", IconId = 123 },
            new() { Name = "Companion B", Description = "Full companion description B", IconId = 456 }],
        Glances = [new("Unchanged glance", "Unchanged text")], RpLanguages = ["Common", "Doman"],
        Portrait = new("image/png", PngFixture.Create()),
    };
}
