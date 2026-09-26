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
    /// <summary>
    /// Three vanilla paths assume that every unit they receive has a map icon. They throw when it does not.
    ///
    /// DynamicMap.GetOrAddIcon queues an icon for a unit that has none and then returns iconLookup[unit]. Nothing
    /// creates an icon for a unit that is disabled, or for a unit whose definition asks for none, so the lookup
    /// throws KeyNotFoundException. Its only caller, FlagIncomingMissile, dereferences the result on the next
    /// line. The exception replaces a warning about a unit that no longer needs one. CombatHUD.HighlightMarker
    /// does the same with markerLookup, reached from a radar warning.
    ///
    /// This mod patches all three to the rule the icon spawner already uses: a live unit whose definition asks
    /// for an icon can have one, and anything else has nothing to draw.
    /// </summary>
    internal static class VanillaFixes
    {
        /// <summary>
        /// The wheel value a camera state should see: none at all while the cursor is up. Two of the four camera states
        /// ask this themselves. This is the same question for the two that do not.
        /// </summary>
        internal static float WheelWhileCursorUp(float value)
        {
            return Cursor.visible ? 0f : value;
        }

        /// <summary>
        /// The pan offset one pixel of cursor movement is worth, replacing the frame time vanilla scales the drag by.
        ///
        /// Vanilla adds `axis * 150 * min(frame time, 0.03) / zoom` to the map's pan offset every frame. Frame time
        /// belongs in a rate. The keyboard pan axes above it are a rate, and a mouse delta is not. With frame time, the
        /// same drag of the same length moves the map further the slower the game runs. The map also moves further than
        /// the cursor at every frame rate. The patch replaces the 150 outside this value with 1, so this returns the pan
        /// itself. Switching `Behavior.MapPanFix` off multiplies the game's own 150 and frame time back in here.
        ///
        /// What is left to work out is the scale between a pixel of cursor movement and the offset the map is drawn
        /// from. The image is placed at `offset * lossyScale`. The game divides the zoom out again after this returns.
        /// So a pixel of movement is `zoom / lossyScale` of offset. `Behavior.MapPanSpeed` scales that, and 1 is one
        /// for one.
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
        /// The map drag's mouse axis, replaced with what the drag asked the map to do this frame. That is the cursor's
        /// own movement, or the push of the stick while `Behavior.MapStick` is on.
        ///
        /// Rewired's axis value is not the pixels the cursor moved. It is in its own units, and it lands on Rewired's
        /// update rather than on the rendered frame. The game treats a displacement as a rate. Handing the pan the
        /// cursor's own movement leaves nothing in between. Adding that movement once per rendered frame stops the map
        /// from arriving in bursts.
        ///
        /// The signs are the ones the game's own arithmetic expects. They are read from what the game does with each
        /// axis: `Tilt View` arrives already inverted. That is why the game's line negates only `Pan View`.
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
        /// What the stick asks the map to do this frame. It is the cursor's push away from where the drag started,
        /// taken as a speed and scaled by the length of the frame. As a result, the map goes on moving while the mouse
        /// is still.
        ///
        /// The push is measured from the press point, not from the map. That measurement is what makes it a stick: the
        /// further the cursor is held from where the drag began, the faster the map travels. `MapStickGain` is what
        /// each pixel of push buys, and there is no ceiling on it. Twice the push is twice the speed, and the edge of
        /// the screen is the only limit.
        ///
        /// Nothing happens until the push passes the game's own drag threshold. That threshold is `UnitSelection`'s,
        /// and it is the same one the mod's marquee starts on. The speed is counted from the push that passed the
        /// threshold, not from the press point. So it starts at nothing instead of stepping.
        ///
        /// The map travels the other way from the push, because the stick scrolls the view. Push up, and the map's own
        /// content goes down, which is north.
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

        /// <summary>Throttle for the pan report, the frame it was last measured on, and the totals it reports.</summary>
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
        /// The cursor movement since the last frame of the current drag. This method measures it once per frame,
        /// however often the drag asks for it, and feeds the report below.
        ///
        /// A frame that does not continue the drag has no movement. So a press is not charged with wherever the cursor
        /// has been since the last one. Such a frame is where the drag started. It is also where the stick's press
        /// point comes from.
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

            // Only a frame that follows the one the drag last used belongs to the same drag. The first frame of a new
            // drag, and the first after the map was closed or the camera jumped, are not movement this drag asked for.
            // Charging them with everything the cursor did since is what made the map jump on every press.
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
        /// Reports the totals the drag has run up over the last second.
        ///
        /// The two pairs are what the cursor moved and what the map moved over the same span. The ratio between them
        /// answers whether the map keeps up. A full second of frames keeps the report readable, where one frame does
        /// not. A single frame also carries the camera follow, a clamped edge, and whatever the frame rate did to the
        /// length of the step. A ratio of one means the map moved exactly as far as the cursor.
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

        /// <summary>Whether an icon can exist for this unit at all, which is what the queue tests.</summary>
        internal static bool CanHaveIcon(Unit unit)
        {
            return unit != null
                && !unit.disabled
                && unit.definition != null
                && unit.definition.mapIconSize > 0f;
        }
    }

    /// <summary>
    /// Fires the event DynamicMap.DeselectAllIcons forgets, so the Target List panel drops its rows with the
    /// selection.
    ///
    /// That method is a manual clear. It deselects every icon, empties selectedIcons, and fires neither
    /// onUnitDeselected nor onAllDeselected. TargetListSelector builds its rows from those two events and nothing
    /// else. So every row survives a clear done this way.
    ///
    /// The panel is then left showing a target that nothing holds. There is no lit icon on the map and no entry in
    /// the weapon list. Nothing tells the row apart from a real target, except that clicking it does nothing.
    ///
    /// Unselect all is the gesture a player reaches for. That gesture is CombatHUD.DeselectAll, which goes through
    /// this same method. So the clear leaves the row where it was. The row outlives the selection for as long as the
    /// player accepts it.
    ///
    /// Vanilla reaches this in normal play: Aircraft.OnStartClient, which is every respawn,
    /// AircraftSelectionMenu.OnEnable, which is the spawn screen, and AirbaseMapIcon.ClickIcon. The row is left behind
    /// for any unit, a wreck or not.
    ///
    /// Calling the event rather than the panel keeps every other subscriber of a clear in the loop, which is what the
    /// game meant to do. The count in the log comes from a prefix, because the body has already emptied the list by the
    /// time a postfix runs.
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
    /// Keeps the wheel out of the camera states that do not ask whether they should have it.
    ///
    /// "Zoom View" is the wheel, and two things want it. DynamicMap.MapControls zooms the map with it, which is what
    /// it is for while the map is up. The camera states move their own view with it.
    ///
    /// CameraChaseState and CameraOrbitState do their wheel work inside `if (!Cursor.visible)`. That is the question
    /// that matters, because the cursor is up for every menu, every dialog and the map. CameraCockpitState asks only
    /// whether the map is maximized. CameraTVState, the flyby camera, asks nothing at all.
    ///
    /// So a scroll inside a dialog moved the field of view of the cockpit behind it. The same scroll in the flyby view
    /// moved its field of view with the map open. Both cases are the same missing question. So the mask covers both
    /// reads while the cursor is up, which is the question the other two states already ask.
    ///
    /// The mask is inserted after the axis read, and does not replace the call. So the stack and the evaluation order
    /// stay as they were. The method's own logic gets a zero instead of a scroll. The cockpit's own map check stays
    /// where it is underneath.
    /// </summary>
    internal static class CameraWheelMask
    {
        internal static IEnumerable<CodeInstruction> Apply(
            IEnumerable<CodeInstruction> instructions, string method, out int masked)
        {
            var getAxis = AccessTools.Method(typeof(Player), nameof(Player.GetAxis), new[] { typeof(string) });
            var mask = AccessTools.Method(typeof(VanillaFixes), nameof(VanillaFixes.WheelWhileCursorUp));

            var patched = 0;
            var result = new List<CodeInstruction>();
            string axis = null;

            foreach (var instruction in instructions)
            {
                result.Add(instruction);

                if (instruction.opcode == OpCodes.Ldstr)
                {
                    axis = instruction.operand as string;
                    continue;
                }

                if (getAxis != null && mask != null && instruction.Calls(getAxis) && axis == "Zoom View")
                {
                    result.Add(new CodeInstruction(OpCodes.Call, mask));
                    patched++;
                }

                axis = null;
            }

            masked = patched;

            if (patched != 1)
            {
                Plugin.Log.LogWarning(
                    "Expected exactly one Zoom View read in " + method + " but found " + patched
                    + ". Scrolling with a menu open, or with the map open in the flyby view, will go on moving the camera.");
            }

            return result;
        }
    }

    /// <summary>
    /// The cockpit camera, whose own gate only asks whether the map is maximized. A menu shows the cursor and leaves
    /// the map minimized. That is how a scroll in a dialog used to change the field of view of a flying cockpit.
    /// </summary>
    [HarmonyPatch(typeof(CameraCockpitState), "UpdateState")]
    internal static class CameraCockpitStateWheelMaskPatch
    {
        /// <summary>Call sites masked, or -1 when the method never ran through the transpiler.</summary>
        internal static int Masked { get; private set; } = -1;

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var result = CameraWheelMask.Apply(instructions, "CameraCockpitState.UpdateState", out var masked);
            Masked = masked;
            return result;
        }
    }

    /// <summary>
    /// The flyby camera, which reads the wheel with no gate of its own at all. Scrolling with the map open moved its field
    /// of view as well as zooming the map, confirmed in play 2026-09-21.
    /// </summary>
    [HarmonyPatch(typeof(CameraTVState), "UpdateState")]
    internal static class CameraTVStateWheelMaskPatch
    {
        /// <summary>Call sites masked, or -1 when the method never ran through the transpiler.</summary>
        internal static int Masked { get; private set; } = -1;

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var result = CameraWheelMask.Apply(instructions, "CameraTVState.UpdateState", out var masked);
            Masked = masked;
            return result;
        }
    }

    /// <summary>
    /// Makes the map follow the cursor while it is being dragged, instead of outrunning it or lagging behind it.
    ///
    /// DynamicMap.MapControls pans by `axis * 150 * min(Time.unscaledDeltaTime, 0.03) / zoom`, summed every frame. The
    /// 150 says the pan was written as if it were a rate, but the axis carries a displacement the mouse made.
    /// Multiplying by frame time makes one drag of the same length move the map further the slower the game runs. It
    /// also moves the map further than the cursor at every frame rate. That is the bug this replaces. The axis-bound
    /// path just above reads `Move Map Horizontal`. That axis is held and does want frame time. The mouse axis is not
    /// and does not. Two things follow from that.
    ///
    /// The frame time goes. What the pan is left with is the scale between a pixel of cursor movement and the offset
    /// the map is drawn from. Removing the frame time alone is not enough, as an earlier attempt showed. The divisor
    /// the game uses is the map scale center's scale, the zoom the map was asked for. The map image's own drawn scale
    /// turns the pan into a position. That scale is a different number, because SetZoomLevel keeps the image's world
    /// scale as it moves the image between parents. `VanillaFixes.PanStep` puts that right.
    ///
    /// The axis itself goes too, and `VanillaFixes.PanAxis` hands the pan the cursor's own movement for the frame
    /// instead. Rewired's axis value is smaller than the movement that produced it. It lands on Rewired's update, not
    /// on the rendered frame. So a pan built from it moves in bursts, however well it is scaled. The report in
    /// `VanillaFixes` measured the axis carrying 0.44 of the cursor's movement per frame. The per-frame ratio was
    /// scattered between 0.2 and 12. Both replacements are calls inserted after the values they replace. So the
    /// arithmetic around them and the stack are untouched. With `Behavior.MapPanFix` off, the game's own values come
    /// back.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), "MapControls")]
    internal static class MapPanProportionPatch
    {
        /// <summary>Frame-time call sites replaced, or -1 when the method never ran through the transpiler.</summary>
        internal static int CallSites { get; private set; } = -1;

        /// <summary>Mouse axis call sites replaced, or -1 when the method never ran through the transpiler.</summary>
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
    /// Returns no icon instead of throwing when the unit cannot have one. Callers that need an icon still get
    /// the cached one or a freshly created one, exactly as before.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.GetOrAddIcon))]
    internal static class DynamicMapGetOrAddIconPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(DynamicMap __instance, Unit unit, ref UnitMapIcon __result)
        {
            if (unit == null || DynamicMap.TryGetMapIcon(unit, out _) || VanillaFixes.CanHaveIcon(unit))
            {
                return true;
            }

            // Vanilla would queue the icon, get nothing back, and throw on iconLookup[unit].
            __result = null;
            Plugin.Verbose("No map icon for " + unit.name + ", which cannot have one.");
            return false;
        }
    }

    /// <summary>
    /// Skips the incoming missile warning when there is no icon to hang it on.
    /// </summary>
    [HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.FlagIncomingMissile))]
    internal static class DynamicMapFlagIncomingMissilePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Unit missileUnit)
        {
            if (missileUnit == null)
            {
                return false;
            }

            if (DynamicMap.TryGetMapIcon(missileUnit, out _) || VanillaFixes.CanHaveIcon(missileUnit))
            {
                return true;
            }

            Plugin.Verbose("Skipped the incoming missile flag for " + missileUnit.name + ": no map icon for it.");
            return false;
        }
    }

    /// <summary>
    /// Skips a radar warning highlight for a unit that has no HUD marker to highlight.
    ///
    /// CombatHUD.CreateMarker decides once, when the game first maps a unit, whether that unit gets a marker. It
    /// refuses a unit that is disabled, a unit that is Scenery, and a unit that turns up while the player has no
    /// aircraft. HighlightMarker then indexes markerLookup directly, so a radar warning about one of those units throws,
    /// where no marker exists to draw. A radar contact that was already wrecked when it was detected is the everyday
    /// case. No marker is created for it, and the warning for it would be the crash.
    /// </summary>
    [HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.HighlightMarker))]
    internal static class CombatHudHighlightMarkerPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CombatHUD __instance, Unit unit)
        {
            if (unit != null && !__instance.TryGetMarker(unit, out _))
            {
                Plugin.Verbose("Skipped the highlight for " + unit.name + ": it has no HUD marker.");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Releases a unit that the weapon target list has nothing to say about.
    ///
    /// CombatHUD.DeSelectUnit returns early when the target list is empty. That return skips the rest of the method,
    /// the map icon included. An empty list is exactly the case this mod walks into. A unit selected in commander
    /// view keeps its selection into the cockpit, with no list entry. A wreck can never be a weapon target at all,
    /// so it is never in the list, whatever selected it. Every deselect path that routes through here then does
    /// nothing, and they are the ones a player reaches. The paths are TargetListSelector.ForceDeselect, the pane's
    /// own exclusion pass, and TargetListSelector_UnitItem.ToggleSelect, the row in that panel. Unselect all was the
    /// only thing left that could clear the selection.
    ///
    /// Where vanilla would give up, this patch does the map half instead. DynamicMap.DeselectIcon is where the rest
    /// of the method ends anyway. It is silent, which is what a release with no list entry should be.
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

    /// <summary>
    /// Makes every colour rebuild go through the unit icon's own update. That update is the only one that knows about
    /// the Target List filters and about wrecks.
    ///
    /// MapIcon.UpdateColor sets the colour from the theme and stops there. The filter dimming and this mod's wreck
    /// tint live one level down, in UnitMapIcon.UnitMapIcon_UpdateColor. So everything that calls the base method
    /// directly draws a filtered out unit, or a wreck, as if it were still alive. MapIcon.SelectIcon and
    /// MapIcon.DeselectIcon call the base method directly. DynamicMap.UnselectAll calls DeselectIcon on every icon
    /// on the map, whether it was selected or not. DynamicMap.UpdateUnitFaction and DynamicMap_OnFactionChanged call
    /// UpdateColor themselves.
    ///
    /// The depth counter keeps this from compounding. UnitMapIcon_UpdateColor calls the base method itself. Without
    /// the counter, the postfix would run the update again inside its own call and apply the tint twice. The patch
    /// uses a finalizer rather than a postfix, so an exception cannot leave the counter raised and silently disable
    /// the fix.
    /// </summary>
    [HarmonyPatch(typeof(UnitMapIcon), "UnitMapIcon_UpdateColor")]
    internal static class UnitMapIconDepthPatch
    {
        internal static int Depth;

        [HarmonyPrefix]
        private static void Prefix()
        {
            Depth++;
        }

        [HarmonyFinalizer]
        private static void Finalizer()
        {
            Depth--;
        }
    }

    /// <summary>
    /// Sends a colour rebuild that bypassed the unit icon's update back through it.
    /// </summary>
    [HarmonyPatch(typeof(MapIcon), nameof(MapIcon.UpdateColor))]
    internal static class MapIconUpdateColorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MapIcon __instance)
        {
            if (UnitMapIconDepthPatch.Depth == 0 && __instance is UnitMapIcon icon)
            {
                icon.UnitMapIcon_UpdateColor();
            }
        }
    }
}
