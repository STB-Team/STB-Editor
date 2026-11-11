using System.Numerics;
using Hexa.NET.ImGui;
using StbEditor.Data;

namespace StbEditor.App.Ui;

/// <summary>
/// Segoe UI for long editing sessions and Cyrillic text; optional Sovngarde headings come from
/// the player's own build. No fonts are shipped with the editor.
/// </summary>
internal sealed record UiFonts(string? Body, string? Header)
{
    private static readonly string[] HeaderNames = ["SovngardeBold.ttf", "Sovngarde.ttf", "SovngardeLight.ttf"];

    public static UiFonts Find(GameInstall? game)
    {
        var systemFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var body = new[] { "segoeui.ttf", "arial.ttf" }
            .Select(name => Path.Combine(systemFonts, name)).FirstOrDefault(File.Exists);
        if (game is null) return new UiFonts(body, null);

        var folders = new List<string>
        {
            Path.Combine(game.DataFolder, "SKSE", "Plugins", "fonts"),
            Path.Combine(game.DataFolder, "Interface", "fonts"),
        };

        if (game.Mo2 is { } mo2)
        {
            // EnabledModFolders runs from low to high priority; the winner is searched first.
            foreach (var mod in mo2.EnabledModFolders().Reverse())
            {
                folders.Add(Path.Combine(mod, "SKSE", "Plugins", "fonts"));
                folders.Add(Path.Combine(mod, "Interface", "fonts"));
            }
        }

        string? FindFirst(string[] names)
        {
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var name in names)
                {
                    var path = Path.Combine(folder, name);
                    if (File.Exists(path)) return path;
                }
            }
            return null;
        }

        return new UiFonts(body, FindFirst(HeaderNames));
    }
}

/// <summary>A colour theme: backgrounds, text, the accent and the tint that lights rows and buttons.</summary>
internal sealed record Palette(string Name, bool Light, Vector4 Bg, Vector4 Bar, Vector4 Text, Vector4 TextDim, Vector4 Accent, Vector4 Tint,
    Vector4 Warning, Vector4 Error, Vector4 Good, Vector4 Override);

/// <summary>
/// Colour themes (View → Settings → Theme), as xEdit's styles. The default, «STB», is a
/// neutral studio workspace: layered graphite surfaces, readable text and restrained white
/// highlights. The other themes retain their individual palettes.
/// </summary>
internal static class Theme
{
    public static Vector4 Accent { get; private set; }
    public static Vector4 Text { get; private set; }
    public static Vector4 TextDim { get; private set; }
    public static Vector4 Warning { get; private set; }
    public static Vector4 Error { get; private set; }
    public static Vector4 Good { get; private set; }
    public static Vector4 Override { get; private set; }
    public static Vector4 ButtonActive { get; private set; }
    public static Vector4 SurfaceSelected { get; private set; }
    public static Vector4 SurfaceHover { get; private set; }
    public static Vector4 ControlBorder { get; private set; }
    public static Vector4 ControlSelectedBorder { get; private set; }
    public static Vector4 EdgeLight { get; private set; }
    public static Vector4 Shadow { get; private set; }
    public static bool IsLight { get; private set; }
    public static string Current { get; private set; } = "STB";

    private static Vector4 Rgb(int r, int g, int b, float a = 1f) => new(r / 255f, g / 255f, b / 255f, a);
    private static Vector4 White(float a) => new(1f, 1f, 1f, a);
    public static Vector4 Alpha(Vector4 c, float a) => c with { W = a };
    private static Vector4 Mix(Vector4 a, Vector4 b, float t) => Vector4.Lerp(a, b, t) with { W = 1f };

    /// <summary>A row background for a highlighted state colour (comparison rows).</summary>
    public static Vector4 RowTint(Vector4 color) => Alpha(color, IsLight ? 0.20f : 0.14f);

    private static readonly Vector4 DarkWarning = Rgb(222, 180, 100), DarkError = Rgb(230, 110, 100), DarkGood = Rgb(140, 210, 130), DarkOverride = Rgb(190, 175, 220);
    private static readonly Vector4 LightWarning = Rgb(160, 100, 0), LightError = Rgb(190, 40, 30), LightGood = Rgb(20, 120, 40), LightOverride = Rgb(110, 60, 170);

