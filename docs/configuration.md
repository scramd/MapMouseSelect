# Configuration reference

Settings live in `BepInEx/config/com.kerbiter.mapmouseselect.cfg`. Optional BepInEx ConfigurationManager exposes them in F1. Colours are RGBA values.

| Section | Key | Default | Effect and limit |
| --- | --- | --- | --- |
| Hotkeys | `PanButton` | `Middle` | `Left`, `Right` or `Middle`. Left conflicts with unmodified marquee ownership; right also triggers stock waypoints. Middle is the intended separation. |
| Hotkeys | `RequireModifier` | `false` | Require the shortcut at the start of a box. Releasing it does not cancel an active box. KeyboardShortcut rejects extra held non-mouse keys, so default Alt does not arm with Shift/Ctrl or flight keys already held; choose a matching combination or press mode keys after starting. |
| Hotkeys | `MarqueeModifier` | `LeftAlt` | BepInEx KeyboardShortcut. Used only with RequireModifier. Keyboard only. |
| Behavior | `ShiftAddsToSelection` | `true` | Shift chooses an additive box. Does not disable native Shift-click behaviour. |
| Behavior | `ControlRemovesFromSelection` | `true` | Ctrl chooses a removing box and takes precedence over Shift. |
| Behavior | `SuppressModifierBindings` | `true` | Temporarily disables enabled bare-key mappings on ModifierKeys while the expanded map is open. It is not a general input reservation system. |
| Behavior | `ModifierKeys` | `LeftShift, RightShift, LeftControl, RightControl` | Comma-separated Unity KeyCode names, parsed case-insensitively. Unknown names log a warning. The marquee shortcut's MainKey joins when RequireModifier is on; extra shortcut modifiers do not automatically join. |
| Behavior | `BatchTargetListWhileFlying` | `true` | Allows a box to change flying targets. False suppresses its application, not the drag state or its input blocking. Click changes remain active. |
| Behavior | `EnableInMissionEditor` | `false` | Allows the marquee in the editor. Does not coordinate the editor's own box or multi-selection store. Other click, pan and suppression patches still operate with this false. |
| Behavior | `MaxTargets` | `128` | Range 1 to 128. Caps flying box additions against retained targets. Does not prune already retained targets to a newly lowered cap. Not used for commander boxes. |
| Behavior | `MapPanFix` | `true` | Uses raw pointer displacement and scale correction instead of native mouse axes and frame-time factor. The transpiler remains installed when false and returns native values. |
| Behavior | `MapPanSpeed` | `1` | Range 0.1 to 5. Multiplier while MapPanFix is on; 1 aims for one screen pixel per pointer pixel. |
| Behavior | `CameraWheelFix` | `true` | Stops the mouse wheel zooming the cockpit and flyby cameras while the map is expanded, while the cursor is visible, or while the ConfigurationManager window is open. The patch stays installed when false and passes the wheel through. |
| Visual | `ShowSelectionBox` | `true` | Draws the outline; false still selects. |
| Visual | `BoxOutlineColor` | `3CB4FFCC` | Replace outline. |
| Visual | `BoxOutlineColorAdditive` | `7CE38BCC` | Add outline. |
| Visual | `BoxOutlineColorRemove` | `FF5A5ACC` | Remove outline. |
| Visual | `OverlaySortingOrder` | `32000` | Order for the mod's screen overlay canvas, read only when created. Restart to change an existing overlay. |
| Target list | `FilterNeutral` | `true` | Creates NTL when the Target List starts. Restart after changing this setting; it is not a live enable/disable gate for an existing button. |
| Diagnostics | `VerboseLogging` | `true` | Selection, input and navigation details. Startup patch errors still log when false. |

NTL starts lit, meaning neutrals pass through unless another filter excludes them. Left click toggles it. Reset restores it lit. As a cloned native button outside the stock lists, its right click turns it on but does not isolate neutrals. Exclusion adds dimming and blocks selection; it does not hide the icon GameObject. Layout retains the inherited half-width button.


Old configuration files can retain orphaned entries for removed features. The plugin no longer binds or reads them. No migration deletes those saved values or native saved input maps.
