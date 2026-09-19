using System.Reflection;
using HarmonyLib;
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
        private static MethodInfo _targetSelect;
        private static bool _targetSelectLookupFailed;

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

                Marquee.ClickNearestIcon(map);
                return;
            }

            if (!flying)
            {
                return;
            }

            // Map closed: the only thing on screen to select is whatever the designator is pointing at.
            hud.DeselectAll(false);
            InvokeTargetSelect(hud);
        }

        /// <summary>
        /// TargetSelect is private, and it owns the marker priority ranking and the 100px designator
        /// range, so it is called rather than reimplemented.
        /// </summary>
        private static void InvokeTargetSelect(CombatHUD hud)
        {
            if (_targetSelect == null && !_targetSelectLookupFailed)
            {
                _targetSelect = AccessTools.Method(typeof(CombatHUD), "TargetSelect", new[] { typeof(bool) });
                if (_targetSelect == null)
                {
                    _targetSelectLookupFailed = true;
                    Plugin.Log.LogWarning(
                        "CombatHUD.TargetSelect was not found, so the select-only key cannot designate from "
                        + "the cockpit. Selecting on the map still works.");
                }
            }

            if (_targetSelect != null)
            {
                _targetSelect.Invoke(hud, new object[] { false });
            }
        }
    }
}
