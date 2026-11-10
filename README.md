# Map Mouse Select

Drag a box on Nuclear Option's expanded tactical map to select units together. A plain box replaces the selection. Shift adds units. Ctrl removes units. In flight, these operations change the current weapon station's target list. Without a live aircraft, they change the map selection.

This fork focuses on selection. It still keeps some navigation and target-key features for the owner to decide on. The controls below describe the code, not verified play on the current game. The plugin builds against the supplied 0.34.6 assemblies. It has not been run in that game version.

## Controls

| Control | Action |
| --- | --- |
| Left drag on the expanded map | Replace the selection with units inside the box |
| Shift + left drag | Add boxed units |
| Ctrl + left drag | Remove boxed units; Ctrl wins over Shift |
| Left click a unit icon | Select it; in flight, keep only that target |
| Shift + left click a selected unit | Deselect it, including its weapon target entry |
| Middle drag | Pan the map |
| Double left click | Jump the commander camera to the cursor; not while you have an aircraft |

The box needs a mouse.

A replacing box containing no eligible units clears the selection. An empty additive or removing box does nothing. A click on empty map space does not clear it. The box respects the Target List filters and inactive map icons. It excludes your own aircraft. For boxes and ordinary clicks in flight, a new target also needs a HUD marker. Airbases are not box-selected. In commander view, stock right click can order selected friendly commandable units, but only when the first selected icon is friendly and commandable. A mixed box may therefore accept no order. A replacing commander box also clears existing waypoints.

`NTL`, an extra Target List filter, excludes neutral units when unlit. It starts lit. Stock faction filters only cover friendly and enemy units. The filter dims excluded icons and affects both map selection and HUD target designation. It does not remove those icons from the map. When the map has no viewing faction, the native Spectator mode makes NTL ineffective.

## Install and configure

Use BepInEx 5 x64 in the game directory. Build this fork, then place its `MapMouseSelect.dll` in `BepInEx/plugins/MapMouseSelect/`. Do not install it alongside the original: they share a plugin GUID and input action names. An upstream release does not contain the changes in this fork.

The plugin writes `BepInEx/config/com.kerbiter.mapmouseselect.cfg`. Edit it with the game closed, or use BepInEx ConfigurationManager (F1) if installed. ConfigurationManager is optional. Restart after changing whether to create NTL. [All settings](docs/configuration.md).

Compared with stock controls, left drag selects and middle drag pans. A plain mouse click in flight replaces targets instead of adding. Selected unit icons remain clickable. Empty-list deselection and clear-all events are repaired so map selection and Target List rows can stay in step. Bare Shift and Ctrl bindings are disabled while the expanded map is open, by default. Declared key combinations and other device bindings remain active. Pan movement follows cursor displacement by default; an optional mouse stick mode scrolls instead.

## Limits

- From a pending left press through a box drag, the plugin skips the entire stock map-control method. Zoom, Move Map axes, native Select/Jump Map, waypoints, map follow, edge clamp and view-indicator placement pause too. This also pauses NuclearMapPan's controller movement.
- A press is gated by the map rectangle, not the top UI hit. If a pane overlaps that rectangle, dragging it may also arm a box and change selection. Actual prefab overlap needs a game check.
- Restoring pointer hits on selected icons also lets the native Select action pick one again. In flight it can do nothing instead of taking the next eligible icon.
- If the map stops updating during a drag, static gesture state and the persistent outline can remain until another map updates. There is no independent teardown cancellation.
- With RequireModifier on, the default Alt shortcut does not arm while an extra non-mouse key is held. Press Shift/Ctrl after starting the box, or configure a shortcut that includes the desired keys.
- The release click guard checks distance from the press at release. A drag that returns to its starting icon can still produce a second click.
- Input has no complete focus/menu cancellation policy. Modifier and camera-jump suppression temporarily change Rewired bindings. Avoid treating this as an isolated input layer.
- The editor option gates only boxes. Pan relocation, click patches, modifier suppression and camera jump remain active in the editor. Shift-click can change map selection without updating editor multi-selection; enabling boxes does not coordinate the systems.
- The move cursor uses Windows APIs. Other platforms should turn `Visual.PanCursor` off; cross-platform cursor behaviour is not established.
- An upstream airbase-selection report has no confirmed cause or current-game reproduction here. The colour API fix does not prove that report resolved.
- Static inspection finds no direct patch conflict with NuclearMinimap, NuclearMapMarkings or NuclearMapTrails. This does not establish runtime compatibility. See the [coupling study](docs/coupling.md).

