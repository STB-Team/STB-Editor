using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using StbEditor.App.Services;
using StbEditor.Render;
using static StbEditor.App.Ui.Loc;

namespace StbEditor.App.Panels;

/// <summary>
/// The CK main toolbar's level-design buttons as the render window's «Create» menu: triggers, multibounds, occlusion planes and
/// boxes, roombounds and portals (with Link Portal to Room / Detach / Join Rooms), collision planes, boxes and spheres, acoustic
/// spaces and sound emitters. Bases, colours and the XPRM «unknown» value are what Skyrim.esm's own references use
/// (`stbedit-cli prims`); a new primitive encloses the selection, else sits where the view's centre points.
/// </summary>
internal sealed partial class RenderWindow
{
    internal enum PrimitiveKind { Trigger, MultiBoundBox, MultiBoundSphere, OcclusionPlane, OcclusionBox, RoomBound, Portal, CollisionPlane, CollisionBox, CollisionSphere, AcousticSpace, SoundEmitter }

    private static readonly FormKey RoomMarker = FormKey.Factory("00001F:Skyrim.esm");
    private static readonly FormKey PortalMarker = FormKey.Factory("000020:Skyrim.esm");
    private static readonly FormKey CollisionMarker = FormKey.Factory("000021:Skyrim.esm");
    private static readonly FormKey PlaneMarker = FormKey.Factory("000017:Skyrim.esm");

    private string _createFilter = "";
    private PrimitiveKind _createPicking;

    private void DrawCreateMenu(EditorApp app)
    {
        ImGui.SameLine();
        if (ImGui.Button(T("Create") + " v###createprim")) ImGui.OpenPopup("##createprim");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("CK main toolbar: triggers, multibounds, occlusion, rooms and portals, collision, acoustic spaces, sound emitters"));
        if (!ImGui.BeginPopup("##createprim")) return;
        if (app.ActivePlugin is null) ImGui.TextColored(Ui.Theme.Warning, T("Choose the active file first (File → Active file)."));
        ImGui.BeginDisabled(app.ActivePlugin is null || _scene is null);
        if (ImGui.BeginMenu(T("Trigger"))) { DrawBasePicker(app, PrimitiveKind.Trigger, "ACTI"); ImGui.EndMenu(); }
        Item(T("Multibound box"), PrimitiveKind.MultiBoundBox);
        Item(T("Multibound sphere"), PrimitiveKind.MultiBoundSphere);
        ImGui.Separator();
        Item(T("Occlusion plane"), PrimitiveKind.OcclusionPlane);
        Item(T("Occlusion box"), PrimitiveKind.OcclusionBox);
        ImGui.Separator();
        Item(T("Roombound"), PrimitiveKind.RoomBound);
        Item(T("Portal"), PrimitiveKind.Portal);
        if (ImGui.MenuItem(T("Link portal to room"))) LinkPortal(app, link: true);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Select a portal and one or two roombounds: the portal joins them"));
        if (ImGui.MenuItem(T("Detach portal from room"))) LinkPortal(app, link: false);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Select a portal and a roombound: the portal no longer joins that room"));
        if (ImGui.MenuItem(T("Join rooms"))) JoinRooms(app);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Select two or more roombounds: each lists the others as linked rooms"));
        ImGui.Separator();
        Item(T("Collision plane"), PrimitiveKind.CollisionPlane);
        Item(T("Collision box"), PrimitiveKind.CollisionBox);
        Item(T("Collision sphere"), PrimitiveKind.CollisionSphere);
        ImGui.Separator();
        if (ImGui.BeginMenu(T("Acoustic space"))) { DrawBasePicker(app, PrimitiveKind.AcousticSpace, "ASPC"); ImGui.EndMenu(); }
        if (ImGui.BeginMenu(T("Sound emitter"))) { DrawBasePicker(app, PrimitiveKind.SoundEmitter, "SOUN"); ImGui.EndMenu(); }
        ImGui.EndDisabled();
        ImGui.EndPopup();

