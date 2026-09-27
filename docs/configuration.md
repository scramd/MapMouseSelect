# Configuration

Every setting is in the file the mod writes, `BepInEx\config\com.kerbiter.mapmouseselect.cfg`, and in the Configuration Manager window (F1) while the game runs. The three outline colours open a colour picker there, with the red, green, blue and alpha sliders, a hex field and a reset button. In the file they are `RRGGBBAA` hex, so `FF5A5ACC` is a mostly opaque red.

| Section | Key | Default | Meaning |
|---|---|---|---|
| Hotkeys | `PanButton` | `Middle` | Mouse button that drags to pan the map |
| Hotkeys | `RequireModifier` | `false` | Require `MarqueeModifier` to start a marquee |
| Hotkeys | `MarqueeModifier` | `LeftAlt` | Modifier used when `RequireModifier` is on |
| Hotkeys | `DefaultBindings` | empty, then one name per action | Actions whose default key the mod has already written. Removing a name has it written again, which is there so a key you clear stays clear. The Controls menu's Restore Defaults clears the relevant names for you |
| Behavior | `ShiftAddsToSelection` | `true` | Shift extends the selection |
| Behavior | `ControlRemovesFromSelection` | `true` | Ctrl takes the boxed units out of the selection |
| Behavior | `SuppressModifierBindings` | `true` | While the map is open, disable keyboard bindings that sit directly on `ModifierKeys` |
| Behavior | `ModifierKeys` | Shift, Control | Keys that act only as map modifiers while the map is open |
| Behavior | `BatchTargetListWhileFlying` | `true` | Allow the box select while flying (weapon targets). Off disables it in that mode |
| Behavior | `FollowFirstUnit` | `false` | In commander view, move the camera to the first boxed unit |
| Behavior | `EnableInMissionEditor` | `false` | Allow the box select in the mission editor, which has its own box select |
| Behavior | `MaxTargets` | `128` | Cap on targets one box may add. The game itself refuses more than 128 |
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
| Visual | `BoxOutlineColorRemove` | `FF5A5ACC` | Outline color for a deselecting box (Ctrl held) |
| Visual | `OverlaySortingOrder` | `32000` | Sorting order of this mod's overlay canvas |
| Destroyed | `Enabled` | `true` | Keep destroyed buildings on the map as darkened wrecks, and add the `DED` filter |
| Destroyed | `Tint` | `0.45` | How much of its usual colour a wrecked building's icon keeps |
| Target list | `FilterNeutral` | `true` | Add the `NTL` button to the Target List filter pane |

## The frame rate cap

An unfocused window keeps the whole game loop at full rate, which holds the CPU and the GPU for a session nobody is looking at. The mod caps it instead, and takes vsync off while the cap is on, since Unity ignores `targetFrameRate` while `vsyncCount` is above zero. Both values go back the way the game had them when you focus the window again.

The cap slows the world with it, because Unity spends at most `Time.maximumDeltaTime` of game time on one frame and a longer frame loses the surplus. That value logs as 0.1s in this game, so 10 frames a second is the point where the world stops keeping real time. Measured against the same build, reading the ratio of game time to real time once a second:

| Cap | World kept |
|---|---|
| 1 | 0.10 of real time |
| 5 | 0.50 |
| 10 | 0.95 to 1.05 |
| 11 | 0.98 to 1.01 |
| 12 | 0.99 to 1.01 |

The floor is worked out from `Time.maximumDeltaTime` at runtime, so it reads 11 here and follows the game if that ever changes. A value below the floor is held at it, unless `FrameLimitAllowWorldSlowdown` is on, which is for anyone who wants the smaller GPU load and does not mind a world that runs slow with it. Leave that one off while connected to someone else's game.

While hosting a game that another player is connected to, the cap releases, because a host answers its players on the frames it takes. `FrameLimitWhileHosting` overrides that and caps anyway.
