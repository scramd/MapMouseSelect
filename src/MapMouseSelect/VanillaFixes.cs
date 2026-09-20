using System;
using HarmonyLib;

namespace MapMouseSelect
{
    /// <summary>
    /// Three vanilla paths that assume every unit they are handed has a map icon, and throw when it does not.
    ///
    /// DynamicMap.GetOrAddIcon queues an icon for a unit that has none and then returns iconLookup[unit]. Nothing
    /// creates an icon for a unit that is disabled, or for one whose definition asks for none, so the lookup
    /// throws KeyNotFoundException. Its only caller, FlagIncomingMissile, dereferences the result on the next
    /// line, which turns the incoming missile HUD warning into an exception instead of a warning about a unit
    /// that no longer needs one. CombatHUD.HighlightMarker has the same shape against markerLookup, reached from
    /// a radar warning.
    ///
    /// All three are patched to the rule the icon spawner already uses: a live unit whose definition asks for an
    /// icon can have one, and anything else has nothing to draw.
    /// </summary>
    internal static class VanillaFixes
    {
        /// <summary>Whether an icon can exist for this unit at all, which is what the queue tests.</summary>
        internal static bool CanHaveIcon(Unit unit)
        {
            return unit != null
                && !unit.disabled
                && unit.definition != null
                && unit.definition.mapIconSize > 0f;
        }
    }

    /// <summary>
    /// Fires the event DynamicMap.DeselectAllIcons forgets, so the Target List panel drops its rows with the selection.
    ///
    /// That method is a hand-rolled clear: it deselects every icon by hand, empties selectedIcons, and fires neither
    /// onUnitDeselected nor onAllDeselected. TargetListSelector builds its rows from those two events and nothing else,
    /// so every row survives a clear that way, and what the panel is left showing is a target that nothing holds: no
    /// lit icon on the map, no entry in the weapon list, and no way to tell it apart from a real one except that
    /// clicking it does nothing. Unselect all is the gesture a player reaches for, and that is CombatHUD.DeselectAll
    /// going through this very method, so the clear leaves the row where it was, and the row outlives the selection by
    /// as long as the player puts up with it.
    ///
    /// Vanilla reaches this by playing normally: Aircraft.OnStartClient, which is every respawn,
    /// AircraftSelectionMenu.OnEnable, which is the spawn screen, and AirbaseMapIcon.ClickIcon. The row is left behind
    /// for any unit, a wreck or not.
    ///
    /// Calling the event rather than the panel keeps every other subscriber of a clear in the loop, which is what the
    /// game meant to do. The count in the log comes from a prefix because the body has already emptied the list by the
    /// time a postfix runs.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.DeselectAllIcons))]
    internal static class DynamicMapDeselectAllIconsPatch
    {
        private static readonly System.Reflection.FieldInfo AllDeselected =
            AccessTools.Field(typeof(DynamicMap), "onAllDeselected");

        private static int _icons;

        [HarmonyPrefix]
        private static void Prefix(DynamicMap __instance)
        {
            var selected = __instance.selectedIcons;
            _icons = selected != null ? selected.Count : 0;
        }

        [HarmonyPostfix]
        private static void Postfix(DynamicMap __instance)
        {
            if (_icons == 0)
            {
                return;
            }

            Plugin.Verbose(
                "Dropped the Target List rows with the selection: DeselectAllIcons cleared " + _icons
                + " selected icon(s) without firing onAllDeselected.");

            var cleared = AllDeselected != null ? AllDeselected.GetValue(__instance) as Action : null;
            if (cleared != null)
            {
                cleared();
            }
        }
    }

    /// <summary>
    /// Returns no icon instead of throwing when the unit cannot have one. Callers that need an icon still get
    /// the cached one or a freshly created one, exactly as before.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.GetOrAddIcon))]
    internal static class DynamicMapGetOrAddIconPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(DynamicMap __instance, Unit unit, ref UnitMapIcon __result)
        {
            if (unit == null || DynamicMap.TryGetMapIcon(unit, out _) || VanillaFixes.CanHaveIcon(unit))
            {
                return true;
            }

