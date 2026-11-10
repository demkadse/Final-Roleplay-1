using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using FRP1.Core.Profiles;

namespace FRP1.Plugin.GameIntegration;

public readonly record struct GameActorKey(string Name, uint HomeWorldId)
{
    public GameActorKey Canonical() => new((Name ?? "").Trim().ToUpperInvariant(), HomeWorldId);
}

// Detached projection only: no register, resolver, game object or profile reference crosses threads.
public sealed record GameDisplayEntry(GameActorKey Actor, string RpName, string Title, ContentRating Rating,
    string? NameColor, Vector3 Position, float Distance, uint IconId = 0);

// SourceToken identifies the immutable actor capture (normally Social.ActorSnapshots itself).
// After a zone change a newer capture is required before any location can be reused.
public sealed record GamePresentationInput(uint TerritoryId, uint MapId, IReadOnlyList<GameDisplayEntry> Entries, object SourceToken);

public enum ChatSenderPartKind { Other, PlayerLink, Text }
public readonly record struct ChatSenderPart(ChatSenderPartKind Kind, string? Text = null, GameActorKey Player = default);

public static class GameDisplayRules
{
    public const int MaximumActors = 512;

    public static FrozenDictionary<GameActorKey, GameDisplayEntry> CopyUnique(IEnumerable<GameDisplayEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var next = new Dictionary<GameActorKey, GameDisplayEntry>();
        var ambiguous = new HashSet<GameActorKey>();
        var count = 0;
        foreach (var entry in entries)
        {
            if (++count > MaximumActors) throw new InvalidDataException("Too many local display entries.");
            if (entry is null || !ValidText(entry.Actor.Name, 80, false) || entry.Actor.HomeWorldId is 0 or > 65535 ||
                !ValidText(entry.RpName, 160, false) || !ValidText(entry.Title, 256, true) || !Enum.IsDefined(entry.Rating) ||
                !Finite(entry.Position) || !float.IsFinite(entry.Distance) || entry.Distance < 0 || entry.IconId > 1_000_000)
                throw new InvalidDataException("Invalid local display entry.");
            var key = entry.Actor.Canonical();
            if (!next.TryAdd(key, entry with { Actor = key, NameColor = TryParseColor(entry.NameColor, out _) ? entry.NameColor : null }))
                ambiguous.Add(key);
        }
        foreach (var key in ambiguous) next.Remove(key);
        return next.ToFrozenDictionary();
    }

    // An exact text payload belongs to its adjacent player link. Never match a substring or the message body.
    public static bool IsExactPlayerText(string? text, string? playerName) =>
        !string.IsNullOrEmpty(playerName) && string.Equals(text, playerName, StringComparison.Ordinal);

    public static string SingleLine(string text) => text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

    public static bool TryFindChatReplacement(IReadOnlyList<ChatSenderPart> parts,
        IReadOnlyDictionary<GameActorKey, GameDisplayEntry> allowed, out int index, out string replacement)
    {
        index = -1; replacement = "";
        ArgumentNullException.ThrowIfNull(parts); ArgumentNullException.ThrowIfNull(allowed);
        if (parts.Count is < 2 or > 64 || parts.Count(part => part.Kind == ChatSenderPartKind.PlayerLink) != 1) return false;
        for (var position = 0; position + 1 < parts.Count; position++)
        {
            var link = parts[position]; var text = parts[position + 1];
            if (link.Kind != ChatSenderPartKind.PlayerLink || text.Kind != ChatSenderPartKind.Text ||
                !IsExactPlayerText(text.Text, link.Player.Name) || !allowed.TryGetValue(link.Player.Canonical(), out var entry) ||
                entry.Rating != ContentRating.General) continue;
            index = position + 1; replacement = entry.RpName; return true;
        }
        return false;
    }

    public static bool TryParseColor(string? value, out uint rgba)
    {
        rgba = 0;
        if (value?.Length != 7 || value[0] != '#' ||
            !uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)) return false;
        rgba = (rgb << 8) | 0xff;
        return true;
    }

    public static bool TryMapCoordinates(Vector3 position, uint sizeFactor, int offsetX, int offsetY, out Vector2 coordinates)
    {
        coordinates = default;
        if (!Finite(position) || sizeFactor is 0 or > 1000 || Math.Abs((long)offsetX) > 100000 || Math.Abs((long)offsetY) > 100000)
            return false;
        var scale = sizeFactor / 100f;
        coordinates = new Vector2(
            41f / scale * (((position.X + offsetX) * scale + 1024f) / 2048f) + 1f,
            41f / scale * (((position.Z + offsetY) * scale + 1024f) / 2048f) + 1f);
        return float.IsFinite(coordinates.X) && float.IsFinite(coordinates.Y);
    }

    private static bool ValidText(string? value, int limit, bool allowEmpty) => value is not null && value.Length <= limit &&
        (allowEmpty || !string.IsNullOrWhiteSpace(value)) && !value.Any(char.IsControl);
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
