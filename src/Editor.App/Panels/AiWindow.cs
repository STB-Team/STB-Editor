using System.Numerics;
using System.Text.Json;
using Hexa.NET.ImGui;
using StbEditor.App.Ui;
using static StbEditor.App.Ui.Loc;

namespace StbEditor.App.Panels;

/// <summary>
/// View → AI / MCP: generic endpoint/configuration, permissions, proposals, marks, notes and call history.
/// Claude Code chat is a separate optional launcher; no CLI is needed for the MCP bridge.
/// </summary>
internal sealed class AiWindow
{
    private bool _open;

    public void Show() => _open = true;

    public static string Endpoint(int port) => $"http://127.0.0.1:{port}/mcp";

    /// <summary>Common MCP JSON shape; clients may use different configuration keys.</summary>
    public static string McpConfig(int port) => JsonSerializer.Serialize(new
    {
        mcpServers = new Dictionary<string, object> { ["stb-editor"] = new { type = "http", url = Endpoint(port) } },
    }, new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Optional Claude Code setup, independent of the generic connection panel.</summary>
    public static string ClaudeConnectCommand(int port) => $"claude mcp add --scope user --transport http stb-editor {Endpoint(port)}";

    /// <summary>The bridge's switches (also in View → Settings).</summary>
    public static void DrawSwitches(EditorApp app)
    {
        var settings = app.Settings;
        var on = settings.AiBridge;
        if (ImGui.Checkbox(T("Enable local AI / MCP bridge"), ref on)) { settings.AiBridge = on; settings.Save(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Compatible MCP clients on this computer can inspect the live editor, capture the viewport, move the camera, select objects and propose edits. No model or Claude CLI is required by the bridge. Only local programs can connect."));
        var edits = settings.AiBridgeEdits;
        ImGui.BeginDisabled(!on && !edits);
        if (ImGui.Checkbox(T("Allow AI edits (structured edits are undoable; no plugin save tool)"), ref edits)) { settings.AiBridgeEdits = edits; settings.Save(); }
        ImGui.EndDisabled();
        var scripts = settings.AiBridgeScripts;
        ImGui.BeginDisabled((!on || !edits) && !scripts);
        if (ImGui.Checkbox(T("Allow remote unrestricted C# scripts (unsafe; separate permission)"), ref scripts)) { settings.AiBridgeScripts = scripts; settings.Save(); }
        ImGui.EndDisabled();
        ImGui.TextWrapped(T("Remote C# is NOT sandboxed: it can read/write files and start processes. These side effects are NOT undoable. Turning permission off blocks queued scripts and record commits, not already-started code."));
        ImGui.BeginDisabled(!on);
        var port = settings.AiBridgePort;
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 10);
        if (Fields.Int(T("Port") + "##aiport", port, out var newPort) && newPort is >= 1024 and <= 65535) { settings.AiBridgePort = newPort; settings.Save(); }
        ImGui.EndDisabled();
        var status = app.Bridge.Status;
        ImGui.TextColored(app.Bridge.Running ? Theme.Accent : status.StartsWith("error", StringComparison.Ordinal) ? Theme.Error : Theme.TextDim,
            app.Bridge.Running ? TF($"Listening: {status}") : status == "off" ? T("Off") : status);
        ImGui.SeparatorText(T("MCP connection"));
        var endpoint = Endpoint(settings.AiBridgePort);
        ImGui.TextUnformatted(endpoint);
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Copy endpoint"))) ImGui.SetClipboardText(endpoint);
        ImGui.TextWrapped(T("Use an MCP client supporting Streamable HTTP with JSON responses. No SSE stream or stdio launcher is provided; client configuration formats may differ."));
        if (ImGui.CollapsingHeader(T("MCP JSON configuration")))
            ImGui.TextUnformatted(McpConfig(settings.AiBridgePort));
        if (ImGui.SmallButton(T("Copy MCP JSON"))) ImGui.SetClipboardText(McpConfig(settings.AiBridgePort));
    }

    private readonly Services.ClaudeChat _chat = new();
    private string _message = "";

    public bool ChatRunning => _chat.Running;

    /// <summary>«Ask Claude»: a message box, the conversation with the tools it used, stop and new conversation.</summary>
    private void DrawChat(EditorApp app)
    {
        ImGui.TextColored(Theme.TextDim, T("Connect Claude Code once (in a terminal):"));
        var command = ClaudeConnectCommand(app.Settings.AiBridgePort);
        ImGui.TextWrapped(command);
        if (ImGui.SmallButton(T("Copy") + "##claudecmd")) ImGui.SetClipboardText(command);
        ImGui.SeparatorText(T("Ask Claude"));
        if (!app.Settings.AiBridge) { ImGui.TextColored(Theme.TextDim, T("Turn the bridge on above first.")); return; }
        if (Services.ClaudeChat.FindCli() is null) { ImGui.TextColored(Theme.Warning, T("Claude Code (claude) is not installed on this computer.")); return; }
        ImGui.TextColored(Theme.TextDim, T("Runs your optional Claude Code CLI with this editor's MCP tools (uses your Claude account). Ctrl+Enter sends."));
        var height = ImGui.GetTextLineHeightWithSpacing() * 10;
        if (ImGui.BeginChild("##chat", new Vector2(0, height), ImGuiChildFlags.Borders))
        {
            foreach (var entry in _chat.Transcript)
            {
                var colour = entry.Kind switch { "user" => Theme.Accent, "tool" => Theme.TextDim, "error" => Theme.Error, _ => Theme.Text };
                var prefix = entry.Kind switch { "user" => T("You: "), "tool" => "  → ", "error" => "! ", _ => T("Claude: ") };
                ImGui.PushStyleColor(ImGuiCol.Text, colour);
                ImGui.TextWrapped(prefix + entry.Text);
                ImGui.PopStyleColor();
            }
            if (_chat.Running) ImGui.TextColored(Theme.Warning, T("Claude is working…"));
            if (_chat.Error is { } error) ImGui.TextColored(Theme.Error, error);
            if (_chat.Running || ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 4) ImGui.SetScrollHereY(1);
        }
        ImGui.EndChild();
        ImGui.InputTextMultiline("##message", ref _message, 4000, new Vector2(-1, ImGui.GetTextLineHeight() * 3.5f));
        var send = ImGui.IsItemFocused() && ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Enter, false);
        ImGui.BeginDisabled(_chat.Running || _message.Trim().Length == 0);
        if (ImGui.Button(T("Send")) || (send && !_chat.Running))
        {
            _chat.Send(app, _message);
            _message = "";
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!_chat.Running);
        if (ImGui.Button(T("Stop"))) _chat.Stop();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button(T("New conversation"))) _chat.Reset();
    }

