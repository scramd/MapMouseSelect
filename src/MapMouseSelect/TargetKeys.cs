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

        /// <summary>Scratch for the single unit a key hands to the batched target list writers.</summary>
        private static readonly List<Unit> One = new List<Unit>(1);

        /// <summary>Scratch for the targets a prune is about to drop.</summary>
        private static readonly List<Unit> Others = new List<Unit>();

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
                    One.Clear();
                    One.Add(oldest);
                    SelectionApplier.RemoveFromTargetList(aircraft, One);
                    One.Clear();
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

                // The mass clear is the gesture here, so it takes the HUD's deselect-all sound, the one
                // vanilla's own Cancel-hold plays. The single unit going back in is silent, so the key does not
                // end on a select chime after dropping everything else.
                hud.DeselectAll(true);

                if (newest != null)
                {
                    // Two refreshes for the whole key press, rather than one per dropped target.
                    One.Clear();
                    One.Add(newest);
                    SelectionApplier.AddToTargetList(aircraft, One, false);
                    One.Clear();
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
        /// Prunes the target list down to the unit being pointed at: with the map up that is the icon under
        /// the cursor, with it closed the marker under the target designator.
        ///
        /// Nothing is delegated here, unlike the rest of the mod. Both of vanilla's picks refuse a unit that is
        /// already selected, so pointing at a target and asking for only that one would do nothing, and emptying
        /// the list first to get around that is what made the key lose the whole selection to a press that landed
        /// on nothing. Writing the prune out is what makes the key mean what it says, and it costs one refresh.
        ///
        /// Pointing at empty sky leaves the list alone. With nothing flying there is no target list, and
        /// vanilla's own Select already means this in commander view, so that stays delegated.
        /// </summary>
        private static void SelectOnly()
        {
            var map = SceneSingleton<DynamicMap>.i;
            if (map == null)
            {
                return;
            }

            var aircraft = SelectionApplier.GetLiveAircraft();

            if (aircraft == null)
            {
                if (DynamicMap.mapMaximized)
                {
                    VanillaCalls.SelectFromMap(map);
                }

                return;
            }

            var hud = SceneSingleton<CombatHUD>.i;
            var targets = TargetList(hud);
            if (targets == null)
            {
                return;
            }

            var keep = PointedAt(map, hud);
            if (keep == null)
            {
                Plugin.Verbose("Select-only found nothing being pointed at; the target list is unchanged.");
                return;
            }

            Others.Clear();
            var isTarget = false;

            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] == keep)
                {
                    isTarget = true;
                }
                else
                {
                    Others.Add(targets[i]);
                }
            }

            if (isTarget)
            {
                // The kept target never leaves the list, so its marker and map icon stay lit while the rest go,
                // all in one refresh and one deselect sound.
                SelectionApplier.RemoveFromTargetList(aircraft, Others);
                Plugin.Verbose(
                    "Select-only kept " + keep.name + ", dropped " + Others.Count + " other target(s).");
            }
            else
            {
                // Pointing at something that is not a target means "only this one", the same as a plain click
                // does in commander view.
                if (hud != null)
                {
                    hud.DeselectAll(false);
                }

                One.Clear();
                One.Add(keep);
                SelectionApplier.AddToTargetList(aircraft, One);
                One.Clear();
                Plugin.Verbose("Select-only replaced the target list with " + keep.name + ".");
            }

            Others.Clear();
        }

        /// <summary>
        /// What the player is pointing at. With the map up that is the map icon nearest the cursor, the pick
        /// DynamicMap.SelectFromMap makes; with it closed it is the HUD marker nearest the target designator,
        /// the pick CombatHUD.TargetSelect makes. Both use a 100 pixel radius.
        ///
        /// Neither pick is called, because both skip a unit that is already selected: a selected map icon has
        /// its raycast target cleared, and TargetSelect skips a marker marked selected. Distance alone is what
        /// this key needs, with the same gates the rest of the mod applies.
        /// </summary>
        private static Unit PointedAt(DynamicMap map, CombatHUD hud)
        {
            var best = (Unit)null;
            var bestDistance = 100f * 100f;

            if (DynamicMap.mapMaximized)
            {
                var icons = map.mapIcons;
                if (icons == null)
                {
                    return null;
                }

                var cursor = (Vector2)Input.mousePosition;

                for (var i = 0; i < icons.Count; i++)
                {
                    if (!(icons[i] is UnitMapIcon icon))
                    {
                        continue;
                    }

                    var unit = icon.unit;
                    if (!Pickable(unit) || !icon.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    var distance = ((Vector2)icon.transform.position - cursor).sqrMagnitude;
                    if (distance <= bestDistance)
                    {
                        bestDistance = distance;
                        best = unit;
                    }
                }

                return best;
            }

            var markers = VanillaCalls.Markers(hud);
            var designator = hud != null ? hud.targetDesignator : null;
            if (markers == null || designator == null)
            {
                return null;
            }

            var aim = (Vector2)designator.transform.position;

            for (var i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                if (marker == null || marker.image == null || !marker.image.enabled)
                {
                    continue;
                }

                var unit = marker.unit;
                if (!Pickable(unit))
                {
                    continue;
                }

                var distance = ((Vector2)marker.image.transform.position - aim).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = unit;
                }
            }

            return best;
        }

        /// <summary>A live unit that is not the player's own aircraft and survives their own filters.</summary>
        private static bool Pickable(Unit unit)
        {
            if (unit == null || unit.disabled)
            {
                return false;
            }

            var hud = SceneSingleton<CombatHUD>.i;
            if (hud != null && unit == hud.aircraft)
            {
                return false;
            }

            var selector = SceneSingleton<TargetListSelector>.i;
            return selector == null || !selector.CheckExclusions(unit);
        }

        /// <summary>
        /// The weapon target list, or null when nothing is flying. CombatHUD's own list is this same
        /// instance, so what a key just changed is visible immediately.
        /// </summary>
        private static List<Unit> TargetList(CombatHUD hud)
        {
            var aircraft = hud != null ? SelectionApplier.GetLiveAircraft() : null;
            var weapons = aircraft != null ? aircraft.weaponManager : null;
            return weapons != null ? weapons.GetTargetList() : null;
        }
    }
}
