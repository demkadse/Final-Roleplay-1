using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using FRP1.Plugin.Profiles;

namespace FRP1.Plugin.UiProfiles;

internal static class RichTextView
{
    public static void Draw(IReadOnlyList<RichBlock> blocks)
    {
        foreach (var block in blocks)
        {
            var factor = block.Kind == RichBlockKind.Heading ? 1.08f : 1f;
            var size = ImGui.GetFontSize() * factor;
            var origin = ImGui.GetCursorScreenPos();
            var available = Math.Max(12f, ImGui.GetContentRegionAvail().X - 5f);
            var indent = block.Kind == RichBlockKind.Bullet ? size : 0;
            var x = origin.X + indent;
            var y = origin.Y;
            var right = origin.X + available;
            var left = origin.X + indent;
            var lineHeight = size + ImGui.GetStyle().ItemSpacing.Y;
            var draw = ImGui.GetWindowDrawList();
            if (indent != 0) draw.AddText(origin, ImGui.GetColorU32(ImGuiCol.Text), "•");
            void NewLine() { x = left; y += lineHeight; }
            foreach (var run in block.Runs)
            {
                var color = Color(run.Color);
                foreach (var token in Words(run.Text))
                {
                    if (token == "\n") { NewLine(); continue; }
                    var width = ImGui.CalcTextSize(token).X * factor;
                    if (width <= right - left)
                    {
                        if (x > left && x + width > right) NewLine();
                        Text(draw, token, new Vector2(x, y), size, color, run.Bold || block.Kind == RichBlockKind.Heading, run.Italic);
                        x += width;
                        continue;
                    }
                    // Long unbroken words also wrap, without splitting surrogate pairs.
                    for (var offset = 0; offset < token.Length;)
                    {
                        var length = char.IsHighSurrogate(token[offset]) && offset + 1 < token.Length && char.IsLowSurrogate(token[offset + 1]) ? 2 : 1;
                        var piece = token.Substring(offset, length);
                        var pieceWidth = ImGui.CalcTextSize(piece).X * factor;
                        if (x > left && x + pieceWidth > right) NewLine();
                        Text(draw, piece, new Vector2(x, y), size, color, run.Bold || block.Kind == RichBlockKind.Heading, run.Italic);
                        x += pieceWidth;
                        offset += length;
                    }
                }
            }
            ImGui.Dummy(new Vector2(available, y - origin.Y + lineHeight));
        }
    }

    private static void Text(ImDrawListPtr draw, string text, Vector2 position, float size, uint color, bool bold, bool italic)
    {
        void Paint(float shift)
        {
            var start = draw.VtxBuffer.Size;
            draw.AddText(ImGui.GetFont(), size, position + new Vector2(shift, 0), color, text);
            if (!italic) return;
            var vertices = draw.VtxBuffer.AsSpan();
            for (var index = start; index < vertices.Length; index++)
                vertices[index].Pos.X += (position.Y + size - vertices[index].Pos.Y) * .15f;
        }
        Paint(0);
        if (bold) Paint(.55f);
    }

    internal static uint Color(string hex)
    {
        if (hex.Length == 7 && uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return ImGui.ColorConvertFloat4ToU32(new Vector4(((value >> 16) & 255) / 255f,
                ((value >> 8) & 255) / 255f, (value & 255) / 255f, 1));
        return ImGui.GetColorU32(ImGuiCol.Text);
    }

    private static IEnumerable<string> Words(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            if (text[start] == '\n') { yield return "\n"; start++; continue; }
            var whitespace = char.IsWhiteSpace(text[start]);
            var end = start + 1;
            while (end < text.Length && text[end] != '\n' && char.IsWhiteSpace(text[end]) == whitespace) end++;
            yield return text[start..end].Replace("\t", "    ", StringComparison.Ordinal);
            start = end;
        }
    }
}
