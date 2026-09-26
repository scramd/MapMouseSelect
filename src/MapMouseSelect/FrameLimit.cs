using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Caps the frame rate while the game is not focused.
    ///
    /// An unfocused window keeps the whole loop at full rate, which holds the CPU and the GPU for a session nobody is
    /// looking at. Skipping rendering does not settle it: the loop simply runs faster, and the work per second stays
    /// where it was. A cap is the lever that does settle it.
    ///
    /// The world slows with the cap, and the numbers are measured. With the loop held to the cap and the ratio of
    /// Time.time to real time read once a second: a cap of 1 kept 0.10 of real time, a cap of 5 kept 0.50, and a cap of
    /// 10 kept 1.00. The loop itself ran at 10 or 11 frames a second in all three, so below 10 the world slows without
    /// the frame rate falling any further, and 10 is the lowest cap that leaves the world alone.
    ///
    /// vsync comes off for the cap to mean anything, since Unity ignores targetFrameRate while vSyncCount is above
    /// zero. Both values are kept on the way out of focus and put back on the way in, and re-asserted while unfocused,
    /// because the game writes them itself when the graphics settings change.
    /// </summary>
    internal static class FrameLimit
    {
        private static bool _capped;
        private static int _gameFrameRate;
        private static int _gameVsync;

        private static int _frames;
        private static float _nextReport;
        private static float _worldTime;
        private static float _realTime;
        private static bool _warned;

        internal static void Tick()
        {
            var wanted = Plugin.FrameLimitUnfocused.Value && !Application.isFocused;

            if (wanted && !_capped)
            {
                _capped = true;
                _gameFrameRate = Application.targetFrameRate;
                _gameVsync = QualitySettings.vSyncCount;
                _warned = false;
                Apply();

                Plugin.Verbose(
                    "Frame limit: " + Application.targetFrameRate + " while the game is not focused, from "
                    + _gameFrameRate + " and vsync " + _gameVsync + ". The game loop runs at that rate too.");
            }
            else if (!wanted && _capped)
            {
                _capped = false;
                Application.targetFrameRate = _gameFrameRate;
                QualitySettings.vSyncCount = _gameVsync;

                Plugin.Verbose(
                    "Frame limit: off, back to " + _gameFrameRate + " and vsync " + _gameVsync + ".");
            }
            else if (wanted)
            {
                Apply();
            }

            Report();
        }

        /// <summary>
        /// Puts the cap on, in case the game has written its own values since the last frame.
        /// </summary>
        private static void Apply()
        {
            var rate = Mathf.Clamp(Plugin.FrameLimitUnfocusedFps.Value, 1, 240);

            if (Application.targetFrameRate != rate)
            {
                Application.targetFrameRate = rate;
            }

            if (QualitySettings.vSyncCount != 0)
            {
                QualitySettings.vSyncCount = 0;
            }
        }

        /// <summary>
        /// Says once a second how many frames were taken, and how much of real time the world kept. The second number is
        /// the point of the exercise: one means the fixed step kept up with the clock at that frame rate, and less means
        /// Unity dropped time and the world really did slow.
        /// </summary>
        private static void Report()
        {
            if (!_capped || !Plugin.VerboseLogging.Value)
            {
                _frames = 0;
                return;
            }

            _frames++;

            if (Time.timeSinceLevelLoad <= _nextReport)
            {
                return;
            }

            _nextReport = Time.timeSinceLevelLoad + 1f;

            var world = Time.time - _worldTime;
            var real = Time.realtimeSinceStartup - _realTime;

            _worldTime = Time.time;
            _realTime = Time.realtimeSinceStartup;

            Plugin.Verbose(
                "Frame limit: " + _frames + " frames in the last second, the world kept "
                + (real > 0f ? (world / real).ToString("0.00") : "-") + " of real time, timeScale "
                + Time.timeScale + ".");

            if (real > 0f && world / real < 0.9f && !_warned)
            {
                _warned = true;
                Plugin.Log.LogWarning(
                    "Frame limit: the world kept " + (world / real).ToString("0.00") + " of real time at a cap of "
                    + Application.targetFrameRate + ". Below 10 the world slows without the frame rate falling further.");
            }

            _frames = 0;
        }
    }
}
