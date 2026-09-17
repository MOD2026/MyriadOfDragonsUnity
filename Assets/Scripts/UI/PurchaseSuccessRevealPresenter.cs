using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Small, reusable success confirmation for completed purchases. Callers invoke this only
    /// after their real result callback has committed the purchase and refreshed the UI.
    /// </summary>
    public static class PurchaseSuccessRevealPresenter
    {
        public const string RootName = "PurchaseSuccessReveal";
        public const string Headline = "PURCHASE CONFIRMED";

        public static GameObject Show(Transform parent, string itemTitle)
        {
            if (parent == null) return null;
            Dismiss(parent);

            GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            root.transform.SetAsLastSibling();

            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 112f);
            rect.sizeDelta = new Vector2(760f, 84f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.10f, 0.32f, 0.22f, 0.98f);
            background.raycastTarget = false;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Text headline = UISharedFoundation.CreateText(root.transform, "Headline", Headline,
                UITextRole.Body, TextAnchor.MiddleCenter, Color.white, true, new Vector2(720f, 34f));
            headline.fontStyle = FontStyle.Bold;
            headline.rectTransform.anchorMin = new Vector2(0f, 0.50f);
            headline.rectTransform.anchorMax = new Vector2(1f, 1f);
            headline.rectTransform.offsetMin = Vector2.zero;
            headline.rectTransform.offsetMax = Vector2.zero;

            Text item = UISharedFoundation.CreateText(root.transform, "Item", itemTitle ?? string.Empty,
                UITextRole.Caption, TextAnchor.MiddleCenter, new Color(0.86f, 0.95f, 0.88f), true,
                new Vector2(720f, 28f));
            item.rectTransform.anchorMin = new Vector2(0f, 0f);
            item.rectTransform.anchorMax = new Vector2(1f, 0.50f);
            item.rectTransform.offsetMin = Vector2.zero;
            item.rectTransform.offsetMax = Vector2.zero;

            PresentationRevealRunner runner = root.AddComponent<PresentationRevealRunner>();
            runner.Initialize(group, rect);
            return root;
        }

        public static void Dismiss(Transform parent)
        {
            if (parent == null) return;
            Transform existing = parent.Find(RootName);
            if (existing == null) return;
            if (Application.isPlaying)
                Object.Destroy(existing.gameObject);
            else
                Object.DestroyImmediate(existing.gameObject);
        }
    }
}
