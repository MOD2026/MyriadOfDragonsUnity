using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class VipSubscriptionUiLibrary
    {
        public const string ResourceRoot = "UI/VipSubscriptionV1/";
        public const string ScreenShellName = "vip_subscription_screen_shell_v1_rgba";
        public const string StateAtlasName = "vip_subscription_state_icons_atlas_v1_rgba";

        /// <summary>Horizontal equal cells in the approved state atlas (crowns + status glyphs).</summary>
        public const int StateAtlasCellCount = 8;

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasVipSubscriptionV1Pack =>
            Load(ScreenShellName) != null && Load(StateAtlasName) != null;

        public static void ApplyFullscreenShell(Image target, Color fallback)
        {
            if (target == null) return;
            Sprite sprite = Load(ScreenShellName);
            if (sprite != null)
            {
                target.sprite = sprite;
                target.type = Image.Type.Simple;
                target.preserveAspect = true;
                target.color = Color.white;
            }
            else
            {
                target.sprite = null;
                target.color = fallback;
                Debug.LogWarning($"[VipSubscription] Failed to load shell sprite '{ScreenShellName}'.");
            }
            target.raycastTarget = false;
        }

        /// <summary>
        /// Runtime equal-width cell from the Single-mode atlas (meta has no sprite sheet slices).
        /// Returns null when the atlas is missing or the index is out of range.
        /// </summary>
        public static Sprite LoadStateAtlasCell(int cellIndex) =>
            LoadHorizontalAtlasCell(StateAtlasName, cellIndex, StateAtlasCellCount);

        internal static Sprite LoadHorizontalAtlasCell(string atlasName, int cellIndex, int cellCount)
        {
            if (cellCount <= 0 || cellIndex < 0 || cellIndex >= cellCount) return null;
            Sprite full = Load(atlasName);
            if (full == null || full.texture == null) return null;

            Rect tr = full.textureRect;
            float cellW = tr.width / cellCount;
            if (cellW < 1f || tr.height < 1f) return null;

            var cell = new Rect(tr.x + cellIndex * cellW, tr.y, cellW, tr.height);
            try
            {
                return Sprite.Create(full.texture, cell, new Vector2(0.5f, 0.5f), full.pixelsPerUnit);
            }
            catch
            {
                // Non-readable textures throw from Sprite.Create — atlas meta must keep Read/Write on.
                return null;
            }
        }

        /// <summary>Child Image showing an atlas cell; non-raycast, preserveAspect.</summary>
        public static void ApplyAtlasIcon(Transform parent, string childName, Sprite sprite,
            float left, float bottom, float right, float top)
        {
            if (parent == null || sprite == null) return;
            GameObject go = new GameObject(childName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = Color.white;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