## Lineage and scope

This is a fork of **Map Mouse Select (& more)** by **Kerbiter (Metadorius)**, [Metadorius/MapMouseSelect](https://github.com/Metadorius/MapMouseSelect). The fork starts from upstream commit [`6e08c30fd6d5315d6ce84650c2f91956e2cb7ab7`](https://github.com/Metadorius/MapMouseSelect/commit/6e08c30fd6d5315d6ce84650c2f91956e2cb7ab7). Its history and copyright remain here. This modified version is maintained in `scramd/MapMouseSelect`; the selection-scope and compatibility changes are dated **2026-11-10**.

The marquee, overlay, click changes, target batching, map navigation changes and neutral filter come from Kerbiter's mod. This fork removes the unfocused frame-rate cap, safe aircraft exit, destroyed-building icons and DED filter, camera-wheel masking, unrelated radar icon/HUD-marker guards, the three target keys with their Controls menu entries, and the option that made the camera follow the first boxed unit. They do not supply map selection. It also removes the obsolete colour rerouting now handled by the game's virtual colour update. [Feature decisions and borderline items](docs/scope.md).

The original code was written with heavy AI assistance. AI assistance also contributed to this fork's edits and documentation; a build and static review do not establish in-game behaviour.

The plugin GUID, displayed name, version `0.3.0`, `meta.json` (including its upstream game value `0.34.1`) and licence are deliberately unchanged. They are owner decisions before distribution, not a claim that upstream's package is this fork.

## Build

Install a .NET SDK with net472 build support. Supply your own legally obtained client game and BepInEx assemblies. Do not ship those assemblies.

```powershell
dotnet build src/MapMouseSelect/MapMouseSelect.csproj -c Release -p:NuclearOptionRoot="D:/Games/Nuclear Option" -p:DeployToPlugins=false
```

For separate reference folders:

```powershell
dotnet build src/MapMouseSelect/MapMouseSelect.csproj -c Release -p:NuclearOptionRoot="X:/none" -p:ManagedDir="D:/references/managed" -p:BepInExCore="D:/references/bepinex-core" -p:DeployToPlugins=false
```

`NuclearOptionRoot` can also come from `Directory.Build.user.props` (copy the example) or `NUCLEAR_OPTION_ROOT`. Explicit command-line reference paths override the default folders. Output is `src/MapMouseSelect/bin/Release/MapMouseSelect.dll`. Always use `DeployToPlugins=false` for inspection or packaging: the project otherwise copies into the game. The inherited VS Code launch task can also deploy and launch; do not use it for a build-only check.

[Developer guide](docs/architecture.md), [game-update checks](docs/updating.md), [next input step](docs/input-next.md), [review decisions](docs/review-log.md).

## Licence

GPL-3.0. See [LICENSE](LICENSE). Copyright (C) 2026 Metadorius. Keep that notice and the GPL terms when redistributing. Provide corresponding source as required by the licence.

This project is an unofficial community modification and is not affiliated with, sponsored by, or endorsed by Shockfront Studios Pty Ltd. Original Nuclear Option assets, vehicle designs, audio, and code are Copyright (c) 2026 Shockfront Studios Pty Ltd. All rights reserved. Nuclear Option and Shockfront Studios are trademarks or registered trademarks of Shockfront Studios Pty Ltd. Original mod content and all other trademarks belong to their respective owners.
