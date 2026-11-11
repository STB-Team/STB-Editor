# Changelog

Each `## <version>` section is the release description on GitHub (see the Release section of `docs/DEVELOPING.md`).

## Unreleased

- New workspace shell: **World**, **Records** and **Classic** layouts, quick save,
  undo/redo, search and active-file controls. Startup preserves the user's layout.
- World places the object catalog below the view; Records docks forms and comparisons as tabs. Open documents,
  the scene, selection and undo history survive layout changes; windows can be undocked.
- The render toolbar is divided into tools, snapping, display, camera and presets. All existing actions
  and shortcuts remain; secondary settings move into menus, with detailed diagnostics enabled separately.
- Segoe UI body text, a graphite STB theme and an adaptive catalog with cards and filters;
  narrow Cell View panels switch lists with tabs. Other themes, RU/EN and UI scaling remain available.
- Form searches retain input by full ImGui ID: identical fields in the Inspector and a separate form no longer
  capture each other's input. Exceptions in screenshot mode terminate the process without a MessageBox.
- `--layout world|records|classic`, `--autotest-layout`: checks layout changes with an unsaved edit,
  an open form and comparison, docking and undo/redo, without writing the plugin.
- Second styling pass: layered surfaces, edges and button states, highlighted docking tabs,
  a two-level header and tool sections in every theme, including Charcoal Dark Slate.
  Forms have a record card, action menu and section navigation sidebar (a dropdown in a narrow Inspector).
- Fixed memory corruption while building model/thumbnail collision: Bepu 2.4 could loop through a face
  and overrun an unmanaged buffer. Pinned the published `2.5.0-beta.33`, which fixes traversal;
  collision and Havok remain supported. `stbedit-cli collisiontest` checks edges, bounds and inertia,
  including the public problematic point cloud.
- At high UI scales or in a short dock, render tools collapse into menus so they do not displace the scene
  or stop streaming. `--inspector-tab` first looks for an exact section ID:
  `Dialogue` opens dialogue, not dialogue conditions.
- Tester feedback: Records gives the catalog 38% of the width, shows the type tree at normal widths,
  and places Preview as the second tab beside Inspector, without a bottom strip. The central dock updates
  after loading the saved layout; forms and comparisons no longer open squeezed into too little space.
- Readable sizes for forms and older floating dock groups; larger close targets for windows, tabs and dialogs
  (24 px at 100%). Buttons use a continuous rounded border instead of contrasting edges and strips.
- Quest alias scripts are available directly in the selected alias's properties through the shared script editor:
  attach/remove, source, auto-fill, properties of every type, arrays and alias references.
  Binding uses the quest FormKey and alias ID, not its list position; deleting an alias clears its local VMAD references.
  `--autotest-aliasscripts <scratch-dir> [quest EditorID]`: properties, undo/redo, roundtrip and save/reload outside the game.
- The bridge is generalized as **AI / MCP**: a local HTTP endpoint and JSON configuration for any compatible
  MCP client, regardless of model. Screenshots/camera, the unsaved document, FormKey, undo and proposals
  remain editor tools; Claude Code is a separate, optional, collapsed launcher.
- Separate `AiBridge`, `AiBridgeEdits` and `AiBridgeScripts` settings (false by default); old bridge settings
  migrate without inheriting C# permission. `run_script` is **not a sandbox**: it requires direct edits and
  a separate opt-in, can write files/run processes, and its external effects cannot be undone.
  Structured tools have no save operation, but there is no general guarantee that AI cannot write files.
- Repository documentation is in English and describes implemented behavior and current limitations.
  `AGENTS.md` replaces the previous agent instructions; redundant planning documents have been removed.
  [docs/AI-BRIDGE.md](docs/AI-BRIDGE.md) replaces the former Claude-specific guide.
  All 11 documentation images were refreshed from the current editor and renderer.
- Screenshot readiness excludes hidden and deleted references, matching the renderer's loading rules.
  Headless and MCP captures no longer wait indefinitely for models deliberately excluded from rendering.
- Extended checking exposed missing confirmation for new masters when `LossyRecords` was zero:
  such saves now always await consent. Confirming only master changes does not disable future
  normalization warnings; `--autotest-savecheck` checks that the original and copy remain unchanged.
- Shift-marquee takes precedence over the gizmo/Alt-orbit. Marquee checks wait for a real press/release
  and verify the exact selection without edits; placement checks no longer accept a zero Move as success.
- Fixed MCP IDs for new references in a plugin not yet loaded: the canonical FormKey can be passed
  back to `select`, movement, deletion and proposals. Placement checks cover this cycle.
- MCP negotiates a supported protocol version instead of echoing an arbitrary client version;
  previously granted permissions can be revoked without re-enabling the bridge/direct edits.

## 0.76.0

Tester feedback, CK main toolbar:
- **Create** menu in the render window (level-editing buttons from CK's main toolbar): **trigger** (choose an activator,
  with triggers listed first), **multibound** box and sphere, **occlusion plane** and **occlusion box**, **room** (Roombound), **portal**,
  **Link portal to room**, **Unlink portal**, **Join rooms**, **collision** plane, box and sphere,
  **acoustic space** and **sound emitter** (choose a base). Bases, colors and other fields match vanilla
  references in Skyrim.esm (`stbedit-cli prims`): RoomMarker / PortalMarker / CollisionMarker / PlaneMarker; rooms
  are persistent, and multibounds use RoomMarker with the multibound flag. A new primitive encloses the selection,
  or appears at the center of the view if nothing is selected; planes face the camera. Collision layer is on the reference window's Other tab.
- Acoustic spaces (ASPC) can be placed in the world.
- Not implemented: Join/Break Occlusion Planes and water currents (Skyrim.esm has no current primitive to use
  as a reference).
- For developers: `--autotest-primitives <folder>`.

## 0.75.0

Tester feedback, the full range of landscape tools:
- Landscape brush: **falloff** (CK: Edit Falloff %; the inner ground circle shows where the brush has full strength), new
  **Set height** (CK: Absolute Height), **Raise to** / **Lower to** (CK: Raise To / Lower To; stop at the target),
  **Noise**, **Erase** texture, **Grass off** / **Grass on** modes. Set target height in a field or with
  **Ctrl+click** on the ground. Painting also has **maximum opacity** (CK: Max Opacity).
- Grass: the texture visible under the brush is replaced with its twin using the same image but without grass (or with it),
  such as LTundra01 / LTundra01NoGrass, so the ground looks unchanged. If a quadrant already has six layers, the texture
  switches to its twin throughout the quadrant (logged).
- Texture list: usage count in quadrants of loaded terrain, and an Only used here checkbox
  (CK: Used column).
- For developers: `--autotest-brushes` (with an external `--cell`).

## 0.74.0

Tester feedback, missing object-editing controls:
- **CK-style reference window**: double-click an object in the render window to open it: Edit base, **3D Data** tab
  (position, rotation in degrees, scale; ◀ ▶ arrows move by a step, each group has its own step; Reset rotation,
  Show in view), **Flags**, **Enable Parent**, **Ownership**, **Lock** and **Teleport** (where applicable), **Extra**,
  **Linked References**, **Scripts**, **Other** (remaining fields: activate parents, emittance, multibound, location
  reference types, attach ref, reflections, primitive, rooms), and **Linked From** (references linked to, enabling or
  activating this reference; double-click to go there). Each edit is a Ctrl+Z step.
- **CK-style reference flags by base type**: the same bit means different things for different types (0x200 means
  Hidden from local map for statics, Motion blur for movable statics, Shadows for lights, Starts dead for actors). Added:
  hidden from local map, inaccessible (doors), open by default, full LOD, reflected by auto water, starts dead,
  shadows / does not light water / landscape / never fades (lights), motion blur, on ground (containers),
  ignored by sandbox. Also available in the Inspector.
- Double-click no longer focuses the camera (use **C**) or immediately asks about a door; use Go through door in the reference window.

## 0.73.0

Tester feedback (a seven-item table), first batch:
- **Primitives were drawn at twice their size**: triggers, collision, rooms, portals and multibounds. XPRM stores full
  dimensions (a 256 x 320 portal exactly fits a Nordic ruin doorway; shelf triggers 35 high are spaced 47 apart),
  but we extended those dimensions in both directions from the center.
