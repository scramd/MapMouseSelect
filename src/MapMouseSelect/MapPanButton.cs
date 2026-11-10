using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>Which mouse button pans the tactical map – Unity's button index for
    /// <see cref="Input.GetMouseButton"/>.</summary>
    public enum MapPanButton
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    internal static class PanButtonCompat
    {
        /// <summary>Stands in for the hardcoded <c>Input.GetMouseButton(0)</c> in <c>DynamicMap.MapControls</c>,
        /// the only <c>GetMouseButton</c> call there – swapping this one moves panning alone.</summary>
        internal static bool GetMouseButton(int vanillaButtonIndex)
        {
            var button = Plugin.PanButton != null ? (int)Plugin.PanButton.Value : vanillaButtonIndex;
            return Input.GetMouseButton(button);
        }

        /// <summary>Stands in for <c>Input.GetMouseButtonDown(1)</c> in <c>DynamicMap.MapControls</c>, the
        /// waypoint order. While the map is dragged with that button, the order waits for a click without a drag,
        /// so a drag does not also send the selected units somewhere.</summary>
        internal static bool GetMouseButtonDown(int vanillaButtonIndex)
        {
            if (Plugin.PanButton == null || (int)Plugin.PanButton.Value != vanillaButtonIndex)
            {
                return Input.GetMouseButtonDown(vanillaButtonIndex);
            }

            return PanClick.ClickedThisFrame(SceneSingleton<DynamicMap>.i);
        }
    }

    /// <summary>
    /// Tells a click of the pan button from a drag. The game acts on that button the moment it goes down, which
    /// would fire at the start of every map drag. The click is reported once, on the frame the button is released
    /// after a press on the map that never passed the game's drag threshold. A drag that returns to where it
    /// started is still a drag. Evaluated once per frame, whoever asks first.
    /// </summary>
    internal static class PanClick
    {
        private static int _frame = -1;
        private static bool _pressed;
        private static bool _dragged;
        private static bool _clicked;
        private static Vector2 _pressPosition;

        internal static bool ClickedThisFrame(DynamicMap map)
        {
            if (Time.frameCount == _frame)
            {
                return _clicked;
            }

            _frame = Time.frameCount;
            _clicked = false;

            if (map == null || !DynamicMap.mapMaximized || Plugin.PanButton == null)
            {
                _pressed = false;
                return false;
            }

            var button = (int)Plugin.PanButton.Value;

            if (Input.GetMouseButtonDown(button))
            {
                _pressed = map.IsCursorInMapRectangle();
                _dragged = false;
                _pressPosition = Input.mousePosition;
            }

            if (!_pressed)
            {
                return false;
            }

            if (NuclearOption.MissionEditorScripts.UnitSelection.DragThresholdPassed(_pressPosition))
            {
                _dragged = true;
            }

            if (Input.GetMouseButtonUp(button))
            {
                _pressed = false;
                _clicked = !_dragged;
            }

            return _clicked;
        }
    }
}
