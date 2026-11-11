using System.Numerics;
using Hexa.NET.ImGui;
using Mutagen.Bethesda.Plugins;
using StbEditor.App.Ui;
using static StbEditor.App.Ui.Loc;

namespace StbEditor.App.Panels;

/// <summary>
/// Separate form windows, one per record, as the Creation Kit opens a dialog per form: double-click
/// a record in the Object Window (or «Open in window» in the Inspector). Several can be open at once.
/// </summary>
internal sealed class FormWindows
{
    private readonly List<(FormKey Key, RecordForm Form, bool New)> _open = [];
    private FormKey? _focus;
    private bool _dockAll;

    public void DockAll() => _dockAll = true;

    public int Count => _open.Count;

    public void Open(FormKey key)
    {
        if (!_open.Any(w => w.Key == key)) _open.Add((key, new RecordForm(), true));
        _focus = key;
    }

    public void CloseAll() => _open.Clear();

    public RecordForm? FormOf(FormKey key) => _open.FirstOrDefault(w => w.Key == key).Form;

    /// <summary>One form for several records of a type (Object Window: Edit together): changes go to all of them.</summary>
    public void OpenBatch(IReadOnlyList<FormKey> keys)
    {
        if (keys.Count == 0) return;
        _open.RemoveAll(w => w.Key == keys[0]);
        _open.Add((keys[0], new RecordForm { Batch = keys }, true));
        _focus = keys[0];
    }

    public void Draw(EditorApp app)
    {
        if (app.Session is not { } session) return;
        for (var i = 0; i < _open.Count; i++)
        {
            var (key, form, isNew) = _open[i];
            var record = session.Catalog.Get(key);
            var title = $"{record?.EditorId ?? session.Workspace.FormIdText(key)}  [{record?.Signature}]###form{key}";
            if (DefaultLayout.DocumentDockId != 0 && (_dockAll || isNew))
                ImGui.SetNextWindowDockID(DefaultLayout.DocumentDockId, ImGuiCond.Always);
            else if (isNew)
            {
                // An explicit position also undocks a window; do not send it with a docking request.
                ImGui.SetNextWindowSize(app.Scaled(1000, 760), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos + ImGui.GetMainViewport().Size * 0.5f + new Vector2(i % 6, i % 6) * 24, ImGuiCond.FirstUseEver, new Vector2(0.5f));
            }
            if (_focus == key)
            {
                ImGui.SetNextWindowFocus();
                _focus = null;
            }
            var maximum = ImGui.GetMainViewport().WorkSize * 0.95f;
            ImGui.SetNextWindowSizeConstraints(Vector2.Min(app.Scaled(760, 540), maximum), maximum);
            var open = true;
            var visible = Studio.BeginWindow(title, ref open);
            DefaultLayout.EnsureFloatingDocumentSize(Vector2.Min(app.Scaled(760, 540), maximum), maximum);
            if (visible) form.Draw(app, key, inWindow: true);
            ImGui.End();
            if (isNew) _open[i] = (key, form, false);
            if (!open) _open.RemoveAt(i--);
        }
        _dockAll = false;
    }
}
