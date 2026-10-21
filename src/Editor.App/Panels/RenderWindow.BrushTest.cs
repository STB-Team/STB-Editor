using System.Numerics;
using StbEditor.App.Services;
using StbEditor.Core;
using StbEditor.Render;

namespace StbEditor.App.Panels;

/// <summary>--autotest-brushes (with an exterior --cell): each new landscape brush once on the land, checked, then undone. Nothing is saved.</summary>
internal sealed partial class RenderWindow
{
    public bool BrushTest(EditorApp app)
    {
        if (!IsSettled || _scene is not { IsInterior: false, Worldspace: { } world } scene || app.Session is not { } session || _document is null) return false;
        var terrain = scene.Terrain.Where(t => t.Data is not null).OrderBy(t => Vector2.Distance(new Vector2(t.Origin.X + 2048, t.Origin.Y + 2048), new Vector2(scene.Focus.X, scene.Focus.Y))).FirstOrDefault();
        if (terrain is null) { Log.Error("autotest-brushes: no land"); return true; }
        var centre = new Vector3(terrain.Origin.X + 2048, terrain.Origin.Y + 2048, TerrainHeight(scene, terrain.Origin.X + 2048, terrain.Origin.Y + 2048) ?? 0);
        var brush = Brush;
        var saved = (brush.Mode, brush.Radius, brush.Strength, brush.Falloff, brush.TargetHeight, brush.MaxOpacity, brush.Texture);
        brush.Radius = 700;
        brush.Falloff = 50;

        LandscapeData Land() => _document.LandDrafts(world).FirstOrDefault(d => d.GridX == terrain.X && d.GridY == terrain.Y)?.Data ?? terrain.Data!;
        // Vertices inside the solid half of the brush (falloff 50 %: up to 350 units from the centre).
        var inner = Enumerable.Range(0, 33 * 33).Where(i =>
        {
            var p = new Vector2(terrain.Origin.X + i % 33 * 128, terrain.Origin.Y + i / 33 * 128);
            return Vector2.Distance(p, new Vector2(centre.X, centre.Y)) < 330;
        }).ToList();

        void Stroke(LandBrushMode mode, int steps = 20, float delta = 0.1f)
        {
            brush.Mode = mode;
            _stroke.Clear();
            _strokeFull = false;
            _flattenHeight = centre.Z;
            for (var i = 0; i < steps; i++) ApplyBrush(session, scene, world, centre, delta, false);
            EndStroke();
            RefreshTerrainNow(app);
        }
        void Undo() { _document.Undo(); RefreshTerrainNow(app); }

        var before = Land().Heights.ToArray();
        brush.TargetHeight = centre.Z + 300;
        brush.Strength = 100;
        Stroke(LandBrushMode.SetHeight);
        var set = inner.Average(i => Land().Heights[i]);
        Undo();
        Log.Info($"autotest-brushes: set height {brush.TargetHeight:0}: inner mean {set:0} (was {inner.Average(i => before[i]):0})");

        brush.TargetHeight = centre.Z + 50;
        brush.Strength = 2000;
        Stroke(LandBrushMode.RaiseTo);
        var raised = inner.Max(i => Land().Heights[i]);
        var raisedAbove = inner.Count(i => before[i] > brush.TargetHeight && Land().Heights[i] != before[i]);
        Undo();
        Log.Info($"autotest-brushes: raise to {brush.TargetHeight:0}: inner max {raised:0}, higher land touched {raisedAbove}");

        brush.TargetHeight = centre.Z - 50;
        Stroke(LandBrushMode.LowerTo);
        var lowered = inner.Min(i => Land().Heights[i]);
        Undo();
        Log.Info($"autotest-brushes: lower to {brush.TargetHeight:0}: inner min {lowered:0}");

        brush.Strength = 400;
        Stroke(LandBrushMode.Noise, 5);
        var noise = inner.Select(i => Land().Heights[i] - before[i]).ToList();
        Undo();
        Log.Info($"autotest-brushes: noise: change {noise.Min():0} … {noise.Max():0}; after undo same as before {Land().Heights.SequenceEqual(before)}");

        // Paint with max opacity 40 %: a texture the quadrant does not use yet.
        var used = terrain.Data!.Quadrants.SelectMany(q => q.Layers.Select(l => l.Texture).Append(q.BaseTexture ?? Mutagen.Bethesda.Plugins.FormKey.Null)).ToHashSet();
        var texture = session.Catalog.BySignature["LTEX"].Select(r => r.FormKey).First(k => !used.Contains(k));
        brush.Texture = texture;
        brush.MaxOpacity = 0.4f;
        brush.Strength = 100;
        Stroke(LandBrushMode.Paint);
        var painted = Land().Quadrants.SelectMany(q => q.Layers).Where(l => l.Texture == texture).SelectMany(l => l.Opacity).DefaultIfEmpty(0).Max();
        Stroke(LandBrushMode.Erase);
        var erased = Land().Quadrants.SelectMany(q => q.Layers).Where(l => l.Texture == texture).SelectMany(l => l.Opacity).DefaultIfEmpty(0).Max();
        Undo();
        Undo();
        Log.Info($"autotest-brushes: paint at max 40 %: highest {painted:0.00}; erase: {erased:0.00}");

        // Grass off: the texture showing most in the middle should end up a twin without grass.
        var twins = GrassTwins(session);
        var middle = inner.FirstOrDefault(i => Land().Weights(i % 33, i / 33).MaxBy(p => p.Value).Key is var k && twins.ContainsKey((k, false)), inner[inner.Count / 2]);
        var (dominant, _) = Land().Weights(middle % 33, middle / 33).MaxBy(p => p.Value);
        brush.MaxOpacity = 1;
        Stroke(LandBrushMode.GrassOff, 40);
        var (after, weight) = Land().Weights(middle % 33, middle / 33).MaxBy(p => p.Value);
        Undo();
        var lookup = new StbEditor.Data.RecordLookup(session.Workspace, session.Catalog);
        int GrassOf(Mutagen.Bethesda.Plugins.FormKey k) => lookup.Resolve<Mutagen.Bethesda.Skyrim.ILandscapeTextureGetter>(k)?.Grasses.Count ?? -1;
        Log.Info($"autotest-brushes: dominant grasses {GrassOf(dominant)}, twin {(twins.TryGetValue((dominant, false), out var tw) ? session.Catalog.Get(tw)?.EditorId + " grasses " + GrassOf(tw) : "-")}, after grasses {GrassOf(after)}");
        Log.Info($"autotest-brushes: grass off at the centre: {session.Catalog.Get(dominant)?.EditorId} -> {session.Catalog.Get(after)?.EditorId} ({weight:0.00}); twin known {twins.ContainsKey((dominant, false))}");

        (brush.Mode, brush.Radius, brush.Strength, brush.Falloff, brush.TargetHeight, brush.MaxOpacity, brush.Texture) = saved;
        Log.Info($"autotest-brushes: unsaved after all undos: {_document.PendingCount}");
        return true;
    }
}
