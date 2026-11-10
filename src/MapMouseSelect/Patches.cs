using System.Collections.Generic;
using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using Rewired;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MapMouseSelect
{
    /// <summary>
    /// Drives the marquee from the game's own per-frame method. DynamicMap.Update ticks while the map canvas is
    /// active and calls MapControls straight after, so the "skip vanilla this frame" decision is always in time.
    /// BepInEx destroys a component added in Plugin.Awake when the game's first real scene starts, and a Harmony
    /// patch outlives that.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), "Update")]
    internal static class DynamicMapUpdatePatch
    {
        [HarmonyPrefix]
        private static void Prefix(DynamicMap __instance)
        {
            ModHost.Ensure();
            Marquee.Tick(__instance);
        }
    }

    /// <summary>
    /// Cancels a map icon click when the pointer travelled past the game's drag threshold between the press and
    /// the release. uGUI only starts a drag when something in the press hierarchy implements IDragHandler, and
    /// nothing on the map does – its panning is axis driven inside DynamicMap.MapControls – so a drag that returns
    /// to the icon it started from selects it. The threshold is the game's own, from the same helper its mission
    /// editor box select gates on.
    /// </summary>
    [HarmonyPatch(typeof(MapIcon), "UnityEngine.EventSystems.IPointerClickHandler.OnPointerClick",
        new[] { typeof(PointerEventData) })]
    internal static class MapIconPointerClickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return true;
            }

            if (!UnitSelection.DragThresholdPassed(eventData.pressPosition))
            {
                return true;
            }

            Plugin.Verbose(
                "Cancelled a map icon click: the pointer travelled "
                + Mathf.RoundToInt(Vector2.Distance(eventData.pressPosition, Input.mousePosition))
                + " px from the press.");

            return false;
        }
    }

    /// <summary>
    /// Makes shift+click on an already selected unit deselect it. UnitMapIcon.ClickIcon deselects through
    /// CombatHUD.DeSelectUnit when there is no live aircraft, which returns at once on an empty weapon target list,
    /// or through DynamicMap.DeselectIcon otherwise, which clears the icon and the panel row but leaves the unit in
    /// the target list, so the weapon stays locked to a unit the player just dropped. Both siblings that deselect,
    /// TargetListSelector.ForceDeselect and TargetListSelector_UnitItem.ToggleSelect, route the other way round.
    /// This patch also makes a plain mouse click replace the target list while flying, which vanilla refuses.
    /// </summary>
    [HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.ClickIcon))]
    internal static class UnitMapIconClickPatch
    {
        /// <summary>Rejects excluded new selections and handles Shift removal. Mouse clicks replace targets;
        /// the native Select action keeps its additive selecting branch.</summary>
        [HarmonyPrefix]
        private static bool Prefix(UnitMapIcon __instance, MapIcon.ClickSource clickSource)
        {
            var map = SceneSingleton<DynamicMap>.i;
            var hud = SceneSingleton<CombatHUD>.i;
            if (map == null || hud == null || __instance == null || __instance.unit == null)
            {
                return true;
            }

            if (__instance.unit == hud.aircraft || !map.IsCursorInMapRectangle())
            {
                return true;
            }

            // Reject an excluded new selection before vanilla clears the old selection or moves the camera.
            // An already selected unit must remain removable even when its filters now exclude it.
            if (Excluded(__instance.unit) && !SelectionApplier.IsSelected(map, __instance.unit))
            {
                Plugin.Verbose("Ignored a click on " + __instance.unit.name + ": the filters exclude it.");
                return false;
            }

            // Reject an unmarkable new target before the plain-click replace drops existing targets.
            // CreateMarker can refuse scenery or units first seen with no local aircraft.
            if (SelectionApplier.GetLiveAircraft() != null
                && !hud.MarkerExists(__instance.unit)
                && !SelectionApplier.IsSelected(map, __instance.unit))
            {
                Plugin.Verbose(
                    "Ignored a click on " + __instance.unit.name + ": the HUD has no marker for it, so it cannot be a target.");
                return false;
            }

            if (!VanillaCalls.ShiftHeld())
            {
                // A plain mouse click means "this one", as a plain marquee drag does. Everything but the clicked
                // unit goes, and it is never dropped and put back, so its icon and HUD entry stay lit through the click.
                if (clickSource == MapIcon.ClickSource.Mouse && !Excluded(__instance.unit))
                {
                    var aircraft = SelectionApplier.GetLiveAircraft();

                    // One sound either way: the drop when the unit stays selected, the click's own select sound
                    // when it is new.
                    SelectionApplier.RemoveAllExcept(
                        aircraft, __instance.unit, SelectionApplier.IsTarget(aircraft, __instance.unit));
                }

                return true;
            }

            return !SelectionApplier.Remove(map, __instance.unit);
        }

        /// <summary>Whether the player's Target List filters keep this unit out.</summary>
        private static bool Excluded(Unit unit)
        {
            var selector = SceneSingleton<TargetListSelector>.i;
            return selector != null && selector.CheckExclusions(unit);
        }
    }

    /// <summary>
    /// Keeps a selected unit's map icon clickable. MapIcon.SelectIcon clears the icon's raycast target and nothing
    /// puts it back until the icon is deselected, and a uGUI graphic without a raycast target gets no pointer events
    /// at all, so a selected unit could not be hovered or clicked - the click lands on the map behind the icon, with
    /// no error. Only unit icons are restored: AirbaseMapIcon clears its own target on purpose once an airbase is
    /// selected, and HighlightIcon clears the local aircraft's icon whenever it rebuilds its colour.
    /// </summary>
    [HarmonyPatch(typeof(MapIcon), nameof(MapIcon.SelectIcon))]
    internal static class MapIconSelectIconPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MapIcon __instance)
        {
            if (__instance is UnitMapIcon icon && icon.iconImage != null)
            {
                icon.iconImage.raycastTarget = true;
            }
        }
    }

    /// <summary>
    /// Logs entering a cockpit. Diagnostics only, one line per transition. Aircraft.OnStartClient calls SetAircraft
    /// and then DynamicMap.DeselectAllIcons in the same local-player branch, so this line marks where a
    /// commander-view selection stops existing.
    /// </summary>
    [HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.SetAircraft))]
    internal static class CombatHudSetAircraftPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CombatHUD __instance)
        {
            var aircraft = __instance.aircraft;
            Plugin.Verbose("Entered " + (aircraft != null ? aircraft.name : "an aircraft") + ".");
        }
    }

    /// <summary>Logs leaving a cockpit. CombatHUD.RemoveAircraft runs on death.</summary>
    [HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.RemoveAircraft))]
    internal static class CombatHudRemoveAircraftPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            Plugin.Verbose("Left the aircraft.");
        }
    }

    /// <summary>Adds the mod's actions to Rewired while it is still loading the game's input data.</summary>
    [HarmonyPatch(typeof(InputManager_Base), "Awake")]
    internal static class RewiredInputManagerAwakePatch
    {
        [HarmonyPrefix]
        private static void Prefix(InputManager_Base __instance)
        {
            ModActions.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "MapControls")]
    internal static class DynamicMapMapControlsPatch
    {
        /// <summary>Number of pan-gate call sites the transpiler rewrote. -1 means it never ran.</summary>
        internal static int PanReplacements { get; private set; } = -1;

        /// <summary>Skipping vanilla controls while the marquee owns the mouse holds off the click-select and
        /// the left-drag map panning for the duration of the drag.</summary>
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !Marquee.OwnsMouse;
        }
        /// <summary>Moves map panning off the hardcoded left mouse button. Gameplay-independent: the pan gate
        /// is the only plain Input.GetMouseButton call in MapControls, so exactly one site must match.</summary>
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = AccessTools.Method(typeof(Input), nameof(Input.GetMouseButton), new[] { typeof(int) });
            var replacement = AccessTools.Method(typeof(PanButtonCompat), nameof(PanButtonCompat.GetMouseButton));

            var patched = 0;
            var result = new List<CodeInstruction>();

            foreach (var instruction in instructions)
            {
                if (original != null && replacement != null && instruction.Calls(original))
                {
                    instruction.operand = replacement;
                    patched++;
                }
                result.Add(instruction);
            }

            PanReplacements = patched;

            if (patched != 1)
            {
                Plugin.Log.LogWarning(
                    "Expected exactly one map-pan GetMouseButton call in DynamicMap.MapControls but found "
                    + patched + ". The PanButton setting will have no effect. The game may have changed.");
            }

            return result;
        }
    }

    /// <summary>
    /// Writes the mod's default keys back after the Controls menu's Restore Defaults.
    ///
    /// That button reloads the maps authored in Rewired and clears the rest, and the mod's actions were added to
    /// Rewired at runtime, so its keys go with them. A postfix runs after either branch of the method, the map reload
    /// and the delegate the game may set in its place.
    /// </summary>
    [HarmonyPatch(typeof(Rewired.UI.ControlMapper.ControlMapper), "OnRestoreDefaultsConfirmed")]
    internal static class RestoreDefaultsPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            ModActions.ForgetDefaults();
        }
    }

}
