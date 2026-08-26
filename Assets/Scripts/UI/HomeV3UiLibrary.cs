using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Home / Deck / Collection V3 chrome from <c>Resources/UI/HomeV3/</c>, gated by
    /// docs pack <c>01_Shared_Foundation</c>. Only manifest-Approved sprites live in that folder;
    /// excluded square nav tiles, dock frame, RGB masters, and unsliced frames are not loaded.
    /// </summary>
    public static class HomeV3UiLibrary
    {
        public const string ResourceRoot = "UI/HomeV3/";
        public const string FramesRoot = "UI/Frames/";

        // These two button-chrome helpers run at 65+ call sites across every screen build - a
        // per-call-site LogWarning would flood the console on a real missing-pack regression
        // instead of surfacing it. Warn once per condition per session (CC decision, 2026-08-26).
        private static bool _warnedSecondaryButtonArtMissing;
        private static bool _warnedPrimaryButtonArtMissing;

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        /// <summary>True when the approved resource-pill pack is present (not the retired dock/nav tiles).</summary>
        public static bool HasHomeV3Pack => Load("home_resource_gold_pill_v3") != null;

        /// <summary>Identity/header frame is not Approved without slice metadata — always false.</summary>
        public static bool TryApplyHeaderFrame(Image headerImage)
        {
            if (headerImage == null) return false;
            headerImage.sprite = null;
            headerImage.type = Image.Type.Simple;
            return false;
        }

        /// <summary>Neutral charcoal/bronze action chrome — never square Home nav tiles or V2 buttons.
        /// Preserves an already-assigned sprite (icon buttons) - if a caller already gave this
        /// Image a real sprite, this never overwrites it with the secondary 9-slice art below,
        /// only applies flat fill color when there is no sprite yet.
        ///
        /// Picks up the real secondary-button 9-slice art (2026-08-26 NineSlice_Production_Kit)
        /// automatically when present - "navy/blackened-metal core with bronze trim, clearly
        /// subordinate" per Visual Authority Memory is the closest real match to what "neutral
        /// action chrome" already meant here, so every one of this function's ~65 existing call
        /// sites gets real bordered art with no per-site change, same pattern as
        /// UISharedFoundation.ApplyFramedPanel. Falls back to the prior flat-color/ColorTint
        /// behavior when the art isn't found (mirrors ApplyFramedPanel's own real-then-procedural
        /// fallback).</summary>
        public static void ApplyNeutralActionButton(Button button, Image targetGraphic, Color? fill = null)
        {
            if (button == null || targetGraphic == null) return;

            if (targetGraphic.sprite == null)
            {
                Sprite normal = Resources.Load<Sprite>("UI/SharedFoundation/ui_button_secondary_normal_v1");
                Sprite pressed = Resources.Load<Sprite>("UI/SharedFoundation/ui_button_secondary_pressed_v1");
                if (normal != null && pressed != null)
                {
                    targetGraphic.sprite = normal;
                    targetGraphic.type = Image.Type.Sliced;
                    targetGraphic.color = Color.white;
                    button.targetGraphic = targetGraphic;
                    button.transition = Selectable.Transition.SpriteSwap;
                    button.spriteState = new SpriteState
                    {
                        highlightedSprite = normal,
                        pressedSprite = pressed,
                        selectedSprite = normal,
                        disabledSprite = normal,
                    };
                    // Root cause of "flat boxes everywhere" (register 2026-08-26, commit
                    // c13d8a0/5724836): requires the caller to have already positioned
                    // targetGraphic's rect - see UISharedFoundation.FitSlicedBorderToRect.
                    UISharedFoundation.FitSlicedBorderToRect(targetGraphic);
                    return;
                }

                if (!_warnedSecondaryButtonArtMissing)
                {
                    _warnedSecondaryButtonArtMissing = true;
                    Debug.LogWarning("[HomeV3] Failed to load secondary button chrome " +
                        "('ui_button_secondary_normal_v1'/'_pressed_v1') - falling back to flat color " +
                        "for every neutral-action button this session (warned once, not per call site).");
                }

                targetGraphic.type = Image.Type.Simple;
                targetGraphic.color = fill ?? new Color(0.14f, 0.18f, 0.22f, 0.96f);
            }
            else if (fill.HasValue)
            {
                targetGraphic.type = Image.Type.Simple;
                targetGraphic.color = fill.Value;
            }

            button.targetGraphic = targetGraphic;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.85f, 0.95f, 0.95f, 1f),
                pressedColor = new Color(0.7f, 0.85f, 0.9f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.45f, 0.48f, 0.5f, 0.7f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }

        /// <summary>Legacy name kept for call sites — redirects to neutral action chrome.</summary>
        public static void ApplyNavTileButton(Button button, Image targetGraphic) =>
            ApplyNeutralActionButton(button, targetGraphic);

        /// <summary>Primary/positive-action chrome (confirm, claim, buy, subscribe) - "emerald or
        /// context-colour energy core, gold/bronze fixed end caps, ivory label, strong pressed-
        /// depth change" per Visual Authority Memory. Deliberately a separate function from
        /// ApplyNeutralActionButton rather than a parameter on it: which buttons are actually
        /// primary CTAs vs subordinate actions is a real per-call-site judgment (the doc itself
        /// says "not every button"), not something safe to default site-wide the way the
        /// secondary-art pickup was. Falls back to the same flat-emerald behavior
        /// ApplyNeutralActionButton used before real art existed if the real art isn't found.</summary>
        public static void ApplyPrimaryActionButton(Button button, Image targetGraphic)
        {
            if (button == null || targetGraphic == null) return;

            Sprite normal = Resources.Load<Sprite>("UI/SharedFoundation/ui_button_primary_normal_v1");
            Sprite pressed = Resources.Load<Sprite>("UI/SharedFoundation/ui_button_primary_pressed_v1");
            if (normal != null && pressed != null)
            {
                targetGraphic.sprite = normal;
                targetGraphic.type = Image.Type.Sliced;
                targetGraphic.color = Color.white;
                button.targetGraphic = targetGraphic;
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = normal,
                    pressedSprite = pressed,
                    selectedSprite = normal,
                    disabledSprite = normal,
                };
                UISharedFoundation.FitSlicedBorderToRect(targetGraphic);
                return;
            }

            if (!_warnedPrimaryButtonArtMissing)
            {
                _warnedPrimaryButtonArtMissing = true;
                Debug.LogWarning("[HomeV3] Failed to load primary button chrome " +
                    "('ui_button_primary_normal_v1'/'_pressed_v1') - falling back to flat emerald " +
                    "for every primary-action button this session (warned once, not per call site).");
            }

            targetGraphic.type = Image.Type.Simple;
            targetGraphic.sprite = null;
            targetGraphic.color = new Color(0.14f, 0.36f, 0.24f, 1f);
            button.targetGraphic = targetGraphic;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.85f, 0.95f, 0.9f, 1f),
                pressedColor = new Color(0.7f, 0.9f, 0.8f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.45f, 0.48f, 0.5f, 0.7f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }

        public static Sprite LoadCardFrameForRarity(int rarity)
        {
            string name = rarity >= 6 ? "Legendary_Card_Frame"
                : rarity >= 4 ? "Epic_Card_Frame"
                : rarity >= 3 ? "Rare_Card_Frame"
                : "Common_Card_Frame";
            return Resources.Load<Sprite>(FramesRoot + name);
        }

        /// <summary>HomeV3 resource pill (label + value) for metagame screen headers.</summary>
        public static Text CreateResourcePill(Transform parent, string pillSpriteName, string label, string value, float width = 190f)
        {
            var pillRoot = new GameObject("ResourcePill", typeof(RectTransform));
            pillRoot.transform.SetParent(parent, false);

            RectTransform pillRect = pillRoot.GetComponent<RectTransform>();
            pillRect.sizeDelta = new Vector2(width, 52f);

            var backing = new GameObject("PillBacking", typeof(RectTransform), typeof(Image));
            backing.transform.SetParent(pillRoot.transform, false);
            Image backingImage = backing.GetComponent<Image>();
            Sprite sprite = Load(pillSpriteName);
            if (sprite != null)
            {
                backingImage.sprite = sprite;
                backingImage.preserveAspect = true;
                backingImage.color = Color.white;
            }
            else
            {
                backingImage.color = new Color(0.14f, 0.16f, 0.22f, 0.95f);
            }

            RectTransform backingRect = backing.GetComponent<RectTransform>();
            backingRect.anchorMin = Vector2.zero;
            backingRect.anchorMax = Vector2.one;
            backingRect.offsetMin = Vector2.zero;
            backingRect.offsetMax = Vector2.zero;
            backingImage.raycastTarget = false;

            // Text plate sits ABOVE the pill art. ScrimLayer is always sibling 0 of its parent —
            // parenting the scrim to pillRoot put it under PillBacking, so the light art still
            // owned the samples (ResourceLabel/Value stayed under 2:1).
            float plateW = width * 0.62f;
            float plateH = 44f;
            var textPlate = new GameObject("ResourceTextPlate", typeof(RectTransform));
            textPlate.transform.SetParent(pillRoot.transform, false);
            RectTransform plateRect = textPlate.GetComponent<RectTransform>();
            plateRect.anchorMin = new Vector2(0f, 0f);
            plateRect.anchorMax = new Vector2(0f, 0f);
            plateRect.pivot = new Vector2(0.5f, 0.5f);
            plateRect.sizeDelta = new Vector2(plateW, plateH);
            plateRect.anchoredPosition = new Vector2(width * 0.64f, 26f);
            UISharedFoundation.AddSemiTransparentScrimPanel(
                textPlate.transform,
                new Vector2(plateW * 0.5f, plateH * 0.5f),
                new Vector2(plateW, plateH),
                UIDesignTokens.FrameTier.Tier1Hero);

            Text labelText = UISharedFoundation.CreateText(
                textPlate.transform, "ResourceLabel", label, UITextRole.Body, TextAnchor.MiddleLeft,
                Color.white, true, new Vector2(80f, 30f));
            labelText.fontSize = 16;
            labelText.raycastTarget = false;
            UISharedFoundation.ApplyTextShadow(labelText);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0.02f, 0.1f);
            labelRect.anchorMax = new Vector2(0.48f, 0.9f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text valueText = UISharedFoundation.CreateText(
                textPlate.transform, "ResourceValue", value, UITextRole.Display, TextAnchor.MiddleRight,
                Color.white, true, new Vector2(100f, 30f));
            valueText.fontSize = 22;
            valueText.fontStyle = FontStyle.Bold;
            valueText.raycastTarget = false;
            UISharedFoundation.ApplyTextShadow(valueText);
            RectTransform valueRect = valueText.rectTransform;
            valueRect.anchorMin = new Vector2(0.48f, 0.1f);
            valueRect.anchorMax = new Vector2(0.98f, 0.9f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            return valueText;
        }
    }
}
