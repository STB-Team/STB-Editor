using System.Numerics;
using Hexa.NET.ImGui;

namespace StbEditor.App.Ui;

/// <summary>Rounded studio controls. ImGui still owns IDs, input, navigation and the last item.</summary>
internal static class Studio
{
    /// <summary>Native Begin with a padded close target; always pair with ImGui.End, even when false.</summary>
    public static bool BeginWindow(string title, ref bool open, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        => WindowChrome.Begin(title, ref open, flags);

    public static bool BeginPopupModal(string title, ref bool open, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        => WindowChrome.BeginPopupModal(title, ref open, flags);

    public static bool Button(string label, Vector2 size = default, bool primary = false, bool selected = false)
        => Control(label, size, primary, selected, false);

    public static bool Tab(string label, bool selected, Vector2 size = default)
        => Control(label, size, false, selected, true);

    private static bool Control(string label, Vector2 size, bool primary, bool selected, bool tab)
    {
        var style = ImGui.GetStyle();
        var padding = style.FramePadding;
        var scale = ImGui.GetFontSize() / 16f;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, padding + new Vector2(tab ? 5 : 0, tab ? 3 : 0) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f * scale);
        var emphasized = selected || primary;
        if (emphasized)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.SurfaceSelected);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.SurfaceHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.ButtonActive);
        }
        // One continuous native border, not bevel strokes or an accent stripe over rounded corners.
        // DisabledAlpha and the navigation cursor stay entirely under ImGui's control.
        ImGui.PushStyleColor(ImGuiCol.Border, emphasized ? Theme.ControlSelectedBorder : Theme.ControlBorder);
        var pressed = ImGui.Button(label, size);
        ImGui.PopStyleColor();
        if (emphasized) ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(3);
        return pressed;
    }

    /// <summary>A surface lip, not a second title bar; docking tabs retain all native hit targets.</summary>
    public static void PanelEdge()
    {
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        var padding = ImGui.GetStyle().WindowPadding;
        var y = ImGui.GetCursorScreenPos().Y - padding.Y * 0.5f;
        var draw = ImGui.GetWindowDrawList();
        draw.AddLine(new Vector2(pos.X + padding.X, y), new Vector2(pos.X + size.X - padding.X, y), ImGui.GetColorU32(Theme.EdgeLight));
    }

    public static void Section(string label)
    {
        ImGui.Spacing();
        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        ImGui.TextColored(Theme.TextDim, label);
        var min = ImGui.GetItemRectMin();
        var end = ImGui.GetItemRectMax();
        if (right > end.X + ImGui.GetFontSize())
            ImGui.GetWindowDrawList().AddLine(new Vector2(end.X + ImGui.GetFontSize() * 0.6f, (min.Y + end.Y) * 0.5f),
                new Vector2(right, (min.Y + end.Y) * 0.5f), ImGui.GetColorU32(ImGuiCol.Separator));
    }
}
