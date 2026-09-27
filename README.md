# Map Mouse Select

Marquee selection for the Nuclear Option tactical map, the fullscreen map on `M`. The game selects one unit per click, and this mod adds drag box selection in both map modes.

## What a map click means

| Mode | Condition | A vanilla click does | A marquee does |
|---|---|---|---|
| Flying | You have a live aircraft | `CombatHUD.SelectUnit` adds a weapon/sensor target to your aircraft's target list | Replaces that target list with every boxed unit that has a HUD marker, which a click cannot do |
| Commander | No live aircraft (ejected, dead, or observing) | `DynamicMap.SelectIcon` selects the unit so right-click move orders apply to it | Selects every boxed unit, so a follow-up right-click orders the whole group |

Shift while dragging adds to the current selection instead of replacing it. Ctrl while dragging takes the boxed units back out of it. Control wins over Shift when you hold both, and the border colour shows which of the three is in force.

The marquee ignores factions on purpose, so use the map's own filters well: it boxes whatever they let through. In flight a committed box plays the HUD's selection sound once, not once per boxed unit, and a box in commander mode is silent.

A drag that ends up empty, a release over clear map, clears what was selected, in either mode. That is a mod choice, not vanilla behaviour. In flight it plays vanilla's deselect-all sound, the one its Cancel-hold uses, and in commander view it is silent. A click on empty map space still does nothing, since the map's controls only ever act on an icon under the cursor, and Shift and Ctrl drags leave an empty box alone.

## Destroyed buildings

The feature is on by default, and one switch turns it off (`Destroyed.Enabled`). A destroyed building keeps its map icon, drawn darker, instead of vanishing from the map. Vanilla drops it in `UnitMapIcon.UnitMapIcon_OnUnitDisabled`, but `Building.UnitDisabled` only spawns the wreckage model and leaves the unit in place, disabled and repairable, so blocking that removal is the whole change. The wreck keeps its position, its scaling and its theme colour, the mod multiplies the colour by a tint that defaults to 45%, and repairing the building puts the normal colour back.

