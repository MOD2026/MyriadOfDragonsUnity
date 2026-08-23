using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public static class UIReleaseGateTestUtility
    {
        public const float CanvasWidth = 1920f;
        public const float CanvasHeight = 1080f;

        public static void RequireInsideCanvas(string elementName, RectTransform rect, float width = CanvasWidth, float height = CanvasHeight)
        {
            if (rect == null)
            {
                Assert.Fail($"{elementName}: missing RectTransform.");
                return;
            }

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float minX = corners.Min(c => c.x);
            float minY = corners.Min(c => c.y);
            float maxX = corners.Max(c => c.x);
            float maxY = corners.Max(c => c.y);

            if (minX < 0f || minY < 0f || maxX > width || maxY > height)
            {
                Assert.Fail($"{elementName}: bounds outside canvas: x=[{minX:F1},{maxX:F1}] y=[{minY:F1},{maxY:F1}] size=({rect.rect.width:F1},{rect.rect.height:F1}).");
            }
        }

        public static void RequireNonOverlapping(string elementName, IEnumerable<RectTransform> rects)
        {
            var list = rects.Where(r => r != null).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    RectTransform a = list[i];
                    RectTransform b = list[j];
                    Rect aRect = GetScreenRect(a);
                    Rect bRect = GetScreenRect(b);
                    if (aRect.Overlaps(bRect))
                    {
                        Assert.Fail($"{elementName}: overlapping bounds detected between '{a.name}' and '{b.name}' -> '{BoundsText(a)}' and '{BoundsText(b)}'.");
                    }
                }
            }
        }

        public static void RequireVerticalOrder(string elementName, RectTransform above, RectTransform middle, RectTransform below)
        {
            if (above == null || middle == null || below == null)
            {
                Assert.Fail($"{elementName}: missing required layout elements: '{above?.name ?? "null"}', '{middle?.name ?? "null"}', '{below?.name ?? "null"}'.");
                return;
            }

            Rect aboveRect = GetScreenRect(above);
            Rect middleRect = GetScreenRect(middle);
            Rect belowRect = GetScreenRect(below);

            if (!(aboveRect.yMax >= middleRect.yMax + 0.1f && middleRect.yMax >= belowRect.yMax + 0.1f))
            {
                Assert.Fail($"{elementName}: vertical order invalid. '{above.name}' {BoundsText(above)}, '{middle.name}' {BoundsText(middle)}, '{below.name}' {BoundsText(below)}.");
            }
        }

        public static bool IsCanvasChild(RectTransform rect)
        {
            return rect != null && rect.parent != null && rect.parent.GetComponent<Canvas>() != null;
        }

        public static void RequireNestedPlacementSafety(GameObject canvasRoot, params string[] panelNames)
        {
            if (canvasRoot == null)
            {
                Assert.Fail("Nested-coordinate safety: missing canvas root object.");
                return;
            }

            foreach (string panelName in panelNames)
            {
                Transform panel = canvasRoot.transform.Find(panelName);
                if (panel == null)
                {
                    continue;
                }

                RectTransform panelRect = panel.GetComponent<RectTransform>();
                foreach (RectTransform child in panel.GetComponentsInChildren<RectTransform>(true))
                {
                    if (child == panelRect)
                    {
                        continue;
                    }

                    if (child.parent == panelRect)
                    {
                        if (!IsNormalizedParentRect(child))
                        {
                            Assert.Fail($"Nested-coordinate safety: child '{child.name}' under panel '{panelName}' is using unnormalized screen-space placement instead of parent-local placement. Bounds={BoundsText(child)}.");
                        }
                    }
                }
            }
        }

        public static void AssertActionRoot(GameObject root, string actionName)
        {
            if (root == null)
            {
                Assert.Fail($"Input gate: missing required action root '{actionName}'.");
                return;
            }

            Button button = root.GetComponent<Button>();
            Image targetGraphic = root.GetComponent<Image>();

            Assert.IsNotNull(button, $"Input gate: required action root '{actionName}' must include a Button.");
            Assert.IsTrue(button.enabled, $"Input gate: action root '{actionName}' Button must be enabled.");
            Assert.IsTrue(button.interactable, $"Input gate: action root '{actionName}' Button must be interactable.");
            Assert.IsNotNull(targetGraphic, $"Input gate: required action root '{actionName}' must include a raycastable Image.");
            Assert.IsTrue(targetGraphic.raycastTarget, $"Input gate: action root '{actionName}' root Image must be raycastable.");
            Assert.AreSame(targetGraphic, button.targetGraphic, $"Input gate: action root '{actionName}' root Button targetGraphic must be its Image.");
        }

        public static void AssertDecorativeChildrenAreNonRaycastable(GameObject root, string actionName)
        {
            if (root == null)
            {
                Assert.Fail($"Input gate: missing action root '{actionName}' when checking decorative children.");
                return;
            }

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject == root)
                {
                    continue;
                }

                if (image.raycastTarget)
                {
                    Assert.Fail($"Input gate: decorative child '{image.name}' under '{actionName}' must be non-raycastable; current raycastTarget=true, bounds={BoundsText(image.rectTransform)}.");
                }
            }

            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.gameObject == root)
                {
                    continue;
                }

                if (text.raycastTarget)
                {
                    Assert.Fail($"Input gate: decorative text '{text.name}' under '{actionName}' must be non-raycastable; current raycastTarget=true, bounds={BoundsText(text.rectTransform)}.");
                }
            }
        }

        public static void AssertNoBlockingGraphicOverAction(GameObject canvasRoot, IEnumerable<string> requiredActionNames)
        {
            if (canvasRoot == null)
            {
                Assert.Fail("Input gate: canvas root is missing.");
                return;
            }

            HashSet<string> allowed = new HashSet<string>(requiredActionNames, StringComparer.OrdinalIgnoreCase);
            foreach (Image image in canvasRoot.GetComponentsInChildren<Image>(true))
            {
                GameObject go = image.gameObject;
                if (allowed.Contains(go.name))
                {
                    continue;
                }

                RectTransform rect = image.rectTransform;
                if (!image.raycastTarget)
                {
                    continue;
                }

                if (rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one)
                {
                    Assert.Fail($"Input gate: full-screen or transparent blocking graphic '{go.name}' is raycastable above required actions. Bounds={BoundsText(rect)}.");
                }
            }
        }

        public static void AssertCardArtPresentation(GameObject cardRoot)
        {
            if (cardRoot == null)
            {
                Assert.Fail("Card gate: card root is missing.");
                return;
            }

            Image art = cardRoot.GetComponentsInChildren<Image>(true).FirstOrDefault(img => img.gameObject.name == "Art");
            Assert.NotNull(art, $"Card gate: '{cardRoot.name}' missing an 'Art' image.");
            Assert.Greater(art.rectTransform.rect.width, 0f, $"Card gate: art rect for '{cardRoot.name}' must have positive width; bounds={BoundsText(art.rectTransform)}.");
            Assert.Greater(art.rectTransform.rect.height, 0f, $"Card gate: art rect for '{cardRoot.name}' must have positive height; bounds={BoundsText(art.rectTransform)}.");

            int artIndex = art.transform.GetSiblingIndex();
            foreach (Image image in cardRoot.GetComponentsInChildren<Image>(true))
            {
                if (image == art)
                {
                    continue;
                }

                if (image.transform.IsChildOf(cardRoot.transform) && image.transform.GetSiblingIndex() > artIndex)
                {
                    if (image.name.Contains("Frame") || image.name.Contains("Border") || image.color.a >= 0.9f)
                    {
                        Assert.Fail($"Card gate: opaque frame/border '{image.name}' sits above art on '{cardRoot.name}'. art={BoundsText(art.rectTransform)}, frame={BoundsText(image.rectTransform)}.");
                    }
                }
            }
        }

        public static string BoundsText(RectTransform rect)
        {
            if (rect == null)
            {
                return "<null>";
            }

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float minX = corners.Min(c => c.x);
            float minY = corners.Min(c => c.y);
            float maxX = corners.Max(c => c.x);
            float maxY = corners.Max(c => c.y);
            return $"x=[{minX:F1},{maxX:F1}] y=[{minY:F1},{maxY:F1}] size=({rect.rect.width:F1},{rect.rect.height:F1})";
        }

        public static Rect GetScreenRect(RectTransform rect)
        {
            if (rect == null)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float minX = corners.Min(c => c.x);
            float minY = corners.Min(c => c.y);
            float maxX = corners.Max(c => c.x);
            float maxY = corners.Max(c => c.y);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private static bool IsNormalizedParentRect(RectTransform rect)
        {
            if (rect == null)
            {
                return false;
            }

            float left = rect.anchorMin.x;
            float right = rect.anchorMax.x;
            float bottom = rect.anchorMin.y;
            float top = rect.anchorMax.y;
            bool normalizedAnchors = left >= 0f && left <= 1f && right >= 0f && right <= 1f && bottom >= 0f && bottom <= 1f && top >= 0f && top <= 1f;

            RectTransform parent = rect.parent as RectTransform;
            if (parent == null)
            {
                return normalizedAnchors;
            }

            float parentWidth = Mathf.Max(1f, parent.rect.width);
            float parentHeight = Mathf.Max(1f, parent.rect.height);
            float anchoredX = Mathf.Abs(rect.anchoredPosition.x);
            float anchoredY = Mathf.Abs(rect.anchoredPosition.y);
            bool localSpaceOnly = anchoredX < parentWidth * 0.8f && anchoredY < parentHeight * 0.8f;
            return normalizedAnchors && localSpaceOnly;
        }
    }
}
