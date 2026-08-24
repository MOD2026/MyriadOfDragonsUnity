using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class MemoryExpeditionUiLibrary
    {
        public const string ResourceRoot = "UI/MemoryExpeditionV1/";
        public const string RouteChoiceShellName = "memory_expedition_route_choice_shell_v2_rgba";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasMemoryExpeditionV1Pack => Load(RouteChoiceShellName) != null;

        public static void ApplyFullscreenShell(Image target, Color fallback)
        {
            if (target == null) return;
            Sprite sprite = Load(RouteChoiceShellName);
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
