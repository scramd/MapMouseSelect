using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Which mouse button pans the tactical map. Values match Unity's button index for
    /// <see cref="Input.GetMouseButton"/> (0 = left, 1 = right, 2 = middle).
    /// </summary>
    public enum MapPanButton
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    internal static class PanButtonCompat
    {
        /// <summary>
        /// Stands in for the hardcoded <c>Input.GetMouseButton(0)</c> that gates map panning inside
        /// <c>DynamicMap.MapControls</c>. That gate is the only plain <c>GetMouseButton</c> call in the
        /// method, so swapping just that call moves panning without touching anything else.
        ///
        /// The transpiler has to call a method here rather than read the config inline, so this exists
        /// as the replacement target. The argument is whatever vanilla passed, which doubles as the
        /// fallback for the frames before the config is read.
        /// </summary>
        internal static bool GetMouseButton(int vanillaButtonIndex)
        {
            var button = Plugin.PanButton != null ? (int)Plugin.PanButton.Value : vanillaButtonIndex;
            return Input.GetMouseButton(button);
        }
    }
}
