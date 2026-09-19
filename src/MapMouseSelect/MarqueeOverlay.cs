using UnityEngine;
using UnityEngine.UI;

namespace MapMouseSelect
{
    /// <summary>
    /// Draws the drag rectangle on its own screen-space overlay canvas.
    ///
    /// This is deliberately independent of the game's map UI hierarchy: the vanilla
    /// ScreenSpaceOverlay canvases use a null camera, so screen pixels map 1:1 to
    /// canvas units at scale factor 1, and the rectangle math needs no scaling or
    /// pivot guesswork.
    /// </summary>
    internal sealed class MarqueeOverlay
    {
        private const float OutlineThickness = 1f;

        private const int Top = 0;
        private const int Bottom = 1;
        private const int Left = 2;
        private const int Right = 3;

        private static Sprite _whiteSprite;

        private Canvas _canvas;
        private readonly RectTransform[] _edges = new RectTransform[4];
        private readonly Image[] _edgeImages = new Image[4];

        internal bool IsCreated => _canvas != null;

        private static Sprite WhiteSprite()
        {
            if (_whiteSprite == null)
            {
                var texture = Texture2D.whiteTexture;
                _whiteSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                _whiteSprite.name = "MapMouseSelect_White";
            }
            return _whiteSprite;
        }

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
            _canvas.sortingOrder = Plugin.OverlaySortingOrder != null ? Plugin.OverlaySortingOrder.Value : 32000;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            // No GraphicRaycaster on purpose: the overlay must never consume pointer input.

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
            image.sprite = WhiteSprite();
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            return rect;
        }

        private static void SetQuad(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
        }

        internal void Show(Rect screenRect, bool additive)
        {
            EnsureCreated();
            if (_canvas == null)
            {
                return;
            }

            var fallback = additive ? new Color(0.49f, 0.89f, 0.55f, 0.8f) : new Color(0.24f, 0.71f, 1f, 0.8f);
            var color = Plugin.ParseColor(
                additive ? Plugin.BoxOutlineColorAdditive : Plugin.BoxOutlineColor, fallback);

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

        internal void Destroy()
        {
            if (_canvas != null)
            {
                Object.Destroy(_canvas.gameObject);
                _canvas = null;
                for (var i = 0; i < _edges.Length; i++)
                {
                    _edges[i] = null;
                    _edgeImages[i] = null;
                }
            }
        }
    }
}
