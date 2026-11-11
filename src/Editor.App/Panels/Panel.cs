using System.Numerics;
using Hexa.NET.ImGui;
using StbEditor.App.Services;
using StbEditor.App.Ui;
using static StbEditor.App.Ui.Loc;

namespace StbEditor.App.Panels;

/// <summary>A dockable editor window.</summary>
internal abstract class Panel
{
    /// <summary>Visible title; also the ImGui window id, so it must stay stable.</summary>
    public abstract string Name { get; }

    public bool IsOpen { get; set; } = true;
    private bool _focus;

    public void OpenAndFocus()
    {
        IsOpen = true;
        _focus = true;
    }

    public void Draw(EditorApp app)
    {
        var open = IsOpen;
        ImGui.SetNextWindowSize(new Vector2(500, 400), ImGuiCond.FirstUseEver);
        if (_focus)
        {
            ImGui.SetNextWindowFocus();
            _focus = false;
        }
        if (Studio.BeginWindow(Title(Name), ref open, WindowFlags))
        {
            Studio.PanelEdge();
            DrawContent(app);
        }
        ImGui.End();
        IsOpen = open;
    }

    protected virtual ImGuiWindowFlags WindowFlags => ImGuiWindowFlags.None;

    protected abstract void DrawContent(EditorApp app);

    public virtual void OnSessionChanged(EditorApp app) { }

    /// <summary>Placeholder for panels that need loaded data.</summary>
    protected static bool RequireSession(EditorApp app)
    {
        if (app.Session is not null) return true;

        var text = app.Loader is { } loader ? loader.Status + "..." : T("No data loaded. File → Data...");
        var size = ImGui.CalcTextSize(text);
        var avail = ImGui.GetContentRegionAvail();
        ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(Math.Max(0, (avail.X - size.X) / 2), Math.Max(0, avail.Y / 2 - size.Y)));
        ImGui.TextColored(Theme.TextDim, text);
        return false;
    }
}

internal static class StatusBar
{
    private static string? _notice;
    private static double _noticeUntil;

    /// <summary>A result shown at the right of the status bar for a few seconds (and logged).</summary>
    public static void Notice(string text)
    {
        _notice = text;
        _noticeUntil = ImGui.GetTime() + 8;
        Log.Info(text);
    }

    public static void Draw(EditorApp app)
    {
        var height = ImGui.GetFrameHeight() + ImGui.GetFontSize() * 0.25f;
        var flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.MenuBar;

        if (ImGuiP.BeginViewportSideBar("##StatusBar", ImGui.GetMainViewport(), ImGuiDir.Down, height, flags))
        {
            var pos = ImGui.GetWindowPos();
            ImGui.GetWindowDrawList().AddLine(pos, pos + new Vector2(ImGui.GetWindowWidth(), 0), ImGui.GetColorU32(Theme.EdgeLight));
            if (ImGui.BeginMenuBar())
            {
                if (app.Loader is { } loader)
                {
                    ImGui.TextColored(Theme.Accent, Spinner());
                    ImGui.Text(loader.Status);
                }
                else if (app.Session is { } session)
                {
                    ImGui.TextColored(Theme.Good, T("Ready"));
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(app.ActivePlugin is { } active
                            ? TF($"· Active file: {active.Name}{(active.IsNew ? " (new)" : "")}")
                            : T("· Active file: none (Ctrl+S asks)"));
                    if (ImGui.GetWindowWidth() > ImGui.GetFontSize() * 65)
                        ImGui.TextColored(Theme.TextDim,
                            TF($"{session.Catalog.ByKey.Count:N0} records · {session.Assets.ArchivedCount + session.Assets.LooseCount:N0} assets · loaded in {session.LoadTime.TotalSeconds:0.0} s"));
                    if (app.Saver.IsBusy) ImGui.TextColored(Theme.Warning, T("· saving…"));
                    if (app.Document.PendingCount > 0)
                        ImGui.TextColored(Theme.Warning, TF($"· {app.Document.PendingCount} unsaved"));
                }
                else
                {
                    ImGui.TextColored(Theme.TextDim, T("No data loaded"));
                }

                var last = Log.Lines.LastOrDefault(l => l.Level != LogLevel.Info);
                if (_notice is not null && ImGui.GetTime() < _noticeUntil)
                {
                    var text = _notice.Length > 120 ? _notice[..120] + "…" : _notice;
                    DrawMessage(text, Theme.Good);
                }
                else if (last is not null)
                {
                    var text = last.Text.Length > 120 ? last.Text[..120] + "…" : last.Text;
                    DrawMessage(text, last.Level == LogLevel.Error ? Theme.Error : Theme.Warning);
                }

                ImGui.EndMenuBar();
            }
        }
        ImGui.End();
    }