            // Vanilla would queue the icon, get nothing back, and throw on iconLookup[unit].
            __result = null;
            Plugin.Verbose("No map icon for " + unit.name + ", which cannot have one.");
            return false;
        }
    }

    /// <summary>
    /// Skips the incoming missile warning when there is no icon to hang it on.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.FlagIncomingMissile))]
    internal static class DynamicMapFlagIncomingMissilePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Unit missileUnit)
        {
            if (missileUnit == null)
            {
                return false;
            }

            if (DynamicMap.TryGetMapIcon(missileUnit, out _) || VanillaFixes.CanHaveIcon(missileUnit))
            {
                return true;
            }

            Plugin.Verbose("Skipped the incoming missile flag for " + missileUnit.name + ": no map icon for it.");
            return false;
        }
    }

    /// <summary>
    /// Skips a radar warning highlight for a unit that has no HUD marker to highlight.
    ///
    /// CombatHUD.CreateMarker decides once, when a unit is first mapped, whether it gets a marker, and it refuses
    /// a unit that is disabled, that is Scenery, or that turns up while the player has no aircraft. HighlightMarker
    /// then indexes markerLookup directly, so a radar warning about one of those units throws instead of just not
    /// drawing anything. A radar contact that was already wrecked by the time it was detected is the everyday case:
    /// no marker is created, and the warning for it would be the crash.
    /// </summary>
    [HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.HighlightMarker))]
    internal static class CombatHudHighlightMarkerPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CombatHUD __instance, Unit unit)
        {
            if (unit != null && !__instance.TryGetMarker(unit, out _))
            {
                Plugin.Verbose("Skipped the highlight for " + unit.name + ": it has no HUD marker.");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Releases a unit that the weapon target list has nothing to say about.
    ///
    /// CombatHUD.DeSelectUnit starts by returning when the target list is empty, and that return skips the rest of
    /// the method, the map icon included. An empty list is exactly the case this mod walks into: a unit selected in
    /// commander view keeps its selection into the cockpit with no list entry, and a wreck can never be a weapon
    /// target at all, so it is never in the list whatever selected it. Every deselect path that routes through here
    /// then does nothing, and they are the ones a player reaches: TargetListSelector.ForceDeselect, which is the
    /// pane's own exclusion pass, and TargetListSelector_UnitItem.ToggleSelect, which is the row in that panel.
    /// Unselect all was the only thing left that could clear the selection.
    ///
    /// Where vanilla would give up, the map half is done instead. DynamicMap.DeselectIcon is where the rest of the
    /// method ends anyway, and it is silent, which is what a release with no list entry should be.
    /// </summary>
    [HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.DeSelectUnit))]
    internal static class CombatHudDeSelectUnitPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CombatHUD __instance, Unit unit)
        {
            if (unit == null)
            {
                return true;
            }

            var targets = __instance.GetTargetList();
            if (targets == null || targets.Count > 0)
            {
                return true;
            }

            var map = SceneSingleton<DynamicMap>.i;
            if (map == null || !DynamicMap.TryGetMapIcon(unit, out _))
            {
                return false;
            }

            Plugin.Verbose("Released " + unit.name + " from the map selection: the weapon list has nothing to drop.");
            map.DeselectIcon(unit);
            return false;
        }
    }

    /// <summary>
    /// Makes every colour rebuild go through the unit icon's own update, which is the only one that knows about the
    /// Target List filters and about wrecks.
    ///
    /// MapIcon.UpdateColor sets the colour from the theme and stops there. The filter dimming, and this mod's wreck
    /// tint, live one level down in UnitMapIcon.UnitMapIcon_UpdateColor, so everything that calls the base method
    /// directly draws a filtered out unit, or a wreck, as if it were still alive: MapIcon.SelectIcon and
    /// MapIcon.DeselectIcon do, DynamicMap.UnselectAll calls DeselectIcon on every icon on the map whether it was
    /// selected or not, and DynamicMap.UpdateUnitFaction and DynamicMap_OnFactionChanged call UpdateColor themselves.
    ///
    /// The depth counter is what keeps this from compounding: UnitMapIcon_UpdateColor calls the base method itself,
    /// so without it the postfix would run the update again inside its own call and apply the tint twice. A finalizer
    /// rather than a postfix, so the counter cannot be left raised by an exception and silently disable the fix.
    /// </summary>
    [HarmonyPatch(typeof(UnitMapIcon), "UnitMapIcon_UpdateColor")]
    internal static class UnitMapIconDepthPatch
    {
        internal static int Depth;

        [HarmonyPrefix]
        private static void Prefix()
        {
            Depth++;
        }

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            Depth--;
        }
    }

    /// <summary>
    /// Sends a colour rebuild that bypassed the unit icon's update back through it.
    /// </summary>
    [HarmonyPatch(typeof(MapIcon), nameof(MapIcon.UpdateColor))]
    internal static class MapIconUpdateColorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MapIcon __instance)
        {
            if (UnitMapIconDepthPatch.Depth == 0 && __instance is UnitMapIcon icon)
            {
                icon.UnitMapIcon_UpdateColor();
            }
        }
    }
}
