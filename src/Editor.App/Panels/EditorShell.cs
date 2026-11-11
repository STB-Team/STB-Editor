using System.Numerics;
using Hexa.NET.ImGui;
using StbEditor.App.Ui;
using static StbEditor.App.Ui.Loc;

namespace StbEditor.App.Panels;

/// <summary>Task-oriented shortcuts over the existing editor commands; no separate editing state.</summary>
internal static class EditorShell
{
    public static void Draw(EditorApp app)
    {
        var em = ImGui.GetFontSize();
        var frame = ImGui.GetFrameHeight();
        var flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings;
        var height = frame * 2 + em * 2.2f;
        ImGui.PushStyleColor(ImGuiCol.WindowBg, ImGui.GetStyle().Colors[(int)ImGuiCol.MenuBarBg]);
        if (ImGuiP.BeginViewportSideBar("##EditorShell", ImGui.GetMainViewport(), ImGuiDir.Up, height, flags))
        {
            var draw = ImGui.GetWindowDrawList();
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            draw.AddLine(pos + new Vector2(0, size.Y - 1), pos + size - Vector2.UnitY, ImGui.GetColorU32(Theme.Shadow));
            ImGui.SetCursorPos(new Vector2(em * 0.8f, em * 0.45f));
            var wide = size.X > em * 92;
            var narrow = size.X < em * 51;
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, Vector2.Zero);
            if (ImGui.BeginTable("##studioidentity", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("##brand", ImGuiTableColumnFlags.WidthStretch, 1);
                ImGui.TableSetupColumn("##document", ImGuiTableColumnFlags.WidthStretch, wide ? 0.8f : 1.2f);
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(Theme.Accent, T("STB EDITOR"));
                if (wide)
                {
                    ImGui.SameLine(0, em);
                    ImGui.TextColored(Theme.TextDim, T("Creation studio"));
                }
                ImGui.TableNextColumn();
                ImGui.BeginDisabled(app.Session is null);
                var pending = app.Document.PendingCount;
                var name = app.ActivePlugin?.Name ?? T("No active file");
                var badge = pending > 0 ? TF($"{pending} unsaved") : T("Active file");
                var columnWidth = ImGui.GetContentRegionAvail().X;
                var badgeWidth = ImGui.CalcTextSize(badge).X + em * 0.8f;
                var showBadge = columnWidth > badgeWidth + em * 8;
                if (Studio.Button(name + "###shellactivefile", new Vector2(Math.Max(1, columnWidth - (showBadge ? badgeWidth : 0)), 0)))
                    app.ShowActiveFileDialog();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip((app.ActivePlugin?.Path ?? T("Choose the plugin that receives your edits"))
                        + (pending > 0 ? "\n" + TF($"{pending} unsaved") : ""));
                if (showBadge)
                {
                    ImGui.SameLine();
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextColored(pending > 0 ? Theme.Warning : Theme.TextDim, badge);
                }
                ImGui.EndDisabled();
                ImGui.EndTable();
            }
            ImGui.PopStyleVar();

            ImGui.SetCursorPosX(em * 0.8f);
            if (wide)
            {
                LayoutButton(app, LayoutPreset.World);
                ImGui.SameLine();
                LayoutButton(app, LayoutPreset.Records);
                ImGui.SameLine();
                LayoutButton(app, LayoutPreset.Classic);
            }
            else
            {
                ImGui.SetNextItemWidth(Math.Min(em * 13, size.X * (narrow ? 0.40f : 0.34f)));
                if (ImGui.BeginCombo("###layoutpreset", T(Label(DefaultLayout.Preset))))
                {
                    LayoutChoice(app, LayoutPreset.World);
                    LayoutChoice(app, LayoutPreset.Records);
                    LayoutChoice(app, LayoutPreset.Classic);
                    ImGui.EndCombo();
                }
            }
            ImGui.SameLine(0, em);
            ImGui.BeginDisabled(app.Session is null || app.Saver.IsBusy);
            if (Studio.Button(T("Save") + "###shellsave", primary: true)) app.Save();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(T("Save into the active file (Ctrl+S)"));
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (narrow)
            {
                if (Studio.Button(T("Actions") + "###shellactions")) ImGui.OpenPopup("##shellactions");
                if (ImGui.BeginPopup("##shellactions"))
                {
                    DrawHistory(app, false);
                    DrawSearch(app);
                    ImGui.EndPopup();
                }
            }
            else
            {
                DrawHistory(app, true);
                ImGui.SameLine();
                DrawSearch(app);
            }
        }
        ImGui.End();
        ImGui.PopStyleColor();
    }

    private static void DrawHistory(EditorApp app, bool inline)
    {
        var history = app.Document.History;
        ImGui.BeginDisabled(!history.CanUndo);
        if (Studio.Button(T("Undo") + "###shellundo")) app.Document.Undo();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(history.UndoName is { } undo ? TF($"Undo {undo}") : T("Undo"));
        ImGui.EndDisabled();
        if (inline) ImGui.SameLine();
        ImGui.BeginDisabled(!history.CanRedo);
        if (Studio.Button(T("Redo") + "###shellredo")) app.Document.Redo();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(history.RedoName is { } redo ? TF($"Redo {redo}") : T("Redo"));
        ImGui.EndDisabled();
    }

    private static void DrawSearch(EditorApp app)
    {
        ImGui.BeginDisabled(app.Session is null);
        if (Studio.Button(T("Search") + "###shellsearch"))
        {
            app.ObjectWindow.IsOpen = true;
            app.ObjectWindow.FocusSearch();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(T("Find a record by EditorID, name or FormID (Ctrl+F)"));
        ImGui.EndDisabled();
    }

    private static string Label(LayoutPreset preset) => preset switch
    {
        LayoutPreset.Records => "Records layout",
        LayoutPreset.Classic => "Classic layout",
        _ => "World layout",
    };

    private static void LayoutChoice(EditorApp app, LayoutPreset preset)
    {
        if (ImGui.Selectable(T(Label(preset)), DefaultLayout.Preset == preset)) app.ApplyLayout(preset);
    }

    private static void LayoutButton(EditorApp app, LayoutPreset preset)
    {
        if (Studio.Tab(T(Label(preset)) + "###layout" + preset, DefaultLayout.Preset == preset)) app.ApplyLayout(preset);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Arrange panels for this task. Open records, selection and unsaved edits are kept."));
    }
}