- **Navmesh display can be disabled while editing**: editing mode previously re-enabled it every frame.
- **Marquee selection**: left-drag from empty space or **Shift+drag** from anywhere (interiors always have a wall or floor
  under the cursor) selects everything whose center is inside; Ctrl adds and Alt removes. In navmesh mode the marquee
  selects triangles (or vertices, depending on selection mode); Shift+click still places a vertex.
- **CK navmesh toolbar**: buttons above the view in navmesh mode and CK shortcuts: select triangles **T**, vertices
  **V**, edges **G** (plus the existing Auto mode); view **W** (normal / on top / navmesh only); triangle or quad from four
  vertices **A**; flood fill **F**, inverse fill **I**; water **O**, preferred path **P**, drop to ground,
  rotate edge, merge **Q**, delete **R** / Del, deselect **D**, find triangle **Ctrl+F**, Finalize
  **Ctrl+F1**, and **path test** (click the start, then the destination: shortest path through loaded navmeshes, across
  navmesh boundaries). **F now means flood fill, as in CK**; drop to ground remains a button.
- For developers: `--autotest-marquee [nav]`, `--autotest-navtools`, `stbedit-cli prims`.

## 0.72.4

- Landscape painting no longer shifts cell normals: unless heights change, normals are written back byte-for-byte.
  Previously they were normalized and re-encoded, moving by one quantization step (~0.4°) and creating lighting seams
  against neighboring cells not included in the plugin.
- For developers: `stbedit-cli landseam` (what causes a terrain seam at an in-game point) and `landseamfix` (a patch plugin
  aligning texture blending at vertices along the shared edges of four cells meeting at a corner).

## 0.72.3

- Navmesh no longer disappears under textures: it was drawn before transparent objects, and low mist (FXMistLow…,
  ~40 units above the floor, for example in Fort Greymoor's prison) covered it with dark patches. It now draws after them.
- Navmesh is pulled slightly toward the camera; areas beneath rubble and rugs (up to ~48 units) remain faintly visible,
  while walls and bars still hide it. Fade where hidden checkbox in the Navmesh panel.
- For developers: `stbedit-cli navcover <cell>` reports what covers navmesh triangles and how far above them it is;
  `--camera =x,y,z,yaw,pitch` sets the camera in world coordinates.

## 0.72.2

- Render window: editing tools on the second row are named Edit navmesh and Edit landscape. Previously they shared
  the Navmesh and Landscape names of the display toggles above them, making the duplicate buttons confusing.
  Toggle tooltips explain that they only control visibility.

## 0.72.1

- Use Info → Frameworks and Framework configs: **double-click opens the config file itself** at the relevant highlighted
  line, with Open in default application, Show in Explorer and Copy path buttons. Previously it opened the same record's
  form. Row details include Open config file and Open record.
- OAR rows now identify the condition and submod (`NOT IsActorBase — base coat of paint gesture replacers`), rather than
  `"pluginName": "Skyrim.esm"`.

## 0.72.0

- **Claude proposes; you decide.** The `propose_changes` tool previews moves / rotations / deletions without committing
  them: objects appear at their proposed positions in purple, with a banner at the bottom of the render window.
  **Enter** applies them as one undo step; **Backspace** rejects them (or use View → Claude… buttons). Also works when Claude edits are disabled.
- **Ask Claude directly in the editor** (View → Claude…): the message field launches your Claude Code (`claude -p`)
  with editor tools only; the window shows replies and tool calls, and the conversation continues.
- New bridge tools: `get_journal` (session log, undo history, last crash), `run_script` (a C# script like those in the
  Scripts window, with edits as one step), `proposal_status`.
- The connection command now uses `--scope user` (Claude Code sees the editor from any folder).

## 0.71.0

- **Claude bridge: Claude Code works with the open editor** (off by default: View → Settings → Claude).
  The editor runs a local MCP server at `127.0.0.1:47110`; connect with one command:
  `claude mcp add --transport http stb-editor http://127.0.0.1:47110/mcp`. Twenty tools: render-window screenshot
  (with annotations, waiting for loading), camera, load cell, nearby objects and objects under a point, selection,
  move / rotate / scale, new object, deletion, undo, record search, all record fields, edit a field by path, Use Info,
  framework configs, viewport annotations and user marks. Claude's edits are ordinary Ctrl+Z steps; only the user saves,
  and a separate checkbox can prohibit edits. This is the historical 0.71.0 description;
  see [docs/AI-BRIDGE.md](docs/AI-BRIDGE.md) for current permissions and unsandboxed `run_script` limitations.
- **K** over the render window (with the bridge enabled) places a mark for Claude; View → Claude… shows marks with notes,
  Claude's annotations and its latest actions.

## 0.70.0

- **Base Object Swapper preview in the render window.** The BOS toolbar button shows loaded objects replaced by the
  mod setup's `_SWAP.ini` rules as their replacements (tinted teal), by base (`[Forms]`) and reference (`[References]`),
  moved / rotated / scaled according to row properties (`posA`, `pos`, `rotA`, `rot`, `scale`; the midpoint for an a/b range).
  `[Transforms]` moves the original object. Random sets show the first replacement. The corner reports how many objects
  were replaced and, for the selection, the replacement, source row, chance and presence of conditions. Selection and editing
  operate on the originals. Included in view presets.
- **Which OAR / DAR animation replacements apply to an NPC.** New Animations tab in the NPC form: all Open Animation
  Replacer subfolders (`config.json`, overridden by `user.json`) and Dynamic Animation Replacer folders (`_conditions.txt`)
  for its skeleton, ordered by priority. Each gets yes / no / ?: conditions are evaluated against the record (base, race,
  class, voice, combat style, factions, perks, spells, keywords, sex, child; equipment and weapons from outfit and inventory).
  ? means some conditions require the game (drawn weapon, weather, combat, randomness…). Yes, playing marks the first match
  with no ? above it; row tooltips show every condition. For Lydia: 512 replacements for the character skeleton, nine definite matches, 132 failures.
- BOS forms such as `80F~Plugin.esp` (without `0x`) are now recognized.
- `--bos` enables screenshot previews; `--autotest-frameworks <NPC>` also writes animation results.

## 0.69.0

- **`1` cycles visibility:** first press makes the selection translucent (50%; objects underneath are visible and the
  object remains selectable), second hides it completely, third shows it again. `Alt+1` shows everything. Requested by a tester.
- **Tester roadmap:** introduced a page covering available features, plans, suggestions and crash reports.
  That redundant planning document has since been removed; current feature documentation is in README.md.
- `--fade-picked [2]` (with `--pick`): press `1` on the selected object once or twice for screenshot checks.

## 0.68.0

- **View presets.** Preset button on the render toolbar: built-in Default, Clean view (in-game), Navmesh work,
  Object placement and Low-end PC presets, plus custom presets: Save current view under a name; x deletes one.
  Presets store toolbar toggles (markers, water, terrain, grass, fog, collision, borders, navmesh, FX, animation,
  NPC highlighting, disabled objects), Show groups, mode and lighting, fly speed and grid radius. The view used on
  exit returns at the next startup.
- **Camera location.** Cell View shows Camera: <cell> [x, y] at the top: the world cell under the camera. Its list row
  is highlighted; Follow scrolls to it as the camera moves, and Show in list switches worldspace and clears the filter.
  Interiors show Loaded: <cell>.
- **Find placed objects.** Cell View → Find placed objects searches base EditorID, name or FormID for all placements in
  the selected worldspace (or interiors), with cell and grid coordinates; double-click loads the cell and selects the object.
  Uses the Use Info index.
- Fixed Dear ImGui's red 2 visible items with conflicting ID window: Navmesh and Landscape display buttons and tools
  shared IDs on the render toolbar. Release builds no longer show that window at all.
- `--autotest-findplaced <text> [worldspace]`.

## 0.67.0

- **Landscape grass.** Exterior terrain grows its textures' grass as in the game: each landscape texture (LTEX) supplies
  its grass types (GRAS); grid spacing uses INI `iMinGrassSize` (40 in STB). At each point, grass from the dominant texture
  rolls density in turn; PositionRange scatter, MinSlope..MaxSlope, distance from water, random rotation,
  height ±HeightRange and FitToSlope alignment apply. Distance uses `fGrassStartFadeDistance` + `fGrassFadeRange`
  (SkyrimPrefs.ini overrides Skyrim.ini). Random numbers are independent, so clumps do not exactly match the game,
  but grass coverage and types are visible. Grass toolbar button; clump count in the corner. Painting immediately updates grass.
  Grass-model vertex alpha is wind weight, not transparency, interpreted as in the game's grass shader.
- **Hide selected: `1`**, show hidden objects: **`Alt+1`** (as in CK). Also in Show, with a hidden count.
  Hidden objects cannot be selected; hiding is viewport-only and is not saved.
- Fixed an Inspector crash when the selected placement's cell unloaded (moving the camera quickly caused the render
  window to stream other cells). It now reports that the cell is not loaded.

