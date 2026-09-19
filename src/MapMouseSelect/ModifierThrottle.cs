using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Keeps the map's modifier keys from also working the throttle, by disabling the bindings rather
    /// than ignoring the keys.
    ///
    /// The throttle action is an axis whose two directions are Shift and Control by default, and Shift is
    /// also the map's additive modifier, for vanilla's click-select and waypoint append and for this mod's
    /// marquee. Rather than dropping throttle input on any frame where a modifier happens to be held, this
    /// disables exactly the bindings that put the throttle on a modifier key, for as long as the map is
    /// maximized. Everything else keeps working: other keys or a lever bound to the same action, and Shift
    /// or Control if the player has rebound them, because then there is no such binding to disable. Only
    /// the keyboard is looked at, so a HOTAS lever is never touched.
    ///
    /// Rewired has no priority system that would resolve this for us. The mechanisms it does have are
    /// per-binding enables like this one, per-map enables, and per-context map categories toggled with
    /// SetMapsEnabled. The game uses none of them for the map, and a category would take Shift with it
    /// everywhere else it is used, so a single binding is the smallest thing that fits.
    ///
    /// While the map is open Rewired's whole action list is re-read each frame, but Syncing is a state
    /// comparison, so the scan only happens on the transition in and out. Bindings disabled here are
    /// tracked, so restoring only ever puts back what this turned off, and nothing is written to Rewired's
    /// saved user data.
    /// </summary>
    internal static class ModifierThrottle
    {
        private const string ThrottleActionName = "Throttle";

        private static readonly List<ControllerMap> KeyboardMaps = new List<ControllerMap>();
        private static readonly List<ActionElementMap> Disabled = new List<ActionElementMap>();

        private static bool _suppressing;

        internal static void Sync()
        {
            var suppress = Plugin.BlockModifierThrottle.Value && DynamicMap.mapMaximized;

            if (!suppress)
            {
                if (_suppressing)
                {
                    Restore();
                    _suppressing = false;
                }
                return;
            }

            // Latched only on success, so a frame where Rewired is not ready yet is retried.
            if (!_suppressing && Suppress())
            {
                _suppressing = true;
            }
        }

        private static bool Suppress()
        {
            var player = GameManager.playerInput;
            var actionId = player != null ? ReInput.mapping.GetActionId(ThrottleActionName) : -1;

            if (player == null || actionId < 0)
            {
                Plugin.Log.LogWarning(
                    "Could not resolve the Rewired '" + ThrottleActionName
                    + "' action, so the map's modifier keys will also move the throttle. The game may have changed.");
                return false;
            }

            KeyboardMaps.Clear();
            player.controllers.maps.GetAllMaps(ControllerType.Keyboard, KeyboardMaps);

            for (var i = 0; i < KeyboardMaps.Count; i++)
            {
                var map = KeyboardMaps[i];
                if (map == null || !map.enabled)
                {
                    continue;
                }

                var elements = map.AllMaps;
                for (var j = 0; j < elements.Count; j++)
                {
                    var element = elements[j];
                    if (element == null || !element.enabled || !IsThrottleOnModifier(element, actionId))
                    {
                        continue;
                    }

                    element.enabled = false;
                    Disabled.Add(element);
                }
            }

            Plugin.Verbose(
                "Disabled " + Disabled.Count + " throttle binding(s) on the map modifier keys while the map is open.");
            return true;
        }

        private static void Restore()
        {
            var restored = 0;
            for (var i = 0; i < Disabled.Count; i++)
            {
                var element = Disabled[i];
                if (element == null)
                {
                    continue;
                }

                element.enabled = true;
                restored++;
            }

            Disabled.Clear();

            if (restored > 0)
            {
                Plugin.Verbose("Restored " + restored + " throttle binding(s) on closing the map.");
            }
        }

        private static bool IsThrottleOnModifier(ActionElementMap element, int throttleActionId)
        {
            if (element.actionId != throttleActionId)
            {
                return false;
            }

            switch (element.keyCode)
            {
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return true;
                default:
                    return false;
            }
        }
    }
}
