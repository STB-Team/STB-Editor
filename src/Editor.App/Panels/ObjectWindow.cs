using System.Numerics;
using Hexa.NET.ImGui;
using StbEditor.App.Ui;
using StbEditor.Core;
using StbEditor.Data.Catalog;
using static StbEditor.App.Ui.Loc;

namespace StbEditor.App.Panels;

/// <summary>
/// The CK Object Window: record types by category on the left, a virtualised record table on the
/// right. The filter matches EditorID, name or FormID across one type or across everything.
/// </summary>
internal sealed class ObjectWindow : Panel
{
    private const string AllTypes = "*";

    private string _type = "STAT";
    private string _filter = string.Empty;
    private bool _focusSearch;
    private enum Show { All, Overridden, Conflicts }

    private Show _show;

    /// <summary>Only records this plugin defines or overrides (null = any).</summary>
    private string? _plugin;
    private string _pluginFilter = string.Empty;
    private readonly ConflictScan _scan = new();

    /// <summary>ModGroups changed: conflicts are found again.</summary>
    public void ResetConflicts() => _scan.Reset();
    private int _scanVersion = -1;

    /// <summary>The conflict filter is still checking records (keeps frames coming).</summary>
    public bool IsScanning => _show == Show.Conflicts && _scan.IsRunning;

    /// <summary>Filters as the toolbar sets them (autotests).</summary>
    public void SetFiltersForTest(string? plugin, bool conflicts)
    {
        _plugin = plugin;
        _show = conflicts ? Show.Conflicts : Show.All;
        _dirty = true;
    }

    public int RowCount => _rows.Count;
    public IEnumerable<Mutagen.Bethesda.Plugins.FormKey> VisibleKeys => _rows.Select(r => r.FormKey);
    public int ConflictsChecked => _scan.Checked;
    private List<CatalogRecord> _rows = [];
    private bool _dirty = true;
    private int _catalogVersion = -1;
    private int _sortColumn;
    private bool _sortAscending = true;
    private int _scrolledToSelection = -1;
    private Dictionary<string, List<RecordTypeInfo>> _categories = new();
    private bool _tiles = true;
    private bool _showTypes = true;

    /// <summary>Records picked with Ctrl/Shift+click (for Compare); the anchor is where a Shift range starts.</summary>
    private readonly HashSet<Mutagen.Bethesda.Plugins.FormKey> _picked = [];
    private int _anchor = -1;

    /// <summary>The picked records in list order (autotests use it as well).</summary>
    public List<Mutagen.Bethesda.Plugins.FormKey> Picked => _rows.Where(r => _picked.Contains(r.FormKey)).Select(r => r.FormKey).ToList();

    /// <summary>Picks records as Ctrl+click would (autotests).</summary>
    public void Pick(IEnumerable<Mutagen.Bethesda.Plugins.FormKey> keys)
    {
        _picked.Clear();
        _picked.UnionWith(keys);
    }

    public override string Name => "Object Window";

    public void SetFilter(string text)
    {
        _filter = text;
        _type = AllTypes;
        _dirty = true;
    }

    /// <summary>Shows one record type (e.g. "STAT"), keeping the filter.</summary>
    public void SetType(string signature)
    {
        _type = signature;
        _dirty = true;
    }

    public void FocusSearch()
    {
        _focusSearch = true;
        ImGui.SetWindowFocus(Title(Name));
    }

