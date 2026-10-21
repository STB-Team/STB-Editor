using System.Numerics;
using Hexa.NET.ImGui;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using StbEditor.App.Services;
using StbEditor.Core;
using StbEditor.Render;
using static StbEditor.App.Ui.Loc;

namespace StbEditor.App.Panels;

/// <summary>
/// The CK navmesh toolbar as a row of the render window while the navmesh tool is on, with CK's keys: select triangles (T) /
/// vertices (V) / edges (G), view modes (W), create a triangle or quad (A), flood fill (F) and its inverse (I), water (O) and
/// preferred pathing (P) flags, drop vertices, edge rotate, merge (Q), delete (R), deselect (D), find triangle (Ctrl+F), finalize
/// (Ctrl+F1), the generation settings (Ctrl+F3) and a pathing test.
/// </summary>
internal sealed partial class RenderWindow
{
    private bool _navFind;
    private int _navFindIndex;

    /// <summary>Pathing test (CK: Test navmesh): first click the start, second the goal; the path is drawn until the next test.</summary>
    private bool _navPathTest;
    private (FormKey Mesh, int Triangle, Vector3 Point)? _navPathStart;
    private List<Vector3>? _navPath;

    private void DrawNavmeshToolbar(EditorApp app)
    {
        var options = _renderer!.Options;
        var first = true;
        Mode(T("Triangles") + "###navT", NavSelectMode.Triangles, T("T — select triangles"));
        Mode(T("Vertices") + "###navV", NavSelectMode.Vertices, T("V — select vertices"));
        Mode(T("Edges") + "###navG", NavSelectMode.Edges, T("G — select edges (for Edge rotate)"));
        Mode(T("Auto") + "###navAuto", NavSelectMode.Auto, T("A vertex near the cursor, else the triangle under it"));
        var viewLabel = options.NavmeshView switch { NavmeshViewMode.OnTop => T("View: on top"), NavmeshViewMode.Only => T("View: navmesh only"), _ => T("View: standard") };
        Act(viewLabel + "###navView", CycleNavmeshView, T("W — view mode: standard, on top of everything, navmesh only"));
        Act(T("Triangle / quad") + "###navA", CreateNavmeshTriangleOrQuad, T("A — a triangle from 3 selected vertices, a quad (two triangles) from 4"));
        Act(T("Flood fill") + "###navF", () => FloodFillNavmesh(false), T("F — every triangle connected to the selection"));
        Act(T("Inverse fill") + "###navI", () => FloodFillNavmesh(true), T("I — every triangle of the navmesh not connected to the selection"));
        Act(T("Water") + "###navO", () => ToggleNavmeshFlag(NavmeshTriangle.Flag.Water, T("Toggle navmesh water")), T("O — toggle the water flag of the selected triangles"));
        Act(T("Preferred") + "###navP", () => ToggleNavmeshFlag(NavmeshTriangle.Flag.Preferred, T("Toggle preferred pathing")), T("P — toggle preferred pathing of the selected triangles"));
        Act(T("Drop") + "###navDrop", DropNavmeshVertices, T("Drop the selected vertices to the ground below"));
        Act(T("Edge rotate") + "###navRot", RotateNavmeshEdge, T("Turn the selected edge between its two triangles to the other diagonal"));
        Act(T("Merge") + "###navQ", MergeNavmeshVertices, T("Q — the selected vertices become one"));
        Act(T("Delete") + "###navR", DeleteNavmeshSelection, T("R / Del — delete the selection (vertices take their triangles)"));
        Act(T("Deselect") + "###navD", () => _navSelection = null, T("D / Esc — select nothing"));
        Act(T("Find") + "###navFind", () => _navFind = true, T("Ctrl+F — find a triangle by its index"));
        var testing = _navPathTest;
        Place(T("Test path") + "###navPath");
        if (testing) ImGui.PushStyleColor(ImGuiCol.Button, Ui.Theme.ButtonActive);
        if (ImGui.Button(T("Test path") + "###navPath")) { _navPathTest = !testing; _navPathStart = null; _navPath = null; }
        if (testing) ImGui.PopStyleColor();
        Tip(T("Test navmesh (CK): click the start, then the goal; the shortest way over the loaded navmeshes is drawn"));
        Act(T("Finalize") + "###navFin", () => Log.Info(FinalizeNavmeshes(app, true) ?? T("Nothing to finalize")), T("Ctrl+F1 — finalize the edited navmeshes (doors, links to neighbours, NAVI)"));
        DrawNavmeshFind();

        // Buttons follow each other and wrap to a new row when the next one would not fit.
        void Place(string label)
        {
            var width = ImGui.CalcTextSize(label.Split("###")[0]).X + ImGui.GetStyle().FramePadding.X * 2;
            if (!first)
            {
                ImGui.SameLine();
                if (ImGui.GetContentRegionAvail().X < width) ImGui.NewLine();
            }
            first = false;
        }

        void Mode(string label, NavSelectMode mode, string tip)
        {
            Place(label);
            var on = _navSelectMode == mode;
            if (on) ImGui.PushStyleColor(ImGuiCol.Button, Ui.Theme.ButtonActive);
            if (ImGui.Button(label)) _navSelectMode = mode;
            if (on) ImGui.PopStyleColor();
            Tip(tip);
        }

        void Act(string label, Action action, string tip)
        {
            Place(label);
            if (ImGui.Button(label)) action();
            Tip(tip);
        }

        static void Tip(string text)
        {
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(text);
        }
    }

