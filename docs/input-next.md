# Prepare the next input step

The current code combines two input systems: one BepInEx KeyboardShortcut and raw Unity mouse/key reads. It also disables some native Rewired mappings while the map is open. No bindings-library migration or new mouse/controller interaction is implemented here. Read the `NuclearOption.Bindings` README (github.com/clankagent/nuclear-option-bindings) before doing that work. Its source-only `NuclearOption.Bindings` package compiles into a mod; players do not install another plugin DLL.

## Input reads and ownership today

| Location | Reads or changes |
| --- | --- |
| `Marquee.Tick`, `ScreenRect`, `ControlHeld`, `CurrentMode` | Unity left down/held/release-by-not-held, mouse position, Ctrl; `KeyboardShortcut.IsPressed` for optional LeftAlt; Shift via VanillaCalls. |
| `VanillaCalls.ShiftHeld` | Both Unity Shift keys; used by marquee and click routing. |
| `MapIconPointerClickPatch` | EventSystem left button and press position, Unity current pointer, native drag threshold. |
| `UnitMapIconClickPatch` | Mouse versus Controller ClickSource, Shift and cursor-in-map. It does not implement Ctrl-click removal. |
| `PanButtonCompat.GetMouseButton` | Configured Unity Left/Right/Middle held button; replaces the stock pan gate. |
| `VanillaFixes.PanAxis`, `CursorStep` | Stock Rewired Pan View/Tilt View axis values, raw mouse position, frame time, drag threshold. |
| `CameraJump.Tick` | Pan button down and up, pointer and drag threshold. A press and release without a drag jumps the camera; checks map rectangle and aircraft. Active only while a Jump Map mapping on the pan button is held off. |
| `CameraJump.HoldJumpButtonOff` | Looks up native Jump Map; disables mouse mappings whose element name matches the pan button. Restores held-off mappings when map closes. |
| `ModifierGuard`, `BindingBlocker` | Parses configured Unity modifier keys; reads the marquee shortcut's MainKey; disables bare-key Rewired elements on enabled keyboard maps while expanded. It does not disable declared modifier combinations. |
| NTL cloned `TargetListSelector_ToggleButton` | Native pointer callbacks handle left toggle and right-click SetOnlyItem. Theme/layout and right-click semantics are coupled to the stock control. |

Panning also leaves native `Move Map Horizontal/Vertical`, `Zoom View`, Select, Jump Map and right-click waypoint branches in `MapControls`. All are skipped during marquee ownership. Those are native reads, not new mod actions, but their arbitration must be included in a redesign. CombatHUD.LateUpdate also reads a timed Select release for HUD TargetSelect even with the map expanded; only its held paint branch checks the map state.

## What migration would change

Use `InputBinding.Bind` for semantic actions. Map actions need `allowFullMap: true`; the library otherwise blocks them with the expanded map open. It supplies keyboard/mouse/gamepad/HOTAS rows, F1 capture, context blocking and neutral rearming. Existing MarqueeModifier can use the documented KeyboardShortcut migration, with an explicit saved-setting policy.

The fork no longer creates its own Rewired actions. The former target keys and their Controls menu entries are removed; configuration files may still hold an unused `DefaultBindings` value from them.

The public library exposes `IsDown` and `IsHeld`, not `IsUp`. Tap/hold actions need previous-held state and a cancelled-context policy, or the documented per-row interface. Do not read a held binding intermittently: release-first rearming depends on frame reads. The library advises reads from the mod's own Update/LateUpdate rather than another persistent host; reconcile this with the inherited host lifecycle using runtime evidence. NuclearMinimap uses plugin Update/LateUpdate, so do not assume every plugin host is destroyed; inspect loader settings before choosing a host.

The library rejects extra Shift/Ctrl/Alt on ordinary key rows. The library is additive. It does not reserve native controls, supply pointer positions, move a virtual cursor or make a controller marquee. Keep gesture geometry separate from action bindings. Modifier suppression and Jump Map suppression need their own reviewed ownership policy. The F1 capture UI reserves left/right mouse clicks for itself, so generic capture does not solve rebinding the left-button drag.

## Open design questions

- Does a controller use a virtual cursor with two corners, a growing rectangle, a radius, or a unit list? What provides coordinates, drag threshold and feedback?
- How do replace/add/remove work without simultaneously holding multiple buttons? Which action cancels a gesture?
- Should zoom, stick pan and waypoints remain available during a marquee? Which controls are consumed, and how do tap/hold and menu transitions restore them?
- Should mouse panning, camera jump and NTL remain in this plugin?
- How should focus loss, chat, F1, pause, loading, device disconnect and aircraft/scene changes cancel and rearm gestures?
- How are old shortcuts migrated without losing intentional unbound settings?
- How should selected-unit clicks, controller Select, neutral exclusion and already selected targets follow one consistent rule?
