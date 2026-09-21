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

        /// <summary>
        /// Lives in three places by necessity: this constant, which the BepInPlugin attribute needs to
        /// be a compile-time literal, &lt;Version&gt; in the csproj, and version in meta.json. Bump all three.
        /// </summary>
        public const string PluginVersion = "0.2.0";

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
        internal static ConfigEntry<bool> FollowFirstUnit { get; private set; }
        internal static ConfigEntry<bool> EnableInMissionEditor { get; private set; }
        internal static ConfigEntry<int> MaxTargets { get; private set; }
        internal static ConfigEntry<bool> ShowSelectionBox { get; private set; }
        internal static ConfigEntry<bool> VerboseLogging { get; private set; }
        internal static ConfigEntry<int> OverlaySortingOrder { get; private set; }
        internal static ConfigEntry<string> BoxOutlineColor { get; private set; }
        internal static ConfigEntry<string> BoxOutlineColorAdditive { get; private set; }
        internal static ConfigEntry<string> BoxOutlineColorRemove { get; private set; }
        internal static ConfigEntry<bool> EnableDestroyedUnits { get; private set; }
        internal static ConfigEntry<float> DestroyedBuildingTint { get; private set; }
        internal static ConfigEntry<bool> FilterNeutral { get; private set; }

        /// <summary>
        /// The mod actions whose default key has already been written. The mod adds a name as it deals with that action
        /// and never writes that default again, so a key cleared in the Controls menu stays cleared.
        /// </summary>
        internal static ConfigEntry<string> DefaultBindings { get; private set; }

        /// <summary>The config file, kept reachable for that one write, which happens outside Awake.</summary>
        internal static ConfigFile Settings { get; private set; }

        private Harmony _harmony;

        private static string _lastRejectedColor;

        private void Awake()
        {
            Log = Logger;

            MarqueeModifier = Config.Bind("Hotkeys", "MarqueeModifier",
                new KeyboardShortcut(KeyCode.LeftAlt),
                "Hold this and left drag on the map to marquee-select. Only used when RequireModifier is on.");

            RequireModifier = Config.Bind("Hotkeys", "RequireModifier", false,
                "Require MarqueeModifier to start a marquee. Off by default, so a plain left drag selects.");

            PanButton = Config.Bind("Hotkeys", "PanButton", MapPanButton.Middle,
                "Mouse button that drags to pan the map. The game hardcodes the left button, which collides "
                + "with the marquee. Right is already the waypoint button.");

            ShiftAddsToSelection = Config.Bind("Behavior", "ShiftAddsToSelection", true,
                "Hold Shift while dragging to add to the current selection instead of replacing it.");

            ControlRemovesFromSelection = Config.Bind("Behavior", "ControlRemovesFromSelection", true,
                "Hold Control while dragging to take the boxed units out of the selection instead of adding "
                + "them. Takes precedence over Shift when both are held.");

            SuppressModifierBindings = Config.Bind("Behavior", "SuppressModifierBindings", true,
                "While the map is maximized, disable any keyboard binding that sits directly on one of "
                + "ModifierKeys. Without this, Shift both adds to a selection and works the throttle.");

            ModifierKeys = Config.Bind("Behavior", "ModifierKeys",
                "LeftShift, RightShift, LeftControl, RightControl",
                "Keys that act only as map modifiers while the map is maximized. Any keyboard binding "
                + "placed directly on one of them is disabled for that time, whatever action it belongs "
                + "to. A binding that uses them as declared modifiers, such as Control+Shift+E, is left "
                + "alone. Unity KeyCode names, comma separated. The marquee modifier is added on its own "
                + "when RequireModifier is on.");

            BatchTargetListWhileFlying = Config.Bind("Behavior", "BatchTargetListWhileFlying", true,
                "While flying, a marquee adds the boxed units to your weapon target list, matching a map click. "
                + "Off disables the marquee in that mode.");

            FollowFirstUnit = Config.Bind("Behavior", "FollowFirstUnit", false,
                "In commander view, move the camera to the first boxed unit.");

            EnableInMissionEditor = Config.Bind("Behavior", "EnableInMissionEditor", false,
                "Allow the marquee in the mission editor, which has its own drag box select.");

            MaxTargets = Config.Bind("Behavior", "MaxTargets", Unit.MAX_STATION_TARGETS,
                new ConfigDescription(
                    "Cap on targets one marquee may add. The game refuses more than 128 per station.",
                    new AcceptableValueRange<int>(1, Unit.MAX_STATION_TARGETS)));

            ShowSelectionBox = Config.Bind("Visual", "ShowSelectionBox", true,
                "Draw the drag rectangle.");

            VerboseLogging = Config.Bind("Diagnostics", "VerboseLogging", true,
                "Log what the marquee is doing. Useful when something does not work.");

            OverlaySortingOrder = Config.Bind("Visual", "OverlaySortingOrder", DefaultOverlaySortingOrder,
                "Sorting order of this mod's overlay canvas. Raise it if the rectangle renders under the map UI.");

            BoxOutlineColor = Config.Bind("Visual", "BoxOutlineColor", "#3CB4FFCC",
                "Outline color for a replacing selection, as #RRGGBB or #RRGGBBAA.");
            BoxOutlineColorAdditive = Config.Bind("Visual", "BoxOutlineColorAdditive", "#7CE38BCC",
                "Outline color for an additive selection (Shift held).");
            BoxOutlineColorRemove = Config.Bind("Visual", "BoxOutlineColorRemove", "#FF5A5ACC",
                "Outline color for a deselecting marquee (Control held).");

            EnableDestroyedUnits = Config.Bind("Destroyed", "Enabled", true,
                "Keep destroyed buildings on the map as darkened wrecks, add the DED filter, and drop a dead unit "
                + "from the target list when it dies.");

            DestroyedBuildingTint = Config.Bind("Destroyed", "Tint", 0.45f,
                new ConfigDescription(
                    "How much of its usual colour a wrecked building's icon keeps. 1 leaves it unchanged. "
                    + "Only used when Destroyed.Enabled is on.",
                    new AcceptableValueRange<float>(0.1f, 1f)));

            FilterNeutral = Config.Bind("Target list", "FilterNeutral", true,
                "Add a NTL button to the Target List pane, for units that are neither friendly nor hostile.");

            Settings = Config;

            DefaultBindings = Config.Bind("Hotkeys", "DefaultBindings", string.Empty,
                "Actions whose default key the mod has already written, added to as it does. Removing a name has its "
                + "default written again; the record exists so a key you clear in the Controls menu stays clear.");

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
        /// Patches every class in this assembly, one class at a time.
        ///
        /// PatchAll stops at the first class it cannot patch, which takes every class after it down too: one
        /// postfix argument named differently from the game's own once left this mod half applied, with the pan
        /// gate still on the left mouse button and the Target List filter buttons missing. Failing per class keeps
        /// the rest working and says which one did not land.
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
            var iconClickTarget = AccessTools.Method(
                typeof(MapIcon),
                "UnityEngine.EventSystems.IPointerClickHandler.OnPointerClick",
                new[] { typeof(PointerEventData) });
            var iconClickInfo = iconClickTarget != null ? PatchProcessor.GetPatchInfo(iconClickTarget) : null;

            var wreckTarget = AccessTools.Method(typeof(UnitMapIcon), "UnitMapIcon_OnUnitDisabled");
            var wreckInfo = wreckTarget != null ? PatchProcessor.GetPatchInfo(wreckTarget) : null;
            var exclusionsTarget = AccessTools.Method(
                typeof(TargetListSelector), nameof(TargetListSelector.CheckExclusions));
            var exclusionsInfo =
                exclusionsTarget != null ? PatchProcessor.GetPatchInfo(exclusionsTarget) : null;

            // The patches added since this report was written are listed by the method they land on, not by what
            // they do, because the point is to tell a silent no-op from a patch that never applied. HarmonyX
            // resolves arguments by name, so a rename in the game can lose one without anything else failing.
            var deselectInfo = PatchInfo(AccessTools.Method(typeof(CombatHUD), nameof(CombatHUD.DeSelectUnit)));
            var setAircraftInfo = PatchInfo(AccessTools.Method(typeof(CombatHUD), nameof(CombatHUD.SetAircraft)));
            var removeAircraftInfo = PatchInfo(AccessTools.Method(typeof(CombatHUD), nameof(CombatHUD.RemoveAircraft)));
            var updateColorInfo = PatchInfo(AccessTools.Method(typeof(MapIcon), nameof(MapIcon.UpdateColor)));
            var unitDestroyInfo = PatchInfo(AccessTools.Method(typeof(Unit), "OnDestroy"));
            var resetInfo = PatchInfo(AccessTools.Method(typeof(TargetListSelector), nameof(TargetListSelector.ResetFilters)));
            var selectIconInfo = PatchInfo(AccessTools.Method(typeof(MapIcon), nameof(MapIcon.SelectIcon)));
            var playerControlsInfo = PatchInfo(AccessTools.Method(typeof(PilotPlayerState), "PlayerControls"));

            Log.LogInfo(
                "Patches: MapControls prefixes=" + (info != null ? info.Prefixes.Count : -1)
                + ", transpilers=" + (info != null ? info.Transpilers.Count : -1)
                + ", pan call sites rewritten=" + DynamicMapMapControlsPatch.PanReplacements
                + ", wheel mask call sites=" + CameraCockpitStateWheelMaskPatch.Masked
                + "/" + CameraTVStateWheelMaskPatch.Masked
                + " | Update prefixes=" + (updateInfo != null ? updateInfo.Prefixes.Count : -1)
                + " | icon click prefixes=" + (iconClickInfo != null ? iconClickInfo.Prefixes.Count : -1)
                + " | wreck icon prefixes=" + (wreckInfo != null ? wreckInfo.Prefixes.Count : -1)
                + " | target filter postfixes="
                + (exclusionsInfo != null ? exclusionsInfo.Postfixes.Count : -1)
                + " | deselect prefixes=" + deselectInfo
                + ", setAircraft postfixes=" + setAircraftInfo
                + ", removeAircraft postfixes=" + removeAircraftInfo
                + ", updateColor postfixes=" + updateColorInfo
                + ", selectIcon postfixes=" + selectIconInfo
                + ", unit destroy postfixes=" + unitDestroyInfo
                + ", player controls postfixes=" + playerControlsInfo
                + ", reset postfixes=" + resetInfo
                + " | destroyedUnits=" + EnableDestroyedUnits.Value
                + " | panButton=" + PanButton.Value
                + ", requireModifier=" + RequireModifier.Value
                + ", enableInMissionEditor=" + EnableInMissionEditor.Value);
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

        /// <summary>
        /// Fires before the main menu appears, because BepInEx destroys its plugin host object while the
        /// game is still starting. Unpatching here would strip the marquee's patches before it ever ran,
        /// so the patches and the overlay are both left alone.
        /// </summary>
        private void OnDestroy()
        {
            Log.LogInfo("Plugin host torn down by the loader; leaving patches in place.");
        }

        /// <summary>
        /// Reads one of the color settings. The config default is the only place a default color is
        /// written down, so there is no second copy here to fall back to.
        /// </summary>
        internal static Color ParseColor(ConfigEntry<string> entry)
        {
            if (entry != null && ColorUtility.TryParseHtmlString(entry.Value, out var parsed))
            {
                return parsed;
            }

            // Mention a bad value once rather than on every frame the box is drawn.
            var bad = entry != null ? entry.Value : "<unbound>";
            if (_lastRejectedColor != bad)
            {
                _lastRejectedColor = bad;
                Log.LogWarning("Could not read '" + bad + "' as #RRGGBB or #RRGGBBAA; drawing white.");
            }

            return Color.white;
        }
    }
}