    private void DrawNavmeshFind()
    {
        if (_navFind) { ImGui.OpenPopup("##navfind"); _navFind = false; }
        if (!ImGui.BeginPopup("##navfind")) return;
        ImGui.TextUnformatted(T("Triangle index"));
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 6);
        if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
        ImGui.InputInt("##navfindindex", ref _navFindIndex);
        ImGui.SameLine();
        var mesh = _navSelection?.Mesh ?? _scene?.Navmeshes.FirstOrDefault()?.Key;
        if ((ImGui.Button(T("Go")) || ImGui.IsKeyPressed(ImGuiKey.Enter)) && mesh is { } m)
        {
            if (!SelectNavmeshIndex(m, _navFindIndex, true)) Log.Warn(TF($"navmesh: no triangle {_navFindIndex}"));
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void CycleNavmeshView()
    {
        if (_renderer is null) return;
        var options = _renderer.Options;
        options.NavmeshView = (NavmeshViewMode)(((int)options.NavmeshView + 1) % 3);
        options.ShowNavmesh = true;
    }

    /// <summary>CK's navmesh keys, while the navmesh tool is on.</summary>
    private void HandleNavmeshKeys(EditorApp app)
    {
        var io = ImGui.GetIO();
        bool Key(ImGuiKey key) => ImGui.IsKeyPressed(key, false);
        if (io.KeyCtrl)
        {
            if (Key(ImGuiKey.F)) _navFind = true;
            if (Key(ImGuiKey.F1)) Log.Info(FinalizeNavmeshes(app, true) ?? T("Nothing to finalize"));
            return;
        }
        if (io.KeyShift || io.KeyAlt) return;
        if (Key(ImGuiKey.T)) _navSelectMode = NavSelectMode.Triangles;
        if (Key(ImGuiKey.V)) _navSelectMode = NavSelectMode.Vertices;
        if (Key(ImGuiKey.G)) _navSelectMode = NavSelectMode.Edges;
        if (Key(ImGuiKey.W)) CycleNavmeshView();
        if (Key(ImGuiKey.A)) CreateNavmeshTriangleOrQuad();
        if (Key(ImGuiKey.F)) FloodFillNavmesh(false);
        if (Key(ImGuiKey.I)) FloodFillNavmesh(true);
        if (Key(ImGuiKey.O)) ToggleNavmeshFlag(NavmeshTriangle.Flag.Water, T("Toggle navmesh water"));
        if (Key(ImGuiKey.P)) ToggleNavmeshFlag(NavmeshTriangle.Flag.Preferred, T("Toggle preferred pathing"));
        if (Key(ImGuiKey.Q)) MergeNavmeshVertices();
        if (Key(ImGuiKey.R) || Key(ImGuiKey.Delete)) DeleteNavmeshSelection();
        if (Key(ImGuiKey.D) || Key(ImGuiKey.Escape)) { _navSelection = null; _navPathStart = null; }
    }

    // ------------------------------------------------------------------ operations

    /// <summary>Triangles of the selection, counting the triangles of selected vertices and edges.</summary>
    private static HashSet<int> SelectedTriangles(NavmeshSelection selection, INavigationMeshGetter mesh)
    {
        var result = new HashSet<int>(selection.Triangles);
        var data = mesh.Data!;
        for (var t = 0; t < data.Triangles.Count; t++)
        {
            var v = data.Triangles[t].Vertices;
            if (selection.Vertices.Contains(v.X) || selection.Vertices.Contains(v.Y) || selection.Vertices.Contains(v.Z)) result.Add(t);
            foreach (var (a, b) in selection.Edges)
                if ((v.X == a || v.Y == a || v.Z == a) && (v.X == b || v.Y == b || v.Z == b)) result.Add(t);
        }
        return result;
    }

    /// <summary>Neighbours inside the same navmesh through its edge links (external links lead to other navmeshes).</summary>
    private static IEnumerable<int> Neighbours(INavmeshTriangleGetter tri)
    {
        if (tri.EdgeLink_0_1 >= 0 && !tri.Flags.HasFlag(NavmeshTriangle.Flag.EdgeLink_0_1)) yield return tri.EdgeLink_0_1;
        if (tri.EdgeLink_1_2 >= 0 && !tri.Flags.HasFlag(NavmeshTriangle.Flag.EdgeLink_1_2)) yield return tri.EdgeLink_1_2;
        if (tri.EdgeLink_2_0 >= 0 && !tri.Flags.HasFlag(NavmeshTriangle.Flag.EdgeLink_2_0)) yield return tri.EdgeLink_2_0;
    }

    /// <summary>CK Flood fill: every triangle connected to the selection; inverse: every one that is not.</summary>
    public void FloodFillNavmesh(bool inverse)
    {
        if (_navSelection is not { IsEmpty: false } selection || View(selection.Mesh) is not { } view || view.Record.Data is not { } data) return;
        var reached = SelectedTriangles(selection, view.Record);
        var queue = new Queue<int>(reached);
        while (queue.TryDequeue(out var t))
            foreach (var n in Neighbours(data.Triangles[t]))
                if (n < data.Triangles.Count && reached.Add(n)) queue.Enqueue(n);
        var result = new NavmeshSelection { Mesh = selection.Mesh };
        foreach (var t in inverse ? Enumerable.Range(0, data.Triangles.Count).Where(t => !reached.Contains(t)) : reached) result.Triangles.Add(t);
        _navSelection = result.IsEmpty ? null : result;
        Log.Info(inverse ? TF($"navmesh: {result.Triangles.Count} triangle(s) not connected to the selection") : TF($"navmesh: {result.Triangles.Count} connected triangle(s)"));
    }

    /// <summary>Water / preferred on the selected triangles: on for all unless all have it already, then off.</summary>
    public void ToggleNavmeshFlag(NavmeshTriangle.Flag flag, string name)
    {
        if (_navSelection is not { IsEmpty: false } selection || View(selection.Mesh) is not { } view) return;
        var triangles = SelectedTriangles(selection, view.Record);
        if (triangles.Count == 0) return;
        var data = view.Record.Data!;
        var on = !triangles.All(t => data.Triangles[t].Flags.HasFlag(flag));
        EditNavmesh(name, (mesh, _) =>
        {
            foreach (var t in triangles)
            {
                var tri = mesh.Data!.Triangles[t];
                tri.Flags = on ? tri.Flags | flag : tri.Flags & ~flag;
            }
        });
    }

    /// <summary>3 selected vertices make a triangle; 4 make a quad (two triangles over the shorter diagonal), in order around their centre.</summary>
    public void CreateNavmeshTriangleOrQuad()
    {
        if (_navSelection is not { } selection) return;
        if (selection.Vertices.Count == 3) { CreateNavmeshTriangle(); return; }
        if (selection.Vertices.Count != 4 || View(selection.Mesh)?.Record.Data is not { } data)
        {
            Log.Warn(T("navmesh: select 3 vertices for a triangle or 4 for a quad"));
            return;
        }
        var corners = selection.Vertices.ToArray();
        var centre = corners.Aggregate(Vector3.Zero, (sum, i) => sum + P(data.Vertices[i])) / 4;
        var ordered = corners.OrderBy(i => MathF.Atan2(data.Vertices[i].Y - centre.Y, data.Vertices[i].X - centre.X)).ToArray();
        var d02 = Vector3.Distance(P(data.Vertices[ordered[0]]), P(data.Vertices[ordered[2]]));
        var d13 = Vector3.Distance(P(data.Vertices[ordered[1]]), P(data.Vertices[ordered[3]]));
        EditNavmesh(T("Create navmesh quad"), (mesh, _) =>
        {
            if (d02 <= d13)
            {
                AddTriangle(mesh, ordered[0], ordered[1], ordered[2]);
                AddTriangle(mesh, ordered[0], ordered[2], ordered[3]);
            }
            else
            {
                AddTriangle(mesh, ordered[1], ordered[2], ordered[3]);
                AddTriangle(mesh, ordered[1], ordered[3], ordered[0]);
            }
        });
    }

    /// <summary>CK Edge rotate: the two triangles on the selected edge (a, b) with far corners c and d become (c, d, a) and (d, c, b).</summary>
    public void RotateNavmeshEdge()
    {
        if (_navSelection is not { Edges.Count: 1 } selection || View(selection.Mesh)?.Record.Data is not { } data)
        {
            Log.Warn(T("navmesh: select one edge (G) shared by two triangles"));
            return;
        }
        var (a, b) = selection.Edges.First();
        var sharing = Enumerable.Range(0, data.Triangles.Count).Where(t =>
        {
            var v = data.Triangles[t].Vertices;
            return (v.X == a || v.Y == a || v.Z == a) && (v.X == b || v.Y == b || v.Z == b);
        }).ToList();
        if (sharing.Count != 2)
        {
            Log.Warn(T("navmesh: the edge is on the border — it needs a triangle on each side"));
            return;
        }
        int Far(int t) { var v = data.Triangles[t].Vertices; return new[] { (int)v.X, v.Y, v.Z }.First(i => i != a && i != b); }
        var (c, d) = (Far(sharing[0]), Far(sharing[1]));
        EditNavmesh(T("Rotate navmesh edge"), (mesh, _) =>
        {
            void Set(int t, int x, int y, int z)
            {
                var px = P(mesh.Data!.Vertices[x]);
                if (Vector3.Cross(P(mesh.Data.Vertices[y]) - px, P(mesh.Data.Vertices[z]) - px).Z < 0) (y, z) = (z, y);
                mesh.Data.Triangles[t].Vertices = new P3Int16((short)x, (short)y, (short)z);
            }
            Set(sharing[0], c, d, a);
            Set(sharing[1], d, c, b);
        });
        _navSelection = new NavmeshSelection { Mesh = selection.Mesh };
        _navSelection.Edges.Add((Math.Min(c, d), Math.Max(c, d)));
    }

    // ------------------------------------------------------------------ pathing test

    /// <summary>A click while testing: start, then goal and the path between them (A* over triangle centres, across navmeshes).</summary>
    private bool PathTestClick(Vector2 mouse, Vector2 size, Vector3 origin, Vector3 direction)
    {
        if (!_navPathTest) return false;
        if (PickNavmesh(mouse, size, origin, direction, NavSelectMode.Triangles) is not { } hit) return true;
        if (_navPathStart is not { } start)
        {
            _navPathStart = (hit.Mesh, hit.Triangle, hit.Point);
            _navPath = null;
            return true;
        }
        _navPath = FindPath(start, (hit.Mesh, hit.Triangle, hit.Point));
        Log.Info(_navPath is null ? T("navmesh test: no way between the two points") : TF($"navmesh test: a way of {_navPath.Count - 1} step(s), {PathLength(_navPath):0} units"));
        _navPathStart = null;
        return true;
    }

    private static float PathLength(List<Vector3> path) => path.Zip(path.Skip(1)).Sum(p => Vector3.Distance(p.First, p.Second));

    private List<Vector3>? FindPath((FormKey Mesh, int Triangle, Vector3 Point) from, (FormKey Mesh, int Triangle, Vector3 Point) to)
    {
        if (_scene is null) return null;
        var meshes = _scene.Navmeshes.Where(v => v.Record.Data is not null).ToDictionary(v => v.Key, v => v.Record.Data!);
        Vector3 Centre((FormKey Mesh, int Triangle) n)
        {
            var d = meshes[n.Mesh];
            var v = d.Triangles[n.Triangle].Vertices;
            return (P(d.Vertices[v.X]) + P(d.Vertices[v.Y]) + P(d.Vertices[v.Z])) / 3;
        }
        IEnumerable<(FormKey, int)> Next((FormKey Mesh, int Triangle) n)
        {
            var d = meshes[n.Mesh];
            var tri = d.Triangles[n.Triangle];
            foreach (var t in Neighbours(tri)) if (t < d.Triangles.Count) yield return (n.Mesh, t);
            foreach (var (link, flag) in new[] { (tri.EdgeLink_0_1, NavmeshTriangle.Flag.EdgeLink_0_1), (tri.EdgeLink_1_2, NavmeshTriangle.Flag.EdgeLink_1_2), (tri.EdgeLink_2_0, NavmeshTriangle.Flag.EdgeLink_2_0) })
            {
                if (link < 0 || !tri.Flags.HasFlag(flag) || link >= d.EdgeLinks.Count) continue;
                var target = d.EdgeLinks[link];
                if (meshes.TryGetValue(target.Mesh.FormKey, out var other) && target.TriangleIndex >= 0 && target.TriangleIndex < other.Triangles.Count)
                    yield return (target.Mesh.FormKey, target.TriangleIndex);
            }
        }

        var start = (from.Mesh, from.Triangle);
        var goal = (to.Mesh, to.Triangle);
        var cost = new Dictionary<(FormKey, int), float> { [start] = 0 };
        var cameFrom = new Dictionary<(FormKey, int), (FormKey, int)>();
        var open = new PriorityQueue<(FormKey, int), float>();
        open.Enqueue(start, 0);
        var goalCentre = Centre(goal);
        var visited = 0;
        while (open.TryDequeue(out var node, out _))
        {
            if (node == goal)
            {
                var path = new List<Vector3> { to.Point };
                for (var n = node; cameFrom.TryGetValue(n, out var prev); n = prev) path.Add(Centre(prev));
                path[^1] = from.Point;
                path.Reverse();
                return path;
            }
            if (++visited > 200_000) break;
            var here = Centre(node);
            foreach (var next in Next(node))
            {
                var c = cost[node] + Vector3.Distance(here, Centre(next));
                if (cost.TryGetValue(next, out var known) && known <= c) continue;
                cost[next] = c;
                cameFrom[next] = node;
                open.Enqueue(next, c + Vector3.Distance(Centre(next), goalCentre));
            }
        }
        return null;
    }

    /// <summary>The test path (green) and its start point, into the overlay lines.</summary>
    private void NavPathOverlay()
    {
        if (_renderer is null || _tool != EditTool.Navmesh) return;
        var lines = _renderer.OverlayLines;
        const uint colour = 0xFF40FF40;
        var lift = new Vector3(0, 0, 12);
        if (_navPath is { } path)
            for (var i = 0; i + 1 < path.Count; i++)
            {
                lines.Add(new LineVertex(path[i] + lift, colour));
                lines.Add(new LineVertex(path[i + 1] + lift, colour));
            }
        if (_navPathStart is { } start)
        {
            lines.Add(new LineVertex(start.Point, colour));
            lines.Add(new LineVertex(start.Point + new Vector3(0, 0, 96), colour));
        }
    }

    // ------------------------------------------------------------------ autotest

    /// <summary>
    /// --autotest-navtools (with --cell): flood fill + inverse cover the biggest navmesh, water toggles and undoes, an inner edge
    /// rotates without new Check NavMeshes problems and undoes, a path runs from triangle 0 to the farthest connected one.
    /// Nothing is saved.
    /// </summary>
    public bool NavToolsTest(EditorApp app)
    {
        if (!IsSettled || _scene is null || _document is null) return false;
        var view = _scene.Navmeshes.Where(v => v.Record.Data is not null).MaxBy(v => v.Record.Data!.Triangles.Count);
        if (view is null) { Log.Error("autotest-navtools: no navmesh"); return true; }
        var data = view.Record.Data!;
        var total = data.Triangles.Count;
        _navSelection = new NavmeshSelection { Mesh = view.Key };
        _navSelection.Triangles.Add(0);
        FloodFillNavmesh(false);
        var connected = _navSelection?.Triangles.ToHashSet() ?? [];
        _navSelection = new NavmeshSelection { Mesh = view.Key };
        _navSelection.Triangles.Add(0);
        FloodFillNavmesh(true);
        var rest = _navSelection?.Triangles.Count ?? 0;
        Log.Info($"autotest-navtools: {view.Key} {total} triangles: flood {connected.Count} + inverse {rest} = {connected.Count + rest}");

        _navSelection = new NavmeshSelection { Mesh = view.Key };
        _navSelection.Triangles.Add(0);
        var before = data.Triangles[0].Flags.HasFlag(NavmeshTriangle.Flag.Water);
        ToggleNavmeshFlag(NavmeshTriangle.Flag.Water, "test water");
        RefreshNavmeshes(app);
        var toggled = View(view.Key)!.Record.Data!.Triangles[0].Flags.HasFlag(NavmeshTriangle.Flag.Water);
        _document.Undo();
        RefreshNavmeshes(app);
        var undone = View(view.Key)!.Record.Data!.Triangles[0].Flags.HasFlag(NavmeshTriangle.Flag.Water);
        Log.Info($"autotest-navtools: water {before} -> {toggled}, after undo {undone}");

        var issuesBefore = NavmeshPanel.CheckAll(_scene).Count(i => i.Mesh == view.Key);
        var current = View(view.Key)!.Record.Data!;
        (int, int)? inner = null;
        for (var t = 0; t < current.Triangles.Count && inner is null; t++)
            foreach (var n in Neighbours(current.Triangles[t]))
            {
                var a = current.Triangles[t].Vertices;
                var b = current.Triangles[n].Vertices;
                var shared = new[] { (int)a.X, a.Y, a.Z }.Intersect(new[] { (int)b.X, b.Y, b.Z }).ToArray();
                if (shared.Length != 2) continue;
                // Only a convex pair can turn: the new diagonal must cross the old one.
                int Far(P3Int16 v) => new[] { (int)v.X, v.Y, v.Z }.First(i => !shared.Contains(i));
                var (c, d) = (P(current.Vertices[Far(a)]), P(current.Vertices[Far(b)]));
                var (p, q) = (P(current.Vertices[shared[0]]), P(current.Vertices[shared[1]]));
                static float Side(Vector3 o, Vector3 x, Vector3 y) => (x.X - o.X) * (y.Y - o.Y) - (x.Y - o.Y) * (y.X - o.X);
                if (Side(c, d, p) * Side(c, d, q) < 0 && Side(p, q, c) * Side(p, q, d) < 0) { inner = (Math.Min(shared[0], shared[1]), Math.Max(shared[0], shared[1])); break; }
            }
        if (inner is { } edge)
        {
            _navSelection = new NavmeshSelection { Mesh = view.Key };
            _navSelection.Edges.Add(edge);
            var trianglesBefore = current.Triangles.Select(t => t.Vertices).ToList();
            RotateNavmeshEdge();
            RefreshNavmeshes(app);
            var changed = View(view.Key)!.Record.Data!.Triangles.Select(t => t.Vertices).Where((v, i) => !v.Equals(trianglesBefore[i])).Count();
            var issuesAfter = NavmeshPanel.CheckAll(_scene).Count(i => i.Mesh == view.Key);
            var rotated = View(view.Key)!.Record.Data!.Triangles.Count;
            _document.Undo();
            RefreshNavmeshes(app);
            Log.Info($"autotest-navtools: edge {edge} rotated: {changed} triangle(s) changed, triangles {rotated} (was {total}), Check NavMeshes {issuesBefore} -> {issuesAfter}; undone {View(view.Key)!.Record.Data!.Triangles.Count == total}");
        }
        else Log.Warn("autotest-navtools: no convex inner edge to rotate");

        var far = connected.MaxBy(t =>
        {
            var v = current.Triangles[t].Vertices;
            var v0 = current.Triangles[0].Vertices;
            return Vector3.Distance(P(current.Vertices[v.X]), P(current.Vertices[v0.X]));
        });
        Vector3 Mid(int t) { var v = current.Triangles[t].Vertices; return (P(current.Vertices[v.X]) + P(current.Vertices[v.Y]) + P(current.Vertices[v.Z])) / 3; }
        var path = FindPath((view.Key, 0, Mid(0)), (view.Key, far, Mid(far)));
        Log.Info(path is null ? "autotest-navtools: no path" : $"autotest-navtools: path 0 -> {far}: {path.Count - 1} steps, {PathLength(path):0} units (straight {Vector3.Distance(Mid(0), Mid(far)):0})");
        _navSelection = null;
        return true;
    }
}
