using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>Which mouse button pans the tactical map – Unity's button index for
    /// <see cref="Input.GetMouseButton"/>.</summary>
    public enum MapPanButton
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    internal static class PanButtonCompat
    {
        /// <summary>Stands in for the hardcoded <c>Input.GetMouseButton(0)</c> in <c>DynamicMap.MapControls</c>,
        /// the only <c>GetMouseButton</c> call there – swapping this one moves panning alone.</summary>
        internal static bool GetMouseButton(int vanillaButtonIndex)
        {
            var button = Plugin.PanButton != null ? (int)Plugin.PanButton.Value : vanillaButtonIndex;
            return Input.GetMouseButton(button);
        }
    }
}
