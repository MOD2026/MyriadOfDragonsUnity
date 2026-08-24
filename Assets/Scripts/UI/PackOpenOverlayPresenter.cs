using System;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>Post-purchase pack reveal overlay for Shop gem SKUs.</summary>
    public static class PackOpenOverlayPresenter
    {
        public const string OverlayRootName = "PackOpenOverlay";

        /// <summary>Soft next-step after CONTINUE — Collection, not auto-navigate.</summary>
        public const string CollectionNextStepCopy =
            "Cards added to Collection — open Collection to view them.";

        /// <summary>Reveal-in-progress hint (Soft #3 clarity — tap advances multi-card packs).</summary>
        public const string RevealTapHintCopy = "Tap to reveal next";

        /// <summary>Last dismiss guidance left on Shop (survives overlay teardown for Soft UI).</summary>
        public static string LastDismissStatusForTests { get; private set; }

        public static GameObject Show(Transform shopCanvasRoot, PackReceiptResult result, Action onDismiss,
            Action onOpenCollection = null, PlayerProfile ownershipProfile = null)
        {
            if (shopCanvasRoot == null || result == null) return null;

            Dismiss(shopCanvasRoot);
            LastDismissStatusForTests = null;

            GameObject overlayRoot = new GameObject(OverlayRootName, typeof(RectTransform));
            overlayRoot.transform.SetParent(shopCanvasRoot, false);
            UISharedFoundation.StretchFull(overlayRoot.GetComponent<RectTransform>());

            RectTransform panel = UISharedFoundation.CreateModalShell(
                overlayRoot.transform,
                "PackOpenDim",
                new Color(0f, 0f, 0f, 0.72f),
                new Color(0.12f, 0.14f, 0.2f, 0.98f),
                new Vector2(920f, 760f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero);
            ApplyHomeV3PanelFrame(panel);

            string packTitle = FormatPackTitle(result.SkuId);
            RectTransform titleRect = UISharedFoundation.CreateText(
                panel, "Title", packTitle, UITextRole.Display, TextAnchor.MiddleCenter,
                new Color(0.95f, 0.92f, 0.82f), false,
                new Vector2(800f, 50f)).GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -24f);

            // Soft #3 Soft-contract subtitle must keep CollectionNextStepCopy (existing EditMode assert).
            string subtitle = result.Success
                ? $"{result.Draws.Count} card(s) · {result.GemsSpent} Gems · {CollectionNextStepCopy}"
                : $"Pack failed ({result.Error}) — no cards granted.";
            RectTransform subRect = UISharedFoundation.CreateText(
                panel, "Subtitle", subtitle, UITextRole.Body, TextAnchor.MiddleCenter, new Color(0.85f, 0.82f, 0.7f),
                false, new Vector2(860f, 56f)).GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 1f);
            subRect.anchorMax = new Vector2(0.5f, 1f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0f, -72f);

            Text progressText = UISharedFoundation.CreateText(
                panel, "RevealProgress", "", UITextRole.Body, TextAnchor.MiddleCenter,
                new Color(0.72f, 0.86f, 0.78f), false, new Vector2(860f, 36f));
            progressText.fontSize = 18;
            RectTransform progressRect = progressText.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.5f, 1f);
            progressRect.anchorMax = new Vector2(0.5f, 1f);
            progressRect.pivot = new Vector2(0.5f, 1f);
            progressRect.anchoredPosition = new Vector2(0f, -118f);

            BuildDrawScroll(panel, result, ownershipProfile);

            Transform drawContentForRunner = DrawContentForTests(shopCanvasRoot);

            bool showCollectionCta = result.Success && onOpenCollection != null;
            string continueLabel = result.Success ? "CONTINUE · Check Collection" : "CONTINUE";
            Button continueBtn = UISharedFoundation.CreateButton(
                panel, "BtnContinue", continueLabel, new Vector2(360f, 64f), new Color(0.2f, 0.45f, 0.32f),
                () =>
                {
                    if (result.Success)
                    {
                        LastDismissStatusForTests = CollectionNextStepCopy;
                        EnsureShopDismissStatus(shopCanvasRoot, CollectionNextStepCopy);
                    }

                    Dismiss(shopCanvasRoot);
                    onDismiss?.Invoke();
                });
            ApplyHomeV3ContinueButton(continueBtn);
            continueBtn.interactable = false;
            RectTransform btnRect = continueBtn.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            // Shift left when OPEN COLLECTION sits beside it (Soft stay vs navigate).
            btnRect.anchoredPosition = new Vector2(showCollectionCta ? -190f : 0f, 28f);

            Button openCollectionBtn = null;
            if (showCollectionCta)
            {
                openCollectionBtn = UISharedFoundation.CreateButton(
                    panel, "BtnOpenCollection", "OPEN COLLECTION", new Vector2(320f, 64f),
                    new Color(0.18f, 0.35f, 0.42f),
                    () =>
                    {
                        LastDismissStatusForTests = CollectionNextStepCopy;
                        Dismiss(shopCanvasRoot);
                        onOpenCollection.Invoke();
                    });
                ApplyHomeV3ContinueButton(openCollectionBtn);
                openCollectionBtn.interactable = false;
                RectTransform openRect = openCollectionBtn.GetComponent<RectTransform>();
                openRect.anchorMin = new Vector2(0.5f, 0f);
                openRect.anchorMax = new Vector2(0.5f, 0f);
                openRect.pivot = new Vector2(0.5f, 0f);
                openRect.anchoredPosition = new Vector2(190f, 28f);
            }

            PackOpenRevealRunner runner = overlayRoot.AddComponent<PackOpenRevealRunner>();

            // Tap anywhere on the draw area to advance — avoids dead-wait on multi-card packs.
            Button tapAdvance = CreateTapAdvanceLayer(panel, runner);

            runner.Initialize(
                CollectRevealTiles(drawContentForRunner),
                onAllRevealed: () =>
                {
                    continueBtn.interactable = true;
                    if (openCollectionBtn != null) openCollectionBtn.interactable = true;
                    if (tapAdvance != null) tapAdvance.gameObject.SetActive(false);
                    if (progressText != null)
                        progressText.text = FormatRevealComplete(result.Draws.Count);
                },
                onRevealProgress: (revealed, total) =>
                {
                    if (progressText == null) return;
                    progressText.text = FormatRevealProgress(revealed, total);
                });

            return overlayRoot;
        }

        public static PackOpenRevealRunner RevealRunnerForTests(Transform shopCanvasRoot)
        {
            Transform overlay = shopCanvasRoot?.Find(OverlayRootName);
            return overlay != null ? overlay.GetComponent<PackOpenRevealRunner>() : null;
        }

        private static PackOpenRevealRunner.RevealTile[] CollectRevealTiles(Transform drawContent)
        {
            if (drawContent == null) return Array.Empty<PackOpenRevealRunner.RevealTile>();

            var tiles = new PackOpenRevealRunner.RevealTile[drawContent.childCount];
            for (int i = 0; i < drawContent.childCount; i++)
            {
                Transform child = drawContent.GetChild(i);
                tiles[i] = new PackOpenRevealRunner.RevealTile
                {
                    Group = child.GetComponent<CanvasGroup>(),
                    Rect = child.GetComponent<RectTransform>(),
                };
            }

            return tiles;
        }

        // Pack-open/reveal has no approved art in the Home/Deck/Collection V3 pack.
        // Do not borrow retired HomeV3 chrome (tutorial banner, square nav tiles).
        private static void ApplyHomeV3PanelFrame(RectTransform panel)
        {
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage == null) return;

            panelImage.sprite = null;
            panelImage.type = Image.Type.Simple;
            panelImage.color = new Color(0.12f, 0.14f, 0.18f, 0.96f);
        }

        private static void ApplyHomeV3ContinueButton(Button button)
        {
            if (button == null) return;

            Image graphic = button.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(button, graphic);
        }

        private static void ApplyHomeV3TileFrame(Image tileImage)
        {
            if (tileImage == null) return;

            tileImage.sprite = null;
            tileImage.type = Image.Type.Simple;
            tileImage.color = new Color(0.16f, 0.18f, 0.22f, 0.92f);
        }

        public static void Dismiss(Transform shopCanvasRoot)
        {
            if (shopCanvasRoot == null) return;
            Transform existing = shopCanvasRoot.Find(OverlayRootName);
            if (existing == null) return;

            if (Application.isPlaying) UnityEngine.Object.Destroy(existing.gameObject);
            else UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        public static Transform DrawContentForTests(Transform shopCanvasRoot) =>
            shopCanvasRoot?.Find($"{OverlayRootName}/PackOpenDim/ModalPanel/DrawScroll/Viewport/DrawContent");

        public static Text SubtitleForTests(Transform shopCanvasRoot) =>
            shopCanvasRoot?.Find($"{OverlayRootName}/PackOpenDim/ModalPanel/Subtitle")?.GetComponent<Text>();

        public static Text ContinueLabelForTests(Transform shopCanvasRoot) =>
            shopCanvasRoot?.Find($"{OverlayRootName}/PackOpenDim/ModalPanel/BtnContinue")?.GetComponentInChildren<Text>();

        public static Text ShopDismissStatusForTests(Transform shopCanvasRoot) =>
            shopCanvasRoot?.Find("PackOpenDismissStatus")?.GetComponent<Text>();

        private static void EnsureShopDismissStatus(Transform shopCanvasRoot, string message)
        {
            if (shopCanvasRoot == null || string.IsNullOrEmpty(message)) return;

            Transform existing = shopCanvasRoot.Find("PackOpenDismissStatus");
            Text status;
            if (existing != null)
            {
                status = existing.GetComponent<Text>();
            }
            else
            {
                status = UISharedFoundation.CreateText(
                    shopCanvasRoot, "PackOpenDismissStatus", message, UITextRole.Body, TextAnchor.MiddleCenter,
                    new Color(0.9f, 0.86f, 0.7f), false, new Vector2(900f, 48f));
                RectTransform rect = status.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 36f);
            }

            if (status != null) status.text = message;
        }

        private static Transform BuildDrawScroll(RectTransform panel, PackReceiptResult result,
            PlayerProfile ownershipProfile)
        {
            GameObject scrollRoot = new GameObject("DrawScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollRoot.transform.SetParent(panel, false);
            RectTransform scrollRect = scrollRoot.GetComponent<RectTransform>();
            // Leave room for RevealProgress under the subtitle.
            scrollRect.anchorMin = new Vector2(0.05f, 0.14f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.72f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            scrollRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewport.transform.SetParent(scrollRoot.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);

            GameObject content = new GameObject("DrawContent", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;

            GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160f, 210f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = result.Draws.Count >= 10 ? 4 : result.Draws.Count >= 5 ? 3 : 2;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollRoot.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            CardDatabase db = CardDatabase.Instance;
            int index = 0;
            foreach (ResolvedPackDraw draw in result.Draws)
            {
                CreateDrawTile(content.transform, draw, db, index++, ownershipProfile);
            }

            return content.transform;
        }

        private static void CreateDrawTile(Transform parent, ResolvedPackDraw draw, CardDatabase db, int index,
            PlayerProfile ownershipProfile)
        {
            GameObject tile = new GameObject($"Draw_{index}_{draw.CardId}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            tile.transform.SetParent(parent, false);
            Image tileImage = tile.GetComponent<Image>();
            tileImage.color = new Color(0.18f, 0.22f, 0.3f, 1f);
            ApplyHomeV3TileFrame(tileImage);

            Card card = db != null ? db.GetCard(draw.CardId) : null;
            string displayName = card != null ? card.DisplayName : draw.CardId;
            Sprite art = db != null && card != null ? db.GetArt(card) : null;

            GameObject artObj = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artObj.transform.SetParent(tile.transform, false);
            Image artImg = artObj.GetComponent<Image>();
            artImg.sprite = art;
            artImg.preserveAspect = true;
            artImg.color = art != null ? Color.white : new Color(0.35f, 0.38f, 0.45f);
            RectTransform artRect = artObj.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0.5f, 0.55f);
            artRect.anchorMax = new Vector2(0.5f, 0.55f);
            artRect.sizeDelta = new Vector2(120f, 120f);

            var nameText = UISharedFoundation.CreateText(
                tile.transform, "Name", displayName, UITextRole.Body, TextAnchor.MiddleCenter, Color.white, false,
                new Vector2(150f, 40f));
            RectTransform nameRect = nameText.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 0.22f);
            nameRect.anchorMax = new Vector2(0.5f, 0.22f);

            string stars = new string('★', Mathf.Clamp(draw.Rarity, 1, 7));
            string drawKindLabel = FormatDrawKind(draw.DrawKind);
            var meta = UISharedFoundation.CreateText(
                tile.transform, "Meta", $"{stars}\n{drawKindLabel}", UITextRole.Body, TextAnchor.MiddleCenter,
                new Color(0.9f, 0.82f, 0.55f), false, new Vector2(150f, 44f));
            meta.fontSize = 14;
            RectTransform metaRect = meta.GetComponent<RectTransform>();
            metaRect.anchorMin = new Vector2(0.5f, 0.02f);
            metaRect.anchorMax = new Vector2(0.5f, 0.02f);

            // Ownership after receipt apply — NEW vs EXTRA COPY answers "why it matters".
            int copies = OwnedCopyCount(ownershipProfile, draw.CardId);
            string ownershipTag = copies <= 1 ? "NEW" : "EXTRA COPY";
            Color ownershipColor = copies <= 1
                ? new Color(0.45f, 0.92f, 0.62f)
                : new Color(0.85f, 0.78f, 0.55f);
            var ownership = UISharedFoundation.CreateText(
                tile.transform, "Ownership", ownershipTag, UITextRole.Body, TextAnchor.UpperLeft,
                ownershipColor, false, new Vector2(120f, 24f));
            ownership.fontSize = 14;
            ownership.fontStyle = FontStyle.Bold;
            RectTransform ownershipRect = ownership.GetComponent<RectTransform>();
            ownershipRect.anchorMin = new Vector2(0f, 1f);
            ownershipRect.anchorMax = new Vector2(0f, 1f);
            ownershipRect.pivot = new Vector2(0f, 1f);
            ownershipRect.anchoredPosition = new Vector2(8f, -6f);

            if (draw.WasPityForced || draw.WasFloorReroll)
            {
                string tag = draw.WasPityForced ? "PITY SAVE" : "FLOOR LIFT";
                var tagText = UISharedFoundation.CreateText(
                    tile.transform, "Tag", tag, UITextRole.Body, TextAnchor.UpperRight, new Color(1f, 0.75f, 0.35f),
                    false, new Vector2(100f, 24f));
                tagText.fontSize = 12;
                RectTransform tagRect = tagText.GetComponent<RectTransform>();
                tagRect.anchorMin = new Vector2(1f, 1f);
                tagRect.anchorMax = new Vector2(1f, 1f);
                tagRect.pivot = new Vector2(1f, 1f);
                tagRect.anchoredPosition = new Vector2(-6f, -6f);
            }
        }

        private static Button CreateTapAdvanceLayer(RectTransform panel, PackOpenRevealRunner runner)
        {
            if (panel == null || runner == null) return null;

            GameObject tapObj = new GameObject("TapToRevealNext", typeof(RectTransform), typeof(Image), typeof(Button));
            tapObj.transform.SetParent(panel, false);
            Image tapImg = tapObj.GetComponent<Image>();
            tapImg.color = new Color(1f, 1f, 1f, 0.01f);
            RectTransform tapRect = tapObj.GetComponent<RectTransform>();
            tapRect.anchorMin = new Vector2(0.05f, 0.14f);
            tapRect.anchorMax = new Vector2(0.95f, 0.72f);
            tapRect.offsetMin = Vector2.zero;
            tapRect.offsetMax = Vector2.zero;

            Button tapBtn = tapObj.GetComponent<Button>();
            tapBtn.targetGraphic = tapImg;
            tapBtn.transition = Selectable.Transition.None;
            tapBtn.onClick.AddListener(runner.RequestRevealNext);
            return tapBtn;
        }

        private static string FormatPackTitle(string skuId)
        {
            if (string.Equals(skuId, CollectionPackCatalog.SingleSigilSkuId, StringComparison.Ordinal))
                return "SINGLE SIGIL OPENED";
            if (string.Equals(skuId, CollectionPackCatalog.ScoutCacheSkuId, StringComparison.Ordinal))
                return "SCOUT CACHE OPENED";
            if (string.Equals(skuId, CollectionPackCatalog.WarbandCacheSkuId, StringComparison.Ordinal))
                return "WARBAND CACHE OPENED";
            if (string.Equals(skuId, CollectionPackCatalog.LegionCacheSkuId, StringComparison.Ordinal))
                return "LEGION CACHE OPENED";
            return "PACK OPENED";
        }

        private static string FormatDrawKind(PackDrawKind kind) =>
            kind == PackDrawKind.High ? "High pull" : "Standard";

        private static string FormatRevealProgress(int revealed, int total)
        {
            if (total <= 0) return string.Empty;
            if (revealed >= total) return FormatRevealComplete(total);
            if (total == 1) return "Card revealed";
            return $"Revealed {revealed} of {total} · {RevealTapHintCopy}";
        }

        private static string FormatRevealComplete(int total) =>
            total <= 1
                ? "Card locked in — ready for Collection"
                : $"All {total} cards revealed — ready for Collection";

        private static int OwnedCopyCount(PlayerProfile profile, string cardId)
        {
            if (profile == null || string.IsNullOrEmpty(cardId)) return 0;

            if (profile.UsesCollectionV1 && profile.cardProgression != null)
            {
                foreach (CardProgressionRecord record in profile.cardProgression)
                {
                    if (record != null && string.Equals(record.cardId, cardId, StringComparison.Ordinal))
                        return Mathf.Max(0, record.copyCount);
                }

                return 0;
            }

            if (profile.cardCollection == null) return 0;
            int count = 0;
            foreach (string id in profile.cardCollection)
            {
                if (string.Equals(id, cardId, StringComparison.Ordinal)) count++;
            }

            return count;
        }
    }
}
