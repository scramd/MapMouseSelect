using HarmonyLib;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Keeps destroyed buildings on the map as darkened wrecks.
    ///
    /// A building stays a live, repairable Unit after it dies, and UnitMapIcon_OnUnitDisabled removes its icon,
    /// so blocking that keeps the icon, its position and its scaling. A wreck the game never gave an icon gets
    /// one from DynamicMap.AddIcon. Whether a wreck can be selected is the Target List WRECK filter's business.
    /// UnitOnDestroyWreckPatch removes the icon when the unit itself goes.
    /// </summary>
    internal static class DestroyedBuildings
    {
        /// <summary>Whether this unit is a disabled building, which the mod draws as a wreck.</summary>
        internal static bool IsWreck(Unit unit)
        {
            return unit != null && unit is Building && unit.disabled;
        }

        /// <summary>Darkens the icon towards black by multiplying its faction colour, leaving alpha alone.</summary>
        internal static void ApplyTint(MapIcon icon)
        {
            if (!(icon is UnitMapIcon unitIcon) || unitIcon.iconImage == null || !IsWreck(unitIcon.unit))
            {
                return;
            }

            var tint = Mathf.Clamp(Plugin.DestroyedBuildingTint.Value, 0.1f, 1f);
            var color = unitIcon.iconImage.color;
            var tinted = new Color(color.r * tint, color.g * tint, color.b * tint, color.a);
            unitIcon.iconImage.color = tinted;

            var pane = SceneSingleton<TargetListSelector>.i;
            Plugin.Verbose(
                "Wreck " + unitIcon.unit.name + ": " + color + " -> " + tinted
                + ", filtered out=" + (pane != null && pane.CheckExclusions(unitIcon.unit))
                + ", clickable=" + unitIcon.iconImage.raycastTarget + ".");
        }

        /// <summary>
        /// Gives a wreck the icon the game refused it. SpawnQueuedIcons rejects a disabled unit, so a building that
        /// was already a wreck when the map first saw it ends up with no icon at all.
        /// </summary>
        internal static void EnsureIcon(DynamicMap map, PersistentID id)
        {
            if (!Plugin.EnableDestroyedUnits.Value || map == null)
            {
                return;
            }

            if (!UnitRegistry.TryGetUnit(new PersistentID?(id), out var unit) || !IsWreck(unit))
            {
                return;
            }

            // mapIconSize is the game's own mark for scenery that is not worth an icon, and a wreck does not change
            // that.
            if (unit.definition == null || unit.definition.mapIconSize <= 0f)
            {
                return;
            }

            if (DynamicMap.TryGetMapIcon(unit, out _))
            {
                return;
            }

            if (VanillaCalls.SpawnIconForUnit(map, unit))
            {
                Plugin.Verbose("Gave wrecked " + unit.name + " the map icon the game never made it.");
            }
        }

        /// <summary>Rebuilds one unit's icon colour, so a wreck darkens and a repair comes back.</summary>
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
    /// Stops the map icon of a destroyed building from being taken off the map. Vanilla's handler is the whole
    /// removal, so skipping it for a disabled Building leaves the icon in place.
    /// </summary>
    [HarmonyPatch(typeof(UnitMapIcon), "UnitMapIcon_OnUnitDisabled")]
    internal static class UnitMapIconKeepWreckPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(UnitMapIcon __instance, Unit unit)
        {
            if (!Plugin.EnableDestroyedUnits.Value || !DestroyedBuildings.IsWreck(unit))
            {
                return true;
            }

            DestroyedBuildings.ApplyTint(__instance);

            // Vanilla's handler is also the deselect. A building that dies while selected would stay marked as a
            // target with nothing able to clear it, so deselect explicitly.
            var map = SceneSingleton<DynamicMap>.i;
            if (map != null)
            {
                map.DeselectIcon(unit);
            }

            Plugin.Verbose("Kept the map icon of wrecked " + unit.name + " (" + unit.UniqueName + ").");
            return false;
        }
    }

    /// <summary>
    /// Recolours a wreck after every colour rebuild. Selection, faction changes, theme changes and the Target List
    /// filters all come through here.
    /// </summary>
    [HarmonyPatch(typeof(UnitMapIcon), "UnitMapIcon_UpdateColor")]
    internal static class UnitMapIconWreckTintPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UnitMapIcon __instance)
        {
            if (Plugin.EnableDestroyedUnits.Value)
            {
                DestroyedBuildings.ApplyTint(__instance);
            }
        }
    }

    /// <summary>
    /// Follows a building in and out of the wrecked state. A repair has no event of its own beyond the disabled
    /// flag clearing, so the icon would otherwise keep the wreck colour.
    /// </summary>
    [HarmonyPatch(typeof(Building), "UnitDisabled")]
    internal static class BuildingUnitDisabledPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Building __instance, bool oldState, bool newState)
        {
            Plugin.Verbose("Building " + __instance.name + ": disabled " + oldState + " -> " + newState + ".");

            if (Plugin.EnableDestroyedUnits.Value)
            {
                DestroyedBuildings.Refresh(__instance);
            }
        }
    }

    /// <summary>
    /// Removes a unit's map icon when the unit itself is destroyed, which vanilla does not cover. Unit.OnDestroy
    /// raises the disable event only for a unit that is not already disabled, so a wreck's icon is left in
    /// mapIcons behind a destroyed unit, out of reach of every deselect path, which all start from the unit.
    /// Aircraft and ships reach this through their own base call.
    /// </summary>
    [HarmonyPatch(typeof(Unit), "OnDestroy")]
    internal static class UnitOnDestroyWreckPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Unit __instance)
        {
            if (!Plugin.EnableDestroyedUnits.Value)
            {
                return;
            }

            var map = SceneSingleton<DynamicMap>.i;
            if (map == null || map.mapIcons == null)
            {
                return;
            }

            for (var i = map.mapIcons.Count - 1; i >= 0; i--)
            {
                if (!(map.mapIcons[i] is UnitMapIcon icon) || !ReferenceEquals(icon.unit, __instance))
                {
                    continue;
                }

                Plugin.Verbose("Removed the map icon of " + __instance.name + ", which is gone.");
                map.RemoveIcon(icon);
                icon.RemoveIcon();
            }
        }
    }

    /// <summary>
    /// Undoes the queue's refusal of a disabled unit, for wrecks only. AddIcon is the one door into the map's
    /// icons: UnitRegistry, FactionHQ's tracking updates, map generation and GetOrAddIcon all call it.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.AddIcon), new[] { typeof(PersistentID) })]
    internal static class DynamicMapAddIconWreckPatch
    {
        [HarmonyPostfix]
        private static void Postfix(DynamicMap __instance, PersistentID id)
        {
            DestroyedBuildings.EnsureIcon(__instance, id);
        }
    }
}
