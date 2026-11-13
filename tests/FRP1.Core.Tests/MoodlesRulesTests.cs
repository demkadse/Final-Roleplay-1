using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Integrations;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class MoodlesRulesTests
{
    internal static MoodlesStatus Status(string label = "Synthetic mood", string description = "Synthetic description", int icon = 123)
    {
        Assert.True(MoodlesRules.TryCreateStatus(2, Guid.NewGuid(), icon, label, description, out var status));
        return Assert.IsType<MoodlesStatus>(status);
    }

    [Fact]
    public void ContentFingerprintTracksAllCopiedFieldsButDoesNotTreatSourceIdentityAsContent()
    {
        var original = Status();
        var equivalent = Status();
        Assert.Equal(64, original.ContentHash.Length);
        Assert.All(original.ContentHash, c => Assert.True(char.IsAsciiHexDigitUpper(c)));
        Assert.Equal(original.ContentHash, equivalent.ContentHash);
        Assert.NotEqual(original.ContentHash, Status(label: "Other").ContentHash);
        Assert.NotEqual(original.ContentHash, Status(description: "Other").ContentHash);
        Assert.NotEqual(original.ContentHash, Status(icon: 124).ContentHash);
        Assert.True(MoodlesRules.TryCreateStatus(2, original.StatusId, 123, original.Label, original.Description, out var repeated));
        Assert.Equal(original, repeated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void UnknownSourceSchemasFailClosed(int version) =>
        Assert.False(MoodlesRules.TryCreateStatus(version, Guid.NewGuid(), 1, "Label", "Body", out _));

    [Fact]
    public void EmptySourceIdentityAndNegativeIconFailClosed()
    {
        Assert.False(MoodlesRules.TryCreateStatus(2, Guid.Empty, 1, "Label", "Body", out _));
        Assert.False(MoodlesRules.TryCreateStatus(2, Guid.NewGuid(), -1, "Label", "Body", out _));
        Assert.True(MoodlesRules.TryCreateStatus(2, Guid.NewGuid(), 1_000_000, "Label", "Body", out _));
        Assert.False(MoodlesRules.TryCreateStatus(2, Guid.NewGuid(), 1_000_001, "Label", "Body", out _));
    }

    public static IEnumerable<object?[]> InvalidText => new string?[]
    {
        null, "\0", "\t", "\n", "\r", "\u001b", "\u007f", "\u0085", new('x', 4097),
    }.Select(value => new object?[] { value });

    [Theory]
    [MemberData(nameof(InvalidText))]
    public void MalformedOrOverlongSourceStringsFailClosedInEitherField(string? value)
    {
        Assert.False(MoodlesRules.TryCreateStatus(2, Guid.NewGuid(), 1, value, "Body", out _));
        Assert.False(MoodlesRules.TryCreateStatus(2, Guid.NewGuid(), 1, "Label", value, out _));
    }

    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDFFF)]
    public void UnpairedUtf16IsRejectedBeforeAnyReplacementCharacterCanBeIntroduced(int codeUnit)
    {
        var value = "x" + (char)codeUnit + "y";
        Assert.False(MoodlesRules.TryCreateStatus(2, Guid.NewGuid(), 1, value, "Body", out _));
        Assert.False(MoodlesRules.TryCreateStatus(2, Guid.NewGuid(), 1, "Label", value, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("é界😀")]
    public void EmptyAndValidUnicodeSourceTextCanBeReadWithoutBecomingAnAutomaticClassification(string text)
    {
        var status = Status(text, text);
        Assert.Null(MoodlesRules.ReviewedRating(status, new(true)));
        Assert.False(MoodlesRules.CanPreview(status, new(true), new(AudienceSelection.Under18)));
    }

    [Fact]
    public void DetachedOutputEnforcesCountAndUtf8BudgetAtTheirExactBoundaries()
    {
        var countBoundary = Enumerable.Range(0, 128).Select(_ => Status("", "")).ToList();
        Assert.True(MoodlesRules.TryValidateStatuses(countBoundary, out var detached));
        countBoundary.Clear();
        Assert.Equal(128, detached.Count);
        Assert.False(MoodlesRules.TryValidateStatuses(Enumerable.Range(0, 129).Select(_ => Status("", "")).ToArray(), out _));

        // Each CJK character occupies three UTF-8 bytes; 8 * (4096 + 4096) ASCII bytes is the other exact boundary.
        var byteBoundary = Enumerable.Range(0, 8).Select(_ => Status(new('x', 4096), new('y', 4096))).ToList();
        Assert.True(MoodlesRules.TryValidateStatuses(byteBoundary, out _));
        byteBoundary.Add(Status("x", ""));
        Assert.False(MoodlesRules.TryValidateStatuses(byteBoundary, out _));
        Assert.False(MoodlesRules.TryValidateStatuses(Enumerable.Range(0, 3)
            .Select(_ => Status(new('界', 4096), new('界', 4096))).ToArray(), out _));
    }

    [Fact]
    public void NullListsDuplicateIdentitiesNullItemsAndForgedContentHashesInvalidateTheWholeSnapshot()
    {
        var status = Status();
        Assert.True(MoodlesRules.TryValidateStatuses(Array.Empty<MoodlesStatus>(), out var empty));
        Assert.Empty(empty);
        Assert.False(MoodlesRules.TryValidateStatuses(null, out _));
        Assert.False(MoodlesRules.TryValidateStatuses([status, status], out _));
        Assert.False(MoodlesRules.TryValidateStatuses([status, null!], out _));
        Assert.False(MoodlesRules.TryValidateStatuses([status with { Label = "Changed" }], out _));
        Assert.False(MoodlesRules.TryValidateStatuses([status with { ContentHash = new('0', 64) }], out _));
        Assert.False(MoodlesRules.TryValidateStatuses([status with { IconId = uint.MaxValue }], out _));
    }

    [Fact]
    public void OptionsDefaultOffUnknownAndDetachCallerOwnedSelections()
    {
        var defaults = MoodlesRules.ValidateOptions(new());
        Assert.False(defaults.Enabled);
        Assert.Empty(defaults.Bindings!);
        var source = Status();
        var bindings = new List<MoodlesBinding> { new(source.StatusId, source.ContentHash) };
        var validated = MoodlesRules.ValidateOptions(new(true, bindings));
        bindings.Clear();
        Assert.Null(Assert.Single(validated.Bindings!).Rating);
        Assert.Null(MoodlesRules.ReviewedRating(source, validated));
        var boundary = Enumerable.Range(0, 5).Select(_ => new MoodlesBinding(Guid.NewGuid(), source.ContentHash)).ToArray();
        Assert.Equal(5, MoodlesRules.ValidateOptions(new(true, boundary)).Bindings!.Count);
        Assert.Throws<InvalidDataException>(() => MoodlesRules.ValidateOptions(new(true, [.. boundary, new(Guid.NewGuid(), source.ContentHash)])));
    }

    [Fact]
    public void BindingIdentityHashRatingAndUniquenessAreStrictlyValidated()
    {
        var status = Status();
        var valid = new MoodlesBinding(status.StatusId, status.ContentHash, ContentRating.General);
        foreach (var invalid in new MoodlesBinding[]
        {
            null!, valid with { StatusId = Guid.Empty }, valid with { ContentHash = null! },
            valid with { ContentHash = new('A', 63) }, valid with { ContentHash = new('A', 65) },
            valid with { ContentHash = new('G', 64) }, valid with { ContentHash = new('a', 64) },
            valid with { Rating = (ContentRating)999 },
        }) Assert.Throws<InvalidDataException>(() => MoodlesRules.ValidateOptions(new(true, [invalid])));
        Assert.Throws<InvalidDataException>(() => MoodlesRules.ValidateOptions(new(true, [valid, valid])));
    }

    [Fact]
    public void AReviewedRatingOnlyAppliesToTheSameSourceAndExactCurrentContent()
    {
        var status = Status();
        var options = new MoodlesOptions(true, [new(status.StatusId, status.ContentHash, ContentRating.General)]);
        Assert.Equal(ContentRating.General, MoodlesRules.ReviewedRating(status, options));
        Assert.Null(MoodlesRules.ReviewedRating(Status() with { ContentHash = status.ContentHash }, options));
        Assert.Null(MoodlesRules.ReviewedRating(status with { ContentHash = Status("Changed").ContentHash }, options));
    }

    public static IEnumerable<object[]> AudienceMatrix =>
        from audience in new[] { new AudiencePreferences(), new(AudienceSelection.Under18), new(AudienceSelection.Under18, true),
            new(AudienceSelection.Adult), new(AudienceSelection.Adult, true) }
        from rating in new ContentRating?[] { null, ContentRating.General, ContentRating.Mature18 }
        from target in new[] { ContentRating.General, ContentRating.Mature18 }
        select new object[] { audience, rating!, target };

    [Theory]
    [MemberData(nameof(AudienceMatrix))]
    public void PreviewAndCopyRequireReviewedContentAndCurrentAudienceConsent(AudiencePreferences audience, ContentRating? rating, ContentRating target)
    {
        var status = Status();
        var options = new MoodlesOptions(true, [new(status.StatusId, status.ContentHash, rating)]);
        var adultOptIn = audience.Selection == AudienceSelection.Adult && audience.MatureContentOptIn;
        var audienceSelected = audience.Selection != AudienceSelection.Unspecified;
        Assert.Equal(audienceSelected && (rating == ContentRating.General || adultOptIn), MoodlesRules.CanPreview(status, options, audience));
        Assert.Equal(audienceSelected && (rating == ContentRating.General || rating == ContentRating.Mature18 && target == ContentRating.Mature18 && adultOptIn),
            MoodlesRules.CanCopy(status, rating, target, audience));
    }

    [Theory]
    [InlineData(80, 1024, 1_000_000, true)]
    [InlineData(81, 1024, 1_000_000, false)]
    [InlineData(80, 1025, 1_000_000, false)]
    [InlineData(80, 1024, 1_000_001, false)]
    [InlineData(1, 0, 0, true)]
    [InlineData(0, 1, 1, false)]
    public void CanonicalGlanceBoundariesAreCheckedWithoutTruncation(int labelLength, int bodyLength, int icon, bool expected)
    {
        var status = Status(new('L', labelLength), new('B', bodyLength), Math.Min(icon, 1_000_000)) with { IconId = (uint)icon };
        Assert.Equal(expected, MoodlesRules.CanCopy(status, ContentRating.General, ContentRating.General, new(AudienceSelection.Under18)));
    }

    [Fact]
    public void WhitespaceOnlyTitleAndUndefinedTargetCannotBeCopied()
    {
        var audience = new AudiencePreferences(AudienceSelection.Adult, true);
        Assert.False(MoodlesRules.CanCopy(Status("   "), ContentRating.General, ContentRating.General, audience));
        Assert.False(MoodlesRules.CanCopy(Status(), ContentRating.General, (ContentRating)999, audience));
    }
}
