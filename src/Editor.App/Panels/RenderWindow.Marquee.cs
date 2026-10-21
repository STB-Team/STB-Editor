using System.Numerics;
using Hexa.NET.ImGui;
using StbEditor.App.Services;
using StbEditor.Render;

namespace StbEditor.App.Panels;

/// <summary>
/// Rubber-band selection (CK): a left drag that starts on empty space draws a rectangle; on release what is inside is selected —
/// objects by their centre with the object tools, navmesh vertices (and triangles with all three corners inside) with the navmesh
/// tool. Ctrl adds to the selection, Alt takes away.
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

    /// <summary>
    /// --autotest-marquee [nav]: Shift+drag from 30 % to 70 % of the view with the fake mouse; logs what got selected against
    /// what lies inside by the same projection. Returns true when done.
    /// </summary>
    public bool MarqueeTestStep()
    {
        var nav = Environment.GetCommandLineArgs().Contains("nav");
        var from = _viewOrigin + _viewSize * 0.3f;
        var to = _viewOrigin + _viewSize * 0.7f;
        _marqueeTestFrame++;
        switch (_marqueeTest)
        {
            case 0 when IsSettled:
                _selection.Clear();
                _navSelection = null;
                if (nav) SetNavmeshMode(true); else _tool = EditTool.Select;
                if (nav) _navSelectMode = NavSelectMode.Triangles;
                Ui.ImGuiLayer.TestShift = true;
                Ui.ImGuiLayer.TestMouse = (from, false);
                (_marqueeTest, _marqueeTestFrame) = (1, 0);
                break;
            case 1 when _marqueeTestFrame > 3:
                Ui.ImGuiLayer.TestMouse = (from, true);
                (_marqueeTest, _marqueeTestFrame) = (2, 0);
                break;
            case 2:
                Ui.ImGuiLayer.TestMouse = (Vector2.Lerp(from, to, Math.Min(1f, _marqueeTestFrame / 10f)), true);
                if (_marqueeTestFrame > 12) (_marqueeTest, _marqueeTestFrame) = (3, 0);
                break;
            case 3:
                Ui.ImGuiLayer.TestMouse = (to, false);
                (_marqueeTest, _marqueeTestFrame) = (4, 0);
                break;
            case 4 when _marqueeTestFrame > 2:
            {
                Ui.ImGuiLayer.TestMouse = null;
                Ui.ImGuiLayer.TestShift = false;
                var rect = (Min: from, Max: to);
                var viewProj = _camera.View * _camera.Projection(_viewSize.X / _viewSize.Y);
                if (nav)
                {
                    var expected = _scene!.Navmeshes.Sum(v => v.Record.Data is { } d ? Enumerable.Range(0, d.Triangles.Count).Count(t =>
                        new[] { d.Triangles[t].Vertices.X, d.Triangles[t].Vertices.Y, d.Triangles[t].Vertices.Z }
                            .All(i => ToScreen(P(d.Vertices[i]), viewProj, _viewOrigin, _viewSize) is { } at && Inside(rect, at))) : 0);
                    Log.Info($"autotest-marquee: navmesh {_navSelection?.Triangles.Count ?? 0} triangle(s), {_navSelection?.Vertices.Count ?? 0} vertices selected; {expected} triangle(s) inside over all navmeshes");
                }
                else
                {
                    var options = _renderer!.Options;
                    var expected = _scene!.Objects.Count(o => !o.IsDeleted && !options.Hides(o) && !options.FrozenReferences.Contains(o.Reference)
                        && (o.Kind != SceneObjectKind.Marker || options.ShowMarkers)
                        && ToScreen(o.BoundsCenter, viewProj, _viewOrigin, _viewSize) is { } at && Inside(rect, at));
                    Log.Info($"autotest-marquee: {_selection.Count} object(s) selected, {expected} inside; marquee cleared {_marqueeFrom is null}");
                    foreach (var o in _scene.Objects.Where(o => !_selection.Contains(o) && !o.IsDeleted && !options.Hides(o) && !options.FrozenReferences.Contains(o.Reference)
                        && (o.Kind != SceneObjectKind.Marker || options.ShowMarkers)
                        && ToScreen(o.BoundsCenter, viewProj, _viewOrigin, _viewSize) is { } at2 && Inside(rect, at2)))
                        Log.Info($"autotest-marquee: inside but not selected: {o.BaseEditorId} {o.Kind} {o.Reference}");
                }
                return true;
            }
        }
        return false;
    }
}
