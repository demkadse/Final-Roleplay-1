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
            try
            {
                var content = HelpContent.Get(topic);
                ImGui.TextUnformatted(content.Title);
                ImGui.Separator();
                ImGui.TextUnformatted(content.Summary);
                ImGui.Spacing();
                ImGui.TextUnformatted("Mehr dazu: „? Hilfe“ direkt am Abschnitt oder „Hilfe / Erste Schritte“.");
            }
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
            DrawButton(topic, "? Hilfe: " + HelpContent.Get(topic).Title);
        }
        finally { ImGui.PopID(); }
    }

    public void Heading(string label, HelpTopicId topic) => DrawHeader(label, topic, collapsible: false, defaultOpen: true);

    public bool Section(string label, HelpTopicId topic, bool defaultOpen = false) =>
        DrawHeader(label, topic, collapsible: true, defaultOpen);

    private bool DrawHeader(string label, HelpTopicId topic, bool collapsible, bool defaultOpen)
    {
        var showHelp = enabled();
        var available = MathF.Max(1, ImGui.GetContentRegionAvail().X);
        var helpWidth = showHelp ? MathF.Min(ImGui.CalcTextSize("? Hilfe").X + ImGui.GetStyle().FramePadding.X * 2,
            MathF.Max(1, available * .40f)) : 1;
        var expanded = !collapsible;
        ImGui.PushID((collapsible ? "help-section-" : "help-heading-") + topic);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(0, ImGui.GetStyle().CellPadding.Y));
        try
        {
            // Keep the same table/field IDs when hints are toggled. An empty one-pixel
            // help column leaves the heading usable without introducing a second control.
            if (ImGui.BeginTable("##AssociatedHelp", 2, ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.SizingStretchProp,
                    new Vector2(available, 0)))
            {
                try
                {
                    ImGui.TableSetupColumn("Heading", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Help", ImGuiTableColumnFlags.WidthFixed, helpWidth);
                    ImGui.TableNextColumn();
                    expanded = DrawHeaderItem(label, collapsible, defaultOpen);
                    if (showHelp) AfterItem(topic);
                    ImGui.TableNextColumn();
                    if (showHelp) DrawButton(topic, "? Hilfe");
                }
                finally { ImGui.EndTable(); }
            }
            else expanded = DrawHeaderItem(label, collapsible, defaultOpen);
        }
        finally { ImGui.PopStyleVar(); ImGui.PopID(); }
        // The caller draws section content after the table, at full available width.
        return expanded;
    }

    private static bool DrawHeaderItem(string label, bool collapsible, bool defaultOpen)
    {
        if (collapsible) return ImGui.CollapsingHeader(label + "###SectionHeader",
            defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
        ImGui.AlignTextToFramePadding();
        ImGui.PushTextWrapPos(0);
        try { ImGui.TextUnformatted(label); }
        finally { ImGui.PopTextWrapPos(); }
        return true;
    }

    private void DrawButton(HelpTopicId topic, string label)
    {
        var width = MathF.Min(ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2,
            MathF.Max(1, ImGui.GetContentRegionAvail().X));
        var padding = ImGui.GetStyle().FramePadding;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(padding.X, MathF.Max(1, padding.Y * .55f)));
        try
        {
            if (ImGui.Button(label + "###OpenHelp", new Vector2(width, 0))) open(topic);
        }
        finally { ImGui.PopStyleVar(); }
        AfterItem(topic);
    }
}
