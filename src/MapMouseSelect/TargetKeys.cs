using System.Collections.Generic;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Key handling for the current target selection.
    ///
    /// The keys are real Rewired actions from ModActions, so they are rebindable and read with vanilla's own
    /// calls. Tick runs from ModHost, since the plugin's object does not outlive the boot. A live aircraft uses
    /// CombatHUD's weapon target list, anything else DynamicMap's selected map icons.
    /// </summary>
    internal static class TargetKeys
    {
        private static bool _longFired;

        private static readonly List<Unit> One = new List<Unit>(1);

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

            if (player.GetButtonDown(ModActions.DeselectLookedAtName))
            {
                DeselectLookedAt();
            }

            // The same thresholds ExtraUiInput uses for Cancel: the hold fires as soon as pressDelay has elapsed,
            // and a release within clickDelay is a tap.
            if (player.GetButtonTimedPressDown(ModActions.DeselectOldestName, PlayerSettings.pressDelay))
            {
                _longFired = true;
                KeepOnlyNewest();
            }

            if (player.GetButtonUp(ModActions.DeselectOldestName))
            {
                // Vanilla fires both branches for a hold between clickDelay and pressDelay, since clearing
                // everything and then dropping the newest of nothing compose. These two do not compose.
                if (!_longFired)
                {
                    DropOldest();
                }

                _longFired = false;
            }
        }

        /// <summary>
        /// Drops the selection made first. The weapon list is built with Insert(0, ...), so the oldest is last,
        /// while map icons are appended, so the oldest is first.
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

                // This is the one that stays.
                var newest = targets[0];
                Plugin.Verbose("Keeping only " + (newest != null ? newest.name : "<null>") + ".");

                // The kept target never reaches the writer, so its marker and map icon never blink, and the server
                // is sent the drop, not a clear followed by a target.
                SelectionApplier.RemoveAllExcept(aircraft, newest);
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
        /// Drops the unit being pointed at out of the selection, picking it the way SelectOnly does. The Target
        /// List filters gate the pick, so a unit they hide cannot be dropped here either.
        /// </summary>
        private static void DeselectLookedAt()
        {
            var map = SceneSingleton<DynamicMap>.i;
            if (map == null)
            {
                return;
            }

            var unit = PointedAt(map, SceneSingleton<CombatHUD>.i);
            if (unit == null)
            {
                Plugin.Verbose("Deselect-looked-at found nothing being pointed at.");
                return;
            }

            if (SelectionApplier.Remove(map, unit))
            {
                Plugin.Verbose("Deselected " + unit.name + ".");
            }
            else
            {
                Plugin.Verbose(unit.name + " was not selected, so nothing was dropped.");
            }
        }

        /// <summary>
        /// Prunes the target list down to the unit being pointed at. Vanilla's picks are not called here, because
        /// the own pick deliberately includes selected units. The native map picker
        /// is separately affected by this mod restoring selected-icon raycasts. Empty sky leaves the list alone.
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
                Plugin.Verbose("Select-only found nothing being pointed at. The target list is unchanged.");
                return;
            }

            if (SelectionApplier.IsTarget(aircraft, keep))
            {
                // One refresh, one deselect sound, and the marker and map icon never blink.
                SelectionApplier.RemoveAllExcept(aircraft, keep);
                Plugin.Verbose("Select-only kept " + keep.name + " and dropped the other target(s).");
                return;
            }

            // Not a target yet means "only this one". Silent drops, so the press ends on the select sound.
            SelectionApplier.RemoveAllExcept(aircraft, keep, false);

            One.Clear();
            One.Add(keep);
            SelectionApplier.AddToTargetList(aircraft, One);
            One.Clear();
            Plugin.Verbose("Select-only replaced the target list with " + keep.name + ".");
        }

        /// <summary>
        /// What the player is pointing at, with the same 100 pixel radius the two vanilla picks use. Neither pick
        /// is called here; this pick explicitly includes selected units.
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

        /// <summary>
        /// A unit this key may point at: not the player's own aircraft, and not excluded by the Target List
        /// filters. That is the same gate vanilla's clicks and the marquee use.
        /// </summary>
        private static bool Pickable(Unit unit)
        {
            if (unit == null)
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

        private static List<Unit> TargetList(CombatHUD hud)
        {
            var aircraft = hud != null ? SelectionApplier.GetLiveAircraft() : null;
            var weapons = aircraft != null ? aircraft.weaponManager : null;
            return weapons != null ? weapons.GetTargetList() : null;
        }
    }
}
