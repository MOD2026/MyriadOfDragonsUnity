using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Formation-header layout. The tutorial/mode caption sits in a deliberately thin Y-band
    /// (TitleY0..TitleY1) chosen so it never renders into the Top HUD region - GameBootstrap's own
    /// comment cites the V4 handoff collision table for exactly that: "Header controls... reserve
    /// HUD regions; no board, guide, tooltip, or combat text may render into them."
    ///
    /// Nothing masks that band, so if the caption's text is taller than its rect it overflows and
    /// renders into the reserved region anyway - which reads on screen as two text blocks stacked
    /// on top of each other. That is geometry, not rendering, so EditMode can assert it: compare
    /// the Text's own preferredHeight against the rect it was given.
    /// </summary>
    public class FormationHeaderLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDFormationHeader_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        private GameBootstrap SpawnBootstrap()
        {
            var go = new GameObject("FormationHeader_Bootstrap");
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();

            // Collect EVERY match, not just the first - the systemic teardown fix. Using Find()
            // here would reintroduce the Canvas leak this suite spent the day removing.
            foreach (string n in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    if (candidate.name == n && !_spawned.Contains(candidate)) _spawned.Add(candidate);

            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo != null)
            {
                canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
                foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                             .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
            return bootstrap;
        }

        private static Text FindCaption(GameBootstrap bootstrap)
        {
            GameObject canvasGo = GameObject.Find("Canvas");
            Assert.IsNotNull(canvasGo, "Setup: expected a Canvas.");
            Transform panel = canvasGo.transform.Find("BattlePresentationRoot/TutorialGuidanceCaption")
                              ?? canvasGo.transform.Find("TutorialGuidanceCaption");
            Assert.IsNotNull(panel, "Setup: expected the TutorialGuidanceCaption band panel to exist.");
            Text caption = panel.GetComponentInChildren<Text>(true);
            Assert.IsNotNull(caption, "Setup: expected a caption Text inside that band.");
            return caption;
        }

        [Test]
        public void TheModeCaption_FitsInsideItsOwnBand_AndCannotBleedIntoTheReservedHudRegion()
        {
            GameBootstrap bootstrap = SpawnBootstrap();

            Text caption = FindCaption(bootstrap);
            caption.gameObject.SetActive(true);
            // Longest string this caption ever shows, so the check covers the worst case rather
            // than whichever text happens to be set at this moment.
            caption.text = "AUTO FORMATION - RECOMMENDED LINEUP READY";
            LayoutRebuilder.ForceRebuildLayoutImmediate(caption.rectTransform);

            float bandHeight = caption.rectTransform.rect.height;
            float textHeight = caption.preferredHeight;

            Assert.Greater(bandHeight, 0f, "Setup: the caption band must have a real height.");
            Assert.LessOrEqual(textHeight, bandHeight,
                $"Caption text is {textHeight:F2}px tall in a {bandHeight:F2}px band, so it overflows by " +
                $"{textHeight - bandHeight:F2}px. Nothing masks this band, so the overflow renders into the " +
                "Top HUD region the V4 collision table reserves - which is what reads on screen as two text " +
                "blocks stacked on each other.");
        }

        [Test]
        public void TheModeCaption_IsSingleLine_SoItCannotGrowDownwardsIntoTheBoard()
        {
            GameBootstrap bootstrap = SpawnBootstrap();

            Text caption = FindCaption(bootstrap);
            caption.gameObject.SetActive(true);
            caption.text = "AUTO FORMATION - RECOMMENDED LINEUP READY";
            LayoutRebuilder.ForceRebuildLayoutImmediate(caption.rectTransform);

            Assert.AreEqual(VerticalWrapMode.Truncate, caption.verticalOverflow,
                "A caption that may overflow vertically would push into the reserved HUD band above it.");
        }

        /// <summary>CC required this explicitly: a moved caption must not collide with anything
        /// else once placed. Asserts the caption's world rect overlaps NO other V4 region, rather
        /// than trusting the anchor arithmetic.</summary>
        [Test]
        public void TheMovedCaption_DoesNotOverlapAnyOtherLiveRegion()
        {
            GameBootstrap bootstrap = SpawnBootstrap();
            Text caption = FindCaption(bootstrap);
            caption.gameObject.SetActive(true);
            caption.text = "AUTO FORMATION - RECOMMENDED LINEUP READY";
            LayoutRebuilder.ForceRebuildLayoutImmediate(caption.rectTransform);

            Rect captionRect = WorldRect((RectTransform)caption.transform.parent);
            GameObject canvasGo = GameObject.Find("Canvas");

            string[] regionNames =
            {
                "TopHudPanel", "PlayerHudPanel", "PhaseHudPanel", "EnemyHudPanel",
                "HandAndPlacementPanel", "PrimaryActionPanel", "SpellRail", "ActivityRail",
            };

            var collisions = new List<string>();
            foreach (string name in regionNames)
            {
                Transform region = FindDeep(canvasGo.transform, name);
                if (region == null) continue;
                Rect other = WorldRect((RectTransform)region);
                if (captionRect.Overlaps(other))
                    collisions.Add($"{name} {other} vs caption {captionRect}");
            }

            CollectionAssert.IsEmpty(collisions,
                "The caption's new slot overlaps live regions: " + string.Join(" | ", collisions));
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
