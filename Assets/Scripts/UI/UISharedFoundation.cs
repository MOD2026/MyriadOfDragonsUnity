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

        // Real color tokens (LOCKED 2026-08-26, register: docs/INDUSTRY_STANDARD_GAP_DIAGNOSIS_
        // 2026-08-26.md §4). Before this, no shared color-token set existed at all - every screen
        // independently authored its own near-identical charcoal/navy/bronze/emerald literal
        // (13+ near-duplicate raw values found, none sharing a source). Each token below is
        // DERIVED from the actual most-common existing literal across presenters, not invented -
        // see each one's own doc comment for the real citation. This pass only builds the token
        // set + the border/frame primitive below; migrating existing screens onto these is a
        // separate, later task.

        /// <summary>Full-screen shell background. Matches the (0.08, 0.09, 0.1x) literal already
        /// used as the fullscreen-shell fallback in BattlePassPresenter.cs:59,
        /// BazaarPresenter.cs:78, ChatSocialPresenter.cs:71, CollectionPresenter.cs:90,
        /// DailyLoginQuestsPresenter.cs:72 and FriendsPresenter.cs:71 - 6 of 23 screens
        /// independently converged on essentially this same value with no shared source.</summary>
        public static readonly Color ColorBackground = new Color(0.08f, 0.09f, 0.12f);

        /// <summary>Secondary surface sitting on top of ColorBackground (cards, panels, modals).
        /// Matches the (0.12, 0.14, 0.2) literal already used in AvatarPresenter.cs:113,
        /// CollectionPresenter.cs:195 and CampaignMapUiLibrary.cs:87's own modal-chrome
        /// fallback.</summary>
        public static readonly Color ColorPanel = new Color(0.12f, 0.14f, 0.2f);

        /// <summary>Header/top-bar band, darker than ColorPanel. Matches the (0.06, 0.06, 0.1)
        /// literal already used identically in AvatarPresenter.cs:52,
        /// CampaignMapPresenter.cs:2232 and EmpirePresenter.cs:86's own top bars.</summary>
        public static readonly Color ColorHeader = new Color(0.06f, 0.06f, 0.1f);

        /// <summary>Warm gold/bronze accent - GameBootstrap.cs:221's own AccentBorderColor
        /// verbatim, the ONE place in ~23 screens with a genuine border treatment already
        /// shipping (its battle-screen buttons). Reused, not reinvented.</summary>
        public static readonly Color ColorAccentBronze = new Color(0.85f, 0.72f, 0.4f, 0.5f);

        /// <summary>Forest-emerald accent for primary/positive actions (confirm, continue,
        /// recommend). Matches the (0.16-0.2, 0.4-0.45, 0.28-0.32) literal already used near-
        /// identically across BazaarPresenter.cs:197, CollectionPresenter.cs:309,
        /// DailyLoginQuestsPresenter.cs:220, EmpireBuildingDetailPresenter.cs:186,
        /// PackOpenOverlayPresenter.cs:87, SpellLoadoutPickerPresenter.cs:270 and
        /// TacticalPuzzlePresenter.cs:535 - 7 of 23 screens.</summary>
        public static readonly Color ColorAccentEmerald = new Color(0.18f, 0.4f, 0.28f);

        /// <summary>Primary readable text on a dark surface (cream/parchment). Matches the single
        /// most common text-color literal found across presenters (12 occurrences of exactly this
        /// value, e.g. ChatSocialPresenter.cs, FriendsPresenter.cs, VipSubscriptionPresenter.cs).</summary>
        public static readonly Color ColorTextPrimary = new Color(0.9f, 0.88f, 0.75f);
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

        /// <summary>Real border/frame primitive (LOCKED 2026-08-26, register: docs/
        /// INDUSTRY_STANDARD_GAP_DIAGNOSIS_2026-08-26.md §4) - generalizes the ONLY two real
        /// border/frame treatments that existed anywhere in ~23 screens before this:
        /// GameBootstrap.cs's CreateRoundedGradientSprite (a procedurally-generated rounded-
        /// corner, top-to-bottom gradient sprite - its battle-screen buttons/modals) and
        /// CampaignMapUiLibrary.ApplyModalChrome's real-art-first/procedural-fallback pattern
        /// (its one stage-detail modal). Every other screen used flat colored Image rectangles
        /// with no border/frame at all.
        ///
        /// Tries a real authored frame sprite from Resources.Load(frameResourcePath) first (same
        /// binding-layer convention as CombatPresentationAssetMap/ApplyModalChrome - when real
        /// art exists, someone drops a file at the path and this picks it up with no code
        /// change), falls back to the procedural gradient sprite when no art exists yet. Either
        /// way every caller gets a REAL rounded/framed look today, not a flat rectangle.</summary>
        public static void ApplyFramedPanel(Image target, string frameResourcePath, Color topColor, Color bottomColor,
            int cornerRadius = UIFrozenTokens.RadiusPrimary)
        {
            if (target == null) return;

            Sprite real = string.IsNullOrEmpty(frameResourcePath) ? null : Resources.Load<Sprite>(frameResourcePath);
            if (real != null)
            {
                target.sprite = real;
                target.type = Image.Type.Sliced;
                target.color = Color.white;
            }
            else
            {
                target.sprite = CreateRoundedPanelSprite(topColor, bottomColor, cornerRadius);
                target.type = Image.Type.Sliced;
                target.color = Color.white;
            }
        }

        /// <summary>Builds a new panel GameObject with <see cref="ApplyFramedPanel"/> already
        /// applied - the convenience most call sites want ("give me a real bordered panel"),
        /// matching the existing CreateModalShell/CreateCardPrimitive style.</summary>
        public static RectTransform CreateFramedPanel(Transform parent, string name, Vector2 size,
            string frameResourcePath, Color topColor, Color bottomColor, int cornerRadius = UIFrozenTokens.RadiusPrimary)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            ApplyFramedPanel(go.GetComponent<Image>(), frameResourcePath, topColor, bottomColor, cornerRadius);
            return rect;
        }

        /// <summary>The exact per-pixel alpha-shaping algorithm GameBootstrap.
        /// CreateRoundedGradientSprite already uses (that method's own real, working technique -
        /// not re-derived), generalized here so any screen can call it instead of duplicating the
        /// same math. A top-to-bottom color gradient with soft-edged rounded corners (1.5px
        /// anti-alias band, not a hard cutoff), returned as a 9-sliceable Sprite so it scales to
        /// any panel size without stretching the corners. Public so a caller that wants a raw
        /// Sprite directly (e.g. for a Button's spriteState variants) can get one without going
        /// through ApplyFramedPanel/CreateFramedPanel.</summary>
        public static Sprite CreateRoundedPanelSprite(Color topColor, Color bottomColor,
            int cornerRadius = UIFrozenTokens.RadiusPrimary, int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < size; y++)
            {
                float t = (float)y / (size - 1);
                Color rowColor = Color.Lerp(bottomColor, topColor, t);
                for (int x = 0; x < size; x++)
                {
                    float alpha = rowColor.a;
                    bool nearEdgeX = x < cornerRadius || x >= size - cornerRadius;
                    bool nearEdgeY = y < cornerRadius || y >= size - cornerRadius;
                    if (nearEdgeX && nearEdgeY)
                    {
                        float cx = x < cornerRadius ? cornerRadius : size - cornerRadius - 1;
                        float cy = y < cornerRadius ? cornerRadius : size - cornerRadius - 1;
                        float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        // 1.5px soft edge instead of a hard cutoff, so the curve doesn't look jagged.
                        alpha *= Mathf.Clamp01(cornerRadius - dist + 1.5f);
                    }
                    tex.SetPixel(x, y, new Color(rowColor.r, rowColor.g, rowColor.b, alpha));
                }
            }
            tex.Apply();
            var border = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
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

        /// <summary>Single-line text-entry field, styled to match CreateText's legacy-Text
        /// conventions (same default font, same left-aligned layout). Caller applies its own
        /// anchoring afterward (e.g. SetNorm), same as CreateText - this only builds the field
        /// itself. Mirrors CollectionPresenter.CreateSearchField's structure (background + Text +
        /// Placeholder + InputField wiring) as the one other real InputField usage in this
        /// codebase, generalized for reuse.</summary>
        public static InputField CreateInputField(Transform parent, string objectName, string placeholderText, Color textColor, Vector2 size, int characterLimit = 80)
        {
            GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one;
            root.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.2f, 0.85f);
            root.GetComponent<RectTransform>().sizeDelta = size;

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(root.transform, false);
            Text text = textObj.GetComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = FontSizeFor(UITextRole.Body);
            text.color = textColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            StretchFull(textObj.GetComponent<RectTransform>());
            textObj.GetComponent<RectTransform>().offsetMin = new Vector2(12f, 0f);
            textObj.GetComponent<RectTransform>().offsetMax = new Vector2(-12f, 0f);

            GameObject placeholderObj = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderObj.transform.SetParent(root.transform, false);
            Text placeholder = placeholderObj.GetComponent<Text>();
            placeholder.font = GetDefaultFont();
            placeholder.fontSize = FontSizeFor(UITextRole.Body);
            placeholder.color = new Color(textColor.r, textColor.g, textColor.b, 0.45f);
            placeholder.text = placeholderText;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.supportRichText = false;
            StretchFull(placeholderObj.GetComponent<RectTransform>());
            placeholderObj.GetComponent<RectTransform>().offsetMin = new Vector2(12f, 0f);
            placeholderObj.GetComponent<RectTransform>().offsetMax = new Vector2(-12f, 0f);

            InputField input = root.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = characterLimit;
            return input;
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
