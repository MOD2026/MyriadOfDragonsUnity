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
        //
        // Title/Body/Caption raised 20/16/12 -> 22 (owner-approved, 2026-08-27, decided on a
        // measured read-only dry run: bumping these three cleared 125 of 145 non-Puzzle
        // font-floor findings with zero new overflow findings - see the commit this change
        // ships in for the real post-commit numbers). All three now sit AT the 22px global
        // floor rather than under it. Display was already compliant (28) and is unchanged.
        public const int TypeDisplaySize = 28;
        public const int TypeTitleSize = 22;
        public const int TypeBodySize = 22;
        public const int TypeCaptionSize = 22;

        // Real color tokens - RELOCKED 2026-08-26 against
        // Myriad_of_Dragons_Visual_Authority_Memory.md's "Materials and palette" section (the
        // Shared_UI_Foundation reference doc, CC-authorized relock). Superseded the first-pass
        // 2026-08-26 consolidation (register: docs/INDUSTRY_STANDARD_GAP_DIAGNOSIS_2026-08-26.md
        // §4), which was explicitly a stopgap ("migrating existing screens onto these is a
        // separate, later task") derived from the OLD flat-box screens' own literals, never
        // claimed as final art direction. The Visual Authority Memory doc is now the real
        // authority - it explicitly rejects "bright gold on every edge" and "flat coloured
        // rectangles", which the old ColorAccentBronze/panel treatment read closer to than not.

        /// <summary>Base surface: "blue-black, charcoal stone, blackened iron and deep navy."</summary>
        public static readonly Color ColorBackground = new Color(0.06f, 0.07f, 0.1f);

        /// <summary>Secondary surface on top of ColorBackground (cards, panels, modals): "dark
        /// charcoal/navy inset surface with mild vertical tonal variation" - a touch lighter than
        /// ColorBackground, not a separate hue.</summary>
        public static readonly Color ColorPanel = new Color(0.1f, 0.11f, 0.15f);

        /// <summary>Header/top-bar band, darker than ColorPanel - blends into ColorBackground per
        /// the doc's "avoid a solid full-width web-style header bar" guidance.</summary>
        public static readonly Color ColorHeader = new Color(0.045f, 0.05f, 0.075f);

        /// <summary>Structural trim: "aged bronze and restrained desaturated gold - not bright
        /// yellow ornament everywhere." Deliberately duller/darker than the prior token, which was
        /// GameBootstrap.cs's battle-screen accent - a real shipped value, but a brighter, more
        /// saturated gold than this doc's own "not bright yellow" rule calls for.</summary>
        public static readonly Color ColorAccentBronze = new Color(0.62f, 0.52f, 0.34f, 0.6f);

        /// <summary>Secondary surface/button core: "navy/blackened-metal core with bronze trim -
        /// clearly subordinate without appearing disabled." New token - no prior screen had a real
        /// secondary treatment distinct from ColorPanel to derive this from.</summary>
        public static readonly Color ColorSecondary = new Color(0.13f, 0.16f, 0.23f);

        /// <summary>Primary positive/action accent: "deep emerald with luminous green edge
        /// energy."</summary>
        public static readonly Color ColorAccentEmerald = new Color(0.14f, 0.36f, 0.24f);

        /// <summary>Information/resource accent: "controlled astral cyan/teal." New token - no
        /// prior screen had a shared cyan value to derive this from; used sparingly per the doc's
        /// "cyan/green/red only where functionally meaningful."</summary>
        public static readonly Color ColorAccentCyan = new Color(0.32f, 0.7f, 0.74f);

        /// <summary>Enemy/danger accent: "dried-blood red and ember red." New token, same
        /// reasoning as ColorAccentCyan.</summary>
        public static readonly Color ColorAccentRed = new Color(0.55f, 0.16f, 0.14f);

        /// <summary>Primary readable text on a dark surface: "warm ivory."</summary>
        public static readonly Color ColorTextPrimary = new Color(0.92f, 0.9f, 0.8f);
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
            scaler.matchWidthOrHeight = MatchWidthOrHeight;

            EnsureEventSystem();
            return canvas;
        }

        /// <summary>Locked project-wide (CC 6939cfc, 2026-08-27): match=1 (height). Landscape-
        /// only game (CLAUDE.md: "LANDSCAPE 1920x1080, never portrait") - real landscape phones
        /// run wider than 16:9 (19.5:9 to 21:9 is common), and match=1 guarantees the full 1080
        /// reference height is always visible, trading extra horizontal space on wide devices
        /// instead of vertical clipping/overlap. Was 0 (match width, Unity's own default) before
        /// this - a real, measured upstream cause of overlap bugs fixed one screen at a time
        /// tonight (a 1920x1440 canvas.rect was measured under match=0 with a 4:3 render surface
        /// against the 16:9 reference; feed-card content built from literal 1920x1080 pixel math
        /// was wrong relative to that ACTUAL rendered size). Known, accepted, tracked-separately
        /// risk: on a device NARROWER than 16:9 (e.g. a 16:10 tablet), match=1 crops width instead
        /// - phones are the primary target and sit wider, so this is correct for them; tablets
        /// get their own pass if they enter scope.</summary>
        public const float MatchWidthOrHeight = 1f;

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
                WarnOnceMissingSprite(spritePath, "CreateFullscreenBackground");
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
                WarnOnceMissingSprite(panelSpritePath, "CreateHeaderShell");
            }

            RectTransform rect = headerObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            // Called AFTER the rect is positioned/sized above, same apply-before-position
            // discipline as ApplyFramedPanel - real regression found by external audit
            // (CC, 2026-08-27): this set Image.Type.Sliced with no border-fit call at all.
            FitSlicedBorderToRect(headerImage);
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
                // Nav skin sprites are one of the two call-time-critical classes (CC 2026-08-27):
                // a silent fallback here is indistinguishable from success and leaves the player
                // staring at the thing they're supposed to press.
                if (highlightedSprite == null) WarnOnceMissingSprite(HomeNavHoverPath, "CreateButton (nav skin, highlighted)", critical: true);
                if (pressedSprite == null) WarnOnceMissingSprite(HomeNavPressedPath, "CreateButton (nav skin, pressed)", critical: true);
                if (disabledSprite == null) WarnOnceMissingSprite(HomeNavDisabledPath, "CreateButton (nav skin, disabled)", critical: true);
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
                // Only warn when a real path was actually attempted - spritePath is legitimately
                // null for callers with no art authored yet, same reasoning as ApplyFramedPanel's
                // identical guard just below.
                if (!string.IsNullOrEmpty(spritePath))
                {
                    WarnOnceMissingSprite(spritePath, "CreateButton", critical: useHomeNavSkin);
                }
            }

            RectTransform rect = buttonObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            EnforceMinTouchTarget(rect);
            // Called AFTER sizing/EnforceMinTouchTarget above, same reasoning as CreateHeaderShell's
            // identical fix - real regression found by external audit (CC, 2026-08-27): this set
            // Image.Type.Sliced with no border-fit call at all.
            FitSlicedBorderToRect(image);

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

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = panelAnchorMin;
            panelRect.anchorMax = panelAnchorMax;
            panelRect.pivot = panelPivot;
            panelRect.anchoredPosition = panelAnchoredPos;
            panelRect.sizeDelta = panelSize;

            // Applied AFTER final positioning - ApplyFramedPanel's border-fit math reads the
            // rect's live size at call time (see FitSlicedBorderToRect).
            ApplyFramedPanel(panel, null, panelColor, panelColor, kind: FramedPanelKind.Modal);
            return panelRect;
        }

        /// <summary>
        /// Root cause of the "flat boxes everywhere" complaint (register 2026-08-26,
        /// "ROOT CAUSE FOUND for the 'flat boxes everywhere' UI complaint", commit c13d8a0):
        /// a Sliced Image's border thickness is a FIXED size in canvas-reference units, driven by
        /// the sprite's own border (in source pixels) at spritePixelsToUnits=100. When a caller's
        /// rect is smaller than the sum of the two opposing border edges on an axis, Unity has no
        /// room left for the stretchable center band and the border strips overlap/collapse -
        /// this renders as a flat, mushy, unstyled-looking block even though the sprite loaded
        /// correctly, is Sliced, and every load-time check passes (the failure is geometric, not a
        /// load failure - measured example: ui_button_secondary_normal_v1's 64+64=128px vertical
        /// border vs DeckBuilder's ~62px-tall rail buttons, roughly 2x over).
        ///
        /// Fix: Image.pixelsPerUnitMultiplier scales the RENDERED border size down (a multiplier
        /// of 2 halves it) without touching the sprite asset or the caller's rect. Computed HERE,
        /// per-instance, from the image's own live rect vs its sprite's own border - not a fixed
        /// global constant - because call sites span tiny nav buttons to full content panels, and
        /// a constant large enough to fix the smallest would over-shrink borders on everything
        /// that already has room (borders always render at their full authored size, multiplier=1,
        /// whenever the rect is big enough - only shrinks when it would otherwise collapse).
        ///
        /// Deliberately compares border pixels straight against RectTransform.rect (reference-
        /// resolution units), with NO CanvasScaler.scaleFactor factored in. Every screen here uses
        /// CanvasScaler.ScaleMode.ScaleWithScreenSize with referencePixelsPerUnit left at its
        /// default (100, never overridden anywhere in this project), matching every one of these
        /// sprites' own spritePixelsToUnits=100 - so border-in-source-pixels already lands 1:1 in
        /// the same reference-unit space RectTransform.rect reports. scaleFactor is a single
        /// uniform transform applied to the WHOLE canvas hierarchy (rect geometry and sliced-
        /// border sizing both happen upstream of it, in the same local space) when converting that
        /// hierarchy to physical screen pixels - multiplying it in here would make the correction
        /// vary by the player's device resolution, which the "boxes everywhere" bug itself does
        /// not (it is a fixed, design-time relationship between an authored rect and an authored
        /// border, reproducible at any device resolution since ScaleWithScreenSize is exactly the
        /// mechanism that keeps reference-unit geometry consistent across devices).
        ///
        /// Requires the caller's RectTransform to already have its FINAL anchors/sizeDelta set
        /// before this runs - it reads the rect once, synchronously, and does not watch for later
        /// changes (a resize-watcher approach was tried and disproven, see the removed-code note
        /// above). Every call site in this project sets final positioning before applying chrome
        /// except one, which was reordered instead (EmpirePresenter.cs's BuildConstructionPanel).
        /// If rect size is genuinely still zero when this runs, it's a no-op (multiplier stays at
        /// Unity's own default of 1) rather than guessing from stale geometry.
        /// </summary>
        public static void FitSlicedBorderToRect(Image image)
        {
            if (image == null || image.sprite == null || image.type != Image.Type.Sliced) return;

            RectTransform rect = image.rectTransform;
            float rectWidth = rect.rect.width;
            float rectHeight = rect.rect.height;
            if (rectWidth <= 0f || rectHeight <= 0f) return;

            Vector4 border = image.sprite.border; // (left, bottom, right, top), source pixels
            float borderWidthSum = border.x + border.z;
            float borderHeightSum = border.y + border.w;

            // A few px of real stretchable center left over, even when borders are shrunk to fit -
            // otherwise a rect exactly equal to the border sum still renders as two abutting edges
            // with no visible center at all, which looks identical to the original collapse bug.
            const float MinCenterPx = 6f;

            float neededForWidth = borderWidthSum / Mathf.Max(1f, rectWidth - MinCenterPx);
            float neededForHeight = borderHeightSum / Mathf.Max(1f, rectHeight - MinCenterPx);

            // Never below 1 (never enlarge past the art's authored size) and clamped at 4 as a
            // sanity ceiling - a rect needing more than 4x shrink is almost certainly a real
            // layout bug elsewhere (an element far too small for this art), not something this
            // fix should silently paper over into an illegibly thin border.
            image.pixelsPerUnitMultiplier = Mathf.Clamp(Mathf.Max(1f, neededForWidth, neededForHeight), 1f, 4f);
        }

        private static readonly System.Collections.Generic.HashSet<string> _warnedMissingSpritePaths =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>Standardized once-per-path fallback logging for every Resources.Load&lt;Sprite&gt;
        /// call site that silently falls back to a flat colour on failure (external audit finding,
        /// CC 2026-08-27): CreateFullscreenBackground, CreateButton, CreateHeaderShell and
        /// ApplyFramedPanel each warned inconsistently or not at all before this - a screen with a
        /// missing sprite passed every test because the CODE ran; only the player saw the flat
        /// colour. Warn-once per path (not per call site), same reasoning as HomeV3UiLibrary's own
        /// _warnedPrimaryButtonArtMissing-style gates.
        ///
        /// <paramref name="critical"/> upgrades this to Debug.LogError instead of LogWarning for
        /// asset classes where silent degradation is unacceptable (primary CTA art, nav skins) -
        /// Unity's EditMode test runner fails a test on any unhandled LogType.Error by default, so
        /// a critical miss now fails a build rather than only ever logging.</summary>
        public static void WarnOnceMissingSprite(string path, string context, bool critical = false)
        {
            if (string.IsNullOrEmpty(path) || !_warnedMissingSpritePaths.Add(path)) return;
            string message = $"[UISharedFoundation] {context}: failed to load sprite '{path}' - " +
                "falling back to a flat colour fill (warned once, not per call site).";
            if (critical) Debug.LogError(message);
            else Debug.LogWarning(message);
        }

        // An earlier version of this fix tried a resize-watcher (OnRectTransformDimensionsChange)
        // so a caller applying chrome before finishing positioning (e.g. EmpirePresenter.cs:207)
        // would still end up correct with zero per-call-site changes. Tested directly
        // (SlicedBorderFitTests.cs) and DISPROVEN: the message did not fire synchronously in
        // EditMode even after an explicit Canvas.ForceUpdateCanvases() pump - real evidence, not
        // assumption. Removed rather than shipped as unverified complexity. The actual fix for
        // that one known bad-order call site is simpler and fully verified: EmpirePresenter.cs
        // was reordered to apply chrome AFTER positioning, same as every other call site already
        // does. FitSlicedBorderToRect below is the one real entry point everything calls.

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
        /// <param name="tier">Optional frame-tier classification (register: "Framing - LOCKED
        /// 2026-08-27"). Null (the default, every existing call site) keeps the exact prior
        /// behavior - real art if one exists, else the flat gradient fallback. When given, the
        /// PROCEDURAL fallback becomes the tier-aware bordered sprite (border thickness + fill
        /// alpha per tier) instead of the flat gradient - real authored art still wins first for
        /// any kind that has one, unchanged. <paramref name="borderColor"/> defaults to the
        /// project's structural-trim token when omitted.</param>
        public static void ApplyFramedPanel(Image target, string frameResourcePath, Color topColor, Color bottomColor,
            int cornerRadius = UIFrozenTokens.RadiusPrimary, FramedPanelKind kind = FramedPanelKind.ContentPanel,
            UIDesignTokens.FrameTier? tier = null, Color? borderColor = null)
        {
            if (target == null) return;

            // Catches a FUTURE call site reintroducing the apply-before-position bug (today's 22
            // known offenders are already fixed - see the register's "flat boxes everywhere" root
            // cause entry). Unity's own default RectTransform size is exactly 100x100, so a rect
            // still at that value here means the caller hasn't positioned it yet - not proof, but
            // a strong, cheap real-time signal worth a warning rather than a silent wrong border.
            Rect liveRect = target.rectTransform.rect;
            if (Mathf.Approximately(liveRect.width, 100f) && Mathf.Approximately(liveRect.height, 100f))
            {
                Debug.LogWarning($"[UISharedFoundation] ApplyFramedPanel called on '{target.name}' " +
                    $"while its rect is still Unity's default 100x100 - this usually means chrome " +
                    $"is being applied before the caller finishes positioning the RectTransform, " +
                    $"which fits the 9-slice border against the wrong size. Move this call to after " +
                    $"final anchors/sizeDelta are set.");
            }

            string path = string.IsNullOrEmpty(frameResourcePath) ? DefaultFramedPanelResourcePath(kind) : frameResourcePath;
            Sprite real = string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
            if (real != null)
            {
                target.sprite = real;
                target.type = Image.Type.Sliced;
                target.color = Color.white;
                FitSlicedBorderToRect(target);

                // Manifest rule (UNITY_9SLICE_IMPORT_MANIFEST.md): the diamond ornament is a
                // separate, non-stretched child layer, never baked into the sliced panel - 9-slice
                // stretches the bottom-middle band horizontally, which would stretch/displace a
                // baked-in diamond at any width other than the source.
                if (kind == FramedPanelKind.ContentPanel)
                    AddContentPanelDiamondOverlay(target.transform);
            }
            else
            {
                target.sprite = tier.HasValue
                    ? CreateOrGetTieredFrameSprite(tier.Value, topColor, borderColor ?? UIFrozenTokens.ColorAccentBronze, cornerRadius)
                    : CreateOrGetRoundedPanelSprite(topColor, bottomColor, cornerRadius);
                target.type = Image.Type.Sliced;
                target.color = Color.white;
                // Real regression, found by external audit (CC, 2026-08-27): this was gated on
                // `tier.HasValue`, but CreateOrGetRoundedPanelSprite (the untiered branch, the
                // COMMON case whenever no authored art exists) sets a 9-slice border too - same
                // "flat boxes everywhere" collapse this session's own root-cause fix addressed for
                // authored art, just left unfixed here. Every Image.Type.Sliced assignment in this
                // method needs its border fitted, authored or procedural, tiered or not.
                FitSlicedBorderToRect(target);

                // Only warn when there WAS a real path to try and it genuinely failed to load -
                // DefaultFramedPanelResourcePath legitimately returns null for a kind with no art
                // authored yet, and that's not a bug. ~24+ call sites share this fallback.
                //
                // Tier1Hero implies critical (CC, 2026-08-27): the locked definition of Tier 1 is
                // literally "the screen's ONE primary CTA" - a button whose chrome is Tier-1 IS the
                // primary CTA by definition, so a missing sprite there is the exact silent-fallback
                // case Finding 2 exists to catch, not a cosmetic miss. Reuses the tier already
                // threaded through this method rather than a new parameter, so callers that pass a
                // tier for border-style reasons get the criticality for free.
                WarnOnceMissingSprite(path, "ApplyFramedPanel/CreateFramedPanel (falls back to the procedural rounded panel)",
                    critical: tier == UIDesignTokens.FrameTier.Tier1Hero);
            }
        }

        private const string ContentPanelDiamondResourcePath = "UI/SharedFoundation/ui_content_panel_diamond_overlay_v1";

        private static void AddContentPanelDiamondOverlay(Transform panelTransform)
        {
            if (panelTransform.Find("DiamondOverlay") != null) return; // idempotent on rebuild
            Sprite diamond = Resources.Load<Sprite>(ContentPanelDiamondResourcePath);
            if (diamond == null) return;

            GameObject overlay = new GameObject("DiamondOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(panelTransform, false);
            Image img = overlay.GetComponent<Image>();
            img.sprite = diamond;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;

            RectTransform rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(58f, 38.5f); // native 116x77, halved for a sane default UI scale
        }

        /// <summary>Builds a new panel GameObject with <see cref="ApplyFramedPanel"/> already
        /// applied - the convenience most call sites want ("give me a real bordered panel"),
        /// matching the existing CreateModalShell/CreateCardPrimitive style.</summary>
        public static RectTransform CreateFramedPanel(Transform parent, string name, Vector2 size,
            string frameResourcePath, Color topColor, Color bottomColor, int cornerRadius = UIFrozenTokens.RadiusPrimary,
            FramedPanelKind kind = FramedPanelKind.ContentPanel)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            ApplyFramedPanel(go.GetComponent<Image>(), frameResourcePath, topColor, bottomColor, cornerRadius, kind);
            return rect;
        }

        /// <summary>Which real 9-slice art family a framed panel should use by default when no
        /// explicit frameResourcePath is given. Matches the 4 shapes in the approved
        /// NineSlice_Production_Kit V1 (UNITY_9SLICE_IMPORT_MANIFEST.md) - each has different
        /// border insets (a list row's are much thinner than a content panel's), so one shape
        /// can't stand in for another without visibly wrong proportions.</summary>
        public enum FramedPanelKind
        {
            ContentPanel,
            ListRow,
            Modal,
        }

        /// <summary>Real art paths for the approved 9-slice kit (2026-08-26). Returns null for a
        /// kind with no real asset yet, which ApplyFramedPanel treats as "fall back to
        /// procedural" - never silently substitutes a differently-shaped sprite.</summary>
        public static string DefaultFramedPanelResourcePath(FramedPanelKind kind)
        {
            switch (kind)
            {
                case FramedPanelKind.ContentPanel: return "UI/SharedFoundation/ui_content_panel_v1";
                case FramedPanelKind.ListRow: return "UI/SharedFoundation/ui_list_row_v1";
                case FramedPanelKind.Modal: return "UI/SharedFoundation/ui_modal_dialog_v1";
                default: return null;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _roundedPanelSpriteCache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>Cached wrapper around <see cref="CreateRoundedPanelSprite"/> - ApplyFramedPanel's
        /// procedural fallback previously allocated a brand new Texture2D/Sprite on every single
        /// call with no reuse, even for the exact same color/radius/size combo repeated across many
        /// call sites and every screen rebuild. The real color+radius+size space this UI actually
        /// uses is small and fixed, so a simple unbounded-but-small dictionary keyed on the
        /// quantized inputs is enough - no eviction, that would be premature generality for a cache
        /// this bounded (same reasoning already applied elsewhere tonight against inventing scope
        /// nothing has asked for).</summary>
        public static Sprite CreateOrGetRoundedPanelSprite(Color topColor, Color bottomColor,
            int cornerRadius = UIFrozenTokens.RadiusPrimary, int size = 64)
        {
            string Quant(Color c) => $"{c.r:F3},{c.g:F3},{c.b:F3},{c.a:F3}";
            string key = $"tr:{Quant(topColor)}|br:{Quant(bottomColor)}|r:{cornerRadius}|s:{size}";
            if (_roundedPanelSpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            Sprite created = CreateRoundedPanelSprite(topColor, bottomColor, cornerRadius, size);
            _roundedPanelSpriteCache[key] = created;
            return created;
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

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _tieredFrameSpriteCache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>Border stroke + fill alpha for each of the four locked frame tiers (register:
        /// "Framing - LOCKED 2026-08-27: four tiers + weighted ratio"). Values are in the
        /// generator's own texture-px space, scaled to the real rect the same way every other
        /// sliced sprite here is (FitSlicedBorderToRect) - not a literal screen-px guarantee.
        /// CC's call (2026-08-27): build this procedurally rather than commission four tiers of
        /// bordered art - fewer assets that can silently fail to load, and it matches how the rest
        /// of this project is built (no prefabs/scenes, everything generated). Tier 1 keeps real
        /// authored art as its first choice wherever one exists (ApplyFramedPanel's normal
        /// real-sprite-first behavior, unchanged) - this generator is what Tier 1 falls back to
        /// when no art exists yet, and what Tiers 2-4 use directly since they were never meant to
        /// carry ornate authored art.</summary>
        private struct FrameTierSpec
        {
            public float BorderThicknessPx;
            public float FillAlpha;
            public bool InnerGlow;
        }

        private static FrameTierSpec SpecFor(UIDesignTokens.FrameTier tier)
        {
            switch (tier)
            {
                case UIDesignTokens.FrameTier.Tier1Hero:
                    return new FrameTierSpec { BorderThicknessPx = 5f, FillAlpha = UIDesignTokens.FillAlpha(tier), InnerGlow = true };
                case UIDesignTokens.FrameTier.Tier2Section:
                    return new FrameTierSpec { BorderThicknessPx = 3f, FillAlpha = UIDesignTokens.FillAlpha(tier), InnerGlow = false };
                case UIDesignTokens.FrameTier.Tier3Utility:
                    return new FrameTierSpec { BorderThicknessPx = 1.5f, FillAlpha = UIDesignTokens.FillAlpha(tier), InnerGlow = false };
                case UIDesignTokens.FrameTier.Tier4Surface:
                default:
                    return new FrameTierSpec { BorderThicknessPx = 0f, FillAlpha = UIDesignTokens.FillAlpha(tier), InnerGlow = false };
            }
        }

        public static Sprite CreateOrGetTieredFrameSprite(UIDesignTokens.FrameTier tier, Color fillColor, Color borderColor,
            int cornerRadius = UIFrozenTokens.RadiusPrimary, int size = 96)
        {
            string Quant(Color c) => $"{c.r:F3},{c.g:F3},{c.b:F3},{c.a:F3}";
            string key = $"tier:{tier}|fill:{Quant(fillColor)}|border:{Quant(borderColor)}|r:{cornerRadius}|s:{size}";
            if (_tieredFrameSpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            Sprite created = CreateTieredFrameSprite(tier, fillColor, borderColor, cornerRadius, size);
            _tieredFrameSpriteCache[key] = created;
            return created;
        }

        /// <summary>Same rounded-corner soft-edge technique as <see cref="CreateRoundedPanelSprite"/>,
        /// extended with an actual border STROKE (a ring of <c>borderColor</c> a fixed distance
        /// from the edge, not just a tinted fill) and an optional inner glow band just inside it -
        /// Tier 1's "inner glow, strong shadow" without needing authored art. Tier 4 (0px border)
        /// degrades to a plain tinted rounded fill, matching its own "no border - spacing/tint/
        /// divider only" definition.</summary>
        public static Sprite CreateTieredFrameSprite(UIDesignTokens.FrameTier tier, Color fillColor, Color borderColor,
            int cornerRadius = UIFrozenTokens.RadiusPrimary, int size = 96)
        {
            FrameTierSpec spec = SpecFor(tier);
            Color fill = new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * spec.FillAlpha);
            float borderPx = spec.BorderThicknessPx;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance from this pixel to the nearest edge of the rounded-rect boundary
                    // (negative outside the shape, positive inside) - reused for both the corner
                    // soft-edge alpha AND for deciding border-vs-fill-vs-glow banding.
                    float cx = x < cornerRadius ? cornerRadius : (x >= size - cornerRadius ? size - cornerRadius - 1 : x);
                    float cy = y < cornerRadius ? cornerRadius : (y >= size - cornerRadius ? size - cornerRadius - 1 : y);
                    bool nearCorner = (x < cornerRadius || x >= size - cornerRadius) && (y < cornerRadius || y >= size - cornerRadius);
                    float distToOuterEdge;
                    if (nearCorner)
                    {
                        float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        distToOuterEdge = cornerRadius - dist;
                    }
                    else
                    {
                        distToOuterEdge = Mathf.Min(x, size - 1 - x, y, size - 1 - y);
                    }

                    float shapeAlpha = Mathf.Clamp01(distToOuterEdge + 1.5f); // 1.5px anti-alias band
                    Color pixel;
                    if (borderPx > 0f && distToOuterEdge < borderPx)
                    {
                        pixel = borderColor;
                    }
                    else if (spec.InnerGlow && distToOuterEdge < borderPx + 4f)
                    {
                        // A soft brighten just inside the border, fading back to the plain fill.
                        float glowT = Mathf.Clamp01((distToOuterEdge - borderPx) / 4f);
                        pixel = Color.Lerp(Color.Lerp(fill, Color.white, 0.18f), fill, glowT);
                    }
                    else
                    {
                        pixel = fill;
                    }

                    tex.SetPixel(x, y, new Color(pixel.r, pixel.g, pixel.b, pixel.a * shapeAlpha));
                }
            }

            tex.Apply();
            var border = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        public enum GradientDirection { TopToBottom, BottomToTop, LeftToRight, RightToLeft }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _gradientSpriteCache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Sprite CreateOrGetGradientSprite(Color baseColor, float opacity, GradientDirection direction)
        {
            string key = $"{baseColor.r:F3},{baseColor.g:F3},{baseColor.b:F3}|op:{opacity:F3}|dir:{direction}";
            if (_gradientSpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            Sprite created = CreateGradientSprite(baseColor, opacity, direction);
            _gradientSpriteCache[key] = created;
            return created;
        }

        /// <summary>Plain (non-sliced) linear alpha ramp from <c>opacity</c> at the named edge to
        /// 0 at the opposite edge. The caller controls the real falloff DISTANCE simply by how
        /// tall/wide they size the Image's RectTransform (register: "falling to 0% over 160-
        /// 240px") - this texture just encodes the ramp shape, not a fixed pixel distance.</summary>
        public static Sprite CreateGradientSprite(Color baseColor, float opacity, GradientDirection direction)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float t;
                    switch (direction)
                    {
                        case GradientDirection.TopToBottom: t = 1f - (float)y / (size - 1); break;
                        case GradientDirection.BottomToTop: t = (float)y / (size - 1); break;
                        case GradientDirection.LeftToRight: t = 1f - (float)x / (size - 1); break;
                        default: t = (float)x / (size - 1); break;
                    }

                    tex.SetPixel(x, y, new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(opacity) * t));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Scrim rank 1 (register: "Contrast + scrims - LOCKED 2026-08-27" - preferred
        /// over art): a local black-to-transparent gradient sized and positioned to sit behind a
        /// text block. Inserted as <paramref name="parent"/>'s FIRST sibling so it always renders
        /// behind whatever else that parent already contains, regardless of call order relative to
        /// the text it protects.
        ///
        /// <paramref name="size"/> and <paramref name="anchoredPosition"/> are ABSOLUTE - this
        /// helper does not measure <paramref name="parent"/> for you and has no way to know if a
        /// value is wrong for it. Real defect found and fixed (CC/CR, 2026-08-27): the identical
        /// literal `(200,200) / (800,800)` appeared at three separate call sites, all three inside
        /// a per-element loop (MemoryExpedition's tile grid, DailyLoginQuests' day plates,
        /// SpellLoadoutPicker's slot summary) - copy-pasted from a screen where those numbers were
        /// once correct, then reused against a much smaller <paramref name="parent"/> without
        /// recomputing. The result renders many times larger than the text it was meant to sit
        /// behind and covers unrelated interactive controls, invisible to any test that only
        /// checks a button exists. Every OTHER call site in the project computes size and position
        /// from <paramref name="parent"/>'s own real pixel dimensions (its SetNorm/sizeDelta chain)
        /// and is correct - a loop is exactly where a literal copy-pasted from elsewhere goes
        /// unnoticed, because it "looks like" a real value and nothing forces it to match the
        /// parent it is actually attached to. Compute both parameters from real geometry every
        /// time; never carry a size/position pair over from a different call site.</summary>
        public static Image AddLocalGradientScrim(Transform parent, Vector2 anchoredPosition, Vector2 size,
            GradientDirection direction, float opacity = 0.62f, Color? tint = null)
        {
            GameObject go = new GameObject("GradientScrim", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(GetOrCreateScrimContainer(parent), false);

            Image img = go.GetComponent<Image>();
            img.sprite = CreateOrGetGradientSprite(tint ?? Color.black, opacity, direction);
            img.type = Image.Type.Simple;
            img.raycastTarget = false;

            RectTransform rect = go.GetComponent<RectTransform>();
            // Explicit POINT anchor (both axes collapsed to one point) - not relying on Unity's
            // implicit default. This makes sizeDelta UNAMBIGUOUSLY ABSOLUTE regardless of what the
            // parent/container does: when anchorMin==anchorMax on an axis, the stretch contribution
            // to that axis is exactly zero by definition, so rect size = sizeDelta, full stop.
            // Defect found by external audit (CC, 82b5e90): the previous version left anchors
            // unset and depended on Unity's default, which on a stretch-anchored parent makes
            // sizeDelta ADDITIVE - the identical bug class that made HomeFeed's cards 786px too
            // tall (content.sizeDelta.y on a stretched axis), caught before any of the 15 screens
            // needing scrims used this.
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            AssertSizeDeltaSafe(rect, "AddLocalGradientScrim");
            return img;
        }

        // AddSemiTransparentScrimPanel (flat Image.color, no sprite) was removed 2026-08-27 (CC):
        // it moved zero measured contrast ratios everywhere it was actually used, and there is no
        // reason to maintain two supported scrim paths when only the sprite-backed
        // AddLocalGradientScrim has proven itself. Its one caller (HomePagePresenter.cs) was
        // switched to AddLocalGradientScrim in the same pass.

        private const string ScrimContainerName = "ScrimLayer";

        /// <summary>Finds or creates a dedicated first-child container to hold scrims, rather than
        /// reordering <paramref name="parent"/>'s own existing children on every call (defect
        /// found by external audit, CC 82b5e90: repeatedly calling SetSiblingIndex(0) on the
        /// parent directly can leave a LATER scrim behind an EARLIER one, or behind unrelated
        /// content the caller never intended to affect, depending on what else lives in that
        /// parent). The container is inserted as sibling 0 exactly once; every scrim added after
        /// that lives inside it, always behind everything else <paramref name="parent"/> contains.</summary>
        private static Transform GetOrCreateScrimContainer(Transform parent)
        {
            Transform existing = parent.Find(ScrimContainerName);
            if (existing != null) return existing;

            GameObject go = new GameObject(ScrimContainerName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(0);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return go.transform;
        }

        /// <summary>General guard (register/CC 82b5e90 - "please add it as a general guard, not
        /// just for scrims"): catches the exact bug shape that made HomeFeed's feed cards 786px
        /// too tall - a non-zero sizeDelta component on an axis where the rect's OWN anchorMin/
        /// anchorMax differ (a stretched axis), which makes that sizeDelta component an ADDITIVE
        /// delta on top of the stretched size rather than an absolute size. Call this after
        /// setting sizeDelta on any rect built from literal pixel math, rather than through the
        /// SetLocalNormalisedRect/SetScreenRectFromTopLeftPixels/StretchFull conventions that
        /// already handle this correctly.</summary>
        public static void AssertSizeDeltaSafe(RectTransform rect, string context)
        {
            if (rect == null) return;
            bool xStretched = !Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x);
            bool yStretched = !Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y);
            bool xUnsafe = xStretched && !Mathf.Approximately(rect.sizeDelta.x, 0f);
            bool yUnsafe = yStretched && !Mathf.Approximately(rect.sizeDelta.y, 0f);
            if (xUnsafe || yUnsafe)
            {
                Debug.LogWarning($"[UISharedFoundation] AssertSizeDeltaSafe: '{context}' sets a non-zero " +
                    $"sizeDelta ({rect.sizeDelta}) on a STRETCH-anchored axis (anchorMin={rect.anchorMin}, " +
                    $"anchorMax={rect.anchorMax}) - sizeDelta is an ADDITIVE delta on a stretched axis, " +
                    $"not an absolute size (the exact bug that made HomeFeed's cards 786px too tall). " +
                    $"This is very likely wrong - either zero the stretched axis's sizeDelta component " +
                    $"or stop stretching that axis.");
            }
        }

        /// <summary>Shadow tokens (register: default black 70% opacity, 2px down/right offset;
        /// hero/display 80% opacity, 3px offset). Support-only per the lock - never the primary
        /// contrast fix, use a scrim first. Unity's built-in legacy-UI Shadow component has no
        /// blur-radius control (offset + colour only) - an honest engine limitation, not a spec
        /// simplification; the locked 4-6px blur figure isn't reproducible with this component.</summary>
        public static void ApplyTextShadow(Text text, bool hero = false)
        {
            if (text == null) return;
            Shadow shadow = text.gameObject.GetComponent<Shadow>();
            if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = hero ? new Color(0f, 0f, 0f, 0.8f) : new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = hero ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
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
