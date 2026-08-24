using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class GuildHallUiLibrary
    {
        public const string ResourceRoot = "UI/GuildHallV1/";
        public const string FlatEntryPopupName = "guild_hall_flat_entry_popup_v2_rgba";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasGuildHallV1Pack => Load(FlatEntryPopupName) != null;

        public static void ApplyFullscreenShell(Image target, Color fallback)
        {
            if (target == null) return;
            Sprite sprite = Load(FlatEntryPopupName);
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
