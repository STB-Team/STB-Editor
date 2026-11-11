# Developing STB Editor

A guide to building the editor, its implemented architecture, unattended checks, and hard-won invariants.
Repository operating guidance: [AGENTS.md](../AGENTS.md).

## Building

- .NET SDK 10 (`global.json`: 10.0.100, roll-forward latestFeature; usually `C:\Program Files\dotnet`),
  Windows 10/11 x64, D3D11 GPU. Both GitHub Actions workflows use `10.0.x`.
- `dotnet build STB-Editor.sln -c Debug` builds the solution.
- `src\Editor.App\bin\Debug\net10.0-windows\win-x64\STBEditor.exe --game C:\Skyrim --open` runs against a game installation.
- `package.ps1` creates the portable self-contained archive in `dist\`.
- Release: `## <version>` in `CHANGELOG.md` → commit → `git tag v<version>` → `git push origin v<version>`.
  GitHub Actions (`.github/workflows/release.yml`) builds with the same `package.ps1` and publishes to Releases,
  using that changelog section as release notes. Pushes to `main` and PRs build through `.github/workflows/build.yml`.
- Diagnostic console: `dotnet build src/Editor.Cli -c Release -o build/cli`, then
  `build\cli\stbedit-cli.exe <command> <game-folder> …` (commands below).

