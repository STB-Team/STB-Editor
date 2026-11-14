using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.D3D11;
using Hexa.NET.ImGui.Backends.Win32;
using StbEditor.App.Graphics;
using HexaDevice = Hexa.NET.ImGui.Backends.D3D11.ID3D11Device;
using HexaContext = Hexa.NET.ImGui.Backends.D3D11.ID3D11DeviceContext;

namespace StbEditor.App.Ui;

/// <summary>ImGui context with the official Win32 + D3D11 backends, fonts and the editor theme.</summary>
internal sealed unsafe class ImGuiLayer : IDisposable
{
    private readonly ImGuiContextPtr _context;
    private readonly nint _iniPath;

    public ImFontPtr DefaultFont { get; private set; }
    public ImFontPtr MonoFont { get; }

    /// <summary>Display font for record titles (Sovngarde when the build has it).</summary>
    public ImFontPtr HeaderFont { get; private set; }

    public string FontSource { get; private set; }
    private readonly Dictionary<string, ImFontPtr> _bodyFonts = new(StringComparer.OrdinalIgnoreCase);
    private readonly float _fontSize;
    private readonly ImFontPtr _fallbackFont;
    private readonly ImFontPtr? _gameHeaderFont;
    private (string? Body, bool GameHeadings)? _pendingFont;

    public ImGuiLayer(nint hwnd, GraphicsDevice graphics, float dpiScale, string iniFile, UiFonts fonts, bool gameHeadings)
    {
        _context = ImGui.CreateContext();
        ImGui.SetCurrentContext(_context);

        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.DockingEnable;
#if !DEBUG
        // Users get no red «conflicting ID» box; Debug builds keep it to catch the cause.
        io.ConfigDebugHighlightIdConflicts = false;
#endif
        io.ConfigWindowsMoveFromTitleBarOnly = true;

        Directory.CreateDirectory(Path.GetDirectoryName(iniFile)!);
        _iniPath = Marshal.StringToCoTaskMemUTF8(iniFile);
        io.IniFilename = (byte*)_iniPath;

        // Only load the selected body face. Further choices are cached, keeping existing ImFont pointers alive.
        _fontSize = MathF.Round(16f * dpiScale);
        DefaultFont = fonts.Body is { } body ? io.Fonts.AddFontFromFileTTF(body, _fontSize) : AddFont(io, ["segoeui.ttf", "arial.ttf"], _fontSize);
        _fallbackFont = DefaultFont;
        if (fonts.Body is { } path) _bodyFonts.Add(path, DefaultFont);
        MonoFont = AddFont(io, ["consola.ttf", "cour.ttf"], MathF.Round(15f * dpiScale));
        _gameHeaderFont = fonts.Header is { } header ? io.Fonts.AddFontFromFileTTF(header, MathF.Round(22f * dpiScale)) : null;
        HeaderFont = gameHeadings ? _gameHeaderFont ?? DefaultFont : DefaultFont;
        io.FontDefault = DefaultFont;
        FontSource = fonts.Body ?? "ImGui default";

        Theme.Apply(dpiScale);

        ImGuiImplWin32.SetCurrentContext(_context);
        ImGuiImplWin32.Init((void*)hwnd);

        ImGuiImplD3D11.SetCurrentContext(_context);
        ImGuiImplD3D11.Init(
            new ID3D11DevicePtr((HexaDevice*)graphics.Device.NativePointer),
            new ID3D11DeviceContextPtr((HexaContext*)graphics.Context.NativePointer));
    }

    private static ImFontPtr AddFont(ImGuiIOPtr io, string[] candidates, float size)
    {
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var name in candidates)
        {
            var path = Path.Combine(fonts, name);
            if (File.Exists(path)) return io.Fonts.AddFontFromFileTTF(path, size);
        }

