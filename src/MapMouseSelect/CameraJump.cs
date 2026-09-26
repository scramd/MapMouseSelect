using System.Collections.Generic;
using NuclearOption.MissionEditorScripts;
using Rewired;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Moves the map's camera jump from the middle mouse button to a double left click.
    ///
    /// Vanilla jumps the commander's camera to the cursor when the Jump Map action goes down, and that action
    /// sits on the middle button by default, which this mod took for panning. While the map is open that binding
    /// is disabled, so Rewired reports nothing from the button and the jump is answered here. Only commander
    /// view has the jump. The drag threshold reports the presses by comparing its current cursor against the
    /// previous press position.
    /// </summary>
    internal static class CameraJump
    {
        /// <summary>uGUI's own window for counting a press as another click of a double click.</summary>
        private const float DoubleClickWindow = 0.3f;

        private const string ActionName = "Jump Map";

        private static readonly List<ControllerMap> MouseMaps = new List<ControllerMap>();
        private static readonly List<ActionElementMap> HeldOff = new List<ActionElementMap>();

        private static float _previousPress = -1f;
        private static Vector2 _previousPressPosition;
        private static bool _reportedBindings;

        internal static void Tick()
        {
            var map = SceneSingleton<DynamicMap>.i;
            var open = map != null && DynamicMap.mapMaximized;

            HoldJumpButtonOff(open);

            if (!open)
            {
                _previousPress = -1f;
                return;
            }

            Aircraft localAircraft;
            if (GameManager.GetLocalAircraft(out localAircraft))
            {
                _previousPress = -1f;
                return;
            }

            if (!Input.GetMouseButtonDown(0) || !map.IsCursorInMapRectangle())
            {
                return;
            }

            var now = Time.unscaledTime;
            var position = (Vector2)Input.mousePosition;

            var second = _previousPress >= 0f
                && now - _previousPress <= DoubleClickWindow
                && !UnitSelection.DragThresholdPassed(_previousPressPosition);

            _previousPress = now;
            _previousPressPosition = position;

            if (!second)
            {
                return;
            }

            // A third press should start a new pair rather than jump again.
            _previousPress = -1f;

            GlobalPosition target;
            if (!map.TryGetCursorCoordinates(out target))
            {
                return;
            }

            if (VanillaCalls.JumpCameraTo(map, target))
            {
                Plugin.Verbose("Double click: jumped the camera to the cursor.");
            }
        }

        /// <summary>
        /// Holds the Jump Map action off the pan button while the map is open, so Rewired reports nothing for it
        /// from that button and vanilla's branch cannot fire. The button is matched by Rewired's mouse element
        /// name, so middle is element 5, "Mouse Button 3", read off the log rather than documentation.
        /// </summary>
        private static void HoldJumpButtonOff(bool open)
        {
            if (!open)
            {
                ReleaseJumpButton();
                return;
            }

            if (HeldOff.Count > 0 || Plugin.PanButton == null)
            {
                return;
            }

            var player = GameManager.playerInput;
            if (player == null)
            {
                return;
            }

            var actionId = ReInput.mapping.GetActionId(ActionName);
            if (actionId < 0)
            {
                return;
            }

            var button = PanButtonName();

            MouseMaps.Clear();
            player.controllers.maps.GetAllMaps(ControllerType.Mouse, MouseMaps);

            for (var i = 0; i < MouseMaps.Count; i++)
            {
                var mouseMap = MouseMaps[i];
                if (mouseMap == null || !mouseMap.enabled)
                {
                    continue;
                }

                var elements = mouseMap.AllMaps;
                for (var j = 0; j < elements.Count; j++)
                {
                    var element = elements[j];
                    if (element == null || !element.enabled || element.actionId != actionId)
                    {
                        continue;
                    }

                    if (!_reportedBindings)
                    {
                        Plugin.Verbose(
                            ActionName + " is bound on the mouse to element " + element.elementIdentifierId
                            + " (index " + element.elementIndex + ", '" + element.elementIdentifierName + "').");
                    }

                    if (element.elementIdentifierName != button)
                    {
                        continue;
                    }

                    element.enabled = false;
                    HeldOff.Add(element);
                    Plugin.Verbose(
                        "Held " + ActionName + " off '" + element.elementIdentifierName
                        + "' while the map is open, so the pan button does not also move the camera.");
                }
            }

            _reportedBindings = true;
        }

        /// <summary>
        /// The Rewired name of the pan button, in HID numbering: middle is "Mouse Button 3", which the Jump Map
        /// binding turned out to be (element 5). Left is 1, right is 2.
        /// </summary>
        private static string PanButtonName()
        {
            switch (Plugin.PanButton.Value)
            {
                case MapPanButton.Left:
                    return "Mouse Button 1";
                case MapPanButton.Right:
                    return "Mouse Button 2";
                default:
                    return "Mouse Button 3";
            }
        }

        private static void ReleaseJumpButton()
        {
            if (HeldOff.Count == 0)
            {
                return;
            }

            for (var i = 0; i < HeldOff.Count; i++)
            {
                if (HeldOff[i] != null)
                {
                    HeldOff[i].enabled = true;
                }
            }

            Plugin.Verbose("Put " + ActionName + " back on the mouse.");
            HeldOff.Clear();
        }
    }
}
