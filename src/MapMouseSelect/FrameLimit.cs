using NuclearOption.Networking;
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
    /// Time.maximumDeltaTime of game time on one frame, so a frame longer than that loses the surplus. It logs as 0.1s
    /// here, which puts the threshold at 10 frames a second. Measured, reading the ratio of Time.time to real time once
    /// a second: a cap of 1 kept 0.10 of real time, a cap of 5 kept 0.50, a cap of 10 kept 0.95 to 1.05, a cap of 11
    /// kept 0.98 to 1.01, and a cap of 12 kept 0.99 to 1.01. Ten sits on the threshold with no margin, so the floor is
    /// one above it, 11 here, and the default is 12, a frame of headroom for a hitch at the cost of a frame a second.
    /// FrameLimitAllowWorldSlowdown lifts the floor and takes the slow world.
    ///
    /// A host hands the cap back while players are connected, since it answers them on the frames it takes. A client
    /// is left to the floor, which is what keeps its world clock straight.
    ///
    /// vsync comes off for the cap to mean anything, since Unity ignores targetFrameRate while vSyncCount is above
    /// zero. Both values are kept on the way out of focus and put back on the way in, and re-asserted while unfocused,
    /// because the game writes them itself when the graphics settings change.
    /// </summary>
    internal static class FrameLimit
    {
        private const int MaxRate = 240;

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
            var unfocused = Plugin.FrameLimitUnfocused.Value && !Application.isFocused;
            var wanted = unfocused && (Plugin.FrameLimitWhileHosting.Value || !Hosting());

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
                    "Frame limit: off" + (unfocused ? " while hosting with players connected" : "")
                    + ", back to " + _gameFrameRate + " and vsync " + _gameVsync + ".");
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
            var rate = Mathf.Clamp(Plugin.FrameLimitUnfocusedFps.Value, Floor(), MaxRate);

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
        /// Lowest rate the limiter applies.
        ///
        /// A frame spends at most Time.maximumDeltaTime of game time, so one above the reciprocal of it is the lowest
        /// rate that keeps the world in real time: 11 at the 0.1s this game logs. FrameLimitAllowWorldSlowdown drops
        /// the floor to 1 and takes the slow world that follows.
        ///
        /// The config range cannot carry this, because Bind fixes it at startup and the game has not set its own
        /// value that early. The file may hold anything, and this clamp is what decides.
        /// </summary>
        private static int Floor()
        {
            if (Plugin.FrameLimitAllowWorldSlowdown.Value)
            {
                return 1;
            }

            var limit = Time.maximumDeltaTime;
            if (limit <= 0f)
            {
                return MaxRate;
            }

            return Mathf.Min(MaxRate, Mathf.CeilToInt(1f / limit) + 1);
        }

        /// <summary>
        /// True while this process runs a game server that someone other than the host is connected to. This is the
        /// server-with-players test the game's own debug panel uses, which covers a host with a client and a headless
        /// server with one, and leaves a single-player offline host alone.
        ///
        /// A null manager means the game has not preloaded one yet, and reading it in that state logs an error, so this
        /// is only called while the cap is on and the caller has not opted out.
        /// </summary>
        private static bool Hosting()
        {
            var manager = NetworkManagerNuclearOption.i;
            if (manager == null)
            {
                return false;
            }

            var server = manager.Server;
            if (server == null || !server.Active)
            {
                return false;
            }

            var players = server.AllPlayers;
            if (players.Count >= 2)
            {
                return true;
            }

            foreach (var player in players)
            {
                if (!player.IsHost)
                {
                    return true;
                }
            }

            return false;
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
                    + Application.targetFrameRate + ". A frame longer than Time.maximumDeltaTime loses the surplus it "
                    + "should have carried, which is what FrameLimitAllowWorldSlowdown permits.");
            }

            _frames = 0;
        }
    }
}
