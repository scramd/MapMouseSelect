using NuclearOption.MissionEditorScripts;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Moves the map's camera jump from the middle mouse button to a double left click.
    ///
    /// Vanilla jumps the commander's camera to the cursor when the Jump Map action goes down, and that
    /// action is on the middle mouse button by default, which this mod took for panning. Rather than let
    /// the two fight over the same press, the jump is answered here and the prefix on DynamicMap.JumpCameraTo
    /// stops vanilla's own branch from doing it as well.
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

        private static float _previousPress = -1f;
        private static Vector2 _previousPressPosition;

        /// <summary>True while the jump in flight is this mod's, which is when vanilla's call is allowed.</summary>
        internal static bool Requested { get; private set; }

        internal static void Tick()
        {
            var map = SceneSingleton<DynamicMap>.i;
            if (map == null || !DynamicMap.mapMaximized)
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

            // The prefix on the vanilla method reads this, so the flag has to be up around the call.
            Requested = true;
            bool jumped;
            try
            {
                jumped = VanillaCalls.JumpCameraTo(map, target);
            }
            finally
            {
                Requested = false;
            }

            if (jumped)
            {
                Plugin.Verbose("Double click: jumped the camera to the cursor.");
            }
        }
    }
}
