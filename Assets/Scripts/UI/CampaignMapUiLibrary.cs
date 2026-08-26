using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public enum CampaignStageNodeVisualState
    {
        Cleared,
        Playable,
        Locked,
    }

    /// <summary>
    /// Campaign Map Full Pack V1 — backdrop, 3-state node sprites (sliced from atlas notes),
    /// detail modal chrome. Mockup PNG is intentionally not loaded.
    /// </summary>
    public static class CampaignMapUiLibrary
    {
        public const string ResourceRoot = "UI/CampaignMapV1/";
        public const string PathBackdropName = "campaign_map_path_backdrop_v1";
        public const string ModalChromeName = "campaign_stage_detail_modal_chrome_v1";
        public const string NodeClearedName = "campaign_stage_node_cleared_v1";
        public const string NodePlayableName = "campaign_stage_node_playable_v1";
        public const string NodeLockedName = "campaign_stage_node_locked_v1";
        public const string NodeAtlasName = "campaign_stage_nodes_3state_atlas_v1";

        /// <summary>RUNTIME_ASSET_NOTES — occupied map region on 1920×1080 (x, y, w, h) top-left.</summary>
        public static readonly Vector4 MapRegionTopLeftXywh = new Vector4(0f, 96f, 1420f, 984f);

        public static Sprite Load(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
            return Resources.Load<Sprite>(ResourceRoot + fileNameWithoutExtension);
        }

        public static bool HasCampaignMapV1Pack =>
            Load(PathBackdropName) != null
            && Load(ModalChromeName) != null
            && Load(NodeClearedName) != null
            && Load(NodePlayableName) != null
            && Load(NodeLockedName) != null;

        public static Sprite LoadNodeSprite(CampaignStageNodeVisualState state)
        {
            switch (state)
            {
                case CampaignStageNodeVisualState.Cleared: return Load(NodeClearedName);
                case CampaignStageNodeVisualState.Playable: return Load(NodePlayableName);
                default: return Load(NodeLockedName);
            }
        }

        public static void ApplyPathBackdrop(Image target, Color fallback)
        {
            if (target == null) return;
            Sprite sprite = Load(PathBackdropName);
            if (sprite != null)
            {
                target.sprite = sprite;
                target.type = Image.Type.Simple;
                target.preserveAspect = true; // 16:9 source; do not non-uniformly stretch
                target.color = Color.white;
            }
            else
            {
                target.sprite = null;
                target.color = fallback;
                Debug.LogWarning($"[CampaignMap] Failed to load path backdrop sprite '{PathBackdropName}'.");
            }

            target.raycastTarget = false;
        }

        public static void ApplyModalChrome(Image target)
        {
            if (target == null) return;
            Sprite sprite = Load(ModalChromeName);
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
                target.color = new Color(0.12f, 0.14f, 0.2f, 0.95f);
                Debug.LogWarning($"[CampaignMap] Failed to load modal chrome sprite '{ModalChromeName}'.");
            }

            target.raycastTarget = false;
        }

        public static void ApplyNodeSprite(Image target, CampaignStageNodeVisualState state)
        {
            if (target == null) return;
            Sprite sprite = LoadNodeSprite(state);
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
                target.color = state == CampaignStageNodeVisualState.Cleared
                    ? new Color(0.25f, 0.55f, 0.35f, 1f)
                    : state == CampaignStageNodeVisualState.Playable
                        ? new Color(0.85f, 0.65f, 0.2f, 1f)
                        : new Color(0.3f, 0.3f, 0.35f, 0.85f);
                Debug.LogWarning($"[CampaignMap] Failed to load {state} node sprite.");
            }
        }

        /// <summary>Modal wells — native 1122×1402 top-left (x,y,w,h) → Unity anchors.</summary>
        public static void SetModalWell(RectTransform rect, float x, float y, float w, float h,
            float nativeW = 1122f, float nativeH = 1402f)
        {
            if (rect == null) return;
            float nx = x / nativeW;
            float ny = y / nativeH;
            float nw = w / nativeW;
            float nh = h / nativeH;
            rect.anchorMin = new Vector2(nx, 1f - (ny + nh));
            rect.anchorMax = new Vector2(nx + nw, 1f - ny);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