        void Item(string label, PrimitiveKind kind)
        {
            if (ImGui.MenuItem(label)) CreatePrimitive(app, kind, null);
        }
    }

    /// <summary>The base to place: filter + list (triggers first for ACTI).</summary>
    private void DrawBasePicker(EditorApp app, PrimitiveKind kind, string signature)
    {
        if (_createPicking != kind) { _createPicking = kind; _createFilter = ""; }
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 14);
        ImGui.InputTextWithHint("##createfilter", T("Filter"), ref _createFilter, 128);
        var records = (app.Session?.Catalog.BySignature.GetValueOrDefault(signature) ?? [])
            .Where(r => _createFilter.Length == 0 || (r.EditorId?.Contains(_createFilter, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(r => signature == "ACTI" && r.EditorId is { } id && (id.Contains("TRIG", StringComparison.OrdinalIgnoreCase) || id.Contains("Trigger", StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
            .ThenBy(r => r.EditorId, StringComparer.OrdinalIgnoreCase)
            .Take(300).ToList();
        if (ImGui.BeginChild("##createlist", new Vector2(ImGui.GetFontSize() * 18, ImGui.GetFontSize() * 16)))
            foreach (var record in records)
                if (ImGui.Selectable($"{record.EditorId}##{record.FormKey}"))
                {
                    CreatePrimitive(app, kind, record.FormKey);
                    ImGui.CloseCurrentPopup();
                }
        ImGui.EndChild();
    }

    /// <summary>Places the primitive's base and gives the new reference its XPRM — two undo steps (place, primitive).</summary>
    public SceneObject? CreatePrimitive(EditorApp app, PrimitiveKind kind, FormKey? chosen)
    {
        if (_scene is null || app.ActivePlugin is null) return null;
        var (baseKey, type, colour, unknown, defaultSize) = kind switch
        {
            PrimitiveKind.Trigger => (chosen, PlacedPrimitive.TypeEnum.Box, Color.FromArgb(204, 76, 51), 0.15f, new Vector3(256, 256, 256)),
            PrimitiveKind.MultiBoundBox => (RoomMarker, PlacedPrimitive.TypeEnum.Box, Color.FromArgb(0, 128, 255), 0.2f, new Vector3(1024, 1024, 512)),
            PrimitiveKind.MultiBoundSphere => (RoomMarker, PlacedPrimitive.TypeEnum.Sphere, Color.FromArgb(0, 128, 255), 0.2f, new Vector3(1024)),
            PrimitiveKind.OcclusionPlane => (PlaneMarker, PlacedPrimitive.TypeEnum.PortalBox, Color.FromArgb(255, 255, 255), 0.15f, new Vector3(1024, 2, 512)),
            PrimitiveKind.OcclusionBox => (PlaneMarker, PlacedPrimitive.TypeEnum.Box, Color.FromArgb(255, 255, 255), 0.15f, new Vector3(512, 512, 512)),
            PrimitiveKind.RoomBound => (RoomMarker, PlacedPrimitive.TypeEnum.Box, Color.FromArgb(0, 128, 255), 0.2f, new Vector3(1024, 1024, 512)),
            PrimitiveKind.Portal => (PortalMarker, PlacedPrimitive.TypeEnum.PortalBox, Color.FromArgb(0, 0, 0), 0.25f, new Vector3(256, 2, 320)),
            PrimitiveKind.CollisionPlane => (CollisionMarker, PlacedPrimitive.TypeEnum.PortalBox, Color.FromArgb(255, 255, 0), 0.15f, new Vector3(512, 2, 512)),
            PrimitiveKind.CollisionBox => (CollisionMarker, PlacedPrimitive.TypeEnum.Box, Color.FromArgb(255, 255, 0), 0.15f, new Vector3(256, 256, 256)),
            PrimitiveKind.CollisionSphere => (CollisionMarker, PlacedPrimitive.TypeEnum.Sphere, Color.FromArgb(255, 255, 0), 0.15f, new Vector3(256)),
            PrimitiveKind.AcousticSpace => (chosen, PlacedPrimitive.TypeEnum.Box, Color.FromArgb(255, 51, 230), 0.15f, new Vector3(2048, 2048, 1024)),
            _ => (chosen, PlacedPrimitive.TypeEnum.None, Color.White, 0f, Vector3.Zero),
        };
        if (baseKey is not { } key) return null;

        // Around the selection (CK does this for triggers and bounds), else where the view's centre points.
        var encloses = kind is not (PrimitiveKind.Portal or PrimitiveKind.OcclusionPlane or PrimitiveKind.CollisionPlane or PrimitiveKind.SoundEmitter);
        Vector3 position;
        var size = defaultSize;
        if (encloses && _selection.Count > 0)
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            foreach (var o in _selection)
            {
                min = Vector3.Min(min, o.BoundsCenter - new Vector3(o.BoundsRadius));
                max = Vector3.Max(max, o.BoundsCenter + new Vector3(o.BoundsRadius));
            }
            position = (min + max) / 2;
            size = type == PlacedPrimitive.TypeEnum.Sphere ? new Vector3(Vector3.Distance(min, max)) : max - min;
        }
        else
        {
            var (origin, direction) = _camera.Ray(_viewSize / 2, _viewSize);
            var hit = Picking.Pick(_scene, origin, direction, _renderer!.Options)?.Point ?? Picking.PickTerrain(_scene, origin, direction) ?? origin + direction * 600;
            position = hit + new Vector3(0, 0, type == PlacedPrimitive.TypeEnum.None ? 0 : size.Z / 2);
        }
        // Planes face the camera (their thin side is the local Y axis).
        var rotation = new Vector3(0, 0, kind is PrimitiveKind.Portal or PrimitiveKind.OcclusionPlane or PrimitiveKind.CollisionPlane ? _camera.Yaw : 0);

        var created = PlaceNew(app, key, position, rotation, 1f);
        if (created is null) { Log.Warn(T("Could not place it here")); return null; }
        if (type != PlacedPrimitive.TypeEnum.None)
        {
            var bounds = size;
            ReferenceProperties.Apply(app, [created], TF($"Primitive of {created.BaseEditorId}"), r =>
            {
                var placed = (PlacedObject)r;
                placed.Primitive = new PlacedPrimitive { Bounds = new Noggog.P3Float(bounds.X, bounds.Y, bounds.Z), Color = colour, Unknown = unknown, Type = type };
                if (kind is PrimitiveKind.MultiBoundBox or PrimitiveKind.MultiBoundSphere) placed.IsMultiBoundPrimitive = true;
                // Roombounds are persistent in Skyrim.esm (flag 0x400 on all 3 545 of them).
                if (kind == PrimitiveKind.RoomBound) placed.MajorRecordFlagsRaw |= 0x400;
            });
            created.Primitive = (bounds / 2, (int)type, 0xFF000000u | ((uint)colour.B << 16) | ((uint)colour.G << 8) | colour.R);
            created.UpdateBounds();
        }
        Log.Info(TF($"Created {kind} {created.BaseEditorId} at {position.X:0}, {position.Y:0}, {position.Z:0}"));
        return created;
    }

    private bool IsBase(SceneObject o, FormKey key) => o.Base == key;

    /// <summary>A portal joins the selected rooms (XPOD origin, destination); or leaves the selected room.</summary>
    private void LinkPortal(EditorApp app, bool link)
    {
        var portal = _selection.FirstOrDefault(o => IsBase(o, PortalMarker));
        var rooms = _selection.Where(o => IsBase(o, RoomMarker)).Select(o => o.Reference).ToList();
        if (portal is null || rooms.Count == 0 || (link && rooms.Count > 2))
        {
            Log.Warn(link ? T("Select a portal and one or two roombounds") : T("Select a portal and a roombound"));
            return;
        }
        ReferenceProperties.Apply(app, [portal], link ? T("Link portal to room") : T("Detach portal from room"), r =>
        {
            var placed = (PlacedObject)r;
            placed.Portals ??= [];
            var current = placed.Portals.FirstOrDefault();
            var origin = current?.Origin.FormKey ?? FormKey.Null;
            var destination = current?.Destination.FormKey ?? FormKey.Null;
            if (link)
            {
                foreach (var room in rooms)
                    if (room != origin && room != destination)
                    {
                        if (origin.IsNull) origin = room;
                        else destination = room;
                    }
            }
            else
            {
                if (rooms.Contains(origin)) origin = FormKey.Null;
                if (rooms.Contains(destination)) destination = FormKey.Null;
            }
            placed.Portals.Clear();
            if (!origin.IsNull || !destination.IsNull)
            {
                var entry = new Portal();
                entry.Origin.SetTo(origin);
                entry.Destination.SetTo(destination);
                placed.Portals.Add(entry);
            }
        });
    }

    /// <summary>Each selected roombound lists the other selected ones as linked rooms (XLRM).</summary>
    private void JoinRooms(EditorApp app)
    {
        var rooms = _selection.Where(o => IsBase(o, RoomMarker)).ToList();
        if (rooms.Count < 2) { Log.Warn(T("Select two or more roombounds")); return; }
        var keys = rooms.Select(r => r.Reference).ToList();
        foreach (var room in rooms)
            ReferenceProperties.Apply(app, [room], T("Join rooms"), r =>
            {
                var placed = (PlacedObject)r;
                foreach (var other in keys.Where(k => k != room.Reference && placed.LinkedRooms.All(l => l.FormKey != k)))
                    placed.LinkedRooms.Add(new FormLink<IPlacedObjectGetter>(other));
            });
    }
}