## 0.66.0

- **What SPID gives this NPC.** Under the NPC's own lists on the Spells and perks form tab (and in the Inspector),
  SPID adds (in game) shows each `_DISTR.ini` row whose filters the NPC passes: spells, then perks, items, keywords,
  factions, outfits, packages…; chance, source row, mod and file; (already in record) when the NPC already has the form.
  ? means a filter cannot be checked by the editor (player-relative level, location), so the NPC might not receive it.
  Double-click opens the form. The reverse direction (who receives a spell or perk) remains Use Info → Frameworks → click a row.
- `stbedit-cli frameworks <game> <NPC EditorID>` prints the same results; `--autotest-frameworks <NPC>` opens the NPC form.

## 0.65.0

- **Perk tree In-game view uses the game's menu itself.** Built from game assets: `Interface\INTPerkSkydome.nif`
  (nebulae, star fields, the skill constellation image and neighboring constellations), `INTPerkStars01.nif` stars,
  camera at the dome's center with fStatsCameraFOV. Star placement comes from SkyrimSE.exe: the skill tree hangs from
  dome node `PointNN`; star position is (width / 2 − (column + offset)) × fStatsStarXIncrement horizontally,
  (row + offset) × fStatsStarYIncrement in depth, and fStatsStarZInitialOffset − (row + offset) × fStatsStarZIncrement
  vertically. Width is the largest column among perk nodes; star scale is fStatsStarScale (settings read from plugins,
  otherwise executable defaults). Thus upper rows converge as in the game, and larger columns lie farther left
  (the in-game tree mirrors the editor view).
- **Drag stars directly over the constellation image** (with an active file): a star slides along the tree's sloped
  plane, recalculating column, row and offsets as one undo step; Snap to grid also works here. Click still takes a rank,
  Shift+click refunds it; RMB looks around, wheel zooms, Fit frames the tree and image, Game scale uses the menu field of view.
  The skill slot (Point / cImage) and tree width appear above.
- Checked on STB: Conjuration stars follow the Oblivion gate outline, and One-Handed stars follow the sword blade.
- `stbedit-cli ls <folder> [extension]`: asset files under a folder and their sources; `nifdump … --nodes / --uvbounds`.

## 0.64.0

- **SKSE frameworks are visible in the editor.** Reads popular framework configs from the mod setup: SPID
  (`_DISTR.ini`), KID (`_KID.ini`), Base Object Swapper (`_SWAP.ini`), FormList Manipulator (`_FLM.ini`), Description
  Framework (`_DESC.ini`), Sound Record Distributor (`_SRD.yaml`), Open Animation Replacer (`config.json` / `user.json`)
  and Dynamic Animation Replacer (`_conditions.txt`). Record references use EditorID, `0x123~Plugin.esp`, `Plugin.esp|123`,
  or pluginName / formID pairs.
- New Frameworks tab in Use Info: what each config does to the record (SPID distribution to NPCs, KID keywords, BOS world
  replacement, FLM list additions, descriptions, sounds, animation-replacement conditions), source line, mod and file.
  SPID and KID rows show **who receives it**: filters check NPCs (EditorID, name, keywords, factions, race, class, voice,
  outfit, spells, perks, level, sex, uniqueness, child) or the appropriate KID record type; chance is shown.
  In STB: 8,717 NPCs receive perk aaMZp_DamageWeaponsNPC from STB_DISTR.ini; 150 armor records receive
  CA_WorkbenchDefault through `*Hood`.
- Records mentioned by configs have a Frameworks: SPID, BOS… (n) button in their form header.
- World → Framework configs… lists all references in the setup (10,461 references to 3,012 records), filtered by framework and text.

## 0.63.0

- **New perk-tree view.** Nodes are plates showing perk names and required skill levels (like dialogue-graph nodes);
  arrows run edge to edge, and the tree fills the window (stretching more horizontally, since grid trees are tall and narrow).
  Perk descriptions display without markup (`<font>`, `<span>`…).
- **In-game view** tests the tree without launching Skyrim: starry sky, perks as stars (taken stars glow, available stars
  are blue, unavailable stars dim), connecting lines; name, rank, description and next-rank requirements below.
  Skill-level slider; click takes a rank if a taken perk (or root) leads to it and skill is sufficient; Shift+click refunds it;
  Reset perks. Does not change the record.
- LOD and local-map generation were excluded from editor scope: xLODGen and DynDoLOD handle LOD, while Skyrim renders local maps itself.

## 0.62.0

(Tag 0.61.0 accidentally pointed to 0.60.0 code and was deleted; any remaining GitHub release 0.61.0 is 0.60.0.)

- **Collision display** in the render window (Collision button or F4; CK: F4): nearby objects' Havok collision as lines,
  including convex pieces and triangle collision for statics and buildings (bhkCompressedMeshShape is now read).
  Useful for finding collision holes and invisible walls.
- Collision is placed as in the game: without the NIF root node's own rotation (some buildings rotate it 180°,
  then rotate child nodes back).
- Nearly flat collision pieces (cloth, sheets) become thin boxes: Bepu crashed on such point sets,
  which could also crash Run Havok Sim.
