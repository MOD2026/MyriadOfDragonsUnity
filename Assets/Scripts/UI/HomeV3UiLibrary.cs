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

        /// <summary>Neutral charcoal/bronze action chrome — never square Home nav tiles or V2 buttons.</summary>
        public static void ApplyNeutralActionButton(Button button, Image targetGraphic, Color? fill = null)
        {
            if (button == null || targetGraphic == null) return;

            targetGraphic.sprite = null;
            targetGraphic.type = Image.Type.Simple;
            targetGraphic.color = fill ?? new Color(0.14f, 0.18f, 0.22f, 0.96f);
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

            Text labelText = UISharedFoundation.CreateText(
                pillRoot.transform, "ResourceLabel", label, UITextRole.Body, TextAnchor.MiddleLeft,
                new Color(0.95f, 0.9f, 0.79f), true, new Vector2(80f, 30f));
            labelText.fontSize = 16;
            labelText.raycastTarget = false;
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0.27f, 0.2f);
            labelRect.anchorMax = new Vector2(0.55f, 0.8f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text valueText = UISharedFoundation.CreateText(
                pillRoot.transform, "ResourceValue", value, UITextRole.Display, TextAnchor.MiddleRight,
                new Color(0.95f, 0.9f, 0.79f), true, new Vector2(100f, 30f));
            valueText.fontSize = 22;
            valueText.fontStyle = FontStyle.Bold;
            valueText.raycastTarget = false;
            RectTransform valueRect = valueText.rectTransform;
            valueRect.anchorMin = new Vector2(0.55f, 0.2f);
            valueRect.anchorMax = new Vector2(0.91f, 0.8f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            return valueText;
        }
    }
}
