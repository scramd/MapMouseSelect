using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Rewired;

namespace MapMouseSelect
{
    /// <summary>
    /// Keeps a map drag from also turning the camera. The game's Free Look sits on the right mouse button by
    /// default, the button this mod drags the map with, and the camera states that use Free Look do not ask
    /// whether the map is open. While the map is expanded and the drag button is held, their Free Look read is
    /// answered with "not held". Free Look on any other control is left alone.
    ///
    /// The mask goes in behind the read and the read itself stays, so another mod that patches the same read
    /// still finds it.
    /// </summary>
    internal static class FreeLookGuard
    {
        private const string ActionName = "Free Look";

        internal static bool Mask(bool held)
        {
            return held
                && !(Plugin.SuppressFreeLookWhileDragging.Value
                    && DynamicMap.mapMaximized
                    && PanButtonCompat.GetMouseButton(0));
        }

        internal static IEnumerable<CodeInstruction> Apply(IEnumerable<CodeInstruction> instructions, string method)
        {
            var getButton = AccessTools.Method(typeof(Player), nameof(Player.GetButton), new[] { typeof(string) });
            var mask = AccessTools.Method(typeof(FreeLookGuard), nameof(Mask));

            var list = new List<CodeInstruction>(instructions);
            var sites = new List<int>();

            for (var i = 1; i < list.Count; i++)
            {
                if (list[i - 1].opcode == OpCodes.Ldstr
                    && list[i - 1].operand as string == ActionName
                    && getButton != null
                    && list[i].Calls(getButton))
                {
                    sites.Add(i);
                }
            }

            if (sites.Count != 1 || mask == null)
            {
                Plugin.Log.LogWarning(
                    "Expected exactly one Free Look read in " + method + " but found " + sites.Count
                    + ". A map drag may also turn that camera. The game may have changed.");
                return list;
            }

            list.Insert(sites[0] + 1, new CodeInstruction(OpCodes.Call, mask));
            return list;
        }
    }

    [HarmonyPatch(typeof(CameraCockpitState), "UpdateState")]
    internal static class CockpitFreeLookPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return FreeLookGuard.Apply(instructions, "CameraCockpitState.UpdateState");
        }
    }

    [HarmonyPatch(typeof(CameraFreeState), "UpdateState")]
    internal static class FreeCameraFreeLookPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return FreeLookGuard.Apply(instructions, "CameraFreeState.UpdateState");
        }
    }

    [HarmonyPatch(typeof(CameraControlledState), "UpdateState")]
    internal static class ControlledCameraFreeLookPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return FreeLookGuard.Apply(instructions, "CameraControlledState.UpdateState");
        }
    }

    [HarmonyPatch(typeof(CameraSelectionState), "MoveCamera")]
    internal static class SelectionCameraFreeLookPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return FreeLookGuard.Apply(instructions, "CameraSelectionState.MoveCamera");
        }
    }
}