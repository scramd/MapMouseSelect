using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// The mod's own per-frame component.
    ///
    /// BepInEx attaches plugins to its own manager object. In this game that object does not survive startup: every
    /// plugin's OnDestroy runs right after "Chainloader startup complete". The established mods here work around it
    /// the same way. They make a GameObject of their own and mark it DontDestroyOnLoad. This does the same. The host
    /// is created lazily and re-created if it ever goes missing. So whatever happens during the game's boot cannot
    /// lose it.
    ///
    /// Work that has to preempt vanilla input stays in Harmony patches. It has to run before the game acts on the
    /// same input in the same frame.
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
            ModActions.EnsureDefaultBindings();
            TargetKeys.Tick();
            ModifierGuard.Tick();
            CameraJump.Tick();
            PanCursor.Tick();

            // One line a session that shows whether this object outlived the boot. The plugin's own object did not.
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
