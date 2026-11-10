using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using Rewired;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>Mouse pan conversion used by MapPanProportionPatch.</summary>
    internal static class VanillaFixes
    {

        /// <summary>
        /// The pan offset one pixel of cursor movement is worth, replacing the frame time vanilla scales the drag by.
        /// Vanilla adds axis * 150 * min(frame time, 0.03) / zoom per frame, treating a mouse delta as a rate, and the
        /// patch turns the 150 into 1. The image sits at offset * lossyScale, and the zoom divides out after this
        /// returns, so a pixel is zoom / lossyScale of offset, scaled by Behavior.MapPanSpeed (1 is one for one).
        /// </summary>
        internal static float PanStep(float frameTerm)
        {
            var vanilla = 150f * frameTerm;

            if (!Plugin.MapPanFix.Value)
            {
                return vanilla;
            }

            var map = SceneSingleton<DynamicMap>.i;
            if (map == null || map.mapImage == null || map.mapScaleCenter == null)
            {
                return vanilla;
            }

            var zoom = map.mapScaleCenter.localScale.x;
            var drawn = map.mapImage.transform.lossyScale.x;
            if (zoom <= 0f || drawn <= 0f)
            {
                return vanilla;
            }

            return Plugin.MapPanSpeed.Value * zoom / drawn;
        }

        /// <summary>
        /// The map drag's mouse axis, replaced with what the drag asked the map to do this frame – the cursor's own
        /// movement, or the stick push while Behavior.MapStick is on. Rewired's value is in its own units and lands on
        /// Rewired's update rather than the rendered frame, so a pan built from it arrives in bursts. Tilt View
        /// arrives already inverted, which is why the game negates only Pan View.
        /// </summary>
        internal static float PanAxis(float value, string axis)
        {
            if (!Plugin.MapPanFix.Value)
            {
                return value;
            }

            var horizontal = axis != "Tilt View";
            var step = Plugin.MapStick.Value ? StickStep() : CursorStep();

            return horizontal ? step.x : -step.y;
        }

        /// <summary>
        /// What the stick asks the map to do this frame: the cursor's push away from the press point, taken as a speed
        /// and scaled by the frame, so the map keeps moving while the mouse is still. MapStickGain is what each pixel
        /// of push buys, with no ceiling. Nothing moves until the push passes UnitSelection's drag threshold, the same
        /// one the marquee starts on, and the speed counts from that push so it starts at zero rather than stepping.
        /// The map travels opposite the push, because the stick scrolls the view.
        /// </summary>
        private static Vector2 StickStep()
        {
            CursorStep();

            var push = new Vector2(_cursor.x, _cursor.y) - _pressPoint;
            var distance = push.magnitude;

            _stickPush = distance;
            _stickGain = Plugin.MapStickGain.Value;

            if (!UnitSelection.DragThresholdPassed(_pressPoint))
            {
                _stickBase = -1f;
                _stickSpeed = 0f;
                return Vector2.zero;
            }

            if (_stickBase < 0f)
            {
                _stickBase = distance;
            }

            _stickSpeed = (distance - _stickBase) * Plugin.MapStickGain.Value;

            return -push / distance * (_stickSpeed * Time.unscaledDeltaTime);
        }

        /// <summary>Pan report throttle, the frame it was last measured on, and the totals it reports.</summary>
        private static float _nextPanReport;
        private static int _panFrame = -1;
        private static Vector2 _cursorStep;
        private static Vector3 _cursor;
        private static Vector3 _mapImage;
        private static Vector2 _pressPoint;
        private static float _stickBase = -1f;
        private static float _stickPush;
        private static float _stickGain;
        private static float _stickSpeed;
        private static float _cursorX;
        private static float _cursorY;
        private static float _mapX;
        private static float _mapY;

        /// <summary>
        /// The cursor movement since the last frame of the current drag, measured once per frame however often the
        /// drag asks. A frame that does not continue the drag has no movement, so a press is not charged with
        /// everywhere the cursor has been – the first frame of a drag, and the first after the map closed or the
        /// camera jumped, are not movement this drag asked for. The stick's press point comes from such a frame.
        /// </summary>
        private static Vector2 CursorStep()
        {
            var cursor = Input.mousePosition;

            if (Time.frameCount == _panFrame)
            {
                return _cursorStep;
            }

            var map = SceneSingleton<DynamicMap>.i;
            var position = map != null && map.mapImage != null ? map.mapImage.transform.position : _mapImage;

            var continuing = _panFrame >= 0 && Time.frameCount == _panFrame + 1;

            _cursorStep = continuing ? new Vector2(cursor.x - _cursor.x, cursor.y - _cursor.y) : Vector2.zero;
            _panFrame = Time.frameCount;
            _cursor = cursor;

            if (continuing)
            {
                _cursorX += _cursorStep.x;
                _cursorY += _cursorStep.y;
                _mapX += position.x - _mapImage.x;
                _mapY += position.y - _mapImage.y;

                Report();
            }
            else
            {
                _pressPoint = cursor;
            }

            _mapImage = position;
            return _cursorStep;
        }

        /// <summary>
        /// Reports what the cursor and the map moved over the last second, and the ratio between them – 1 means the
        /// map kept up. A full second of frames is readable where one frame is not, since a single frame also carries
        /// the camera follow, a clamped edge and whatever the frame rate did to the step.
        /// </summary>
        private static void Report()
        {
            if (Time.timeSinceLevelLoad <= _nextPanReport)
            {
                return;
            }

            _nextPanReport = Time.timeSinceLevelLoad + 1f;

            if (Plugin.MapStick.Value)
            {
                Plugin.Verbose(
                    "Map pan over the last second: map " + _mapX.ToString("0.#") + ", " + _mapY.ToString("0.#")
                    + "px, stick push " + _stickPush.ToString("0.#") + "px, speed " + _stickSpeed.ToString("0")
                    + "px/s at " + _stickGain.ToString("0.##") + "px/s per px.");
            }
            else
            {
                Plugin.Verbose(
                    "Map pan over the last second: cursor " + _cursorX.ToString("0.#") + ", " + _cursorY.ToString("0.#")
                    + "px, map " + _mapX.ToString("0.#") + ", " + _mapY.ToString("0.#") + "px, ratio "
                    + Ratio(_mapX, _cursorX) + ", " + Ratio(_mapY, _cursorY) + ".");
            }

            _cursorX = 0f;
            _cursorY = 0f;
            _mapX = 0f;
            _mapY = 0f;
        }

        /// <summary>Map movement per pixel of cursor movement, or a dash when the cursor barely moved.</summary>
        private static string Ratio(float map, float cursor)
        {
            return Mathf.Abs(cursor) < 20f ? "-" : (map / cursor).ToString("0.###");
        }

    }

    /// <summary>
    /// Fires the onAllDeselected event DynamicMap.DeselectAllIcons forgets. That method clears selectedIcons without
    /// firing either deselect event, and TargetListSelector builds its rows from those two events alone, so every row
    /// survives. CombatHUD.DeselectAll, the unselect-all gesture, goes through the same method. Vanilla reaches it in
    /// normal play: Aircraft.OnStartClient on every respawn, AircraftSelectionMenu.OnEnable for the spawn screen, and
    /// AirbaseMapIcon.ClickIcon. The icon count comes from a prefix, because the body has emptied the list by the time
    /// a postfix runs.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.DeselectAllIcons))]
    internal static class DynamicMapDeselectAllIconsPatch
    {
        private static readonly System.Reflection.FieldInfo AllDeselected =
            AccessTools.Field(typeof(DynamicMap), "onAllDeselected");

        private static int _icons;

        [HarmonyPrefix]
        private static void Prefix(DynamicMap __instance)
        {
            var selected = __instance.selectedIcons;
            _icons = selected != null ? selected.Count : 0;
        }

        [HarmonyPostfix]
        private static void Postfix(DynamicMap __instance)
        {
            if (_icons == 0)
            {
                return;
            }

            Plugin.Verbose(
                "Dropped the Target List rows with the selection: DeselectAllIcons cleared " + _icons
                + " selected icon(s) without firing onAllDeselected.");

            var cleared = AllDeselected != null ? AllDeselected.GetValue(__instance) as Action : null;
            if (cleared != null)
            {
                cleared();
            }
        }
    }

    /// <summary>
    /// Makes the map follow the cursor while it is dragged. DynamicMap.MapControls scales the pan by the frame time,
    /// on a formula written for the keyboard axes just above, which are a rate – a mouse axis is a displacement, so
    /// the same drag moves the map further the slower the game runs and further than the cursor at any frame rate.
    /// Dropping the frame time alone is not enough, because the divisor is the map scale center's scale while
    /// SetZoomLevel keeps the image's world scale different as it moves it between parents.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), "MapControls")]
    internal static class MapPanProportionPatch
    {
        /// <summary>Frame-time call sites replaced, or -1 when the transpiler never ran.</summary>
        internal static int CallSites { get; private set; } = -1;

        /// <summary>Mouse axis call sites replaced, or -1 when the transpiler never ran.</summary>
        internal static int AxisSites { get; private set; } = -1;

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var min = AccessTools.Method(typeof(Mathf), nameof(Mathf.Min), new[] { typeof(float), typeof(float) });
            var unscaledDeltaTime = AccessTools.PropertyGetter(typeof(Time), nameof(Time.unscaledDeltaTime));
            var step = AccessTools.Method(typeof(VanillaFixes), nameof(VanillaFixes.PanStep));
            var axis = AccessTools.Method(typeof(VanillaFixes), nameof(VanillaFixes.PanAxis));

            var list = new List<CodeInstruction>(instructions);
            var patched = 0;
            var axes = 0;

            for (var i = 0; i < list.Count; i++)
            {
                // ldstr "Pan View", then callvirt GetAxis
                var axisName = list[i].operand as string;
                if (list[i].opcode == OpCodes.Ldstr
                    && (axisName == "Pan View" || axisName == "Tilt View")
                    && axis != null
                    && i + 1 < list.Count
                    && IsGetAxis(list[i + 1]))
                {
                    list.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldstr, axisName),
                        new CodeInstruction(OpCodes.Call, axis),
                    });

                    axes++;
                    i += 2;
                    continue;
                }

                // 150f * Mathf.Min(Time.unscaledDeltaTime, 0.03f)
                if (min == null
                    || step == null
                    || i < 3
                    || !list[i].Calls(min)
                    || !IsConstant(list[i - 1], 0.03f)
                    || unscaledDeltaTime == null
                    || !list[i - 2].Calls(unscaledDeltaTime)
                    || !IsConstant(list[i - 3], 150f))
                {
                    continue;
                }

                list[i - 3] = new CodeInstruction(OpCodes.Ldc_R4, 1f);
                list.Insert(i + 1, new CodeInstruction(OpCodes.Call, step));
                patched++;
                i++;
            }

            CallSites = patched;
            AxisSites = axes;

            if (patched != 1)
            {
                Plugin.Log.LogWarning(
                    "Expected exactly one frame-time pan factor in DynamicMap.MapControls but found " + patched
                    + ". The map will go on outrunning the cursor while panning.");
            }

            if (axes != 2)
            {
                Plugin.Log.LogWarning(
                    "Expected the two mouse axes in DynamicMap.MapControls but found " + axes + ". The map will go on"
                    + " panning in bursts while one of them is dragged.");
            }

            return list;
        }

        /// <summary>Whether this instruction reads one of the game's Rewired axes.</summary>
        private static bool IsGetAxis(CodeInstruction instruction)
        {
            return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                && instruction.operand is MethodInfo method
                && method.Name == "GetAxis"
                && method.GetParameters().Length == 1
                && method.GetParameters()[0].ParameterType == typeof(string);
        }

        private static bool IsConstant(CodeInstruction instruction, float value)
        {
            return instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float number && number == value;
        }
    }

    /// <summary>
    /// Releases a unit the weapon target list has nothing to say about. CombatHUD.DeSelectUnit returns early when the
    /// target list is empty, skipping the rest of the method, the map icon included. That is this mod's case: a unit
    /// selected on the map can have no weapon target entry. TargetListSelector.ForceDeselect, the pane's exclusion pass and
    /// TargetListSelector_UnitItem.ToggleSelect all route through here and then do nothing. The patch does the map
    /// half instead, through DynamicMap.DeselectIcon.
    /// </summary>
    [HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.DeSelectUnit))]
    internal static class CombatHudDeSelectUnitPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CombatHUD __instance, Unit unit)
        {
            if (unit == null)
            {
                return true;
            }

            var targets = __instance.GetTargetList();
            if (targets == null || targets.Count > 0)
            {
                return true;
            }

            var map = SceneSingleton<DynamicMap>.i;
            if (map == null || !DynamicMap.TryGetMapIcon(unit, out _))
            {
                return false;
            }

            Plugin.Verbose("Released " + unit.name + " from the map selection: the weapon list has nothing to drop.");
            map.DeselectIcon(unit);
            return false;
        }
    }

}
