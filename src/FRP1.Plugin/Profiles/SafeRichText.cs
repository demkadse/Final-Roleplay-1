using FRP1.Core.Profiles;

namespace FRP1.Plugin.Profiles;

public enum RichBlockKind { Paragraph, Heading, Bullet }
public sealed record RichRun(string Text, bool Bold = false, bool Italic = false, string Color = "");
public sealed record RichBlock(RichBlockKind Kind, int HeadingLevel, IReadOnlyList<RichRun> Runs);

public static class SafeRichText
{
    public const int MaximumCharacters = 32 * 1024;
    public const int MaximumTokens = 1024;
    public const int MaximumDepth = 4;

    public static IReadOnlyList<RichBlock> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumCharacters) throw new InvalidDataException("Formatted text exceeds the size limit.");
        var blocks = new List<RichBlock>();
        var remaining = MaximumTokens;
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var content = raw;
            var kind = RichBlockKind.Paragraph;
            var level = 0;
            while (level < 3 && level < content.Length && content[level] == '#') level++;
            if (level > 0 && content.Length > level && content[level] == ' ')
            { kind = RichBlockKind.Heading; content = content[(level + 1)..]; }
            else if (content.StartsWith("- ", StringComparison.Ordinal) || content.StartsWith("* ", StringComparison.Ordinal))
            { kind = RichBlockKind.Bullet; level = 0; content = content[2..]; }
            else level = 0;
            if (--remaining < 0) return PlainFallback(text);
            var runs = ParseLine(content, ref remaining);
            if (runs is null) return PlainFallback(text);
            blocks.Add(new RichBlock(kind, level, runs));
        }
        return Array.AsReadOnly(blocks.ToArray());
    }

    public static string PlainText(IReadOnlyList<RichBlock> blocks) =>
        string.Join("\n", blocks.Select(block => string.Concat(block.Runs.Select(run => run.Text))));

    private static IReadOnlyList<RichBlock> PlainFallback(string text) =>
        Array.AsReadOnly(new[] { new RichBlock(RichBlockKind.Paragraph, 0,
            Array.AsReadOnly(new[] { new RichRun(text) })) });

    private static IReadOnlyList<RichRun>? ParseLine(string text, ref int remaining)
    {
        var result = new List<RichRun>();
        var stack = new Stack<(string Tag, bool Bold, bool Italic, string Color)>();
        var bold = false;
        var italic = false;
        var color = "";
        var start = 0;
        var cursor = 0;
        while (cursor < text.Length)
        {
            if (text[cursor] != '[') { cursor++; continue; }
            var end = text.IndexOf(']', cursor + 1);
            if (end < 0 || end - cursor > 32) { cursor++; continue; }
            var tag = text[(cursor + 1)..end];
            var opening = tag is "b" or "i" || (tag.StartsWith("color=", StringComparison.Ordinal) &&
                tag.Length == 13 && ProfileJson.IsColor(tag[6..]));
            var closing = tag is "/b" or "/i" or "/color";
            if (!opening && !closing) { cursor = end + 1; continue; }
            if (cursor > start)
            {
                if (--remaining < 0) return null;
                result.Add(new RichRun(text[start..cursor], bold, italic, color));
            }
            if (--remaining < 0) return null;
            if (opening)
            {
                if (stack.Count >= MaximumDepth) return Array.AsReadOnly(new[] { new RichRun(text) });
                var name = tag.StartsWith("color=", StringComparison.Ordinal) ? "color" : tag;
                stack.Push((name, bold, italic, color));
                if (name == "b") bold = true;
                else if (name == "i") italic = true;
                else color = tag[6..];
            }
            else
            {
                if (stack.Count == 0 || stack.Peek().Tag != tag[1..])
                    return Array.AsReadOnly(new[] { new RichRun(text) });
                var previous = stack.Pop();
                bold = previous.Bold; italic = previous.Italic; color = previous.Color;
            }
            cursor = end + 1;
            start = cursor;
        }
        if (stack.Count != 0) return Array.AsReadOnly(new[] { new RichRun(text) });
        if (start < text.Length || result.Count == 0)
        {
            if (--remaining < 0) return null;
            result.Add(new RichRun(text[start..], bold, italic, color));
        }
        return Array.AsReadOnly(result.ToArray());
    }
}
