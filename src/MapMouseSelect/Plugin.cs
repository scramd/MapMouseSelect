using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MapMouseSelect
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.kerbiter.mapmouseselect";
        public const string PluginName = "Map Mouse Select";

        /// <summary>Lives in three places: this constant, &lt;Version&gt; in the csproj, and version in meta.json.</summary>
        public const string PluginVersion = "0.3.0";

        /// <summary>Draw order for the mod's overlay canvas, above the map UI.</summary>
        internal const int DefaultOverlaySortingOrder = 32000;

        internal static ManualLogSource Log { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> MarqueeModifier { get; private set; }
        internal static ConfigEntry<bool> RequireModifier { get; private set; }
        internal static ConfigEntry<MapPanButton> PanButton { get; private set; }
        internal static ConfigEntry<bool> ShiftAddsToSelection { get; private set; }
        internal static ConfigEntry<bool> ControlRemovesFromSelection { get; private set; }
        internal static ConfigEntry<bool> SuppressModifierBindings { get; private set; }
        internal static ConfigEntry<string> ModifierKeys { get; private set; }
        internal static ConfigEntry<bool> BatchTargetListWhileFlying { get; private set; }
        internal static ConfigEntry<bool> EnableInMissionEditor { get; private set; }
        internal static ConfigEntry<int> MaxTargets { get; private set; }
        internal static ConfigEntry<bool> ShowSelectionBox { get; private set; }

        internal static ConfigEntry<bool> VerboseLogging { get; private set; }
        internal static ConfigEntry<int> OverlaySortingOrder { get; private set; }

        /// <summary>Box outline colors, as colors rather than text, so the config UI shows its picker.</summary>
        internal static ConfigEntry<Color> BoxOutlineColor { get; private set; }
        internal static ConfigEntry<Color> BoxOutlineColorAdditive { get; private set; }
        internal static ConfigEntry<Color> BoxOutlineColorRemove { get; private set; }
        internal static ConfigEntry<bool> FilterNeutral { get; private set; }

        /// <summary>Whether the map drag follows the cursor. Off leaves vanilla's frame-time-scaled pan in place.</summary>
        internal static ConfigEntry<bool> MapPanFix { get; private set; }

        /// <summary>How far the map moves per pixel of mouse movement, used while MapPanFix is on. One is under the cursor.</summary>
        internal static ConfigEntry<float> MapPanSpeed { get; private set; }


        /// <summary>Whether Free Look is ignored while the map is dragged with the button it shares.</summary>
        internal static ConfigEntry<bool> SuppressFreeLookWhileDragging { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            MarqueeModifier = Config.Bind("Hotkeys", "MarqueeModifier",
                new KeyboardShortcut(KeyCode.LeftAlt),
                "Hold this and left drag on the map to marquee-select. Only used when RequireModifier is on.");

            RequireModifier = Config.Bind("Hotkeys", "RequireModifier", false,
                "Require MarqueeModifier to start a marquee. Off by default, so a plain left drag selects.");

            PanButton = Config.Bind("Hotkeys", "PanButton", MapPanButton.Right,
                "Mouse button that drags to move the map. The game hardcodes the left button, which collides "
                + "with the marquee. The button keeps its own click: a right click without a drag still sets a "
                + "waypoint, a middle click without a drag still jumps the camera.");

            ShiftAddsToSelection = Config.Bind("Behavior", "ShiftAddsToSelection", true,
                "Hold Shift while dragging to add to the current selection instead of replacing it.");

            ControlRemovesFromSelection = Config.Bind("Behavior", "ControlRemovesFromSelection", true,
                "Hold Control while dragging to take the boxed units out of the selection instead of adding "
                + "them. Takes precedence over Shift when both are held.");

            SuppressModifierBindings = Config.Bind("Behavior", "SuppressModifierBindings", true,
                "While the map is maximized, disable any keyboard binding placed directly on one of "
                + "ModifierKeys, so Shift does not also work the throttle.");

            ModifierKeys = Config.Bind("Behavior", "ModifierKeys",
                "LeftShift, RightShift, LeftControl, RightControl",
                "Keys that act as map modifiers while the map is maximized, as Unity KeyCode names, comma "
                + "separated. A binding placed directly on one is disabled, a declared modifier binding such as "
                + "Control+Shift+E is left alone, and the marquee modifier joins when RequireModifier is on.");

            BatchTargetListWhileFlying = Config.Bind("Behavior", "BatchTargetListWhileFlying", true,
                "While flying, a marquee adds the boxed units to your weapon target list, matching a map click. "
                + "Off disables the marquee in that mode.");

            EnableInMissionEditor = Config.Bind("Behavior", "EnableInMissionEditor", false,
                "Allow the marquee in the mission editor, which has its own drag box select.");

            MaxTargets = Config.Bind("Behavior", "MaxTargets", Unit.MAX_STATION_TARGETS,
                new ConfigDescription(
                    "Cap on targets one marquee may add. The game refuses more than 128 per station.",
                    new AcceptableValueRange<int>(1, Unit.MAX_STATION_TARGETS)));

            ShowSelectionBox = Config.Bind("Visual", "ShowSelectionBox", true,
                "Draw the drag rectangle.");

            VerboseLogging = Config.Bind("Diagnostics", "VerboseLogging", true,
                "Log what the marquee is doing.");

            OverlaySortingOrder = Config.Bind("Visual", "OverlaySortingOrder", DefaultOverlaySortingOrder,
                "Sorting order of this mod's overlay canvas. Raise it if the rectangle renders under the map UI.");

            BoxOutlineColor = Config.Bind("Visual", "BoxOutlineColor", Hex("#3CB4FFCC"),
                "Outline color for a replacing selection.");
            BoxOutlineColorAdditive = Config.Bind("Visual", "BoxOutlineColorAdditive", Hex("#7CE38BCC"),
                "Outline color for an additive selection (Shift held).");
            BoxOutlineColorRemove = Config.Bind("Visual", "BoxOutlineColorRemove", Hex("#FF5A5ACC"),
                "Outline color for a deselecting marquee (Control held).");

            FilterNeutral = Config.Bind("Target list", "FilterNeutral", true,
                "Add a NTL button to the Target List pane, for units that are neither friendly nor hostile.");

            MapPanFix = Config.Bind("Behavior", "MapPanFix", true,
                "Make the map follow the cursor while it is dragged, instead of outrunning it. This replaces the "
                + "frame time vanilla scales the drag by and reads the cursor directly, so off leaves vanilla's "
                + "pan alone.");

            MapPanSpeed = Config.Bind("Behavior", "MapPanSpeed", 1f,
                new ConfigDescription(
                    "How far the map moves per pixel the mouse moves while dragging it, used while MapPanFix is on. "
                    + "One keeps the map under the cursor.",
                    new AcceptableValueRange<float>(0.1f, 5f)));

            SuppressFreeLookWhileDragging = Config.Bind("Behavior", "SuppressFreeLookWhileDragging", true,
                "While the map is open and the map-drag button is held, ignore the game's Free Look, which sits on "
                + "the right mouse button by default and would turn the camera behind the map during a drag.");

            _harmony = new Harmony(PluginGuid);
            ApplyPatches();
            ModHost.Ensure();
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
        /// Patches every class in this assembly, one at a time. PatchAll stops at the first class it cannot patch,
        /// which takes every class after it down too, so a bad postfix argument would leave the mod half applied.
        /// </summary>
        private void ApplyPatches()
        {
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
                {
                    continue;
                }

                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    Log.LogError("Could not apply the patches in " + type.Name + ": " + e);
                }
            }
        }

        /// <summary>Startup check so a silent no-op is distinguishable from a broken patch.</summary>
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
            var iconClickTarget = AccessTools.Method(
                typeof(MapIcon),
                "UnityEngine.EventSystems.IPointerClickHandler.OnPointerClick",
                new[] { typeof(PointerEventData) });
            var iconClickInfo = iconClickTarget != null ? PatchProcessor.GetPatchInfo(iconClickTarget) : null;

            var exclusionsTarget = AccessTools.Method(
                typeof(TargetListSelector), nameof(TargetListSelector.CheckExclusions));
            var exclusionsInfo =
                exclusionsTarget != null ? PatchProcessor.GetPatchInfo(exclusionsTarget) : null;

            // The patches are listed by the method they land on, because the point is to tell a silent no-op from
            // a patch that never applied. HarmonyX resolves arguments by name, so a game-side rename loses one.
            var deselectInfo = PatchInfo(AccessTools.Method(typeof(CombatHUD), nameof(CombatHUD.DeSelectUnit)));
            var setAircraftInfo = PatchInfo(AccessTools.Method(typeof(CombatHUD), nameof(CombatHUD.SetAircraft)));
            var removeAircraftInfo = PatchInfo(AccessTools.Method(typeof(CombatHUD), nameof(CombatHUD.RemoveAircraft)));
            var resetInfo = PatchInfo(AccessTools.Method(typeof(TargetListSelector), nameof(TargetListSelector.ResetFilters)));
            var selectIconInfo = PatchInfo(AccessTools.Method(typeof(MapIcon), nameof(MapIcon.SelectIcon)));

            Log.LogInfo(
                "Patches: MapControls prefixes=" + (info != null ? info.Prefixes.Count : -1)
                + ", transpilers=" + (info != null ? info.Transpilers.Count : -1)
                + ", pan call sites rewritten=" + DynamicMapMapControlsPatch.PanReplacements
                + ", waypoint call sites rewritten=" + DynamicMapMapControlsPatch.OrderReplacements
                + ", pan factor call sites=" + MapPanProportionPatch.CallSites
                + ", pan axis call sites=" + MapPanProportionPatch.AxisSites
                + " | Update prefixes=" + (updateInfo != null ? updateInfo.Prefixes.Count : -1)
                + " | icon click prefixes=" + (iconClickInfo != null ? iconClickInfo.Prefixes.Count : -1)
                + " | target filter postfixes="
                + (exclusionsInfo != null ? exclusionsInfo.Postfixes.Count : -1)
                + " | deselect prefixes=" + deselectInfo
                + ", setAircraft postfixes=" + setAircraftInfo
                + ", removeAircraft postfixes=" + removeAircraftInfo
                + ", selectIcon postfixes=" + selectIconInfo
                + ", reset postfixes=" + resetInfo
                + " | panButton=" + PanButton.Value
                + ", requireModifier=" + RequireModifier.Value
                + ", enableInMissionEditor=" + EnableInMissionEditor.Value
                + ", mapPanFix=" + MapPanFix.Value);
        }

        /// <summary>How many prefixes and postfixes landed on a method, or -1 when the method is not there.</summary>
        private static int PatchInfo(System.Reflection.MethodBase target)
        {
            var info = target != null ? PatchProcessor.GetPatchInfo(target) : null;
            return info != null ? info.Prefixes.Count + info.Postfixes.Count : -1;
        }

        internal static void Verbose(string message)
        {
            if (VerboseLogging != null && VerboseLogging.Value)
            {
                Log.LogInfo(message);
            }
        }

        /// <summary>Fires before the main menu appears. Unpatching here would strip the marquee's patches before they ran.</summary>
        private void OnDestroy()
        {
            Log.LogInfo("Plugin host torn down by the loader. Patches left in place.");
        }

        /// <summary>A color from a #RRGGBB or #RRGGBBAA literal, for the defaults above.</summary>
        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out var parsed);
            return parsed;
        }
    }
}
