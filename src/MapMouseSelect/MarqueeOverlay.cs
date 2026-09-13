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
        private const float OutlineThickness = 1.5f;

        private static Sprite _whiteSprite;

        private Canvas _canvas;
        private RectTransform _outline;
        private RectTransform _fill;
        private Image _outlineImage;
        private Image _fillImage;

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

            _outline = CreateQuad(root.transform, "Outline", out _outlineImage);
            _fill = CreateQuad(root.transform, "Fill", out _fillImage);

            root.SetActive(false);
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

            var fillFallback = additive ? new Color(0.49f, 0.89f, 0.55f, 0.15f) : new Color(0.24f, 0.71f, 1f, 0.15f);
            var outlineFallback = additive ? new Color(0.49f, 0.89f, 0.55f, 0.8f) : new Color(0.24f, 0.71f, 1f, 0.8f);

            _fillImage.color = additive
                ? Plugin.ParseColor(Plugin.BoxFillColorAdditive, fillFallback)
                : Plugin.ParseColor(Plugin.BoxFillColor, fillFallback);
            _outlineImage.color = additive
                ? Plugin.ParseColor(Plugin.BoxOutlineColorAdditive, outlineFallback)
                : Plugin.ParseColor(Plugin.BoxOutlineColor, outlineFallback);

            _canvas.gameObject.SetActive(true);

            const float b = OutlineThickness;
            SetQuad(_outline, screenRect.xMin - b, screenRect.yMin - b, screenRect.width + 2f * b, screenRect.height + 2f * b);
            SetQuad(_fill, screenRect.xMin, screenRect.yMin, screenRect.width, screenRect.height);
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
                _outline = null;
                _fill = null;
                _outlineImage = null;
                _fillImage = null;
            }
        }
    }
}