        return io.Fonts.AddFontDefault();
    }

    /// <summary>Apply outside the current ImGui frame; never recreate the context or discard open editors.</summary>
    public void RequestFont(UiFontFace? face, bool gameHeadings) => _pendingFont = (face?.Path, gameHeadings);

    private void ApplyPendingFont()
    {
        if (_pendingFont is not { } pending) return;
        _pendingFont = null;
        var font = _fallbackFont;
        if (pending.Body is { } path && !_bodyFonts.TryGetValue(path, out font))
        {
            // The atlas is unlocked here. The official backend recreates its device objects in NewFrame.
            ImGuiImplD3D11.InvalidateDeviceObjects();
            font = ImGui.GetIO().Fonts.AddFontFromFileTTF(path, _fontSize);
            _bodyFonts.Add(path, font);
        }
        DefaultFont = font;
        HeaderFont = pending.GameHeadings ? _gameHeaderFont ?? font : font;
        ImGui.GetIO().FontDefault = font;
        FontSource = pending.Body ?? "ImGui default";
        StbEditor.App.Services.Log.Info($"UI font: {FontSource}; game headings {pending.GameHeadings}");
    }

    /// <summary>Feeds a window message to ImGui. True when ImGui consumed it.</summary>
    public bool HandleMessage(nint hwnd, uint msg, nint wParam, nint lParam) =>
        ImGuiImplWin32.WndProcHandler(hwnd, msg, (nuint)wParam, lParam) != 0;

    /// <summary>Autotests: mouse position / left button to feed ImGui instead of the real mouse.</summary>
    public static (System.Numerics.Vector2 Position, bool Down)? TestMouse { get; set; }

    /// <summary>Autotests: the button <see cref="TestMouse"/> presses (0 left, 1 right).</summary>
    public static int TestMouseButton { get; set; }

    /// <summary>Autotests: text typed into the focused field this frame, and keys pressed (released the next frame).</summary>
    public static string? TestText { get; set; }

    /// <summary>Autotests: wheel notches this frame.</summary>
    public static float TestWheel { get; set; }
    public static Queue<ImGuiKey> TestKeys { get; } = new();
    private static ImGuiKey? _testKeyDown;

    /// <summary>Autotests: Shift held while true.</summary>
    public static bool TestShift { get; set; }
    private static bool _testShiftSent;

    public void NewFrame()
    {
        ApplyPendingFont();
        ImGuiImplD3D11.NewFrame();
        ImGuiImplWin32.NewFrame();
        if (TestMouse is { } mouse)
        {
            var io = ImGui.GetIO();
            io.AddMousePosEvent(mouse.Position.X, mouse.Position.Y);
            io.AddMouseButtonEvent(TestMouseButton, mouse.Down);
        }
        if (TestShift != _testShiftSent)
        {
            ImGui.GetIO().AddKeyEvent(ImGuiKey.ModShift, TestShift);
            ImGui.GetIO().AddKeyEvent(ImGuiKey.LeftShift, TestShift);
            _testShiftSent = TestShift;
        }
        if (TestWheel != 0)
        {
            ImGui.GetIO().AddMouseWheelEvent(0, TestWheel);
            TestWheel = 0;
        }
        if (TestText is { } typed)
        {
            foreach (var ch in typed) ImGui.GetIO().AddInputCharacter(ch);
            TestText = null;
        }
        if (_testKeyDown is { } down)
        {
            ImGui.GetIO().AddKeyEvent(down, false);
            _testKeyDown = null;
        }
        else if (TestKeys.TryDequeue(out var key))
        {
            ImGui.GetIO().AddKeyEvent(key, true);
            _testKeyDown = key;
        }
        ImGui.NewFrame();
    }

    public void Render()
    {
        ImGui.Render();
        ImGuiImplD3D11.RenderDrawData(ImGui.GetDrawData());
    }

    public static bool WantsInput
    {
        get
        {
            var io = ImGui.GetIO();
            return io.WantCaptureMouse || io.WantCaptureKeyboard;
        }
    }

    public void Dispose()
    {
        ImGuiImplD3D11.Shutdown();
        ImGuiImplWin32.Shutdown();
        // cimguizmo keeps its own pointer to the context; left dangling, its static state frees through
        // it when the DLL unloads (access violation on exit, only after the gizmo was used).
        Hexa.NET.ImGuizmo.ImGuizmo.SetImGuiContext(null);
        ImGui.DestroyContext(_context);
        Marshal.FreeCoTaskMem(_iniPath);
    }
}

