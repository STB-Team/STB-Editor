# AI / MCP: access to an open STB Editor session

An optional local MCP server connects a **compatible MCP client** to the running editor.
The client chooses the model: the bridge does not depend on Claude or require a model provider's API key.
A model alone cannot connect without an MCP client or adapter.
The generalized names and permissions are available from version **0.77.0**.

**Off by default.** Open **View → AI / MCP**; settings are under **View → Settings → AI / MCP**.
Direct edits are disabled separately; running C# requires a third, more dangerous permission.

## Connecting a compatible client

1. Enable the bridge in settings or the AI / MCP window. Load the editor's data.
2. Set the client's HTTP MCP endpoint to **`http://127.0.0.1:47110/mcp`**.
   The port is configurable; use the address displayed in the editor.
3. A typical client configuration:

   ```json
   {
     "mcpServers": {
       "stb-editor": {
         "type": "http",
         "url": "http://127.0.0.1:47110/mcp"
       }
     }
   }
   ```

This illustrates clients using `mcpServers`, not a universal application schema:
consult your client's documentation for its field names and registration procedure.
The client needs Streamable HTTP support with JSON responses. The server negotiates MCP version
`2025-03-26`, not arbitrary client versions. There is no SSE transport or stdio command;
a stdio-only client needs an external adapter. Clients obtain exact tool parameters through `tools/list`.
Loaded record IDs usually look like `00012EB7`; a draft in a plugin not yet loaded may use
`000800:MyPlugin.esp`. Pass returned IDs back unchanged.

<details>
<summary>Optional Claude Code launcher</summary>

The collapsed **Claude Code** section in the AI / MCP window can run an existing `claude -p`
through **Ask Claude**, displaying replies and tool calls and supporting conversation continuation.
Claude Code must be installed and authenticated; it uses your account.
This is one optional launcher, not support for other CLIs or providers in that field.

For external Claude Code instead of the built-in launcher:

```text
claude mcp add --scope user --transport http stb-editor http://127.0.0.1:47110/mcp
```

</details>

## Where the bridge helps

- **Unsaved document:** edits, the active file, selection and undo history are not available by
  reading only the ESP on disk.
- **Loaded scene:** the camera, viewport screenshot, picking, user marks (**K**) and visible objects
  provide a concrete place to discuss instead of guessing from file coordinates.
- **Structured operations:** FormKeys and field paths, normal editor commands and undo,
  and proposals with explicit **Apply / Reject**, rather than directly overwriting a plugin.

A coding agent with a shell and source access is not inherently excluded from these capabilities:
it can implement alternative IPC or a test harness. The bridge is a ready-made, controlled route
into the live session without that development, not a special ability of Claude. An ordinary coding
agent is often sufficient for repository source edits, builds or offline file analysis; the bridge
does not replace those tools. It does not connect to running Skyrim and is not an SKSE live-link.

## Tools

| Tool | Purpose |
|---|---|
| `get_status` | Plugins, active file, unsaved changes, cell, camera, selection, marks; `ai_may_change` and `ai_may_run_scripts` |
| `screenshot` | Viewport screenshot with annotations and overlay; waits for models and textures to load |
| `set_camera`, `look_at`, `load_cell` | Camera, focus on an object, load a cell or world grid |
| `list_objects`, `pick`, `select` | Nearby objects, object under a screenshot point, selection |
| `move_objects`, `place_object`, `delete_objects` | Structured object edits through normal undo commands |
| `undo` | Undo or redo steps |
| `find_records`, `get_record`, `set_field` | Search, record fields, editing by path (`Name`, `Configuration.HealthOffset`, `Keywords[1]`) |
| `use_info`, `framework_info` | References and placements, SKSE framework configs, SPID for NPCs |
| `annotate`, `clear_annotations`, `get_user_marks` | Scene annotations and user K marks |
| `propose_changes`, `proposal_status` | Preview moves, rotations and deletions; the user applies them as one undo step or rejects them |
| `get_journal` | Session log, undo history, latest crash report |
| `run_script` | Arbitrary C# through the existing ScriptHost, only with separate permission |

Proposals are available without direct-edit permission: the user decides with **Enter / Apply**
or **Backspace / Reject**. Preview object positions are not yet recorded document commands.
Proposals do not cover arbitrary field edits or scripts.

## Permissions and safety boundaries

| Setting | Default | Allows |
|---|---|---|
| `AiBridge` | `false` | Local HTTP server, inspection, navigation, screenshots, annotations, proposals |
| `AiBridgePort` | `47110` | Endpoint port |
| `AiBridgeEdits` | `false` | Direct structured edits and undo through the bridge |
| `AiBridgeScripts` | `false` | Additionally, `run_script`; requires both the bridge and direct edits to be enabled |

- The server listens on `127.0.0.1`; requests with an unrelated browser `Origin` are rejected.
  This does not authenticate local processes: permissions apply to all clients that can reach
  the endpoint. Do not expose the port through a proxy or tunnel.
- Structured tools have **no save-plugin command**; their changes remain in the document until
  the user saves. This is not a general guarantee that AI cannot write files.
- **`run_script` is UNSANDBOXED.** C# runs with the editor process's privileges and can read and
  write files, launch processes and execute other local code. Record edits through the supplied
  API are collected into an undo command, but external effects, file writes and process launches
  cannot be undone with Ctrl+Z. Enable it only for a trusted client and reviewed code.
- Revoking permission blocks queued calls and script edit commits that have not executed;
  it neither reverses nor guarantees termination of arbitrary code already running.
- `run_script` is a separate trust boundary even for read-only tasks. It is also forbidden when
  direct edits are disabled. Inspection and proposals do not require it.
- The window's call history helps show client actions, but is neither a sandbox nor proof that
  no side effects occurred.

When old settings load, `ClaudeBridge`, `ClaudeBridgePort` and `ClaudeBridgeEdits` migrate to
new keys; explicitly supplied new keys take precedence. Script permission is **not inherited**.

## For developers

`src/Editor.App/Services/AiBridge.cs` handles HTTP/JSON-RPC (`initialize`, `ping`, `tools/list`,
`tools/call`) and queues calls for the UI thread; `Pump` and `MainWindow.Wake` run between frames.
`Services/AiTools.cs` contains the tools; `Panels/AiWindow.cs` contains settings, history and the launcher;
`Panels/RenderWindow.Ai.cs` handles screenshots, annotations, K marks and edits through `Commit`.
Screenshots with overlays are cropped from the finished back buffer (`ServeWindowShots` in Program).
Proposals are applied in `Panels/RenderWindow.Proposals.cs`.
`Services/ClaudeChat.cs` remains only an adapter for the optional Claude Code CLI.

A model-free example using the [official Python SDK](https://github.com/modelcontextprotocol/python-sdk)
(`mcp` 2.x in a separate venv; the bridge and editor data must already be enabled):

```python
import asyncio
from mcp import Client

async def main():
    async with Client("http://127.0.0.1:47110/mcp") as client:
        result = await client.call_tool("get_status", {})
        for block in result.content:
            if block.type == "text":
                print(block.text)

asyncio.run(main())
```

This is an endpoint-checking example, not a report of tests passing. Permission checks must separately
cover denied direct edits, denied `run_script`, applying and rejecting proposals, and the absence of
side effects. Use scratch files outside the game and mod directories for write scenarios.
