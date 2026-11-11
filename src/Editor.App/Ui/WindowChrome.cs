using System.Numerics;
using Hexa.NET.ImGui;

namespace StbEditor.App.Ui;

/// <summary>
/// Larger close targets inside the space already reserved by native window titles/docking tabs.
/// ImGui 1.92's CloseButton is font-sized regardless of FramePadding. Keep its keyboard/navigation
/// behavior, but use a separate native button ID for mouse presses across the entire padded slot.
/// All native structure access goes through the versioned Hexa.NET binding, never byte offsets.
/// </summary>
internal static unsafe class WindowChrome
{
    public static bool Begin(string title, ref bool open, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        var visible = ImGui.Begin(title, ref open, flags);
        if (open) CloseTarget(ref open);
        return visible && open;
    }

    public static bool BeginPopupModal(string title, ref bool open, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        if (!ImGui.BeginPopupModal(title, ref open, flags)) return false;
        CloseTarget(ref open);
        if (open) return true;
        ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
        return false;
    }

    /// <summary>Actual clickable screen rectangle, also available to input-driven shell tests.</summary>
    public static bool TryGetCloseTarget(ImGuiWindowPtr window, out ImRect target)
        => Geometry(window, out target, out _, out _, previousFrame: true);

    private static bool Geometry(ImGuiWindowPtr window, out ImRect target, out ImGuiWindowPtr host, out float fontSize, bool previousFrame = false)
    {
        target = default;
        host = window;
        fontSize = 0;
        if (window.Handle == null || !window.HasCloseButton) return false;
        var padding = ImGui.GetStyle().FramePadding;
        ImRect bounds;
        if (window.DockIsActive)
        {
            var node = window.DockNode;
            if (node.Handle == null || node.HostWindow.Handle == null || node.TabBar.Handle == null) return false;
            var frame = ImGui.GetFrameCount();
            if (node.TabBar.CurrFrameVisible != frame && (!previousFrame || node.TabBar.CurrFrameVisible != frame - 1)) return false;
            host = node.HostWindow;
            padding = node.TabBar.FramePadding;
            bounds = window.DC.DockTabItemRect;
            fontSize = node.TabBar.BarRect.Max.Y - node.TabBar.BarRect.Min.Y - 2 * padding.Y;
            // A scrolled/clipped tab must not claim its neighbor's space or the scroll buttons.
            var clipMin = MathF.Max(node.TabBar.BarRect.Min.X, node.TabBar.ScrollingRectMinX);
            var clipMax = MathF.Min(node.TabBar.BarRect.Max.X, node.TabBar.ScrollingRectMaxX);
            if (bounds.Max.X - padding.X > clipMax || bounds.Max.X <= clipMin) return false;
            bounds.Min.X = MathF.Max(bounds.Min.X, clipMin);
        }
        else
        {
            if (window.Hidden || (window.Flags & ImGuiWindowFlags.NoTitleBar) != 0) return false;
            bounds = new ImRect(window.Pos, window.Pos + new Vector2(window.Size.X, window.TitleBarHeight));
            fontSize = window.FontRefSize;
        }
        if (fontSize <= 0 || bounds.Max.X <= bounds.Min.X || bounds.Max.Y <= bounds.Min.Y) return false;
        var nativeMin = new Vector2(MathF.Max(bounds.Min.X, bounds.Max.X - padding.X - fontSize), bounds.Min.Y + padding.Y);
        var center = nativeMin + new Vector2(fontSize * 0.5f);
        var half = new Vector2(MathF.Round(fontSize * 1.5f) * 0.5f);
        target = new ImRect(Vector2.Max(center - half, bounds.Min), Vector2.Min(center + half, bounds.Max));
        return target.Max.X > target.Min.X && target.Max.Y > target.Min.Y;
    }

    private static void CloseTarget(ref bool open)
    {
        var window = ImGuiP.GetCurrentWindow();
        if (!Geometry(window, out var target, out var host, out var fontSize)) return;
        var context = ImGui.GetCurrentContext();
        var nativeCloseId = ImGuiP.ImHashStr("#CLOSE"u8, (nuint)0, window.ID);
        var id = ImGuiP.ImHashStr("#StudioClose"u8, (nuint)0, window.ID);
        var titleId = ImGuiP.ImHashStr("#TITLEBAR"u8, (nuint)0, host.ID);
        var mouse = ImGui.GetMousePos();
        var inside = mouse.X >= target.Min.X && mouse.X < target.Max.X && mouse.Y >= target.Min.Y && mouse.Y < target.Max.Y;
        var nativeHovered = context.HoveredId == 0 || context.HoveredId == nativeCloseId
            || context.HoveredId == window.TabId || context.HoveredId == titleId || context.HoveredId == id;
        var canHover = inside && nativeHovered && context.HoveredWindow.Handle == host.Handle
            && ImGuiP.IsWindowContentHoverable(host);
        // Native Begin may already have activated the tab or its small close button. Claim only a
        // fresh press in this window's reserved close slot; never steal a drag or another widget.
        if (canHover && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && context.ActiveIdIsJustActivated
            && (context.ActiveId == nativeCloseId || context.ActiveId == window.TabId
                || context.ActiveId == titleId || context.ActiveId == window.MoveId))
        {
            ImGuiP.ClearActiveID();
            ImGuiP.SetKeyOwner(ImGuiKey.MouseLeft, id);
        }
        if (canHover) ImGuiP.SetHoveredID(id);

        var lastItem = context.LastItemData;
        var currentWindow = context.CurrentWindow;
        var itemFlags = context.CurrentItemFlags;
        var clip = host.ClipRect;
        // Begin has already installed the content clip, which excludes a floating title bar.
        // Hit-test against the native outer clip just for this title/tab control.
        host.ClipRect = host.OuterRectClipped;
        context.CurrentWindow = host;
        context.CurrentItemFlags = ImGuiItemFlags.NoNav;
        ImGuiP.KeepAliveID(id);
        bool hovered, held;
        var pressed = ImGuiP.ButtonBehavior(target, id, &hovered, &held,
            (ImGuiButtonFlags)(ImGuiButtonFlagsPrivate.NoNavFocus | ImGuiButtonFlagsPrivate.NoFocus));
        host.ClipRect = clip;
        context.CurrentWindow = currentWindow;
        context.CurrentItemFlags = itemFlags;
        context.LastItemData = lastItem;
        if (pressed) open = false;

        // Native content has already been drawn. Keep the small glyph, outline its real target,
        // and redraw the glyph only when the larger hover/pressed fill covers the native one.
        var draw = host.DrawList;
        var scale = fontSize / 16f;
        draw.PushClipRect(target.Min, target.Max, false);
        if (hovered || held)
        {
            draw.AddRectFilled(target.Min, target.Max, ImGui.GetColorU32(held ? Theme.ButtonActive : Theme.SurfaceHover), 3 * scale);
            var center = (target.Min + target.Max) * 0.5f - new Vector2(0.5f * scale);
            var extent = (fontSize * 0.5f * 0.7071f - scale);
            var a = new Vector2(extent, extent);
            var b = new Vector2(extent, -extent);
            var color = ImGui.GetColorU32(ImGuiCol.Text);
            draw.AddLine(center - a, center + a, color, scale);
            draw.AddLine(center - b, center + b, color, scale);
        }
        draw.AddRect(target.Min + new Vector2(0.5f * scale), target.Max - new Vector2(0.5f * scale),
            ImGui.GetColorU32(Theme.Alpha(Theme.ControlBorder, hovered || held ? 1f : 0.55f)), 3 * scale);
        draw.PopClipRect();
    }
}
