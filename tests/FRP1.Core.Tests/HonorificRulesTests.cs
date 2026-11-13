using System.Text;
using System.Text.Json;
using FRP1.Plugin.Integrations;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class HonorificRulesTests
{
    [Theory]
    [InlineData("Keeper of Stories")]
    [InlineData("Étoile 星")]
    [InlineData("<color=red>/command</color>")]
    [InlineData("")]
    public void TitleIsDetachedPlainTextWithoutFormattingInterpretation(string candidate)
    {
        Assert.True(HonorificRules.TryReadTitle(JsonSerializer.Serialize(new { Title = candidate }), out var title));
        Assert.Equal(candidate, title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalFlagAndExternalFormattingDoNotChangePlainTitle(bool original)
    {
        var json = JsonSerializer.Serialize(new { Title = "Synthetic title", IsOriginal = original,
            Color = new { R = 1, G = 0, B = 0 }, IsPrefix = true });
        Assert.True(HonorificRules.TryReadTitle(json, out var title));
        Assert.Equal("Synthetic title", title);
    }

    [Fact]
    public void NullTitleMeansReadyWithoutAnImportableTitle()
    {
        Assert.True(HonorificRules.TryReadTitle("", out var empty));
        Assert.Empty(empty);
        Assert.True(HonorificRules.TryReadTitle("{\"Title\":null}", out var title));
        Assert.Equal("", title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"title\":\"wrong case\"}")]
    [InlineData("{\"Title\":1}")]
    [InlineData("{\"Title\":true}")]
    [InlineData("{\"Title\":{}}")]
    [InlineData("{\"Title\":[]}")]
    [InlineData("{\"Title\":\"a\",\"Title\":\"b\"}")]
    [InlineData("{\"Title\":\"a\",\"Unknown\":0,\"Unknown\":1}")]
    [InlineData("{\"Title\":\"a\",\"Unknown\":{\"x\":0,\"x\":1}}")]
    [InlineData("{\"Title\":\"a\",\"Unknown\":[{\"x\":0,\"x\":1}]}")]
    [InlineData("{\"Title\":\"a\",}")]
    [InlineData("/*comment*/{\"Title\":\"a\"}")]
    public void MalformedWrongTypeMissingAndDuplicatePayloadsFailClosed(string? json)
    {
        Assert.False(HonorificRules.TryReadTitle(json, out var title));
        Assert.Equal("", title);
    }

    [Theory]
    [InlineData('\0')]
    [InlineData('\n')]
    [InlineData('\r')]
    [InlineData('\t')]
    [InlineData('\u001f')]
    [InlineData('\u007f')]
    [InlineData('\u0085')]
    public void EscapedControlCharactersAreRejected(char control)
    {
        Assert.False(HonorificRules.TryReadTitle(JsonSerializer.Serialize(new { Title = $"a{control}b" }), out var title));
        Assert.Empty(title);
    }

    [Theory]
    [InlineData("""{"Title":"\uD800"}""")]
    [InlineData("""{"Title":"\uDC00"}""")]
    [InlineData("""{"Title":"\uD800\uD800"}""")]
    [InlineData("""{"Title":"\uDC00\uD800"}""")]
    [InlineData("""{"Title":"\uD800A"}""")]
    [InlineData("""{"Title":"valid","\uD800":0}""")]
    [InlineData("""{"Title":"valid","Unknown":{"\uDC00":0}}""")]
    [InlineData("""{"Title":"valid","Unknown":[{"\uD800":0}]}""")]
    public void InvalidEscapedSurrogatesInTitleOrPropertyNamesFailClosedWithoutThrowing(string json)
    {
        Assert.False(HonorificRules.TryReadTitle(json, out var title));
        Assert.Empty(title);
    }

    [Theory]
    [InlineData('\uD800')]
    [InlineData('\uDC00')]
    public void RawUnpairedUtf16InputCannotSilentlyBecomeAReplacementCharacter(char surrogate)
    {
        var json = "{\"Title\":\"" + surrogate + "\"}";
        Assert.False(HonorificRules.TryReadTitle(json, out var title));
        Assert.Empty(title);
    }

    [Fact]
    public void ValidRawAndEscapedSurrogatePairsPreserveTheExactEmojiTitle()
    {
        const string expected = "Story 🌟";
        Assert.True(HonorificRules.TryReadTitle("""{"Title":"Story \uD83C\uDF1F"}""", out var escaped));
        Assert.Equal(expected, escaped);
        Assert.True(HonorificRules.TryReadTitle("{\"Title\":\"" + expected + "\"}", out var raw));
        Assert.Equal(expected, raw);
        Assert.DoesNotContain("\uFFFD", raw);
    }

    [Fact]
    public void CanonicalTitleLimitAcceptsBoundaryAndRejectsOneExtraUtf16Unit()
    {
        var boundary = new string('a', 256);
        Assert.Equal(256, HonorificRules.MaximumTitleLength);
        Assert.True(HonorificRules.TryReadTitle(JsonSerializer.Serialize(new { Title = boundary }), out var title));
        Assert.Equal(boundary, title);
        Assert.False(HonorificRules.TryReadTitle(JsonSerializer.Serialize(new { Title = boundary + "a" }), out title));
        Assert.Empty(title);
        var emojiBoundary = string.Concat(Enumerable.Repeat("🌟", 128));
        Assert.True(HonorificRules.TryReadTitle(JsonSerializer.Serialize(new { Title = emojiBoundary }), out title));
        Assert.Equal(emojiBoundary, title);
        Assert.False(HonorificRules.TryReadTitle(JsonSerializer.Serialize(new { Title = emojiBoundary + "🌟" }), out _));
    }

    [Fact]
    public void PayloadLimitCountsUtf8BytesIncludingIgnoredFieldsAndWhitespace()
    {
        const string prefix = "{\"Title\":\"a\",\"Ignored\":\"";
        const string suffix = "\"}";
        var boundary = prefix + new string('a', HonorificRules.MaximumPayloadBytes - prefix.Length - suffix.Length) + suffix;
        Assert.Equal(HonorificRules.MaximumPayloadBytes, Encoding.UTF8.GetByteCount(boundary));
        Assert.True(HonorificRules.TryReadTitle(boundary, out _));
        Assert.False(HonorificRules.TryReadTitle(boundary + " ", out _));
        var multibyte = prefix + new string('星', 3000) + suffix;
        Assert.True(multibyte.Length < HonorificRules.MaximumPayloadBytes);
        Assert.False(HonorificRules.TryReadTitle(multibyte, out _));
    }

    [Fact]
    public void ExcessiveDepthInsideIgnoredFieldFailsClosed()
    {
        var json = "{\"Title\":\"a\",\"Ignored\":" + new string('[', 8) + "0" + new string(']', 8) + "}";
        Assert.False(HonorificRules.TryReadTitle(json, out var title));
        Assert.Empty(title);
    }

    [Fact]
    public void NativeYieldRequiresExactValidOwnEntityAndConfirmedReadiness()
    {
        var actor = new IntegrationActor(1, 2, 3, "Synthetic Actor", 4, 5, 6);
        Assert.True(HonorificRules.YieldOwnTitle(true, IntegrationStatus.Ready, actor, actor, 2, 3));
        Assert.False(HonorificRules.YieldOwnTitle(false, IntegrationStatus.Ready, actor, actor, 2, 3));
        Assert.False(HonorificRules.YieldOwnTitle(true, IntegrationStatus.Missing, actor, actor, 2, 3));
        Assert.False(HonorificRules.YieldOwnTitle(true, IntegrationStatus.Ready, actor, actor, 99, 3));
        Assert.False(HonorificRules.YieldOwnTitle(true, IntegrationStatus.Ready, actor, actor, 2, 99));
        foreach (var changed in new[] { actor with { ContentId = 99 }, actor with { ObjectId = 99 },
                     actor with { Address = 99 }, actor with { Name = "Other Actor" }, actor with { HomeWorldId = 99 },
                     actor with { TerritoryId = 99 }, actor with { MapId = 99 } })
            Assert.False(HonorificRules.YieldOwnTitle(true, IntegrationStatus.Ready, actor, changed, 2, 3));
        var invalid = actor with { ContentId = 0 };
        Assert.False(HonorificRules.YieldOwnTitle(true, IntegrationStatus.Ready, invalid, invalid, 2, 3));
    }
}
