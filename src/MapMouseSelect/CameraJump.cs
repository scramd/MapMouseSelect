using System.Collections.Generic;
using NuclearOption.MissionEditorScripts;
using Rewired;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Moves the map's camera jump from the middle mouse button to a double left click.
    ///
    /// Vanilla jumps the commander's camera to the cursor when the Jump Map action goes down, and that
    /// action is on the middle mouse button by default, which this mod took for panning. The binding is what
    /// moves out of the way: while the map is open the action's binding on the pan button is disabled, so
    /// Rewired reports nothing for it from that button, and the jump is answered here instead, from a double
    /// click. The action keeps every other binding, a gamepad button included.
    ///
    /// A double click is the game's idea of one: uGUI counts a second press as a further click when it
    /// arrives within 0.3s of the previous press, and the two have to land on the same object, which is
    /// read here as the game's drag threshold saying the pointer did not travel. That threshold helper
    /// compares against the cursor as it is now, so asking it about the previous press position at the
    /// moment of the second press is exactly "did the pointer stay where it was".
    ///
    /// Only commander view has the jump, which is when vanilla's branch runs at all: with a live aircraft
    /// the Jump Map action does nothing.
    ///
    /// Ticked from ModHost, along with the rest of the mod's per-frame work.
    /// </summary>
    internal static class CameraJump
    {
        /// <summary>uGUI's own window for counting a press as another click of the same double click.</summary>
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
        /// Holds the Jump Map action off the button this mod pans with, for as long as the map is open.
        ///
        /// Disabling that one binding is what separates the pan from the jump: Rewired then reports nothing
        /// for the action from that button, so vanilla's own branch cannot fire, and every other binding the
        /// action has, a gamepad button included, is left exactly as it was. Nothing has to infer where a
        /// press came from, and the binding goes back the moment the map closes.
        ///
        /// The button is matched by the name Rewired gives its mouse elements, which is the HID numbering:
        /// the middle button is element 5, "Mouse Button 3". That came out of the log rather than out of any
        /// documentation, so what the action is bound to on the mouse is logged once: if the name is not the
        /// one expected here, the log says what it actually is and the button simply keeps its binding.
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
        /// The name Rewired gives the mouse button this mod pans with, in HID numbering: the middle button,
        /// which is what the Jump Map binding turned out to be (element 5), is "Mouse Button 3", so left is
        /// 1 and right is 2. The middle one was read off this install; left and right follow the same
        /// numbering rather than being observed.
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
