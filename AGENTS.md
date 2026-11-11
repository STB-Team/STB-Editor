# Agent guidance

## Project and documentation

STB Editor is a standalone Skyrim Creation Kit replacement for placement, cells, landscape,
navmesh, records, and Papyrus source/properties with external compilation. It uses .NET 10
(`global.json`), Win32, D3D11 and Dear ImGui. The interface remains bilingual English/Russian;
repository documentation is English.

- `README.md`: implemented features and current limitations; `CHANGELOG.md`: release history.
- `docs/DEVELOPING.md`: development, diagnostics and localization.
- `docs/RENDERING.md`: rendering behavior and asset formats.
- `docs/AI-BRIDGE.md`: provider-neutral MCP access and security boundaries.
- Keep these documents and this guidance accurate when behavior changes. Record unreleased
  changes under `Unreleased`; do not invent release dates or imply untested regressions passed.

## Build, run and releases

```powershell
dotnet build STB-Editor.sln -c Debug
src\Editor.App\bin\Debug\net10.0-windows\win-x64\STBEditor.exe --game C:\Skyrim --open
```

`C:\Skyrim` is an example installation; use the actual game path. The original development
setup uses portable MO2 at `[STB] Mod Organizer`, profile `STB`. SDK 10 usually lives in
`C:\Program Files\dotnet` and may not be on PATH.

Repository: https://github.com/STB-Team/STB-Editor, branch `main`. Releases use
`.github/workflows/release.yml`: add a `## <version>` changelog section, commit, then
`git tag v<version>` and `git push origin v<version>`. The workflow runs
`package.ps1 -Version <version>` and publishes `STBEditor-<version>.zip` with that exact
changelog section as release notes. Never tag without a release commit. `build/` and `dist/`
are generated, not committed. Packaging is self-contained; no separate .NET installation is needed.

**Keep `<CETCompat>false</CETCompat>` in Editor.App.csproj.** .NET 9+ CET-compatible executables
crash inside MO2 usvfs hooks before managed startup (0xC0000005, no log or crash report).

## Safe diagnostics and screenshots

**Never write test output into game or mod folders.** Copy plugins into a scratch directory.
Papyrus compilation writes beside the active plugin, so compilation tests also need a scratch
active file. In-app autotests can change `%LOCALAPPDATA%\STBEditor\settings.json`; restore the
previous settings afterward, including `ActivePluginPath` (normally null).

```powershell
STBEditor.exe --game C:\Skyrim --open --cell RiverwoodSleepingGiantInn --screenshot C:\Temp\stb-shot.bmp
dotnet build src/Editor.Cli -c Release -o build/cli
build\cli\stbedit-cli.exe <command> C:\Skyrim <arguments>
```

Screenshot mode loads, renders, writes BMP and exits without crash message boxes. Convert BMP
to PNG with System.Drawing. `--cell <EditorID|FormID>` waits for cell meshes/textures to stream.
Useful flags: `--search X`, `--select 0E01A348`, `--camera dx,dy,dz,yawDeg,pitchDeg` (relative to
scene focus), `--pick fx,fy` (viewport fractions, logs `pick:`), `--no-markers`,
`--render opaque=0,blended=0,markers=0,shading=N,logblended=1`,
`--object-shot <FormID> <scratch.bmp> [size yaw pitch zoom]`, `--psys <name>`, and
`--layout world|records|classic`. Other useful cells: `BleakFallsBarrow01`, `WhiterunExterior10`.
Screenshots/autotests must not persist view presets or `LastView`.

CLI inspection: `plugins`, `catalog`, `find <text> [--type <signature>]`,
`find <plugin name> --plugin`, `assets <paths…>`, `extract <asset> <file>`,
`nif [prefix]`, `nifdump <mesh>`, `scene <cell EditorID> [--shaders]`,
`sweep [n]`. Use scratch destinations for extraction. Relevant regression commands are listed
below; see the developer guide for full invocation details. Do not claim a check passed unless run.

