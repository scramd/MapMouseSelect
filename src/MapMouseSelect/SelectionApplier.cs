using System.Collections.Generic;
using UnityEngine;

namespace MapMouseSelect
{
    internal enum MarqueeMode
    {
        Replace,

        Add,

        Remove
    }

    /// <summary>
    /// Routes a selection change to whichever system owns it, and owns the weapon target list writes itself.
    /// Flying, the selection is the current weapon station's networked target list, where every change is an
    /// unthrottled server call: CombatHUD.SelectUnit and DeSelectUnit handle one unit each and refresh every time, so
    /// a box over thirty units is thirty messages. Vanilla's own TargetSelect batches instead - inserts, one
    /// TargetListChanged, one sound - and the writers below copy that shape. The clear stays with
    /// CombatHUD.DeselectAll, which refreshes once and puts the HUD's target arrow and text away.
    /// </summary>
    internal static class SelectionApplier
    {
        /// <summary>Scratch for a batched write. Every path using it runs one at a time and none nests.</summary>
        private static readonly List<Unit> Pending = new List<Unit>();

        private static readonly List<Unit> Others = new List<Unit>();

        internal static void Apply(DynamicMap map, List<Unit> units, MarqueeMode mode)
        {
            var aircraft = GetLiveAircraft();

            // The weapon target list is the one thing the user can stop the mod from writing to.
            if (aircraft != null && !Plugin.BatchTargetListWhileFlying.Value)
            {
                return;
            }

            if (units == null || units.Count == 0)
            {
                // A box is a targeting gesture, so releasing it over nothing drops what is selected. Vanilla does
                // not clear on a click in empty space, so this is a mod choice.
                if (mode == MarqueeMode.Replace)
                {
                    if (aircraft != null)
                    {
                        // DeselectAll's argument is its withAudio flag. This asks for vanilla's deselect-all clip,
                        // the one its Cancel-hold plays.
                        var hud = SceneSingleton<CombatHUD>.i;
                        if (hud != null)
                        {
                            hud.DeselectAll(true);
                        }
                    }
                    else
                    {
                        // Vanilla has no clip for clearing the map selection. UnselectAll is silent.
                        map.UnselectAll();
                    }
                }
                return;
            }

            if (mode == MarqueeMode.Remove)
            {
                ApplyRemove(map, aircraft, units);
                return;
            }

            if (aircraft != null)
            {
                ApplyFlying(aircraft, units, mode == MarqueeMode.Replace);
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

        /// <summary>
        /// Adds units to the current weapon station's target list, then refreshes it and plays the select sound once
        /// for the whole set. AddTargetList inserts at the front, so the last unit added is the newest target, and the
        /// marker's SelectMarker is what selects the map icon too. The caller decides the sound, because it belongs to
        /// the gesture rather than the write.
        /// </summary>
        internal static void AddToTargetList(Aircraft aircraft, List<Unit> units, bool playSound = true)
        {
            var hud = SceneSingleton<CombatHUD>.i;
            var targets = TargetList(aircraft);
            if (hud == null || targets == null || units == null || units.Count == 0)
            {
                return;
            }

            var added = 0;
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || targets.Contains(unit))
                {
                    continue;
                }

                targets.Insert(0, unit);

                if (hud.TryGetMarker(unit, out var marker))
                {
                    marker.SelectMarker();
                }

                added++;
            }

            if (added == 0)
            {
                return;
            }

            aircraft.weaponManager.TargetListChanged();
            if (playSound)
            {
                VanillaCalls.PlaySelectSound(hud);
            }

            Plugin.Verbose("[targets] added " + added + " unit(s) with one refresh.");
        }

        /// <summary>
        /// Takes units out of the target list and refreshes it once, the mirror of AddToTargetList. This reproduces
        /// CombatHUD.DeSelectUnit: the marker's DeselectMarker, the map icon and the list removal. DeselectMarker
        /// already ends in DynamicMap.DeselectIcon, so the direct call is here for a unit with no marker at all,
        /// whose icon would otherwise stay selected on the map.
        /// </summary>
        internal static void RemoveFromTargetList(Aircraft aircraft, List<Unit> units, bool playSound = true)
        {
            var hud = SceneSingleton<CombatHUD>.i;
            var targets = TargetList(aircraft);
            if (hud == null || targets == null || units == null || units.Count == 0)
            {
                return;
            }

            var map = SceneSingleton<DynamicMap>.i;

            var removed = 0;
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || !targets.Contains(unit))
                {
                    continue;
                }

                if (hud.TryGetMarker(unit, out var marker))
                {
                    marker.DeselectMarker();
                }

                if (map != null)
                {
                    map.DeselectIcon(unit);
                }

                targets.Remove(unit);
                removed++;
            }

            if (removed == 0)
            {
                return;
            }

            aircraft.weaponManager.TargetListChanged();
            if (playSound)
            {
                VanillaCalls.PlayDeselectSound(hud);
            }

