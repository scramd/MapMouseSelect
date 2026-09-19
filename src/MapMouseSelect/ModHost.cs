using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// The mod's own per-frame component.
    ///
    /// BepInEx attaches plugins to its own manager object, and in this game that object does not survive
    /// startup: every plugin's OnDestroy runs right after "Chainloader startup complete". The established
    /// mods here work around it the same way, by making a GameObject of their own and marking it
    /// DontDestroyOnLoad, which is what this does. It is created lazily and re-created if it ever goes
    /// missing, so it cannot be lost by whatever happens during the game's boot.
    ///
    /// Work that has to preempt vanilla input stays in Harmony patches instead, because it has to run
    /// before the game acts on the same input in the same frame.
    /// </summary>
    internal sealed class ModHost : MonoBehaviour
    {
        private const string HostName = "MapMouseSelect.Host";

        private static ModHost _instance;
        private static bool _survivalReported;

        internal static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(HostName);
            Object.DontDestroyOnLoad(host);

            // Keeps the object out of the hierarchy and out of Resources.UnloadUnusedAssets, which the
            // game calls on its way between scenes.
            host.hideFlags = HideFlags.HideAndDontSave;

            _instance = host.AddComponent<ModHost>();
        }

        private void Update()
        {
            TargetKeys.Tick();
            ModifierGuard.Tick();

            // One line a session answering whether this object outlived the boot, which is exactly what
            // the plugin's own object did not.
            if (!_survivalReported && Time.timeSinceLevelLoad > 60f)
            {
                _survivalReported = true;
                Plugin.Verbose("Mod host is still ticking after 60s of level time.");
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
