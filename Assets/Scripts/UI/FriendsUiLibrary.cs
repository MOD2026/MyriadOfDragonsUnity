using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public static class FriendsUiLibrary
    {
        public const string ResourceRoot = "UI/FriendsV1/";
        public const string ScreenShellName = "friends_screen_shell_v2_1920x1080_rgba";
        public const string RelationshipAtlasName = "friends_relationship_state_icons_atlas_v1_rgba";
        public const string ProfileActionAtlasName = "friends_profile_action_icons_atlas_v1_rgba";

        public const int RelationshipAtlasCellCount = 8;
        public const int ProfileActionAtlasCellCount = 7;

        // Relationship strip (L→R): online, offline, incoming, outgoing, friend, blocked, muted, sync.
        public const int RelOnline = 0;
        public const int RelOffline = 1;
        public const int RelIncoming = 2;
        public const int RelOutgoing = 3;
        public const int RelFriend = 4;
        public const int RelBlocked = 5;
        public const int RelMuted = 6;
        public const int RelSync = 7;

        // Profile actions (L→R): chat, add, unfriend, mute, block, report, locked.
        public const int ActionChat = 0;
        public const int ActionAdd = 1;
        public const int ActionUnfriend = 2;
        public const int ActionMute = 3;
        public const int ActionBlock = 4;
        public const int ActionReport = 5;
        public const int ActionLocked = 6;

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasFriendsV1Pack =>
            Load(ScreenShellName) != null
            && Load(RelationshipAtlasName) != null
            && Load(ProfileActionAtlasName) != null;

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
                Debug.LogWarning($"[Friends] Failed to load shell sprite '{ScreenShellName}'.");
            }
            target.raycastTarget = false;
        }

        public static Sprite LoadRelationshipCell(int cellIndex) =>
            LoadHorizontalAtlasCell(RelationshipAtlasName, cellIndex, RelationshipAtlasCellCount);

        public static Sprite LoadProfileActionCell(int cellIndex) =>
            LoadHorizontalAtlasCell(ProfileActionAtlasName, cellIndex, ProfileActionAtlasCellCount);

        private static Sprite LoadHorizontalAtlasCell(string atlasName, int cellIndex, int cellCount)
        {
            if (cellCount <= 0 || cellIndex < 0 || cellIndex >= cellCount) return null;
            Sprite full = Load(atlasName);
            if (full == null || full.texture == null) return null;

            Rect tr = full.textureRect;
            float cellW = tr.width / cellCount;
            if (cellW < 1f || tr.height < 1f) return null;

            var cell = new Rect(tr.x + cellIndex * cellW, tr.y, cellW, tr.height);
            try
            {
                return Sprite.Create(full.texture, cell, new Vector2(0.5f, 0.5f), full.pixelsPerUnit);
            }
            catch
            {
                return null;
            }
        }

        public static void ApplyAtlasIcon(Transform parent, string childName, Sprite sprite,
            float left, float bottom, float right, float top)
        {
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
