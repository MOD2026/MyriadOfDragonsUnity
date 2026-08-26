using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Shop V1 runtime chrome from <c>Resources/UI/ShopV1/</c> (catalog shell, pack-open frame,
    /// stamina tier unlocked/locked tiles sliced 4×2 per RUNTIME_ASSET_NOTES.md).
    /// </summary>
    public static class ShopV1UiLibrary
    {
        public const string ResourceRoot = "UI/ShopV1/";
        public const string CatalogShellName = "shop_catalog_grid_landscape_v1";
        public const string PackOpenFrameName = "pack_open_reveal_landscape_v1";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public const string GemPackTileName = "shop_gem_pack_tile_shared_v1";

        /// <summary>GEM_PACK_WELL_MAP.md — normalised (x, y, w, h) top-left on the shared tile.</summary>
        public static readonly Vector4 ProductArtWell = new Vector4(0.184f, 0.074f, 0.632f, 0.482f);
        public static readonly Vector4 ProductNameWell = new Vector4(0.138f, 0.590f, 0.724f, 0.074f);
        public static readonly Vector4 PriceWell = new Vector4(0.136f, 0.711f, 0.355f, 0.103f);
        public static readonly Vector4 PityWell = new Vector4(0.524f, 0.710f, 0.336f, 0.103f);
        public static readonly Vector4 BuyActionWell = new Vector4(0.141f, 0.843f, 0.718f, 0.101f);

        /// <summary>Measured on shop_stamina_tier*_v1 (467×257) — left icon circle, center copy,
        /// right BUY plate. Top-left normalised (x, y, w, h).</summary>
        public static readonly Vector4 StaminaIconWell = new Vector4(0.094f, 0.265f, 0.201f, 0.362f);
        public static readonly Vector4 StaminaCopyWell = new Vector4(0.310f, 0.280f, 0.320f, 0.420f);
        public static readonly Vector4 StaminaBuyWell = new Vector4(0.653f, 0.311f, 0.244f, 0.292f);

        /// <summary>Catalog shell header wells (1920×1080 top-left pixels) — shell art already
        /// draws the frames; runtime only fills text/hit-targets inside them.</summary>
        public static readonly Vector4 ShellBackWellPx = new Vector4(20f, 30f, 230f, 90f);
        public static readonly Vector4 ShellTitleWellPx = new Vector4(465f, 30f, 520f, 90f);
        public static readonly Vector4 ShellGoldPillWellPx = new Vector4(1050f, 30f, 210f, 90f);
        public static readonly Vector4 ShellGemsPillWellPx = new Vector4(1269f, 30f, 225f, 90f);
        public static readonly Vector4 ShellStaminaPillWellPx = new Vector4(1504f, 30f, 350f, 90f);

        public static bool HasGemPackTile => Load(GemPackTileName) != null;

        public static bool HasShopV1Pack =>
            Load(CatalogShellName) != null
            && Load(PackOpenFrameName) != null
            && LoadStaminaTierSprite(1, unlocked: true) != null
            && LoadStaminaTierSprite(4, unlocked: false) != null
            && HasGemPackTile;

        public static Sprite LoadGemPackTile() => Load(GemPackTileName);

        /// <summary>Apply shared gem-pack frame full-bleed on a tile Image (raycast off).</summary>
        public static void ApplyGemPackTileFrame(Image target)
        {
            if (target == null) return;
            Sprite sprite = LoadGemPackTile();
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
                target.color = new Color(0.12f, 0.14f, 0.18f, 0.9f);
            }

            target.raycastTarget = false;
        }

        /// <summary>Top-left normalised (x,y,w,h) → Unity anchors on a parent RectTransform.</summary>
        public static void SetNormalizedWellFromTopLeft(RectTransform rect, Vector4 wellXywh)
        {
            if (rect == null) return;
            float x = wellXywh.x;
            float y = wellXywh.y;
            float w = wellXywh.z;
            float h = wellXywh.w;
            rect.anchorMin = new Vector2(x, 1f - (y + h));
            rect.anchorMax = new Vector2(x + w, 1f - y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static Sprite LoadStaminaTierSprite(int tierIndex1Based, bool unlocked)
        {
            if (tierIndex1Based < 1 || tierIndex1Based > 4) return null;
            string state = unlocked ? "unlocked" : "locked";
            return Load($"shop_stamina_tier{tierIndex1Based}_{state}_v1");
        }

        public static void ApplyFullscreenShell(Image target, string spriteName, Color fallback)
        {
            if (target == null) return;
            Sprite sprite = Load(spriteName);
            if (sprite != null)
            {
                target.sprite = sprite;
                target.type = Image.Type.Simple;
                target.preserveAspect = false;
                target.color = Color.white;
            }
            else
            {
                target.sprite = null;
                target.color = fallback;
            }
        }

        public static void ApplyStaminaTierSprite(Image target, int tierIndex1Based, bool unlocked)
        {
            if (target == null) return;
            Sprite sprite = LoadStaminaTierSprite(tierIndex1Based, unlocked);
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
                target.color = unlocked
                    ? new Color(0.16f, 0.28f, 0.2f, 0.95f)
                    : new Color(0.18f, 0.18f, 0.2f, 0.9f);
            }
        }
    }
}
