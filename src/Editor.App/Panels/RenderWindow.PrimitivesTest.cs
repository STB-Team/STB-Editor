using Mutagen.Bethesda.Skyrim;
using StbEditor.App.Services;
using StbEditor.Core;
using StbEditor.Render;

namespace StbEditor.App.Panels;

/// <summary>
/// --autotest-primitives &lt;folder&gt; (with an interior --cell): a new active file in the folder (never saved), every kind of the
/// Create menu once, the XPRM read back from the edited record, a portal linked to two rooms and the rooms joined, then all undone.
/// </summary>
internal sealed partial class RenderWindow
{
    public bool PrimitivesTest(EditorApp app)
    {
        if (!IsSettled || _scene is null || app.Session is not { } session || _document is null) return false;
        var cli = Environment.GetCommandLineArgs();
        var folder = cli.SkipWhile(a => a != "--autotest-primitives").Skip(1).FirstOrDefault() ?? Path.GetTempPath();
        Directory.CreateDirectory(folder);
        app.SetActivePlugin(StbEditor.Core.ActivePlugin.ForNew(Path.Combine(folder, "STBEditorPrimitives.esp"), true, false, session.Game, session.Workspace.DefaultEncoding));
        _selection.Clear();
        var trigger = session.Catalog.BySignature["ACTI"].First(r => r.EditorId == "defaultActivateSelfTRIG").FormKey;
        var acoustic = session.Catalog.BySignature["ASPC"].First().FormKey;
        var made = new List<(PrimitiveKind Kind, SceneObject Object)>();
        foreach (var kind in Enum.GetValues<PrimitiveKind>().Where(k => k != PrimitiveKind.SoundEmitter))
        {
            _selection.Clear();
            var chosen = kind switch { PrimitiveKind.Trigger => trigger, PrimitiveKind.AcousticSpace => acoustic, _ => (Mutagen.Bethesda.Plugins.FormKey?)null };
            if (CreatePrimitive(app, kind, chosen) is { } obj) made.Add((kind, obj));
            else Log.Error($"autotest-primitives: {kind} not created");
        }
        foreach (var (kind, obj) in made)
        {
            var record = _document.Override(obj.Reference)?.Record as IPlacedObjectGetter;
            var p = record?.Primitive;
            Log.Info($"autotest-primitives: {kind}: {obj.BaseEditorId} {p?.Type} bounds {p?.Bounds} colour {p?.Color.R},{p?.Color.G},{p?.Color.B} multibound {record?.IsMultiBoundPrimitive} flags 0x{(uint)(record?.MajorRecordFlagsRaw ?? 0):X}; scene half {obj.Primitive?.Bounds}");
        }

        // A portal between two rooms, the rooms joined.
        var rooms = made.Where(m => m.Kind == PrimitiveKind.RoomBound).Select(m => m.Object).ToList();
        _selection.Clear();
        if (CreatePrimitive(app, PrimitiveKind.RoomBound, null) is { } second) rooms.Add(second);
        var portal = made.First(m => m.Kind == PrimitiveKind.Portal).Object;
        _selection.Clear();
        _selection.Add(portal);
        foreach (var r in rooms) _selection.Add(r);
        LinkPortal(app, link: true);
        var portals = (_document.Override(portal.Reference)?.Record as IPlacedObjectGetter)?.Portals;
        Log.Info($"autotest-primitives: portal links {portals?.Count}: {portals?.FirstOrDefault()?.Origin.FormKey} -> {portals?.FirstOrDefault()?.Destination.FormKey}; rooms {string.Join(", ", rooms.Select(r => r.Reference))}");
        _selection.Clear();
        foreach (var r in rooms) _selection.Add(r);
        JoinRooms(app);
        Log.Info($"autotest-primitives: joined: {string.Join("; ", rooms.Select(r => (_document.Override(r.Reference)?.Record as IPlacedObjectGetter)?.LinkedRooms.Count ?? 0))} linked room(s)");

        while (_document.History.CanUndo) _document.Undo();
        Log.Info($"autotest-primitives: after undo {_document.PendingCount} unsaved");
        app.SetActivePlugin(null);
        return true;
    }
}
