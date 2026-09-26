using System.Collections.Generic;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Decides when the map's keys stop doing their normal job, and which keys those are.
    ///
    /// The rule is the mod's. While the map is maximized, and unless the user has turned the behavior off, the
    /// map takes over the keys in ModifierKeys as modifiers. It also takes the marquee's own modifier when
    /// RequireModifier is on. That key is held while dragging on the map, so it is a map modifier by definition.
    ///
    /// Driven from ModHost, the only thing in the mod guaranteed to outlive the boot. The key set is rebuilt
    /// every frame, which makes a config edit take effect without reopening the map. BindingBlocker ignores a
    /// set it is already holding, so the steady state costs one string comparison and a handful of set writes.
    /// </summary>
    internal static class ModifierGuard
    {
        private const string Reason = "while the map is maximized";

        private static readonly HashSet<KeyCode> Keys = new HashSet<KeyCode>();
        private static readonly List<KeyCode> Configured = new List<KeyCode>();

        private static string _parsedFrom;

        internal static void Tick()
        {
            if (!Plugin.SuppressModifierBindings.Value || !DynamicMap.mapMaximized)
            {
                BindingBlocker.Unblock();
                return;
            }

            CollectKeys();
            BindingBlocker.Block(Keys, Reason);
        }

        /// <summary>
        /// The configured keys go through a parsed-once cache. An unchanged ModifierKeys string then costs one
        /// comparison per frame, not an array of substrings for the garbage collector.
        /// </summary>
        private static void CollectKeys()
        {
            var configured = Plugin.ModifierKeys.Value;
            if (configured != _parsedFrom)
            {
                _parsedFrom = configured;
                Configured.Clear();
                ParseKeys(configured);
            }

            Keys.Clear();

            for (var i = 0; i < Configured.Count; i++)
            {
                Keys.Add(Configured[i]);
            }

            if (Plugin.RequireModifier.Value
                && Plugin.MarqueeModifier != null
                && Plugin.MarqueeModifier.Value.MainKey != KeyCode.None)
            {
                Keys.Add(Plugin.MarqueeModifier.Value.MainKey);
            }
        }

        private static void ParseKeys(string configured)
        {
            if (string.IsNullOrEmpty(configured))
            {
                return;
            }

            var names = configured.Split(',');
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i].Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                if (System.Enum.TryParse(name, true, out KeyCode parsed))
                {
                    Configured.Add(parsed);
                }
                else
                {
                    Plugin.Log.LogWarning("ModifierKeys: '" + name + "' is not a Unity KeyCode name, ignoring it.");
                }
            }
        }
    }
}
