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
    /// The world slows with the cap, and a frame's delta is what decides by how much. Unity spends at most
    /// Time.maximumDeltaTime of game time on one frame, so once a frame is longer than that the world falls behind by
    /// the difference. Measured, with the ratio of Time.time to real time read once a second: a cap of 1 kept 0.10 of
    /// real time, a cap of 5 kept 0.50, and a cap of 10 kept 1.00, which is that limit at about 0.1s a frame. At 10 and
    /// above the world advances in real time, and below it the world slows while the frame rate and the GPU load fall
    /// with the cap.
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
        private static int _reports;
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
                _reports = 0;

                // The baselines are set here so the first window is a real second rather than everything since the game
                // started, which would read as a much slower world and warn about it.
                _worldTime = Time.time;
                _realTime = Time.realtimeSinceStartup;
                _nextReport = Time.realtimeSinceStartup + 1f;
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
        /// Says once a real second how many frames were taken, how much of real time the world kept, and the delta limit
        /// that decides it. The second number is the point: one means the world advanced in real time at that frame
        /// rate, and less means it fell behind.
        ///
        /// The window is real time, not level time. Level time runs at the world's own rate, so counting frames in a
        /// level second would report the same count at every cap and mean nothing.
        /// </summary>
        private static void Report()
        {
            if (!_capped || !Plugin.VerboseLogging.Value)
            {
                _frames = 0;
                return;
            }

            _frames++;

            if (Time.realtimeSinceStartup <= _nextReport)
            {
                return;
            }

            _nextReport = Time.realtimeSinceStartup + 1f;
            _reports++;

            var world = Time.time - _worldTime;
            var real = Time.realtimeSinceStartup - _realTime;

            _worldTime = Time.time;
            _realTime = Time.realtimeSinceStartup;

            var kept = real > 0f ? world / real : 1f;

            Plugin.Verbose(
                "Frame limit: " + _frames + " frames in the last real second, the world kept " + kept.ToString("0.00")
                + " of real time, a frame spends at most " + Time.maximumDeltaTime.ToString("0.###")
                + "s of it, timeScale " + Time.timeScale + ".");

            if (kept < 0.9f && _reports > 1 && !_warned)
            {
                _warned = true;
                Plugin.Log.LogWarning(
                    "Frame limit: the world kept " + kept.ToString("0.00") + " of real time at a cap of "
                    + Application.targetFrameRate + ". Below 10 the world falls behind, and the GPU load falls with the "
                    + "frame rate.");
            }

            _frames = 0;
        }
    }
}
