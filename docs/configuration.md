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
| Visual | `BoxOutlineColor` | `3CB4FFCC` | Outline color for a replacing selection |
| Visual | `BoxOutlineColorAdditive` | `7CE38BCC` | Outline color for an additive selection (Shift held) |
| Visual | `BoxOutlineColorRemove` | `FF5A5ACC` | Outline color for a deselecting box (Ctrl held) |
| Visual | `OverlaySortingOrder` | `32000` | Sorting order of this mod's overlay canvas |
| Target list | `FilterNeutral` | `true` | Add the `NTL` button to the Target List filter pane |