    public override void OnSessionChanged(EditorApp app)
    {
        var catalog = app.Session!.Catalog;
        _categories = catalog.BySignature.Keys
            .Select(RecordTypes.Get)
            .Where(t => t.Signature != "CELL")
            .GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Name).ToList());
        _scan.Reset();
        if (_plugin is not null && !app.Session.Workspace.Active.Any(p => p.Name.Equals(_plugin, StringComparison.OrdinalIgnoreCase))) _plugin = null;
        _dirty = true;
    }

    protected override void DrawContent(EditorApp app)
    {
        if (!RequireSession(app)) return;
        var session = app.Session!;

        var em = ImGui.GetFontSize();
        var width = ImGui.GetContentRegionAvail().X;
        var compact = width < em * 32;
        var toolbarRight = ImGui.GetCursorScreenPos().X + width;
        DrawSearchToolbar(session.Catalog, compact, width);
        DrawFilterToolbar(app, compact, toolbarRight);

        if (app.Session!.Catalog.Version != _catalogVersion)
        {
            _catalogVersion = app.Session.Catalog.Version;
            _dirty = true;
        }
        if (_dirty) Rebuild(app);

        if (_show == Show.Conflicts)
        {
            _scan.Step(app);
            if (_scan.Version != _scanVersion)
            {
                _scanVersion = _scan.Version;
                Rebuild(app);
            }
        }

        var count = $"{(_type == AllTypes ? T("All records") : _type)} · {_rows.Count:N0}" + (_show == Show.Conflicts && _scan.IsRunning ? " …" : "");
        ContinueToolbar(toolbarRight, ImGui.CalcTextSize(count).X);
        ImGui.TextColored(_show == Show.Conflicts && _scan.IsRunning ? Theme.Warning : Theme.TextDim, count);
        if (_show == Show.Conflicts && _scan.IsRunning && ImGui.IsItemHovered())
            ImGui.SetTooltip(TF($"checking: {_scan.Checked:N0} done, {_scan.Pending:N0} left"));
        ImGui.Separator();

        if (!compact && _showTypes)
        {
            var leftWidth = Math.Min(em * 12, width * 0.3f);
            ImGui.BeginChild("##types", new Vector2(leftWidth, 0), ImGuiChildFlags.ResizeX | ImGuiChildFlags.Borders);
            DrawTypeTree(session.Catalog);
            ImGui.EndChild();
            ImGui.SameLine();
        }
        ImGui.BeginChild("##records", Vector2.Zero);

        // Ctrl+F4 (as in the CK): FaceGen for the selected NPCs.
        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows) && !ImGui.GetIO().WantTextInput && ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.F4, false))
        {
            var npcs = (_picked.Count > 1 ? Picked : app.Selected is { } one ? [one] : [])
                .Where(k => app.Session!.Catalog.Get(k)?.Signature == "NPC_").ToList();
            if (npcs.Count > 0) ExportFaceGen(app, npcs);
        }

        // Delete in the record list: take the selected records out of the active file (Ctrl+Z brings them back).
        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows) && !ImGui.GetIO().WantTextInput && ImGui.IsKeyPressed(ImGuiKey.Delete, false))
        {
            foreach (var key in _picked.Count > 1 ? Picked : app.Selected is { } one ? [one] : [])
                RecordActions.Remove(app, key);
        }

        var tileable = _type != AllTypes && RecordTypes.IsPlaceable(_type);
        if (_tiles && tileable && app.Thumbnails is not null) DrawTiles(app);
        else DrawTable(app);
        ImGui.EndChild();
    }

    private void DrawSearchToolbar(RecordCatalog catalog, bool compact, float width)
    {
        var em = ImGui.GetFontSize();
        if (ImGui.BeginTable("##catalogSearch", 3, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("##typesControl", ImGuiTableColumnFlags.WidthFixed, Math.Min(em * 5, width * 0.24f));
            ImGui.TableSetupColumn("##searchControl", ImGuiTableColumnFlags.WidthStretch);
            var viewLabel = _tiles ? T("List") : T("Tiles");
            ImGui.TableSetupColumn("##viewControl", ImGuiTableColumnFlags.WidthFixed, Math.Min(ImGui.CalcTextSize(viewLabel).X + ImGui.GetStyle().FramePadding.X * 2, width * 0.24f));
            ImGui.TableNextColumn();
            var typesExpanded = !compact && _showTypes;
            if (Studio.Button(T("Types") + "###typesToggle", new Vector2(-1, 0), selected: typesExpanded))
            {
                if (compact) ImGui.OpenPopup("##typePicker");
                else _showTypes = !_showTypes;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Choose record types; click to show or hide the type tree"));
            ImGui.SetNextWindowSize(new Vector2(Math.Min(width, em * 26), Math.Min(em * 24, ImGui.GetMainViewport().WorkSize.Y * 0.75f)), ImGuiCond.Appearing);
            if (ImGui.BeginPopup("##typePicker"))
            {
                ImGui.BeginChild("##typeChoices", Vector2.Zero);
                DrawTypeTree(catalog);
                ImGui.EndChild();
                ImGui.EndPopup();
            }
            ImGui.TableNextColumn();
            if (_focusSearch)
            {
                ImGui.SetKeyboardFocusHere();
                _focusSearch = false;
            }
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##filter", T("Filter: EditorID, name or FormID   (Ctrl+F)"), ref _filter, 256)) _dirty = true;
            ImGui.TableNextColumn();
            if (Studio.Button(viewLabel + "###catalogView", new Vector2(-1, 0))) _tiles = !_tiles;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Thumbnails of placeable objects, or the record table"));
            ImGui.EndTable();
        }
    }

    private void DrawFilterToolbar(EditorApp app, bool compact, float right)
    {
        var em = ImGui.GetFontSize();
        if (compact)
        {
            if (Studio.Button(T("Filters") + (_plugin is not null || _show != Show.All ? " *" : "") + "###catalogFilters"))
                ImGui.OpenPopup("##catalogFiltersPopup");
            ImGui.SetNextWindowSize(new Vector2(Math.Min(ImGui.GetWindowWidth() - em * 2, em * 28), 0), ImGuiCond.Appearing);
            if (ImGui.BeginPopup("##catalogFiltersPopup"))
            {
                DrawRecordScope(ImGui.GetContentRegionAvail().X);
                DrawPluginFilter(app, ImGui.GetContentRegionAvail().X);
                if (_show == Show.Conflicts && _scan.IsRunning)
                    ImGui.TextWrapped(TF($"checking: {_scan.Checked:N0} done, {_scan.Pending:N0} left"));
                ImGui.EndPopup();
            }
        }
        else
        {
            var reserve = em * 7;
            if (SpreadsheetWindow.Has(_type)) reserve += ImGui.CalcTextSize(T("Table")).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().ItemSpacing.X;
            if (_type != AllTypes && NewFormDialog.CanCreate(_type)) reserve += ImGui.CalcTextSize(T("+ New")).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().ItemSpacing.X;
            DrawRecordScope(em * 9);
            ImGui.SameLine();
            DrawPluginFilter(app, Math.Max(em * 8, ImGui.GetContentRegionAvail().X - reserve));
        }
        if (SpreadsheetWindow.Has(_type))
        {
            ContinueToolbar(right, ImGui.CalcTextSize(T("Table")).X + ImGui.GetStyle().FramePadding.X * 2);
            if (Studio.Button(T("Table"))) app.Spreadsheets.Show(_type);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("All records of this type as a sortable table, edited in place"));
        }
        if (_type != AllTypes && NewFormDialog.CanCreate(_type))
        {
            ContinueToolbar(right, ImGui.CalcTextSize(T("+ New")).X + ImGui.GetStyle().FramePadding.X * 2);
            if (Studio.Button(T("+ New"), primary: true)) app.NewForm.Show(_type);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(TF($"A new {T(RecordTypes.Get(_type).Name)} in the active file (as the CK's New)"));
        }
    }

    private void DrawRecordScope(float width)
    {
        ImGui.SetNextItemWidth(width);
        string[] shows = [T("All records"), T("Overridden"), T("Conflicts")];
        var show = (int)_show;
        if (ImGui.Combo("##show", ref show, shows, shows.Length)) { _show = (Show)show; _dirty = true; }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(T("Overridden: more than one plugin has the record. Conflicts: some plugin's change is lost to the winner (as xEdit's conflict filter; checked field by field)"));
    }

    private static void ContinueToolbar(float right, float width)
    {
        if (ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + width <= right)
            ImGui.SameLine();
    }


    /// <summary>Plugin combo: any, the active file, or one plugin of the load order (with a filter box).</summary>
    private void DrawPluginFilter(EditorApp app, float width)
    {
        ImGui.SetNextItemWidth(width);
        if (!ImGui.BeginCombo("##plugin", _plugin ?? T("Any plugin"), ImGuiComboFlags.HeightLarge))
        {
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Only records that this plugin adds or overrides (xEdit: the plugin's branch)"));
            return;
        }
        if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##pf", T("Filter plugins"), ref _pluginFilter, 128);
        if (ImGui.Selectable(T("Any plugin"), _plugin is null)) { _plugin = null; _dirty = true; }
        if (app.ActivePlugin is { } active && ImGui.Selectable(TF($"{active.Name}  (active file)"), _plugin == active.Name)) { _plugin = active.Name; _dirty = true; }
        ImGui.Separator();
        foreach (var plugin in app.Session!.Workspace.Active)
        {
            if (plugin.Mod is null) continue;
            if (_pluginFilter.Length > 0 && !plugin.Name.Contains(_pluginFilter, StringComparison.OrdinalIgnoreCase)) continue;
            if (ImGui.Selectable($"{plugin.IndexLabel,-6} {plugin.Name}", string.Equals(_plugin, plugin.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _plugin = plugin.Name;
                _dirty = true;
            }
        }
        ImGui.EndCombo();
    }

    /// <summary>FaceGen for these NPCs; the outcome goes to the log and the status line.</summary>
    public static void ExportFaceGen(EditorApp app, IReadOnlyCollection<Mutagen.Bethesda.Plugins.FormKey> npcs)
    {
        var outcome = Services.FaceGenExports.Export(app, npcs);
        foreach (var problem in outcome.Problems) Services.Log.Warn("FaceGen: " + problem);
        if (outcome.Exported > 0) StatusBar.Notice(TF($"FaceGen exported for {outcome.Exported} NPC(s)") + (outcome.Problems.Count > 0 ? TF($", {outcome.Problems.Count} note(s) in the log") : ""));
    }

    private void DrawTypeTree(RecordCatalog catalog)
    {
        if (ImGui.Selectable(TF($"All types ({catalog.ByKey.Count:N0})"), _type == AllTypes))
        {
            _type = AllTypes;
            _dirty = true;
        }

        foreach (var category in RecordTypes.CategoryOrder)
        {
            if (!_categories.TryGetValue(category, out var types)) continue;

            var defaultOpen = category is "World Objects" ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            if (!ImGui.TreeNodeEx(T(category) + "##" + category, defaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth)) continue;

            foreach (var type in types)
            {
                var count = catalog.BySignature.TryGetValue(type.Signature, out var list) ? list.Count : 0;
                var countText = count.ToString("N0");
                var name = T(type.Name);
                var start = ImGui.GetCursorScreenPos();
                var width = ImGui.GetContentRegionAvail().X;
                if (ImGui.Selectable("##" + type.Signature, _type == type.Signature, ImGuiSelectableFlags.None, new Vector2(0, ImGui.GetTextLineHeight())))
                {
                    _type = type.Signature;
                    _dirty = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{name}\n{type.Signature} · {countText}");
                var countWidth = ImGui.CalcTextSize(countText).X;
                var labelWidth = width - countWidth - ImGui.GetStyle().ItemInnerSpacing.X;
                if (labelWidth > 0) DrawClippedLabel(name, start, labelWidth, Theme.Text);
                DrawClippedLabel(countText, start + new Vector2(Math.Max(0, width - countWidth), 0), Math.Min(width, countWidth), Theme.TextDim);
            }

            ImGui.TreePop();
        }
    }

    private void Rebuild(EditorApp app)
    {
        _dirty = false;
        var session = app.Session!;
        var catalog = session.Catalog;

        IEnumerable<CatalogRecord> source = _type == AllTypes
            ? catalog.ByKey.Values.Where(r => r.Signature != "CELL")
            : catalog.BySignature.GetValueOrDefault(_type) ?? [];

        if (_plugin is { } plugin) source = source.Where(r => r.Plugins.Any(p => p.Name.Equals(plugin, StringComparison.OrdinalIgnoreCase)));
        if (_show == Show.Overridden) source = source.Where(r => r.IsOverridden);
        if (_show == Show.Conflicts)
        {
            var candidates = source.Where(r => r.Plugins.Count >= 3).ToList();
            _scan.Request(candidates);
            source = candidates.Where(r => _scan.IsConflict(r.FormKey) == true);
        }

        var filter = _filter.Trim();
        if (filter.Length > 0)
        {
            var formKey = session.Workspace.ParseFormReference(filter);
            source = source.Where(r =>
                (formKey is { } k && r.FormKey == k)
                || (r.EditorId?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        _rows = source.ToList();
        Sort(app);
    }

    private void Sort(EditorApp app)
    {
        var ws = app.Session!.Workspace;
        Comparison<CatalogRecord> compare = _sortColumn switch
        {
            1 => (a, b) => (ws.ToFormId(a.FormKey) ?? 0).CompareTo(ws.ToFormId(b.FormKey) ?? 0),
            2 => (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase),
            3 => (a, b) => string.CompareOrdinal(a.Signature, b.Signature),
            4 => (a, b) => a.Plugins.Count.CompareTo(b.Plugins.Count),
            5 => (a, b) => a.WinningPlugin.LoadIndex.CompareTo(b.WinningPlugin.LoadIndex),
            _ => (a, b) => string.Compare(a.EditorId, b.EditorId, StringComparison.OrdinalIgnoreCase),
        };

        _rows.Sort(_sortAscending ? compare : (a, b) => compare(b, a));
    }

    /// <summary>Grid of rendered thumbnails; click selects, drag places into the Render Window.</summary>
    private unsafe void DrawTiles(EditorApp app)
    {
        if (!ImGui.BeginChild("##tiles", Vector2.Zero)) { ImGui.EndChild(); return; }
        var em = ImGui.GetFontSize();
        var style = ImGui.GetStyle();
        var padding = style.FramePadding.X;
        var line = ImGui.GetTextLineHeight();
        var avail = ImGui.GetContentRegionAvail();
        // The world workspace is a shallow shelf; put labels beside, rather than below, its images.
        var horizontal = avail.Y < em * 10 && avail.X > em * 12;
        var imageSize = horizontal ? Math.Clamp(avail.Y - padding * 2, em * 3, em * 5) : em * 6;
        var preferredWidth = em * (horizontal ? 17 : 12);
        var columns = Math.Max(1, (int)((avail.X + style.ItemSpacing.X) / (preferredWidth + style.ItemSpacing.X)));
        var cardWidth = Math.Max(1, (avail.X - style.ItemSpacing.X * (columns - 1)) / columns);
        var cardHeight = horizontal ? Math.Max(imageSize, line * 3) + padding * 2 : imageSize + line * 2 + padding * 3;
        var rowHeight = cardHeight + style.ItemSpacing.Y;
        var rows = (_rows.Count + columns - 1) / columns;
        var ws = app.Session!.Workspace;
        var draw = ImGui.GetWindowDrawList();

        var scrollTo = -1;
        if (_scrolledToSelection != app.SelectionVersion && app.Selected is { } selectedKey)
        {
            _scrolledToSelection = app.SelectionVersion;
            scrollTo = _rows.FindIndex(r => r.FormKey == selectedKey);
        }
        if (app.Selected is { } current && !_picked.Contains(current)) _picked.Clear();

        var clipper = new ImGuiListClipper();
        clipper.Begin(rows, rowHeight);
        if (scrollTo >= 0) clipper.IncludeItemByIndex(scrollTo / columns);
        while (clipper.Step())
        {
            for (var row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
            {
                for (var c = 0; c < columns; c++)
                {
                    var i = row * columns + c;
                    if (i >= _rows.Count) break;
                    var record = _rows[i];
                    var formId = ws.FormIdText(record.FormKey);
                    if (c > 0) ImGui.SameLine();
                    ImGui.PushID(i);
                    var start = ImGui.GetCursorScreenPos();
                    var size = new Vector2(cardWidth, cardHeight);
                    var selected = app.Selected == record.FormKey || _picked.Contains(record.FormKey);
                    var removed = app.Document.IsRemoved(record.FormKey);
                    draw.AddRectFilled(start + new Vector2(0, em / 8), start + size + new Vector2(0, em / 8),
                        ImGui.GetColorU32(Theme.Shadow), style.FrameRounding);
                    draw.AddRectFilled(start, start + size, ImGui.GetColorU32(ImGuiCol.Button), style.FrameRounding);
                    draw.AddLine(start + new Vector2(style.FrameRounding, 1), start + new Vector2(size.X - style.FrameRounding, 1),
                        ImGui.GetColorU32(Theme.EdgeLight));
                    if (ImGui.Selectable("##t", selected, ImGuiSelectableFlags.AllowDoubleClick, size))
                    {
                        if (Pick(app, i)) app.Select(record.FormKey);
                        else if (_picked.Count > 0) app.Select(_picked.First());
                        _scrolledToSelection = app.SelectionVersion;
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) app.FormWindows.Open(record.FormKey);
                    }
                    if (i == scrollTo) ImGui.SetScrollHereY(0.4f);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"{record.EditorId}\n{record.Name}\n{formId} · {record.WinningPlugin.Name}"
                            + (removed ? "\n" + T("Taken out of the active file when you save; Ctrl+Z brings it back") : ""));

                    var imageStart = start + new Vector2(padding);
                    var imageExtent = Math.Min(imageSize, cardWidth - padding * 2);
                    draw.AddRectFilled(imageStart - Vector2.One, imageStart + new Vector2(imageExtent + 1),
                        ImGui.GetColorU32(ImGuiCol.WindowBg), style.FrameRounding);
                    var texture = app.Thumbnails!.Get(record.FormKey);
                    if (texture != 0 && imageExtent > 0)
                        draw.AddImage(new ImTextureRef(null, new ImTextureID((ulong)texture)), imageStart, imageStart + new Vector2(imageExtent));
                    else
                        draw.AddText(imageStart + new Vector2(padding), ImGui.GetColorU32(Theme.TextDim), "…");

                    var textStart = horizontal
                        ? imageStart + new Vector2(imageExtent + padding, Math.Max(0, (imageSize - line * 3) / 2))
                        : imageStart + new Vector2(0, imageSize + padding);
                    var textWidth = Math.Max(1, start.X + cardWidth - padding - textStart.X);
                    DrawClippedLabel(record.EditorId ?? T("<no editor id>"), textStart, textWidth, removed ? Theme.Error : Theme.Text);
                    var detail = record.Name is { Length: > 0 } name ? name : horizontal ? record.WinningPlugin.Name : formId;
                    DrawClippedLabel(detail, textStart + new Vector2(0, line), textWidth, Theme.TextDim);
                    if (horizontal)
                        DrawClippedLabel(formId, textStart + new Vector2(0, line * 2), textWidth, Theme.TextDim);
                    draw.AddRect(start, start + size, ImGui.GetColorU32(selected ? Theme.Accent : style.Colors[(int)ImGuiCol.Border]),
                        style.FrameRounding, 0, Math.Max(1, em / 16) * (selected ? 2 : 1));

                    DrawRecordContext(app, record);
                    if (ImGui.BeginDragDropSource())
                    {
                        FormDrag.Current = record.FormKey;
                        ImGui.SetDragDropPayload(FormDrag.PayloadType, null, 0);
                        ImGui.Text(TF($"Place {record.EditorId}"));
                        ImGui.EndDragDropSource();
                    }
                    ImGui.PopID();
                }
            }
        }
        clipper.End();
        ImGui.EndChild();
    }

    /// <summary>Clip long identifiers without repeatedly copying progressively shorter strings.</summary>
    private static void DrawClippedLabel(string text, Vector2 position, float width, Vector4 color)
    {
        var draw = ImGui.GetWindowDrawList();
        var clipped = ImGui.CalcTextSize(text).X > width;
        var dots = clipped ? ImGui.CalcTextSize("…").X : 0;
        draw.PushClipRect(position, position + new Vector2(Math.Max(0, width - dots), ImGui.GetTextLineHeight()), true);
        draw.AddText(position, ImGui.GetColorU32(color), text);
        draw.PopClipRect();
        if (clipped)
        {
            draw.PushClipRect(position, position + new Vector2(width, ImGui.GetTextLineHeight()), true);
            draw.AddText(position + new Vector2(Math.Max(0, width - dots), 0), ImGui.GetColorU32(color), "…");
            draw.PopClipRect();
        }
    }

    /// <summary>Click: just this record. Ctrl+click: add or drop it. Shift+click: the range from the last click.</summary>
    /// <returns>False when Ctrl+click dropped the record from the picked set.</returns>
    private bool Pick(EditorApp app, int index)
    {
        var io = ImGui.GetIO();
        var key = _rows[index].FormKey;
        if (io.KeyCtrl)
        {
            if (_picked.Count == 0 && app.Selected is { } current) _picked.Add(current);
            _anchor = index;
            if (_picked.Add(key)) return true;
            _picked.Remove(key);
            return _picked.Count == 0;
        }
        else if (io.KeyShift && _anchor >= 0 && _anchor < _rows.Count)
        {
            _picked.Clear();
            var (from, to) = (Math.Min(_anchor, index), Math.Max(_anchor, index));
            for (var j = from; j <= to && _picked.Count < 500; j++) _picked.Add(_rows[j].FormKey);
        }
        else
        {
            _picked.Clear();
            _picked.Add(key);
            _anchor = index;
        }
        return true;
    }

    private void DrawRecordContext(EditorApp app, CatalogRecord record)
    {
        if (!ImGui.BeginPopupContextItem("##ctx")) return;
        var ws = app.Session!.Workspace;
        if (NewFormDialog.CanCreate(record.Signature) && ImGui.MenuItem(TF($"New {T(RecordTypes.Get(record.Signature).Name)}...")))
            app.NewForm.Show(record.Signature);
        var removal = RecordActions.RemovalOf(app, record.FormKey);
        if (ImGui.MenuItem(RecordActions.Label(app, removal), "Del", false, removal != RecordActions.Removal.None))
            RecordActions.Remove(app, record.FormKey);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(RecordActions.Tooltip(app, removal, record.FormKey));
        ImGui.Separator();
        if (_picked.Count > 1 && _picked.Contains(record.FormKey))
        {
            if (ImGui.MenuItem(TF($"Compare selected ({_picked.Count})"))) app.Compare.OpenRecords(Picked);
            if (_picked.Count > CompareWindows.MaxColumns && ImGui.IsItemHovered())
                ImGui.SetTooltip(TF($"The first {CompareWindows.MaxColumns} are compared"));
        }
        else if (app.Selected is { } other && other != record.FormKey && ImGui.MenuItem(TF($"Compare with {app.Session!.Catalog.Get(other)?.EditorId ?? ws.FormIdText(other)}")))
        {
            app.Compare.OpenRecords([other, record.FormKey]);
        }
        if (ImGui.MenuItem(T("Use Info"))) app.UseInfo.Open(record.FormKey);
        var own = app.ActivePlugin is { IsNew: false } activePlugin && record.FormKey.ModKey.FileName.String.Equals(activePlugin.Name, StringComparison.OrdinalIgnoreCase);
        if (ImGui.MenuItem(T("Change FormID..."), (string?)null, false, own)) app.Compact.ShowChange(app, record.FormKey);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(own ? T("Another number for this record of the active file; links inside the file follow (xEdit: Change FormID)") : T("Only the saved active file's own records can be renumbered"));
        if (_picked.Count > 1 && _picked.Contains(record.FormKey))
        {
            var together = Picked;
            var sameType = together.All(k => app.Session!.Catalog.Get(k)?.Signature == record.Signature);
            if (ImGui.MenuItem(TF($"Edit together ({together.Count})"), (string?)null, false, sameType)) app.FormWindows.OpenBatch(together);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(sameType ? T("One form: a field you change is set on every selected record (xEdit: Copy to selected records)") : T("Only records of one type can be edited together"));
        }
        var copyKeys = _picked.Count > 1 && _picked.Contains(record.FormKey) ? Picked : [record.FormKey];
        var copyable = RecordActions.CopyableAsOverride(app, copyKeys).Count;
        if (ImGui.MenuItem(app.ActivePlugin is { } activeFile ? TF($"Copy as override into {activeFile.Name} ({copyable})") : T("Copy as override into the active file"), (string?)null, false, copyable > 0))
            RecordActions.CopyAsOverride(app, copyKeys);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(T("The winning version goes into the active file unchanged (xEdit: Copy as override). Already there, cells, quests and dialogue are skipped."));
        if (ImGui.MenuItem(TF($"Compare versions ({record.Plugins.Count})"), (string?)null, false, record.Plugins.Count > 1))
            app.Compare.OpenVersions(record.FormKey);
        if (record.Signature == "NPC_")
        {
            var npcs = copyKeys.Where(k => app.Session!.Catalog.Get(k)?.Signature == "NPC_").ToList();
            if (ImGui.MenuItem(TF($"Export FaceGen ({npcs.Count})"), "Ctrl+F4")) ExportFaceGen(app, npcs);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(T("The NPC's head (mesh with its face morphs, hair, eyes…) and tint texture, written next to the active file (CK: Ctrl+F4). Without it the face is dark in game."));
        }
        ImGui.Separator();
        if (ImGui.MenuItem(T("Copy Form ID"))) ImGui.SetClipboardText(ws.FormIdText(record.FormKey));
        if (ImGui.MenuItem(T("Copy Editor ID"), (string?)null, false, record.EditorId is not null)) ImGui.SetClipboardText(record.EditorId!);
        if (ImGui.MenuItem(T("Copy FormKey"))) ImGui.SetClipboardText(record.FormKey.ToString());
        ImGui.EndPopup();
    }

    private unsafe void DrawTable(EditorApp app)
    {
        var ws = app.Session!.Workspace;
        var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable
            | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Hideable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.SizingFixedFit;

        if (!ImGui.BeginTable("##recordTable", 6, flags)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        var em = ImGui.GetFontSize();
        ImGui.TableSetupColumn(T("Editor ID"), ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort, 2.0f);
        ImGui.TableSetupColumn(T("Form ID"), ImGuiTableColumnFlags.WidthFixed, em * 4.6f);
        ImGui.TableSetupColumn(T("Name"), ImGuiTableColumnFlags.WidthStretch, 1.4f);
        ImGui.TableSetupColumn(T("Type"), ImGuiTableColumnFlags.WidthFixed | (_type == AllTypes ? ImGuiTableColumnFlags.None : ImGuiTableColumnFlags.DefaultHide), em * 2.8f);
        ImGui.TableSetupColumn(T("Count"), ImGuiTableColumnFlags.WidthFixed, em * 2.4f);
        ImGui.TableSetupColumn(T("Last change"), ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultHide, 1.0f);
        ImGui.TableHeadersRow();

        var specs = ImGui.TableGetSortSpecs();
        if (specs.Handle != null && specs.SpecsDirty)
        {
            if (specs.SpecsCount > 0)
            {
                var spec = specs.Specs[0];
                _sortColumn = spec.ColumnIndex;
                _sortAscending = spec.SortDirection == ImGuiSortDirection.Ascending;
                Sort(app);
            }
            specs.SpecsDirty = false;
        }

        // Scroll the selection into view once when it changes from elsewhere (search, links).
        var scrollTo = -1;
        if (_scrolledToSelection != app.SelectionVersion && app.Selected is { } selectedKey)
        {
            _scrolledToSelection = app.SelectionVersion;
            scrollTo = _rows.FindIndex(r => r.FormKey == selectedKey);
        }

        // A selection made elsewhere (render window, links) replaces the picked set.
        if (app.Selected is { } current && !_picked.Contains(current)) _picked.Clear();

        var clipper = new ImGuiListClipper();
        clipper.Begin(_rows.Count);
        if (scrollTo >= 0) clipper.IncludeItemByIndex(scrollTo);

        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var record = _rows[i];
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var selected = app.Selected == record.FormKey || _picked.Contains(record.FormKey);
                var removed = app.Document.IsRemoved(record.FormKey);
                ImGui.PushID(i);
                if (removed) ImGui.PushStyleColor(ImGuiCol.Text, Theme.Error);
                var clickedRow = ImGui.Selectable(removed ? $"{record.EditorId}  ({T("removed on save")})" : record.EditorId ?? T("<no editor id>"), selected,
                    ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick);
                if (removed) ImGui.PopStyleColor();
                if (removed && ImGui.IsItemHovered()) ImGui.SetTooltip(T("Taken out of the active file when you save; Ctrl+Z brings it back"));
                if (clickedRow)
                {
                    if (Pick(app, i)) app.Select(record.FormKey);
                    else if (_picked.Count > 0) app.Select(_picked.First());
                    _scrolledToSelection = app.SelectionVersion;
                    // As in the Creation Kit: double-click opens the record's form window.
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) app.FormWindows.Open(record.FormKey);
                }

                if (i == scrollTo) ImGui.SetScrollHereY(0.4f);

                DrawRecordContext(app, record);

                if (ImGui.BeginDragDropSource())
                {
                    // The render window reads the form from FormDrag; the payload only marks the drag.
                    FormDrag.Current = record.FormKey;
                    ImGui.SetDragDropPayload(FormDrag.PayloadType, null, 0);
                    ImGui.Text(record.EditorId ?? ws.FormIdText(record.FormKey));
                    ImGui.EndDragDropSource();
                }
                ImGui.PopID();

                ImGui.TableNextColumn();
                ImGui.TextColored(Theme.TextDim, ws.FormIdText(record.FormKey));

                ImGui.TableNextColumn();
                ImGui.Text(record.Name ?? string.Empty);

                ImGui.TableNextColumn();
                ImGui.Text(record.Signature);

                ImGui.TableNextColumn();
                if (record.IsOverridden) ImGui.TextColored(Theme.Override, record.Plugins.Count.ToString());
                else ImGui.TextColored(Theme.TextDim, "1");

                ImGui.TableNextColumn();
                ImGui.TextColored(record.IsOverridden ? Theme.Override : Theme.TextDim, record.WinningPlugin.Name);
            }
        }

        clipper.End();
        ImGui.EndTable();
    }
}
