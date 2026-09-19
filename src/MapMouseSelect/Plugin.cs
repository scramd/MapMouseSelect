using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MapMouseSelect
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.kerbiter.mapmouseselect";
        public const string PluginName = "Map Mouse Select";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> MarqueeModifier { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> SelectOnlyKey { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> DeselectOldestKey { get; private set; }
        internal static ConfigEntry<bool> RequireModifier { get; private set; }
        internal static ConfigEntry<MapPanButton> PanButton { get; private set; }
        internal static ConfigEntry<bool> ShiftAddsToSelection { get; private set; }
        internal static ConfigEntry<bool> BatchTargetListWhileFlying { get; private set; }
        internal static ConfigEntry<bool> FlyingHostilesOnly { get; private set; }
        internal static ConfigEntry<bool> FollowFirstUnit { get; private set; }
        internal static ConfigEntry<bool> EnableInMissionEditor { get; private set; }
        internal static ConfigEntry<int> MaxTargets { get; private set; }
        internal static ConfigEntry<bool> ShowSelectionBox { get; private set; }
        internal static ConfigEntry<bool> VerboseLogging { get; private set; }
        internal static ConfigEntry<int> OverlaySortingOrder { get; private set; }
        internal static ConfigEntry<string> BoxOutlineColor { get; private set; }
        internal static ConfigEntry<string> BoxOutlineColorAdditive { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            MarqueeModifier = Config.Bind("Hotkeys", "MarqueeModifier",
                new KeyboardShortcut(KeyCode.LeftAlt),
                "Hold this and left drag on the map to marquee-select. Only used when RequireModifier is on.");

            RequireModifier = Config.Bind("Hotkeys", "RequireModifier", false,
                "Require MarqueeModifier to start a marquee. Off by default, so a plain left drag selects.");

            SelectOnlyKey = Config.Bind("Hotkeys", "SelectOnlyTargetKey",
                new KeyboardShortcut(KeyCode.Backslash),
                "Clear the current selection and select the unit under the map cursor, or the target under "
                + "the HUD designator while flying with the map closed.");

            DeselectOldestKey = Config.Bind("Hotkeys", "DeselectOldestTargetKey",
                new KeyboardShortcut(KeyCode.Delete),
                "Tap to drop the target you selected first. Hold to keep only the most recent one. Cancel "
                + "(Backspace) does the same thing for the newest target.");

            PanButton = Config.Bind("Hotkeys", "PanButton", MapPanButton.Middle,
                "Mouse button that drags to pan the map. The game hardcodes the left button, which collides "
                + "with the marquee. Right is already the waypoint button.");

            ShiftAddsToSelection = Config.Bind("Behavior", "ShiftAddsToSelection", true,
                "Hold Shift while dragging to add to the current selection instead of replacing it.");

            BatchTargetListWhileFlying = Config.Bind("Behavior", "BatchTargetListWhileFlying", true,
                "While flying, a marquee adds the boxed units to your weapon target list, matching a map click. "
                + "Off disables the marquee in that mode.");

            FlyingHostilesOnly = Config.Bind("Behavior", "FlyingHostilesOnly", false,
                "While flying, only box hostiles. Map clicks allow any unit with a HUD marker.");

            FollowFirstUnit = Config.Bind("Behavior", "FollowFirstUnit", false,
                "In commander view, move the camera to the first boxed unit.");

            EnableInMissionEditor = Config.Bind("Behavior", "EnableInMissionEditor", false,
                "Allow the marquee in the mission editor, which has its own drag box select.");

            MaxTargets = Config.Bind("Behavior", "MaxTargets", 128,
                new ConfigDescription(
                    "Cap on targets one marquee may add. The game refuses more than 128 per station.",
                    new AcceptableValueRange<int>(1, 128)));

            ShowSelectionBox = Config.Bind("Visual", "ShowSelectionBox", true,
                "Draw the drag rectangle.");

            VerboseLogging = Config.Bind("Diagnostics", "VerboseLogging", true,
                "Log what the marquee is doing. Useful when something does not work.");

            OverlaySortingOrder = Config.Bind("Visual", "OverlaySortingOrder", 32000,
                "Sorting order of this mod's overlay canvas. Raise it if the rectangle renders under the map UI.");

            BoxOutlineColor = Config.Bind("Visual", "BoxOutlineColor", "#3CB4FFCC",
                "Outline color for a replacing selection, as #RRGGBB or #RRGGBBAA.");
            BoxOutlineColorAdditive = Config.Bind("Visual", "BoxOutlineColorAdditive", "#7CE38BCC",
                "Outline color for an additive selection (Shift held).");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            ReportPatchState();

            try
            {
                var location = typeof(Plugin).Assembly.Location;
                Log.LogInfo("Loaded build: " + location + " (mtime "
                    + System.IO.File.GetLastWriteTime(location).ToString("HH:mm:ss") + ")");
            }
            catch
            {
                // Location is unavailable for in-memory assemblies.
            }
        }

        /// <summary>
        /// Startup check so a silent no-op is distinguishable from a broken patch.
        /// </summary>
        private static void ReportPatchState()
        {
            var target = AccessTools.Method(typeof(DynamicMap), "MapControls");
            if (target == null)
            {
                Log.LogError("Could not resolve DynamicMap.MapControls, so the marquee cannot run.");
                return;
            }

            var info = PatchProcessor.GetPatchInfo(target);
            var updateTarget = AccessTools.Method(typeof(DynamicMap), "Update");
            var updateInfo = updateTarget != null ? PatchProcessor.GetPatchInfo(updateTarget) : null;

            Log.LogInfo(
                "Patches: MapControls prefixes=" + (info != null ? info.Prefixes.Count : -1)
                + ", transpilers=" + (info != null ? info.Transpilers.Count : -1)
                + ", pan call sites rewritten=" + DynamicMapMapControlsPatch.PanReplacements
                + " | Update prefixes=" + (updateInfo != null ? updateInfo.Prefixes.Count : -1)
                + " | panButton=" + PanButton.Value
                + ", requireModifier=" + RequireModifier.Value
                + ", enableInMissionEditor=" + EnableInMissionEditor.Value);
        }

        internal static void Verbose(string message)
        {
            if (VerboseLogging != null && VerboseLogging.Value)
            {
                Log.LogInfo(message);
            }
        }

        /// <summary>
        /// Fires before the main menu appears, because BepInEx destroys its plugin host object while the
        /// game is still starting. Unpatching here would strip the marquee's patches before it ever ran,
        /// so the patches and the overlay are both left alone.
        /// </summary>
        private void OnDestroy()
        {
            Log.LogInfo("Plugin host torn down by the loader; leaving patches in place.");
        }

        internal static Color ParseColor(ConfigEntry<string> entry, Color fallback)
        {
            if (entry != null && ColorUtility.TryParseHtmlString(entry.Value, out var parsed))
            {
                return parsed;
            }
            return fallback;
        }
    }
}
