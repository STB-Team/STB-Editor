using System.Numerics;
using Hexa.NET.ImGui;
using StbEditor.App.Services;
using StbEditor.Render;
using StbEditor.Core;

namespace StbEditor.App.Panels;

/// <summary>
/// Rubber-band selection (CK): a left drag on empty space, or Shift+drag anywhere, draws a rectangle; on release what is inside
/// is selected — objects by their centre with the object tools, navmesh vertices (and triangles with all three corners inside)
/// with the navmesh tool. Ctrl adds to the selection, Alt takes away.
/// </summary>
internal sealed partial class RenderWindow
{
    /// <summary>Screen position where the rectangle started (absolute, ImGui coordinates); null while none is drawn.</summary>
    private Vector2? _marqueeFrom;

    private const float MarqueeThreshold = 4f;

    /// <summary>The drag from <paramref name="pressedAt"/> is a rectangle by now.</summary>
    private bool MarqueeGrowing(Vector2 pressedAt)
    {
        if (_marqueeFrom is null && Vector2.Distance(ImGui.GetIO().MousePos, pressedAt) > MarqueeThreshold) _marqueeFrom = pressedAt;
        return _marqueeFrom is not null;
    }

    private (Vector2 Min, Vector2 Max) MarqueeRect()
    {
        var a = _marqueeFrom!.Value;
        var b = ImGui.GetIO().MousePos;
        return (Vector2.Min(a, b), Vector2.Max(a, b));
    }

    /// <summary>Screen point (absolute) of a world point, or null behind the camera.</summary>
    private Vector2? ToScreen(Vector3 p, Matrix4x4 viewProj, Vector2 origin, Vector2 size)
    {
        var clip = Vector4.Transform(new Vector4(p, 1), viewProj);
        if (clip.W <= 0) return null;
        return origin + new Vector2((clip.X / clip.W * 0.5f + 0.5f) * size.X, (0.5f - clip.Y / clip.W * 0.5f) * size.Y);
    }

    private static bool Inside((Vector2 Min, Vector2 Max) r, Vector2 p) => p.X >= r.Min.X && p.X <= r.Max.X && p.Y >= r.Min.Y && p.Y <= r.Max.Y;

    /// <summary>Objects whose centre is inside the rectangle replace / join / leave the selection.</summary>
    private void FinishObjectMarquee(EditorApp app, Vector2 origin, Vector2 size)
    {
        if (_scene is null || _renderer is null) { _marqueeFrom = null; return; }
        var rect = MarqueeRect();
        _marqueeFrom = null;
        var io = ImGui.GetIO();
        var viewProj = _camera.View * _camera.Projection(size.X / size.Y);
        var options = _renderer.Options;
        var inside = _scene.Objects.Where(o => !o.IsDeleted && !options.Hides(o) && !options.FrozenReferences.Contains(o.Reference)
                && (o.Kind != SceneObjectKind.Marker || options.ShowMarkers)
                && ToScreen(o.BoundsCenter, viewProj, origin, size) is { } at && Inside(rect, at))
            .ToList();
        if (io.KeyAlt) foreach (var o in inside) _selection.Remove(o);
        else
        {
            if (!io.KeyCtrl) _selection.Clear();
            foreach (var o in inside) _selection.Add(o);
        }
        if (_selection.FirstOrDefault() is { } first)
        {
            app.Select(first.Reference);
            _lastSyncedSelection = app.SelectionVersion;
        }
    }

    /// <summary>Navmesh vertices inside the rectangle, of the navmesh most of them belong to; triangles with all corners inside.</summary>
    private void FinishNavmeshMarquee(Vector2 origin, Vector2 size)
    {
        if (_scene is null) { _marqueeFrom = null; return; }
        var rect = MarqueeRect();
        _marqueeFrom = null;
        var io = ImGui.GetIO();
        var viewProj = _camera.View * _camera.Projection(size.X / size.Y);
        var best = (Mesh: default(Mutagen.Bethesda.Plugins.FormKey), Vertices: new HashSet<int>());
        foreach (var view in _scene.Navmeshes)
        {
            if (view.Record.Data is not { } data) continue;
            var picked = new HashSet<int>();
            for (var i = 0; i < data.Vertices.Count; i++)
                if (ToScreen(P(data.Vertices[i]), viewProj, origin, size) is { } at && Inside(rect, at)) picked.Add(i);
            // With a navmesh already selected and Ctrl / Alt, stay on it.
            var current = _navSelection?.Mesh == view.Key && (io.KeyCtrl || io.KeyAlt);
            if (current || (picked.Count > best.Vertices.Count && !(io.KeyCtrl || io.KeyAlt) )) best = (view.Key, picked);
            if (current) break;
        }
        if (best.Vertices.Count == 0 && !(io.KeyCtrl || io.KeyAlt)) { _navSelection = null; return; }
        var mesh = _scene.Navmeshes.First(v => v.Key == best.Mesh).Record.Data!;
        var triangles = Enumerable.Range(0, mesh.Triangles.Count)
            .Where(t => best.Vertices.Contains(mesh.Triangles[t].Vertices.X) && best.Vertices.Contains(mesh.Triangles[t].Vertices.Y) && best.Vertices.Contains(mesh.Triangles[t].Vertices.Z))
            .ToList();

        if (!(io.KeyCtrl || io.KeyAlt) || _navSelection is null || _navSelection.Mesh != best.Mesh)
            _navSelection = new NavmeshSelection { Mesh = best.Mesh };
        var sel = _navSelection;
        // Triangle mode (CK): the triangles; vertex mode: the vertices; auto: triangles, or vertices when no whole triangle is inside.
        if (io.KeyAlt)
        {
            foreach (var v in best.Vertices) sel.Vertices.Remove(v);
            foreach (var t in triangles) sel.Triangles.Remove(t);
        }
        else if (_navSelectMode == NavSelectMode.Vertices || (_navSelectMode == NavSelectMode.Auto && triangles.Count == 0))
            foreach (var v in best.Vertices) sel.Vertices.Add(v);
        else foreach (var t in triangles) sel.Triangles.Add(t);
        if (sel.IsEmpty) _navSelection = null;
    }