The map shows a wreck whether or not the game ever put it there. `DynamicMap.SpawnQueuedIcons` is the only thing that creates unit icons, and it refuses a disabled unit, so a building that dies before its first icon is made, or that is already a wreck when a client generates its map, would have no icon to keep. The mod undoes that refusal in `DynamicMap.AddIcon`, where every path that puts a unit on the map asks for its icon, and only for wrecks whose definition asks for an icon (`mapIconSize > 0`, vanilla's own "not worth an icon" case).

The `WRECK` filter decides whether you can select a wreck, not the icon. Vanilla's clicks, the `Select` action and the marquee all pass through `TargetListSelector.CheckExclusions`, so an unlit filter is what makes a wreck unselectable. The mod leaves the icon's raycast target alone, because it still hovers for its tooltip.

Two extra groups join the filter pane of the game's Target List, because neither fits what vanilla models there. The faction buttons carry a same-faction flag, not a faction, so neutral units can never be filtered, and nothing knows about destroyed buildings. The new groups are half-width copies of the pane's own faction buttons, added to the row when the pane starts:

| Button | Lit means | Unlit means | Starts |
|---|---|---|---|
| `NTL` | Units that are neither friendly nor hostile are shown | Neutral units are filtered out | Lit, as vanilla behaves |
| `DED` | Wrecks can be selected again | Wrecks are filtered out, so they cannot be selected | Unlit, so wrecks are inert by default. Added only when `Destroyed.Enabled` is on |

The mod places both copies from the measured right edge of the faction buttons, in world space after forcing a canvas layout, so the placement holds whatever the row and the canvas scale themselves to. Each copy is half a normal button less the row's gap, and together they take one button's worth of the row, with `NTL` first.

They behave like the pane's own filters. Clicking toggles them, `Reset` turns them back on, and changing one re-checks the icons the way a vanilla filter does. Filtering a wreck out also deselects it. The mod keeps both out of vanilla's filter lists, because those lists are indexed by code that expects each entry to carry unit definitions or unit types.

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
| Hold middle and push the cursor | Scroll the map like a stick, once `MapStick` is on |
| Double left click on the map | Move the camera there, which vanilla puts on the middle button |
| `\` | Select only the unit being pointed at, dropping the rest of the target list |
| `-` | Drop the unit being pointed at out of the selection |
| `Delete` | Tap to drop your oldest target. Hold to keep only the newest |
| `Ctrl + E` | Get out of an aircraft that is parked at one of your own airbases. It does nothing anywhere else |

- The four keys are Rewired actions, so they show up in the game's own Controls menu and can be rebound, gamepad included. The mod writes each default key once and only where nothing is bound, then records the action by name in `Hotkeys.DefaultBindings` and never writes it again, so a key you clear stays cleared. The Controls menu's own "Restore Defaults" cannot restore one.
- `Ctrl+E` is the safe exit, the one key that is not about targets. It acts only when the aircraft is parked on the ground inside a friendly airbase's radius, the game's own `Aircraft.IsLanded` test (`radarAlt < 5`, `speed < 2.5`) plus the base. Anywhere else it does nothing, and vanilla's own eject stays on `Control+Shift+E`.
- Shift+click on a selected unit deselects it. Vanilla routes that to the wrong call in both modes, and the mod follows the two vanilla paths that route the other way round, `TargetListSelector.ForceDeselect` and `TargetListSelector_UnitItem.ToggleSelect`.
- A plain click in flight replaces the target list instead of adding to it, so it means the same thing as a plain drag and Shift stays additive. Vanilla only ever adds, because its `UnselectAll` sits behind a test for a live aircraft. The `Select` key keeps vanilla's behaviour.
- A click also works on an already selected unit, which it did not before. `MapIcon.SelectIcon` clears the icon's raycast target and vanilla only puts it back on deselect, so the mod puts it back while the icon is selected. The tooltip returns too, and vanilla's shift-click deselect branch becomes reachable. Airbase and own-aircraft icons stay as vanilla clears them.
- `\` prunes the target list to the unit being pointed at, using `DynamicMap.SelectFromMap`'s 100 pixel pick on the map and `CombatHUD.TargetSelect`'s pick with it closed. It prunes directly, because both vanilla picks skip an already selected unit, which is what the key must keep, so a press on empty sky changes nothing.
- `-` is the same pick with one removal instead of a prune. Flying, it removes the marker, the map icon, one refresh and one deselect sound. With no aircraft it removes the unit from the map selection. The filters gate the pick, so a unit they hide cannot be dropped this way.
- No mod action deselects a target that is meant to stay, so kept markers, map icons and HUD entries never blink out and back and the server is never sent an empty list and then a refill. Vanilla's own `CombatHUD.DeselectAll` and `DynamicMap.UnselectAll` are left to an empty replace box and to commander view, where `UnselectAll` also cancels a half-built waypoint.
- A click stays a click only if the release lands within the drag threshold of the press. Nothing on the map absorbs a drag, so vanilla lets a press, a drag and a release back on the same icon select it. A left press that never passes the threshold goes entirely to vanilla, and the mod applies the threshold the mission editor's box select uses, `EventSystem.pixelDragThreshold`.
- While the map is maximized, a keyboard binding sitting directly on a key in `ModifierKeys` is disabled, whatever action it belongs to. By default that is Shift and Control, the keys the game hangs its throttle on, and Shift is also the map's additive modifier. Vanilla freezes the axes, camera and trigger while the map is open, and this is the same idea.
- The suppression is per binding, not per key, so `Control+Shift+E` and other keys on the same action keep working, as does a lever. Only a binding it turned off is turned back on, and nothing is written to Rewired's saved user data. Set `ModifierKeys` to your layout, or turn off `SuppressModifierBindings`.
- Left drag is hardcoded for panning, so `PanButton` exists. Setting it to `Left` keeps vanilla panning and suppresses it only during a marquee drag. The camera jump moves to a double left click, counted as a second press within 0.3s of the previous one, close enough for the drag threshold. `Jump Map` loses only its `PanButton` binding, and only while the map is open.

## Configuration

Every setting is in the file the mod writes, `BepInEx\config\com.kerbiter.mapmouseselect.cfg`, and in the Configuration Manager window (F1) while the game runs. The three outline colours open a colour picker there, with the red, green, blue and alpha sliders, a hex field and a reset button. In the file they are `RRGGBBAA` hex, so `FF5A5ACC` is a mostly opaque red.

| Section | Key | Default | Meaning |
|---|---|---|---|
| Hotkeys | `PanButton` | `Middle` | Mouse button that drags to pan the map |
| Hotkeys | `RequireModifier` | `false` | Require `MarqueeModifier` to start a marquee |
| Hotkeys | `MarqueeModifier` | `LeftAlt` | Modifier used when `RequireModifier` is on |
| Hotkeys | `DefaultBindings` | empty, then one name per action | Actions whose default key the mod has already written. Removing a name has it written again, which is there so a key you clear stays clear |
| Behavior | `ShiftAddsToSelection` | `true` | Shift extends the selection |
| Behavior | `ControlRemovesFromSelection` | `true` | Ctrl takes the boxed units out of the selection |
| Behavior | `SuppressModifierBindings` | `true` | While the map is open, disable keyboard bindings that sit directly on `ModifierKeys` |
| Behavior | `ModifierKeys` | Shift, Control | Keys that act only as map modifiers while the map is open |
| Behavior | `BatchTargetListWhileFlying` | `true` | Allow marquee while flying (weapon targets). Off disables it in that mode |
| Behavior | `FollowFirstUnit` | `false` | In commander view, move the camera to the first boxed unit |
| Behavior | `EnableInMissionEditor` | `false` | Allow the marquee in the mission editor, which has its own box select |
| Behavior | `MaxTargets` | `128` | Cap on targets a marquee may add. The game itself refuses more than 128 |
| Behavior | `MapPanFix` | `true` | Make the map follow the cursor while it is dragged, by reading the cursor instead of the game's mouse axes |
| Behavior | `MapPanSpeed` | `1` | How far the map moves per pixel of mouse movement while dragging it, used while `MapPanFix` is on |
| Behavior | `MapStick` | `false` | Scroll the map like a stick while the pan button is held, instead of dragging it |
| Behavior | `MapStickGain` | `8` | Scroll speed the stick gains per pixel of push, in screen pixels per second, with no ceiling |
| Visual | `ShowSelectionBox` | `true` | Draw the drag box outline |
| Visual | `PanCursor` | `true` | Show Windows' move cursor while the pan button is held |
| Visual | `FrameLimitUnfocused` | `true` | Cap the frame rate while the game is not focused. A host hands the cap back while players are connected |
| Visual | `FrameLimitUnfocusedFps` | `12` | Frame rate to cap to while unfocused, 1 to 240. The GPU load falls with the rate |
| Visual | `FrameLimitAllowWorldSlowdown` | `false` | Let the rate go below the floor that keeps the world in real time (`1 / Time.maximumDeltaTime` plus one, 11 here), down to 1 |
| Visual | `FrameLimitWhileHosting` | `false` | Cap the frame rate while hosting a game other players are connected to. A host answers its players on the frames it takes, so the cap stands down by default |
| Visual | `BoxOutlineColor` | `3CB4FFCC` | Outline color for a replacing selection |
| Visual | `BoxOutlineColorAdditive` | `7CE38BCC` | Outline color for an additive selection (Shift held) |
| Visual | `BoxOutlineColorRemove` | `FF5A5ACC` | Outline color for a deselecting marquee (Ctrl held) |
| Visual | `OverlaySortingOrder` | `32000` | Sorting order of this mod's overlay canvas |
| Destroyed | `Enabled` | `true` | Keep destroyed buildings on the map as darkened wrecks, and add the `DED` filter |
| Destroyed | `Tint` | `0.45` | How much of its usual colour a wrecked building's icon keeps |
| Target list | `FilterNeutral` | `true` | Add the `NTL` button to the Target List filter pane |

## Build

```powershell
dotnet build src\MapMouseSelect\MapMouseSelect.csproj
```

The build needs your game path. It resolves `-p:NuclearOptionRoot=...` first, then `Directory.Build.user.props`, then the `NUCLEAR_OPTION_ROOT` environment variable, and stops with an explanation rather than guessing. Copy `Directory.Build.user.props.example` to get started. By default the build copies the DLL into `BepInEx\plugins\MapMouseSelect\`, and `-p:DeployToPlugins=false` skips that.

`tools\Find-GameTypes.ps1` indexes the game's types through Mono.Cecil from BepInEx, handy for finding things without opening a decompiler:

```powershell
.\tools\Find-GameTypes.ps1 -Pattern 'Map|Select' -Methods
```

## Launch

`.vscode\launch.json` builds, deploys and starts the game with `-autoHost`, the game's own shortcut into a hosted Free Flight mission. It waits for the main menu, hosts without a menu being touched, and names the mission "Free Flight". The game is a release Mono build, so there are no managed breakpoints.

The install path is the `nuclearOption.root` setting in `.vscode\settings.json`, which the build task uses too, so one edit covers a moved game. The file is gitignored because it is a machine path, and without the key the build and launch stop on an unresolved variable. `NUCLEAR_OPTION_ROOT` cannot stand in, since VS Code resolves `${env:...}` against the environment it started with, so a later variable never reaches tasks or launches.

## How it works

`DynamicMap.MapControls` runs every frame while the map is maximized, and a Harmony prefix gives the marquee first refusal on that frame. Returning false skips vanilla controls entirely, which holds off both the click-select and the left-drag pan during a drag. The pan button moves via a transpiler that swaps the pan gate, the single `Input.GetMouseButton(0)` call inside `MapControls`, for one that reads `PanButton`.

Unit picking mirrors `DynamicMap.SelectFromMap`: the nearest icon within 10000 squared pixels for clicks, and a screen-space rectangle over `UnitMapIcon.transform.position` for the marquee. Both work in raw `Input.mousePosition` pixels, since the game's canvases are `ScreenSpaceOverlay` with a null camera, which is also why the mod's rectangle draws on its own overlay canvas with a scale factor of 1. Both modes filter through `TargetListSelector.CheckExclusions`, the Target List panel filters, and an icon switched off in Map Options is left out too, since a click needs a raycast hit and the range select needs the HUD marker's image enabled.

While flying, the mod batches both directions. Adding writes the target list, each unit's HUD marker and the map icon, then calls `WeaponManager.TargetListChanged()` once, and removing applies `CombatHUD.DeSelectUnit`'s per-unit effects the same way. The game networks that list through `SetStationTargets`, so a 20 unit marquee costs one message instead of twenty. Sounds play once for the gesture, the way `CombatHUD.TargetSelect` plays once for its whole batch, each key that changes targets sounds like what it did, and an empty box drops the list in one `CombatHUD.DeselectAll` call.

## Limitations

- The marquee is off in the mission editor by default, which has its own drag box select on `UnitSelection`.
- The box is drawn over everything, including the map tooltip.
- A marquee that starts and ends on the same icon can also fire the game's own EventSystem click, which the dedup guards make harmless but is still a double-handled click.
- Panning is not a bindable game action, so `PanButton` is mod-only and the in-game controls menu will not show it.

## Vanilla fixes

Three game paths assume every unit they are handed has a map icon or a HUD marker, and they throw when it does not. Vanilla's own radar and missile warnings hit the same assumption, and the wreck feature makes a unit with an icon but no marker an everyday thing, so the mod fixes all three.

- `DynamicMap.GetOrAddIcon` queues an icon and then returns `iconLookup[unit]`, which throws `KeyNotFoundException` for a unit that gets no icon, such as a disabled one or one whose definition asks for none. It returns no icon now.
- `DynamicMap.FlagIncomingMissile` calls that and dereferences the result, so it skips a unit with no icon.
- `CombatHUD.HighlightMarker` indexes `markerLookup` directly, which a radar warning reaches. `CombatHUD.CreateMarker` refuses a marker to a unit that is disabled, is `Scenery`, or turns up with no aircraft, and a radar contact already wrecked when detected is the everyday case. The mod skips the highlight when there is no marker.
- Filter dimming: `MapIcon.UpdateColor` sets the theme colour and stops there, so anything that calls it directly, `SelectIcon`, `DeselectIcon` and `DynamicMap.UnselectAll` included, draws a filtered out unit as if it were live. The dimming lives one level down in `UnitMapIcon.UnitMapIcon_UpdateColor`, so the base method now ends in that update, through a postfix with a depth counter that stops the tint being applied twice.
- Target List rows: `DynamicMap.DeselectAllIcons` empties `selectedIcons` without firing `onUnitDeselected` or `onAllDeselected`, the only two events the panel builds its rows from, so the row outlives the selection it stands for. It is reached by `CombatHUD.DeselectAll`, by `Aircraft.OnStartClient` (every respawn) and by `AircraftSelectionMenu.OnEnable` (the spawn screen), and it hits any unit selected in commander view, not just wrecks. The mod fires the skipped event, once per clear that actually dropped something.
- Wheel in menus and in the flyby view: `Zoom View` is the wheel, and `DynamicMap.MapControls` reads it to zoom the map, as do other camera states. `CameraChaseState` and `CameraOrbitState` only do their wheel work inside `if (!Cursor.visible)`, and `CameraCockpitState` only asks whether the map is maximized, so a scroll inside a dialog moved the cockpit's field of view behind it. `CameraTVState`, the flyby camera, asks nothing, so a scroll that zoomed the map moved its field of view too. The mod masks both reads while the cursor is up and leaves the two states that already ask that alone.
- Map drag: `DynamicMap.MapControls` pans by `axis * 150 * min(Time.unscaledDeltaTime, 0.03) / zoom`, which treats a mouse axis as a rate, so the same drag moved the map further the slower the game ran and further than the cursor at every frame rate. The mod's own log measured that axis carrying 0.44 of the cursor's movement, on Rewired's update and not on the rendered frame, so a pan built from it arrives in bursts. The mod offsets the pan by the cursor's own movement each frame, scaled by `zoom / lossyScale`, so a pixel of mouse is a pixel of map, and `MapPanSpeed` scales that. `MapPanFix` off hands the game back its own axis and its own frame time.
- Cursor: the game never changes the cursor's shape, since `CursorManager` decides only whether it is visible and whether it is locked. While the pan button is held over the map, the mod reads the cursor theme's move cursor out of Windows at the size the display uses and hands it to Unity, so it matches the player's scheme. `Visual.PanCursor` turns that off.

## Status

This release is 0.2.0, played against 0.34.1. The marquee, the target keys, the wreck icons and the fixes listed above are all in use in play. The one rough edge left is the double-handled click a drag can produce when it ends on the icon it started from, which the dedup guards make harmless.

## License

GPL-3.0. See LICENSE.

This is an unofficial mod, not affiliated with the developers of Nuclear Option. It ships no game code or assets. The build references the installed `Assembly-CSharp.dll` and Unity modules with `Private=false`, so they are never copied into the output.
