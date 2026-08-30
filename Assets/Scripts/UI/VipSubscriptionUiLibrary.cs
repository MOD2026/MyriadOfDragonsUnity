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

        /// <summary>Atlas cell locked to the Weekly plan socket (UI-VIP-REPAIR-DESIGN-027).</summary>
        public const int WeeklyPlanAtlasCell = 6;

        /// <summary>Atlas cell locked to the Fortnight plan socket (UI-VIP-REPAIR-DESIGN-027).</summary>
        public const int FortnightPlanAtlasCell = 7;

        /// <summary>
        /// Monthly is the one plan role the eight-cell atlas does not cover, so it ships as its own
        /// approved sprite instead of a ninth cell. Cells 0-7 keep their locked order.
        /// </summary>
        public const string MonthlyPlanIconResourceRoot = "UI/VipSubscriptionV2/";
        public const string MonthlyPlanIconName = "vip_plan_monthly_icon_v1";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasVipSubscriptionV1Pack =>
            Load(ScreenShellName) != null && Load(StateAtlasName) != null;

        /// <summary>Separate from the V1 pack flag on purpose - the Monthly icon ships outside it.</summary>
        public static bool HasMonthlyPlanIcon => LoadMonthlyPlanIcon() != null;

        public static Sprite LoadMonthlyPlanIcon() =>
            Resources.Load<Sprite>(MonthlyPlanIconResourceRoot + MonthlyPlanIconName);

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

        /// <summary>
        /// Sprite for plan socket <paramref name="planIndex"/> (0 Weekly, 1 Fortnight, 2 Monthly),
        /// matching <see cref="VipPlanKind"/>. Weekly/Fortnight read their locked atlas cells;
        /// Monthly reads the approved standalone icon. Returns null when the source is missing so a
        /// missing asset stays visible instead of silently borrowing a benefit glyph.
        /// </summary>
        public static Sprite LoadPlanSocketSprite(int planIndex)
        {
            switch (planIndex)
            {
                case 0: return LoadStateAtlasCell(WeeklyPlanAtlasCell);
                case 1: return LoadStateAtlasCell(FortnightPlanAtlasCell);
                case 2:
                    Sprite monthly = LoadMonthlyPlanIcon();
                    if (monthly == null)
                    {
                        Debug.LogWarning($"[VipSubscription] Failed to load Monthly plan icon " +
                            $"'{MonthlyPlanIconResourceRoot}{MonthlyPlanIconName}'.");
                    }
                    return monthly;
                default: return null;
            }
        }

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
