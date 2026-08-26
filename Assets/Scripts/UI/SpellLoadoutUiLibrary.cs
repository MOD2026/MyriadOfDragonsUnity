using MyriadOfDragons.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class SpellLoadoutUiLibrary
    {
        public const string ResourceRoot = "UI/SpellLoadoutV1/";
        public const string ScreenShellName = "spell_loadout_shell_background_v1";
        public const string SchoolAndrasName = "spell_school_andras";
        public const string SchoolKtiniName = "spell_school_ktini";
        public const string SchoolPnevmasName = "spell_school_pnevmas";

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasSpellLoadoutV1Pack =>
            Load(ScreenShellName) != null
            && Load(SchoolAndrasName) != null
            && Load(SchoolKtiniName) != null
            && Load(SchoolPnevmasName) != null;

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

        public static Sprite LoadSchool(SpellSchool school) => school switch
        {
            SpellSchool.Ktini => Load(SchoolKtiniName),
            SpellSchool.Pnevmas => Load(SchoolPnevmasName),
            _ => Load(SchoolAndrasName),
        };

        public static void ApplySchoolIcon(Transform parent, string childName, SpellSchool school,
            float left, float bottom, float right, float top)
        {
            Sprite sprite = LoadSchool(school);
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
