using System.Numerics;
using Hexa.NET.ImGui;
using StbEditor.App.Ui;
using StbEditor.Core;
using StbEditor.Render;
using static StbEditor.App.Ui.Loc;

namespace StbEditor.App.Panels;

/// <summary>
/// CK's Reference window: a double-click on an object in the render window opens it — the base object (Edit Base), 3D data with
/// step buttons, the reference check boxes and CK's tabs (enable parent, ownership, lock, teleport, extra, linked refs, linked
/// from, scripts and every other field). The same sections as the Inspector's Reference tab, one object at a time.
/// </summary>
internal sealed class ReferenceWindow
{
    private SceneObject? _object;
    private bool _open;
    private float _moveStep = 1f;
    private float _turnStep = 1f;
    private float _scaleStep = 0.01f;

    public void Open(SceneObject obj)
    {
        _object = obj;
        _open = true;
        ImGui.SetWindowFocus(WindowName);
    }

    private static string WindowName => T("Reference") + "###referencewindow";

    public void Draw(EditorApp app)
    {
        if (!_open || _object is not { } obj) return;
        // The object can go (cell unloaded, deleted, swapped by a reload): close rather than edit a stale one.
        if (obj.IsDeleted || app.RenderWindow.Scene?.Objects.Contains(obj) != true) { _open = false; return; }

        ImGui.SetNextWindowSize(app.Scaled(560, 640), ImGuiCond.FirstUseEver);
        if (ImGui.Begin(WindowName, ref _open, ImGuiWindowFlags.NoSavedSettings))
        {
            var session = app.Session!;
            ImGui.TextUnformatted($"{obj.BaseEditorId ?? "?"} [{obj.BaseSignature}]");
            ImGui.SameLine();
            ImGui.TextColored(Theme.TextDim, session.Workspace.FormIdText(obj.Reference));
            if (obj.Base is { } baseKey)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton(T("Edit base"))) app.FormWindows.Open(baseKey);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Open the base object's form"));
            }
            if (obj.BaseSignature == "DOOR" && app.RenderWindow.TeleportOfObject(app, obj) is not null)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton(T("Go through the door"))) app.RenderWindow.AskGoThroughDoor(app, obj);
            }

            if (ImGui.BeginTabBar("##reftabs", ImGuiTabBarFlags.FittingPolicyScroll))
            {
                if (ImGui.BeginTabItem(T("3D Data") + "###ref3d"))
                {
                    Draw3D(app, obj);
                    ImGui.EndTabItem();
                }
                ReferenceProperties.DrawTabs(app, obj);
                if (ImGui.BeginTabItem(T("Linked from") + "###reflinkedfrom"))
                {
                    DrawLinkedFrom(app, obj);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
    }

    /// <summary>Position / rotation (degrees) / scale with CK's step buttons: each click moves by the step, one undo step.</summary>
    private void Draw3D(EditorApp app, SceneObject obj)
    {
        var window = app.RenderWindow;
        var state = obj.State;
        ImGui.SeparatorText(T("Position"));
        Axis("X", state.Position.X, _moveStep, v => state with { Position = state.Position with { X = v } }, T("Position"));
        Axis("Y", state.Position.Y, _moveStep, v => state with { Position = state.Position with { Y = v } }, T("Position"));
        Axis("Z", state.Position.Z, _moveStep, v => state with { Position = state.Position with { Z = v } }, T("Position"));
        Step("##movestep", ref _moveStep);

        ImGui.SeparatorText(T("Rotation"));
        var degrees = new Vector3(TransformMath.Degrees(state.Rotation.X), TransformMath.Degrees(state.Rotation.Y), TransformMath.Degrees(state.Rotation.Z));
        Axis("X##rot", degrees.X, _turnStep, v => state with { Rotation = state.Rotation with { X = TransformMath.Radians(v) } }, T("Rotation"), "%.4f°");
        Axis("Y##rot", degrees.Y, _turnStep, v => state with { Rotation = state.Rotation with { Y = TransformMath.Radians(v) } }, T("Rotation"), "%.4f°");
        Axis("Z##rot", degrees.Z, _turnStep, v => state with { Rotation = state.Rotation with { Z = TransformMath.Radians(v) } }, T("Rotation"), "%.4f°");
        Step("##turnstep", ref _turnStep);
        if (ImGui.SmallButton(T("Reset rotation")))
        {
            window.PreviewFieldEdit(obj, state with { Rotation = Vector3.Zero });
            window.CommitFieldEdit(T("Reset rotation"));
        }

        ImGui.SeparatorText(T("Scale"));
        Axis(T("Scale") + "##scale", state.Scale, _scaleStep, v => state with { Scale = Math.Clamp(v, 0.01f, 10f) }, T("Scale"), "%.4f");
        Step("##scalestep", ref _scaleStep);

        ImGui.Spacing();
        if (ImGui.SmallButton(T("Show in the view"))) window.FrameObject(obj);

        void Axis(string label, float value, float step, Func<float, RefState> with, string name, string format = "%.4f")
        {
            ImGui.PushID(label);
            if (ImGui.ArrowButton("##less", ImGuiDir.Left)) { window.PreviewFieldEdit(obj, with(value - step)); window.CommitFieldEdit(name); }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 9);
            if (ImGui.DragFloat("##value", ref value, step * 0.1f, 0, 0, format)) window.PreviewFieldEdit(obj, with(value));
            if (ImGui.IsItemDeactivatedAfterEdit()) window.CommitFieldEdit(name);
            ImGui.SameLine();
            if (ImGui.ArrowButton("##more", ImGuiDir.Right)) { window.PreviewFieldEdit(obj, with(value + step)); window.CommitFieldEdit(name); }
            ImGui.SameLine();
            ImGui.TextUnformatted(label.Split("##")[0]);
            ImGui.PopID();
        }

        static void Step(string id, ref float step)
        {
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 6);
            if (Fields.Float(T("Step") + id, step, out var value, "%.4g") && value > 0) step = value;
        }
    }

    /// <summary>CK's Linked From tab: references whose linked refs point here, from the Use Info index.</summary>
    private static void DrawLinkedFrom(EditorApp app, SceneObject obj)
    {
        var session = app.Session!;
        if (!session.Uses.IsCompletedSuccessfully)
        {
            ImGui.TextColored(Theme.TextDim, T("The Use Info index is still being built…"));
            return;
        }
        var uses = session.Uses.Result.UsesOf(obj.Reference).Where(u => u.Subrecord is "XLKR" or "XESP" or "XAPR").ToList();
        if (uses.Count == 0)
        {
            ImGui.TextColored(Theme.TextDim, T("No reference links, enable-parents or activates this one."));
            return;
        }
        ImGui.TextColored(Theme.TextDim, T("Double-click goes to the reference."));
        if (!ImGui.BeginTable("##linkedfrom", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY)) return;
        ImGui.TableSetupColumn(T("Reference"));
        ImGui.TableSetupColumn(T("How"));
        ImGui.TableSetupColumn(T("Plugin"));
        ImGui.TableHeadersRow();
        for (var i = 0; i < uses.Count; i++)
        {
            var use = uses[i];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.PushID(i);
            if (ImGui.Selectable($"{use.Type} {session.Workspace.FormIdText(use.Source)}", false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick)
                && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && use.Parent is { } cell)
                app.RenderWindow.GoToReference(app, use.Source, cell);
            ImGui.PopID();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(use.Subrecord switch { "XLKR" => T("linked ref"), "XESP" => T("enable parent"), _ => T("activate parent") });
            ImGui.TableNextColumn();
            ImGui.TextColored(Theme.TextDim, use.Plugin.Name);
        }
        ImGui.EndTable();
    }
}