    private static Vector4 Rgb((int R, int G, int B) c) => Rgb(c.R, c.G, c.B);

    private static Palette Dark(string name, (int, int, int) bg, (int, int, int) bar, (int, int, int) text, (int, int, int) dim, (int, int, int) accent, (int, int, int) tint) =>
        new(name, false, Rgb(bg), Rgb(bar), Rgb(text), Rgb(dim), Rgb(accent), Rgb(tint), DarkWarning, DarkError, DarkGood, DarkOverride);

    private static Palette Bright(string name, (int, int, int) bg, (int, int, int) bar, (int, int, int) text, (int, int, int) dim, (int, int, int) accent, (int, int, int) tint) =>
        new(name, true, Rgb(bg), Rgb(bar), Rgb(text), Rgb(dim), Rgb(accent), Rgb(tint), LightWarning, LightError, LightGood, LightOverride);

    /// <summary>Every theme, in the order the settings list them; the first is the default.</summary>
    public static readonly Palette[] Palettes =
    [
        Dark("STB", (32, 34, 38), (24, 26, 30), (232, 234, 237), (166, 172, 181), (238, 241, 245), (255, 255, 255)),
        Dark("Windows Dark", (32, 32, 32), (24, 24, 24), (232, 232, 232), (150, 150, 150), (96, 172, 255), (0, 120, 215)),
        Dark("Carbon", (28, 28, 28), (17, 17, 17), (222, 222, 222), (140, 140, 140), (255, 160, 60), (230, 125, 20)),
        Dark("Charcoal Dark Slate", (38, 43, 48), (29, 33, 37), (216, 223, 229), (140, 150, 158), (100, 210, 200), (55, 145, 150)),
        Dark("Cobalt", (20, 30, 50), (13, 21, 38), (222, 230, 242), (138, 155, 182), (120, 186, 255), (48, 108, 220)),
        Dark("Oxford Blue", (31, 38, 52), (23, 29, 41), (226, 229, 236), (146, 153, 170), (255, 196, 40), (82, 110, 170)),
        Dark("Turquoise Gray", (44, 48, 50), (33, 36, 38), (224, 228, 230), (150, 157, 161), (70, 226, 210), (30, 160, 150)),
        Dark("Nord", (46, 52, 64), (37, 42, 52), (229, 233, 240), (152, 162, 180), (136, 192, 208), (94, 129, 172)),
        Dark("Amethyst", (30, 24, 38), (22, 17, 29), (230, 222, 240), (156, 143, 172), (196, 150, 255), (128, 78, 200)),
        Dark("Emerald", (20, 32, 28), (13, 23, 20), (222, 238, 230), (140, 166, 153), (90, 225, 155), (30, 150, 100)),
        Dark("Dwemer", (30, 25, 20), (21, 17, 13), (234, 222, 198), (162, 147, 122), (226, 176, 96), (170, 120, 60)),
        Dark("Dark Brotherhood", (26, 18, 18), (17, 11, 11), (234, 218, 218), (166, 140, 140), (232, 92, 92), (150, 32, 32)),
        Bright("Windows Light", (240, 240, 240), (223, 223, 226), (24, 24, 28), (100, 100, 108), (0, 92, 180), (0, 120, 215)),
        Bright("Iceberg", (231, 237, 243), (212, 222, 232), (26, 34, 46), (92, 108, 126), (25, 105, 175), (70, 140, 200)),
        Bright("Aqua Light Slate", (225, 231, 235), (199, 211, 219), (28, 38, 44), (94, 108, 116), (0, 125, 145), (40, 160, 180)),
    ];

