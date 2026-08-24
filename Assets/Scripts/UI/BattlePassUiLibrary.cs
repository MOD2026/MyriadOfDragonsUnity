using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class BattlePassUiLibrary
    {
        public const string ResourceRoot = "UI/BattlePassV1/";
        public const string DualTrackShellName = "battle_pass_dual_track_landscape_v1";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasBattlePassV1Pack => Load(DualTrackShellName) != null;

        public static void ApplyFullscreenShell(Image target, Color fallback)
        {
            if (target == null) return;
            Sprite sprite = Load(DualTrackShellName);
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
    }
}
