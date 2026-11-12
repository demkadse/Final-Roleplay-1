using System.Numerics;
using Dalamud.Bindings.ImGui;
using FRP1.Plugin.Theming;

namespace FRP1.Plugin;

internal static class NativeThemeRenderer
{
    public static IDisposable Begin(ThemeDefinition theme, bool reduceDecoration)
    {
        var scope = new StyleScope();
        try
        {
            var palette = theme.Palette;
            scope.Color(ImGuiCol.Text, palette.Text);
            scope.Color(ImGuiCol.TextDisabled, palette.MutedText);
            scope.Color(ImGuiCol.WindowBg, palette.Background);
            scope.Color(ImGuiCol.ChildBg, palette.Panel);
            scope.Color(ImGuiCol.PopupBg, palette.Panel);
            scope.Color(ImGuiCol.Border, palette.Border);
            scope.Color(ImGuiCol.TitleBg, palette.Panel);
            scope.Color(ImGuiCol.TitleBgActive, palette.Selected);
            scope.Color(ImGuiCol.TitleBgCollapsed, palette.Panel);
            scope.Color(ImGuiCol.FrameBg, palette.Input);
            scope.Color(ImGuiCol.FrameBgHovered, palette.Hover);
            scope.Color(ImGuiCol.FrameBgActive, palette.Selected);
            scope.Color(ImGuiCol.Button, palette.Button);
            scope.Color(ImGuiCol.ButtonHovered, palette.Hover);
            scope.Color(ImGuiCol.ButtonActive, palette.Selected);
            scope.Color(ImGuiCol.Header, palette.Selected);
            scope.Color(ImGuiCol.HeaderHovered, palette.Hover);
            scope.Color(ImGuiCol.HeaderActive, palette.Selected);
            scope.Color(ImGuiCol.Tab, palette.Panel);
            scope.Color(ImGuiCol.TabActive, palette.Selected);
            scope.Color(ImGuiCol.TabHovered, palette.Hover);
            scope.Color(ImGuiCol.TabUnfocused, palette.Panel);
            scope.Color(ImGuiCol.TabUnfocusedActive, palette.Selected);
            scope.Color(ImGuiCol.CheckMark, palette.Accent);
            scope.Color(ImGuiCol.SliderGrab, palette.Accent);
            scope.Color(ImGuiCol.SliderGrabActive, palette.Focus);
            scope.Color(ImGuiCol.Separator, palette.Border);
            scope.Color(ImGuiCol.SeparatorHovered, palette.Focus);
            scope.Color(ImGuiCol.SeparatorActive, palette.Focus);
            scope.Color(ImGuiCol.ResizeGrip, palette.Border);
            scope.Color(ImGuiCol.ResizeGripHovered, palette.Accent);
            scope.Color(ImGuiCol.ResizeGripActive, palette.Focus);
            scope.Color(ImGuiCol.ScrollbarBg, palette.Background);
            scope.Color(ImGuiCol.ScrollbarGrab, palette.Border);
            scope.Color(ImGuiCol.ScrollbarGrabHovered, palette.Accent);
            scope.Color(ImGuiCol.ScrollbarGrabActive, palette.Focus);
            scope.Color(ImGuiCol.TableHeaderBg, palette.Panel);
            scope.Color(ImGuiCol.TableBorderStrong, palette.Border);
            scope.Color(ImGuiCol.TableBorderLight, palette.Border);
            scope.Color(ImGuiCol.NavHighlight, palette.Focus);
            scope.Color(ImGuiCol.TextSelectedBg, palette.Selected);
            scope.Variable(ImGuiStyleVar.WindowRounding, theme.Rounding);
            scope.Variable(ImGuiStyleVar.WindowBorderSize, 1f);
            scope.Variable(ImGuiStyleVar.FrameRounding, 3f);
            scope.Variable(ImGuiStyleVar.FrameBorderSize, 0f);
            scope.Variable(ImGuiStyleVar.TabRounding, theme.Rounding);
            scope.Variable(ImGuiStyleVar.PopupRounding, theme.Rounding);
            scope.Variable(ImGuiStyleVar.WindowPadding, new Vector2(9, 8));
            scope.Variable(ImGuiStyleVar.FramePadding, new Vector2(7, 3));
            scope.Variable(ImGuiStyleVar.ItemSpacing, new Vector2(6, 5));
            scope.Variable(ImGuiStyleVar.CellPadding, new Vector2(0, 2));
            scope.Variable(ImGuiStyleVar.ScrollbarSize, 10f);
            return scope;
        }
        catch { scope.Dispose(); throw; }
    }

    public static void DrawInterior(ThemeDefinition theme, bool reduceDecoration)
    {
        if (reduceDecoration) return;
        var draw = ImGui.GetWindowDrawList();
        var position = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        if (size.X < 20) return;
        var start = position + new Vector2(9, size.Y - 1);
        draw.AddLine(start, start + new Vector2(MathF.Min(36, size.X - 18), 0),
            ImGui.ColorConvertFloat4ToU32(theme.Palette.Accent));
    }

    private sealed class StyleScope : IDisposable
    {
        private int colors;
        private int variables;
        private bool disposed;
        public void Color(ImGuiCol color, Vector4 value) { ImGui.PushStyleColor(color, value); colors++; }
        public void Variable(ImGuiStyleVar variable, float value) { ImGui.PushStyleVar(variable, value); variables++; }
        public void Variable(ImGuiStyleVar variable, Vector2 value) { ImGui.PushStyleVar(variable, value); variables++; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (variables != 0) ImGui.PopStyleVar(variables);
            if (colors != 0) ImGui.PopStyleColor(colors);
        }
    }
}
