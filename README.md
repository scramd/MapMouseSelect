# Map Mouse Select

Marquee selection for the Nuclear Option tactical map (the fullscreen map on `M`).

The game only lets you pick map units one click at a time. This adds drag box selection in both map modes.

## What a map click means

A left click means different things in the two map modes, and the marquee follows the same split.

| Mode | Condition | A vanilla click does | A marquee does |
|---|---|---|---|
| Flying | You have a live aircraft | `CombatHUD.SelectUnit` adds a weapon/sensor target to your aircraft's target list | Adds every boxed unit with a HUD marker to that same target list |
| Commander | No live aircraft (ejected, dead, or observing) | `DynamicMap.SelectIcon` selects the unit so right-click move orders apply to it | Selects every boxed unit, so a follow-up right-click orders the whole group |

Shift while dragging adds to the current selection instead of replacing it, and Ctrl while dragging takes the
boxed units back out of it, with the border colour showing which of the three is in force. Control wins over
Shift when both are held.

The marquee is deliberately dumb about factions: anything that survives the map's own filters gets boxed, so
using those filters well is part of the job.

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
| Ctrl + left drag | Take the boxed units out of the selection |
| Shift + click a selected unit | Deselect it |
| Middle drag on the map | Pan the map (moved off the left button by this mod) |
| Double left click on the map | Move the camera there, which vanilla puts on the middle button |
| `\` | Select only the unit under the cursor, dropping the rest of the target list |
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

Clicking a map icon also has to stay a click: the release has to land within the game's drag threshold of
the press. uGUI only cancels a click when the pressed object has somewhere to hand the drag to, and
nothing on the map has, since its panning is axis driven inside `MapControls`. Vanilla therefore lets you
press an icon, drag across the map and release back on that icon to select it. This mod applies the
threshold the mission editor's own box select uses, which is `EventSystem.pixelDragThreshold`.

While the map is maximized, any keyboard binding that sits directly on a key listed in `ModifierKeys` is
disabled, whatever action it belongs to. By default that is Shift and Control, which is what stops the
throttle: the game hangs its two directions on those keys and also uses Shift as the map's additive
modifier, so adding to a selection would otherwise spool the engine. Vanilla freezes the aircraft's axes,
the camera and the trigger while the map is open, and this is the same idea applied to those keys.

It is done per binding rather than per key, so nothing about your layout is assumed: a binding such as
`Control+Shift+E`, where those keys are declared modifiers, is left alone; other keys bound to the same
action keep working, as does a lever, since only the keyboard is scanned; and a binding this turned off is
the only thing it turns back on. Nothing is written to Rewired's saved user data. Set `ModifierKeys` to
whatever your layout uses, or turn off `SuppressModifierBindings` to get vanilla behavior back.

`PanButton` exists because the game hardcodes left drag for panning, which collides with the marquee.
Set it to `Left` to keep vanilla panning; panning is then suppressed only while a marquee drag is in
progress. Vanilla's camera jump is on the middle button, which makes it collide with `PanButton`, so a
double left click does it instead, counted the way uGUI counts one: a second press within 0.3s of the
previous press, close enough that the game's drag threshold says the pointer did not travel. The `Jump Map`
action loses only its binding on `PanButton`, and only while the map is open, so anything else bound to it, a
gamepad button included, still moves the camera.

A left press that never passes the drag threshold is left entirely to vanilla: the marquee only acts once
the gesture is a drag, and it never takes anything away from the mouse click path, which is the
EventSystem one.

## Configuration

| Section | Key | Default | Meaning |
|---|---|---|---|
| Hotkeys | `PanButton` | `Middle` | Mouse button that drags to pan the map |
| Hotkeys | `RequireModifier` | `false` | Require `MarqueeModifier` to start a marquee |
| Hotkeys | `MarqueeModifier` | `LeftAlt` | Modifier used when `RequireModifier` is on |
| Behavior | `ShiftAddsToSelection` | `true` | Shift extends the selection |
| Behavior | `ControlRemovesFromSelection` | `true` | Ctrl takes the boxed units out of the selection |
| Behavior | `SuppressModifierBindings` | `true` | While the map is open, disable keyboard bindings that sit directly on `ModifierKeys` |
| Behavior | `ModifierKeys` | Shift, Control | Keys that act only as map modifiers while the map is open |
| Behavior | `BatchTargetListWhileFlying` | `true` | Allow marquee while flying (weapon targets). Off disables it in that mode |
| Behavior | `FollowFirstUnit` | `false` | In commander view, move the camera to the first boxed unit |
| Behavior | `EnableInMissionEditor` | `false` | Allow the marquee in the mission editor, which has its own box select |
| Behavior | `MaxTargets` | `128` | Cap on targets a marquee may add. The game itself refuses more than 128 |
| Visual | `ShowSelectionBox` | `true` | Draw the drag box outline |
| Visual | `BoxOutlineColor` | blue | Outline color for a replacing selection |
| Visual | `BoxOutlineColorAdditive` | green | Outline color for an additive selection (Shift held) |
| Visual | `BoxOutlineColorRemove` | red | Outline color for a deselecting marquee (Ctrl held) |
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
