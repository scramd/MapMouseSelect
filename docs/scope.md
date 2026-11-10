# What this fork keeps, and why

The mod has one feature: selecting units on the map with a box. Everything else is in the mod for one of three reasons. It is part of selecting, it replaces a stock control that the box displaced, or it repairs a stock fault that selection runs into.

## The feature

| Part | What it does |
| --- | --- |
| Box selection | A left drag selects the units inside. Plain replaces, Shift adds, Ctrl removes. An empty plain box clears. |
| Applying the result | With an aircraft, the units go into the current weapon's target list in one update, up to the game's limit of 128. Without an aircraft, the units become the map selection, which the stock game uses for move orders to friendly vehicles and ships. |
| Click behaviour | A plain click selects only that unit. Shift + click adds, or removes a unit that is already selected. |
| Neutral filter (NTL) | A Target List button that keeps neutral units out of a selection. The stock filters cover only friendly and enemy units. |

## Replacements for displaced stock controls

| Part | Why it is needed |
| --- | --- |
| Map drag on the middle button | The stock game moves the map with a left drag. The box now uses the left drag. |
| Camera jump on a middle click | The stock Jump Map action fires when the middle button goes down. With the map drag on that button, every drag would begin with a jump. The mod holds that one binding off while the map is open and jumps on a press and release without a drag. |
| Bare Shift and Ctrl bindings held off | Shift and Ctrl choose the kind of box. A stock binding on one of those keys alone would fire at the same time. |

## Repairs to the stock game

| Part | Why it is needed |
| --- | --- |
| Selected icons stay clickable | The stock game stops a selected icon from receiving the mouse, so Shift + click could not remove it. |
| Click guards | A click that the filters reject, or that has no HUD marker, no longer clears the existing targets first. |
| No click at the end of a drag | A box that ends on an icon does not also click it. |
| Target List rows follow the selection | Two stock paths clear the selection without telling the Target List. Its rows stayed. |
| Map drag follows the mouse | The stock game scales a mouse drag by the frame time, so the map moves further at low frame rates. Moving the map is part of every selection now that it shares the mouse with the box. |
| Camera wheel | The wheel zooms the map. The stock cockpit and flyby cameras also read it, so the camera zoomed behind the map, behind menus and behind the ConfigurationManager window. |

## Removed from the original

| Part | Reason |
| --- | --- |
| Frame rate limit while the game is not focused | Not about selection. |
| Safe exit key for a parked aircraft | Not about selection. |
| Icons for destroyed buildings, the DED filter button and the crash guards they needed | Extra map content, not selection. |
| Three target keys in the game's Controls menu | Keyboard actions on the target list, also with the map closed. Not mouse selection. They also needed the mod to write into the game's private input tables. |
| Camera follows the first boxed unit | An optional camera reaction, off by default. |
| "Stick" scrolling with the mouse | A second way to move the map, off by default. |
| Move cursor while dragging the map | Cosmetic, and built on Windows-only calls. |
| Double left click for the camera jump | Replaced by the middle click. |
| Colour rerouting for map icons | The game does this itself since 0.34.6. The old patch no longer builds against the current game. |

Removals are scope cuts. They do not say that the original features were poor.

## Unchanged for now

The plugin ID `com.kerbiter.mapmouseselect`, the name, the version number 0.3.0 and `meta.json` are still the original's. They change before the first release of this fork.
