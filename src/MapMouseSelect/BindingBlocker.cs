using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Disables keyboard bindings that sit directly on a given set of keys, and hands them back later.
    ///
    /// Nothing here knows about the map, the marquee or the config. Block takes a key set and a phrase for the log,
    /// and the caller decides when to use it. That keeps the mechanism usable for any key that has to stop doing
    /// its normal job for a while, and keeps the rules about which keys those are in one place, in ModifierGuard.
    ///
    /// The case it was written for: the game hangs the throttle's two directions on Shift and Control. Shift is
    /// also the map's additive modifier, for vanilla's click-select and waypoint append and for this mod's
    /// marquee, so holding Shift to add to a selection spools the engine. Nothing about the fix is
    /// throttle-specific though. The rule is about the keys.
    ///
    /// Directly is the word that matters. A binding such as Eject, which is E with Control and Shift declared as
    /// its modifiers, is left alone, because there those keys are already acting as modifiers. Only bindings whose
    /// own key is in the set are touched.
    ///
    /// Rewired has a mechanism of its own here, the key combination override. It stops a bare binding from
    /// responding when the user presses the combination it belongs to. It only engages while a keyboard
    /// combination is held, so a mouse gesture can never trigger it, and the machinery is internal and obfuscated.
    /// Per-binding enables are the public equivalent.
    ///
    /// Bindings disabled here are tracked, so Unblock puts back only what this turned off. Nothing is written to
    /// Rewired's saved user data.
    /// </summary>
    internal static class BindingBlocker
    {
        private static readonly List<ControllerMap> KeyboardMaps = new List<ControllerMap>();
        private static readonly List<ActionElementMap> Disabled = new List<ActionElementMap>();
        private static readonly HashSet<KeyCode> Keys = new HashSet<KeyCode>();
        private static readonly KeyCode[] NoKeys = new KeyCode[0];

        private static bool _inEffect;

        /// <summary>
        /// Disables every enabled keyboard binding of the local player whose own key is in
        /// <paramref name="keys"/>. Calling it again with the same set does nothing. Calling it with a different
        /// set swaps the block for the new one. <paramref name="reason"/> is appended to the log lines, so it
        /// should read as the end of the sentence "blocked &lt;binding&gt; on &lt;key&gt; &lt;reason&gt;".
        ///
        /// Returns false when Rewired is not ready to be asked yet. Nothing was disabled then, and the caller
        /// should call again. Safe to call every frame.
        /// </summary>
        internal static bool Block(IEnumerable<KeyCode> keys, string reason)
        {
            if (keys == null)
            {
                keys = NoKeys;
            }

            if (_inEffect && Keys.SetEquals(keys))
            {
                return true;
            }

            Unblock();

            foreach (var key in keys)
            {
                Keys.Add(key);
            }

            // An empty set is a real answer. There is nothing to hold, and nothing to do again until it changes.
            if (Keys.Count == 0)
            {
                _inEffect = true;
                return true;
            }

            var player = GameManager.playerInput;
            if (player == null)
            {
                Keys.Clear();
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
                    if (element == null || !element.enabled || !Keys.Contains(element.keyCode))
                    {
                        continue;
                    }

                    element.enabled = false;
                    Disabled.Add(element);
                    Plugin.Verbose(
                        "Blocked '" + element.actionDescriptiveName + "' on " + element.keyCode + " " + reason
                        + ".");
                }
            }

            _inEffect = true;
            Plugin.Verbose("Blocked " + Disabled.Count + " binding(s) " + reason + ".");
            return true;
        }

        /// <summary>
        /// Hands back every binding this disabled. Safe at any time, including when nothing is disabled, so
        /// a caller that wants no block at all can call this every frame.
        /// </summary>
        internal static void Unblock()
        {
            for (var i = 0; i < Disabled.Count; i++)
            {
                var element = Disabled[i];
                if (element != null)
                {
                    element.enabled = true;
                }
            }

            if (Disabled.Count > 0)
            {
                Plugin.Verbose("Unblocked " + Disabled.Count + " binding(s).");
            }

            Disabled.Clear();
            Keys.Clear();
            _inEffect = false;
        }
    }
}
