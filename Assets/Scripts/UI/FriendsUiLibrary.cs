using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class FriendsUiLibrary
    {
        public const string ResourceRoot = "UI/FriendsV1/";
        public const string ScreenShellName = "friends_screen_shell_v2_1920x1080_rgba";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasFriendsV1Pack => Load(ScreenShellName) != null;

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