    /// <summary>Sizes once (scaled for the monitor), then the colours of the current theme.</summary>
    public static void Apply(float scale)
    {
        var style = ImGui.GetStyle();
        style.WindowRounding = 0;
        style.ChildRounding = 4;
        style.FrameRounding = 4;
        style.PopupRounding = 6;
        style.ScrollbarRounding = 2;
        style.GrabRounding = 2;
        style.TabRounding = 5;
        style.WindowBorderSize = 1;
        style.ChildBorderSize = 1;
        style.FrameBorderSize = 1;
        style.PopupBorderSize = 1;
        style.TabBorderSize = 1;
        style.TabBarBorderSize = 1;
        style.TabBarOverlineSize = 0;
        style.TabMinWidthBase = 54;
        // Keep a close slot visible and stable even on inactive docking tabs.
        style.TabCloseButtonMinWidthSelected = -1;
        style.TabCloseButtonMinWidthUnselected = -1;
        style.WindowPadding = new Vector2(12, 11);
        style.FramePadding = new Vector2(10, 5);
        style.ItemSpacing = new Vector2(8, 7);
        style.ItemInnerSpacing = new Vector2(6, 4);
        style.IndentSpacing = 16;
        style.ScrollbarSize = 13;
        style.GrabMinSize = 11;
        style.CellPadding = new Vector2(8, 5);
        style.WindowTitleAlign = new Vector2(0f, 0.5f);
        style.SeparatorTextBorderSize = 1;
        style.WindowMenuButtonPosition = ImGuiDir.None;
        style.ScaleAllSizes(scale);
        unsafe { _baseStyle = *style.Handle; }
        _hasBase = true;
        Use(Current);
    }

    private static ImGuiStyle _baseStyle;
    private static bool _hasBase;

    /// <summary>Interface size (1 = 100 %): text and every padding, spacing and rounding scale together.</summary>
    public static float UiScale { get; private set; } = 1f;

    public const float MinUiScale = 0.75f, MaxUiScale = 2f;

    /// <summary>Resizes the whole interface: fonts by FontScaleMain, the style's sizes from the unscaled copy (not cumulatively).</summary>
    public static void SetUiScale(float scale)
    {
        if (!_hasBase) return;
        UiScale = Math.Clamp(MathF.Round(scale * 20f) / 20f, MinUiScale, MaxUiScale);
        var style = ImGui.GetStyle();
        unsafe { *style.Handle = _baseStyle; }
        style.ScaleAllSizes(UiScale);
        style.FontScaleMain = UiScale;
        Use(Current);
    }

    /// <summary>Switches the colours to the theme <paramref name="name"/> (unknown names: STB).</summary>
    public static void Use(string? name)
    {
        var palette = Palettes.FirstOrDefault(p => p.Name == name) ?? Palettes[0];
        Current = palette.Name;
        IsLight = palette.Light;
        Accent = palette.Accent;
        Text = palette.Text;
        TextDim = palette.TextDim;
        Warning = palette.Warning;
        Error = palette.Error;
        Good = palette.Good;
        Override = palette.Override;

        if (palette == Palettes[0]) Monochrome();
        else Colors(palette);
        Surfaces(palette);
    }

