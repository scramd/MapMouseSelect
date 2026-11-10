# Game-update checks

A build verifies direct API references. Static member/IL checks verify named targets and patterns. Neither verifies Harmony application, event order, prefabs, device input or gameplay. This fork has no in-game verification on 0.34.6.

## Inspect and build

1. Supply client Managed assemblies and BepInEx 5 references from the game to inspect. Keep assemblies, decompiled source and assets outside the repository.
2. Build with the README command and `-p:DeployToPlugins=false`. Do not use the inherited launch task for inspection.
3. Run the read-only metadata/IL check:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Check-GameApi.ps1 -ManagedDir "D:/references/managed" -BepInExCore "D:/references/bepinex-core"
```

The process-local execution-policy switch does not change machine policy. The script reads assemblies with Cecil. It checks remaining patch signatures, reflected names, and the unmodified MapControls IL patterns. It does not load the game or run Harmony. Its hard-coded inventory must be kept in step with the patches. A parameter name that a patch omits does not itself affect Harmony binding, but the script still reports signature drift for review.

4. Review every row of [coupling](coupling.md). Recheck named argument binding (`eventData`, `clickSource`, `unit`, `u`), return types and overloads. Check the virtual UpdateColor path before ever restoring the old rerouting patch.
5. Inspect actual IL: one GetMouseButton(int) pan gate with index 0; adjacent Pan View/Tilt View and Player.GetAxis(string); one 150f, unscaledDeltaTime, 0.03f, Mathf.Min(float,float) factor. Check branches/exception blocks around inserted instructions, not only constants. The retained transpilers warn on mismatch but can return partly changed IL.
6. Review native selection semantics: ClickIcon, SelectIcon/DeselectIcon, DeselectAllIcons/UnselectAll, CombatHUD.SelectUnit/DeSelectUnit/DeselectAll/TargetSelect, HUDUnitMarker selection, WeaponManager.GetTargetList/TargetListChanged and station limits. Check whether native fixes make mod repairs redundant.
7. Review Rewired data layout and runtime action loading, ControlMapper default restoration, native action strings, NTL clone fields/callbacks and UI geometry. Public signatures can remain stable while their semantics change.
8. Compare the four companion mods again. Their checked paths are listed in the coupling study. Check any newly installed mod that touches map controls, selection, input maps or the cursor too.

## Runtime acceptance still needed

Capture startup patch errors and mismatch warnings. Exercise each control with no aircraft, a live aircraft, and a destroyed own aircraft still referenced by the HUD. Check plain, additive and removing boxes, empty boxes, selected and excluded clicks, missing markers, target cap, airbase clicks, Target List rows, respawn and spawn screen.

Try drags returning to the starting icon; pan/zoom/waypoints during presses; Shift/Ctrl native bindings; editor opt-in; camera jump; NTL left/right click and Reset. Exercise focus loss, menus, F1, chat, scenes, settings changes and disconnects. Inspect saved controls after closing/reopening the game: temporary disabled mappings have no save/copy guard in this mod. Compare all four companion mods together, especially Move Map during a pending box. Check different canvas scales and the selection outline's placement.

A passing build, a matching instruction count or a successful patch log is not evidence that these interactions work in play. Keep that limit in any distribution description.
