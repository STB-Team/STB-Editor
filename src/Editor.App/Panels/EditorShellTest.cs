using Hexa.NET.ImGui;
using StbEditor.App.Services;
using StbEditor.Core;
using StbEditor.App.Ui;
using StbEditor.Render;

namespace StbEditor.App.Panels;

/// <summary>Exercises layout changes with an unsaved placement, open forms and undo history. Never saves.</summary>
internal sealed unsafe class EditorShellTest
{
    private readonly bool _enabled = Environment.GetCommandLineArgs().Contains("--autotest-layout");
    private int _stage;
    private int _frames;
    private Scene? _scene;
    private EditDocument? _document;
    private SceneObject? _object;
    private RefState _before;
    private RefState _edited;
    private int _forms;
    private int _comparisons;
    private System.Numerics.Vector2 _closePoint;

    public bool Completed => !_enabled || _stage == 8;

    public void Step(EditorApp app)
    {
        if (Completed || app.Session is null || app.Loader is not null) return;
        if (_stage == 0)
        {
            if (!app.RenderWindow.IsSettled) return;
            if (DefaultLayout.Preset == LayoutPreset.Records)
            {
                var inspector = ImGuiP.FindWindowByName("###Inspector");
                Require(DefaultLayout.DocumentDockId != 0 && inspector.Handle != null
                    && inspector.DockId == DefaultLayout.DocumentDockId, "saved records layout has no usable document dock after startup");
            }
            _scene = app.RenderWindow.Scene!;
            _document = app.Document;
            _object = _scene.Objects.First(o => o.BaseSignature == "STAT" && o.Model is { IsEmpty: false } && !o.IsDeleted && app.Session.Catalog.Get(o.Base) is not null);
            _before = _object.State;
            app.RenderWindow.SelectForTest(_object);
            app.Select(_object.Reference);
            app.RenderWindow.RotateSelectionForTest(15);
            _edited = _object.State;
            Require(_edited != _before && app.Document.PendingCount == 1, "placement edit was not recorded");
            app.FormWindows.Open(_object.Base);
            app.Compare.OpenVersions(_object.Base);
            _forms = app.FormWindows.Count;
            _comparisons = app.Compare.Count;
            app.ApplyLayout(LayoutPreset.Records);
            _stage = 1;
            _frames = 0;
            return;
        }

        if (++_frames < 4) return; // Allow the real dockspace and its panels to redraw after each transition.
        if (_stage >= 4)
        {
            CheckCloseTargets(app);
            return;
        }
        Require(ReferenceEquals(_scene, app.RenderWindow.Scene) && ReferenceEquals(_document, app.Document), "layout replaced scene or document");
        Require(app.Selected == _object!.Reference && ReferenceEquals(app.RenderWindow.SingleSelection, _object), "layout lost selection");
        if (_stage == 1)
        {
            var form = ImGuiP.FindWindowByName("###form" + _object.Base);
            var comparison = ImGuiP.FindWindowByName("###compare0");
            Require(DefaultLayout.DocumentDockId != 0 && form.Handle != null && comparison.Handle != null
                && form.DockId == DefaultLayout.DocumentDockId && comparison.DockId == DefaultLayout.DocumentDockId,
                $"records layout did not dock documents (target {DefaultLayout.DocumentDockId:X}, form {(form.Handle != null ? form.DockId : uint.MaxValue):X}, comparison {(comparison.Handle != null ? comparison.DockId : uint.MaxValue):X})");
            var inspector = ImGuiP.FindWindowByName("###Inspector");
            var preview = ImGuiP.FindWindowByName("###Preview");
            var catalog = ImGuiP.FindWindowByName("###Object Window");
            Require(inspector.Handle != null && preview.Handle != null && inspector.DockId == preview.DockId
                && inspector.DockId == DefaultLayout.DocumentDockId, "preview is not a full-height tab beside the inspector");
            Require(catalog.Handle != null && catalog.Size.X >= ImGui.GetMainViewport().WorkSize.X * 0.36f,
                "record catalog is too narrow");
        }
        if (_stage == 2)
        {
            var form = ImGuiP.FindWindowByName("###form" + _object.Base);
            var comparison = ImGuiP.FindWindowByName("###compare0");
            var minimum = System.Numerics.Vector2.Min(app.Scaled(760, 540), ImGui.GetMainViewport().WorkSize * 0.95f);
            Require(form.Handle != null && comparison.Handle != null
                && form.Size.X >= minimum.X - 1 && form.Size.Y >= minimum.Y - 1
                && comparison.Size.X >= minimum.X - 1 && comparison.Size.Y >= minimum.Y - 1,
                "floating record or comparison opened below its readable minimum size");
        }
        Require(_object.State == _edited && app.Document.PendingCount == 1, "layout changed the unsaved placement");
        Require(app.FormWindows.Count == _forms && app.Compare.Count == _comparisons, "layout closed an open document");
        Log.Info($"autotest-layout: {DefaultLayout.Preset}: scene, selection, edit and open documents preserved");
        _frames = 0;
        if (_stage == 1)
        {
            app.ApplyLayout(LayoutPreset.Classic);
            _stage = 2;
        }
        else if (_stage == 2)
        {
            app.ApplyLayout(LayoutPreset.World);
            _stage = 3;
        }
        else
        {
            app.Document.Undo();
            Require(_object.State == _before && app.Document.PendingCount == 0, "undo failed after layout transitions");
            app.Document.Redo();
            Require(_object.State == _edited && app.Document.PendingCount == 1, "redo failed after layout transitions");
            app.Document.Undo();
            Require(_object.State == _before && app.Document.PendingCount == 0, "final undo did not restore the original placement");
            app.ApplyLayout(LayoutPreset.Records);
            _stage = 4;
        }
    }

