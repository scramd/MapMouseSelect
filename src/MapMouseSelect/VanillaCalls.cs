using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Wrappers over the two private vanilla methods the mod needs verbatim.
    ///
    /// Both of them own selection rules that would otherwise have to be copied here and kept in step
    /// with the game: DynamicMap.SelectFromMap picks the nearest unit icon to the cursor, and
    /// CombatHUD.TargetSelect ranks the HUD markers around the target designator. Each is resolved
    /// once and cached.
    ///
    /// AccessTools returns null instead of throwing when a member is missing, so a game update that
    /// renames either method costs a logged warning rather than a crash.
    /// </summary>
    internal static class VanillaCalls
    {
        private static MethodInfo _selectFromMap;
        private static MethodInfo _targetSelect;
        private static bool _selectFromMapResolved;
        private static bool _targetSelectResolved;

        /// <summary>Vanilla's own "shift is held" test, both keys, as UnitMapIcon.ClickIcon writes it.</summary>
        internal static bool ShiftHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        /// <summary>Vanilla's "select the unit the cursor is over", including the click it issues.</summary>
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
