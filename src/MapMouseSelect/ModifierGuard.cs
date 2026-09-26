using System.Collections.Generic;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Decides when the map's keys stop doing their normal job, and which keys those are: the ModifierKeys set,
    /// plus the marquee's own modifier when RequireModifier is on. The key set is rebuilt every frame, so a config
    /// edit takes effect without reopening the map.
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

        /// <summary>The configured keys go through a parsed-once cache, so an unchanged string avoids per-frame garbage.</summary>
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
