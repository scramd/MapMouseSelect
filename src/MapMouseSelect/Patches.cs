using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Drives the marquee from the game's own per-frame method.
    ///
    /// DynamicMap.Update ticks whenever the map canvas object is active and calls MapControls straight
    /// after, so running the state machine here means the "skip vanilla this frame" decision is always
    /// made in time. This deliberately does not depend on a GameObject of our own: BepInEx destroys its
    /// plugin host object when the game's first real scene initialises, which takes any component added
    /// in Plugin.Awake with it. Harmony patches outlive that.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), "Update")]
    internal static class DynamicMapUpdatePatch
    {
        [HarmonyPrefix]
        private static void Prefix(DynamicMap __instance)
        {
            Marquee.Tick(__instance);
            TargetKeys.Tick();
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "MapControls")]
    internal static class DynamicMapMapControlsPatch
    {
        /// <summary>Number of pan-gate call sites the transpiler rewrote. -1 means it never ran.</summary>
        internal static int PanReplacements { get; private set; } = -1;

        /// <summary>
        /// Skipping vanilla controls while the marquee owns the mouse is what holds off both the
        /// click-select and the left-drag map panning for the duration of the drag.
        /// </summary>
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !Marquee.OwnsMouse;
        }

        /// <summary>
        /// Moves map panning off the hardcoded left mouse button. Gameplay-independent: the pan gate is
        /// the only plain Input.GetMouseButton call in MapControls, so exactly one site must match.
        /// </summary>
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = AccessTools.Method(typeof(Input), nameof(Input.GetMouseButton), new[] { typeof(int) });
            var replacement = AccessTools.Method(typeof(PanButtonCompat), nameof(PanButtonCompat.GetMouseButton));

            var patched = 0;
            var result = new List<CodeInstruction>();

            foreach (var instruction in instructions)
            {
                if (original != null && replacement != null && instruction.Calls(original))
                {
                    instruction.operand = replacement;
                    patched++;
                }
                result.Add(instruction);
            }

            PanReplacements = patched;

            if (patched != 1)
            {
                Plugin.Log.LogWarning(
                    "Expected exactly one map-pan GetMouseButton call in DynamicMap.MapControls but found "
                    + patched + ". The PanButton setting will have no effect; the game may have changed.");
            }

            return result;
        }
    }
}
