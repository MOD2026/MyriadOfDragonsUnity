using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class VipSubscriptionUiLibrary
    {
        public const string ResourceRoot = "UI/VipSubscriptionV1/";
        public const string ScreenShellName = "vip_subscription_screen_shell_v1_rgba";
        public const string StateAtlasName = "vip_subscription_state_icons_atlas_v1_rgba";

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
            }
            target.raycastTarget = false;
        }
    }
}