Logs: `%LOCALAPPDATA%\STBEditor\logs\editor.log`; crash reports: `crashes\`; docking: `layout.ini`
in the same settings directory (remove only when intentionally resetting layout).

## Architecture and ownership

- `Editor.Data`: no UI. `GameInstall` reads the executable's **string** version (SkyrimSE 1.5.97
  numeric fields say 1.0.0.0), detects BEES/ESL limits; `Mo2Instance`/`LoadOrderReader` resolve
  profiles; `Workspace` owns Mutagen overlays, masters-first order and FormID/FormKey/FE mapping.
  `Raw/` reads TES4 headers/records; `Catalog/` caches merged base records and CELL headers,
  not placed references, navmeshes or landscape. Cell loaders own contents.
- `Editor.Assets`: `AssetFileSystem`; MO2-priority loose files override BSAs, archives follow
  INI order then `<plugin>.bsa` / `<plugin> - Textures.bsa` for each plugin.
- `Editor.Nif`: NIF 20.2.0.7, BS 83 LE / 100 SSE; unknown blocks skip by header sizes.
  Pre-Skyrim BS < 83 meshes are reported, not drawn. Effects/controllers: `NifFile.Effects.cs`.
- `Editor.Render`: asynchronous `MeshCache`/`TextureCache` (D3D11 creation is free-threaded),
  `SceneBuilder` merges cell versions and persistent world references by position, LAND/water/
  XCLL/LGTM; `SceneRenderer` owns opaque, sorted blended, terrain and overlay passes;
  `Picking` uses CPU rays; `Camera` uses infinite reversed Z. DDS supports BC1–7 and legacy RGB/L8.
- `Editor.Core`: `EditDocument` retains original/pending FormKey states so dragging back removes
  a change; `UndoStack` groups edits. Reapply the document after every scene load.
- `Editor.App`: Vortice D3D11 and Hexa.NET.ImGui docking with official backends; `Panels/` owns
  windows; `Ui/PropertyTree` draws Mutagen getter fields. `Editor.Cli` owns diagnostics.

## Data, editing and saving invariants

- **Use typed lookups.** Never call untyped `LinkCache.TryResolve(key)` or
  `ResolveAllSimpleContexts(key)`: they index every type, including a million REFRs.
  Resolve the catalog signature to its getter type.
- **Walk each plugin's groups once.** Mutagen stream overlays reread and retain group bytes on
  each walk. Per-cell worldspace walks previously retained 5 GB for a 5x5 Tamriel grid.
  Use `RecordLookup.ModCells` (one index per plugin). `memprobe <cell>` and `--autotest-fly <n>`
  should show flat memory. Caches trim items unused for 20 seconds beyond their DXGI budget
  share; `GpuModel` holders check `IsDisposed`. Frame limits: 60 FPS, 15 in background.
- **Encoding is per string.** `StringEncodingDetector.ReadEncoding` uses strict UTF-8 with
  cp1251 fallback; writing uses the plugin's dominant encoding. Mixed plugins exist. MO2
  `plugins.txt`/`loadorder.txt` use line-wise `TextFiles.ReadLines`, not one assumed encoding.
- Localized masters may live in MO2 mods: `Workspace.StringSources` supplies winning loose
  `Strings` or Data archives through `StringsFolderOverride`/`BsaFolderOverride`.
  Diagnostic: `strtest C:\Skyrim <EditorID>`.
- FormID prefixes follow **active** plugins, not only successfully opened overlays. A broken
  plugin must not shift later prefixes. MCP IDs use canonical FormKey for new/unloaded plugins,
  not human-only `Workspace.FormIdText`.
- **Preserve untouched bytes and masters.** Mutagen alone normalizes untouched records and
  drops unused masters, renumbering own IDs. `PluginWriter` retains original masters, appends
  needed ones, uses `MastersListContent/Ordering = NoCheck`, then `RawSplice` restores untouched
  bytes; `CompareUntouched` must report 0. Adding a master shifts own indices and skips the splice;
  reordering masters also takes the full rewrite path. `CompareUntouched` maps old master indices
  to new ones. `SaveController` must request confirmation for new masters even with zero
  `LossyRecords`; master-only consent must not suppress a later lossy warning.
  Checks: `savetest C:\Skyrim <interior EditorID> <out dir> [existing plugin]`,
  `--autotest-save <dir>`, `--autotest-savecheck` (hashes unchanged before consent).
- Record edits use `OverrideDraft`: `WorkingCopy` → mutate → `CommitEdit`. Top-level writer
  groups use `Set`, not `AddUntyped` for existing keys; references replace cell entries.
  New bases are catalog transients synchronized from drafts (`RecordCatalog.AddTransient`).
  `RecordForm` starts session-created references from `ReferenceProperties.NewRecord`, rereads
  after saves via `_loadedIn`, and tolerates missing catalog entries when cells unload.
- **All field access/editing goes through `Core/RecordFields`**, including paths, lists,
  dictionaries and gendered pairs; UI only draws. `EditTarget` applies paths to fresh copies.
  Clear Mutagen Break flags (`Unbreak`) or fields after short structs disappear. `RecordRoundTrip`
  writes/reads one record, supplies format warnings and rejects unreadable edits. Use round-trip
  `FloatText`, never `"%.2f"`; hide `System.Type` values, not properties merely named `Type`.
  `formtest <dir> 5` expects 0 unexpected / 0 set errors.
- New CELL/WRLD are mutable `CellDraft`/`WorldDraft` in `EditDocument`, visible through
  `SceneBuilder.Document` and written before references. Missing exterior destination cells get
  drafts (or auto cells in the writer). Unsaved means `PendingCount`, not `ChangedCount`.
  Checks: `newtest <dir> [plugin]`, `--autotest-new <dir>`, `--autotest-place` (selection via
  get_status/select, real Rotate/Move input, bounded movement and scratch save/reload).
- INFO drafts use `RecordPlace.DialogInfo`, topic in `Cell`, written beneath a header-only topic;
  CELL/WRLD header edits use `RecordPlace.Header`. Batch text edits use `Services/TextEdits` to
  preserve placement. Dialogue order is PNAM, not list position; only active-file-owned INFOs
  can be deleted. VMAD alias scripts bind by quest FormKey + alias ID (`Core/AliasScripts`);
  `--autotest-aliasscripts <scratch-dir> [questEditorID]` covers save/reload.
- Landscape heights are world units; VHGT = offset + clamped sbyte steps x8. Preserve original
  VNML bytes unless heights change; re-encoding normalized normals creates seams. ATXT allows
  at most six layers per quadrant; painting blends. Rebuild from drafts on `DraftsChanged`,
  and from the current stroke's draft during painting, never the original `TerrainCell`.
  Grass twins match TXST with opposite GNAM state: names such as "NoGrass" are not reliable;
  a full quadrant may require `Retexture`. Checks: `landtest`, `--autotest-land <dir>` with
  `--cell`, `--autotest-landview <dir>`, `landseam`, `landseamfix`.
- NAVM uses `RecordPlace.Navmesh`; deleting triangles renumbers loaded neighbors' edge links
  in the same undo step. Finalize computes doors/XNDP, one-to-one overlap links and NAVI only
  for the plugin's navmeshes. The lookup grid stores uint count + ushort triangle indices per
  cell and is a safe superset of the game. Checks: `navtest`, `finalizetest`; vanilla interior
  navmeshes should pass `Core/NavmeshCheck`. Generation maps Skyrim (x,y,z) to Recast (x,z,-y),
  takes detail-mesh heights and collision-bearing ground types, excluding doors/activators/
  effects/alpha; replacing the biggest NAVM drops neighbors' old incoming links, then Finalizes.
- `ScriptGlobals` must remain public. User-script record edits are copies validated through
  `RecordRoundTrip` and committed on the UI thread. PEX parsing is big-endian and walks function
  bodies to skip them. CK LipGenerator needs mono 16-bit WAV and writes `tmp16khz.wav` in its
  working directory: always run it in a temp folder.

## Rendering, NIF and physics constraints

- Reference rotation uses engine `NiMatrix3::SetEulerAnglesXYZ` rows on column vectors;
  Z heading is clockwise. NIF/terrain fronts are CCW (`FrontCounterClockwise = true`).
  CK-style rotations pivot on drag-start positions, not rounded live positions (`mathtest`).
  Move drags are capped at 4x cursor speed at the object; leave edge-on rotation rings out.
- NIF `EditorMarker` nodes are game-hidden by name even if their hidden flag is unset; show
  only with Markers. Refraction shapes are omitted because diffuse contains a normal map.
  Map NiAlphaProperty *_COLOR blend factors to alpha equivalents for the D3D11 alpha channel.
  Normalize authored build paths with `AssetFileSystem.TexturePath` at `data\` / `\textures\`.
- Read niftools `nif.xml` (develop) for layouts; particle aspect/subtexture data is in NiPSysData.
  Read Community Shaders HLSL before changing shader behavior, e.g.
  `C:\Skyrim\[STB] Mod Organizer\mods\Community Shaders\Shaders\Effect.hlsl`.
  Effect palettes are 2D: alpha = palette(tex.a, alpha); color = emissiveMult x
  palette(tex.g, baseColour.r x vertex.r). Grass vertex alpha is wind weight, not opacity.
- Actor composites (`ActorAppearance`, MeshCache `actor:a.nif|b.nif?fallback,…`) use SSE
  skin-partition indices **directly** into the shared vertex buffer, never through the vertex
  map. Bind pose blends the file's bone nodes x NiSkinData transforms per vertex. Hide a skin
  part when its main slot (body, otherwise lowest) is worn. Checks: `npc <id>`, `npcscan`.
- FaceGen slider directions come from `FaceGenMorphs.Sliders`, not Mutagen names. Race morph
  uses MorphRace; SkinnyMorph = 1 - weight/100. TXST TX02..TX05 map to NIF slots 5,2,3,4.
  BSDynamicTriShape head partitions contain no positions: never patch them. Tint starts at
  QNAM, blends layers except SkinTone by mask red x value; hair color = hair color x 2/255.
  Face preview builds on a worker into memory assets under `meshes\stbeditor\facepreview`;
  releasing a drag commits the entire appearance in one `EditTarget.Apply`. Checks: `facegen*`.
- **Keep Bepu pinned to 2.5.0-beta.33.** Stable 2.4 ReduceFace can cycle and overrun unmanaged
  storage during thumbnail collision creation; do not downgrade or disable collision.
  Keep hull inputs through `NifFile.SafePiece`: nearly flat point sets can crash natively.
  `collisiontest` covers cube edges, cycle regression, bounds and inertia.
- Physics uses meters internally, clockwise triangles, shared static meshes and unit-mass
  compounds; omit moving contents and pieces initially inside one another. Havok convex pieces
  scale by 69.99124, grow vertices by convex radius; incomplete triangle collision falls back
  to a render-mesh hull. Do not apply the NIF root's own transform to physics bodies.
  Compressed collision chunks use origin + ushort x error, chunk transform, strips then triangles.
  Run simulation on a worker; `FinishHavokSim` commits one undo step.
- Draw navmesh overlays after blended geometry. VSNavNear/VSNavFar pull vertices 8/48 units
  toward the camera without moving their screen positions; `navcover` diagnoses coverage.
  XPRM bounds are full sizes, halved into `SceneObject.Primitive`; portals use XPOD, rooms XLRM.
- Perk connections refer to node `Index`, not list positions; vanilla root grid fields are junk.
  The game-view star coordinates/skill-slot order come from StatsMenu and INTPerkSkydome.nif;
  preserve `PerkSky` conventions rather than inventing a flat layout. Room checks must not
  require reciprocal room links or room-to-room links for portals; vanilla does neither reliably.

## UI and input constraints

- Use `Theme.*` colors, not hard-coded colors. The style uses graphite surfaces/white-alpha
  highlights; `Ui/Studio.cs` owns edged controls, `Theme.Surfaces` structural surfaces,
  `Studio.BeginWindow/BeginPopupModal` and `WindowChrome` padded close targets. Segoe UI is body
  text/Cyrillic; optional Sovngarde headings come from the user's build, never ship game fonts.
  Keep a plain-space digit separator. For font portability, prefer x, +, <-> to missing symbols.
- Every visible string uses `T`, `TF` or `Title` (`Ui/Loc.cs`) and a Russian `Ui/LangRu.cs` entry.
  `python tools/loc.py missing` must exit 0 with no missing keys on stdout (`0 missing` on stderr); see
  [UI translation](docs/DEVELOPING.md#ui-translation). Give repeatable/translatable controls explicit `###` IDs; two distinct
  English labels can translate identically. Field typing/suggestions use scoped numeric ImGui IDs.
