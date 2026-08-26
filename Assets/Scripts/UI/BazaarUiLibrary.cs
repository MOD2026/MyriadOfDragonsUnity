using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class BazaarUiLibrary
    {
        public const string ResourceRoot = "UI/BazaarV1/";
        public const string CatalogShellName = "bazaar_catalog_shell_v2_rgba";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasBazaarV1Pack => Load(CatalogShellName) != null;

        public static void ApplyFullscreenShell(Image target, Color fallback)
        {
            if (target == null) return;
            Sprite sprite = Load(CatalogShellName);
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
                Debug.LogWarning($"[Bazaar] Failed to load shell sprite '{CatalogShellName}'.");
            }
            target.raycastTarget = false;
        }
    }
}
