using UnityEngine;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Authoritative placement surfaces for Home geometry (LOCKED 2026-08-25).
    /// Full-canvas stretch parents so existing pixel rect helpers keep working; NEW Home
    /// controls must parent under one of these regions rather than the canvas root.
    /// </summary>
    public static class HomeSemanticRegions
    {
        public const string TopHud = "TopHud";
        public const string TutorialStrip = "TutorialStrip";
        public const string ActionRail = "ActionRail";
        public const string ContentPanel = "ContentPanel";
        public const string Footer = "Footer";

        public static readonly string[] All =
        {
            TopHud, TutorialStrip, ActionRail, ContentPanel, Footer,
        };

        /// <summary>Creates the five stretch regions under <paramref name="canvasRoot"/> if missing.</summary>
        public static void EnsureAll(Transform canvasRoot)
        {
            if (canvasRoot == null) return;
            foreach (string name in All)
                Ensure(canvasRoot, name);
        }

        public static Transform Ensure(Transform canvasRoot, string regionName)
        {
            if (canvasRoot == null || string.IsNullOrEmpty(regionName)) return null;
            Transform existing = canvasRoot.Find(regionName);
            if (existing != null) return existing;

            var go = new GameObject(regionName, typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go.transform;
        }

        public static Transform Get(Transform canvasRoot, string regionName) =>
            canvasRoot != null ? canvasRoot.Find(regionName) : null;
    }
}
