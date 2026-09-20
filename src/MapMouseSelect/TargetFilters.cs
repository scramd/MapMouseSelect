using System.Collections.Generic;
using System.Reflection;
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
        /// <summary>Prefix of every button this mod adds, so its own copies can be told from the pane's.</summary>
        private const string ClonePrefix = "MapMouseSelect_";

        private static TargetListSelector_ToggleButton _neutral;
        private static TargetListSelector_ToggleButton _wreck;
        private static FieldInfo _context;
        private static MethodInfo _themeChanged;

        internal static void Add(TargetListSelector selector)
        {
            if (selector == null || selector.toggleFactionItems == null || selector.toggleFactionItems.Count == 0)
            {
                return;
            }

            // The row has no layout group: the pane places its filter buttons by hand, so a copy keeps the anchored
            // position it was taken from and lands on top of the button it came from. Ours take the slot after the
            // faction buttons, one half of a slot each, and both the slot and the half are measured from the
            // faction buttons themselves rather than from the row, which holds buttons of other widths too.
            var items = selector.toggleFactionItems;
            var template = items[0];
            var last = items[items.Count - 1];
            var lastRect = (RectTransform)last.transform;

            var pitch = FactionPitch(items);
            var gap = 2f;
            var half = (pitch - gap) * 0.5f;
            var slot = lastRect.anchoredPosition + new Vector2(pitch, 0f);

            if (Plugin.FilterNeutral.Value && _neutral == null)
            {
                _neutral = Clone(template, last, "NTL", true, slot, half);
                slot.x += half + gap;
            }

            // Wrecks start filtered out. Nothing about a wreck is worth selecting by default, and an unlit
            // filter is exactly what stops it being selectable, through the same gate every vanilla selection
            // path already uses. Lighting the button is how a player asks to select wrecks on purpose.
            if (Plugin.FilterWrecks.Value && _wreck == null)
            {
                _wreck = Clone(template, last, "DED", false, slot, half);
            }

            Plugin.Verbose(
                "Target List filters: " + Describe(_neutral) + ", " + Describe(_wreck)
                + ", pitch=" + pitch + ", half=" + half + ".");
        }

        private static string Describe(TargetListSelector_ToggleButton button)
        {
            if (button == null)
            {
                return "absent";
            }

            var rect = (RectTransform)button.transform;
            return button.label.text + (button.status ? " lit at " : " unlit at ") + rect.anchoredPosition;
        }

        /// <summary>
        /// The distance between two of the row's faction buttons, which is what one slot is worth. Measuring the
        /// faction buttons rather than the row's last two children keeps this right when the row also holds
        /// buttons of other sizes.
        /// </summary>
        private static float FactionPitch(List<TargetListSelector_ToggleButton> items)
        {
            if (items.Count >= 2)
            {
                var first = (RectTransform)items[0].transform;
                var last = (RectTransform)items[items.Count - 1].transform;
                var span = last.anchoredPosition.x - first.anchoredPosition.x;
                if (span > 1f)
                {
                    return span / (items.Count - 1);
                }
            }

            return ((RectTransform)items[0].transform).rect.width + 4f;
        }

        /// <summary>
        /// Copies one of the pane's own faction buttons, sized to the slot it shares and placed where the row's own
        /// anchoring puts it. Its theme context is switched to neutral, which is the theme group the pane's type
        /// filters use: lit buttons come out green rather than in a faction colour.
        /// </summary>
        private static TargetListSelector_ToggleButton Clone(
            TargetListSelector_ToggleButton template,
            TargetListSelector_ToggleButton anchor,
            string text,
            bool lit,
            Vector2 position,
            float width)
        {
            var copy = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            copy.name = ClonePrefix + text;
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

            var rect = (RectTransform)copy.transform;
            var anchorRect = (RectTransform)anchor.transform;
            rect.anchorMin = anchorRect.anchorMin;
            rect.anchorMax = anchorRect.anchorMax;
            rect.pivot = anchorRect.pivot;
            rect.sizeDelta = new Vector2(width, anchorRect.sizeDelta.y);
            rect.anchoredPosition = position;

            UseNeutralTheme(button);
            button.Set(lit);
            return button;
        }

        /// <summary>
        /// Moves a copied button into the theme group the pane's type filters use. The context is a private
        /// serialized field, and it is what decides the lit colour, so the theme handler has to run again for the
        /// change to show.
        /// </summary>
        private static void UseNeutralTheme(TargetListSelector_ToggleButton button)
        {
            if (_context == null)
            {
                _context = AccessTools.Field(typeof(TargetListSelector_ToggleButton), "context");
                _themeChanged = AccessTools.Method(
                    typeof(TargetListSelector_ToggleButton), "TargetListSelector_ToggleButton_OnThemeGroupChanged");

                if (_context == null || _themeChanged == null)
                {
                    Plugin.Log.LogWarning(
                        "Could not find the Target List button theme members, so the new filters keep the "
                        + "faction colour they were copied from.");
                    return;
                }
            }

            _context.SetValue(button, TargetListSelector_ToggleButton.ButtonContext.NEUTRAL);
            _themeChanged.Invoke(button, null);
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
