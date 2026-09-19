using NuclearOption.MissionEditorScripts;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Keyboard actions that operate on the current target selection.
    ///
    /// Ticked from the same DynamicMap.Update prefix as the marquee. That method runs every frame the
    /// map object is alive, maximized or not, so the cockpit path gets a tick with the map closed too.
    ///
    /// Both map modes are covered because they keep the selection in different places: a live aircraft
    /// uses CombatHUD's weapon target list, anything else uses DynamicMap's selected map icons.
    /// </summary>
    internal static class TargetKeys
    {
        private const float FallbackPressDelay = 0.15f;
        private const float FallbackClickDelay = 0.25f;

        private static bool _held;
        private static bool _longFired;
        private static float _pressedAt;

        internal static void Tick()
        {
            if (InputFieldChecker.InsideInputField)
            {
                return;
            }

            if (GameManager.gameState == GameState.Editor && !Plugin.EnableInMissionEditor.Value)
            {
                return;
            }

            if (Plugin.SelectOnlyKey.Value.IsDown())
            {
                SelectOnly();
            }

            HandleDeselectOldest();
        }

        /// <summary>
        /// Tap drops the oldest selection, hold keeps only the newest. The timings are the game's own:
        /// ExtraUiInput does the same thing for Cancel, reading GetButtonTimedPressUp(clickDelay) for
        /// the tap and GetButtonTimedPressDown(pressDelay) for the hold, so a hold has to trigger once
        /// the threshold passes rather than on release.
        /// </summary>
        private static void HandleDeselectOldest()
        {
            var key = Plugin.DeselectOldestKey.Value;

            if (!_held)
            {
                if (key.IsDown())
                {
                    _held = true;
                    _longFired = false;
                    _pressedAt = Time.unscaledTime;
                }
                return;
            }

            var heldFor = Time.unscaledTime - _pressedAt;

            if (!_longFired && heldFor >= PressDelay())
            {
                _longFired = true;
                KeepOnlyNewest();
            }

            if (key.IsUp())
            {
                _held = false;

                // Vanilla fires nothing at all for a hold between clickDelay and pressDelay.
                if (!_longFired && heldFor <= ClickDelay())
                {
                    DropOldest();
                }

                _longFired = false;
            }
        }

        private static float PressDelay()
        {
            return PlayerSettings.pressDelay > 0f ? PlayerSettings.pressDelay : FallbackPressDelay;
        }

        private static float ClickDelay()
        {
            return PlayerSettings.clickDelay > 0f ? PlayerSettings.clickDelay : FallbackClickDelay;
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
        /// Drops the whole selection, then selects what the cursor is on. Vanilla's Select action only
        /// ever adds, so replacing the list needs a clear first.
        /// </summary>
        private static void SelectOnly()
        {
            var map = SceneSingleton<DynamicMap>.i;
            if (map == null)
            {
                return;
            }

            var hud = SceneSingleton<CombatHUD>.i;
            var flying = hud != null && SelectionApplier.GetLiveAircraft() != null;

            if (DynamicMap.mapMaximized)
            {
                if (flying)
                {
                    // Clears the weapon target list, the HUD markers and the map icons together.
                    hud.DeselectAll(false);
                }
                else
                {
                    map.UnselectAll();
                }

                VanillaCalls.SelectFromMap(map);
                return;
            }

            if (!flying)
            {
                return;
            }

            // Map closed: the only thing on screen to select is whatever the designator is pointing at.
            hud.DeselectAll(false);
            VanillaCalls.TargetSelect(hud, false);
        }
    }
}
