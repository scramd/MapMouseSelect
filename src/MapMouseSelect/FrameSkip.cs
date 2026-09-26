using UnityEngine;
using UnityEngine.Rendering;

namespace MapMouseSelect
{
    /// <summary>
    /// Draws fewer frames while the game is not focused, and leaves the game loop at its own rate.
    ///
    /// This is for the case where the game still runs while you are looking at something else: an unfocused window
    /// keeps the GPU busy at full rate. `OnDemandRendering.renderFrameInterval` is the engine's own answer to that. The
    /// loop, input and network keep running every frame, and the engine renders one frame in N. This game runs URP,
    /// which is the pipeline that honours the setting.
    ///
    /// The interval is set on the way out of focus and put back on the way in, and the report says how many frames were
    /// drawn out of how many taken, so whether the pipeline honours it is a number in the log rather than an assumption.
    /// </summary>
    internal static class FrameSkip
    {
        /// <summary>The largest interval the setting allows, which is one frame drawn in eight.</summary>
        private const int Longest = 8;

        /// <summary>The interval the game was using before the mod touched it, kept so it can be put back.</summary>
        private static int _gameInterval;

        /// <summary>Whether the mod is the one holding the interval up.</summary>
        private static bool _raised;

        private static int _frames;
        private static int _drawn;
        private static float _nextReport;

        internal static void Tick()
        {
            var wanted = Plugin.FrameSkipUnfocused.Value && !Application.isFocused;
            var interval = Mathf.Clamp(Plugin.FrameSkipInterval.Value, 1, Longest);

            if (wanted && !_raised)
            {
                _gameInterval = OnDemandRendering.renderFrameInterval;
                _raised = true;
                OnDemandRendering.renderFrameInterval = interval;
                Plugin.Verbose("Frame skip: drawing one frame in " + interval + " while the game is not focused.");
            }
            else if (!wanted && _raised)
            {
                _raised = false;
                OnDemandRendering.renderFrameInterval = _gameInterval;
                Plugin.Verbose("Frame skip: off, drawing every frame again.");
            }
            else if (wanted && OnDemandRendering.renderFrameInterval != interval)
            {
                // The game writes its own value when the graphics settings change, so keep it where it was put.
                OnDemandRendering.renderFrameInterval = interval;
            }

            if (!_raised || !Plugin.VerboseLogging.Value)
            {
                _frames = 0;
                _drawn = 0;
                return;
            }

            _frames++;

            if (OnDemandRendering.willCurrentFrameRender)
            {
                _drawn++;
            }

            if (Time.timeSinceLevelLoad <= _nextReport)
            {
                return;
            }

            _nextReport = Time.timeSinceLevelLoad + 1f;
            Plugin.Verbose("Frame skip: drew " + _drawn + " frames of " + _frames + " in the last second.");
            _frames = 0;
            _drawn = 0;
        }
    }
}
