using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Shows the cursor theme's move cursor while the pan button is held over the map.
    ///
    /// The game never changes the cursor's shape itself. CursorManager decides only whether the cursor is visible and
    /// whether it is locked. So the pan looks like the plain arrow that every button and the marquee use. That arrow
    /// is the one shape that says nothing about what the button is about to do.
    ///
    /// What goes up is the theme's own cursor, read by `SystemCursor`, not a shape this mod draws. The mod hands it
    /// to Unity, and does not put it on the mouse pointer behind Unity's back. Unity applies the cursor itself, and
    /// two writers for one pointer is a flicker. The mod reads it again at each press, so the next pan picks up a
    /// scheme that changed while the game runs.
    ///
    /// The mod makes the swap only while the button is held. It puts the game's arrow back on release, so the game
    /// keeps deciding when a cursor is shown at all.
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
