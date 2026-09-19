using System.Collections.Generic;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>What a finished drag does to the units inside the box.</summary>
    internal enum MarqueeMode
    {
        /// <summary>Select the boxed units, dropping whatever was selected before.</summary>
        Replace,

        /// <summary>Add the boxed units to the selection.</summary>
        Add,

        /// <summary>Take the boxed units out of the selection.</summary>
        Remove
    }

    /// <summary>
    /// Routes a marquee result to whichever selection system the current mode uses.
    ///
    /// Flying a live aircraft, a map click adds a weapon target (CombatHUD.SelectUnit). With no live
    /// aircraft, a map click select a unit for commanding (DynamicMap.SelectIcon) and that selection
    /// is what right-click move orders are issued to. Dropping a unit has the same split, and lives in
    /// Remove so that the click path and the marquee cannot disagree about it.
    /// </summary>
    internal static class SelectionApplier
    {
        internal static void Apply(DynamicMap map, List<Unit> units, MarqueeMode mode)
        {
            var aircraft = GetLiveAircraft();

            // The flying marquee writes to the weapon target list, and that list is the one thing the user
            // can turn the mod off from touching.
            if (aircraft != null && !Plugin.BatchTargetListWhileFlying.Value)
            {
                return;
            }

            if (units == null || units.Count == 0)
            {
                // An empty box clears the commander selection, which is the usual marquee meaning. Note
                // this is a mod choice, not vanilla: a click on empty map space does nothing at all in
                // this game, because MapControls only ever acts on an icon found under the cursor. An
                // empty box that removes or adds has nothing to do either way.
                if (aircraft == null && mode == MarqueeMode.Replace)
                {
                    map.UnselectAll();
                }
                return;
            }

            if (mode == MarqueeMode.Remove)
            {
                ApplyRemove(map, units);
                return;
            }

            // Vanilla map clicks only ever add a weapon target, so a flying marquee adds whatever the user
            // asked for; the replacing half of the mode only exists in the commander view.
            if (aircraft != null)
            {
                ApplyFlying(aircraft, units);
            }
            else
            {
                ApplyCommander(map, units, mode == MarqueeMode.Add);
            }
        }

        internal static Aircraft GetLiveAircraft()
        {
            var hud = SceneSingleton<CombatHUD>.i;
            if (hud == null)
            {
                return null;
            }

            var aircraft = hud.aircraft;
            return aircraft != null && !aircraft.disabled ? aircraft : null;
        }

        private static void ApplyFlying(Aircraft aircraft, List<Unit> units)
        {
            var hud = SceneSingleton<CombatHUD>.i;
            var weapons = aircraft.weaponManager;
            if (hud == null || weapons == null)
            {
                return;
            }

            // This is the live list, shared with CombatHUD. GetTargetList() returns the field itself.
            var targets = weapons.GetTargetList();
            if (targets == null)
            {
                return;
            }

            var limit = Mathf.Clamp(Plugin.MaxTargets.Value, 1, Unit.MAX_STATION_TARGETS);

            var added = 0;
            var alreadySelected = 0;
            var capped = 0;
            var noMarker = 0;

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];

                if (Plugin.FlyingHostilesOnly.Value
                    && DynamicMap.GetFactionMode(unit.NetworkHQ, false) != FactionMode.Enemy)
                {
                    noMarker++;
                    continue;
                }

                // Vanilla map clicks only add a unit that has a HUD marker to select on.
                if (!hud.MarkerExists(unit))
                {
                    noMarker++;
                    continue;
                }

                if (targets.Contains(unit))
                {
                    alreadySelected++;
                    continue;
                }

                if (targets.Count >= limit)
                {
                    capped++;
                    continue;
                }

                targets.Insert(0, unit);

                if (hud.TryGetMarker(unit, out var marker))
                {
                    marker.SelectMarker();
                }
                added++;
            }

            if (added > 0)
            {
                // One refresh for the whole batch. The list is networked (SetStationTargets), so this
                // keeps a 20-unit marquee to a single message instead of twenty.
                weapons.TargetListChanged();
            }

            if (capped > 0)
            {
                Plugin.Log.LogWarning(
                    $"Marquee skipped {capped} unit(s): the weapon target list is capped at {limit}. "
                    + "Raise MaxTargets (max 128) or select fewer units.");
            }

            Report("flying", added, alreadySelected, noMarker);
        }

        private static void ApplyCommander(DynamicMap map, List<Unit> units, bool additive)
        {
            if (!additive)
            {
                map.UnselectAll();
            }

            var added = 0;
            var alreadySelected = 0;

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];

                // DynamicMap.SelectIcon logs an error for a unit that is already selected, so filter.
                if (IsSelected(map, unit))
                {
                    alreadySelected++;
                    continue;
                }

                map.SelectIcon(unit);
                added++;
            }

            if (Plugin.FollowFirstUnit.Value && added > 0)
            {
                var camera = SceneSingleton<CameraStateManager>.i;
                if (camera != null)
                {
                    // Follow exactly one. Following every boxed unit would just leave the last one.
                    camera.SetFollowingUnit(units[0]);
                }
            }

            Report("commander", added, alreadySelected, 0);
        }

        /// <summary>Takes every boxed unit that is selected out of the selection.</summary>
        private static void ApplyRemove(DynamicMap map, List<Unit> units)
        {
            var removed = 0;

            for (var i = 0; i < units.Count; i++)
            {
                if (Remove(map, units[i]))
                {
                    removed++;
                }
            }

            Plugin.Verbose(
                "[remove] marquee dropped " + removed + " of " + units.Count + " boxed unit(s).");
        }

        /// <summary>
        /// Takes one unit out of the selection by whichever route owns it, and says whether it was in there
        /// at all. A weapon target has to leave the weapon target list too, or the weapon stays locked to a
        /// unit the player just dropped; the commander's selection only lives on the map.
        /// </summary>
        internal static bool Remove(DynamicMap map, Unit unit)
        {
            if (map == null || unit == null)
            {
                return false;
            }

            var aircraft = GetLiveAircraft();
            var weapons = aircraft != null ? aircraft.weaponManager : null;
            var targets = weapons != null ? weapons.GetTargetList() : null;

            if (targets != null && targets.Contains(unit))
            {
                var hud = SceneSingleton<CombatHUD>.i;
                if (hud == null)
                {
                    return false;
                }

                hud.DeSelectUnit(unit);
                return true;
            }

            if (!IsSelected(map, unit))
            {
                return false;
            }

            map.DeselectIcon(unit);
            return true;
        }

        private static bool IsSelected(DynamicMap map, Unit unit)
        {
            var selected = map.selectedIcons;
            if (selected == null)
            {
                return false;
            }

            for (var i = 0; i < selected.Count; i++)
            {
                if (selected[i] is UnitMapIcon icon && icon.unit == unit)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Report(string mode, int added, int alreadySelected, int skipped)
        {
            Plugin.Verbose(
                "[" + mode + "] marquee added " + added
                + ", already selected " + alreadySelected
                + ", skipped " + skipped + ".");
        }
    }
}
