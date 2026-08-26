using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class MailInboxUiLibrary
    {
        public const string ResourceRoot = "UI/MailInboxV1/";
        public const string InboxShellName = "mail_inbox_shell_v1_rgba";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasMailInboxV1Pack => Load(InboxShellName) != null;

        public static void ApplyFullscreenShell(Image target, Color fallback)
        {
            if (target == null) return;
            Sprite sprite = Load(InboxShellName);
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
                Debug.LogWarning($"[MailInbox] Failed to load shell sprite '{InboxShellName}'.");
            }
            target.raycastTarget = false;
        }
    }
}
