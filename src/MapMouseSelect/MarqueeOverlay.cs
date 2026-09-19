using UnityEngine;
using UnityEngine.UI;

namespace MapMouseSelect
{
    /// <summary>
    /// Draws the drag box on its own screen-space overlay canvas.
    ///
    /// Independent of the game's map UI hierarchy on purpose: for a ScreenSpaceOverlay canvas the
    /// scale factor defaults to 1 and the canvas rect is the screen, so screen pixels map 1:1 to
    /// canvas units and the box math needs no scaling or pivot guesswork.
    /// </summary>
    internal sealed class MarqueeOverlay
    {
        /// <summary>Width of the frame, in screen pixels.</summary>
        private const float OutlineThickness = 1f;

        private const int Top = 0;
        private const int Bottom = 1;
        private const int Left = 2;
        private const int Right = 3;

        private Canvas _canvas;
        private readonly RectTransform[] _edges = new RectTransform[4];
        private readonly Image[] _edgeImages = new Image[4];

        private void EnsureCreated()
        {
            if (_canvas != null)
            {
                return;
            }

            var root = new GameObject("MapMouseSelect_Marquee");
            Object.DontDestroyOnLoad(root);

            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = Plugin.OverlaySortingOrder != null
                ? Plugin.OverlaySortingOrder.Value
                : Plugin.DefaultOverlaySortingOrder;

            // No CanvasScaler: ConstantPixelSize at scale factor 1 is what an overlay canvas already is.
            // No GraphicRaycaster either, on purpose: the overlay must never consume pointer input.

            CreateEdge(root.transform, "Top", Top);
            CreateEdge(root.transform, "Bottom", Bottom);
            CreateEdge(root.transform, "Left", Left);
            CreateEdge(root.transform, "Right", Right);

            root.SetActive(false);
        }

        private void CreateEdge(Transform parent, string name, int index)
        {
            _edges[index] = CreateQuad(parent, name, out var image);
            _edgeImages[index] = image;
        }

        private static RectTransform CreateQuad(Transform parent, string name, out Image image)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            // Anchor to the canvas bottom-left so anchoredPosition is directly convertible
            // from screen pixels, matching Input.mousePosition.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;

            image = go.AddComponent<Image>();
            // No sprite: an Image with a null sprite draws a plain quad through Graphic.OnPopulateMesh,
            // which is all a bar of the frame needs.
            image.raycastTarget = false;

            return rect;
        }

        private static void SetQuad(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
        }

        internal void Show(Rect screenRect, Color color)
        {
            EnsureCreated();
            if (_canvas == null)
            {
                return;
            }

            foreach (var image in _edgeImages)
            {
                image.color = color;
            }

            _canvas.gameObject.SetActive(true);

            // Four bars rather than one quad with a translucent center, so the box stays hollow.
            const float b = OutlineThickness;
            var xMin = screenRect.xMin;
            var yMin = screenRect.yMin;
            var width = screenRect.width;
            var height = screenRect.height;

            SetQuad(_edges[Top], xMin - b, yMin + height, width + 2f * b, b);
            SetQuad(_edges[Bottom], xMin - b, yMin - b, width + 2f * b, b);
            SetQuad(_edges[Left], xMin - b, yMin, b, height);
            SetQuad(_edges[Right], xMin + width, yMin, b, height);
        }

        internal void Hide()
        {
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(false);
            }
        }
    }
}
