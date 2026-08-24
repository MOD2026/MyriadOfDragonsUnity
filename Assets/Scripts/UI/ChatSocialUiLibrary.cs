using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class ChatSocialUiLibrary
    {
        public const string ResourceRoot = "UI/ChatSocialV1/";
        public const string ShellName = "chat_social_shell_v2_rgba";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasChatSocialV1Pack => Load(ShellName) != null;

        public static void ApplyFullscreenShell(Image target, Color fallback)
        {
            if (target == null) return;
            Sprite sprite = Load(ShellName);
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