- Navmesh generation can use collision (`stbedit-cli navgen … --collision`), but render meshes remain the default:
  comparison with game navmeshes showed no improvement from collision (it cut off the entrance in Riverwood's inn).

## 0.60.0

- **Run Havok Sim uses actual collision.** Falling objects now use their own NIF Havok collision: boxes, spheres,
  capsules, convex hulls (expanded by their convex radius, as in Havok), lists, transforms and MOPP, assembled into
  a compound body. Concave objects (bowls, cooking pots, rimmed plates) are no longer solid hulls. Triangle-mesh
  collision (used by statics and buildings) or unknown collision still falls back to the render mesh's convex hull.
  In Riverwood's inn, all six pots and plates use their own collision and settle within ±0.3–0.4 units of their
  original positions (one plate overlapping another settles onto it); everything settles within 0.9 seconds.
- `stbedit-cli nifcollision <mesh or EditorID…>`: mesh collision pieces and their bounds versus render-mesh bounds.

## 0.59.0

- **Dialogue graph** (quest Dialogue tab → Graph; CK: Dialogue Views): quest topics as nodes showing name, branch,
  line count and first line; arrows represent a line's Link To another topic. Columns represent distance from
  conversation starts (branch starting topics and topics with no incoming links); disconnected topics occupy the last column.
  Hover shows topic lines, click selects, double-click opens the topic in the editing list; RMB / MMB pans, wheel zooms.

## 0.58.0

- **Delta patch** in Compare with another file (xEdit: delta patch): records a plugin adds or changes relative to
  another version are copied to the active file as overrides (dialogue lines under their topics), as one Ctrl+Z step.
  References, cells, worldspaces, navmeshes and landscape are skipped (they need cell placement); deleted records cannot
  be represented by this patch. The result reports counts. The active file must be a different plugin loaded after the compared plugin.

## 0.57.0

- **Rooms and portals** in Check and clean (plugin errors): in interiors touched by the plugin, a room marker without
  bounds, a room link (XLRM) to something other than a room in the cell, or a portal connecting non-rooms (XPOD) is an error;
  a portal without bounds or room data is a warning. Cells merge all versions as the game sees them. Skyrim.esm has
  25 warnings and no errors (rules vanilla does not follow, such as reciprocal room links, are not checked).
  `stbedit-cli roomcheck [plugin]`.

## 0.56.0

- **Tint presets** in the face editor: tint layers with race-defined presets offer a color list with swatches;
  choosing one sets the layer color and preset number as CK does, and updates the head preview. (custom color) identifies a manually chosen color.

## 0.55.0

- **Filtered Dialogue** (World → Filtered Dialogue…; CK: Filtered Dialogue): all lines an NPC might say, checking
  conditions on its ID, voice (including voice lists), race, sex, class, factions and ranks (including template traits
  and factions), with game-style OR groups; other conditions (stages, globals, locations) pass. Lines naming the NPC
  appear first, then those tied through voice, faction or class; checkbox for tied lines only, text filter, click to open
  the topic. Belethor: 14,973 possible lines, 671 tied to him, with his Whiterun topics first.
- **Export relationships** (Export data window; CK: Export Relationship Data): all relationships (RELA) in the setup,
  including parent, child, rank and association, to `Documents\STB Editor\Export\Relationships.txt`.
- `stbedit-cli filtereddialogue <NPC EditorID>`.

## 0.54.0

- **Editable AI-package procedure tree** (Procedure Tree tab): choose branch types, including composites (Sequence,
  Stacked, Simultaneous, Random… with children) and procedures (Wander, Sit, Sleep, Travel…; all types present in loaded
  packages). Add child appends under the selection; Add after inserts after its subtree; Up / Down moves the subtree;
  Delete removes it; Add first branch handles an empty tree. Child counts update automatically. Procedures offer
  package-input selection, Add input and x. Each action is one Ctrl+Z step; the edited record is read back by the package autotest.

## 0.53.0

- **Merged Patch** (File → Merged Patch…, formerly Merge leveled lists): now merges containers (contents), NPCs
  (inventory, factions with ranks, perks, spells, packages, keywords), outfits and form lists as well as leveled lists,
  with per-type checkboxes. The rule remains: for each element, the last plugin changing it relative to the original wins,
  so one plugin's addition survives a later plugin that leaves it untouched. Winner order is preserved (important for
  packages), followed by additions. In this setup: 26 leveled lists, 35 containers, 44 NPCs, three outfits and one form list;
  one Ctrl+Z step. Review additions and removals: a plugin may have removed something deliberately.
- `stbedit-cli mergeplan <signature> [n]`: what merging adds and removes.

## 0.52.0

- **Check NavMeshes** (Navmesh panel; CK: Check NavMeshes): checks all loaded navmeshes for out-of-range and repeated
  triangle vertex indices, zero-area and downward-facing triangles, edges shared by three or more triangles, links to
  missing triangles or without reciprocal links, unused vertices and coincident vertices. Click an issue to select its
  triangle or vertex and focus the camera. Vanilla inns and Bleak Falls Barrow are clean apart from one unused vertex;
  generated navmesh has no issues.
- **Deleted navmeshes** in Check and clean (new tab): deleting another plugin's navmesh crashes the game. Restore and lower
  restores the master copy with every vertex at Z −30000, like xEdit Quick Auto Clean, as one Ctrl+Z step.
  This setup has no plugins with deleted navmeshes, so this was not tested against real cases.

## 0.51.0

- **Sort Masters** (masters window; xEdit: Sort Masters): active-file masters are sorted by load order on save;
  FormIDs are renumbered and the file fully rewritten. Checked with `stbedit-cli mastersorttest` on a plugin copy
  with unsorted masters: order corrected, all 251 records retained, no differences.
- **Change FormID** (Object Window → right-click → Change FormID…, or the section in Compact FormIDs; xEdit:
  Change FormID): assigns a new number to an own record in the saved active file; internal links follow it, optionally
  renaming adjacent FaceGen and voice files. The file is backed up and reloaded. Dependent plugins are listed:
  they continue referencing the old number. Checked with `stbedit-cli formidtest`: record moved, all 17 references updated,
  everything else byte-for-byte unchanged.

## 0.50.0

