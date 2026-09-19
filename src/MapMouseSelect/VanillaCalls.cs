using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Wrappers over the private vanilla members the mod needs verbatim.
    ///
    /// They own rules that would otherwise have to be copied here and kept in step with the game:
    /// DynamicMap.SelectFromMap is the map half of the Select action's handler, taking the icon with the
    /// smallest squared distance to the cursor among those its own filters allow, and DynamicMap.JumpCameraTo
    /// is the commander's camera jump. Each is resolved once and cached.
    ///
    /// Select is the keyboard and gamepad action, not the mouse: a mouse click reaches a map icon through
    /// its EventSystem handler instead, so this is not the call that reproduces one.
    ///
    /// AccessTools returns null instead of throwing when a member is missing, so a game update that
    /// renames any of them costs a logged warning rather than a crash.
    /// </summary>
    internal static class VanillaCalls
    {
        private static MethodInfo _selectFromMap;
        private static MethodInfo _jumpCameraTo;
        private static MethodInfo _spawnIconForUnit;
        private static FieldInfo _selectSound;
        private static FieldInfo _deselectSound;
        private static FieldInfo _markers;
        private static bool _selectFromMapResolved;
        private static bool _jumpCameraToResolved;
        private static bool _spawnIconForUnitResolved;
        private static bool _selectSoundResolved;
        private static bool _deselectSoundResolved;
        private static bool _markersResolved;

        /// <summary>Vanilla's own "shift is held" test, both keys, as UnitMapIcon.ClickIcon writes it.</summary>
        internal static bool ShiftHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        /// <summary>Vanilla's map selection for the Select action, including the click it issues.</summary>
        internal static void SelectFromMap(DynamicMap map)
        {
            if (!_selectFromMapResolved)
            {
                _selectFromMapResolved = true;
                _selectFromMap = Resolve(typeof(DynamicMap), "SelectFromMap", null);
            }

            Invoke(_selectFromMap, map);
        }

        /// <summary>
        /// Every HUD marker on screen. CombatHUD keeps them in a field rather than an accessor, so this is the
        /// one thing here that reads state rather than calling vanilla: the target designator's pick needs to
        /// walk them to find what the player is pointing at when the map is closed.
        /// </summary>
        internal static List<HUDUnitMarker> Markers(CombatHUD hud)
        {
            if (hud == null)
            {
                return null;
            }

            if (!_markersResolved)
            {
                _markersResolved = true;
                _markers = AccessTools.Field(typeof(CombatHUD), "markers");
                if (_markers == null)
                {
                    Plugin.Log.LogWarning(
                        "Could not find CombatHUD.markers, so the select-only key will not see the target "
                        + "designator. The game may have changed.");
                }
            }

            return _markers != null ? _markers.GetValue(hud) as List<HUDUnitMarker> : null;
        }

        /// <summary>
        /// Vanilla's icon creation for a single unit, which its own code only reaches through the queue.
        ///
        /// The queue refuses a unit that is disabled, which is right for a corpse and wrong for a wreck this mod
        /// keeps on the map, so the wreck path calls this directly instead.
        /// </summary>
        internal static bool SpawnIconForUnit(DynamicMap map, Unit unit)
        {
            if (!_spawnIconForUnitResolved)
            {
                _spawnIconForUnitResolved = true;
                _spawnIconForUnit = Resolve(typeof(DynamicMap), "SpawnIconForUnit", new[] { typeof(Unit) });
            }

            if (_spawnIconForUnit == null)
            {
                return false;
            }

            Invoke(_spawnIconForUnit, map, unit);
            return true;
        }

        /// <summary>Vanilla's commander camera jump, the call its own Jump Map branch makes.</summary>
        internal static bool JumpCameraTo(DynamicMap map, GlobalPosition position)
        {
            if (!_jumpCameraToResolved)
            {
                _jumpCameraToResolved = true;
                _jumpCameraTo = Resolve(typeof(DynamicMap), "JumpCameraTo", new[] { typeof(GlobalPosition) });
            }

            if (_jumpCameraTo == null)
            {
                return false;
            }

            Invoke(_jumpCameraTo, map, position);
            return true;
        }

        /// <summary>
        /// Plays the clip CombatHUD uses for a selection. The mod plays it once for a whole gesture, which is
        /// what CombatHUD's own multi-select does: TargetSelect adds every marker in range, then plays this once
        /// and refreshes the target list once, rather than doing either per unit.
        /// </summary>
        internal static void PlaySelectSound(CombatHUD hud)
        {
            PlaySound(hud, ref _selectSound, ref _selectSoundResolved, "selectSound");
        }

        /// <summary>The same for a removal, which CombatHUD.DeSelectUnit plays for a single unit.</summary>
        internal static void PlayDeselectSound(CombatHUD hud)
        {
            PlaySound(hud, ref _deselectSound, ref _deselectSoundResolved, "deselectSound");
        }

        /// <summary>
        /// Both clips are private serialized fields on CombatHUD, so they are reached the same way the private
        /// methods above are, and each is looked up once.
        /// </summary>
        private static void PlaySound(CombatHUD hud, ref FieldInfo field, ref bool resolved, string name)
        {
            if (hud == null || SoundManager.i == null)
            {
                return;
            }

            if (!resolved)
            {
                resolved = true;
                field = AccessTools.Field(typeof(CombatHUD), name);
                if (field == null)
                {
                    Plugin.Log.LogWarning(
                        "Could not find CombatHUD." + name + ", so a marquee will be silent for it. "
                        + "The game may have changed.");
                }
            }

            var clip = field != null ? field.GetValue(hud) as AudioClip : null;
            if (clip != null)
            {
                SoundManager.PlayInterfaceOneShot(clip);
            }
        }

        private static MethodInfo Resolve(Type type, string name, Type[] parameters)
        {
            var method = AccessTools.Method(type, name, parameters);
            if (method == null)
            {
                Plugin.Log.LogWarning(
                    "Could not find " + type.Name + "." + name + ", so the action that uses it is "
                    + "disabled. The game may have changed.");
            }
            return method;
        }

        private static void Invoke(MethodInfo method, object instance, params object[] arguments)
        {
            if (method != null)
            {
                method.Invoke(instance, arguments);
            }
        }
    }
}
