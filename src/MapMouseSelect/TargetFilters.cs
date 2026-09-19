using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Two extra groups for the game's Target List filter pane: wrecks and neutrals.
    ///
    /// The pane keeps its filters in three lists the selector walks in CheckExclusions, and the classes behind
    /// them only model what vanilla needed: the faction button carries a same-faction flag rather than a faction,
    /// so neutral units (neither friend nor foe) can never be filtered, and nothing at all knows about destroyed
    /// buildings, which the game simply removes from the map. A destroyed building is still a live, repairable
    /// Unit, so once this mod keeps its icon on the map it also belongs in that pane.
    ///
    /// A button added to one of those lists would either never match anything or break the pane: SetOnlyItem
    /// indexes listDefinitions[0] when it is handed a vehicle button, so an entry with no definitions would throw.
    /// These two buttons therefore live outside the lists, are read by the exclusion patch below, and keep the
    /// pane's own behaviour untouched: clicking toggles them, the pane's Reset button turns them back on, and a
    /// toggle asks the selector to re-check its icons exactly like a vanilla button does.
    ///
    /// The polarity is vanilla's: lit means the group is shown, unlit means it is filtered out.
    /// </summary>
    internal static class TargetFilters
    {
        private static TargetListSelector_ToggleButton _neutral;
        private static TargetListSelector_ToggleButton _wreck;

        internal static void Add(TargetListSelector selector)
        {
            if (selector == null || selector.toggleFactionItems == null || selector.toggleFactionItems.Count == 0)
            {
                return;
            }

            // Destroyed objects compare equal to null, so a scene change clears these by itself.
            if (Plugin.FilterNeutral.Value && _neutral == null)
            {
                _neutral = Clone(selector.toggleFactionItems[0], "NEUTRAL", true);
            }

            // Wrecks start filtered out. Nothing about a wreck is worth selecting by default, and an unlit
            // filter is exactly what stops it being selectable, through the same gate every vanilla selection
            // path already uses. Lighting the button is how a player asks to select wrecks on purpose.
            if (Plugin.FilterWrecks.Value && _wreck == null)
            {
                _wreck = Clone(selector.toggleFactionItems[0], "WRECK", false);
            }
        }

        /// <summary>
        /// Copies one of the pane's own faction buttons. Copying keeps the sizing, the label and the theme
        /// colours the pane already uses, so the two new buttons sit in the row looking like the rest of it.
        /// </summary>
        private static TargetListSelector_ToggleButton Clone(
            TargetListSelector_ToggleButton template, string text, bool lit)
        {
            var copy = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            copy.name = "MapMouseSelect_" + text;
            copy.transform.SetAsLastSibling();

            var button = copy.GetComponent<TargetListSelector_ToggleButton>();
            if (button == null)
            {
                Plugin.Log.LogWarning("Could not copy a Target List filter button for " + text + ".");
                return null;
            }

            if (button.label != null)
            {
                button.label.text = text;
            }

            button.Set(lit);
            return button;
        }

        /// <summary>
        /// The mod's half of the pane's exclusions, added to whatever the pane itself decided.
        /// </summary>
        internal static bool Excludes(Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (_wreck != null && !_wreck.status && DestroyedBuildings.IsWreck(unit))
            {
                return true;
            }

            return _neutral != null && !_neutral.status && IsNeutral(unit);
        }

        /// <summary>
        /// Neutral is what the map treats as its default case: neither your faction nor a hostile one. Spectator
        /// mode shows every faction in its own colour, so those units are not neutrals either.
        /// </summary>
        private static bool IsNeutral(Unit unit)
        {
            var mode = DynamicMap.GetFactionMode(unit.NetworkHQ, false);
            return mode != FactionMode.Friendly && mode != FactionMode.Enemy && mode != FactionMode.Spectator;
        }
    }

    /// <summary>
    /// Adds the two buttons once the pane has built itself. Start is what fills the vehicle type row, and the
    /// faction and unit type buttons are already on the prefab, so a postfix here is the first moment the row
    /// exists and is safe to copy from.
    /// </summary>
    [HarmonyPatch(typeof(TargetListSelector), "Start")]
    internal static class TargetListSelectorStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TargetListSelector __instance)
        {
            TargetFilters.Add(__instance);
        }
    }

    /// <summary>
    /// Lets the two extra groups filter units the way the pane's own groups do. Everything that reads this
    /// method gets it: map clicks, the Select action, the marquee, and CheckAllExclusions, which deselects
    /// anything a filter now excludes and rebuilds every icon colour.
    /// </summary>
    [HarmonyPatch(typeof(TargetListSelector), nameof(TargetListSelector.CheckExclusions))]
    internal static class TargetListSelectorCheckExclusionsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Unit u, ref bool __result)
        {
            if (!__result)
            {
                __result = TargetFilters.Excludes(u);
            }
        }
    }
}
