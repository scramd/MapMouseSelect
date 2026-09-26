using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Shows the cursor theme's move cursor while the pan button is held over the map. The game never changes the
    /// cursor's shape itself – CursorManager only decides whether it is visible and locked – so the pan would
    /// otherwise be the plain arrow. Unity owns the pointer, so the theme cursor is handed over as a texture rather
    /// than set on the OS pointer, which flickers. The theme is re-read at each press.
    /// </summary>
    internal static class PanCursor
    {
        private static Texture2D _glyph;
        private static Vector2 _hotspot;
        private static bool _panning;
        private static bool _shown;

        internal static void Tick()
        {
            var panning = Plugin.PanCursor.Value
                && DynamicMap.mapMaximized
                && Cursor.visible
                && PanButtonCompat.GetMouseButton(0);

            var starting = panning && !_panning;
            _panning = panning;

            if (!panning)
            {
                if (_shown)
                {
                    _shown = false;
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                }

                return;
            }

            if (!starting)
            {
                return;
            }

            if (_glyph != null)
            {
                Object.Destroy(_glyph);
                _glyph = null;
            }

            _glyph = SystemCursor.Move(SystemCursor.Size(), out _hotspot);

            if (_glyph == null)
            {
                _shown = false;
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return;
            }

            _shown = true;
            Cursor.SetCursor(_glyph, _hotspot, CursorMode.Auto);
        }
    }
}
