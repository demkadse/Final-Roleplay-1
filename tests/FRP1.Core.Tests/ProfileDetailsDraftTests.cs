using FRP1.Core.Profiles;
using FRP1.Plugin;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class ProfileDetailsDraftTests
{
    [Fact]
    public void OpeningAndSavingDetailsPreservesAllPublicFields()
    {
        var source = DetailedProfile();
        var result = new ProfileDetailsDraft(source).ApplyTo(source);
        Assert.Equal(ProfileJson.Hash(source), ProfileJson.Hash(result));
        Assert.NotSame(source.Glances, result.Glances);
        Assert.NotSame(source.RpHooks, result.RpHooks);
    }

    [Fact]
    public void ImportedDetailsCanBeSavedAsNewProfileWithoutLosingTargetFields()
    {
        var imported = ProfileJson.Import(ProfileJson.Export(DetailedProfile()));
        var target = ProfileTests.Valid() with
        {
            Revision = 7, DisplayName = "Local replacement name", FirstName = "First", LastName = "Last",
            Title = "Local title", Race = "Local race", Clan = "Local clan", Age = "27", Gender = "Other",
            Origin = "Local origin", Occupation = "Local occupation", Appearance = "Local appearance",
            Personality = "Local personality", Background = "Local background", ContentRating = ContentRating.Mature18,
            ExtraFields = new() { ["Custom"] = "Preserved" },
        };
        var result = new ProfileDetailsDraft(imported).ApplyTo(target);
        Assert.Equal(target.Id, result.Id);
        Assert.Equal(target.Revision, result.Revision);
        Assert.Equal(target.ContentRating, result.ContentRating);
        Assert.Equal(target.DisplayName, result.DisplayName);
        Assert.Equal(target.FirstName, result.FirstName);
        Assert.Equal(target.LastName, result.LastName);
        Assert.Equal(target.Title, result.Title);
        Assert.Equal(target.Race, result.Race);
        Assert.Equal(target.Clan, result.Clan);
        Assert.Equal(target.Age, result.Age);
        Assert.Equal(target.Gender, result.Gender);
        Assert.Equal(target.Origin, result.Origin);
        Assert.Equal(target.Occupation, result.Occupation);
        Assert.Equal(target.Appearance, result.Appearance);
        Assert.Equal(target.Personality, result.Personality);
        Assert.Equal(target.Background, result.Background);
        Assert.Equal(target.ExtraFields, result.ExtraFields);
        Assert.Equal(imported.Status, result.Status);
        Assert.Equal(imported.Availability, result.Availability);
        Assert.Equal(imported.Summary, result.Summary);
        Assert.Equal(imported.Pronouns, result.Pronouns);
        Assert.Equal(imported.OocInformation, result.OocInformation);
        Assert.Equal(imported.CurrentLocation, result.CurrentLocation);
        Assert.Equal(imported.FreeCompany, result.FreeCompany);
        Assert.Equal(imported.Glances, result.Glances);
        Assert.Equal(imported.RpHooks, result.RpHooks);
    }

    [Fact]
    public void EditingOrDiscardingDraftCannotMutateSourceProfile()
    {
        var source = DetailedProfile();
        var before = ProfileJson.Hash(source);
        var draft = new ProfileDetailsDraft(source)
        {
            Status = RpStatus.Afk, Availability = RpAvailability.Unavailable, Summary = "Changed", Pronouns = "Other",
            OocInformation = "Changed", CurrentLocation = "Changed", FreeCompany = "Changed",
        };
        draft.Glances[0] = new GlanceEntry("Changed", "Changed", 99);
        draft.Glances.Add(new GlanceEntry("Added", "Entry"));
        draft.RpHooks.Clear();
        Assert.Equal(before, ProfileJson.Hash(source));
    }

    [Fact]
    public void LaterDraftEditsCannotChangeCapturedSaveSnapshot()
    {
        var source = DetailedProfile();
        var draft = new ProfileDetailsDraft(source);
        var snapshot = draft.ApplyTo(source);
        var capturedHash = ProfileJson.Hash(snapshot);
        draft.Status = RpStatus.Afk;
        draft.Summary = "Later edit";
        draft.Glances.Clear();
        draft.RpHooks.Add(new RpHook("Later hook", "Later text"));
        Assert.Equal(capturedHash, ProfileJson.Hash(snapshot));
        Assert.NotEqual(capturedHash, ProfileJson.Hash(draft.ApplyTo(source)));
    }

    [Fact]
    public void EditingSourceCollectionsAfterOpeningCannotChangeDraft()
    {
        var source = DetailedProfile();
        var draft = new ProfileDetailsDraft(source);
        source.Glances.Clear();
        source.RpHooks.Clear();
        Assert.Single(draft.Glances);
        Assert.Single(draft.RpHooks);
        Assert.Equal("Eyes", draft.Glances[0].Label);
        Assert.Equal("Library", draft.RpHooks[0].Title);
    }

    private static ProfileDocument DetailedProfile() => ProfileTests.Valid() with
    {
        Status = RpStatus.Ic, Availability = RpAvailability.Open,
        Summary = "A synthetic introduction", Pronouns = "she/her", OocInformation = "Synthetic OOC text",
        CurrentLocation = "Gridania", FreeCompany = "Synthetic company",
        Glances = [new("Eyes", "Green", 1234)], RpHooks = [new("Library", "Looking for books")],
    };
}