- **Dialogue line order and deletion.** Topic lines follow their Previous line (PNAM) chain, as in the game. Drag a line
  onto another to place it afterward, or use Up / Down in the context menu: updates its and its neighbors' PNAM
  (other plugins' lines become active-file overrides), as one Ctrl+Z step. Delete line removes a new line or one
  belonging to the active file (removed from the topic on save, with its successor linked to its predecessor).
  Other plugins' lines cannot be removed because the game would see them as deleted; use an impossible condition instead.
- Saving can remove dialogue lines (INFO) from active-file topics; checked with `stbedit-cli dialremovetest` on a plugin copy
  (exactly one line removed; the other 507 records byte-for-byte unchanged).

## 0.49.0

- **Story Manager node moves.** Drag a branch or quest node onto another to place it afterward; Ctrl-drop onto an event
  or branch appends it as the last child; drop onto an event without Ctrl makes it the first. Context menu offers Up and Down.
  Each move is one Ctrl+Z step covering every node whose parent or predecessor changes; the tree includes unsaved edits (marked *).
- Shared parent / previous chain editing for idle animations and Story Manager (`Core/SiblingChains.cs`).

## 0.48.0

- **Plugin assets** (File → Plugin assets…): every file named by plugin records (models, textures, sounds, music,
  behavior graphs…) and textures in its meshes, showing each source (which mod's loose file or which archive) and missing
  files; missing-only filter and list export. STB.esp: 9,029 files, 87 missing.

## 0.47.0

- **Export data** (File → Export data…; xEdit / CK: Export): plugin text by category (names, descriptions, books,
  messages, quests including names/journal/objectives, dialogue, other text) and game settings (GMST) with values.
  Each category gets a tab-separated UTF-8 file in `Documents\STB Editor\Export\<plugin>`: FormID, signature, EditorID, field, text.
- **ModGroups** (File → ModGroups…, as in xEdit): groups of plugins used together (a mod and its patches). When the
  whole group is loaded, Object Window's conflict filter compares only the group's last version of a record;
  overrides within the group are not counted as conflicts. Stored in `Documents\STB Editor\modgroups.json`.

## 0.46.0

- **Scripts** (File → Scripts…; xEdit: Apply Script): C# scripts (Roslyn, .csx format) in
  `Documents\STB Editor\Scripts`, with list, editor, Check (compile errors), Run, Stop and output.
  Scripts can access loaded records (`Records<IWeaponGetter>()`, `Get(key)`, `Find("EditorID or FormID")`), Object Window
  selection (`Selected`), plugin list, `Print`, `Name(record)` and edits with `Edit<Weapon>(record, w => …)`.
  Edits reach the active file as one Ctrl+Z step after successful script completion; records that would not save correctly
  are not changed (the log explains why). The final expression's value is printed.
- First opening installs examples: NPCs without voices, selected weapon damage ×1.1, EditorID prefixes for selected records,
  and record counts by originating plugin.

## 0.45.0

- **Regions** (World → Regions…; CK: Region Editor): open-worldspace regions listed with map color, area count and
  corner count; region-colored borders follow terrain. The render-window region tool selects by clicking inside
  (smaller region wins overlaps); drag selected corners across the ground, Shift+click a border to add a corner,
  Del to remove the selected corner (at least three remain per area). New region here creates an active-file region
  with a one-cell square area at the view center; Add area here adds another area to the selection. Each edit is one Ctrl+Z step.
- Selected regions show their weather and chances, map name, objects, grass, sounds and music, editable in the region form.
  Region-based object and grass generation (CK: Generate) is not implemented.

## 0.44.0

- **Weather and time of day** in exterior render views (Sky button; CK: sky / time of day): selected weather (this
  world's climate weathers first, then all others) at a chosen hour, with a 0–24 slider and 6 / 9 / 12 / 18 / 21 / 0 buttons.
  Sunrise/day/sunset/night weather colors blend using climate sunrise and sunset times; sunlight and sun direction
  (east → south → west, moon direction at night), ambient light from the weather's directional ambient colors,
  fog color/distance and background sky color. Default lighting restores the earlier editor lighting.
- Clouds, the sun disc, precipitation and image space (game brightness / color grading) are not rendered;
  only lighting, fog and sky color are previewed.

## 0.43.0

- **Voice files** (World → Voice files…; CK: Voice Assets, Export Dialogue, Generate LIP): each plugin dialogue response
  for every voice that speaks it, with the game's expected path
  (`Sound\Voice\<plugin>\<voice>\<quest>_<topic>_<FormID>_<n>`) and available files: .fuz, audio + .lip, WAV without .lip,
  or nothing. Voices come from the line's speaker, subject conditions (GetIsID, GetIsVoiceType including lists,
  GetIsAliasRef → unique actor / created object / forced reference / alias voices; faction or class if none is named),
  quest dialogue conditions and scenes where an alias speaks the topic.
- Compared with Skyrim.esm voice assets (`stbedit-cli voicecheck`): filenames match in 34,285 of 34,293 cases;
  voices found for 97% of recorded lines; 325 lines have no determinable speaker from conditions (CK would offer all voices).
- **Export list**: tab-separated table for voice actors (file, voice, quest, topic, FormID, number, emotion, text, notes, status).
- **Generate missing .lip** runs CK LipGenerator (`Tools\LipGen\LipGenerator`, requires FonixData.cdf) for every WAV without
  a .lip; stereo and non-16-bit WAVs are first converted to mono 16-bit (otherwise LipGenerator crashes). Latin-script
  response text is passed as a hint. **Generate .lip and pack .fuz** also encodes WAV to xWMA
  (`Tools\Audio\xwmaencode.exe`) and writes .fuz beside the WAV; standard LIPFuzer reads the result.

## 0.42.0

- **Idle animation tree** (World → Idle animations…; CK: Gameplay → Animations): all idles grouped by behavior graph
  (0_Master.hkx, draugr, horse…) and root action (AACT); children follow the game's condition-evaluation order
  (the first match plays). Click opens the idle form (event, conditions, flags). Drop onto another idle to place it after
  that idle (Ctrl makes it the last child), or onto an action to make it the first child. Context menu: Up, Down,
  Into previous idle, Out of parent, New idle after this, New child idle. Each move is one Ctrl+Z step covering all idles
  whose parent or predecessor changes (RelatedIdles). Name/event filter and graph selection.
- `stbedit-cli idletree [depth] [filter]`: text tree and graph summary.
- Behavior graphs (.hkx) are not parsed, so the idle form cannot suggest graph event names.

## 0.41.0

- **Perk tree** (skill AVIF form → Perk tree tab; CK: Actor Values → Perk Tree): nodes on a row/column grid with offsets,
  as in the game; arrows to connected nodes, perk names and required skill levels (from perk conditions). Drag to move
  a node (Snap to grid uses whole rows/columns); Ctrl+click another node toggles a connection; double-click opens the perk.
  Add node connects from the selection, Delete node removes it and all incoming connections, Fit frames the tree;
  RMB / MMB pans, wheel zooms. Below: selected node fields (perk, column, row, offsets, connections) and perk description
  with rank count. Each edit is one Ctrl+Z step.
- The root is drawn below its children: vanilla root row/column fields contain junk.
- `stbedit-cli perktree <skill EditorID>`: text listing of tree nodes.

## 0.40.0

- **Navmesh generation** (Navmesh panel → Generate navmesh; CK: Generate NavMesh): walkable surfaces in the open interior
  cell (central cell outdoors), based on collision-bearing meshes and landscape, for an actor of configurable size
  (radius, height, step, slope, voxel size in Generation settings). Runs in the background (Recast through DotRecast).
  Replaces the cell's largest navmesh or creates a new active-file navmesh as one Ctrl+Z step, then immediately Finalizes
  doors, neighbor links and NAVI as a second step.
- Interiors retain only pieces connected to doors: tabletops, beams, wall/cliff tops and unreachable bottoms under bridges
  are discarded (Keep pieces not connected to doors restores large separate rooms). Exteriors retain pieces at cell edges
  (continued by neighbors) and large pieces. Triangles under cell water are flagged Water. Doors, activators and meshes
  without collision (black planes, cloth, light beams) do not obstruct generation.
- Compared with game navmeshes (`stbedit-cli navgen`): Riverwood inn has 86–90% of game triangles covered by ours and 83%
  of ours on game triangles; Bannered Mare 84 / 83%; Bleak Falls Barrow 01/02 88 / 63% and 83 / 71% (extras are mostly
  edges and recesses CK designers trim manually). Cover edges are not calculated.
- With multiple navmeshes in a cell, only the largest is replaced; others must be removed or trimmed where they overlap
  (the result reports their count).

## 0.39.0

- **Face editor** (NPC form → Appearance tab; CK: Character Gen Parts, Face Morphs, Tints): head parts by type (hair,
  beard, eyebrows, eyes, scars, head; filtered by race/sex, or race default), hair color, skin color (skin-tone layer follows),
  weight, nose/eye/mouth presets, 18 face sliders (double-click resets to zero), tint layers (enabled, color, strength),
  and a **live head preview** using the same FaceGen builder as export, entirely in memory (RMB rotates, wheel zooms).
  Sliders update the preview during dragging and commit one Ctrl+Z step on release. Export FaceGen writes the game head.
  Raw appearance fields are under All appearance fields.
- Asset storage supports in-memory files (preview writes nothing to disk).

## 0.38.0

- **Run Havok Sim** (render window: Alt+H or Havok button): selected objects fall with physics (BepuPhysics) onto their
  surroundings and settle, rather than simply dropping to the ground as with F. Bodies use convex render-mesh hulls;
  support uses nearby unselected object meshes and landscape. Small objects inside a falling object (ingredients in pots,
  food on plates) stay in place without pushing it out; objects initially intersecting each other do not push one another.
  Runs in the background (Havok sim… in the corner), with the result as one Ctrl+Z step.
- Autotested in Riverwood's inn: six pots and plates, raised and tilted 20°, return to their positions (height within
  ±0.4 units, level); one plate settles tilted onto its originally overlapping neighbor. Limitation at this release:
  convex hulls, not NIF Havok collision, so concave bowls/pots cannot accept objects inside.

## 0.37.0

- **FaceGen export** (CK: Ctrl+F4): writes NPC heads (`meshes\actors\character\facegendata\facegeom\<plugin>\<FormID>.nif`)
  and tint textures (`textures\…\facetint\…\<FormID>.dds`) beside the active file. Object Window context menu → Export
  FaceGen, Ctrl+F4 on selected NPCs, or Export FaceGen for N NPCs in Check and clean (missing/stale FaceGen).
  Uses the NPC's current state, including unsaved edits; the render window immediately shows the new head.
- Matches CK: race-default head parts for the sex, replaced by NPC parts of the same type, plus extra parts; each part's
  mesh uses its TRI morphs: race morph (Morph Race for vampires and afflicted), weight (SkinnyMorph = 1 − weight/100),
  nose/eye/lip presets, 18 face sliders (direction table derived from CK files), VampireMorph for vampire races.
  NPC or part texture sets (TXST → NIF slots), hair color ×2 for hair/eyebrow/beard shaders, skin color for scars;
  tint combines skin color (QNAM) and NPC layers through race masks.
- Compared with CK files (`stbedit-cli facegencheck`, `facegentint`, `facegenbuild`): eyes, mouth and hair match to
  thousandths; faces within 0.06 units (335 of 353 vanilla NPCs; CK SE-built mod NPCs within 0.02–0.05); tint average
  error 0.3–3 out of 255. File structure matches CK (nodes, bones, BSDynamicTriShape, skin, shaders).

## 0.36.6

- Rotation gizmo hides nearly edge-on rings (for example Z when the camera is level with the object): they could be grabbed
  but did not rotate the object. At that angle, rotate around Z with left-drag in Rotate mode or right-drag on the object.
- RMB (rotation / look), wheel and MMB also work while the cursor is over the gizmo.
- Palette mode hides the gizmo (clicking it placed an object).

## 0.36.5

- Removed the 0.36.3 edit log (`edit:` in `editor.log`): it was used to find objects flying away during rotation (fixed in 0.36.4).

## 0.36.4

- **Objects flew away during mouse rotation** (RMB, or LMB in Rotate mode) when holding an angle over 60°: each frame
  used the object's current position as pivot instead of its drag-start position. Rounding error was rotated back and
  grew each frame (doubling at 180°), sending objects hundreds of units away and then to infinity. Found through the
  0.36.3 log (1.3-unit shift at 180°). The pivot now stays at the drag-start position, with rotation calculated relative
  to it without subtracting large world coordinates: zero shift. `stbedit-cli mathtest` replays the logged case (old method:
  over 100 units by 90°, then infinity; new: zero); `--autotest-place` holds 180° rotation for 60 frames.

## 0.36.3

- Render-window edit logging: each drag, gizmo rotation and Placement-field edit writes to
  `%LOCALAPPDATA%\STBEditor\logs\editor.log` (`edit:`: initiating action, mode, angle step, camera, before/after position
  and rotation). If rotation moves a single object's position, a detailed warning is logged too. Used to investigate objects flying away during rotation.

## 0.36.2

- **Preview Spin button crash**: clicking closed the editor (ImGui popped a color that had not been pushed). Fixed.
- **Objects flew away while dragging.** LMB on selected objects in Rotate mode moved instead of rotating them; movement
  intersected the mouse ray with a horizontal plane, so with a nearly level camera (mines, caves), ten pixels moved
  objects hundreds or thousands of units. LMB now rotates in Rotate mode (E), like RMB, and scales in Scale mode (R).
  Movement still keeps objects under the cursor, but is capped at four times cursor travel at object distance
  (level-camera test: 110 units instead of 514 and beyond).
- Autotested on XMarkerHeading in EmbershardMine01: RMB rotation with 15° Angle snapping (exact 15° steps, unchanged
  position), snapped rotation gizmo (90° → 165°), LMB in Rotate and Move modes.

## 0.36.1

- **Inspector for newly placed references** (dragged from Object Window, pasted or duplicated): instead of Record not
  found in the loaded plugins…, shows the Reference tab with position, rotation, scale, reference properties and All fields,
  like loaded references. Editing starts from a blank record with its base and saves with the reference.
- Inspector reloads records after saving: previously reselecting the same reference or record retained its pre-save version.
- Checked on XMarkerHeading (older-version report: one white dot and three axes, cannot rotate): dragged from Object
  Window, it renders as a red translucent box with a heading nose, selects by click, rotates through RMB, gizmo and Inspector
  fields, and saves its rotation. `--autotest-place`.

## 0.36.0

- **Story Manager** (World → Story Manager…): CK-style tree of root, events (CraftItem, KillActorEvent, ScriptEvent…),
  branches and quest nodes in game evaluation order (Previous Sibling chain), with quests under quest nodes; node condition
  counts, node/quest filter, click to open the node or quest form (conditions, flags, parent and order are edited there).
  STB setup: 663 nodes, 1,638 quest slots.

## 0.35.0

- **Layers** (World → Layers…; CK: Layers): named reference groups, created from the selection; add/remove selected
  references, select loaded layer references; **Hidden** (not drawn or selectable) and **Frozen** (visible but not selectable,
  useful for keeping walls fixed while placing clutter). Stored by the editor in
  `%LOCALAPPDATA%\STBEditor\layers.json`, not in the plugin.

## 0.34.0

- **Object palette** (World → Object palette…; CK: Object Palette): base-object sets (search by name or Add selected
  from Object Window) and Place from palette mode: every render-window click places a random object with random rotation,
  scale in a specified range, tilt up to N° and ground sinking; each placement is one Ctrl+Z step. For rocks, trees and debris.

## 0.33.0

- Landscape **Color** brush (CK: Vertex Color) blends terrain vertex colors toward the selected color (white means no
  tint; uncolored terrain is treated as white), using the other brushes' radius and strength. One stroke is one Ctrl+Z
  step; saved in the LAND record's VCLR.

## 0.32.0

- **Heightmap** (Landscape panel; CK: Heightmap Editor): export loaded world-cell heights to `.r16` (16-bit, north at top;
  read by World Machine, Gaea and image editors), with adjacent `.json` metadata (worldspace, cells, height range), then
  import them back. Loaded cells inside the map receive its heights as one undo step, like a brush stroke.
  Edit the `.json` height range to shift everything, for example by 1,000 units.

## 0.31.0

- **NPC body tint** (CK: Update NPC Body Tint Colors): Check and clean warns when body color (QNAM) differs from facial
  skin tone, causing a neck seam. Update body tint fixes these NPCs in the active file as one undo step. CK's formula
  (gray blended with the skin-tone layer color using its interpolation) was derived from data and matches all 2,168
  Skyrim.esm and 213 Dawnguard.esm NPCs; STB.esp has two affected NPCs (`tintprobe`).

## 0.30.0

- **Merge leveled lists** (File; xEdit: Create Merged Patch / Wrye Bash, Mator Smash): finds item, NPC and spell leveled
  lists changed by two or more plugins (only the last takes effect in game), and previews additions/removals relative
  to the winner. Per-item rule: entries from the last plugin changing that item win, avoiding duplicates for changed
  levels while retaining other plugins' additions. Checked lists go into the active file as one undo step; refuses with
  an explanation if the active file loads before participants. STB setup: 26 of 6,931 lists (for example Dawnguard Falmer in `LCharFalmerMelee`).

## 0.29.0

- **Compare with another file** (File; xEdit: Compare to…): compares a loaded plugin with another version of that file
  (backup, colleague's copy, previous release), showing records only in the plugin, only in the other file, and changed
  records with differing fields. Records match by FormKey through each file's masters and compare byte-for-byte
  (recompression does not count as a change). STB.esp against a copy: two seconds. Click a row to open the record form.

## 0.28.0

- Navmesh **Find by index** (CK: Select Triangle by Index) selects a loaded triangle or vertex by number and focuses
  the camera. **Move to new navmesh** (CK: Move Selection to Separate NavMesh) moves selected triangles into a separate
  active-file navmesh in the same cell, renumbering neighbor links, all as one undo step; Finalize then links the edges.
  Checked on 75 navmeshes (`navsplittest`).

## 0.27.0

- **Perk Papyrus fragments**: code for each Activate entry under the Perk entries tab
  (`PRKF_<EditorID>_<FormID>`, functions with `akTargetRef` and `akActor`, as in CK; labeled with activation-button text).
  Compile and bind like quest, dialogue-line, package and scene fragments. All CK fragment types are now editable.

## 0.26.0

- **Texts for translation** (File → Texts for translation…): exports all player-visible plugin text (names, descriptions,
  books, dialogue, journal, messages, cells) to JSON, one entry per text: FormKey, type, EditorID, field, original text
  and empty `translation`. Import the completed file into the active file (overrides, or own records when the source
  plugin is active) as one undo step. Text changes only if it still matches the export; later-plugin changes are reported,
  with an override checkbox. STB.esp: 30,754 texts in seven seconds.
- Find text: topics (DIAL) no longer also return their lines' text (those are found as INFO records); replacement in
  cell/worldspace names saves as a header edit, like the cell properties window.

## 0.25.0

- **Quest alias Use Report** (CK: Use Report): button for the selected alias on the Aliases tab. Searches the quest itself
  (dialogue/event conditions, journal entries, objectives and conditions, other aliases' fill-from-alias settings and
  conditions, quest-script properties referring to the alias, alias scripts), quest scenes (actor, actions, head tracking,
  scene/phase conditions), packages owned by the quest (alias targets, conditions, procedure conditions), and lines in
  quest topics. Includes this session's edits. Click a row to open the record form.

## 0.24.0

- **Package and scene Papyrus fragments.** Packages: Begin / End / Change tab shows idles and event topics, followed by
  Begin / End / Change code (`PF_<EditorID>_<FormID>`, functions with `akActor`, as in CK). Scenes: Begin / End on the Scene
  tab, phase start/end under the selected phase (`SF_…`). **Compile** writes the script beside the active file and binds
  fragments to the record on success (one Ctrl+Z step); errors include line numbers.
- Inserting/removing scene phases also shifts phase fragments.
- Fragment references absent from script source (38 in game data) are removed on compilation instead of left broken.
- Package event tab shows only idles and topics (old internal script blocks are hidden).

## 0.23.0

- **Scenes** (SCEN) can be edited and created (New). CK-style tabs: Scene (quest, flags, conditions), Actors (quest aliases
  by name, flags and death/combat/dialogue behavior checkboxes; new actors match vanilla: death and combat end the scene,
  dialogue pauses it), **Phases and actions** timeline with one row per actor plus timers, one column per phase, and
  actions spanning start/end phases. + in a cell adds dialogue, a package or timer; append a phase, insert before the
  selection or remove one (actions confined to that phase are removed; others shift). Below the timeline: selected action
  (actor, phases, head tracking, flags, emotion, packages, timer seconds) or phase (name, start/end conditions).
- Scene dialogue shows its topic, lines and responses directly in the scene form (like the quest Dialogue tab);
  without a topic, **Add scene topic** creates one with Scene category and the scene's quest.
- A new scene matches vanilla `dunFellglow_ReleaseDogs`, built with the same steps, at subrecord level
  (NEXT phase markers, width 200, action order, indices).

## 0.22.0

- **AI packages** (PACK) have CK-style forms. Package Data: template (choosing one fills inputs with its defaults,
  like CK), owning quest, combat style, speed, flags and interrupt flags; **template inputs by name** (Location,
  Allow Eating, Energy…). Locations: kind (near reference, in cell, near object, linked reference, special: near self /
  starting position…) and radius; targets: specific reference, object, object type, linked reference, owning-quest alias
  by name, self; booleans, numbers, topics. Schedule: month, weekday (weekdays/weekends…), date, hour, minute, duration,
  readable summary. Procedure Tree: branches (Sequence → Travel, UnlockDoors, Sandbox…), branch type, procedure,
  flags, named inputs and conditions; templated packages show a read-only template tree.
- Package conditions: Run on package data and package-data parameters select inputs by name; aliases use names from the owning quest.
- New packages (New) match vanilla defaults: any time, running, all interrupts allowed, Begin / End / Change events
  present (previous schedule: January, Sunday, 0:00). A Sandbox-template package matches vanilla
  `DefaultSandboxCurrentLocation256` at subrecord level.
- Flags: Mutagen defines Wear Sleep Outfit incorrectly (decimal 20000000, eight bits rather than bit 29);
  flag checkboxes now accept only single bits, using the correct bit for this flag.

## 0.21.0

- Render window **reference primitives** (XPRM) draw with markers (M), like CK: trigger boxes as translucent boxes with
  edges, activation areas as three-ring spheres, using record colors. Rooms, portals, multibounds and occlusion draw
  only edges; boxes containing the camera also draw only edges (otherwise they fill the view). Primitive size and type
  remain editable under More properties for the reference.

## 0.20.0

- Render-window **Show** button (CK: Show/Hide) hides statics, furniture, containers, doors, lights, actors, activators,
  flora/trees, items, sounds, effects and idle markers; exteriors offer **Current cell only**. Hidden objects are neither
  drawn nor selectable by click.
- Check and clean warns about another plugin's NPCs whose face, hair or skin is changed while FaceGen comes from a
  different plugin (possible dark face; head-part order and float noise do not count as appearance changes).

## 0.19.0

- **Edit → Find text…** (Ctrl+Shift+F): searches all plugin record text fields (dialogue, descriptions, books, quest
  journal…) or EditorIDs/names across all plugins. **Replace** visible text (and optionally EditorID) as one undo step;
  internal strings (script variables, paths) are not replaced.

## 0.18.0

- **Edit from comparison**: right-click a field value → Take this value into <active file> (version comparison), or
  Copy this value to other records (record comparison, like xEdit Copy to selected records). The entire field is copied
  through a Mutagen mask; fields Mutagen changes beyond the mask (race skeleton) are restored.

## 0.17.0

- **File → Compact FormIDs…** (Compact FormIDs for ESL / Convert to light): compacts active-file own records into
  0x800–0xFFF, updates internal links, optionally sets ESL and renames adjacent FaceGen/voice assets; backup, reload and
  dependent-plugin warning. Unaffected records remain byte-for-byte unchanged.

## 0.16.0

- **File → Pack BSA…** (CK: Create Archive): packs assets beside the active file into `<plugin>.bsa` and
  `<plugin> - Textures.bsa` (SSE version 105, uncompressed). Script sources and SEQ are excluded; old archives are backed up.
  Filename hashes match every file in vanilla archives (`stbedit-cli bsatest`).

## 0.15.0

- **File → Check and clean…** (xEdit: Check for Errors, Quick Auto Clean): errors (missing record links, self-containing
  leveled lists, unreadable records), warnings (deleted base records, new NPCs without FaceGen), **ITM** (overrides identical
  to their master version: records, cell headers, references) with Remove N ITM, and **deleted references** with Restore
  and disable (UDR, like xEdit). Fixes are active-file edits with Ctrl+Z.
- Writer can remove references from plugins (ITM references, deleted own references) and clears Deleted during UDR.

## 0.14.0

- **Navmesh Finalize** (Navmesh panel: Finalize changed / Finalize cell): door triangles and door-to-navmesh links
  (XNDP), edge links to neighboring navmeshes across cell boundaries, and navmesh entries in the plugin's navmesh map
  (NAVI). Compared with CK output: 12 of 12 doors match; edge links match in seven Tamriel areas
  (except degenerate sliver triangles at boundaries).

## 0.13.0

- **Existing cell properties** (right-click cell → Cell properties…): name, flags, owner, location, encounter zone,
  regions, lighting (template, colors, fog, immediately visible in the render window), image space, water, acoustics, music, sky.
- **Worldspace properties** (right-click → Worldspace properties…): parent, climate, water and LOD water, music,
  interior lighting, default land, map.
- Only cell/worldspace headers save; references, landscape and navmesh remain untouched.

## 0.12.0

- **Dialogue**: quest-form Dialogue tab lists quest branches/topics and topic lines from every plugin. Lines have
  flags, responses (emotion, text, notes, sound, animations), player prompt, speaker, conditions, links and Shared Info;
  create branches (with starting topic), topics, lines and responses. Only changed lines save under a topic header
  (other topic subrecords are not overridden; topic line count is recalculated).
- **Dialogue fragments** Begin / End (`TIF__…` script, `akSpeaker`), compiled like quest-stage fragments.
- Fragment-script parsing preserves function parameters (all 7,353 QF / TIF scripts in the setup roundtrip unchanged).

## 0.11.0

- **Papyrus compilation**: scripts on the Scripts tab offer Source (edit .psc, Save and compile, errors with line numbers)
  and New script… (creates source with the required `extends`, attaching after compilation). Compiler comes from the game
  folder or settings; imports use every MO2 mod's sources, including disabled mods such as SOURCE; output goes beside the active file.
- **Quest-stage fragments**: Papyrus code directly under journal entries, compiling `QF_…` like CK (preserving other
  fragments and author properties, auto-filling alias properties, `kmyQuest`), with quest fragment-table updates.
- **SEQ files** are written on save when the plugin contains Start Game Enabled quests.
- Scripts tab also supports quests, perks, packages and scenes (with their own script data).

## 0.10.0

- **Quest editor**: CK-style tabs for quest data, dialogue/event conditions, **Stages** (flags, journal entries with text,
  conditions, complete/fail quest, next quest), **Objectives** (text, map targets using named aliases, conditions),
  **Aliases** (reference/location, fill method, flags, conditions, inventory, packages, spells, factions).
  New aliases receive the next free ID; stage/objective indices are unique and sorted. Quest conditions show aliases by name.
- Create new quests in Object Window (New).

## 0.9.0

- **Condition editor** (CK: Condition Items) in every form containing conditions: CK-style rows
  (`Subject.GetIsID(Hulda) == 1 AND`), inline operator/value/OR, selected-condition function search (all ~400),
  Run on (Subject, Target, reference, alias, package data…), parameters (form or alias number), global instead of numeric
  value, Swap Subject and Target; reorder, delete and copy/paste conditions between records.
- **More reference properties**: remaining fields corresponding to CK tabs, including leveled-actor level, encounter zone,
  persistent location, location ref types, activate parents, map marker, emittance, light, NPC horse and merchant chest,
  primitive, rooms, portals and reference Editor ID. With multiple selected objects, changes apply to all as one Ctrl+Z step.

## 0.8.0

- **Use Info** (CK: Use Info; xEdit: Referenced By): record-form button and Object Window context action. Records tab
  lists referencing records (lists, containers, recipes, scripts, conditions, dialogue); Placed tab lists world placements.
  Double-click opens the record or loads the cell with the object selected. Only winning record versions count.
  The reference index builds in the background after loading (seconds; then ~1 second from cache), using about 70 MB.
  Also finds alternate textures (TXST in MODS); dialogue lines are checked in their winning plugin; Go to also supports
  persistent world references (map markers, NPCs).
- Deleting an own record with incoming references warns in the tooltip and log.
- **Batch reference properties** (CK: Reference Batch Action): select multiple objects to apply flags, enable parent,
  owner, lock, count and radius to all in Inspector (one Ctrl+Z step). Teleport, linked references and scripts remain single-object edits.
- **Replace reference base** (one or all selected): Base object field in reference properties preserves placement and
  properties while immediately changing the render model.
- **Edit together**: select same-type records in Object Window (Ctrl/Shift+click) → right-click → Edit together; one form
  applies each changed field to all (like xEdit Copy to selected records), one Ctrl+Z step.
- **Tables** (View → Tables… or Table in Object Window): weapons, armor, ammunition, potions, ingredients, books and misc
  items; sort any column, edit cells directly (like xEdit tables), filter by name/plugin.
- Object Window → right-click → **Copy as override into <active file>** (like xEdit), also for multiple selections.

## 0.7.0

- **New record of any type**: Object Window New button (or right-click → New record…): Editor ID, name and, for globals
  and game settings, type (float / int / string / bool). Opens the record form.
- **Removal**: right-click → Remove from <active file> or Del in Object Window deletes own records from the file,
  removes overrides (the preceding plugin wins again), or discards new session records. Other plugins' records cannot
  be deleted. Everything is undoable with Ctrl+Z until save.
- **Copy / cut / paste objects**: Ctrl+C, Ctrl+X, Ctrl+V (under cursor), Ctrl+Shift+V (same position, including copying
  from one cell to another). Copies and Ctrl+D preserve reference properties (flags, enable parent, linked references,
  lock, owner); teleport is not copied.
- Object Window: **All / Overridden / Conflicts** and **only records from plugin…** filters. Like xEdit's filter,
  conflicts compare record versions by field and show records where a plugin's change is lost in the winner;
  scanning is time-sliced so the window remains responsive.
- **File → Masters**: active-file masters, unused status, Add master, Remove unused (like xEdit Add / Clean Masters).
  Applies on save; FormIDs are renumbered, and save checking accounts for that.
- **UI scale** (View → Settings, 75–200%, 100 / 115 / 125 / 150% buttons): font and spacing scale together;
  Ctrl+= / Ctrl+- / Ctrl+0 anywhere.
- Fixed empty names/descriptions for Update.esm, Dawnguard, HearthFires and Dragonborn records when those masters live
  in an MO2 mod (for example Cleaned Masters): strings are now found like the game does, using prioritized loose mod files,
  then Data archives.

## 0.6.1

- Save check no longer warns that 50+ records will change when an edit needs a new master: shifts in a plugin's own
  FormIDs do not count as changes. STB.esp drops from 50+ to two records (empty markers in invisible races).
  The dialog explains the reason in Russian, naming the added master, and shows record EditorIDs.

## 0.6.0

- xEdit-style record comparison: versions from all plugins in columns (master left, winner right), through the form's
  Versions tab or record context menu → Compare versions. Green marks changes reaching the game; red marks changes
  lost in the winner (conflicts).
- Side-by-side records: Ctrl+click / Shift+click in Object Window, right-click → Compare selected (or Compare with…
  for the selected and clicked records). Differences are yellow. Lists (keywords, inventory, effects) align by value,
  not row index; Differences only hides equal rows; double-click a link to open the record.
- Themes (View → Settings → Theme): STB (original), Windows dark/light, Carbon, Cobalt, Nord, Dwemer and others;
  15 in total, applied immediately.
- Record header fields (FormVersion, VersionControl, Version2, raw flags, compression) are hidden by default in tabs
  and All fields; enable Settings → Show record header fields.

## 0.5.3

- Double-clicking a door first asks Go to…? with the destination name, like CK (can be disabled).
- Mouse wheel zoom is smoother with smaller steps; wheel and fly speeds are in View → Settings….
- Fixed Shift-orbit abruptly moving the camera when not looking at the selection, for example immediately after going
  through a door. The destination door is no longer selected after travel.
- Inspector no longer reports Record not found for a reference selected before its cell loaded.
- Settings window: wheel speed, smoothing, fly speed, door confirmation, NPC highlighting, FPS and language.

## 0.5.2

- New STB Editor icon for the executable and window.

## 0.5.1

- Reference fields search while typing: matching records of the required type appear below (EditorID, name or FormID),
  selected by arrows/Enter or click; ▼ opens the full filtered type list, like CK dropdowns. Clicking a field selects
  its text, so typing replaces the value immediately.
- STB application/window icon.

## 0.5.0

- CK-style record forms with meaningful tabs (actors: Traits, Stats, Factions, Inventory, Spells and perks, AI, Appearance,
  Attacks; specialized tabs for armor, weapons, perks, spells and more), and lists as tables with row addition/removal.
  Fields edit immediately without an Edit fields mode.
- Separate form windows: double-click records in Object Window or use Open in window.
- Floats show all digits; fields stored at lower precision are marked ! with the value that will be saved.
- NPC skills (dictionaries), male/female variants, conditions and different perk entry types are editable.
- Scripts have their own tab, scrollable suggestion list and visible properties.
- File → Data selects plugins to load (masters are included automatically).
- Saving fixes: GLOB/GMST edits previously were not written; fields after a truncated structure (old records) were lost;
  editing VMAD format corrupted records. Checked with `formtest` across all 106 types.
- The Type field (Mutagen's internal type) is no longer shown.

## 0.4.0

- Fixed open-world PC freezes: opening Tamriel used 5–12 GB (each cell retained its own copy of plugin world data).
  A 5×5 grid now uses about 0.5 GB of data; memory stays flat while flying, with long-unseen meshes/textures evicted.
- FPS limit (View → FPS limit, default 60, background 15).
- Gizmo arrows/rings can be grabbed with the mouse; world-axis rotation works; no crash on exit.
- Doors: double-click a teleport door (or Linked door in Inspector) to open its destination cell.
- Cell View lists loaded-cell objects on the right (click selects and focuses).
- Russian UI (View → Language), Keys and features help window (F1, Help menu).
- NPC/creature highlighting (NPC button, enabled by default).
- Log and Load Order default to closed (open from View); new plugins default to no ESL flag.
- Migrated to .NET 10.

## 0.3.0

- Reference properties in Inspector: flags, enable parent, owner (NPC / faction and rank), lock (level, key),
  teleport with **Link with a door…** (links two doors and places markers, including across cells),
  linked references, count and light radius.
- Reference fields: FormID / EditorID input, drag from Object Window, pick in 3D (**+**).
- **Edit fields** edits any record's fields; **Duplicate** copies a base record with a new FormID.
- Scripts: attached-script list, properties typed from .pex, add, remove and Auto-fill.
- Navmesh (**N**): display, vertex/triangle selection, move, delete, new vertices/triangles, vertex merge, flags
  (water, preferred, no large creatures). Neighbor links and lookup grid are recalculated.
  Finalize (NAVI, doors) was not yet available; these edits required finalization in CK at this release.

## 0.2.0

- Particles (fire, smoke, sparks), billboard nodes and effect-shader animation using the game's formula.
- NPCs/creatures replace green boxes: race, clothing, FaceGen faces, bind pose.
- World → New Interior Cell / New Worldspace; exterior cells are created where objects are placed.
- Landscape: height brushes (raise, lower, smooth, flatten), texture painting and flat terrain (Landscape panel, **H**).

## 0.1.0

- Load a mod setup (MO2 or Data), record catalog, Object Window, Cell View, Inspector, Load Order.
- 3D view: interiors, exteriors with landscape/water, lighting and CK-style camera.
- Object placement/movement, undo/redo, lossless saving to the active file (untouched records byte-for-byte unchanged).
