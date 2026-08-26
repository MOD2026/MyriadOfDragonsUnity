using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class GuildExpeditionUiLibrary
    {
        public const string ResourceRoot = "UI/GuildExpeditionV1/";
        public const string ScreenShellName = "guild_expedition_shell_background_v1";
        public const string StageLockedName = "stage_locked";
        public const string StageAvailableName = "stage_available";
        public const string StageCompletedName = "stage_completed";

        public enum StageState
        {
            Locked,
            Available,
            Completed,
        }

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasGuildExpeditionV1Pack =>
            Load(ScreenShellName) != null
            && Load(StageLockedName) != null
            && Load(StageAvailableName) != null
            && Load(StageCompletedName) != null;

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
                Debug.LogWarning($"[GuildExpedition] Failed to load shell sprite '{ScreenShellName}'.");
            }
            target.raycastTarget = false;
        }

        public static Sprite LoadStageState(StageState state) => state switch
        {
            StageState.Locked => Load(StageLockedName),
            StageState.Completed => Load(StageCompletedName),
            _ => Load(StageAvailableName),
        };

        public static void ApplyStageIcon(Transform parent, string childName, StageState state,
            float left, float bottom, float right, float top)
        {
            Sprite sprite = LoadStageState(state);
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
