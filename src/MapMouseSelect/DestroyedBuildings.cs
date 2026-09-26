using HarmonyLib;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Keeps destroyed buildings on the map instead of letting them vanish.
    ///
    /// A building is a Unit, and it stays one after it is destroyed. Building.UnitDisabled spawns the wreckage
    /// prefab and leaves the unit in place, disabled and repairable. UnitMapIcon.UnitMapIcon_OnUnitDisabled
    /// removes it from the map: it detaches the icon and destroys its object. Blocking that for a disabled
    /// Building is all it takes for the wreck to keep its icon, its position and its scaling. A wreck the game
    /// never gave an icon to gets one from DynamicMap.AddIcon instead.
    ///
    /// The icon is then darkened, so a wreck reads as a wreck, not as a live building. Nothing else about it
    /// changes. It stays hoverable for its tooltip. Whether it can be selected at all is the Target List pane's
    /// business, through the WRECK filter this mod adds, which is off by default. That filter is the same gate
    /// vanilla's own clicks, the Select action and the marquee pass through. An unlit WRECK button therefore makes
    /// wrecks unselectable, without any click handling of this mod's own.
    ///
    /// The icon's life is the unit's life. It is kept while the disabled building exists, which is where vanilla
    /// would have dropped it, and removed when the unit itself is destroyed. Vanilla never has to handle the
    /// second case, because its icon is long gone by then. UnitOnDestroyWreckPatch is that second half.
    /// </summary>
    internal static class DestroyedBuildings
    {
        /// <summary>Whether this unit is a building that is out of action, which the mod draws as a wreck.</summary>
        internal static bool IsWreck(Unit unit)
        {
            return unit != null && unit is Building && unit.disabled;
        }

        /// <summary>
        /// The wreck colour: the faction colour the icon would have had, darkened towards black. Multiplication
        /// keeps whatever the game picked (neutral grey, friendly blue, hostile red) and leaves the icon's alpha
        /// alone. The Target List filter dimming also does this.
        /// </summary>
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
        /// Makes sure a wrecked building the game has decided to put on the map actually gets there.
        ///
        /// SpawnQueuedIcons refuses a disabled unit. A building that was already a wreck when the game first
        /// mapped it therefore ends up with no icon at all. It can be a wreck at that moment because it died
        /// before its first icon, or because this client generated its map only afterwards. Every path that
        /// decides a unit belongs on the map asks for one through DynamicMap.AddIcon. That is where the refusal is
        /// undone, and only for a wreck this mod is drawing.
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

            // mapIconSize is the game's own way of saying a unit is not worth an icon, and a wreck does not change
            // that: scenery stays scenery.
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
    /// visible. The handler's own icon removal still runs for everything else, and ClearMap at the end of a
    /// mission still takes every icon with it.
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

            // Vanilla's handler is also the deselect: it takes the icon out of the map's selection and destroys its
            // target marker. Keeping the icon must not keep that. A building that dies while selected would
            // otherwise stay marked as a target, with nothing able to clear it. Every deselect path starts from an
            // icon whose unit is still there. The deselect itself rebuilds the colour, which lands on the same tint
            // through the postfix on MapIcon.UpdateColor.
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
    /// Recolours a wreck after every colour rebuild, the only thing that can undo the darkening. Selection,
    /// faction changes, theme changes and the Target List filters all come through here.
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
    /// Follows a building in and out of the wrecked state. The icon removal patch above handles destruction.
    /// A repair has no such event: the unit simply stops being disabled, and the icon would keep the wreck
    /// colour until something else rebuilt it.
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
    /// Removes a unit's map icon when the unit itself is destroyed.
    ///
    /// Vanilla removes the icon when the unit is disabled. For an aircraft or a ship that is the same moment as
    /// the unit being gone. A building is the one case where the wreck outlives that moment, and blocking the
    /// removal is the whole feature. The icon must therefore be taken away when the object really goes. Vanilla
    /// does not cover it and cannot. Unit.OnDestroy raises the disable event only for a unit that is not disabled.
    /// A wreck's icon is therefore left in mapIcons, with a destroyed unit behind it. That icon draws a colour
    /// picked for a unit that no longer exists. It is out of reach of every deselect path, because they all start
    /// from the unit, and it hands a destroyed unit to the tooltip.
    ///
    /// Every unit passes through Unit.OnDestroy: buildings directly, and aircraft, ships and vehicles through
    /// their own override that calls base. One postfix here therefore covers all of them. The icon is matched by
    /// reference, not through the map's lookup. The lookup is keyed by the unit, and this runs while that unit is
    /// being destroyed.
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
    /// Undoes the queue's refusal of a disabled unit, for wrecks only.
    ///
    /// AddIcon is the one door into the map's icons: UnitRegistry, FactionHQ's tracking updates, map generation
    /// and GetOrAddIcon all call it. The icon a wreck should have is therefore created here, no matter which of
    /// them decided the unit belongs on the map. The colour patch above tints the icon like any other.
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
