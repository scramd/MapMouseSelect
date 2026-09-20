# Map Mouse Select

Marquee selection for the Nuclear Option tactical map (the fullscreen map on `M`).

The game only lets you pick map units one click at a time. This adds drag box selection in both map modes.

## What a map click means

A left click means different things in the two map modes, and the marquee follows the same split.

| Mode | Condition | A vanilla click does | A marquee does |
|---|---|---|---|
| Flying | You have a live aircraft | `CombatHUD.SelectUnit` adds a weapon/sensor target to your aircraft's target list | Replaces that target list with every boxed unit that has a HUD marker, which a click cannot do |
| Commander | No live aircraft (ejected, dead, or observing) | `DynamicMap.SelectIcon` selects the unit so right-click move orders apply to it | Selects every boxed unit, so a follow-up right-click orders the whole group |

Shift while dragging adds to the current selection instead of replacing it, and Ctrl while dragging takes the
boxed units back out of it, with the border colour showing which of the three is in force. Control wins over
Shift when both are held.

The marquee is deliberately dumb about factions: anything that survives the map's own filters gets boxed, so
using those filters well is part of the job. In flight a committed box plays the HUD's selection sound once,
the same way the game's own multi-select does, rather than once per boxed unit; a box in commander mode is
silent, as vanilla's icon clicks are there.

A box that ends up empty (a release over clear map) clears what was selected, in either mode, when the drag
was a plain one. In flight it plays vanilla's deselect-all sound, the one its Cancel-hold uses; in commander
view it is silent, as vanilla's own selection changes there are. That is a mod choice rather than vanilla
behaviour: a click on empty map space does nothing in this game, because the map's own controls only ever act
on an icon under the cursor. Shift and Ctrl drags leave an empty box alone, since there is nothing to add or
take out.

## Destroyed buildings

On by default, and off in one switch (`Destroyed.Enabled`). A destroyed building keeps its map icon, drawn
darker, instead of vanishing from the map. Vanilla drops the
icon in `UnitMapIcon.UnitMapIcon_OnUnitDisabled`, and that building is not gone: `Building.UnitDisabled` spawns
the wreckage model and leaves the unit in place, disabled and repairable. Blocking the icon removal is the whole
change, so the wreck keeps its position, its scaling and its theme colour, and the tint is applied afterwards by
multiplying the colour, defaulting to 45% of it. Repairing the building puts the normal colour back.

A wreck shows whether or not the game ever put it on the map. `DynamicMap.SpawnQueuedIcons`, the only thing that
creates unit icons, refuses a unit that is disabled, so a building that dies before its first icon is made, or one
that is already a wreck when a client generates its map, would otherwise have no icon to keep. Every path that
decides a unit belongs on the map asks for its icon through `DynamicMap.AddIcon`, so that is where the refusal is
undone, for wrecks only and only for units whose definition asks for an icon (`mapIconSize > 0`, vanilla's own
"not worth an icon" case).

Whether a wreck can be selected is the `WRECK` filter's business, not the icon's. Vanilla's clicks, the `Select`
action and the marquee all pass through `TargetListSelector.CheckExclusions`, so an unlit filter is what makes a
wreck unselectable, and the icon is left alone otherwise: it still hovers for its tooltip, which a cleared
raycast target would have taken away.

Two extra groups join the game's Target List filter pane, because neither fits what vanilla models there: the
faction buttons carry a same-faction flag rather than a faction, so neutral units can never be filtered, and
nothing at all knows about destroyed buildings. They are half-width copies of the pane's own faction buttons,
sharing the slot after them, and they are added to the row when the pane starts:

| Button | Lit means | Unlit means | Starts |
|---|---|---|---|
| `NTL` | Units that are neither friendly nor hostile are shown | Neutral units are filtered out | Lit, as vanilla behaves |
| `DED` | Wrecks can be selected again | Wrecks are filtered out, so they cannot be selected | Unlit, so wrecks are inert by default; only added when `Destroyed.Enabled` is on |

Both copies are placed from the measured right edge of the faction buttons, and each is half of a normal button
less the gap the row leaves between its own buttons, so together they take exactly one button's worth of the row
with `NTL` first. The placement is measured in world space after forcing a canvas layout, so it holds whatever
the row and the canvas scale themselves to.

