using HarmonyLib;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Keeps destroyed buildings on the map instead of letting them vanish.
    ///
    /// A building is a Unit, and it stays one after it is destroyed: Building.UnitDisabled spawns the wreckage
    /// prefab and leaves the unit in place, disabled and repairable. What removes it from the map is
    /// UnitMapIcon.UnitMapIcon_OnUnitDisabled, which detaches the icon and destroys its object. Blocking that
    /// for a disabled Building is all it takes for the wreck to keep its icon, its position and its scaling.
    ///
    /// The icon is then darkened, so a wreck reads as a wreck rather than as a live building, and its raycast
    /// target is cleared, so it cannot be clicked or picked by the Select action. Nothing else about the wreck
    /// changes: it is still dimmed by the Target List filters, still hoverable for its tooltip, and repairing it
    /// puts the normal colour back, because every colour rebuild goes through the same patch.
    /// </summary>
    internal static class DestroyedBuildings
    {
        /// <summary>Whether this unit is a building that is out of action, which the mod draws as a wreck.</summary>
        internal static bool IsWreck(Unit unit)
        {
            return unit != null && unit is Building && unit.disabled;
        }

        /// <summary>
        /// The wreck colour: the faction colour the icon would have had, darkened towards black. Multiplying
        /// keeps whatever the game picked (neutral grey, friendly blue, hostile red) and leaves the icon's
        /// alpha alone, which is what the Target List filter dimming also does.
        /// </summary>
        internal static void ApplyTint(MapIcon icon)
        {
            if (!(icon is UnitMapIcon unitIcon) || unitIcon.iconImage == null || !IsWreck(unitIcon.unit))
            {
                return;
            }

            var tint = Mathf.Clamp(Plugin.DestroyedBuildingTint.Value, 0.1f, 1f);
            var color = unitIcon.iconImage.color;
            unitIcon.iconImage.color = new Color(color.r * tint, color.g * tint, color.b * tint, color.a);

            // A wreck is not a target: no click, no Select action pick, and no tooltip flicker from the pointer.
            unitIcon.iconImage.raycastTarget = false;
        }

        /// <summary>
        /// Rebuilds one unit's map icon colour, wreck or not, so that both directions land: a building that is
        /// destroyed darkens when the game disables it, and one that is repaired comes back.
        /// </summary>
        internal static void Refresh(Unit unit)
        {
            if (unit == null || SceneSingleton<DynamicMap>.i == null)
            {
                return;
            }

            if (DynamicMap.TryGetMapIcon(unit, out var icon) && icon is UnitMapIcon unitIcon)
            {
                unitIcon.UnitMapIcon_UpdateColor();
            }
        }
    }

    /// <summary>
    /// Stops the map icon of a destroyed building from being taken off the map.
    ///
    /// Vanilla's handler is the whole removal: it detaches the icon from DynamicMap and destroys the object.
    /// Skipping it for a disabled Building leaves the icon exactly where it was, which is what makes the wreck
    /// visible. Its own icon removal still runs for everything else, and ClearMap at the end of a mission still
    /// takes every icon with it.
    /// </summary>
    [HarmonyPatch(typeof(UnitMapIcon), "UnitMapIcon_OnUnitDisabled")]
    internal static class UnitMapIconKeepWreckPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(UnitMapIcon __instance, Unit unit)
        {
            if (!Plugin.ShowDestroyedBuildings.Value || !DestroyedBuildings.IsWreck(unit))
            {
                return true;
            }

            DestroyedBuildings.ApplyTint(__instance);
            Plugin.Verbose("Kept the map icon of wrecked " + unit.name + " (" + unit.UniqueName + ").");
            return false;
        }
    }

    /// <summary>
    /// Recolours a wreck after every colour rebuild, which is the only thing that can undo the darkening:
    /// selection, faction changes, theme changes and the Target List filters all come through here.
    /// </summary>
    [HarmonyPatch(typeof(UnitMapIcon), "UnitMapIcon_UpdateColor")]
    internal static class UnitMapIconWreckTintPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UnitMapIcon __instance)
        {
            if (Plugin.ShowDestroyedBuildings.Value)
            {
                DestroyedBuildings.ApplyTint(__instance);
            }
        }
    }

    /// <summary>
    /// Follows a building in and out of the wrecked state. Destruction is handled by the icon removal patch
    /// above, but a repair has no such event: the unit simply stops being disabled, and the icon would keep the
    /// wreck colour until something else happened to rebuild it.
    /// </summary>
    [HarmonyPatch(typeof(Building), "UnitDisabled")]
    internal static class BuildingUnitDisabledPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Building __instance)
        {
            DestroyedBuildings.Refresh(__instance);
        }
    }
}
