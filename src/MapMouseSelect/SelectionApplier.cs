using System.Collections.Generic;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Routes a marquee result to whichever selection system the current mode uses.
    ///
    /// Flying a live aircraft, a map click adds a weapon target (CombatHUD.SelectUnit). With no live
    /// aircraft, a map click select a unit for commanding (DynamicMap.SelectIcon) and that selection
    /// is what right-click move orders are issued to.
    /// </summary>
    internal static class SelectionApplier
    {
        private const int GameMaxTargets = 128;

        internal static void Apply(DynamicMap map, List<Unit> units, bool additive)
        {
            var aircraft = GetLiveAircraft();

            if (units == null || units.Count == 0)
            {
                // Vanilla: clicking empty map space clears the commander selection, and does nothing
                // at all while flying.
                if (aircraft == null && !additive)
                {
                    map.UnselectAll();
                }
                return;
            }

            if (aircraft != null)
            {
                ApplyFlying(aircraft, units);
            }
            else
            {
                ApplyCommander(map, units, additive);
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
            if (!Plugin.BatchTargetListWhileFlying.Value)
            {
                return;
            }

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

            var limit = Mathf.Clamp(Plugin.MaxTargets.Value, 1, GameMaxTargets);

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
