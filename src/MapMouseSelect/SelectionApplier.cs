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
    /// Routes a selection change to whichever system owns it, and owns the weapon target list writes itself.
    ///
    /// Flying, the selection is the target list of the current weapon station, and that list is networked:
    /// every change is a server call, and the RPC behind it has no rate limit. CombatHUD.SelectUnit and
    /// DeSelectUnit handle one unit each and refresh the list each time, so a gesture over thirty units through
    /// them is thirty messages. Vanilla's own multi-select does not work that way: TargetSelect marks every
    /// unit, inserts into the list and calls TargetListChanged once, then plays one sound. That is the shape of
    /// AddToTargetList and RemoveFromTargetList below, and every path in the mod that writes the list goes
    /// through them.
    ///
    /// The clear is the one thing still left to vanilla: CombatHUD.DeselectAll is already a single refresh, and
    /// it also puts the HUD's target arrow and text away, which are private fields, so writing it again here
    /// would buy nothing.
    ///
    /// With no live aircraft there is no target list at all. The selection is DynamicMap's selected icons, a
    /// local HUD thing with no network behind it, and it goes through the same calls vanilla's clicks use.
    /// </summary>
    internal static class SelectionApplier
    {
        /// <summary>Scratch for the units a batched write is about to apply. The paths that use it are all reached from
        /// a key press or a released mouse button, one at a time, and none of them nests.</summary>
        private static readonly List<Unit> Pending = new List<Unit>();

        /// <summary>Scratch for the targets a change is about to take away.</summary>
        private static readonly List<Unit> Others = new List<Unit>();

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
                // An empty box clears the selection, which is the usual marquee meaning. Vanilla does not
                // clear on a click in empty space, so this is a mod choice: a box is a targeting gesture, and
                // releasing it over nothing is how a player expects to drop what is selected. Adding or
                // removing from an empty box has nothing to do either way.
                if (mode == MarqueeMode.Replace)
                {
                    if (aircraft != null)
                    {
                        // Same clear a replace box does before it adds anything, so an empty one leaves the
                        // weapon with its targets dropped rather than keeping the previous box's picks.
                        var hud = SceneSingleton<CombatHUD>.i;
                        if (hud != null)
                        {
                            hud.DeselectAll(false);
                        }
                    }
                    else
                    {
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
        /// Adds units to the target list of the current weapon station, then refreshes it and plays the select
        /// sound once for the whole set.
        ///
        /// The per-unit effects are vanilla's: AddTargetList inserts at the front, so the last unit added ends
        /// up first, which is the newest target, and the HUD marker's SelectMarker is what selects the map icon
        /// as well. A unit already in the list is skipped, which is what vanilla's callers check for before
        /// calling SelectUnit.
        ///
        /// The caller decides the sound, because the sound belongs to the gesture rather than to the write: a
        /// mass deselect that keeps one unit still reads as a deselect, and ending it on a select chime would
        /// say the opposite of what happened.
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
        /// Takes units out of the target list and refreshes it once, the mirror of AddToTargetList.
        ///
        /// CombatHUD.DeSelectUnit is what this reproduces: the HUD marker's DeselectMarker, the map icon, the
        /// list removal. DeselectMarker already ends in DynamicMap.DeselectIcon, and the direct call is here
        /// for the unit that has no marker at all, whose icon would otherwise stay selected on the map.
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
        /// Takes one unit out of the selection by whichever system owns it, and says whether it was in there at
        /// all. A weapon target has to leave the target list too, or the weapon stays locked to a unit the
        /// player just dropped; the commander's selection only lives on the map.
        /// </summary>
        internal static bool Remove(DynamicMap map, Unit unit)
        {
            if (map == null || unit == null)
            {
                return false;
            }

            var aircraft = GetLiveAircraft();
            var targets = TargetList(aircraft);
            if (targets != null)
            {
                if (!targets.Contains(unit))
                {
                    return false;
                }

                Pending.Clear();
                Pending.Add(unit);
                RemoveFromTargetList(aircraft, Pending);
                Pending.Clear();
                return true;
            }

            if (!IsSelected(map, unit))
            {
                return false;
            }

            map.DeselectIcon(unit);
            return true;
        }

        /// <summary>
        /// Drops every target except one, in a single batched remove.
        ///
        /// The kept unit is never handed to the writer, which is the point of the helper: nothing that has to
        /// stay selected is deselected, not even for the length of a call, so its marker, its map icon and the
        /// HUD's target list entry are untouched and the server is sent the drop alone rather than a clear and a
        /// re-add. A unit that is not in the list at all means every target goes, which is the click that means
        /// "this one" when the one it means is not a target yet.
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

        /// <summary>
        /// Drops the targets the box left out, in one batched remove. The units inside the box are never passed
        /// here, so nothing that has to stay selected is deselected even for a frame.
        /// </summary>
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

            if (replace)
            {
                // A replace box means the boxed units, so the targets it keeps are the boxed ones and they stay
                // where they are. Clearing the list and adding them back would deselect them on the way: their
                // markers and map icons would blink out and back, and the server would be sent an empty list in
                // between. Only the targets the box leaves out are dropped, below.
                RemoveTargetsOutsideBox(aircraft, targets, units, false);
            }

            var limit = Mathf.Clamp(Plugin.MaxTargets.Value, 1, Unit.MAX_STATION_TARGETS);

            // Only the units that will be added are new, whenever the box keeps some of what it covers. The
            // rest of the list is either already there or on its way out, so it does not count against the cap.
            var baseCount = replace ? CountInBox(targets, units) : targets.Count;

            Pending.Clear();

            var alreadySelected = 0;
            var capped = 0;
            var noMarker = 0;

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];

                // Vanilla map clicks only add a unit that has a HUD marker to select on. Nothing here looks at
                // the unit's faction: the map's own filters are the player's tool for that, and the marquee is
                // deliberately dumb about it.
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
                // Silent when something is coming in, because the gesture then ends on a selection and should
                // end on the select sound rather than on both. When the box only takes targets away, the drop is
                // the gesture and keeps its own sound.
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

                // DynamicMap.SelectIcon logs an error for a unit that is already selected, so filter. It also
                // skips a unit the Target List filters exclude, silently and before it does anything, which is
                // the same gate vanilla's own clicks run into.
                if (IsSelected(map, unit))
                {
                    alreadySelected++;
                    continue;
                }

                map.SelectIcon(unit);
                added++;
            }

            // No sound here: vanilla's commander path is DynamicMap.SelectIcon, which is silent. The sound
            // belongs to the weapon target selection, which is the flying mode.
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
