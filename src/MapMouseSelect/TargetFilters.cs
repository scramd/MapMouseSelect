using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MapMouseSelect
{
    /// <summary>
    /// Adds two groups to the game's Target List filter pane: wrecks and neutrals.
    ///
    /// The pane keeps its filters in three lists that the selector walks in CheckExclusions. The classes behind
    /// those lists only model what vanilla needed. The faction button carries a same-faction flag, not a faction,
    /// so a neutral unit (neither friend nor foe) can never be filtered. Nothing knows about destroyed buildings,
    /// because the game removes them from the map. A destroyed building is still a live, repairable Unit, so it
    /// belongs in that pane once this mod keeps its icon on the map.
    ///
    /// A button added to one of those lists would never match anything, or would break the pane. SetOnlyItem
    /// indexes listDefinitions[0] when it receives a vehicle button, so an entry with no definitions would throw.
    /// The two buttons therefore live outside the lists. The exclusion patch below reads them, and the pane's own
    /// behaviour stays untouched. A click toggles them. The pane's Reset button turns them back on. A toggle asks
    /// the selector to re-check its icons exactly like a vanilla button does.
    ///
    /// The polarity is vanilla's: lit means the group is shown, unlit means the pane filters it out.
    /// </summary>
    internal static class TargetFilters
    {
        /// <summary>Prefix of every button this mod adds, so its own copies can be told from the pane's.</summary>
        private const string ClonePrefix = "MapMouseSelect_";

        private static TargetListSelector_ToggleButton _neutral;
        private static TargetListSelector_ToggleButton _wreck;
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

            // The pane places its filter buttons by hand and has no layout group. Their geometry is real only after
            // the canvas is laid out, so the canvas is laid out first. The copies are placed against the measured
            // edges of the free lane, not against spacing worked out from the row.
            Canvas.ForceUpdateCanvases();

            var items = selector.toggleFactionItems;
            var template = items[0];
            var templateRect = (RectTransform)template.transform;
            var left = (RectTransform)items[items.Count - 1].transform;

            // The row's edges and its spacing are measured, not assumed, and all of them are in the row's local
            // units. Only the left edge is needed here. The pair is sized from a normal button, not from the lane,
            // so the two of them together take the space of one button.
            var row = (RectTransform)left.parent;
            var scale = row.lossyScale.x > 0f ? row.lossyScale.x : 1f;
            var centreWorld = row.TransformPoint(row.rect.center).x;
            var laneStart = (RightEdge(left) - centreWorld) / scale;
            var gap = items.Count >= 2
                ? Mathf.Max(1f, (LeftEdge(left) - RightEdge((RectTransform)items[items.Count - 2].transform)) / scale)
                : 4f;

            // The split between the two is half the row's spacing, and it comes out of their width, not out of the
            // row. Half a button each, less half the split, keeps the pair exactly as wide as the single button
            // whose slot it takes. Only the balance inside the pair changes.
            var split = Mathf.Max(1f, gap * 0.5f);
            var each = Mathf.Max(1f, (templateRect.rect.width - split) * 0.5f);

            // The pair takes the slot a third normal button would have taken, the opening gap included. It starts
            // one gap past the last faction button. Gap + each + split + each is exactly one button wide, so the
            // pair also ends where that third button would have ended.
            var slot = laneStart + gap;

            // The copies take the tone of the pane's type filters. That lights them in the neutral colour, not the
            // faction colour they were copied from. That enum value is not the one called NEUTRAL: asking
            // for NEUTRAL gave the copies the friendly blue. The context is therefore read off one of those
            // buttons, not named.
            var toned = selector.toggleUnitTypesItems != null && selector.toggleUnitTypesItems.Count > 0
                ? selector.toggleUnitTypesItems[0]
                : null;

            if (Plugin.FilterNeutral.Value && _neutral == null)
            {
                Dump("filter template: ", templateRect, 2);
                _neutral = Clone(template, toned, left, "NTL", true, slot, each);
                slot += each + split;
            }

            // Wrecks start filtered out. Nothing about a wreck is worth selecting by default. An unlit filter is what
            // stops it being selectable, through the same gate every vanilla selection path uses. Lighting the button
            // is how a player asks to select wrecks on purpose.
            if (Plugin.EnableDestroyedUnits.Value && _wreck == null)
            {
                _wreck = Clone(template, toned, left, "DED", false, slot, each);
            }

            Plugin.Verbose(
                "Target List filters: " + Describe(_neutral) + ", " + Describe(_wreck)
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

        /// <summary>The colour the button's frame is drawn in, which is what its theme context decided.</summary>
        private static string Tint(TargetListSelector_ToggleButton button)
        {
            return "tint=" + (button.image != null ? button.image.color.ToString() : "none")
                + " context=" + Context(button);
        }

        private static string Context(TargetListSelector_ToggleButton button)
        {
            return ThemeContext() != null ? ThemeContext().GetValue(button).ToString() : "unknown";
        }

        /// <summary>
        /// Dumps a button's hierarchy and the frame settings of everything in it. The filter buttons are drawn
        /// a prefab nobody here can see. When a copy looked wrong, the log named the sprite that draws the frame,
        /// and the rect around it. Verbose only, and it runs once, when the buttons are first added.
        /// </summary>
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
        /// Copies one of the pane's own faction buttons. The copy is sized to the half of the lane it takes, and it
        /// is anchored to the middle of the row horizontally. The vertical anchoring is the pane's own, so the
        /// copies sit on the same line as the buttons they came from. Their theme context is switched to neutral,
        /// the theme group the pane's type filters use: lit buttons come out green, not in a faction colour.
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

            // The copies draw their own frame, generated at the copy's own size with one canvas unit to a stroke.
            // The stroke therefore does not depend on the button's width. The pane's own frame is a plain sprite with
            // no nine slice borders, drawn stretched over whatever width it is given. The log confirms that much: it
            // prints type, border and aspect for the template.
            //
            // Unverified: why it happens. Squeezing the pane's sprite into a narrower rect is the obvious candidate,
            // but that was a reading of a screenshot, not a measurement. That it happens is not in doubt. With the
            // sprite assignment below taken out, a copy shows the top and bottom of a frame and nothing else.
            //
            // Simple with the aspect off is what the template already uses. Those two lines matter only if a future
            // version of the pane ships a sliced frame.
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
        /// A hollow rectangle drawn at the size of one copy, one canvas unit to a stroke. It is white, so the
        /// Image tints it with the lit or unlit colour exactly like it tints the pane's own frame. The copy's own
        /// size is the point: the stroke keeps its width whatever the button's width is.
        ///
        /// The two copies are the same size, so the result is cached and built at most once per size.
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

            // The sprite's own size comes out as the button's size, so the Image draws it one to one. A stroke is
            // then one canvas unit, not a stretched fraction of one.
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
        /// Tones a copy like the pane's type filters, by copying their theme context. The context is a private
        /// serialized field, and it decides the lit colour. The theme handler must therefore run again for the
        /// change to show.
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

        /// <summary>
        /// Puts a filter back to the state it starts in. Reset returns the pane to normal, and normal is where the
        /// pane comes up: neutrals shown, wrecks hidden.
        /// </summary>
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
        /// Helps the pane's Reset button. That button walks only the three lists the pane owns, so it leaves the
        /// two copies where they were. It still lights up for them like for any other filter.
        ///
        /// Wrecks go back to hidden, not to shown. Selecting a wreck is something a player opts into with that
        /// button. A reset that quietly turned it on would be the one control in this pane that makes wrecks
        /// targetable without being asked. Setting a filter asks the pane to rebuild its icons, which is also what
        /// lights the Reset button. This method then greys the button again, the way the pane's own method ends.
        /// </summary>
        internal static void Reset(TargetListSelector pane)
        {
            if (_neutral == null && _wreck == null)
            {
                return;
            }

            Restore(_neutral, "NTL", true);
            Restore(_wreck, "DED", false);
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

        /// <summary>
        /// The mod's half of the pane's exclusions, added to whatever the pane itself decided.
        /// </summary>
        internal static bool Excludes(Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (_wreck != null && !_wreck.status && DestroyedBuildings.IsWreck(unit))
            {
                return true;
            }

            return _neutral != null && !_neutral.status && IsNeutral(unit);
        }

        /// <summary>
        /// Neutral is what the map treats as its default case: neither your faction nor a hostile one. Spectator
        /// mode shows every faction in its own colour, so those units are not neutrals either.
        /// </summary>
        private static bool IsNeutral(Unit unit)
        {
            var mode = DynamicMap.GetFactionMode(unit.NetworkHQ, false);
            return mode != FactionMode.Friendly && mode != FactionMode.Enemy && mode != FactionMode.Spectator;
        }
    }

    /// <summary>
    /// Adds the two buttons once the pane has built itself. Start fills the vehicle type row, and the faction and
    /// unit type buttons are already on the prefab. A postfix here is therefore the first moment the row exists
    /// and is safe to copy from.
    /// </summary>
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
    /// Lets the two extra groups filter units the way the pane's own groups do. Everything that reads this method
    /// gets it: map clicks, the Select action, the marquee, and CheckAllExclusions. CheckAllExclusions deselects
    /// anything a filter now excludes, and it rebuilds every icon colour.
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

    /// <summary>
    /// The pane's Reset button walks the three filter lists the pane owns, and the two this mod adds are not in
    /// them. It therefore lit up for filters it could not put back. This patch puts them back.
    /// </summary>
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
