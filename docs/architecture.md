# How selection reaches the game

This describes the remaining source against a read-only inspection of the 0.34.6 client source and assemblies. No game session was run. Names below identify game members; no proprietary implementation is included.

## One marquee drag

1. A prefix on `DynamicMap.Update` ensures `ModHost` exists and calls `Marquee.Tick` before native map controls. The host is created as a persistent hidden Unity object. Upstream reports that its original plugin host is destroyed during loading; that lifecycle claim has not been reproduced here. NuclearMinimap does per-frame work on its own plugin component, which is static counter-evidence to a universal destruction rule. Check actual loader settings and lifecycle before keeping this host design; a BepInEx-setting explanation remains unverified.
2. With the map expanded, editor use allowed, and the optional marquee modifier held, a left press inside the map sets a pending gesture. It captures the screen position. `OwnsMouse` is true from this point.
3. The `MapControls` prefix returns false while `OwnsMouse` is true, even during the pending part of a simple click. It skips every stock branch in that method, not just left pan. This includes map follow, image placement, edge clamp and view-indicator placement. `DynamicMap.Update` itself continues, so other Update postfixes still run.
4. Each frame, `UnitSelection.DragThresholdPassed` compares the pointer to the press. Crossing it starts dragging. The mod draws a non-raycasting outline on its own screen overlay canvas. Shift chooses add; Ctrl chooses remove and wins. The mode is sampled during the drag, including release.
5. On release, the screen rectangle is compared with `UnitMapIcon.transform.position` in `map.mapIcons`. The collector ignores inactive icons, your own aircraft, and units excluded by `TargetListSelector.CheckExclusions`. It does not box airbase icons. Root-transform screen coordinates are an inherited assumption about the UI prefab; static source inspection alone cannot validate every prefab/layout.
6. `SelectionApplier` treats a non-disabled HUD aircraft as flying. In flight it rejects new entries without HUD markers and applies `MaxTargets` to additions. It writes the current weapon target list directly, updates markers/map selection, then calls `TargetListChanged` per changed batch. A replace may have one removal batch and one addition batch. It does not guarantee one network update for the entire gesture.
7. Without a live aircraft it uses `DynamicMap.UnselectAll`, `SelectIcon` and `DeselectIcon`. Additive boxes preserve the previous selection. The editor opt-in does not populate the editor's separate multi-selection store.
8. Empty replace uses native deselect-all; other empty modes leave selection alone. Cancellation hides the outline and clears the state. Closing the expanded map and the editor gate cancel it. There is no complete focus-loss, menu or aircraft-change cancellation system. Cancellation itself needs DynamicMap.Update. If the map disables or unloads mid-drag, static state and the persistent outline can remain until another map updates.

`CombatHUD.TargetSelect` and `WeaponManager.TargetListChanged` supply the source model for batching. The latter sends station targets only under its network/weapon-station conditions and limits the transmitted count to 128. Do not equate direct list writes with a general public bulk-selection API.

## One ordinary click

The left press may arm a pending marquee. If it releases before crossing the threshold, the state cancels. Unity's EventSystem still delivers icon clicks through `MapIcon`'s explicit pointer handler. Its prefix rejects a left click only when the release is beyond the drag threshold from the press. It cannot detect a drag that returns close to the start.

The handler calls `UnitMapIcon.ClickIcon`. Its prefix rejects an excluded new selection before stock code can clear the old selection or move the commander camera. The click path also rejects an unmarkable new flight target before replacement can discard existing targets. An already selected unit remains removable.

With Shift held, the prefix removes the unit from whichever selection stores contain it and skips the stock method when removal succeeded. Without Shift, a mouse-source click prunes the flight target list to the clicked unit, then allows stock selection. Controller-source selection does not get that plain-mouse replacement. Other filtering and removal branches can still affect it. Ctrl removal is a box action, not a click action.

A postfix on `MapIcon.SelectIcon` restores raycasting for unit icons. It does not change airbase raycasting. Two small repairs cover empty-list deselection and the missing clear-all event; see the [patch inventory](coupling.md).

## Supporting systems

`ModHost.Update` suppresses bare modifier bindings, handles double-click camera jumps and draws the pan cursor. Native map pan remains in `MapControls`, changed by two transpilers. `TargetFilters` adds NTL outside stock filter lists and extends `CheckExclusions`.

`Plugin.ApplyPatches` processes patch classes separately and logs failures. This lets unrelated classes apply after a failure; it can also leave a partly active mod. `ReportPatchState` reports patch totals, including other owners' patches, and transpiler match counts. Counts alone do not prove that a hook executes correctly.

See [scope](scope.md), [coupling](coupling.md) and [input preparation](input-next.md).

## Target-key exceptions