- Shell World / Records / Classic switches preserve scene, selections and `EditDocument`, and
  startup preserves existing `layout.ini`. Refresh the central dock ID after ini loading; enforce
  document size constraints for tiny floating hosts. Complete workers in `RenderWindow.Update`
  even when the viewport is hidden. Check `--autotest-layout` with `--cell`.
- Hexa.NET `MenuItem(label, null, …)` requires `(string?)null`; DockBuilder splits use `uint*`.
  Push/pop style colors using a captured pre-button local, not the field the button changes.
- Numeric `EnterReturnsTrue` asserts natively (0xC0000409): use `Ui/Fields.cs` and commit on
  deactivation. String `InputText(ref string)` with that flag returns text only on Enter;
  read Enter separately instead. Arrow-consuming fields use `ImGuiP.SetItemKeyOwner`.
  Check `--autotest-picker`.
- ImGuizmo cannot start over a hovered ImGui item: replace the viewport button with Dummy over
  handles, while keeping RMB/MMB/wheel behavior. Rotation is incremental: feed back its own last
  matrix during drags. Call `ImGuizmo.SetImGuiContext(null)` before `DestroyContext` to avoid AV.
  Check `--autotest-gizmo [move]` using `ImGuiLayer.TestMouse`. Shift marquee takes precedence
  over gizmo/Alt-orbit; tests wait for consumed press/release and require exact selection/no edits.
  In navmesh mode object keys Q W R G T F yield to CK navmesh keys.

