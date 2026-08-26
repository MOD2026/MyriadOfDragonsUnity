using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class DailyLoginQuestsUiLibrary
    {
        public const string ResourceRoot = "UI/DailyLoginQuestsV1/";
        public const string LandscapeShellName = "daily_login_quests_landscape_v1";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasDailyLoginQuestsV1Pack => Load(LandscapeShellName) != null;

        public static void ApplyFullscreenShell(Image target, Color fallback)
        {
            if (target == null) return;
            Sprite sprite = Load(LandscapeShellName);
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
                Debug.LogWarning($"[DailyLoginQuests] Failed to load shell sprite '{LandscapeShellName}'.");
            }
        }
    }
}
