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
    ///
    /// It also makes a plain mouse click replace the target list while flying, which vanilla's branch refuses
    /// to do; the prefix carries the reasoning for why those two belong in the same patch.
    /// </summary>
    [HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.ClickIcon))]
    internal static class UnitMapIconClickPatch
    {
        /// <summary>
        /// Returns false only for the shift+click-on-an-already-selected-unit case. A plain mouse click is
        /// left to vanilla, but only after clearing the target list, and the Select key is not touched.
        /// </summary>
        [HarmonyPrefix]
        private static bool Prefix(UnitMapIcon __instance, MapIcon.ClickSource clickSource)
        {
            var map = SceneSingleton<DynamicMap>.i;
            var hud = SceneSingleton<CombatHUD>.i;
            if (map == null || hud == null || __instance == null || __instance.unit == null)
            {
                return true;
            }

            // The same gates vanilla applies before it ever looks at the selection state.
            if (__instance.unit == hud.aircraft || !map.IsCursorInMapRectangle())
            {
                return true;
            }

            // A unit the player's own filters exclude is not selectable, but the click still gets here in vanilla:
            // in commander view it clears the whole selection before selecting nothing, and while flying it plays
            // the selection sound for a target list it then refuses to change. Doing nothing is what the filter
            // has to mean for a click to make sense.
            if (Excluded(__instance.unit))
            {
                Plugin.Verbose("Ignored a click on " + __instance.unit.name + ": the filters exclude it.");
                return false;
            }

            if (!VanillaCalls.ShiftHeld())
            {
                // A plain mouse click means "this one", the same as a plain marquee drag, and vanilla's
                // flying branch only ever adds: it calls UnselectAll only when there is no live aircraft.
                // Everything but the clicked unit goes, which is also what lets vanilla take a unit that is not
                // a target yet; the unit the click is about is never dropped and put back, so its icon and its HUD
                // entry stay lit through the click. Shift is the additive click and the Select key is left as
                // vanilla wrote it, so only the mouse is patched here.
                if (clickSource == MapIcon.ClickSource.Mouse && !Excluded(__instance.unit))
                {
                    var aircraft = SelectionApplier.GetLiveAircraft();

                    // The drop is the gesture when the clicked unit stays selected, and the click's own select
                    // sound is the gesture when it is new: one sound either way.
                    SelectionApplier.RemoveAllExcept(
                        aircraft, __instance.unit, SelectionApplier.IsTarget(aircraft, __instance.unit));
                }

                return true;
            }

            // Shift on a unit that is already selected deselects it. Removing reports whether the unit was
            // in the selection, which is the condition this half is here for.
            return !SelectionApplier.Remove(map, __instance.unit);
        }

        /// <summary>
        /// Whether the player's Target List filters keep this unit out, which is what would leave vanilla's
        /// select branch with nothing to add after the list was cleared.
        /// </summary>
        private static bool Excluded(Unit unit)
        {
            var selector = SceneSingleton<TargetListSelector>.i;
            return selector != null && selector.CheckExclusions(unit);
        }
    }

    /// <summary>
    /// Keeps a selected unit's map icon clickable.
    ///
    /// MapIcon.SelectIcon clears the icon's raycast target and nothing puts it back until the icon is
    /// deselected again. A uGUI graphic that is not a raycast target receives no pointer events at all, so a
    /// selected unit could not be hovered for its tooltip and could not be clicked: vanilla's own shift-click
    /// deselect branch inside ClickIcon was unreachable, and so were this mod's replacement for it and its
    /// click replace. Nothing about that shows up as an error, the click simply lands on the map behind the
    /// icon, which is why a click on something already selected appeared to do nothing.
    ///
    /// Only unit icons are restored. AirbaseMapIcon.ClickIcon clears its own target on purpose once an airbase
    /// is selected, and the local aircraft's icon is cleared by HighlightIcon every time its colour is rebuilt.
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
    /// Takes a unit out of the weapon target list when it dies.
    ///
    /// Vanilla leaves a dead unit in the target list: its HUD marker is removed by the disable handler, so the
    /// aircraft's own display stops showing it as locked, while its map icon stays lit and the weapon list still
    /// holds it. The two then disagree about what is selected, and only clearing the list by hand puts them back
    /// together. Unit.UnitDisabled is the one edge every unit passes through, Building's override included, since
    /// that override calls base.
    /// </summary>
    [HarmonyPatch(typeof(Unit), nameof(Unit.UnitDisabled))]
    internal static class UnitUnitDisabledPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Unit __instance, bool newState)
        {
            if (newState)
            {
                SelectionApplier.Forget(__instance);
            }
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