    private static void DrawMessage(string text, Vector4 color)
    {
        var width = ImGui.CalcTextSize(text).X;
        var remaining = ImGui.GetContentRegionAvail().X;
        if (remaining < ImGui.GetFontSize() * 8) return;
        var shownWidth = Math.Min(width, remaining - ImGui.GetFontSize());
        ImGui.SameLine(ImGui.GetWindowWidth() - shownWidth - ImGui.GetStyle().WindowPadding.X);
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(shownWidth, ImGui.GetTextLineHeight());
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(min, max, true);
        draw.AddText(min, ImGui.GetColorU32(color), text);
        draw.PopClipRect();
        ImGui.Dummy(max - min);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(text);
    }

    private static string Spinner() => "|/-\\"[(int)(ImGui.GetTime() * 8) % 4].ToString();
}

internal enum LayoutPreset { World, Records, Classic }

/// <summary>Explicit panel arrangements. Existing saved layouts are never replaced on startup.</summary>
internal static unsafe class DefaultLayout
{
    private static bool _pending;
    public static LayoutPreset Preset { get; private set; }
    public static uint DocumentDockId { get; private set; }

    public static void Initialize(LayoutPreset preset) => Preset = preset;

    public static void Request(LayoutPreset preset)
    {
        Preset = preset;
        _pending = true;
    }

    public static void BuildIfMissing(uint dockId, string iniFile)
    {
        if (File.Exists(iniFile) && !_pending)
        {
            RefreshDocumentDock(dockId);
            return;
        }
        Build(dockId);
    }

    public static bool ApplyPendingReset(uint dockId)
    {
        if (!_pending)
        {
            // Saved docking nodes can become available after the first frame, or change
            // when a user splits/moves a tab. Do not cache an absent or obsolete node.
            RefreshDocumentDock(dockId);
            return false;
        }
        Build(dockId);
        return true;
    }

    private static void RefreshDocumentDock(uint dockId)
    {
        var center = Preset == LayoutPreset.Records ? ImGuiP.DockBuilderGetCentralNode(dockId) : default;
        DocumentDockId = center.Handle != null ? center.ID : 0;
    }

    /// <summary>Native per-window constraints do not resize a saved floating dock host.</summary>
    public static void EnsureFloatingDocumentSize(Vector2 minimum, Vector2 maximum)
    {
        var window = ImGuiP.GetCurrentWindow();
        var host = window;
        if (window.DockNode.Handle != null)
        {
            var root = ImGuiP.DockNodeGetRootNode(window.DockNode);
            if (root.Handle == null || ImGuiP.IsDockSpace(root) || root.HostWindow.Handle == null) return;
            host = root.HostWindow;
        }
        var size = Vector2.Clamp(host.Size, minimum, maximum);
        if (size != host.Size) ImGuiP.SetWindowSize(host, size, ImGuiCond.Always);
        var viewport = ImGui.GetMainViewport();
        var lastPosition = Vector2.Max(viewport.WorkPos, viewport.WorkPos + viewport.WorkSize - size);
        var position = Vector2.Clamp(host.Pos, viewport.WorkPos, lastPosition);
        if (position != host.Pos) ImGuiP.SetWindowPos(host, position, ImGuiCond.Always);
    }