    private void DrawMarquee(ImDrawListPtr drawList)
    {
        if (_marqueeFrom is null) return;
        var (min, max) = MarqueeRect();
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Ui.Theme.Alpha(Ui.Theme.Accent, 0.12f)));
        drawList.AddRect(min, max, ImGui.GetColorU32(Ui.Theme.Accent));
    }

    // ------------------------------------------------------------------ autotest

    private int _marqueeTest;
    private int _marqueeTestFrame;
    private Vector2 _marqueeTestFrom, _marqueeTestTo;
    private Matrix4x4 _marqueeTestView;
    private Dictionary<SceneObject, RefState>? _marqueeTestStates;
    private int _marqueeTestPending;
    private string? _marqueeTestUndo;

    /// <summary>
    /// --autotest-marquee [nav]: Shift+drag from 30 % to 70 % of the view with the fake mouse.
    /// Waits for input consumption, then checks the exact selection, release cleanup and absence of edits.
    /// A failed gesture throws so the process cannot report success with an unfinished rectangle.
    /// </summary>
    public bool MarqueeTestStep()
    {
        var nav = Environment.GetCommandLineArgs().Contains("nav");
        var io = ImGui.GetIO();
        if (++_marqueeTestFrame > 120 && _marqueeTest != 0)
            Fail($"input timed out at stage {_marqueeTest}: mouse {io.MousePos}, down {ImGui.IsMouseDown(ImGuiMouseButton.Left)}, Shift {io.KeyShift}, pressed {_pressed}, object {_pressObject?.Reference}, nav press {_navMarqueePress}, marquee {_marqueeFrom}, drag {_drag}, gizmo {_gizmoWasUsing}/{_gizmoOver}");

        switch (_marqueeTest)
        {
            case 0 when IsSettled && _viewSize.X > 8 && _viewSize.Y > 8:
                _selection.Clear();
                _navSelection = null;
                if (nav) SetNavmeshMode(true); else _tool = EditTool.Select;
                if (nav) _navSelectMode = NavSelectMode.Triangles;
                Ui.ImGuiLayer.TestMouseButton = 0;
                Ui.ImGuiLayer.TestShift = false;
                Ui.ImGuiLayer.TestMouse = (Pixel(_viewOrigin + _viewSize * 0.3f), false);
                Next(1);
                break;
            case 1 when _marqueeTestFrame > 3 && !io.KeyShift && !ImGui.IsMouseDown(ImGuiMouseButton.Left):
            {
                // Moving an unpressed mouse with Shift held is CK camera input, not test setup.
                // Nav mode can also add toolbar rows, so take the rectangle only after layout settles.
                var from = Pixel(_viewOrigin + _viewSize * 0.3f);
                Ui.ImGuiLayer.TestMouse = (from, false);
                if (Vector2.Distance(io.MousePos, from) > 0.5f) break;
                _marqueeTestFrom = from;
                _marqueeTestTo = Pixel(_viewOrigin + _viewSize * 0.7f);
                Ui.ImGuiLayer.TestShift = true;
                Next(2);
                break;
            }
            case 2 when _marqueeTestFrame > 2 && io.KeyShift:
                _marqueeTestView = _camera.View;
                _marqueeTestStates = _scene!.Objects.ToDictionary(o => o, o => o.State);
                _marqueeTestPending = _document?.PendingCount ?? 0;
                _marqueeTestUndo = _document?.History.UndoName;
                Ui.ImGuiLayer.TestMouse = (_marqueeTestFrom, true);
                Next(3);
                break;
            case 3 when (nav ? _navMarqueePress : _pressed && _pressObject is null) && ImGui.IsMouseDown(ImGuiMouseButton.Left):
                Next(4);
                break;
            case 4:
                Ui.ImGuiLayer.TestMouse = (Vector2.Lerp(_marqueeTestFrom, _marqueeTestTo, Math.Min(1f, _marqueeTestFrame / 10f)), true);
                if (_marqueeTestFrame > 12) Next(5);
                break;
            case 5 when _marqueeTestFrame > 2 && _marqueeFrom == _marqueeTestFrom && Vector2.Distance(io.MousePos, _marqueeTestTo) < 0.5f:
                // TestMouse is queued for the next NewFrame. Do not drop it or Shift until both
                // ImGui and the viewport have actually consumed the release.
                Ui.ImGuiLayer.TestMouse = (_marqueeTestTo, false);
                Next(6);
                break;
            case 6 when _marqueeTestFrame > 3 && !ImGui.IsMouseDown(ImGuiMouseButton.Left)
                && _marqueeFrom is null && !_pressed && !_navMarqueePress:
                CheckSelection();
                Ui.ImGuiLayer.TestShift = false;
                Next(7);
                break;
            case 7 when _marqueeTestFrame > 3 && !io.KeyShift:
                CheckSelection();
                Ui.ImGuiLayer.TestMouse = null;
                Log.Info("autotest-marquee: PASS; selection retained after release, input state cleared, no record edits");
                return true;
        }
        return false;

        void Next(int stage) => (_marqueeTest, _marqueeTestFrame) = (stage, 0);

        static Vector2 Pixel(Vector2 point) => new(MathF.Floor(point.X), MathF.Floor(point.Y));

        void Fail(string reason)
        {
            Ui.ImGuiLayer.TestMouse = null;
            Ui.ImGuiLayer.TestShift = false;
            Log.Error($"autotest-marquee: FAIL: {reason}");
            throw new InvalidOperationException($"autotest-marquee: {reason}");
        }

        void CheckSelection()
        {
            if (_marqueeFrom is not null || _pressed || _pressObject is not null || _navPressed || _navMarqueePress || _navShiftPress
                || _navDrag is not null || _drag != DragKind.None || _gizmoWasUsing || ImGui.IsMouseDown(ImGuiMouseButton.Left))
                Fail("the released gesture left active input state");
            if (_camera.View != _marqueeTestView)
                Fail("the selection gesture moved the camera");
            if ((_document?.PendingCount ?? 0) != _marqueeTestPending || _document?.History.UndoName != _marqueeTestUndo
                || _marqueeTestStates!.Any(pair => !pair.Value.Equals(pair.Key.State)))
                Fail("the selection gesture edited a record or created an undo step");

            var rect = (Min: _marqueeTestFrom, Max: _marqueeTestTo);
            var viewProj = _marqueeTestView * _camera.Projection(_viewSize.X / _viewSize.Y);
            if (nav)
            {
                // The consumer selects the mesh with the most enclosed vertices, not every
                // overlapping mesh. Check its identity and triangle indices, not just a count.
                var candidates = _scene!.Navmeshes.Where(v => v.Record.Data is not null)
                    .Select(v => (View: v, Vertices: Enumerable.Range(0, v.Record.Data!.Vertices.Count)
                        .Where(i => ToScreen(P(v.Record.Data.Vertices[i]), viewProj, _viewOrigin, _viewSize) is { } at && Inside(rect, at)).ToHashSet()))
                    .OrderByDescending(v => v.Vertices.Count).ToList();
                if (candidates.Count == 0 || candidates[0].Vertices.Count == 0)
                    Fail("no navmesh vertices inside the test rectangle");
                var best = candidates[0];
                var data = best.View.Record.Data!;
                var expected = Enumerable.Range(0, data.Triangles.Count)
                    .Where(t => best.Vertices.Contains(data.Triangles[t].Vertices.X) && best.Vertices.Contains(data.Triangles[t].Vertices.Y)
                        && best.Vertices.Contains(data.Triangles[t].Vertices.Z)).ToHashSet();
                if (expected.Count == 0 || _navSelection is not { } selected || selected.Mesh != best.View.Key
                    || !selected.Triangles.SetEquals(expected) || selected.Vertices.Count != 0 || selected.Edges.Count != 0
                    || !ReferenceEquals(_renderer!.NavSelection, selected))
                    Fail($"navmesh selection differs: {_navSelection?.Triangles.Count ?? 0} selected, {expected.Count} expected");
                Log.Info($"autotest-marquee: navmesh {best.View.Key}, {expected.Count} triangle(s) selected and inside; marquee cleared True");
            }
            else
            {
                var options = _renderer!.Options;
                var expected = _scene!.Objects.Where(o => !o.IsDeleted && !options.Hides(o) && !options.FrozenReferences.Contains(o.Reference)
                    && (o.Kind != SceneObjectKind.Marker || options.ShowMarkers)
                    && ToScreen(o.BoundsCenter, viewProj, _viewOrigin, _viewSize) is { } at && Inside(rect, at)).ToHashSet();
                if (expected.Count == 0 || !_selection.SetEquals(expected))
                    Fail($"object selection differs: {_selection.Count} selected, {expected.Count} expected; missing {expected.Except(_selection).Count()}, extra {_selection.Except(expected).Count()}");
                Log.Info($"autotest-marquee: {_selection.Count} object(s) selected, {expected.Count} inside; marquee cleared True");
            }
        }
    }
}
