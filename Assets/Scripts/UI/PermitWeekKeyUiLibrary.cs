using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class PermitWeekKeyUiLibrary
    {
        public const string ResourceRoot = "UI/PermitWeekKeyV1/";
        public const string ScreenShellName = "permit_weekly_key_shell_background_v1";
        public const string AvailableName = "permit_available";
        public const string ClaimedName = "permit_claimed";
        public const string UnavailableName = "permit_unavailable";

        public enum PermitState
        {
            Available,
            Claimed,
            Unavailable,
        }

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasPermitWeekKeyV1Pack =>
            Load(ScreenShellName) != null
            && Load(AvailableName) != null
            && Load(ClaimedName) != null
            && Load(UnavailableName) != null;

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

        public static Sprite LoadPermitState(PermitState state) => state switch
        {
            PermitState.Claimed => Load(ClaimedName),
            PermitState.Unavailable => Load(UnavailableName),
            _ => Load(AvailableName),
        };
    }
}
