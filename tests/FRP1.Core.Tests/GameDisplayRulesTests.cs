using System.Numerics;
using FRP1.Core.Profiles;
using FRP1.Plugin.GameIntegration;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class GameDisplayRulesTests
{
    [Fact]
    public void DuplicateSameWorldActorsAreRemovedWhileOtherWorldRemainsDistinct()
    {
        var first = Entry();
        var duplicate = first with { Actor = new(" real name ", 1), RpName = "Conflicting RP name" };
        var otherWorld = first with { Actor = new("Real Name", 2) };
        var copied = GameDisplayRules.CopyUnique([first, duplicate, otherWorld]);
        Assert.Single(copied);
        Assert.False(copied.ContainsKey(first.Actor.Canonical()));
        Assert.True(copied.ContainsKey(otherWorld.Actor.Canonical()));
        Assert.True(Assert.IsAssignableFrom<IDictionary<GameActorKey, GameDisplayEntry>>(copied).IsReadOnly);
    }

    [Fact]
    public void DisplaySnapshotsRejectInvalidLocationsAndRemainBounded()
    {
        var entry = Entry();
        foreach (var invalid in new[] { entry with { Position = new(float.NaN, 0, 0) }, entry with { Distance = -1 },
                     entry with { Actor = new("Name", 0) }, entry with { RpName = "Bad\0Name" }, entry with { Rating = (ContentRating)999 } })
            Assert.Throws<InvalidDataException>(() => GameDisplayRules.CopyUnique([invalid]));
        Assert.Throws<InvalidDataException>(() => GameDisplayRules.CopyUnique(Enumerable.Range(0, GameDisplayRules.MaximumActors + 1).Select(i => entry with { Actor = new("Name " + i, 1) })));
        Assert.Null(GameDisplayRules.CopyUnique([entry with { NameColor = "untrusted value" }]).Values.Single().NameColor);
    }

    [Fact]
    public void OptionalProfileIconsDefaultToNoneAndFrozenSnapshotsEnforceIconBoundary()
    {
        var entry = Entry();
        Assert.Equal(0u, entry.IconId);
        var source = new List<GameDisplayEntry> { entry with { IconId = 1_000_000 } };
        var snapshot = GameDisplayRules.CopyUnique(source);
        source[0] = entry with { IconId = 0 };
        source.Clear();
        Assert.Equal(1_000_000u, Assert.Single(snapshot.Values).IconId);
        Assert.Throws<InvalidDataException>(() => GameDisplayRules.CopyUnique([entry with { IconId = 1_000_001 }]));
    }

    [Theory]
    [InlineData("#AABBCC", true)]
    [InlineData("#aabbcc", true)]
    [InlineData("red", false)]
    [InlineData("#GG0000", false)]
    [InlineData("#AABBCC00", false)]
    [InlineData(null, false)]
    public void ColorsAcceptOnlyBoundedRgbHex(string? input, bool valid)
    {
        Assert.Equal(valid, GameDisplayRules.TryParseColor(input, out var color));
        if (valid) Assert.Equal(0xAABBCCFFu, color);
        else Assert.Equal(0u, color);
    }

    [Fact]
    public void ChatReplacementSelectsOnlyAdjacentExactSenderTextAndPreservesMentionOrBodyParts()
    {
        var entry = Entry();
        var allowed = GameDisplayRules.CopyUnique([entry]);
        ChatSenderPart[] parts = [new(ChatSenderPartKind.Text, "Real Name"), new(ChatSenderPartKind.PlayerLink, Player: entry.Actor),
            new(ChatSenderPartKind.Text, "Real Name"), new(ChatSenderPartKind.Other), new(ChatSenderPartKind.Text, "Real Name says hello")];
        Assert.True(GameDisplayRules.TryFindChatReplacement(parts, allowed, out var index, out var replacement));
        Assert.Equal(2, index);
        Assert.Equal(entry.RpName, replacement);
        Assert.Equal("Real Name", parts[0].Text);
        Assert.Equal("Real Name says hello", parts[4].Text);
    }

    [Theory]
    [InlineData(ContentRating.General, true)]
    [InlineData(ContentRating.Mature18, false)]
    public void PersistentChatReplacementNeverEmbedsMatureProfileNames(ContentRating rating, bool expected)
    {
        var entry = Entry() with { Rating = rating };
        var allowed = GameDisplayRules.CopyUnique([entry]);
        ChatSenderPart[] parts = [new(ChatSenderPartKind.PlayerLink, Player: entry.Actor), new(ChatSenderPartKind.Text, entry.Actor.Name)];
        Assert.Equal(expected, GameDisplayRules.TryFindChatReplacement(parts, allowed, out var index, out var replacement));
        if (expected)
        {
            Assert.Equal(1, index);
            Assert.Equal(entry.RpName, replacement);
        }
        else
        {
            Assert.Equal(-1, index);
            Assert.Equal("", replacement);
        }
    }

    [Theory]
    [InlineData("substring")]
    [InlineData("non-adjacent")]
    [InlineData("wrong-world")]
    [InlineData("two-links")]
    [InlineData("too-many")]
    [InlineData("no-link")]
    public void AmbiguousOrNonmatchingChatPayloadsAreNeverRewritten(string reason)
    {
        var entry = Entry();
        var allowed = GameDisplayRules.CopyUnique([entry]);
        ChatSenderPart link = new(ChatSenderPartKind.PlayerLink, Player: entry.Actor);
        ChatSenderPart exact = new(ChatSenderPartKind.Text, "Real Name");
        IReadOnlyList<ChatSenderPart> parts = reason switch
        {
            "substring" => [link, new(ChatSenderPartKind.Text, "Real Name says")],
            "non-adjacent" => [link, new(ChatSenderPartKind.Other), exact],
            "wrong-world" => [link with { Player = new("Real Name", 2) }, exact],
            "two-links" => [link, exact, link, exact],
            "too-many" => new[] { link, exact }.Concat(Enumerable.Repeat(new ChatSenderPart(ChatSenderPartKind.Other), 63)).ToArray(),
            _ => [exact, exact],
        };
        Assert.False(GameDisplayRules.TryFindChatReplacement(parts, allowed, out var index, out var replacement));
        Assert.Equal(-1, index);
        Assert.Equal("", replacement);
    }

    [Fact]
    public void KnownMapAnchorAndHeightIndependenceAreCorrectAndInvalidInputsFailClosed()
    {
        Assert.True(GameDisplayRules.TryMapCoordinates(Vector3.Zero, 100, 0, 0, out var center));
        Assert.Equal(new Vector2(21.5f, 21.5f), center);
        Assert.True(GameDisplayRules.TryMapCoordinates(new(0, 10000, 0), 100, 0, 0, out var differentHeight));
        Assert.Equal(center, differentHeight);
        Assert.True(GameDisplayRules.TryMapCoordinates(new(1024, 0, -1024), 100, 0, 0, out var bounds));
        Assert.Equal(new Vector2(42, 1), bounds);
        Assert.False(GameDisplayRules.TryMapCoordinates(new(float.NaN, 0, 0), 100, 0, 0, out _));
        Assert.False(GameDisplayRules.TryMapCoordinates(Vector3.Zero, 0, 0, 0, out _));
        Assert.False(GameDisplayRules.TryMapCoordinates(Vector3.Zero, 1001, 0, 0, out _));
        Assert.False(GameDisplayRules.TryMapCoordinates(Vector3.Zero, 100, int.MinValue, 0, out _));
    }

    private static GameDisplayEntry Entry() => new(new("Real Name", 1), "Synthetic RP name", "Title", ContentRating.General, "#AABBCC", Vector3.Zero, 10);
}
