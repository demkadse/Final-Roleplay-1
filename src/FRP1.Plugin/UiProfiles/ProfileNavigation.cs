using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FRP1.Plugin.UiProfiles;

internal static class ProfileNavigation
{
    public static void Draw(ref ProfileArea selected)
    {
        var available = MathF.Max(1, ImGui.GetContentRegionAvail().X);
        var padding = ImGui.GetStyle().FramePadding;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var used = 0f;
        foreach (var area in ProfileAreas.All)
        {
            var label = ProfileUiText.Get(ProfileAreas.Title(area));
            var width = MathF.Min(available, ImGui.CalcTextSize(label).X + padding.X * 2);
            if (used > 0 && used + spacing + width <= available) { ImGui.SameLine(); used += spacing; }
            else used = 0;
            var textWidth = MathF.Max(1, width - padding.X * 2);
            var height = ImGui.CalcTextSize(label, false, textWidth).Y + padding.Y * 2;
            ImGui.PushID((int)area);
            var active = selected == area;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.Header]);
            try
            {
                var before = ImGui.GetCursorScreenPos();
                if (ImGui.Button("##profile-area", new Vector2(width, height))) selected = area;
                var after = ImGui.GetCursorScreenPos();
                ImGui.SetCursorScreenPos(before + padding);
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth);
                try { ImGui.TextUnformatted(label); }
                finally { ImGui.PopTextWrapPos(); }
                ImGui.SetCursorScreenPos(after);
            }
            finally { if (active) ImGui.PopStyleColor(); ImGui.PopID(); }
            used += width;
        }
        ImGui.Separator();
    }
}
