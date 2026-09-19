# Map Mouse Select

Marquee selection for the Nuclear Option tactical map (the fullscreen map on `M`).

The game only lets you pick map units one click at a time. This adds drag box selection in both map modes.

## What a map click means

A left click means different things in the two map modes, and the marquee follows the same split.

| Mode | Condition | A vanilla click does | A marquee does |
|---|---|---|---|
| Flying | You have a live aircraft | `CombatHUD.SelectUnit` adds a weapon/sensor target to your aircraft's target list | Adds every boxed unit with a HUD marker to that same target list |
| Commander | No live aircraft (ejected, dead, or observing) | `DynamicMap.SelectIcon` selects the unit so right-click move orders apply to it | Selects every boxed unit, so a follow-up right-click orders the whole group |

Shift while dragging adds to the current selection instead of replacing it.

## Install

Requires BepInEx 5 x64 in the game directory.

1. Build, or take `MapMouseSelect.dll` from a release.
2. Put it in `Nuclear Option/BepInEx/plugins/MapMouseSelect/`.
3. Start the game. Config appears at `BepInEx/config/com.kerbiter.mapmouseselect.cfg`.

## Controls

| Input | Action |
|---|---|
| Left drag on the map | Marquee select |
| Shift + left drag | Add to selection instead of replacing |
| Shift + click a selected unit | Deselect it |
| Middle drag on the map | Pan the map (moved off the left button by this mod) |
| `\` | Drop the selection, then select under the cursor, or the HUD designator while flying |
| `Delete` | Tap to drop your oldest target. Hold to keep only the newest |

Those two keys are registered as Rewired actions rather than as mod-only keyboard shortcuts, so they
show up in the game's own Controls menu, can be rebound to a gamepad or stick button, and are read with
the same timed-press calls the game uses for its target keys. They join the action category that
`Cancel` belongs to, which is why the menu lists them without any prefab changes.

Shift + click on a unit that is already selected is meant to deselect it, and this mod makes that work.
Vanilla has the branch but routes it to the wrong call in both modes: with no live aircraft it asks
`CombatHUD.DeSelectUnit`, which returns immediately on an empty target list, and with one it asks
`DynamicMap.DeselectIcon`, which clears the icon and the target list row but leaves the weapon locked to
the unit. The two other vanilla deselect paths, `TargetListSelector.ForceDeselect` and
`TargetListSelector_UnitItem.ToggleSelect`, route the other way round and are what this follows.

While the map is maximized, the bindings that put the throttle on Shift and Control are disabled. The game
binds them to the two directions of its `Throttle` axis and also uses Shift as the map's additive modifier,
so without this, adding to a selection spools the engine. Vanilla freezes the aircraft's axes, the camera
and the trigger while the map is open, and this is the same idea applied to those two bindings. It is
done per binding rather than per key: other keys bound to the throttle, a throttle lever, and Shift or
Control rebound to something else are all untouched, and so is a joystick. `BlockModifierThrottle` turns it
off if you would rather have vanilla's behavior.

`PanButton` exists because the game hardcodes left drag for panning, which collides with the marquee.
Set it to `Left` to keep vanilla panning; panning is then suppressed only while a marquee drag is in
progress.

A left click that never passes the drag threshold is replayed as the vanilla single click, and the
EventSystem icon-click path is untouched, so ordinary clicking still works.

## Configuration

| Section | Key | Default | Meaning |
|---|---|---|---|
| Hotkeys | `PanButton` | `Middle` | Mouse button that drags to pan the map |
| Hotkeys | `RequireModifier` | `false` | Require `MarqueeModifier` to start a marquee |
| Hotkeys | `MarqueeModifier` | `LeftAlt` | Modifier used when `RequireModifier` is on |
| Behavior | `ShiftAddsToSelection` | `true` | Shift extends the selection |
| Behavior | `BlockModifierThrottle` | `true` | While the map is open, disable the throttle bindings on Shift and Control |
| Behavior | `BatchTargetListWhileFlying` | `true` | Allow marquee while flying (weapon targets). Off disables it in that mode |
| Behavior | `FlyingHostilesOnly` | `false` | While flying, only box hostiles. Vanilla clicks allow any unit with a HUD marker |
| Behavior | `FollowFirstUnit` | `false` | In commander view, move the camera to the first boxed unit |
| Behavior | `EnableInMissionEditor` | `false` | Allow the marquee in the mission editor, which has its own box select |
| Behavior | `MaxTargets` | `128` | Cap on targets a marquee may add. The game itself refuses more than 128 |
| Visual | `ShowSelectionBox` | `true` | Draw the drag box outline |
| Visual | `BoxOutlineColor` | blue | Outline color for a replacing selection |
| Visual | `BoxOutlineColorAdditive` | green | Outline color for an additive selection (Shift held) |
| Visual | `OverlaySortingOrder` | `32000` | Sorting order of this mod's overlay canvas |

## Build

```powershell
dotnet build src\MapMouseSelect\MapMouseSelect.csproj
```

The build needs your game path. It resolves `-p:NuclearOptionRoot=...` first, then
`Directory.Build.user.props`, then the `NUCLEAR_OPTION_ROOT` environment variable. Copy
`Directory.Build.user.props.example` to get started; without it the build stops with an explanation
rather than guessing.

The DLL is copied into `BepInEx\plugins\MapMouseSelect\` by default. Pass
`-p:DeployToPlugins=false` to skip that.

`tools\Find-GameTypes.ps1` indexes the game's types through Mono.Cecil from BepInEx, which is handy
for finding things without opening a decompiler:

```powershell
.\tools\Find-GameTypes.ps1 -Pattern 'Map|Select' -Methods
```

## How it works

`DynamicMap.MapControls` runs every frame while the map is maximized. A Harmony prefix gives the
marquee first refusal on the frame; returning false skips vanilla controls entirely, which is how
both the click-select and the left-drag pan are held off during a drag.

The pan button moves via a transpiler that swaps the single `Input.GetMouseButton(0)` call inside
`MapControls` (the pan gate) for one that reads `PanButton`.

Unit picking mirrors `DynamicMap.SelectFromMap`: nearest icon within 10000 squared pixels for
clicks, and a screen-space rectangle over `UnitMapIcon.transform.position` for the marquee. That
comparison works in raw `Input.mousePosition` pixels because the game's canvases are
`ScreenSpaceOverlay` with a null camera, which is also why this mod's rectangle draws on its own
overlay canvas with a scale factor of 1.

Both modes filter through `TargetListSelector.CheckExclusions`, the player's own Target List panel
filters, exactly like vanilla clicks do.

While flying, the batch is applied with a single `WeaponManager.TargetListChanged()` rather than one
per unit. That list is networked through `SetStationTargets`, so a 20 unit marquee costs one message
instead of twenty.

## Limitations

- Marquee is skipped in the mission editor by default, since the editor already has its own
  drag box select on `UnitSelection`.
- The box is drawn over everything, including the map tooltip.
- A marquee that starts and ends on the same icon can also fire the game's own EventSystem click.
  The dedup guards make that harmless but it is a double-handled click.
- Panning is not a bindable game action, so `PanButton` is mod-only and the in-game controls menu
  will not show it.

## Status

The plugin loads and both Harmony patches register, but the marquee itself is not yet confirmed to
work in game. Unverified: the overlay rectangle's placement and scale, whether `DynamicMap.Update`
is the right per-frame driver, and the rectangle versus EventSystem click interaction listed above.

## License

GPL-3.0. See LICENSE.

Unofficial mod, not affiliated with the developers of Nuclear Option. It ships no game code or
assets: the build references the installed `Assembly-CSharp.dll` and Unity modules with
`Private=false`, so they are never copied into the output.