NuGet dependencies: Mutagen.Bethesda.Skyrim (plugins), Hexa.NET.ImGui + Backends + ImGuizmo (UI),
Vortice.Direct3D11 / D3DCompiler (rendering), BepuPhysics (dropping/hulls), DotRecast (navmesh),
Roslyn (C# scripts). The primary Segoe UI font comes from Windows; game heading fonts are located in
the user's installation (`Ui/Theme.cs`). Game fonts are not distributed.

## Architecture

```
src/
  Editor.Data     No UI: game installation, MO2, load order, Workspace (Mutagen), record catalog,
                  raw TES4 layer (Raw/: headers, record traversal, byte-preserving RawSplice)
  Editor.Assets   AssetFileSystem: loose files by MO2 priority over BSAs
  Editor.Nif      Handwritten rendering NIF reader, collision/effects; block-level FaceGen writer, not a general NIF editor
  Editor.Render   D3D11: mesh/texture caches, scene, SceneBuilder (cells → objects), SceneRenderer,
                  particles (Particles.cs), picking, camera
  Editor.Core     Edits: EditDocument + undo, ActivePlugin (FormID), PluginWriter (saving)
  Editor.App      Win32 window, ImGui, panels (Panels/), services (saving, logs, settings)
  Editor.Cli      Windowless diagnostics
```

Data flow: `Workspace` (load order + Mutagen overlays) → `RecordCatalog` (all non-placed records)
→ `SceneBuilder` builds a cell/cell-grid `Scene` → `SceneRenderer` draws and `Picking` selects
→ edits enter `EditDocument` (reference states + undo) → `PluginWriter` writes the active plugin.

The optional [AI/MCP bridge](AI-BRIDGE.md), `Services/AiBridge.cs` / `AiTools.cs` and
`Panels/AiWindow.cs` / `RenderWindow.Ai.cs`, exposes the live unsaved session to compatible clients;
it is not a model provider. `ClaudeChat.cs` is only an optional launcher for installed Claude Code.
`AiBridge`, `AiBridgeEdits`, and `AiBridgeScripts` default to false. Arbitrary `run_script` is not sandboxed:
it requires all three permissions, and external file/process side effects cannot be undone.
The absence of a structured save command does not mean AI cannot write files.

New records (cells, worlds) are **drafts** (`Core/EditDocument.cs`: `CellDraft`, `WorldDraft`): ordinary
mutable Mutagen `Cell`/`Worldspace` records held in the document until saving. `Core/NewRecords.cs`
creates them. Landscape edits use `LandDraft` with `Core/LandscapeData.cs` (world-unit heights,
quadrant layers; VHGT/VNML/VCLR/BTXT/ATXT output); brush strokes use `LandStrokeCommand`
(`Panels/RenderWindow.Landscape.cs`, `LandscapePanel`). Existing-record edits use `OverrideDraft`
(`Core/Overrides.cs`): a mutable copy (`WorkingCopy` → mutation → `CommitEdit`, before/after snapshots
for undo). `RecordPlace` identifies placement in the plugin (type group, reference cell, navmesh).
`SceneBuilder.Document` builds scenes from drafts just like loaded records; `PluginWriter` adds them
before references. Creation is an undo command (`DraftCommand`, or `CompositeCommand` with references).

Each UI panel is a `Panel` subclass in `Editor.App/Panels`; layouts are in `Panels/Panel.cs`
(`DefaultLayout`, `LayoutPreset`), and the workspace strip is in `Panels/EditorShell.cs`.
`EditorApp.ApplyLayout` changes visibility/docking only: `EditDocument`, scene, and selection remain intact.
Startup does not replace saved `layout.ini`; `--layout world|records|classic` explicitly applies a layout.
In Records, new forms/comparisons target the central dock. Set a floating window's initial position
only when opening it, and never alongside a docking request: delayed `FirstUseEver` otherwise undocks
it on the next frame. Panel `###` IDs remain language-independent.
Records has two columns: catalog 38%, Inspector/Preview/forms/comparisons in the right dock.
`DefaultLayout` updates the central ID every frame: an ini-loaded node may not exist on the first frame.
Forms have readable minimum sizes bounded by the work area; `EnsureFloatingDocumentSize` separately
restores undersized floating dock hosts, which ignore per-window constraints.
`RenderWindow.Update` finishes loading/background operations even with the viewport hidden; GUI/input
live in `DrawContent`. Typography/surfaces use `Ui/Theme.cs`, Segoe UI body text and installation-provided headings.
`Ui/FormField` input/tooltip state uses full numeric ImGui IDs, not just field paths: Inspector and
multiple forms can display `Race` simultaneously.
`Ui/Studio.cs` styles native ImGui Button/Tab controls: ImGui retains IDs, disabled state, keyboard focus,
and last-item state. `Theme.Surfaces` defines the shared surface hierarchy across palettes. `RecordForm`
retains the selected section and draws the existing specialized editor in a content child, with sidebar
navigation or a popup depending on width.
When space is tight, `RenderWindow.DrawToolbar` provides the same controls in a menu; never let them
reduce viewport height to zero, because GPU-model loading depends on viewport drawing.
`--inspector-tab` prefers exact ID/name matches, then partial matches (Dialogue ≠ Dialogue conditions).
All closable windows use `Studio.BeginWindow` / `Studio.BeginPopupModal` (`Ui/WindowChrome.cs`).
These preserve native Begin/End and provide a close-button hit area of 1.5 font sizes (24 px at 100%)
within the allocated space. Do not replace this with global TouchExtraPadding: it captures adjacent tabs.
`--autotest-layout` clicks the outer edge of the enlarged area on an inactive tab and floating window,
in addition to docking/size/undo checks. `WindowChrome` temporarily uses the outer clip only for X hit-testing:
after native Begin, the content clip already excludes floating-window titles. Original clip/context/last-item are restored.
At low heights `RecordForm` keeps ID/EditorID/actions in the header and moves secondary details to tooltips;
unsaved/deleted/non-editable warnings remain visible.

Alias scripts: `Core/AliasScripts` binds QuestAdapter.Aliases by FormKey + unsigned alias ID;
`ScriptsEditor` edits quest and alias ScriptEntry values with the same controls. Missing PEX does not
block saved properties. Removing an alias removes its binding, clears local scalar references, and
clears array elements without shifting indices; external quests are unchanged.
`--autotest-aliasscripts <scratch-dir> [quest EditorID]` checks value types, identity, other VMAD preservation,
undo/redo, removal, roundtrip, and scratch save/reload; it does not change the active document/file.

Collision pins published Bepu **2.5.0-beta.33** (prerelease): stable 2.4 has an unbounded face walk that
writes beyond an unmanaged buffer. Do not restore 2.4 or disable collision: upstream fixes traversal
with a visited set and bounded steps. Game-independent `stbedit-cli collisiontest` checks cube edges and
the problematic cloud from [upstream #325](https://github.com/bepu/bepuphysics2/issues/325), bounds preservation,
unique/valid edges, and positive dynamic-body inertia.

## Unattended checks

The application can run a scenario and exit, supporting unattended rendering/UI checks.

Capture readiness waits for models the renderer can load: deleted and hidden references are excluded.
This also applies to MCP screenshot readiness. Check captures with **Display → Disabled** off and with
hidden object groups; neither should wait indefinitely for deliberately unloaded models.

| Flag | Behavior |
|---|---|
| `--screenshot out.bmp` | Capture the whole window after loading and exit, without dialogs/MessageBox |
| `--cell <EditorID\|FormID>` | Open a cell and wait for all meshes/textures to load |
| `--camera dx,dy,dz,yaw,pitch` | Camera relative to scene focus (degrees; yaw 0 = +Y); `=x,y,z,yaw,pitch` uses world coordinates |
| `--pick fx,fy` | Select at viewport fractions; log `pick:` |
| `--select <FormID>` / `--search <text>` | Select a record / filter Object Window |
| `--layout world\|records\|classic` | Explicitly apply a panel layout without changing scene, selected records, or edits |
| `--autotest-layout` (with `--cell`) | Edit a reference, open form/comparison → Records (check actual docking) → Classic → World → undo/redo; preserve scene/selection/documents, end with 0 unsaved, write nothing |
| `--no-markers` | Hide markers |
| `--render opaque=0,blended=0,markers=0,shading=N,particles=0,animate=0,psys=<name>,logblended=1,collision=1` | Isolate rendering passes |
| `--psys <name>` | Draw only particle systems with this name, including Preview |
| `--object-shot <FormID> out.bmp [size] [yaw] [pitch] [zoom]` | Close-up base-object Preview render; wait for assets and play ~1 s of animation |
| `--autotest-save <dir>` (with `--cell` and `--pick fx,fy`) | Edit → save → reload on a copy; restore `settings.json` afterward |
| `--autotest-new <dir>` | New interior + object, new world + objects (including a nonexistent cell) → save a new plugin in dir → reload; log `autotest-new:` |
| `--autotest-land <dir>` (with `--cell`) | Hill at the central cell edge, Ctrl+Z / Ctrl+Y, paint, save new plugin, reload, compare height; log `autotest-land:` |
| `--autotest-landview <dir>` | New world with flat terrain, hill, snow, grass, smoothing for screenshots; save nothing |
| `--autotest-dup <dir>` (with interior `--cell`) | Duplicate STAT → catalog → place → save → reload reference to new record |
| `--autotest-nav <dir>` (with interior `--cell`) | Delete navmesh triangle, rebuild from three vertices, move vertex, Ctrl+Z/Y, save, reload, compare |
| `--dialog newcell\|newworld <plugin.esp>` | Open creation dialog with temporary active file for screenshots |
| `--autotest-fly <n>` (with exterior `--cell`) | Fly east one cell every 3 s; log process/GC/video memory and cache sizes under `autotest-fly:`; memory must stay flat |
| `--video-budget <MB>` | Override GPU memory budget to check cache eviction on large maps |
| `--reference-window` (with `--select` or `--pick`) | Open selected reference window for screenshots |
| `--autotest-primitives <dir>` (with `--cell`) | New unsaved active file in dir; each Create menu item once, XPRM from edited record, portal between two rooms and room join; undo everything |
| `--autotest-brushes` (with exterior `--cell`) | One stroke per new landscape mode: set height, raise/lower to, noise, max-opacity paint, erase, remove grass; check and Ctrl+Z; end with 0 unsaved |
| `--autotest-marquee [nav]` (with `--cell`) | Real Shift+drag using synthetic mouse; exact objects/triangle indices, press/release cleanup, selection retained after Shift release, unchanged camera/records/undo; wrong result exits with error |
| `--autotest-navtools` (with `--cell`) | Fill + inverse fill = all triangles, water flag and Ctrl+Z, rotate internal edge (Check NavMeshes before/after) and Ctrl+Z, path from triangle 0 to farthest connected; save nothing |
| `--autotest-gizmo [move]` (with `--cell`) | Select object, rotation/move gizmo, find ring/arrow with synthetic mouse (`ImGuiLayer.TestMouse`), drag; log before/after rotation/position |
| `--autotest-door [ask]` (with interior `--cell`) | Leave through teleport and return; `ask` waits for the actually drawn confirmation and presses Enter, rather than polling popup IDs from another ImGui context |
| `--autotest-zoom` (with `--cell`) | One synthetic wheel notch over viewport; log camera displacement and frame count |
| `--lang en\|ru` | UI language for this run, without changing settings |
| `--autotest-form <dir> [NPC FormID]` | Form edit through tabs' `EditTarget`: height 1.000123, skill, enum, new inventory row → save → reload; log `autotest-form:` |
| `--autotest-picker <NPC FormID> <text> [click\|show\|browse]` | Synthetic click Race field in form, type with `ImGuiLayer.TestText`/`TestKeys`, choose suggestion by arrow+Enter or click; `show`/`browse` leaves list / ▼ open for screenshot |
| `--open-form <FormID>` / `--inspector-tab <tab>` / `--expand-forms` | Open form, tab (English name), expand list rows for screenshots |
| `--show-help` / `--show-settings` | Open Keys and features / Settings for screenshots |
| `--autotest-ops <dir>` (with interior `--cell`) | New MISC with name and GLOB, remove new record, copy/paste reference, save/reload; then remove own record, save/check |
| `--autotest-filter <plugin> [conflicts]` (with `--type SIG`) | Object Window plugin/conflict filters: row counts, checking time, first conflicts' fields |
| `--autotest-useinfo <FormID\|EditorID> [goto]` | Use Info: confirmed/rejected/placed counts; `goto` opens first object's cell and checks selection |
| `--autotest-batch <dir>` (with interior `--cell`) | Set Persistent on three references (Ctrl+Z/Y), replace one base, copy MISC as override → save/reload/check |
| `--autotest-findtext [text]` | Search Skyrim.esm/catalog, replace all matches in one step: line under its topic, service string (condition variable) unchanged; undo |
| `--autotest-comparecopy` | Copy keywords/weight from oldest armor version to active-file version (columns run first plugin to winner); copy one armor's weight to two others; undo all |
| `--autotest-compact <dir> <plugin>` | Copy plugin as active file; Compact FormIDs with ESL: backup, replace, reload; all own records in 0x800–0xFFF with Small flag |
| `--autotest-bsa <dir>` | New active plugin in dir, adjacent meshes/textures/.pex/.psc; Pack BSA: Mutagen reads main/texture archives with expected byte-identical files; excludes .psc |
| `--autotest-clean <dir> <plugin>` | Copy active plugin; Check and clean errors/ITM/UDR; remove ITMs in one step (check Ctrl+Z/Y), save copy lacking exactly those overrides |
| `--autotest-finalize <dir>` (with interior `--cell` containing door) | New active plugin; Finalize cell (CK door triangle), delete triangle + Finalize changed (door XNDP follows index); save/reload: navmesh, door, NAVI agree |
| `--autotest-cellprops <dir>` (with interior `--cell`) | New active plugin; cell properties: ambient (viewport changes immediately), music, owner, flag, name; Tamriel climate; save/reload header-only cell/world (0 references) with new values |
| `--autotest-dialogue <dir>` (with `--inspector-tab Dialogue`) | New active plugin; DA01 Dialogue: vanilla-topic line (text, second response, goodbye, condition), new line, topic priority, new branch/topic/line, compiled End fragment; save/reload: only our lines in topic; new branch/topic/line present and visible on quest |
| `--autotest-fragments <dir>` | New active plugin; new Start Game Enabled quest with alias/stage; broken fragment fails, valid fragment compiles (QF_ beside plugin), quest has fragment and `Alias_` property; new source-window script attaches after compiling; save with SEQ, reload/check; writes only to dir |
| `--autotest-package <dir> [EditorID]` (with `--inspector-tab "Package Data"` / `"Schedule"` / `"Procedure Tree"`) | New package in scratch active file: Sandbox through template-picker logic, schedule, input, location kind, Wear Sleep Outfit; read back, undo/redo; procedure tree root, nested branch, following branch, move, delete, read back; also opens supplied EditorID; save nothing, restore settings.json |
| `--autotest-scene <dir> [EditorID]` (with `--inspector-tab "Phases and Actions"` / `"Actors"` / `"Scene"`) | New scene in scratch active file for `dunFellglow_ReleaseDogs`: two actors/phases, dialogue with new scene topic/line, package, timer, phase inserted first; read back, undo/redo; also opens supplied EditorID; save nothing, restore settings.json |
| `--autotest-aliasuse [quest] [alias ID]` (with `--inspector-tab "Quest Aliases"`; quote spaced names in PowerShell `Start-Process`: `"\"Quest Aliases\""`) | Quest form with selected alias and Use Info (default MS13, alias 1 Arvel); log uses; change nothing |
| `--autotest-texts <dir> [plugin]` | Export plugin texts (default The Heart Of Dibella), “translate” every fifth, import into scratch active file in dir; undo/redo; save nothing, restore settings.json |
| `--autotest-navsplit <dir>` (with interior `--cell`) | Scratch active file; triangle 5 of first navmesh by ID, then move one third of its triangles into new navmesh (one undo step); undo/redo; save nothing, restore settings.json |
| `--autotest-comparefile <plugin> <other-file> [dir]` | Compare with another file and log differences; with dir, create delta patch in scratch active file and Ctrl+Z; restore settings.json |
| `--autotest-levelmerge <dir>` | Scratch active file; find leveled lists to merge and write in one undo step; undo/redo; save nothing, restore settings.json |
| `--autotest-bodytint <dir> [NPC]` | Scratch active file; fix NPC body tint (default aaMZa_Imp_Fire from STB.esp) through check-window logic; undo; save nothing, restore settings.json |
| `--autotest-heightmap <dir>` (with exterior `--cell`) | Export loaded cells' heightmap, import copy with range +1000 in one undo step, check height; undo; save nothing |
| `--autotest-vcolor` (with exterior `--cell`) | One red vertex-color stroke, radius 1500, at view focus; log color; save nothing |
| `--autotest-face [NPC FormID]` (with `--inspector-tab Appearance`; default Orgnar) | NPC face editor: “release” two sliders at 1, choose second preset of first tint with presets; rebuild preview (`%TEMP%\stbeditor_face_before.bmp` / `_after.bmp`), record changed, two Ctrl+Z; save nothing |
| `--autotest-dialoguegraph [quest]` (with `--inspector-tab Dialogue`; default MQ101) | Quest dialogue graph for screenshot |
| AI / MCP | See [AI-BRIDGE.md](AI-BRIDGE.md): `AiBridge: true`, HTTP JSON-RPC at `127.0.0.1:47110/mcp`; separately check `AiBridgeEdits`/`AiBridgeScripts` (default false), apply/reject proposals; restore settings.json |
| `--bos` | Enable Base Object Swapper preview for screenshots; screenshot log `BOS N swapped` |
| `--fade-picked [2]` (with `--pick fx,fy`) | Apply key `1` once (translucent) or twice (hidden); log `fade-picked` |
| `--autotest-findplaced <text> [world]` (default Tamriel) | Cell View → Find placed objects: log count and first rows |
| `--autotest-frameworks [EditorID]` (default aaMZp_DamageWeaponsNPC; NPC EditorID opens its form with `--inspector-tab "Spells and perks"` and SPID gifts) | Read configs, record Use Info Frameworks tab, SPID/KID lines and recipient counts |
| `--autotest-filtereddialogue [NPC]` (default Belethor) | NPC dialogue: line/bound counts and first three |
| `--autotest-lineorder <dir> [quest]` (default MQ101) | Scratch active file; move first line down in first topic with three lines, log order before/after; two new lines, remove last, reject vanilla removal; Ctrl+Z to zero; restore settings.json |
| `--autotest-assets [plugin]` (default Dawnguard.esm) | Plugin assets: counts by kind/source, first missing files |
| `--autotest-export <dir> [plugin]` (default Dawnguard.esm) | Export every data kind; log files/rows; check ModGroups collapse with synthetic versions |
| `--autotest-script` | Run script counting voiceless NPCs and adding 1 to IronSword damage through Run-button logic; output, edited damage, Ctrl+Z; no Documents examples, save nothing |
| `--autotest-region [dir]` (with exterior `--cell`) | Regions window/borders/tool; select region at view center, top-down `%TEMP%\stbeditor_region.bmp`; move first corner 500 east, create region in scratch active file in dir, two Ctrl+Z; restore settings.json |
| `--autotest-sky [WTHR EditorID]` (with exterior `--cell`; default SkyrimClear) | Weather lighting at 6:30, 12, 19, 23; screenshot each (`%TEMP%\stbeditor_sky_<hour>.bmp`), log lighting, then restore default light |
| `--autotest-voice <dir>` | MQ101 Voice files counts by state; turn one response into stereo WAV in dir from installation .fuz; Create .lip and pack .fuz produces both in dir; writes only there |
| `--autotest-idletree` | Idle animations window; under first idle with three children move first down via menu logic, log rebuilt order, `%TEMP%\stbeditor_idletree.bmp`, Ctrl+Z and order again; save nothing |
| `--autotest-perktree [AVIF EditorID]` (with `--inspector-tab "Perk tree"`; default AVOneHanded) | Skill form/perk tree, `%TEMP%\stbeditor_perktree_before.bmp`; move node 1 half-column right/up, toggle 1 → 2, add node from 1 through canvas logic; log record tree, `_after.bmp`, three Ctrl+Z; then As in game with six ranks at skill 40 (`_game.bmp`, game skydome), `sky` log: skill slot, width, menu settings, node 1 world star and inverse grid conversion (must match); save nothing |
| `--autotest-navgen` (with interior/exterior `--cell`) | Generate navmesh with default panel-button logic; log triangles before/after and Finalize result, top-down `%TEMP%\stbeditor_navgen_after.bmp`, Ctrl+Z to zero unsaved; save nothing |
| `--autotest-havok` (with `--cell RiverwoodSleepingGiantInn`) | Raise six hearth pots/plates by 10, tilt 20°, Run Havok Sim; compare resting position with original (height ±0.5, no tilt), `%TEMP%\stbeditor_havok_after.bmp`, Ctrl+Z; save nothing |
| `--autotest-facegen <dir> [NPC FormID]` (with `--cell`; default Orgnar in `RiverwoodSleepingGiantInn`) | Scratch active file; face with CK head (`facegen_before.bmp`), Ctrl+F4 FaceGen export, files/readback, reload head, final screenshot; restore settings.json |
| `--autotest-place <dir> [base FormID]` (with `--cell`) | Place base (default XMarkerHeading), reselect new reference through `select` using its `get_status` MCP ID; model/picking/Inspector, gizmo/CK rotation, LMB Rotate without translation and bounded nonzero Move without rotation; Inspector EditorID, scratch save/readback, Inspector after reload. Hover handle before press; zero Move fails. Restore settings.json |
| `--autotest-palette <dir>` (with `--cell`) | Scratch active file; three rock piles in palette, five objects around focus with random selection/rotation/scale; save nothing, restore settings.json |
| `--autotest-layers` (with `--cell`) | Layer of 40 cell statics: hide (excluded from drawing/picking), show/freeze; remove test layer afterward; remove empty layers.json if originally absent |
| `--autotest-storymanager` | Build Story Manager tree, log node/event counts; move first node down under branch with two children, before/after order, Ctrl+Z |
| `--autotest-quest [EditorID]` (with `--inspector-tab "Quest Stages"` / `"Quest Objectives"` / `"Quest Aliases"`) | Quest form through EditTarget: new stage/log/condition, change stage index (resort), objective with map target, specific-reference alias; write/read quest; undo/redo all; save nothing |
| `--autotest-conditions` (prefer `--inspector-tab Conditions`) | Perk conditions through form EditTarget: function, parameter, operator, value, OR, global, new row/move; copy/paste to another perk; undo/redo all; save nothing |
| `--autotest-refmore` (with interior `--cell`) | More properties on two references: fields, ignored by sandbox and encounter zone on both; two Ctrl+Z restore; save nothing |
| `--autotest-together` | Three armors in Edit together: weight 9.5 in all, Ctrl+Z restores |
| `--autotest-sheet <SIG>` | Type table (WEAP / ARMO / AMMO …): rows, empty cells by column (wrong path leaves whole column empty), weight edit/undo |
| `--autotest-masters <dir>` | New plugin with unused Update.esm master in middle → save → Clean Masters → save: Update.esm removed, Dawnguard links intact |
| `--autotest-savecheck <dir> <plugin>` | Copy existing plugin, override from former non-master, prepare save; leave new-master confirmation for screenshot. Original/copy SHA-256 unchanged before consent; validation temp file created. Missing confirmation/suitable data fails instead of waiting forever |
| `--ui-scale <factor>` | UI scale for this run (`1.3` = 130%) |
| `--theme <name>` | Theme for this run (`Theme.Palettes`, e.g. `Cobalt`, `"Windows Light"`) |
| `--autotest-compare <FormID> [FormID...] [versions\|records]` | Compare first record's versions, then select all in Object Window and compare; log `autotest-compare:` color counts/differing rows; `versions`/`records` keeps only one window for screenshot |

Convert BMP to PNG with System.Drawing or another converter.

Recorded pre-0.77.0 QA run, 2026-11-11: Skyrim SE 1.5.97, STB profile, Windows 11 x64.
- 65 main windowed scenarios; failures found were fixed and checked separately. Additional checks:
  Move gizmo and delta patch with added/deleted/changed records and undo.
- 2566 fields: 0 unexpected differences, 0 Set errors; 98 known format limitations.
  401 condition functions round-tripped, 2100 valid field copies, 33502 NIFs and 15113 PEXs without read errors.
- Independent MCP Python SDK 2.3: five permission configurations/migrations, new reference IDs, edits/undo,
  screenshot, proposal apply/reject through real UI input, C# denial and permission revocation before commit.
- Output stayed in TEMP outside game/mod folders; settings/layout/layers restored byte-for-byte. This does
  not cover all profiles/data variants; source-installation warnings are not claimed as editor fixes.
  Optional Claude Code CLI was not run; independent MCP access was checked.

`stbedit-cli` commands:

| Command | Behavior |
|---|---|
| `plugins` | Load order, indices, read errors |
| `catalog` | Build catalog/cache |
| `find <text> [--type <signature>]` | Records by EditorID/name, optionally limited to one record type |
| `assets <paths…>` | Winning file source: loose or specific BSA |
| `extract <asset> <file>` | Extract respecting priorities |
| `bsaextract <archive.bsa> <path> <file>` | Extract from specific BSA, e.g. vanilla rather than mod |
| `nif [prefix]` | Read all NIFs (~33k, ~20 s): errors/warnings, particle/billboard statistics |
| `nifdump <mesh>` | One NIF's shapes, materials, animation, particles |
| `nifblocks <mesh\|file>` | NIF blocks (type/size/name), header strings |
| `facegenprobe <NPC> [plugin]` | NPC head parts, meshes/TRI morphs, CK FaceGen |
| `facegencheck <NPC\|#N> [--plugin X] [--faces] [--bad]` | `FaceGenMorphs` vertices versus CK FaceGen by part type (335 of 353 vanilla faces within 0.06) |
| `voicecheck [plugin] [quest-filter]` | Editor-derived voice filenames/speakers versus installation: name matches, found/missing/extra voices, speakerless lines |
| `idletree [depth] [filter]` | Idle roots, action parents, graph summary, upper levels |
| `hulltest <interior EditorID>` | Build convex hulls for cell mesh collision pieces individually to locate Bepu crashes |
| `nifcollision <mesh-or-EditorID…>` | Havok convex pieces: counts, completeness, bounds versus mesh |
| `roomcheck [plugin]` | Interior room/portal issues by kind; default Skyrim.esm |
| `ls <dir> [extension]` | Loose/BSA assets below directory and each source |
| `frameworks [EditorID…]` | Config references by kind; named records' lines, SPID NPC match counts; NPC SPID gifts |
| `filtereddialogue <NPC EditorID>` | Possible NPC lines, condition-bound count, first entries |
| `mergeplan <signature> [n]` | Merged Patch additions/removals by record kind (LVLI, CONT, NPC_, OTFT, FLST…) |
| `navcheck <interior EditorID…>` | Check NavMeshes issue counts by kind |
| `deletednav` | Installation plugins excluding base .esm with deleted navmeshes |
| `mastersorttest <out> [plugin]` | Save copy with unordered masters through Sort Masters; load-order masters and intact records |
| `formidtest <out> <plugin>` | Change most-referenced own record's FormID in copy; old absent/new present, all links updated |
| `dialremovetest <out> <plugin>` | Remove one own topic line through Delete line logic; exactly one fewer, others intact |
| `perktree <AVIF EditorID>` | Skill tree nodes: column/row/offsets/FNAM/perk/links |
| `landseam <world> <x> <y> [radius]` | Seam near game `getpos`: four cells at nearest corner, LAND owners (height/color/normal hashes by plugin), cell water/type/changers by plugin, quadrant textures, shared-edge differences (heights/normals/colors/texture weights by diffuse file; approximate layer blending, 0.3–1.0 differences also occur in vanilla), each LAND version's normals versus neighbors (whose normals stop matching), point height and nearby objects |
| `landseamfix <world> <corner-X> <corner-Y> <plugin.esp>` | Texture-seam patch at cell corner (grid units: 28 8 = 114688, 32768): at each vertex on four shared edges, meeting quadrants receive one texture blend by recomputing existing layer opacities (base, then ordered overlays); only those vertices change; copy winning LAND, read back/compare plugin |
| `prims [plugin]` | Reference XPRM by type/base: counts, non-unit scales, example full sizes (not half-extents)/positions |
| `navcover <interior EditorID> [N]` | Triangle centers beneath opaque geometry 2..80 units above, threshold counts, N deepest places with covering object |
| `navgen <interior EditorID> [--cs N] [--radius N] [--climb N] [--big N] [--obj f.obj] [--game-obj f.obj] [--near x,y,z]` | Generate without writing; compare game navmesh triangles/area/traversal/mutual coverage; `--big` is retained doorless-piece area (0 keeps door-connected only); `--near` identifies meshes beneath point |
| `facegensliders #N` | Fit face-slider morph weights to CK files across NPCs, compare values to check direction table |
| `facegentint #N [--plugin X] [--layers]` | `FaceGenTint` versus CK; `--layers` identifies layer types CK actually applies |
| `facegenbuild <NPC> <dir> [--winner]` | Export outside game, compare CK shapes/vertices/shaders/textures/colors/blocks/tint |
| `scene <cell EditorID> [--grep text] [--shaders]` | Build cell scene, find objects |
| `find <plugin name> --plugin` | First named records added or overridden by that plugin |
| `npc <FormID\|EditorID>` | Appearance meshes: armor/ARMA/slots, FaceGen, file sources |
| `npcscan [n]` | Build all NPC appearances: missing appearance/FaceGen/meshes |
| `reflect <Mutagen-interface>` | Interface properties (e.g. `INpcGetter`) without guessing APIs |
| `sweep [n]` | Build n random cells to find crashes |
| `savetest <game> <cell> <dir> [plugin]` | Save copied plugin; 0 untouched-record differences |
| `recordtest <game> <dir> <plugin>` | Write/read blank record of every creatable type; 100+ new records, own-record/override removal on copy |
| `cleanmasters <game> <dir> <plugin> [add.esp]` | On copy: unused masters, Clean Masters save (with addition); save comparison must be clean |
| `papyrustest <game> <dir> [QF_script]` | Find compiler/imports/flags; parse/rewrite all installation `QF_` / `TIF__` / `PF_` / `SF_*.psc` unchanged; edit/compile real quest fragment into dir; broken code reports line error |
| `looksprobe <game> <NPC EditorID>` | Appearance fields (race/head parts/morphs/tint/weight…) in each version, exact differences |
| `findtest <game> <plugin> <text>` | Text search in all plugin fields/catalog: counts/types/time |
| `copytest <game>` | Each overridden field of 14 types copied oldest → winner copy; target field matches oldest, others winner |
| `compacttest <game> <plugin> <dir>` | Copy: own IDs in 0x800–0xFFF, same count, 0 dangling own links, each record equals source with remapped links; byte-preserved count |
| `bsatest <game> <vanilla.bsa> <dir>` | `BsaWriter.Hash` versus all vanilla folder/file names; write archive of 300 files and read byte-identically with Mutagen |
| `itmtest <game> <plugin>` | ITM/deleted references against master, timing; 300 mixed-type self-equality checks |
| `checktest <game> <plugin>` | Check for Errors grouped errors/warnings |
| `udrtest <game> <dir>` | Deleted vanilla reference found/fixed: undeleted, initially disabled, z −30000, inverted player enable parent |
| `finalizetest <game> <comma,separated,interiors> <world> <x> <y>` | Vanilla-copy Finalize versus CK: interior door triangles/XNDP; central cell edge links in 3×3 grid cleared/rebuilt |
| `navprobe <game> <cell>` | Game navmesh/door/NAVI relationships; NAVI constants Unknown2 = CRC A5E9A03C, doors E48B73F3 |
| `packprobe <game> [EditorID…]` | Package input/target classes, template/procedure statistics; named package template/named inputs/tree/schedule |
| `packtest <game> <dir>` | Flag bits (Wear Sleep Outfit = 29); every procedure list parses into one tree; new Sandbox template package writes matching vanilla `DefaultSandboxCurrentLocation256` subrecords except own values, reads back |
| `sceneprobe <game> [EditorID]` | Vanilla phase widths/service blocks/action kinds/actor flags/dialogue defaults; one Skyrim.esm scene's subrecords |
| `scenetest <game> <dir>` | New-plugin `dunFellglow_ReleaseDogs` from `Core/Scenes` matches vanilla subrecords; phase insertion/removal shifts actions |
| `fragprobe <game>` | Vanilla package/scene fragment binding versions/phase flags, PF_ / SF_ sources |
| `fragtest <game> <dir>` | All package/scene/perk fragment tables reference source functions; new code compiles in dir, binds to copies, reads back; phase insertion shifts phase fragments |
| `aliastest <game> [quest] [alias-ID \| all]` | Alias uses in quest/scenes/packages/dialogue (default guard in `dunFellglow_ReleaseDogs`); `all` checks every alias |
| `texttest <game> <plugin> <dir>` | Plugin texts → JSON → identical readback; “translate” every seventh, apply to own-record copies (lines under topics), write/read translated records |
| `navsplittest <game> [n]` | Move one third of n interior navmeshes' triangles into new mesh (`NavmeshTools.MoveTriangles`); counts/angles/flags preserved, each link joins shared-edge triangles |
| `difftest <game> <plugin> <other-file>` | `PluginDiff`: only here/there/changed with fields |
| `mergetest <game> [patch \| -] [dir]` | Leveled-list merge across load order excluding named patch; with dir, writer saves new patch/readback |
| `mergeshow <game> <EditorID>` | All item leveled-list versions and merge result |
| `tintprobe <game> [plugin]` | NPC QNAM agreement with CK skin-tone-layer formula (`BodyTint`) |
| `dialtest <game> <dir> [plugin]` | Writer: vanilla-topic line, new line, topic priority → exactly two lines in topic; optional plugin uses copy, lossy 0 |
| `condtest <game>` | CK descriptions for all PERK/MGEF/PACK/QUST/IDLE/COBJ/SPEL/LSCR conditions, 0 errors; each ~400 function assigned to perk condition, alternating global, write/read agreement |
| `usetest <game> <EditorID\|FormID>...` | Use index cold/cache time, reference counts/users; regular-record matches checked against Mutagen links (FALSE means false positive hidden by UI) |
| `strtest <game> <EditorID>` | Name by plugin; check MO2 mod-master .strings lookup |
| `mastertest <game> <dir> <plugin>` | Copy: edit record from non-master plugin, save with new master, list non-byte-identical records accounting for FormID shift |
| `newtest <dir> [plugin]` | New cell/world/grid cells/objects into new plugin or copy; lossy must be 0 |
| `landtest [n]` | LAND → `LandscapeData` → LAND for n cells: heights/layers match, normal statistics |
| `proptest <dir> [plugin]` | Reference lock/owner/linked ref/flags/teleport/script, STAT override, own-record edit: save/read |
| `pex [script]` | Read all .pex (~15k, ~1 s) or one script's properties with parents |
| `vmadfind <signature>` | Base records of type with scripts |
| `navtest [n]` | Rebuild n lookup grids/adjacencies as editor; grid superset of game's, adjacency identical |
| `mathtest`, `cellat` | Rotation math (including mouse rotation held at 180° without translation), cell lookup |
| `formtest <dir> [n] [signatures…]` | **Edit all forms**: n records/type, every editable field via `RecordFields` like UI, add each list row → new plugin → readback. `unexpected` must be 0; `format` denotes Inspector `!` limitations |
| `plugins --only a.esm,b.esp` | Load listed plugins and masters, like Data selection |
| `memprobe <exterior-cell EditorID>` | GC memory after 5×5 loading stages (world persistent cell, cells, scene), to identify retained memory |

## UI translation

Code uses English strings: `T("Text")`, interpolation `TF($"{n} objects")` (key `"{0} objects"`),
and window titles `Title("Name")` (`"Translated text###Name"`, preserving language-independent layouts).
Russian is the English → Russian dictionary `src/Editor.App/Ui/LangRu.cs`. Missing translations remain
English without breaking functionality. The application remains bilingual.

```powershell
python tools/loc.py missing          # Translate missing LangRu.cs keys before committing
python tools/loc.py wrap <file.cs>   # Wrap a new file's visible strings in T()/TF(); --dry only lists
```

`missing` prints missing keys to stdout and their count to stderr; it exits 1 when keys are missing,
0 otherwise. Label tables in `static readonly` arrays are translated when drawn (`T(label)`), not at declaration.

## Hard-won invariants

- **Never write into users' game/mod installations during tests**: use copies in a separate directory.
- Plugin text uses per-string encoding (UTF-8 with cp1251 fallback); see `StringEncodingDetector`.
- Never call untyped `LinkCache.TryResolve(key)` / `ResolveAllSimpleContexts(key)`: they index a million REFRs
  (19 s). Use signature-typed `RecordLookup`. Do not locate placed references through Mutagen;
  doors use `DoorIndex` (raw file scan, ~1 s).
- **Traverse plugin groups once.** Mutagen overlays read group bytes on every traversal; each returned
  record retains that copy. Per-cell world traversal retained a whole Skyrim.esm Tamriel copy (~200 MB)
  per cell: 5 GB for 5×5 and freezes on 16 GB machines. Use per-plugin `RecordLookup.ModCells` indices.
  Checks: `memprobe`, `--autotest-fly`.
- **Record fields go through `Core/RecordFields`**: traversal/editability/path `Set`/row insertion.
  UI (`FormView`, `PropertyTree`, `FieldEditors`) must not mutate Mutagen objects directly, so `formtest`
  exercises the same operations. Struct edits clear Break flags (`Unbreak`), otherwise Mutagen omits
  later fields. New rows initialize required nested parts (`FillRequired`).
- **Conditions (CTDA)**: `Core/Conditions.cs`. A Mutagen condition function is its data *class*
  (`GetIsIDConditionData`, one class/function, properties as parameters). Use Global is the condition
  class (`ConditionGlobal` rather than `ConditionFloat`); alias/package-data parameters are data flags;
  form parameters use `IFormLinkOrIndex` (link or index). `SetFunction` creates new data of the class;
  `WithGlobal` replaces the list item. `Ui/ConditionsEditor.cs` hooks into every `FormView` condition list.
  Checks: `condtest`, `--autotest-conditions`.
- **Papyrus**: `Data/Papyrus.cs`, `PapyrusSetup` finds compiler/source directories by MO2 priority including
  disabled mods, outputs beside plugin, invokes compiler/parses `file(line,col): text` errors.
  `Core/QuestFragments.cs`: `FragmentScript` parses/writes CK format; `QuestFragments.Merge/Apply`
  manages fragment tables and VMAD `Alias_` properties. UI: `Panels/QuestEditor.Fragments.cs`,
  `Panels/ScriptSourceWindows.cs`. New .pex is immediately available through `AssetFileSystem.AddLoose`
  (replace entire map because loading threads read it) and `ScriptLibrary.Forget`.
  `Data/Raw/SeqFile.cs` writes SEQ in `SaveController.Commit`.
- **Text search**: `Core/TextSearch.cs` visits `RecordFields` leaves of every plugin record; dialogue
  lines retain topics for editing. `Display` denotes translatable text, the only kind replacement changes.
  UI: `Panels/FindTextWindow.cs`.
- **Field copy**: `Core/MemberCopy.cs` uses `DeepCopyIn(…, ErrorMaskBuilder, TranslationCrystal)` with
  exactly one field enabled (null gendered-pair entries mean copy). Mutagen still copies race skeletons;
  snapshot before copying and restore all unrelated changes afterward. Version columns run first plugin
  to winner; Take value targets the edit or winner.
- **FormID compaction**: `Core/FormIdCompactor.cs` computes new IDs only outside 0x800–0xFFF, applies
  `Duplicate` at the record's actual location (groups, cells/children, worlds, topics/lines), then `RemapLinks`.
  `PluginWriter.Compact` reads/writes the whole plugin with the same masters, using `RawSplice` for untouched
  records: those neither moved nor referencing remapped records.
- **BSA**: `Assets/BsaWriter.cs`, version 105, folder/file-name flags, uncompressed data, ascending
  folder/file hashes. Folder offset = its block + total filename length (format peculiarity).
  UI: `Panels/ArchiveWindow.cs`.
- **Check and clean**: `Core/PluginChecks.cs`. ITM compares against nearest *master* version like xEdit,
  ignoring header service fields/compression; CELL/WRLD/DIAL containers compare headers only.
  Mutagen compares GenderedItem pairs (armor models/race data) by reference, so unequal results for those
  types compare all `RecordFields.Leaves`. Errors check missing links only: Mutagen's stricter link types
  cause false positives for condition parameters. Skyrim.esm IDs below 0x800 are engine-defined.
  FaceGen is needed for NPCs with FaceGenHead races and no Traits template. UI: `Panels/PluginCheckWindow.cs`.
- **Navmesh Finalize**: `Core/NavmeshFinalize.cs` follows game data: door triangle lies under arrival point,
  i.e. the *other* door's teleport marker; door XNDP references it. Edge links pair collinear overlapping
  edges one-to-one, type 0. Each plugin NAVI contains only its own navmeshes; the game merges them.
  `Panels/RenderWindow.NavmeshFinalize.cs` commits meshes/doors/NAVI in one undo step.
  Checks: `finalizetest`, `--autotest-finalize`.
- **Cell/world headers**: `RecordPlace.Header` drafts copy without references through `HeaderCopies`;
  `WorkingCopy` does this automatically for CELL/WRLD. Writer applies fields to plugin cell/world at the
  end using masked `DeepCopyIn`. Open-interior lighting reads edits through `SceneBuilder.InteriorLightingOf`.
  UI: `Panels/PlacePropertiesWindows.cs`.
- **Dialogue**: INFO lines are outside catalog. `Data/DialogueIndex` builds quest → topics/branches in
  background (~0.2 s); `DialogueIndex.Lines` merges all plugins' topic lines, latest versions in appearance order.
  A line edit is `RecordPlace.DialogInfo` with topic in `Cell`; writer inserts it into plugin topic
  (header-only topic copy if absent), marking topic touched so Mutagen updates TIFC. Topic edits retain
  only lines already in plugin. UI: `Panels/DialogueEditor.cs` and `.Fragments.cs`.
- `RecordRoundTrip` writes one record to a temporary plugin/readback, identifying format normalization
  for Inspector `!` notes and rejecting edits that would break readability.
- Mesh/texture `AsyncCache` evicts assets not drawn for 20 s when over video-memory budget.
  `GpuModel` holders check `IsDisposed` and request again, as `SceneRenderer` does.
- Hexa.NET.ImGui `InputText(..., ref string)` with `EnterReturnsTrue` returns typed text only on Enter.
  Omit the flag for live searches and handle the Enter key separately (`FormField`). Keyboard navigation
  is enabled; fields needing arrows claim them with `ImGuiP.SetItemKeyOwner`.
- ImGuizmo cannot start dragging while an ImGui item is hovered in this or previous frame; omit the
  viewport button over gizmo handles. Rotation adds to its input matrix: feed back its last matrix during
  dragging. Call `ImGuizmo.SetImGuiContext(null)` before `DestroyContext` to avoid exit crashes.
- Saving: Mutagen normalizes untouched records and drops “unused” masters. `PluginWriter` keeps original
  masters and restores untouched bytes through `RawSplice`; `CompareUntouched` must report 0.
- Keep `<CETCompat>false</CETCompat>` in Editor.App.csproj: otherwise MO2/usvfs crashes at startup.
- Reference rotation is `NiMatrix3::SetEulerAnglesXYZ`; NIF triangles are CCW (`FrontCounterClockwise = true`).
- The game hides `EditorMarker` nodes by name; draw only with Markers enabled.
- NiAlphaProperty `*_COLOR` blend factors are illegal for D3D11 alpha channels and must be substituted.
- Check shader behavior against Community Shaders sources (`mods\Community Shaders\Shaders\*.hlsl`
  in the installation), rewritten game shaders. See [RENDERING.md](RENDERING.md).
- NIF fields follow niftools `nif.xml` (20.2.0.7, BS 83 = LE, 100 = SSE). Unknown blocks are skipped
  by the size table, so new block types do not break the file.

## Adding functionality

- **NIF block type for rendering**: parsing in `Editor.Nif/NifFile*.cs`, model in `NifModel.cs` / `NifEffects.cs`;
  run `stbedit-cli nif` (0 crashes) and `nifdump` on an example.
- **Rendering mode/pass**: `SceneRenderer` + HLSL in `Shaders.cs`; check `--object-shot` and `--cell … --screenshot`.
- **Panel**: class in `Editor.App/Panels`, register in `EditorApp`, place in `Panel.DefaultLayout`.
- **Reference edit**: state in `RefState` / `EditDocument`, saving in `PluginWriter`; check `savetest`.
