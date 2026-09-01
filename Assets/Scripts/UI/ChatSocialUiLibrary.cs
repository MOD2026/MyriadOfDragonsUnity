using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class ChatSocialUiLibrary
    {
        public const string ResourceRoot = "UI/ChatSocialV1/";
        public const string ShellName = "chat_social_shell_v2_rgba";

        /// <summary>Whole-panel chrome for the DM-unavailable state. Used as an illustration, not
        /// as a working panel: DM has no route. The channel-icon atlas is deliberately NOT wired
        /// as per-channel glyphs - it ships as a single unsliced sprite (spriteMode 1, no
        /// spritesheet metadata), and the approved reference requires slicing/semantic cell
        /// mapping to be verified before use. Channel state is carried by label + marker glyph
        /// instead until that metadata exists.</summary>
        public const string DirectMessagesChromeName = "direct_messages_panel_chrome_v1";

        public static string DirectMessagesChromeResourcePath => ResourceRoot + DirectMessagesChromeName;

        public static bool HasDirectMessagesChrome => Load(DirectMessagesChromeName) != null;

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
                Debug.LogWarning($"[ChatSocial] Failed to load shell sprite '{ShellName}'.");
            }
            target.raycastTarget = false;
        }
    }
}
