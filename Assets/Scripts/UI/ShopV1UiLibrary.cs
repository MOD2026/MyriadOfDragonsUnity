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

        public static bool HasShopV1Pack =>
            Load(CatalogShellName) != null
            && Load(PackOpenFrameName) != null
            && LoadStaminaTierSprite(1, unlocked: true) != null
            && LoadStaminaTierSprite(4, unlocked: false) != null;

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
