using System;
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
    /// smallest squared distance to the cursor among those its own filters allow; CombatHUD.TargetSelect
    /// ranks the HUD markers around the target designator; and DynamicMap.JumpCameraTo is the commander's
    /// camera jump. Each is resolved once and cached.
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
        private static MethodInfo _targetSelect;
        private static MethodInfo _jumpCameraTo;
        private static FieldInfo _selectSound;
        private static bool _selectFromMapResolved;
        private static bool _targetSelectResolved;
        private static bool _jumpCameraToResolved;
        private static bool _selectSoundResolved;

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

        /// <summary>Vanilla's "select what the designator is pointing at". paint adds every marker in range.</summary>
        internal static void TargetSelect(CombatHUD hud, bool paint)
        {
            if (!_targetSelectResolved)
            {
                _targetSelectResolved = true;
                _targetSelect = Resolve(typeof(CombatHUD), "TargetSelect", new[] { typeof(bool) });
            }

            Invoke(_targetSelect, hud, paint);
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
        /// Plays the clip CombatHUD uses for a selection. The marquee plays it once for the whole box, which
        /// is what CombatHUD's own multi-select does: TargetSelect adds every marker in range, then plays this
        /// once and refreshes the target list once, rather than doing either per unit. The clip is a private
        /// serialized field, so it is reached the same way the private methods above are.
        /// </summary>
        internal static void PlaySelectSound(CombatHUD hud)
        {
            if (hud == null || SoundManager.i == null)
            {
                return;
            }

            if (!_selectSoundResolved)
            {
                _selectSoundResolved = true;
                _selectSound = AccessTools.Field(typeof(CombatHUD), "selectSound");
                if (_selectSound == null)
                {
                    Plugin.Log.LogWarning(
                        "Could not find CombatHUD.selectSound, so a marquee will be silent. The game may have changed.");
                }
            }

            var clip = _selectSound != null ? _selectSound.GetValue(hud) as AudioClip : null;
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