    private void CheckCloseTargets(EditorApp app)
    {
        switch (_stage)
        {
            case 4:
                // Comparison is the active tab; close the inactive form from outside its native 16px X.
                PrepareClose(ImGuiP.FindWindowByName("###form" + _object!.Base));
                _stage = 5;
                break;
            case 5 when CompleteCloseClick():
                Require(app.FormWindows.Count == _forms - 1 && app.Compare.Count == _comparisons,
                    "padded inactive tab close target did not close only that form");
                Log.Info("autotest-layout: inactive dock tab closed at the outer edge of its padded target");
                app.ApplyLayout(LayoutPreset.Classic);
                _stage = 6;
                _frames = 0;
                break;
            case 6:
                var comparison = ImGuiP.FindWindowByName("###compare0");
                Require(comparison.Handle != null && !comparison.DockIsActive, "comparison did not become a floating window");
                ImGuiP.FocusWindow(comparison);
                PrepareClose(comparison);
                _stage = 7;
                break;
            case 7 when CompleteCloseClick():
                Require(app.Compare.Count == _comparisons - 1 && app.FormWindows.Count == _forms - 1,
                    $"padded floating close target did not close only the comparison (point {_closePoint}, hovered window {(ImGui.GetCurrentContext().HoveredWindow.Handle != null ? ImGui.GetCurrentContext().HoveredWindow.ID : 0):X})");
                Log.Info("autotest-layout: floating window closed at the outer edge of its padded target");
                app.ApplyLayout(LayoutPreset.World);
                Require(app.Document.PendingCount == 0 && _object!.State == _before, "closing documents changed placement edits");
                Log.Info("autotest-layout: PASS: layouts, sizes, preview tab, undo/redo and padded close targets; 0 unsaved; nothing saved");
                _stage = 8;
                break;
        }
    }

    private void PrepareClose(ImGuiWindowPtr window)
    {
        Require(WindowChrome.TryGetCloseTarget(window, out var target), "no close target was available");
        Require(target.Max.X - target.Min.X >= ImGui.GetFontSize() * 1.4f, "close target is still font-sized");
        _closePoint = new System.Numerics.Vector2(target.Min.X + 1, (target.Min.Y + target.Max.Y) * 0.5f);
        Log.Info($"autotest-layout: close {window.ID:X}, position {window.Pos}, size {window.Size}, target {target.Min}..{target.Max}, point {_closePoint}");
        ImGuiLayer.TestMouseButton = 0;
        ImGuiLayer.TestMouse = (_closePoint, false);
        _frames = 0;
    }

    private bool CompleteCloseClick()
    {
        if (_frames == 4) ImGuiLayer.TestMouse = (_closePoint, true);
        if (_frames == 8) ImGuiLayer.TestMouse = (_closePoint, false);
        if (_frames < 12) return false;
        ImGuiLayer.TestMouse = null;
        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("autotest-layout: " + message);
    }
}
