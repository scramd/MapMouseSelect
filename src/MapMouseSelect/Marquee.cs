using System.Collections.Generic;
using NuclearOption.MissionEditorScripts;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Drag state machine for marquee selecting units on the maximized tactical map, driven from DynamicMap's own
    /// per-frame methods: BepInEx destroys its plugin host object at "Chainloader startup complete", so a component
    /// added in Plugin.Awake is gone before gameplay, while a Harmony patch survives. The left mouse button is the
    /// marquee button while the map is maximized, so vanilla controls are suppressed from press to release.
    /// </summary>
    internal static class Marquee
    {
        private static readonly MarqueeOverlay Overlay = new MarqueeOverlay();
        private static readonly List<Unit> Picked = new List<Unit>();

        private static bool _pending;
        private static bool _dragging;
        private static MarqueeMode _mode;
        private static bool _hookReported;
        private static Vector2 _startScreen;

        /// <summary>True while the marquee owns the mouse, when vanilla MapControls must be skipped.</summary>
        internal static bool OwnsMouse => _pending || _dragging;

        /// <summary>Advances the state machine, called from the DynamicMap.Update prefix before MapControls.</summary>
        internal static void Tick(DynamicMap map)
        {
            if (map == null)
            {
                return;
            }

            if (!DynamicMap.mapMaximized)
            {
                _hookReported = false;
                if (_pending || _dragging)
                {
                    Cancel();
                }
                return;
            }

            var inEditor = !Plugin.EnableInMissionEditor.Value && GameManager.gameState == GameState.Editor;

            if (!_hookReported)
            {
                _hookReported = true;
                Plugin.Verbose(
                    "Marquee hook is running with the map maximized. gameState=" + GameManager.gameState
                    + ", bailingForEditor=" + inEditor
                    + ", icons=" + (map.mapIcons != null ? map.mapIcons.Count : -1));
            }

            if (inEditor)
            {
                if (_pending || _dragging)
                {
                    Cancel();
                }
                return;
            }

            var armed = !Plugin.RequireModifier.Value || Plugin.MarqueeModifier.Value.IsPressed();

            if (!_pending && !_dragging)
            {
                if (armed && Input.GetMouseButtonDown(0) && map.IsCursorInMapRectangle())
                {
                    _pending = true;
                    _startScreen = Input.mousePosition;
                    Plugin.Verbose("Marquee armed at " + _startScreen + ".");
                }
                return;
            }

            if (_pending && !_dragging)
            {
                if (!Input.GetMouseButton(0))
                {
                    // A click, not a drag. Mouse clicks reach the icons through their EventSystem handlers, which
                    // this mod never suppresses - MapControls only carries the pan and the Select action.
                    Plugin.Verbose("Marquee released without dragging; the click is vanilla's.");
                    Cancel();
                    return;
                }

                if (UnitSelection.DragThresholdPassed(_startScreen))
                {
                    _dragging = true;
                    Plugin.Verbose("Marquee drag started.");
                    UpdateOverlay();
                }
                return;
            }

            UpdateOverlay();

            if (!Input.GetMouseButton(0))
            {
                Commit(map);
                Cancel();
            }
        }

        private static void Cancel()
        {
            _pending = false;
            _dragging = false;
            Overlay.Hide();
        }

        private static bool ShiftHeld()
        {
            return VanillaCalls.ShiftHeld();
        }

        private static bool ControlHeld()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        /// <summary>What this drag will do when it ends. Control wins over Shift when both are held, and the
        /// border shows which one is in force.</summary>
        private static MarqueeMode CurrentMode()
        {
            if (Plugin.ControlRemovesFromSelection.Value && ControlHeld())
            {
                return MarqueeMode.Remove;
            }

            if (Plugin.ShiftAddsToSelection.Value && ShiftHeld())
            {
                return MarqueeMode.Add;
            }

            return MarqueeMode.Replace;
        }

        private static Color BoxColor(MarqueeMode mode)
        {
            switch (mode)
            {
                case MarqueeMode.Add:
                    return Plugin.BoxOutlineColorAdditive.Value;
                case MarqueeMode.Remove:
                    return Plugin.BoxOutlineColorRemove.Value;
                default:
                    return Plugin.BoxOutlineColor.Value;
            }
        }

        private static Rect ScreenRect()
        {
            var current = (Vector2)Input.mousePosition;
            return Rect.MinMaxRect(
                Mathf.Min(_startScreen.x, current.x),
                Mathf.Min(_startScreen.y, current.y),
                Mathf.Max(_startScreen.x, current.x),
                Mathf.Max(_startScreen.y, current.y));
        }

        private static void UpdateOverlay()
        {
            _mode = CurrentMode();

            if (!Plugin.ShowSelectionBox.Value)
            {
                Overlay.Hide();
                return;
            }

            Overlay.Show(ScreenRect(), BoxColor(_mode));
        }

        private static void Commit(DynamicMap map)
        {
            var box = ScreenRect();
            CollectUnits(map, box, Picked);
            Plugin.Verbose(
                "Marquee released. box=" + box + ", boxed units=" + Picked.Count + ", mode=" + _mode);
            SelectionApplier.Apply(map, Picked, _mode);
        }

        private static void CollectUnits(DynamicMap map, Rect screenRect, List<Unit> results)
        {
            results.Clear();

            var icons = map.mapIcons;
            if (icons == null)
            {
                return;
            }

            var hud = SceneSingleton<CombatHUD>.i;
            var localAircraft = hud != null ? hud.aircraft : null;
            var selector = SceneSingleton<TargetListSelector>.i;

            for (var i = 0; i < icons.Count; i++)
            {
                var unitIcon = icons[i] as UnitMapIcon;
                if (unitIcon == null)
                {
                    continue;
                }

                var unit = unitIcon.unit;
                if (unit == null)
                {
                    continue;
                }

                // An icon switched off in Map Options is an inactive object, and vanilla cannot select one either:
                // clicking needs a raycast hit, and the range select needs the marker's image enabled.
                if (!unitIcon.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // Vanilla map clicks ignore your own aircraft, so the marquee does too.
                if (unit == localAircraft)
                {
                    continue;
                }

                if (!screenRect.Contains(unitIcon.transform.position))
                {
                    continue;
                }

                // Respects the player's own Target List filters, the same gate vanilla clicks use.
                if (selector != null && selector.CheckExclusions(unit))
                {
                    continue;
                }

                results.Add(unit);
            }
        }
    }
}