They behave like the pane's own filters: clicking toggles them, `Reset` turns them back on, and changing one
re-checks the icons the way a vanilla filter does. Filtering a wreck out also deselects it, if it was selected.
Both buttons are kept out of vanilla's filter lists on purpose, since those lists are indexed by code that
expects each entry to carry unit definitions or unit types.

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
| Left click a unit | Leave only that unit selected |
| Shift + click a selected unit | Deselect it |
| Middle drag on the map | Pan the map (moved off the left button by this mod) |
| Double left click on the map | Move the camera there, which vanilla puts on the middle button |
| `\` | Select only the unit being pointed at, dropping the rest of the target list |
| `-` | Drop the unit being pointed at out of the selection |
| `Delete` | Tap to drop your oldest target. Hold to keep only the newest |

Those three keys are registered as Rewired actions rather than as mod-only keyboard shortcuts, so they
show up in the game's own Controls menu, can be rebound to a gamepad or stick button, and are read with
the same timed-press calls the game uses for its target keys. They join the action category that
`Cancel` belongs to, which is why the menu lists them without any prefab changes. The mod binds them to
their default keys once Rewired is up, which is also what puts a default in front of anyone whose saved
controls layout predates them, as long as nothing is bound to either action already.

Shift + click on a unit that is already selected is meant to deselect it, and this mod makes that work.
Vanilla has the branch but routes it to the wrong call in both modes: with no live aircraft it asks
`CombatHUD.DeSelectUnit`, which returns immediately on an empty target list, and with one it asks
`DynamicMap.DeselectIcon`, which clears the icon and the target list row but leaves the weapon locked to
the unit. The two other vanilla deselect paths, `TargetListSelector.ForceDeselect` and
`TargetListSelector_UnitItem.ToggleSelect`, route the other way round and are what this follows.

A plain mouse click also replaces the target list while flying. Vanilla's branch only ever adds when an
aircraft is live, because the `UnselectAll` it runs in commander view sits behind a test for exactly that, so
clicking unit after unit quietly accumulates targets and nothing about the click says so. The mod drops every
target except the clicked unit instead, which makes a plain click mean the same thing as a plain drag, and
leaves Shift as the additive click. The `Select` key keeps vanilla's behaviour.

That works for a unit that is already selected too, which it did not before. `MapIcon.SelectIcon` clears the
icon's raycast target and vanilla only puts it back when the icon is deselected, and a uGUI graphic that is not
a raycast target gets no pointer events at all: the click landed on the map behind the icon. Vanilla's own
shift-click deselect branch inside `UnitMapIcon.ClickIcon` was unreachable for the same reason, so the mod puts
the raycast target back on unit icons while they are selected. A side effect worth knowing is that a selected
unit can be hovered for its tooltip again, which vanilla also could not do. Airbase and own-aircraft icons are
left as vanilla clears them.

`\` prunes the target list down to whatever is being pointed at: the map icon nearest the cursor with the map
up (`DynamicMap.SelectFromMap`'s 100 pixel pick), or the HUD marker nearest the target designator with it closed
(`CombatHUD.TargetSelect`'s pick). It applies the prune itself rather than delegating, because both of those
vanilla picks skip a unit that is already selected, and a target that is already selected is exactly what the
key is asked to keep; pruning directly also means a press that lands on empty sky changes nothing, instead of
losing the selection to a pick that never came. Pointing at a unit that is not a target replaces the list with
it, the same as a plain click does in commander view.

`-` is that key's mirror: the same pick, and one removal instead of a prune. Flying, it takes the unit out of
the weapon target list with the marker, the map icon, one refresh and one deselect sound, which is what
shift-clicking the map does. With no aircraft it is the map selection that loses the unit. Pointing at nothing
does nothing, and the filters gate the pick, so a unit they hide cannot be dropped this way.

Nothing the mod does deselects a target that is meant to stay in the selection, at any point in the call. There
is no clear-then-re-add anywhere in the flying target list: a replace box drops only the targets the box leaves
out, `\` drops only what is not the unit being pointed at, the `` Delete `` hold drops only what is not the newest
target, and a plain click drops only what is not the unit clicked. That keeps the kept targets' markers, map
icons and HUD entries from blinking out and back, and keeps the server from being sent an empty list and then a
refill. Vanilla's own clears, `CombatHUD.DeselectAll` and `DynamicMap.UnselectAll`, are left to the two places
where nothing is meant to survive: an empty replace box, and commander view, where `UnselectAll` is what also
cancels a half-built waypoint and is what vanilla's own click does.

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
| Destroyed | `Enabled` | `true` | Keep destroyed buildings on the map as darkened wrecks, and add the `DED` filter |
| Destroyed | `Tint` | `0.45` | How much of its usual colour a wrecked building's icon keeps |
| Target list | `FilterNeutral` | `true` | Add the `NTL` button to the Target List filter pane |

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

## Launch

`.vscode\launch.json` builds, deploys and then starts the game with `-autoHost`, which is the game's
own shortcut into a hosted Free Flight mission: it waits for the main menu, hosts without a menu
being touched, and defaults the mission name to "Free Flight". The game is a release Mono build, so
this launches the process rather than offering managed breakpoints.

The install path is the `nuclearOption.root` setting in `.vscode\settings.json`, which the build task
uses too, so there is one place to edit if the game moves. That file is gitignored because it is a
machine path, and the key is the one thing to add yourself; without it the build and the launch stop
on an unresolved variable rather than guessing.

`NUCLEAR_OPTION_ROOT` cannot stand in for it: VS Code resolves `${env:...}` against the environment it
was started with, so a variable set after that is missing from tasks and launches even though new
terminals see it.

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
filters, exactly like vanilla clicks do. An icon the player has switched off in Map Options is left
out as well: those icons are inactive objects, and vanilla cannot select one either, since a click
needs a raycast hit and the range select needs the HUD marker's image enabled.

While flying, both directions are batched: adding writes the target list, each unit's HUD marker and
the map icon, then calls `WeaponManager.TargetListChanged()` once, and removing applies
`CombatHUD.DeSelectUnit`'s per-unit effects the same way. That list is networked through
`SetStationTargets`, so a 20 unit marquee costs one message instead of twenty, and the HUD's own
select or deselect sound plays once for the gesture, the way `CombatHUD.TargetSelect` plays it once
for its whole batch. An empty box while flying drops the target list in one `CombatHUD.DeselectAll` call, with
its deselect-all sound. Each of the keys that change targets sounds like what it did: dropping targets plays
the deselect sound, and a drop that ends on a new selection instead plays the select sound once rather than
both.

## Limitations

- Marquee is skipped in the mission editor by default, since the editor already has its own
  drag box select on `UnitSelection`.
- The box is drawn over everything, including the map tooltip.
- A marquee that starts and ends on the same icon can also fire the game's own EventSystem click.
  The dedup guards make that harmless but it is a double-handled click.
- Panning is not a bindable game action, so `PanButton` is mod-only and the in-game controls menu
  will not show it.

## Vanilla fixes

Three game paths assume every unit they are handed has a map icon or a HUD marker, and throw when it does
not. They are in the mod because the wreck feature is what makes a unit with an icon but no marker an everyday
thing, and because the same assumption is what vanilla's own radar and missile warnings hit:

- `DynamicMap.GetOrAddIcon` queues an icon and then returns `iconLookup[unit]`, which throws
  `KeyNotFoundException` for a unit that gets no icon: a disabled one, or one whose definition asks for none.
  It returns no icon now.
- `DynamicMap.FlagIncomingMissile` calls that and dereferences the result. It skips a unit with no icon.
- `CombatHUD.HighlightMarker` indexes `markerLookup` directly, reached from a radar warning, and
  `CombatHUD.CreateMarker` refuses to give a marker to a unit that is disabled, is `Scenery`, or turns up while
  the player has no aircraft. A radar contact that was already wrecked when it was detected is the everyday case.
  The highlight is skipped when there is no marker.

A fourth is not a crash but a colour that goes missing. `MapIcon.UpdateColor` sets the theme colour and stops
there, while the dimming a filtered out unit carries lives one level down in
`UnitMapIcon.UnitMapIcon_UpdateColor`. Anything that calls the base method directly therefore draws a
filtered out unit as if it were live, and several things do: `SelectIcon`, `DeselectIcon`, and
`DynamicMap.UnselectAll`, which deselects every icon on the map whether or not it was selected. Since this
mod's batched deselection reaches those paths far more often than vanilla clicks do, the base method now
ends in the unit icon's own update, through a postfix with a depth counter so its own call back into the
base method cannot apply the tint twice.

## Status

The plugin loads and both Harmony patches register, but the marquee itself is not yet confirmed to
work in game. Unverified: the overlay rectangle's placement and scale, whether `DynamicMap.Update`
is the right per-frame driver, and the rectangle versus EventSystem click interaction listed above.

## License

GPL-3.0. See LICENSE.

Unofficial mod, not affiliated with the developers of Nuclear Option. It ships no game code or
assets: the build references the installed `Assembly-CSharp.dll` and Unity modules with
`Private=false`, so they are never copied into the output.
