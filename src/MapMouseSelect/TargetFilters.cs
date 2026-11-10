using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MapMouseSelect
{
    /// <summary>
    /// Adds neutrals to the pane's Target List filters. Lit shows a group, unlit filters it out.
    /// The button sit outside the pane's three filter lists. SetOnlyItem indexes listDefinitions[0] for a
    /// vehicle button, so an entry with no definitions would throw, and the faction button carries a
    /// same-faction flag, not a faction, so no list entry can express a neutral unit.
    /// </summary>
    internal static class TargetFilters
    {
        private const string ClonePrefix = "MapMouseSelect_";

        private static TargetListSelector_ToggleButton _neutral;
        private static FieldInfo _context;
        private static MethodInfo _themeChanged;
        private static Sprite _frame;
        private static float _frameWidth;
        private static float _frameHeight;
        private static FieldInfo _resetImage;
        private static FieldInfo _resetLabel;

        internal static void Add(TargetListSelector selector)
        {
            if (selector == null || selector.toggleFactionItems == null || selector.toggleFactionItems.Count == 0)
            {
                return;
            }

            // The pane places its buttons by hand and has no layout group, so their geometry is only real after
            // the canvas is laid out.
            Canvas.ForceUpdateCanvases();

            var items = selector.toggleFactionItems;
            var template = items[0];
            var templateRect = (RectTransform)template.transform;
            var left = (RectTransform)items[items.Count - 1].transform;

            // Edges and spacing are measured, not assumed, in the row's local units. Only the left edge is needed.
            var row = (RectTransform)left.parent;
            var scale = row.lossyScale.x > 0f ? row.lossyScale.x : 1f;
            var centreWorld = row.TransformPoint(row.rect.center).x;
            var laneStart = (RightEdge(left) - centreWorld) / scale;
            var gap = items.Count >= 2
                ? Mathf.Max(1f, (LeftEdge(left) - RightEdge((RectTransform)items[items.Count - 2].transform)) / scale)
                : 4f;

            // Retain the upstream half-width slot and spacing.
            var split = Mathf.Max(1f, gap * 0.5f);
            var each = Mathf.Max(1f, (templateRect.rect.width - split) * 0.5f);

            // One gap past the last faction button.
            var slot = laneStart + gap;

            // The type filters' theme context lights a button green, not in the faction colour a copy starts with.
            // Asking for the enum value called NEUTRAL gave the friendly blue, so read it off a button.
            var toned = selector.toggleUnitTypesItems != null && selector.toggleUnitTypesItems.Count > 0
                ? selector.toggleUnitTypesItems[0]
                : null;

            if (Plugin.FilterNeutral.Value && _neutral == null)
            {
                Dump("filter template: ", templateRect, 2);
                _neutral = Clone(template, toned, left, "NTL", true, slot, each);
            }

            Plugin.Verbose(
                "Target List filters: " + Describe(_neutral)
                + ", laneStart=" + laneStart.ToString("0.#") + ", normal=" + templateRect.rect.width.ToString("0.#")
                + ", gap=" + gap.ToString("0.#") + ", split=" + split.ToString("0.#")
                + ", each=" + each.ToString("0.#")
                + ", themed after " + Describe(toned) + ".");
        }

        private static string Describe(TargetListSelector_ToggleButton button)
        {
            if (button == null)
            {
                return "absent";
            }

            var rect = (RectTransform)button.transform;
            return button.label.text + (button.status ? " lit at " : " unlit at ") + rect.anchoredPosition
                + " " + rect.rect.width.ToString("0.#") + "x" + rect.rect.height.ToString("0.#")
                + " " + Tint(button);
        }

        private static string Tint(TargetListSelector_ToggleButton button)
        {
            return "tint=" + (button.image != null ? button.image.color.ToString() : "none")
                + " context=" + Context(button);
        }

        private static string Context(TargetListSelector_ToggleButton button)
        {
            return ThemeContext() != null ? ThemeContext().GetValue(button).ToString() : "unknown";
        }

        /// <summary>Dumps a button's hierarchy and the frame settings in it, to the verbose log.</summary>
        private static void Dump(string label, RectTransform root, int depth)
        {
            if (depth < 0)
            {
                return;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var line = label + child.name
                    + " rect=" + child.rect.width.ToString("0.#") + "x" + child.rect.height.ToString("0.#")
                    + " anchors=" + child.anchorMin + ".." + child.anchorMax
                    + " pos=" + child.anchoredPosition;

                var image = child.GetComponent<Image>();
                if (image != null)
                {
                    var sprite = image.sprite;
                    line += " image[sprite=" + (sprite != null ? sprite.name : "none")
                        + ", type=" + image.type
                        + ", border=" + (sprite != null ? sprite.border.ToString() : "-")
                        + ", ppu=" + image.pixelsPerUnitMultiplier.ToString("0.###")
                        + ", aspect=" + image.preserveAspect
                        + ", center=" + image.fillCenter
                        + ", color=" + image.color + "]";
                }

                Plugin.Verbose(line);
                Dump(label + "  ", child, depth - 1);
            }
        }

        private static float LeftEdge(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Mathf.Min(corners[0].x, corners[3].x);
        }

        private static float RightEdge(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Mathf.Max(corners[1].x, corners[2].x);
        }

        /// <summary>
        /// Copies a faction button at half the lane's width, on the pane's own vertical line, with the theme
        /// context the pane's type filters use.
        /// </summary>
        private static TargetListSelector_ToggleButton Clone(
            TargetListSelector_ToggleButton template,
            TargetListSelector_ToggleButton toned,
            RectTransform anchorRect,
            string text,
            bool lit,
            float leftEdge,
            float width)
        {
            var copy = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            copy.name = ClonePrefix + text;
            copy.transform.SetAsLastSibling();

            var button = copy.GetComponent<TargetListSelector_ToggleButton>();
            if (button == null)
            {
                Plugin.Log.LogWarning("Could not copy a Target List filter button for " + text + ".");
                return null;
            }

            if (button.label != null)
            {
                button.label.text = text;
            }

            var rect = (RectTransform)copy.transform;
            rect.anchorMin = new Vector2(0.5f, anchorRect.anchorMin.y);
            rect.anchorMax = new Vector2(0.5f, anchorRect.anchorMax.y);
            rect.pivot = anchorRect.pivot;
            rect.sizeDelta = new Vector2(width, anchorRect.sizeDelta.y);

            // Left edge at leftEdge: the pivot sits that far in from it.
            rect.anchoredPosition = new Vector2(leftEdge + rect.pivot.x * width, anchorRect.anchoredPosition.y);

            // The copy draws its own frame at their own size, one canvas unit to a stroke. The pane's frame is a
            // plain sprite with no nine slice borders, and squeezing one left only the top and bottom rows visible.
            if (button.image != null)
            {
                button.image.sprite = Frame(width, rect.rect.height);
                button.image.type = Image.Type.Simple;
                button.image.preserveAspect = false;
            }

            Dump("filter copy: ", rect, 1);
            MatchTheme(button, toned);
            button.Set(lit);
            return button;
        }

        /// <summary>
        /// A hollow rectangle at the size of one copy, one canvas unit to a stroke, white so the Image tints it
        /// like the pane's own frame. Cached, since the generated frame can be reused.
        /// </summary>
        private static Sprite Frame(float width, float height)
        {
            const int Oversample = 4;

            if (_frame != null && Mathf.Approximately(_frameWidth, width) && Mathf.Approximately(_frameHeight, height))
            {
                return _frame;
            }

            var w = Mathf.Max(Oversample, Mathf.RoundToInt(width * Oversample));
            var h = Mathf.Max(Oversample, Mathf.RoundToInt(height * Oversample));
            var stroke = Oversample;
            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);
            var pixels = new Color32[w * h];

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var edge = x < stroke || y < stroke || x >= w - stroke || y >= h - stroke;
                    pixels[(y * w) + x] = edge ? solid : clear;
                }
            }

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = ClonePrefix + "Frame",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            // The sprite comes out at the button's size, so a stroke is one canvas unit, not a stretched fraction.
            _frameWidth = width;
            _frameHeight = height;
            _frame = Sprite.Create(
                texture,
                new Rect(0f, 0f, w, h),
                new Vector2(0.5f, 0.5f),
                Oversample,
                0,
                SpriteMeshType.FullRect);

            return _frame;
        }

        /// <summary>
        /// Copies the type filters' theme context onto a button. The context is a private serialized field, and the
        /// theme handler has to run again for the colour to show.
        /// </summary>
        private static void MatchTheme(TargetListSelector_ToggleButton button, TargetListSelector_ToggleButton source)
        {
            var context = ThemeContext();
            if (context == null || source == null)
            {
                Plugin.Log.LogWarning(
                    "Could not read the Target List button theme, so the new filters keep the faction colour they "
                    + "were copied from.");
                return;
            }

            context.SetValue(button, context.GetValue(source));
            _themeChanged.Invoke(button, null);
        }

        private static FieldInfo ThemeContext()
        {
            if (_context == null)
            {
                _context = AccessTools.Field(typeof(TargetListSelector_ToggleButton), "context");
                _themeChanged = AccessTools.Method(
                    typeof(TargetListSelector_ToggleButton), "TargetListSelector_ToggleButton_OnThemeGroupChanged");

                if (_context == null || _themeChanged == null)
                {
                    return null;
                }
            }

            return _context;
        }

        private static void Restore(TargetListSelector_ToggleButton button, string who, bool startsOn)
        {
            if (button == null || button.status == startsOn)
            {
                return;
            }

            button.Set(startsOn);
            Plugin.Verbose("Reset Filter put " + who + " back to its default.");
        }

        /// <summary>
        /// Restores NTL for the pane's Reset button, which walks only the three lists the pane owns.
        /// </summary>
        internal static void Reset(TargetListSelector pane)
        {
            if (_neutral == null)
            {
                return;
            }

            Restore(_neutral, "NTL", true);
            GreyResetButton(pane);
        }

        private static void GreyResetButton(TargetListSelector pane)
        {
            if (_resetImage == null)
            {
                _resetImage = AccessTools.Field(typeof(TargetListSelector), "resetButtonImage");
                _resetLabel = AccessTools.Field(typeof(TargetListSelector), "resetButtonLabel");
            }

            if (pane == null || _resetImage == null || _resetLabel == null)
            {
                return;
            }

            var image = _resetImage.GetValue(pane) as Graphic;
            var label = _resetLabel.GetValue(pane) as Graphic;

            if (image != null)
            {
                image.color = Color.gray;
            }

            if (label != null)
            {
                label.color = Color.gray;
            }
        }

        /// <summary>The mod's half of the pane's exclusions, added to whatever the pane decided.</summary>
        internal static bool Excludes(Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            return _neutral != null && !_neutral.status && IsNeutral(unit);
        }

        /// <summary>
        /// Neutral is the map's default case: neither your faction nor a hostile one. Spectator mode draws each
        /// faction in its own colour, so those are not neutrals either.
        /// </summary>
        private static bool IsNeutral(Unit unit)
        {
            var mode = DynamicMap.GetFactionMode(unit.NetworkHQ, false);
            return mode != FactionMode.Friendly && mode != FactionMode.Enemy && mode != FactionMode.Spectator;
        }
    }

    /// <summary>Adds NTL once the pane has built itself, the first moment the filter row exists.</summary>
    [HarmonyPatch(typeof(TargetListSelector), "Start")]
    internal static class TargetListSelectorStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TargetListSelector __instance)
        {
            TargetFilters.Add(__instance);
        }
    }

    /// <summary>
    /// Lets the extra group filter units like the pane's own groups. Everything that reads this method gets
    /// it: map clicks, the Select action, the marquee and CheckAllExclusions.
    /// </summary>
    [HarmonyPatch(typeof(TargetListSelector), nameof(TargetListSelector.CheckExclusions))]
    internal static class TargetListSelectorCheckExclusionsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Unit u, ref bool __result)
        {
            if (!__result)
            {
                __result = TargetFilters.Excludes(u);
            }
        }
    }

    /// <summary>Puts the mod's filter back, which the pane's own Reset cannot, since it walks only its lists.</summary>
    [HarmonyPatch(typeof(TargetListSelector), nameof(TargetListSelector.ResetFilters))]
    internal static class TargetListSelectorResetFiltersPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TargetListSelector __instance)
        {
            TargetFilters.Reset(__instance);
        }
    }
}
