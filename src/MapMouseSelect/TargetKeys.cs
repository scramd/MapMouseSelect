using System.Collections.Generic;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Key handling for the current target selection.
    ///
    /// The keys are real Rewired actions registered by ModActions, so they are read with the same calls
    /// vanilla's own target handling uses and are rebindable in the Controls menu. Tick runs from the
    /// mod's own host object (ModHost), which is a GameObject this mod owns and marks DontDestroyOnLoad
    /// because the plugin's own object does not outlive this game's boot.
    ///
    /// Both map modes are covered because they keep the selection in different places: a live aircraft
    /// uses CombatHUD's weapon target list, anything else uses DynamicMap's selected map icons.
    /// </summary>
    internal static class TargetKeys
    {
        private static bool _longFired;

        internal static void Tick()
        {
            if (!ModActions.Registered)
            {
                return;
            }

            if (GameManager.gameState == GameState.Editor && !Plugin.EnableInMissionEditor.Value)
            {
                return;
            }

            var player = GameManager.playerInput;
            if (player == null)
            {
                return;
            }

            if (player.GetButtonDown(ModActions.SelectOnlyName))
            {
                SelectOnly();
            }

            // The same thresholds ExtraUiInput uses for Cancel: the hold fires as soon as pressDelay has
            // elapsed, and a release within clickDelay is a tap.
            if (player.GetButtonTimedPressDown(ModActions.DeselectOldestName, PlayerSettings.pressDelay))
            {
                _longFired = true;
                KeepOnlyNewest();
            }

            if (player.GetButtonUp(ModActions.DeselectOldestName))
            {
                // Vanilla fires both branches for a hold of between clickDelay and pressDelay, because
                // clearing everything and then dropping the newest of nothing happens to compose. These
                // two do not compose, so the tap is skipped once the hold has already run.
                if (!_longFired)
                {
                    DropOldest();
                }

                _longFired = false;
            }
        }

        /// <summary>
        /// Drops the selection made first. Which end of the list that is depends on the system: the
        /// weapon target list is built with Insert(0, ...) so the oldest sits last, while map icons are
        /// appended, so the oldest sits first. After a network sync the target list order is whatever
        /// the server sent, which is the same order the local list was built in.
        /// </summary>
        private static void DropOldest()
        {
            var hud = SceneSingleton<CombatHUD>.i;
            var aircraft = hud != null ? SelectionApplier.GetLiveAircraft() : null;

            if (aircraft != null)
            {
                var weapons = aircraft.weaponManager;
                var targets = weapons != null ? weapons.GetTargetList() : null;
                if (targets == null || targets.Count == 0)
                {
                    return;
                }

                var oldest = targets[targets.Count - 1];
                if (oldest != null)
                {
                    Plugin.Verbose("Dropping oldest target " + oldest.name + ".");
                    hud.DeSelectUnit(oldest);
                }
                return;
            }

            var map = SceneSingleton<DynamicMap>.i;
            var selected = map != null ? map.selectedIcons : null;
            if (selected == null || selected.Count == 0)
            {
                return;
            }

            Plugin.Verbose("Dropping oldest of " + selected.Count + " selected map icons.");
            DropIcon(map, selected[0]);
        }

        /// <summary>
        /// Drops everything except the most recent selection, which is what Cancel hold does for the
        /// whole list and what this key does for all but one.
        /// </summary>
        private static void KeepOnlyNewest()
        {
            var hud = SceneSingleton<CombatHUD>.i;
            var aircraft = hud != null ? SelectionApplier.GetLiveAircraft() : null;

            if (aircraft != null)
            {
                var weapons = aircraft.weaponManager;
                var targets = weapons != null ? weapons.GetTargetList() : null;
                if (targets == null || targets.Count <= 1)
                {
                    return;
                }

                // Read it before clearing: DeselectAll empties the very list this came from.
                var newest = targets[0];
                Plugin.Verbose("Keeping only " + (newest != null ? newest.name : "<null>") + ".");
                hud.DeselectAll(false);

                if (newest != null)
                {
                    // SelectUnit re-adds it and AddTargetList does the network refresh.
                    hud.SelectUnit(newest);
                }
                return;
            }

            var map = SceneSingleton<DynamicMap>.i;
            var selected = map != null ? map.selectedIcons : null;
            if (selected == null || selected.Count <= 1)
            {
                return;
            }

            var keep = selected[selected.Count - 1];
            if (keep is UnitMapIcon unitIcon && unitIcon.unit != null)
            {
                map.UnselectAll();
                map.SelectIcon(unitIcon.unit);
                return;
            }

            // Airbase icons have no SelectIcon(Unit) path, so drop the rest one at a time instead.
            for (var i = selected.Count - 2; i >= 0; i--)
            {
                DropIcon(map, selected[i]);
            }
        }

        private static void DropIcon(DynamicMap map, MapIcon icon)
        {
            if (icon == null)
            {
                return;
            }

            if (icon is UnitMapIcon unitIcon && unitIcon.unit != null)
            {
                map.DeselectIcon(unitIcon.unit);
                return;
            }

            icon.DeselectIcon();
            map.selectedIcons.Remove(icon);
        }

        /// <summary>
        /// Vanilla's Select, then a cut back to whatever it picked up.
        ///
        /// Select is exactly what the Select key and the gamepad button run, so it is called rather than
        /// reproduced, and it needs no clear first: it replaces the map selection on its own when nothing
        /// is flying, and adds to the weapon target list while flying. The adding is where the second half
        /// comes in, since the point of this key is to end up with the one unit being pointed at. So when
        /// the call did take something, the list is cut back to it, the same way the Delete hold does. When
        /// it took nothing, the list is left alone: pointing at empty sky should not throw away targets.
        /// </summary>
        private static void SelectOnly()
        {
            var map = SceneSingleton<DynamicMap>.i;
            if (map == null)
            {
                return;
            }

            var hud = SceneSingleton<CombatHUD>.i;
            var targets = TargetList(hud);

            if (DynamicMap.mapMaximized)
            {
                var before = targets != null ? targets.Count : 0;
                VanillaCalls.SelectFromMap(map);
                KeepOnlyPick(targets, before);
                return;
            }

            if (targets == null)
            {
                return;
            }

            // Map closed: the only thing on screen to select is whatever the designator is pointing at.
            var designatorBefore = targets.Count;
            VanillaCalls.TargetSelect(hud, false);
            KeepOnlyPick(targets, designatorBefore);
        }

        /// <summary>
        /// The weapon target list, or null when nothing is flying. CombatHUD's own list is this same
        /// instance, so what the select call just added is visible immediately.
        /// </summary>
        private static List<Unit> TargetList(CombatHUD hud)
        {
            var aircraft = hud != null ? SelectionApplier.GetLiveAircraft() : null;
            var weapons = aircraft != null ? aircraft.weaponManager : null;
            return weapons != null ? weapons.GetTargetList() : null;
        }

        /// <summary>
        /// Cuts the target list back to the newest entry when the select call added one.
        /// </summary>
        private static void KeepOnlyPick(List<Unit> targets, int before)
        {
            if (targets != null && targets.Count > before)
            {
                KeepOnlyNewest();
            }
        }
    }
}