    private static void Monochrome()
    {
        ButtonActive = Rgb(82, 88, 98);
        var c = ImGui.GetStyle().Colors;
        c[(int)ImGuiCol.Text] = Text;
        c[(int)ImGuiCol.TextDisabled] = TextDim;
        c[(int)ImGuiCol.WindowBg] = Rgb(32, 34, 38);
        c[(int)ImGuiCol.ChildBg] = Rgb(29, 31, 35);
        c[(int)ImGuiCol.PopupBg] = Rgb(40, 43, 49);
        c[(int)ImGuiCol.Border] = Rgb(69, 74, 83);
        c[(int)ImGuiCol.BorderShadow] = Rgb(0, 0, 0, 0);
        c[(int)ImGuiCol.FrameBg] = Rgb(44, 48, 55);
        c[(int)ImGuiCol.FrameBgHovered] = Rgb(58, 64, 73);
        c[(int)ImGuiCol.FrameBgActive] = Rgb(71, 78, 89);
        c[(int)ImGuiCol.TitleBg] = Rgb(24, 26, 30);
        c[(int)ImGuiCol.TitleBgActive] = Rgb(44, 48, 55);
        c[(int)ImGuiCol.TitleBgCollapsed] = Rgb(24, 26, 30);
        c[(int)ImGuiCol.MenuBarBg] = Rgb(24, 26, 30);
        c[(int)ImGuiCol.ScrollbarBg] = Rgb(25, 27, 31);
        c[(int)ImGuiCol.ScrollbarGrab] = Rgb(76, 82, 93);
        c[(int)ImGuiCol.ScrollbarGrabHovered] = Rgb(108, 116, 130);
        c[(int)ImGuiCol.ScrollbarGrabActive] = Rgb(153, 162, 178);
        c[(int)ImGuiCol.CheckMark] = Accent;
        c[(int)ImGuiCol.SliderGrab] = Rgb(168, 177, 191);
        c[(int)ImGuiCol.SliderGrabActive] = Accent;
        c[(int)ImGuiCol.Button] = Rgb(49, 53, 61);
        c[(int)ImGuiCol.ButtonHovered] = Rgb(66, 72, 83);
        c[(int)ImGuiCol.ButtonActive] = ButtonActive;
        c[(int)ImGuiCol.Header] = Rgb(66, 73, 85);
        c[(int)ImGuiCol.HeaderHovered] = Rgb(79, 87, 101);
        c[(int)ImGuiCol.HeaderActive] = Rgb(96, 106, 122);
        c[(int)ImGuiCol.Separator] = Rgb(72, 78, 88);
        c[(int)ImGuiCol.SeparatorHovered] = Rgb(148, 160, 180);
        c[(int)ImGuiCol.SeparatorActive] = Accent;
        c[(int)ImGuiCol.ResizeGrip] = Rgb(0, 0, 0, 0);
        c[(int)ImGuiCol.ResizeGripHovered] = White(0.40f);
        c[(int)ImGuiCol.ResizeGripActive] = White(0.70f);
        c[(int)ImGuiCol.Tab] = Rgb(27, 29, 33);
        c[(int)ImGuiCol.TabHovered] = Rgb(62, 68, 78);
        c[(int)ImGuiCol.TabSelected] = Rgb(47, 52, 61);
        c[(int)ImGuiCol.TabSelectedOverline] = Rgb(194, 204, 220);
        c[(int)ImGuiCol.TabDimmed] = Rgb(25, 27, 31);
        c[(int)ImGuiCol.TabDimmedSelected] = Rgb(38, 42, 49);
        c[(int)ImGuiCol.TabDimmedSelectedOverline] = Rgb(107, 117, 133);
        c[(int)ImGuiCol.DockingPreview] = White(0.25f);
        c[(int)ImGuiCol.DockingEmptyBg] = Rgb(22, 24, 28);
        c[(int)ImGuiCol.TableHeaderBg] = Rgb(42, 46, 53);
        c[(int)ImGuiCol.TableBorderStrong] = Rgb(72, 78, 88);
        c[(int)ImGuiCol.TableBorderLight] = Rgb(49, 54, 62);
        c[(int)ImGuiCol.TableRowBg] = Rgb(0, 0, 0, 0);
        c[(int)ImGuiCol.TableRowBgAlt] = White(0.035f);
        c[(int)ImGuiCol.TextSelectedBg] = White(0.25f);
        c[(int)ImGuiCol.NavCursor] = Accent;
        c[(int)ImGuiCol.ModalWindowDimBg] = Rgb(0, 0, 0, 0.6f);
        c[(int)ImGuiCol.PlotHistogram] = White(0.8f);
        c[(int)ImGuiCol.DragDropTarget] = White(0.8f);
    }