            Plugin.Verbose("[targets] removed " + removed + " unit(s) with one refresh.");
        }

        /// <summary>
        /// Takes one unit out of the selection by whichever system holds it, and says whether it was in one. The
        /// weapon target list and the map's own selection are separate states, and while flying they can hold
        /// different things: a wreck never reaches the weapon list, because that needs a HUD marker, and a selection
        /// made in commander view survives the move into a cockpit. Vanilla routes by mode instead, so its flying
        /// deselect branch can leave a selected icon behind - it only reaches the weapon list, and DeSelectUnit
        /// returns immediately when that list is empty.
        /// </summary>
        internal static bool Remove(DynamicMap map, Unit unit)
        {
            if (map == null || unit == null)
            {
                return false;
            }

            var removed = false;
            var aircraft = GetLiveAircraft();
            var targets = TargetList(aircraft);

            if (targets != null && targets.Contains(unit))
            {
                Pending.Clear();
                Pending.Add(unit);
                RemoveFromTargetList(aircraft, Pending);
                Pending.Clear();
                removed = true;
            }

            if (IsSelected(map, unit))
            {
                map.DeselectIcon(unit);
                removed = true;
            }

            return removed;
        }

        /// <summary>
        /// Drops every target except one in a single batched remove. The kept unit is never handed to the writer,
        /// so its marker, map icon and HUD list entry are untouched and the server is sent the drop alone, not a
        /// clear and a re-add. A unit that is not in the list at all means every target goes.
        /// </summary>
        internal static void RemoveAllExcept(Aircraft aircraft, Unit keep, bool playSound = true)
        {
            var targets = TargetList(aircraft);
            if (targets == null)
            {
                return;
            }

            Others.Clear();

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target != null && target != keep)
                {
                    Others.Add(target);
                }
            }

            var total = targets.Count;
            var dropped = Others.Count;

            RemoveFromTargetList(aircraft, Others, playSound);
            Others.Clear();

            if (dropped > 0)
            {
                Plugin.Verbose("[keep-one] dropped " + dropped + " of " + total + " target(s).");
            }
        }

        /// <summary>Whether a unit is one of the current targets, which decides whose sound a gesture takes.</summary>
        internal static bool IsTarget(Aircraft aircraft, Unit unit)
        {
            var targets = TargetList(aircraft);
            return targets != null && unit != null && targets.Contains(unit);
        }


        /// <summary>The target list of the current weapon station, or null when nothing is flying.</summary>
        private static List<Unit> TargetList(Aircraft aircraft)
        {
            var weapons = aircraft != null ? aircraft.weaponManager : null;
            return weapons != null ? weapons.GetTargetList() : null;
        }

        /// <summary>How many of the boxed units are targets already, which a replace box keeps where they are.</summary>
        private static int CountInBox(List<Unit> targets, List<Unit> units)
        {
            var count = 0;

            for (var i = 0; i < units.Count; i++)
            {
                if (units[i] != null && targets.Contains(units[i]))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Drops the targets the box left out, in one batched remove. The units inside the box are never
        /// passed here, so nothing that has to stay selected is deselected even for a frame.</summary>
        private static void RemoveTargetsOutsideBox(
            Aircraft aircraft, List<Unit> targets, List<Unit> units, bool playSound)
        {
            Others.Clear();

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                var boxed = false;
                for (var j = 0; j < units.Count; j++)
                {
                    if (units[j] == target)
                    {
                        boxed = true;
                        break;
                    }
                }

                if (!boxed)
                {
                    Others.Add(target);
                }
            }

            var dropped = Others.Count;
            RemoveFromTargetList(aircraft, Others, playSound);
            Others.Clear();

            if (dropped > 0)
            {
                Plugin.Verbose("[replace] dropped " + dropped + " target(s) the box left out.");
            }
        }

        private static void ApplyFlying(Aircraft aircraft, List<Unit> units, bool replace)
        {
            var hud = SceneSingleton<CombatHUD>.i;
            var targets = TargetList(aircraft);
            if (hud == null || targets == null)
            {
                return;
            }

            // A replace box keeps the boxed targets where they are. Clearing the list and adding them back would
            // deselect them on the way, blinking their markers and icons out and sending the server an empty list.
            var limit = Mathf.Clamp(Plugin.MaxTargets.Value, 1, Unit.MAX_STATION_TARGETS);

            // Only units that will be added are new, so the rest of the list does not count against the cap.
            var baseCount = replace ? CountInBox(targets, units) : targets.Count;

            Pending.Clear();

            var alreadySelected = 0;
            var capped = 0;
            var noMarker = 0;

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];

                // Vanilla map clicks only add a unit with a HUD marker. Faction is the map filters' business.
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

                if (baseCount + Pending.Count >= limit)
                {
                    capped++;
                    continue;
                }

                Pending.Add(unit);
            }

            var added = Pending.Count;

            if (replace)
            {
                // Silent when something is coming in, so the gesture ends on the select sound alone. When the box
                // only takes targets away, the drop is the gesture and keeps its own sound.
                RemoveTargetsOutsideBox(aircraft, targets, units, added == 0);
            }

            AddToTargetList(aircraft, Pending);
            Pending.Clear();

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

                // SelectIcon logs an error for an already selected unit, and skips one the Target List filters exclude.
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
                    camera.SetFollowingUnit(units[0]);
                }
            }

            Report("commander", added, alreadySelected, 0);
        }

        /// <summary>Takes every boxed unit that is selected out of the selection, in one refresh while flying.</summary>
        private static void ApplyRemove(DynamicMap map, Aircraft aircraft, List<Unit> units)
        {
            var targets = TargetList(aircraft);
            if (targets != null)
            {
                Pending.Clear();

                for (var i = 0; i < units.Count; i++)
                {
                    if (units[i] != null && targets.Contains(units[i]))
                    {
                        Pending.Add(units[i]);
                    }
                }

                var dropped = Pending.Count;
                RemoveFromTargetList(aircraft, Pending);
                Pending.Clear();

                Plugin.Verbose("[remove] marquee dropped " + dropped + " of " + units.Count + " boxed unit(s).");
                return;
            }

            var removed = 0;

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || !IsSelected(map, unit))
                {
                    continue;
                }

                map.DeselectIcon(unit);
                removed++;
            }

            Plugin.Verbose("[remove] marquee dropped " + removed + " of " + units.Count + " boxed unit(s).");
        }

        /// <summary>Whether this unit's icon is in the map's own selection, which is a separate state from the list.</summary>
        internal static bool IsSelected(DynamicMap map, Unit unit)
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
