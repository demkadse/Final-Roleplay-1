using FRP1.Core.Profiles;
using FRP1.Plugin.UiProfiles;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class ExtendedProfileDraftTests
{
    [Fact]
    public void ExtendedEditorRoundtripPreservesAllPublicModulesAndTargetIdentity()
    {
        var source = ProfileModuleTests.Expanded() with { Portrait = new("image/png", PngFixture.Create()), Summary = "Preserved summary", Background = "Preserved background" };
        var draft = ExtendedProfileDraft.From(source);
        var result = draft.ApplyTo(source);
        Assert.Equal(ProfileJson.Hash(source), ProfileJson.Hash(result));
        var target = source with { Id = Guid.NewGuid(), Revision = 99, DisplayName = "Other target name", Summary = "Other target summary" };
        var applied = draft.ApplyTo(target);
        Assert.Equal(target.Id, applied.Id);
        Assert.Equal(99, applied.Revision);
        Assert.Equal(target.DisplayName, applied.DisplayName);
        Assert.Equal(target.Summary, applied.Summary);
        Assert.Equal(source.Preferences, applied.Preferences);
        Assert.Equal(source.Companions, applied.Companions);
    }

    [Fact]
    public void DraftEditsAndLaterSourceEditsCannotMutateEachOtherOrCapturedSnapshot()
    {
        var source = ProfileModuleTests.Expanded() with { Portrait = new("image/png", PngFixture.Create()) };
        var originalHash = ProfileJson.Hash(source);
        var draft = ExtendedProfileDraft.From(source);
        draft.PersonalityTraits.Add(new("New", "Trait", 25));
        draft.Companions[0] = draft.Companions[0] with { Name = "Draft edit" };
        draft.Portrait!.Data[0] = 0;
        Assert.Equal(originalHash, ProfileJson.Hash(source));
        Assert.Equal(137, draft.Portrait!.Data[0]);
        var snapshot = draft.ApplyTo(source);
        var snapshotHash = ProfileJson.Hash(snapshot);
        draft.Companions.Clear();
        draft.PersonalityTraits.Clear();
        draft.Portrait = null;
        source.Portrait!.Data[0] = 0;
        source.Companions.Clear();
        Assert.Equal(snapshotHash, ProfileJson.Hash(snapshot));
        Assert.Equal(137, snapshot.Portrait!.Data[0]);
    }

    [Fact]
    public void PortraitSetterCopiesCallerDataAndRejectsNonNormalizedMetadata()
    {
        var draft = new ExtendedProfileDraft();
        var data = PngFixture.Create();
        draft.Portrait = new("image/png", data);
        data[0] = 0;
        Assert.Equal(137, draft.Portrait!.Data[0]);
        Assert.Throws<InvalidDataException>(() => draft.Portrait = new("image/png", PngFixture.Create(metadata: true)));
        Assert.Equal(137, draft.Portrait!.Data[0]);
    }

    [Fact]
    public void LanguageLinesAreTrimmedAndEmptyLinesRemovedWithoutChangingOtherProfileFields()
    {
        var source = ProfileTests.Valid();
        var draft = ExtendedProfileDraft.From(source);
        draft.LanguagesText = " Deutsch \n\n English\r\n  ";
        var applied = draft.ApplyTo(source);
        Assert.Equal(new[] { "Deutsch", "English" }, applied.RpLanguages);
        Assert.Equal(source.Id, applied.Id);
        Assert.Equal(source.ContentRating, applied.ContentRating);
        Assert.Equal(source.DisplayName, applied.DisplayName);
    }
}