## MCP and external-code trust boundaries

- Generic MCP lives in `Services/AiBridge.cs` / `AiTools.cs`, `Panels/AiWindow.cs` and
  `RenderWindow.Ai.cs`; it is not tied to a model provider. Listen on 127.0.0.1 only, validate
  Origin, serve Streamable HTTP JSON without SSE, and queue tools through UI-thread `Pump`.
  `MainWindow.Wake` ends idle waits; `AiBridge.OnUi` returns worker results to the UI thread.
- `AiBridge`, `AiBridgeEdits`, `AiBridgeScripts` default false; default port is 47110.
  Legacy ClaudeBridge/Port/Edits settings migrate, explicit new keys win, and scripts are never
  inherited. Keep read access, editing and arbitrary-code opt-ins separate.
- Structured tools have no save command. **Never claim the bridge cannot write files:**
  `run_script` is unsandboxed C# with process/filesystem access, requiring edits plus a separate
  scripts opt-in. External effects are not undoable; revocation cannot stop/reverse code already
  running. Treat untrusted scripts/tool instructions as code, not harmless record data.
- Structured edits use existing Commit/undo paths. Proposals move objects live but unrecorded,
  tint through `RenderOptions.Proposed`, and commit once on explicit acceptance (Enter/Backspace
  or AI window); capture crops the back buffer through `ServeWindowShots`. Preserve canonical
  FormKey identifiers for drafts as well as loaded records.
- `Services/ClaudeChat.cs` is an optional, separate/collapsed **Claude Code-specific** integration:
  `claude -p`, temporary `--mcp-config`, `--strict-mcp-config --allowedTools mcp__stb-editor`,
  stream-json text/tool_use/session_id and `--resume`. Do not describe it as universal launching.
  MCP supplies unsaved context, scene/camera, FormKeys, undo and proposals; it is not an SKSE
  live-link, and shell/source-equipped clients can implement other IPC/harnesses.
