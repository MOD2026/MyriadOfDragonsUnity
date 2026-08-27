using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Regression tests for HomeV3UiLibrary.CreateResourcePill shared widget.
    /// Asserts label and value bounding boxes never overlap across all metagame configurations,
    /// including narrowest 190f width (Empire), Collection (180f/200f), and Shop (210f/225f/350f).
    /// </summary>
    public class ResourcePillLayoutRegressionTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float xMin = corners.Min(v => v.x);
            float xMax = corners.Max(v => v.x);
            float yMin = corners.Min(v => v.y);
            float yMax = corners.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private (GameObject canvasGo, Text valueText) BuildPillOnCanvas(string spriteName, string label, string value, float width)
        {
            var canvasGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _spawned.Add(canvasGo);
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            Text valText = HomeV3UiLibrary.CreateResourcePill(canvasGo.transform, spriteName, label, value, width);
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);

            return (canvasGo, valText);
        }

        [TestCase("home_resource_gold_pill_v3", "Gold", "99999", 190f, TestName = "Empire_Gold_190px")]
        [TestCase("home_resource_gold_pill_v3", "Gold", "5000", 180f, TestName = "Collection_Gold_180px")]
        [TestCase("home_resource_gems_pill_v3", "Permits", "10/10", 200f, TestName = "Collection_Permits_200px")]
        [TestCase("home_resource_gold_pill_v3", "Gold", "123456", 210f, TestName = "Shop_Gold_210px")]
        [TestCase("home_resource_gems_pill_v3", "Gems", "9999", 225f, TestName = "Shop_Gems_225px")]
        [TestCase("home_resource_energy_pill_v3", "Stamina", "120/120", 350f, TestName = "Shop_Stamina_350px")]
        public void ResourcePill_LabelAndValue_NeverOverlap_AndMeetFontFloor(string spriteName, string label, string value, float width)
        {
            var (canvasGo, valueText) = BuildPillOnCanvas(spriteName, label, value, width);

            Transform plate = canvasGo.transform.Find("ResourcePill/ResourceTextPlate");
            Assert.IsNotNull(plate, "ResourceTextPlate missing from pill hierarchy");

            Text labelText = plate.Find("ResourceLabel")?.GetComponent<Text>();
            Assert.IsNotNull(labelText, "ResourceLabel component missing");
            Assert.IsNotNull(valueText, "ResourceValue component missing");

            // Font floor assertions
            Assert.GreaterOrEqual(labelText.fontSize, 22, "ResourceLabel font size must meet 22px floor");
            Assert.GreaterOrEqual(valueText.fontSize, 22, "ResourceValue font size must meet 22px floor");

            Rect labelRect = WorldRect(labelText.rectTransform);
            Rect valueRect = WorldRect(valueText.rectTransform);

            Assert.Greater(labelRect.width, 0f, "ResourceLabel must have non-zero width");
            Assert.Greater(valueRect.width, 0f, "ResourceValue must have non-zero width");

            Assert.IsFalse(labelRect.Overlaps(valueRect),
                $"Label {label} at {labelRect} overlaps Value {value} at {valueRect} on pill width {width}px");

            // Label must sit strictly to the left of value
            Assert.LessOrEqual(labelRect.xMax, valueRect.xMin,
                $"Label {label} right edge ({labelRect.xMax:F1}) must be <= Value {value} left edge ({valueRect.xMin:F1})");
        }
    }
}
