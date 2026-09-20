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
    /// Puts the filter dimming back after an icon is deselected.
    ///
    /// DynamicMap.UnselectAll deselects every icon in mapIcons, not only the selected ones, and MapIcon.DeselectIcon
    /// rebuilds an icon's colour through MapIcon.UpdateColor, which knows nothing about the target list filters. The
    /// dimming lives one level down, in UnitMapIcon.UnitMapIcon_UpdateColor, and the only thing that runs that for
    /// every icon is the pane's own exclusion pass, which happens when a filter is toggled. So any click that ends in
    /// UnselectAll, or any bulk deselect, leaves the filtered out icons drawn at full brightness until the player
    /// touches a filter. Vanilla does this too; batched deselection just makes it happen far more often.
    ///
    /// Re-running the update after the deselect restores what the icon should look like, whichever deselect did it.
    /// </summary>
    [HarmonyPatch(typeof(MapIcon), nameof(MapIcon.DeselectIcon))]
    internal static class MapIconDeselectIconPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MapIcon __instance)
        {
            if (__instance is UnitMapIcon icon)
            {
                icon.UnitMapIcon_UpdateColor();
            }
        }
    }
}
