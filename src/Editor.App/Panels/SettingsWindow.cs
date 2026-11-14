using Hexa.NET.ImGui;
using StbEditor.App.Ui;
using static StbEditor.App.Ui.Loc;

namespace StbEditor.App.Panels;

/// <summary>View → Settings: camera, behaviour and display preferences (saved in settings.json on exit).</summary>
internal sealed class SettingsWindow
{
    private bool _open;
    private int _uiScale = 100;
    private bool _draggingScale;

    public void Show() => _open = true;

    public void Draw(EditorApp app)
    {
        if (!_open) return;
        var settings = app.Settings;
        var work = ImGui.GetMainViewport().WorkSize;
        var size = app.Scaled(600, 860);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(Math.Min(size.X, work.X), Math.Min(size.Y, work.Y * 0.9f)), ImGuiCond.Appearing);
        if (Studio.BeginWindow(Title("Settings"), ref _open, ImGuiWindowFlags.NoDocking))
        {
            ImGui.SeparatorText(T("Camera"));
            var zoom = settings.WheelZoomSpeed;
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 12);
            if (ImGui.SliderFloat(T("Wheel zoom speed"), ref zoom, 0.1f, 5f, "x %.2f", ImGuiSliderFlags.Logarithmic)) settings.WheelZoomSpeed = zoom;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("How far one wheel notch moves the camera (Shift x4, Ctrl x0.25)"));
            var smooth = settings.SmoothZoom;
            if (ImGui.Checkbox(T("Smooth wheel zoom"), ref smooth)) settings.SmoothZoom = smooth;
            var fly = app.RenderWindow.FlySpeed;
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 12);
            if (ImGui.SliderFloat(T("Flying speed"), ref fly, 50f, 20000f, "%.0f", ImGuiSliderFlags.Logarithmic)) app.RenderWindow.FlySpeed = fly;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("RMB + W A S D; the wheel while holding RMB changes it too"));

            ImGui.SeparatorText(T("Render Window"));
            var doors = settings.ConfirmDoorTravel;
            if (ImGui.Checkbox(T("Ask before going through a door"), ref doors)) settings.ConfirmDoorTravel = doors;
            var actors = settings.HighlightActors;
            if (ImGui.Checkbox(T("Tint NPCs and creatures so they are easy to find"), ref actors)) app.RenderWindow.SetHighlightActors(app, actors);

            ImGui.SeparatorText(T("AI / MCP"));
            AiWindow.DrawSwitches(app);
            if (ImGui.SmallButton(T("AI / MCP window..."))) app.AiWindow.Show();

            ImGui.SeparatorText(T("Performance"));
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 12);
            var limits = new[] { 30, 60, 120, 0 };
            var labels = new[] { "30 FPS", "60 FPS", "120 FPS", T("Monitor refresh (vsync)") };
            var index = Math.Max(0, Array.IndexOf(limits, settings.FpsLimit));
            if (ImGui.Combo(T("Frame limit"), ref index, labels, labels.Length)) settings.FpsLimit = limits[index];

            ImGui.SeparatorText(T("Interface"));
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 12);
            var language = Loc.Russian ? 1 : 0;
            if (ImGui.Combo("Language / Язык", ref language, ["English", "Русский"], 2)) app.SetLanguage(language == 1 ? "ru" : "en");

            var currentFont = UiFonts.Resolve(settings.UiFontFamily, settings.UiFontWeight);
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 16);
            if (ImGui.BeginCombo(T("Interface font") + "###uifontfamily", currentFont is null ? T("Default") : T(currentFont.Family)))
            {
                foreach (var family in UiFonts.Families)
                    if (ImGui.Selectable(T(family), family == currentFont?.Family))
                        app.SetInterfaceFont(family, settings.UiFontWeight, settings.GameFontHeadings);
                ImGui.EndCombo();
            }
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 16);
            if (ImGui.BeginCombo(T("Font weight") + "###uifontweight", T(currentFont?.Weight ?? "Regular")))
            {
                foreach (var face in UiFonts.Faces)
                    if (face.Family == currentFont?.Family && ImGui.Selectable(T(face.Weight), face.Weight == currentFont?.Weight))
                        app.SetInterfaceFont(face.Family, face.Weight, settings.GameFontHeadings);
                ImGui.EndCombo();
            }
            var headingSource = app.Ui.GameHeaderFontSource;
            var gameHeadings = settings.GameFontHeadings && headingSource is not null;
            ImGui.BeginDisabled(headingSource is null);
            if (ImGui.Checkbox(T("Separate font for record headings") + "###uigameheadings", ref gameHeadings))
                app.SetInterfaceFont(settings.UiFontFamily, settings.UiFontWeight, gameHeadings);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(T("Only record EditorIDs and section titles. Window tabs keep the interface font."));
            if (headingSource is null)
                ImGui.TextWrapped(T("No separate record heading font found; headings use the interface font."));
            else
            {
                ImGui.TextColored(Theme.TextDim, TF($"Available heading font: {Path.GetFileName(headingSource)}"));
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(headingSource);
            }
            ImGui.PushFont(app.Ui.HeaderFont, 0);
            ImGui.TextWrapped(T("Record heading preview: ABC abc"));
            ImGui.PopFont();
            ImGui.TextWrapped(T("Font preview: ABC abc 0123456789"));
            ImGui.TextColored(Theme.TextDim, T("Font changes apply immediately. Code and logs keep their monospace font."));

            // Interface size: applied when the slider is let go (live resizing would move the slider under the mouse).
            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 12);
            if (!_draggingScale) _uiScale = (int)MathF.Round(Theme.UiScale * 100);
            ImGui.SliderInt(T("Interface size"), ref _uiScale, (int)(Theme.MinUiScale * 100), (int)(Theme.MaxUiScale * 100), "%d %%");
            _draggingScale = ImGui.IsItemActive();
            if (ImGui.IsItemDeactivatedAfterEdit()) app.SetUiScale(_uiScale / 100f);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Text and spacing of the whole interface. Ctrl+= / Ctrl+- / Ctrl+0 anywhere (100 %)"));
            foreach (var preset in new[] { 100, 115, 125, 150 })
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"{preset}%##ui{preset}")) app.SetUiScale(preset / 100f);
            }

            ImGui.SetNextItemWidth(ImGui.GetFontSize() * 12);
            if (ImGui.BeginCombo(T("Theme"), T(Theme.Current)))
            {
                foreach (var palette in Theme.Palettes)
                {
                    // Each entry in its own colours: a swatch of the background and the accent.
                    var draw = ImGui.GetWindowDrawList();
                    var at = ImGui.GetCursorScreenPos();
                    var h = ImGui.GetTextLineHeight();
                    draw.AddRectFilled(at, at + new System.Numerics.Vector2(h * 1.4f, h), ImGui.GetColorU32(palette.Bg with { W = 1 }));
                    draw.AddRectFilled(at + new System.Numerics.Vector2(h * 0.9f, h * 0.2f), at + new System.Numerics.Vector2(h * 1.3f, h * 0.8f), ImGui.GetColorU32(palette.Accent));
                    draw.AddRect(at, at + new System.Numerics.Vector2(h * 1.4f, h), ImGui.GetColorU32(ImGuiCol.Border));
                    ImGui.Indent(h * 1.8f);
                    if (ImGui.Selectable(T(palette.Name), palette.Name == Theme.Current))
                    {
                        Theme.Use(palette.Name);
                        settings.Theme = palette.Name;
                    }
                    ImGui.Unindent(h * 1.8f);
                }
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Colours of the whole interface (as xEdit's styles)"));

            ImGui.SeparatorText(T("Records"));
            var header = settings.ShowRecordHeader;
            if (ImGui.Checkbox(T("Show record header fields"), ref header)) settings.ShowRecordHeader = header;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Form version, version control, raw flags, compression: bookkeeping the editor writes itself"));
            var onlyDiff = settings.CompareOnlyDifferences;
            if (ImGui.Checkbox(T("Comparison: only differences"), ref onlyDiff)) settings.CompareOnlyDifferences = onlyDiff;

            ImGui.SeparatorText(T("Papyrus"));
            var compiler = settings.PapyrusCompiler ?? string.Empty;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##papyrus", T("PapyrusCompiler.exe (empty: the game's Papyrus Compiler folder)"), ref compiler, 512))
                settings.PapyrusCompiler = compiler.Trim().Trim('"') is { Length: > 0 } path ? path : null;
            if (app.Papyrus is { } papyrus)
            {
                ImGui.TextColored(papyrus.Problem is null ? Theme.TextDim : Theme.Warning,
                    papyrus.Problem ?? TF($"Compiler: {papyrus.Compiler}; {papyrus.SourceFolders.Count} script source folder(s)"));
            }

            ImGui.Spacing();
            ImGui.TextColored(Theme.TextDim, T("Saved when the editor closes."));
        }
        ImGui.End();
    }
}
