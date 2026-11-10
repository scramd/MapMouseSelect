using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using Rewired;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Keeps the mouse wheel from zooming the camera while the wheel belongs to something on screen: the expanded
    /// map, a menu or dialog with a free cursor, or the BepInEx ConfigurationManager window (F1).
    ///
    /// "Zoom View" is one wheel shared by the map zoom and the camera zoom. The chase and orbit cameras already ask
    /// Cursor.visible before they read it. The cockpit camera asks only whether the map is expanded, and the flyby
    /// camera asks nothing, so a scroll over the map also changes its field of view. The patch puts the same test
    /// behind those two reads. The read itself stays, so nothing else in either method changes.
    /// </summary>
    [HarmonyPatch]
    internal static class CameraWheelPatch
    {
        private const string ConfigurationManagerGuid = "com.bepis.bepinex.configurationmanager";

        private static BaseUnityPlugin _configurationManager;
        private static PropertyInfo _displayingWindow;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in new[] { typeof(CameraCockpitState), typeof(CameraTVState) })
            {
                var method = AccessTools.Method(type, "UpdateState");
                if (method == null)
                {
                    Plugin.Log.LogWarning(
                        "Could not find " + type.Name + ".UpdateState, so the wheel will go on zooming that camera.");
                    continue;
                }

                yield return method;
            }
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var getAxis = AccessTools.Method(typeof(Player), nameof(Player.GetAxis), new[] { typeof(string) });
            var mask = AccessTools.Method(typeof(CameraWheelPatch), nameof(Mask));

            var list = new List<CodeInstruction>(instructions);
            var masked = 0;

            for (var i = 0; i + 1 < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Ldstr
                    && list[i].operand as string == "Zoom View"
                    && getAxis != null
                    && list[i + 1].Calls(getAxis))
                {
                    list.Insert(i + 2, new CodeInstruction(OpCodes.Call, mask));
                    masked++;
                    i += 2;
                }
            }

            if (masked != 1)
            {
                Plugin.Log.LogWarning(
                    "Expected exactly one Zoom View read in " + original.DeclaringType.Name + ".UpdateState but found "
                    + masked + ". The game may have changed.");
            }

            return list;
        }

        /// <summary>The wheel value the camera may use: zero while the wheel belongs to the map or a window.</summary>
        internal static float Mask(float value)
        {
            if (value == 0f || !Plugin.CameraWheelFix.Value)
            {
                return value;
            }

            return DynamicMap.mapMaximized || Cursor.visible || ConfigurationWindowOpen() ? 0f : value;
        }

        /// <summary>Whether the ConfigurationManager window is showing. False when that plugin is not installed.</summary>
        private static bool ConfigurationWindowOpen()
        {
            if (_configurationManager == null)
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(ConfigurationManagerGuid, out info) || info.Instance == null)
                {
                    return false;
                }

                _configurationManager = info.Instance;
                _displayingWindow = _configurationManager.GetType().GetProperty(
                    "DisplayingWindow", BindingFlags.Public | BindingFlags.Instance);
            }

            return _displayingWindow != null
                && _displayingWindow.PropertyType == typeof(bool)
                && (bool)_displayingWindow.GetValue(_configurationManager, null);
        }
    }
}