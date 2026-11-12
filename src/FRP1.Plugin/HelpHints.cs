using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FRP1.Plugin;

internal sealed class HelpHints
{
    private readonly Func<bool> enabled;

    public HelpHints(Func<bool> enabled, Action<HelpTopicId> open)
    {
        this.enabled = enabled;
        ArgumentNullException.ThrowIfNull(open);
    }

    public void AfterItem(HelpTopicId topic)
    {
        if (!enabled() || (!ImGui.IsItemHovered() && !ImGui.IsItemFocused())) return;
        var available = Vector2.Max(Vector2.One, ImGui.GetMainViewport().WorkSize - new Vector2(16));
        var width = MathF.Min(ImGui.GetFontSize() * 26, available.X);
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

    public void Badge(HelpTopicId topic) { }

    public void Heading(string label, HelpTopicId topic)
    {
        ImGui.TextUnformatted(label);
        AfterItem(topic);
    }

    public bool Section(string label, HelpTopicId topic, bool defaultOpen = false)
    {
        ImGui.PushID("section-" + topic);
        try
        {
            var expanded = ImGui.CollapsingHeader(label + "###SectionHeader",
                defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
            AfterItem(topic);
            return expanded;
        }
        finally { ImGui.PopID(); }
    }
}