    /// <summary>A palette theme: overlays of white (dark themes) or black (light ones), the tint on rows and buttons.</summary>
    private static void Colors(Palette p)
    {
        var lift = p.Light ? new Vector4(0, 0, 0, 1) : new Vector4(1, 1, 1, 1);
        var k = p.Light ? 1.35f : 1f; // overlays read weaker on light backgrounds
        Vector4 L(float a) => Alpha(lift, a * k);
        Vector4 T(float a) => Alpha(p.Tint, a);
        var paper = Rgb(255, 255, 255);

        ButtonActive = T(0.45f);
        var c = ImGui.GetStyle().Colors;
        c[(int)ImGuiCol.Text] = p.Text;
        c[(int)ImGuiCol.TextDisabled] = Alpha(p.TextDim, 0.7f);
        c[(int)ImGuiCol.WindowBg] = Alpha(p.Bg, 0.98f);
        c[(int)ImGuiCol.ChildBg] = Rgb(0, 0, 0, 0);
        c[(int)ImGuiCol.PopupBg] = Alpha(Mix(p.Bg, lift, 0.03f), 0.98f);
        c[(int)ImGuiCol.Border] = L(0.14f);
        c[(int)ImGuiCol.BorderShadow] = Rgb(0, 0, 0, 0);
        c[(int)ImGuiCol.FrameBg] = p.Light ? paper : L(0.07f);
        c[(int)ImGuiCol.FrameBgHovered] = p.Light ? Mix(paper, p.Tint, 0.10f) : L(0.12f);
        c[(int)ImGuiCol.FrameBgActive] = p.Light ? Mix(paper, p.Tint, 0.18f) : L(0.17f);
        c[(int)ImGuiCol.TitleBg] = p.Bar;
        c[(int)ImGuiCol.TitleBgActive] = Mix(p.Bar, p.Tint, p.Light ? 0.18f : 0.22f);
        c[(int)ImGuiCol.TitleBgCollapsed] = p.Bar;
        c[(int)ImGuiCol.MenuBarBg] = p.Bar;
        c[(int)ImGuiCol.ScrollbarBg] = Rgb(0, 0, 0, 0);
        c[(int)ImGuiCol.ScrollbarGrab] = L(0.18f);
        c[(int)ImGuiCol.ScrollbarGrabHovered] = L(0.32f);
        c[(int)ImGuiCol.ScrollbarGrabActive] = Alpha(p.Accent, 0.8f);
        c[(int)ImGuiCol.CheckMark] = p.Accent;
        c[(int)ImGuiCol.SliderGrab] = Alpha(p.Accent, 0.8f);
        c[(int)ImGuiCol.SliderGrabActive] = p.Accent;
        c[(int)ImGuiCol.Button] = L(0.08f);
        c[(int)ImGuiCol.ButtonHovered] = T(0.38f);
        c[(int)ImGuiCol.ButtonActive] = T(0.58f);
        c[(int)ImGuiCol.Header] = T(0.32f);
        c[(int)ImGuiCol.HeaderHovered] = T(0.20f);
        c[(int)ImGuiCol.HeaderActive] = T(0.45f);
        c[(int)ImGuiCol.Separator] = L(0.20f);
        c[(int)ImGuiCol.SeparatorHovered] = Alpha(p.Accent, 0.7f);
        c[(int)ImGuiCol.SeparatorActive] = p.Accent;
        c[(int)ImGuiCol.ResizeGrip] = Rgb(0, 0, 0, 0);
        c[(int)ImGuiCol.ResizeGripHovered] = T(0.45f);
        c[(int)ImGuiCol.ResizeGripActive] = T(0.75f);
        c[(int)ImGuiCol.Tab] = p.Bar;
        c[(int)ImGuiCol.TabHovered] = T(0.35f);
        c[(int)ImGuiCol.TabSelected] = Mix(p.Bg, p.Tint, 0.20f);
        c[(int)ImGuiCol.TabSelectedOverline] = p.Accent;
        c[(int)ImGuiCol.TabDimmed] = p.Bar;
        c[(int)ImGuiCol.TabDimmedSelected] = Mix(p.Bg, p.Tint, 0.10f);
        c[(int)ImGuiCol.TabDimmedSelectedOverline] = Alpha(p.Accent, 0.4f);
        c[(int)ImGuiCol.DockingPreview] = T(0.40f);
        c[(int)ImGuiCol.DockingEmptyBg] = p.Bar;
        c[(int)ImGuiCol.TableHeaderBg] = Mix(p.Bg, p.Tint, 0.12f);
        c[(int)ImGuiCol.TableBorderStrong] = L(0.14f);
        c[(int)ImGuiCol.TableBorderLight] = L(0.08f);
        c[(int)ImGuiCol.TableRowBg] = Rgb(0, 0, 0, 0);
        c[(int)ImGuiCol.TableRowBgAlt] = L(0.025f);
        c[(int)ImGuiCol.TextSelectedBg] = T(0.40f);
        c[(int)ImGuiCol.NavCursor] = p.Accent;
        c[(int)ImGuiCol.ModalWindowDimBg] = Rgb(0, 0, 0, p.Light ? 0.35f : 0.6f);
        c[(int)ImGuiCol.PlotHistogram] = p.Accent;
        c[(int)ImGuiCol.DragDropTarget] = p.Accent;
    }

