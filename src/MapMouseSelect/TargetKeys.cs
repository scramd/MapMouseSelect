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
        /// Vanilla's Select, on an emptied list while flying, so that what it picks is all that is left.
        ///
        /// Select is exactly what the Select key and the gamepad button run, so it is called rather than
        /// reproduced. While flying it only ever adds, and it skips a unit that is already in the list, so
        /// pointing at a unit that is already a target would do nothing at all, and the rest of the list
        /// would stay. Emptying the list first is what makes the pick land in that case. If the pick comes
        /// up empty the previous list is put back, so pointing at empty sky is not a way to lose targets.
        ///
        /// Nothing is flying in commander view, where Select replaces the map selection by itself, so there
        /// is nothing to do there but call it.
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

            if (targets == null)
            {
                if (DynamicMap.mapMaximized)
                {
                    VanillaCalls.SelectFromMap(map);
                }

                return;
            }

            var previous = new List<Unit>(targets);
            hud.DeselectAll(false);

            if (DynamicMap.mapMaximized)
            {
                VanillaCalls.SelectFromMap(map);
            }
            else
            {
                // Map closed: the only thing on screen to select is whatever the designator is pointing at.
                VanillaCalls.TargetSelect(hud, false);
            }

            if (targets.Count == 0)
            {
                PutBack(hud, previous);
            }
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
        /// Puts back the targets that were dropped to make room for a pick that never came. SelectUnit
        /// restores the whole thing for a unit, marker and map icon alike, since the marker's own
        /// SelectMarker is what selects the icon. AddTargetList inserts at the front, so the list is
        /// rebuilt back to front to leave the order as it was.
        /// </summary>
        private static void PutBack(CombatHUD hud, List<Unit> previous)
        {
            for (var i = previous.Count - 1; i >= 0; i--)
            {
                if (previous[i] != null)
                {
                    hud.SelectUnit(previous[i]);
                }
            }

            Plugin.Verbose("Nothing under the cursor; put back " + previous.Count + " target(s).");
        }
    }
}
