using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Frozen token surface used by shared UI builders.
    /// Values here mirror approved preparation constraints and must not drift.
    /// </summary>
    public static class UIFrozenTokens
    {
        public const int SpacingGrid = 8;
        public const float MinTouchTargetAndroid = 48f;
        public const float MinTouchTargetIOS = 44f;
        public const int RadiusPrimary = 12;
        public const int RadiusSecondary = 10;

        // UI/UX Bible typography scale pairs (font size / line height).
        // Legacy Text does not expose line-height, so only font size is directly applied.
        public const int TypeDisplaySize = 28;
        public const int TypeTitleSize = 20;
        public const int TypeBodySize = 16;
        public const int TypeCaptionSize = 12;
    }

    public enum UITextRole
    {
        Display,
        Title,
        Body,
        Caption,
    }

    public static class UISharedFoundation
    {
        private static Font _cachedFont;
        private const string HomeNavHoverPath = "UI/Buttons/btn_home_nav_hover_v2";
        private const string HomeNavPressedPath = "UI/Buttons/btn_home_nav_pressed_v2";
        private const string HomeNavDisabledPath = "UI/Buttons/btn_home_nav_disabled_v2";

        public static Canvas CreateScreenCanvas(string name, Vector2 referenceResolution)
        {
            GameObject canvasObj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;

            EnsureEventSystem();
            return canvas;
        }

        public static Image CreateFullscreenBackground(Transform parent, string spritePath, Color fallback)
        {
            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(parent, false);

            Image bg = bgObj.GetComponent<Image>();
            Sprite sprite = string.IsNullOrEmpty(spritePath) ? null : Resources.Load<Sprite>(spritePath);
            if (sprite != null)
            {
                bg.sprite = sprite;
                bg.color = Color.white;
            }
            else
            {
                bg.color = fallback;
            }

            StretchFull(bg.rectTransform);
            return bg;
        }

        public static RectTransform CreateHeaderShell(Transform parent, string name, float height, string panelSpritePath, Color fallback)
        {
            GameObject headerObj = new GameObject(name, typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(parent, false);

            Image headerImage = headerObj.GetComponent<Image>();
            Sprite sprite = string.IsNullOrEmpty(panelSpritePath) ? null : Resources.Load<Sprite>(panelSpritePath);
            if (sprite != null)
            {
                headerImage.sprite = sprite;
                headerImage.type = Image.Type.Sliced;
                headerImage.color = Color.white;
            }
            else
            {
                headerImage.color = fallback;
            }

            RectTransform rect = headerObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            return rect;
        }

        public static RectTransform CreateBottomDock(Transform parent, string name, float height, Color background)
        {
            GameObject dockObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            dockObj.transform.SetParent(parent, false);

            Image bg = dockObj.GetComponent<Image>();
            bg.color = background;

            RectTransform rect = dockObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, height);

            HorizontalLayoutGroup hlg = dockObj.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = UIFrozenTokens.SpacingGrid * 4;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            return rect;
        }

        public static Text CreateCurrencyPill(Transform parent, string text, Color background)
        {
            GameObject pill = new GameObject("CurrencyPill", typeof(RectTransform), typeof(Image));
            pill.transform.SetParent(parent, false);

            Image bg = pill.GetComponent<Image>();
            bg.color = background;

            RectTransform rect = pill.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(170, 50);
            EnforceMinTouchTarget(rect);

            return CreateText(pill.transform, "Text", text, UITextRole.Body, TextAnchor.MiddleCenter, Color.white, true, new Vector2(170, 50));
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 size, Color tint, UnityEngine.Events.UnityAction action, string spritePath = null, bool useHomeNavSkin = false)
        {
            GameObject buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(parent, false);

            Image image = buttonObj.GetComponent<Image>();
            Sprite sprite = string.IsNullOrEmpty(spritePath) ? null : Resources.Load<Sprite>(spritePath);
            Sprite highlightedSprite = null;
            Sprite pressedSprite = null;
            Sprite disabledSprite = null;

            if (useHomeNavSkin)
            {
                highlightedSprite = Resources.Load<Sprite>(HomeNavHoverPath);
                pressedSprite = Resources.Load<Sprite>(HomeNavPressedPath);
                disabledSprite = Resources.Load<Sprite>(HomeNavDisabledPath);
            }

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = useHomeNavSkin ? Color.white : tint;
            }
            else
            {
                image.color = tint;
            }

            RectTransform rect = buttonObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            EnforceMinTouchTarget(rect);

            Button button = buttonObj.GetComponent<Button>();
            if (useHomeNavSkin && sprite != null && (highlightedSprite != null || pressedSprite != null || disabledSprite != null))
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = highlightedSprite,
                    pressedSprite = pressedSprite,
                    selectedSprite = highlightedSprite,
                    disabledSprite = disabledSprite,
                };
            }

            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            CreateText(buttonObj.transform, "Text", label, UITextRole.Display, TextAnchor.MiddleCenter, Color.white, true, new Vector2(380f, 80f));
            return button;
        }

        public static RectTransform CreateModalShell(Transform parent, string name, Color dimColor, Color panelColor, Vector2 panelSize, Vector2 panelAnchorMin, Vector2 panelAnchorMax, Vector2 panelPivot, Vector2 panelAnchoredPos)
        {
            GameObject overlay = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            overlay.transform.SetParent(parent, false);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            StretchFull(overlayRect);

            Image dim = overlay.GetComponent<Image>();
            dim.color = dimColor;

            GameObject panelObj = new GameObject("ModalPanel", typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(overlay.transform, false);
            Image panel = panelObj.GetComponent<Image>();
            panel.color = panelColor;

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = panelAnchorMin;
            panelRect.anchorMax = panelAnchorMax;
            panelRect.pivot = panelPivot;
            panelRect.anchoredPosition = panelAnchoredPos;
            panelRect.sizeDelta = panelSize;
            return panelRect;
        }

        public static RectTransform CreateCardPrimitive(Transform parent, string name, Vector2 size, Color background)
        {
            GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            Image image = card.GetComponent<Image>();
            image.color = background;

            RectTransform rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return rect;
        }

        public static Text CreateText(Transform parent, string objectName, string content, UITextRole role, TextAnchor alignment, Color color, bool richText, Vector2 size)
        {
            GameObject textObj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(parent, false);
            textObj.transform.localScale = Vector3.one;

            Text text = textObj.GetComponent<Text>();
            text.text = content;
            text.font = GetDefaultFont();
            text.fontSize = FontSizeFor(role);
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = richText;
            // Matches GameBootstrap.CreateText's explicit overflow settings - left at Unity's
            // own defaults (horizontalOverflow=Overflow, verticalOverflow=Truncate) a Text whose
            // rendered line height exceeds its RectTransform's height renders nothing at all
            // rather than spilling over, which is a silent, easy-to-hit way for a legacy Text to
            // register correctly (valid rect/color/font) yet paint zero visible glyphs.
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return text;
        }

        public static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static int FontSizeFor(UITextRole role)
        {
            switch (role)
            {
                case UITextRole.Display:
                    return UIFrozenTokens.TypeDisplaySize;
                case UITextRole.Title:
                    return UIFrozenTokens.TypeTitleSize;
                case UITextRole.Caption:
                    return UIFrozenTokens.TypeCaptionSize;
                default:
                    return UIFrozenTokens.TypeBodySize;
            }
        }

        private static Font GetDefaultFont()
        {
            if (_cachedFont == null)
            {
                // Matches GameBootstrap.GetDefaultFont's own fallback - if the built-in font
                // name Unity ships under ever changes/differs on a given install, this factory
                // was leaving every Text's font silently null (a legacy Text with no font
                // populates zero mesh vertices - registered, valid rect/color, but no visible
                // glyphs at all) instead of falling back the way GameBootstrap's proven path does.
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_cachedFont == null)
                {
                    _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
            }

            return _cachedFont;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
        }

        private static void EnforceMinTouchTarget(RectTransform rect)
        {
            float minimum = Mathf.Max(UIFrozenTokens.MinTouchTargetAndroid, UIFrozenTokens.MinTouchTargetIOS);
            Vector2 size = rect.sizeDelta;
            if (size.x < minimum) size.x = minimum;
            if (size.y < minimum) size.y = minimum;
            rect.sizeDelta = size;
        }
    }
}
