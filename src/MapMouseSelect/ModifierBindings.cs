using System;
using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Stops the keys the map uses as modifiers from doing their normal job at the same time.
    ///
    /// The game hangs the throttle's two directions on Shift and Control, and Shift is also the map's
    /// additive modifier, for vanilla's click-select and waypoint append and for this mod's marquee, so
    /// holding Shift to add to a selection spools the engine. Any keyboard binding that sits directly on
    /// one of the configured modifier keys is disabled while the map is maximized, whatever action it
    /// belongs to. Nothing here is specific to the throttle, or to any other action: the rule is about
    /// the keys.
    ///
    /// Directly is the word that matters. A binding such as Eject, which is E with Control and Shift
    /// declared as its modifiers, is left alone, because there those keys are already being used as
    /// modifiers. Only bindings whose own key is one of these are touched.
    ///
    /// Rewired has no priority system that resolves this. It offers per-binding enables like this one,
    /// per-map enables, and per-context map categories toggled with SetMapsEnabled. The game uses none of
    /// them for the map, and switching a category off would take Shift with it everywhere else it is used.
    /// The game's own way of repurposing the throttle is the Axis Modifier action, unbound by default,
    /// which when held moves the throttle axis input onto Custom Axis 1 instead of blocking it.
    ///
    /// Bindings disabled here are tracked, so restoring only puts back what this turned off, and nothing is
    /// written to Rewired's saved user data.
    /// </summary>
    internal static class ModifierBindings
    {
        private static readonly List<ControllerMap> KeyboardMaps = new List<ControllerMap>();
        private static readonly List<ActionElementMap> Disabled = new List<ActionElementMap>();
        private static readonly List<KeyCode> Keys = new List<KeyCode>();

        private static bool _suppressing;

        internal static void Sync()
        {
            var suppress = Plugin.SuppressModifierBindings.Value && DynamicMap.mapMaximized;

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
            if (player == null)
            {
                return false;
            }

            ReadKeys();
            if (Keys.Count == 0)
            {
                return true;
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
                    if (element == null || !element.enabled || !Keys.Contains(element.keyCode))
                    {
                        continue;
                    }

                    element.enabled = false;
                    Disabled.Add(element);
                    Plugin.Verbose(
                        "Suppressed '" + element.actionDescriptiveName + "' on " + element.keyCode
                        + " while the map is open.");
                }
            }

            Plugin.Verbose("Suppressed " + Disabled.Count + " binding(s) on the map modifier keys.");
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
                Plugin.Verbose("Restored " + restored + " binding(s) when the map closed.");
            }
        }

        /// <summary>
        /// The configured keys, plus the marquee's own modifier while it is required. That key is dragged
        /// on the map, so it is a map modifier by definition.
        /// </summary>
        private static void ReadKeys()
        {
            Keys.Clear();

            var configured = Plugin.ModifierKeys.Value;
            if (!string.IsNullOrEmpty(configured))
            {
                var names = configured.Split(',');
                for (var i = 0; i < names.Length; i++)
                {
                    var name = names[i].Trim();
                    if (name.Length == 0)
                    {
                        continue;
                    }

                    if (Enum.TryParse(name, true, out KeyCode parsed))
                    {
                        Keys.Add(parsed);
                    }
                    else
                    {
                        Plugin.Log.LogWarning("ModifierKeys: '" + name + "' is not a Unity KeyCode name, ignoring it.");
                    }
                }
            }

            if (Plugin.RequireModifier.Value
                && Plugin.MarqueeModifier != null
                && Plugin.MarqueeModifier.Value.MainKey != KeyCode.None)
            {
                Keys.Add(Plugin.MarqueeModifier.Value.MainKey);
            }
        }
    }
}