    private static void Build(uint dockId)
    {
        _pending = false;
        DocumentDockId = 0;
        var viewport = ImGui.GetMainViewport();
        ImGuiP.DockBuilderRemoveNode(dockId);
        ImGuiP.DockBuilderAddNode(dockId, (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate.Space | ImGuiDockNodeFlags.PassthruCentralNode);
        ImGuiP.DockBuilderSetNodeSize(dockId, viewport.WorkSize);

        uint left = 0, right = 0, bottom = 0, center = dockId, preview = 0;
        if (Preset == LayoutPreset.Classic)
        {
            ImGuiP.DockBuilderSplitNode(center, ImGuiDir.Left, 0.36f, &left, &center);
            ImGuiP.DockBuilderSplitNode(center, ImGuiDir.Right, 0.34f, &right, &center);
            ImGuiP.DockBuilderSplitNode(center, ImGuiDir.Down, 0.25f, &bottom, &center);
            uint cells = 0;
            ImGuiP.DockBuilderSplitNode(left, ImGuiDir.Down, 0.40f, &cells, &left);
            ImGuiP.DockBuilderSplitNode(right, ImGuiDir.Down, 0.30f, &preview, &right);
            Dock("Object Window", left);
            Dock("Cell View", cells);
            Dock("Inspector", right);
            Dock("Render Window", center);
        }
        else if (Preset == LayoutPreset.Records)
        {
            ImGuiP.DockBuilderSplitNode(center, ImGuiDir.Left, 0.38f, &left, &center);
            DocumentDockId = center;
            preview = center;
            bottom = left;
            Dock("Object Window", left);
            Dock("Cell View", left);
            Dock("Inspector", center);
            Dock("Render Window", center);
        }
        else
        {
            ImGuiP.DockBuilderSplitNode(center, ImGuiDir.Right, 0.23f, &right, &center);
            var catalogHeight = Math.Clamp(ImGui.GetFontSize() * 12 / viewport.WorkSize.Y, 0.27f, 0.36f);
            ImGuiP.DockBuilderSplitNode(center, ImGuiDir.Down, catalogHeight, &bottom, &center);
            ImGuiP.DockBuilderSplitNode(center, ImGuiDir.Left, 0.23f, &left, &center);
            ImGuiP.DockBuilderSplitNode(right, ImGuiDir.Down, 0.28f, &preview, &right);
            Dock("Cell View", left);
            Dock("Object Window", bottom);
            Dock("Inspector", right);
            Dock("Render Window", center);
        }
        Dock("Preview", preview);
        Dock("Landscape", preview);
        Dock("Navmesh", preview);
        Dock("Log", bottom);
        Dock("Load Order", bottom);
        ImGuiP.DockBuilderFinish(dockId);
    }

    private static void Dock(string name, uint node) => ImGuiP.DockBuilderDockWindow("###" + name, node);
}

internal sealed class LogPanel : Panel
{
    private int _seenVersion;
    private bool _autoScroll = true;
    private bool _showInfo = true;

    public override string Name => "Log";

    protected override void DrawContent(EditorApp app)
    {
        ImGui.Checkbox(T("Info"), ref _showInfo);
        ImGui.SameLine();
        ImGui.Checkbox(T("Auto-scroll"), ref _autoScroll);
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Open log file")))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPaths.LogFile) { UseShellExecute = true });

        ImGui.BeginChild("##log", new Vector2(0, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar);
        ImGui.PushFont(app.Ui.MonoFont, 0);
        foreach (var line in Log.Lines)
        {
            if (!_showInfo && line.Level == LogLevel.Info) continue;
            var color = line.Level switch
            {
                LogLevel.Error => Theme.Error,
                LogLevel.Warning => Theme.Warning,
                _ => Theme.TextDim,
            };
            ImGui.TextColored(color, $"{line.Time:HH:mm:ss}  {line.Text}");
        }
        ImGui.PopFont();

        if (_autoScroll && _seenVersion != Log.Version) ImGui.SetScrollHereY(1f);
        _seenVersion = Log.Version;
        ImGui.EndChild();
    }
}