    public void Draw(EditorApp app)
    {
        _chat.Pump();
        if (!_open) return;
        ImGui.SetNextWindowSize(app.Scaled(620, 560), ImGuiCond.FirstUseEver);
        if (Studio.BeginWindow(Title("AI / MCP") + "###aiwindow", ref _open, ImGuiWindowFlags.NoSavedSettings))
        {
            DrawSwitches(app);
            if (ImGui.CollapsingHeader(T("Optional: Claude Code launcher"))) DrawChat(app);

            var window = app.RenderWindow;
            if (window.PendingProposal is { } proposal)
            {
                ImGui.SeparatorText(T("AI proposes"));
                ImGui.TextColored(Theme.Accent, TF($"{proposal.Title}: {proposal.Changes.Count} object(s), shown tinted in the Render Window"));
                if (ImGui.Button(T("Apply") + "##proposal")) window.Decide(true);
                ImGui.SameLine();
                if (ImGui.Button(T("Reject") + "##proposal")) window.Decide(false);
                ImGui.SameLine();
                ImGui.TextColored(Theme.TextDim, T("(over the view: Enter / Backspace)"));
            }
            if (window.PastProposals.Count > 0 && ImGui.CollapsingHeader(TF($"Earlier proposals ({window.PastProposals.Count})")))
                foreach (var p in window.PastProposals) ImGui.TextColored(Theme.TextDim, $"#{p.Id} {p.Title}: {p.Status}");

            ImGui.SeparatorText(TF($"Marks for AI ({window.Marks.Count})"));
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextDim);
            ImGui.TextWrapped(T("K over the Render Window marks the point under the cursor; write what you want there. MCP clients read them with get_user_marks."));
            ImGui.PopStyleColor();
            for (var i = 0; i < window.Marks.Count; i++)
            {
                var m = window.Marks[i];
                ImGui.PushID(m.Number);
                ImGui.TextColored(Theme.Accent, $"{m.Number}");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - ImGui.GetFontSize() * 10);
                ImGui.InputTextWithHint("##note", T("what to do here"), ref m.Note, 400);
                ImGui.SameLine();
                ImGui.TextColored(Theme.TextDim, m.ReferenceLabel ?? $"{m.Position.X:0} {m.Position.Y:0}");
                ImGui.SameLine();
                if (ImGui.SmallButton("x")) { window.Marks.RemoveAt(i); ImGui.PopID(); break; }
                ImGui.PopID();
            }
            if (window.Marks.Count > 0 && ImGui.SmallButton(T("Remove all marks"))) window.Marks.Clear();

            ImGui.SeparatorText(TF($"AI notes on the view ({window.Annotations.Count})"));
            if (window.Annotations.Count > 0 && ImGui.SmallButton(T("Clear the notes"))) window.Annotations.Clear();

            ImGui.SeparatorText(T("MCP tool history"));
            if (ImGui.BeginChild("##aihistory", Vector2.Zero, ImGuiChildFlags.Borders))
            {
                foreach (var (at, tool, summary, error) in app.Bridge.History)
                {
                    ImGui.TextColored(Theme.TextDim, at.ToString("HH:mm:ss"));
                    ImGui.SameLine();
                    ImGui.TextColored(error ? Theme.Error : Theme.Text, tool);
                    ImGui.SameLine();
                    ImGui.TextColored(Theme.TextDim, summary);
                }
            }
            ImGui.EndChild();
        }
        ImGui.End();
    }
}
