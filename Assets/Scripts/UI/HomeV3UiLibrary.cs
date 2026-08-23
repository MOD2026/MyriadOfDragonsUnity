using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>Loads HomeV3 sprites from Resources/UI/HomeV3/ — single source for presenters.</summary>
    public static class HomeV3UiLibrary
    {
        public const string ResourceRoot = "UI/HomeV3/";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static void ApplyNavTileButton(Button button, Image targetGraphic)
        {
            if (button == null || targetGraphic == null) return;

            Sprite normal = Load("home_nav_tile_normal_v3");
            if (normal == null) return;

            targetGraphic.sprite = normal;
            targetGraphic.type = Image.Type.Sliced;
            targetGraphic.color = Color.white;
            button.targetGraphic = targetGraphic;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Load("home_nav_tile_hover_v3") ?? normal,
                pressedSprite = Load("home_nav_tile_pressed_v3") ?? normal,
                disabledSprite = Load("home_nav_tile_disabled_v3") ?? normal,
            };
        }

        public static bool HasHomeV3Pack => Load("home_nav_dock_frame_v3") != null;

        /// <summary>Applies HomeV3 identity/header frame when the art pack is present.</summary>
        public static bool TryApplyHeaderFrame(Image headerImage)
        {
            if (headerImage == null) return false;

            Sprite frame = Load("home_hud_identity_frame_v3");
            if (frame == null) return false;

            headerImage.sprite = frame;
            headerImage.type = Image.Type.Sliced;
            headerImage.color = Color.white;
            return true;
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
            labelRect.anchorMin = new Vector2(0.12f, 0.2f);
            labelRect.anchorMax = new Vector2(0.45f, 0.8f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text valueText = UISharedFoundation.CreateText(
                pillRoot.transform, "ResourceValue", value, UITextRole.Display, TextAnchor.MiddleRight,
                new Color(0.95f, 0.9f, 0.79f), true, new Vector2(100f, 30f));
            valueText.fontSize = 22;
            valueText.fontStyle = FontStyle.Bold;
            valueText.raycastTarget = false;
            RectTransform valueRect = valueText.rectTransform;
            valueRect.anchorMin = new Vector2(0.45f, 0.2f);
            valueRect.anchorMax = new Vector2(0.9f, 0.8f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            return valueText;
        }
    }
}
