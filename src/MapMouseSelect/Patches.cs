using System.Collections.Generic;
using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using Rewired;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MapMouseSelect
{
    /// <summary>
    /// Drives the marquee from the game's own per-frame method.
    ///
    /// DynamicMap.Update ticks whenever the map canvas object is active and calls MapControls straight
    /// after, so running the state machine here means the "skip vanilla this frame" decision is always
    /// made in time. This deliberately does not depend on a GameObject of our own: BepInEx destroys its
    /// plugin host object when the game's first real scene initialises, which takes any component added
    /// in Plugin.Awake with it. Harmony patches outlive that.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), "Update")]
    internal static class DynamicMapUpdatePatch
    {
        [HarmonyPrefix]
        private static void Prefix(DynamicMap __instance)
        {
            // Cheap guard against the host object having been lost during the game's boot.
            ModHost.Ensure();
            Marquee.Tick(__instance);
        }
    }

    /// <summary>
    /// Keeps vanilla's own camera jump out of the middle mouse button, which is this mod's map pan now.
    ///
    /// The jump is not gone: CameraJump makes the same call from a double left click, and that is the only
    /// thing this lets through. Vanilla reaches this method from one place, the Jump Map branch in
    /// MapControls, which requires the map to be maximized and the commander to have no aircraft.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), "JumpCameraTo")]
    internal static class DynamicMapJumpCameraToPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return CameraJump.Requested;
        }
    }

    /// <summary>
    /// Cancels a map icon click when the pointer travelled past the game's drag threshold between the
    /// press and the release.
    ///
    /// uGUI only does this when the pressed object has somewhere to hand the drag to: PointerInputModule
    /// clears eligibleForClick when the pressed object and the drag handler differ, and it never starts a
    /// drag at all when nothing in the hierarchy implements IDragHandler. Nothing on the map does, its
    /// panning is axis driven inside DynamicMap.MapControls, so pressing an icon, dragging across the map
    /// and releasing back on the same icon selects it. Nobody expects a drag to end in a click, and for
    /// the marquee it is not cosmetic: the drag is the gesture being used.
    ///
    /// The threshold is the game's own, through the same helper its mission editor box select gates on, so
    /// this cannot disagree with the UI about where a click ends and a drag begins.
    ///
    /// The target is the compiler generated explicit implementation of IPointerClickHandler on MapIcon,
    /// which all of the map's icons share. That name is not the game's to change, but it is a name, so
    /// Plugin.ReportPatchState reports whether a prefix landed on it.
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
    /// Makes shift+click on an already selected unit deselect it, which is what UnitMapIcon.ClickIcon sets
    /// out to do and gets wrong in both modes.
    ///
    /// Its deselect branch is:
    ///   aircraft null or disabled -> CombatHUD.DeSelectUnit
    ///   otherwise                 -> DynamicMap.DeselectIcon
    ///
    /// With no aircraft the weapon target list is empty, and DeSelectUnit starts by returning when that
    /// list is empty, so the click does nothing. With one, DeselectIcon clears the icon and the target
    /// list panel row but leaves the unit in the target list, so the weapon stays locked to a unit the
    /// player just tried to drop. Both siblings that deselect, TargetListSelector.ForceDeselect and
    /// TargetListSelector_UnitItem.ToggleSelect, route the other way round, and that is what this does.
    /// </summary>
    [HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.ClickIcon))]
    internal static class UnitMapIconClickPatch
    {
        /// <summary>
        /// Returns false only for the case it handles, so selecting, additive selecting and the vanilla
        /// guards are all left exactly as they were.
        /// </summary>
        [HarmonyPrefix]
        private static bool Prefix(UnitMapIcon __instance)
        {
            var map = SceneSingleton<DynamicMap>.i;
            var hud = SceneSingleton<CombatHUD>.i;
            if (map == null || hud == null || __instance == null || __instance.unit == null)
            {
                return true;
            }

            // The same gates vanilla applies before it ever looks at the selection state.
            if (__instance.unit == hud.aircraft || !map.IsCursorInMapRectangle() || !VanillaCalls.ShiftHeld())
            {
                return true;
            }

            if (!IsSelected(map, __instance))
            {
                return true;
            }

            Deselect(map, hud, __instance.unit);
            return false;
        }

        /// <summary>
        /// Reads the selection through the public list rather than the icon's protected isSelected flag,
        /// since the list is what a deselect can actually take the unit out of.
        /// </summary>
        private static bool IsSelected(DynamicMap map, UnitMapIcon icon)
        {
            var selected = map.selectedIcons;
            return selected != null && selected.Contains(icon);
        }

        private static void Deselect(DynamicMap map, CombatHUD hud, Unit unit)
        {
            var aircraft = SelectionApplier.GetLiveAircraft();
            var weapons = aircraft != null ? aircraft.weaponManager : null;
            var targets = weapons != null ? weapons.GetTargetList() : null;

            // A weapon target has to leave the target list too, or the weapon stays locked to it.
            if (targets != null && targets.Contains(unit))
            {
                hud.DeSelectUnit(unit);
                return;
            }

            map.DeselectIcon(unit);
        }
    }

    /// <summary>
    /// Adds the mod's actions to Rewired while it is still loading the game's input data.
    /// </summary>
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

        /// <summary>
        /// Skipping vanilla controls while the marquee owns the mouse is what holds off both the
        /// click-select and the left-drag map panning for the duration of the drag.
        /// </summary>
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !Marquee.OwnsMouse;
        }
        /// <summary>
        /// Moves map panning off the hardcoded left mouse button. Gameplay-independent: the pan gate is
        /// the only plain Input.GetMouseButton call in MapControls, so exactly one site must match.
        /// </summary>
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
                    + patched + ". The PanButton setting will have no effect; the game may have changed.");
            }

            return result;
        }
    }
}