    /// <summary>Structural surfaces are shared by every palette, not just the default theme.</summary>
    private static void Surfaces(Palette p)
    {
        var white = new Vector4(1, 1, 1, 1);
        var black = new Vector4(0, 0, 0, 1);
        var lift = p.Light ? black : white;
        var c = ImGui.GetStyle().Colors;
        EdgeLight = Mix(p.Bg, white, p.Light ? 0.72f : 0.22f);
        Shadow = Mix(p.Bar, black, p.Light ? 0.22f : 0.48f);
        ControlBorder = Mix(p.Bg, lift, p.Light ? 0.24f : 0.23f);
        ControlSelectedBorder = Mix(p.Bg, p.Accent, p.Light ? 0.46f : 0.40f);
        SurfaceSelected = Mix(p.Bg, p.Accent, p.Light ? 0.14f : 0.19f);
        SurfaceHover = Mix(p.Bg, p.Accent, p.Light ? 0.20f : 0.27f);
        ButtonActive = Mix(p.Bg, p.Accent, p.Light ? 0.25f : 0.33f);
        c[(int)ImGuiCol.WindowBg] = p.Bg;
        c[(int)ImGuiCol.ChildBg] = Mix(p.Bg, p.Bar, 0.40f);
        c[(int)ImGuiCol.PopupBg] = Mix(p.Bg, lift, p.Light ? 0.02f : 0.055f);
        c[(int)ImGuiCol.Border] = Mix(p.Bg, lift, p.Light ? 0.20f : 0.18f);
        c[(int)ImGuiCol.FrameBg] = p.Light ? Mix(p.Bg, white, 0.70f) : Mix(p.Bar, black, 0.10f);
        c[(int)ImGuiCol.FrameBgHovered] = Mix(p.Bg, p.Accent, 0.12f);
        c[(int)ImGuiCol.FrameBgActive] = SurfaceSelected;
        c[(int)ImGuiCol.Button] = Mix(p.Bg, lift, p.Light ? 0.045f : 0.09f);
        c[(int)ImGuiCol.ButtonHovered] = SurfaceHover;
        c[(int)ImGuiCol.ButtonActive] = ButtonActive;
        c[(int)ImGuiCol.TitleBg] = p.Bar;
        c[(int)ImGuiCol.TitleBgActive] = Mix(p.Bg, lift, 0.075f);
        c[(int)ImGuiCol.Tab] = Mix(p.Bar, lift, 0.035f);
        c[(int)ImGuiCol.TabHovered] = SurfaceHover;
        c[(int)ImGuiCol.TabSelected] = Mix(p.Bg, lift, p.Light ? 0.015f : 0.13f);
        c[(int)ImGuiCol.TabSelectedOverline] = p.Accent;
        c[(int)ImGuiCol.TabDimmed] = p.Bar;
        c[(int)ImGuiCol.TabDimmedSelected] = Mix(p.Bg, lift, 0.065f);
        c[(int)ImGuiCol.TabDimmedSelectedOverline] = Mix(p.Bg, p.Accent, 0.50f);
        c[(int)ImGuiCol.TableHeaderBg] = Mix(p.Bg, lift, p.Light ? 0.055f : 0.07f);
    }
}
