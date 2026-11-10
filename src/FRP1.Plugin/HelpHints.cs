using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FRP1.Plugin;

internal sealed class HelpHints(Func<bool> enabled, Action<HelpTopicId> open)
{
    public void AfterItem(HelpTopicId topic)
    {
        if (!enabled() || (!ImGui.IsItemHovered() && !ImGui.IsItemFocused())) return;
        var available = Vector2.Max(Vector2.One, ImGui.GetMainViewport().WorkSize - new Vector2(16));
        var width = MathF.Min(ImGui.GetFontSize() * 29, available.X);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, available.Y));
        ImGui.BeginTooltip();
        try
        {
            ImGui.PushTextWrapPos(0);
            try { ImGui.TextUnformatted(HelpContent.Get(topic).Summary); }
            finally { ImGui.PopTextWrapPos(); }
        }
        finally { ImGui.EndTooltip(); }
    }

    public void Badge(HelpTopicId topic)
    {
        if (!enabled()) return;
        ImGui.PushID("help-" + topic);
        try
        {
            if (ImGui.SmallButton("?")) open(topic);
            AfterItem(topic);
        }
        finally { ImGui.PopID(); }
    }
}
