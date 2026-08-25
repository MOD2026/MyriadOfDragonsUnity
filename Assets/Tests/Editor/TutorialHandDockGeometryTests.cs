using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// TUTORIAL HAND DOCK GEOMETRY, 2026-08-16 - corrective regression guard for "the purple hand
    /// dock overlaps the Player Back lane; its background intercepts taps, so the required
    /// Back-lane tutorial action is impossible." Two independent things are checked, because
    /// either one alone being fixed does not prove the other is: (1) the Hand dock's real,
    /// post-layout RectTransform bounds never intersect any Player lane button's bounds, and
    /// (2) even where a decorative background sits above another control, it never intercepts
    /// the tap (raycastTarget=false) - proven by actually driving the real guided-tutorial
    /// Back-lane action end to end through the production handler.
    /// </summary>
    public class TutorialHandDockGeometryTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        private GameBootstrap SpawnAndInitializeBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (candidate.name == spawnedName && !_spawned.Contains(candidate))
                        _spawned.Add(candidate);
                }
            }

            // Two EditMode-only corrections, both confirmed necessary by direct inspection
            // (dumping real RectTransform values), neither of which affects real Play Mode:
            //
            // 1. Canvas.scaleFactor is computed from the real Screen size, which does not exist
            //    in headless EditMode - the canvas's own RectTransform silently collapses to
            //    Unity's tiny fallback size (640x480) instead of this game's real reference
            //    resolution (1920x1080, CanvasWidth/CanvasHeight), which on its own would make
            //    every fixed-pixel LayoutElement size in this file look proportionally enormous
            //    and falsely "overflow" - not a real bug, an artifact of measuring 1920-reference
            //    content against a 640-wide canvas. Forcing the canvas RectTransform to the real
            //    1920x1080 size before rebuilding is what makes the measurement below meaningful.
            // 2. A single top-down ForceRebuildLayoutImmediate call on the canvas root does not
            //    reliably resolve every nested LayoutGroup/ContentSizeFitter either - confirmed
            //    directly: the three player lane buttons stayed at Unity's own pristine
            //    RectTransform default (sizeDelta 100x100, anchoredPosition 0,0, all three
            //    IDENTICAL - never actually laid out) after only a root-level call. Rebuilding
            //    every RectTransform bottom-up (deepest first) before a final root pass is what
            //    actually resolves the whole tree.
            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo != null)
            {
                RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                             .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
            }

            return bootstrap;
        }

        /// <summary>Axis-aligned world-space bounds of a RectTransform, via its own four world
        /// corners - resolution/anchor-independent, so this works regardless of the canvas's
        /// actual reference-resolution scaling.</summary>
        private static Rect WorldBounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float xMin = corners.Min(c => c.x);
            float xMax = corners.Max(c => c.x);
            float yMin = corners.Min(c => c.y);
            float yMax = corners.Max(c => c.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        [Test]
        public void HandDockBounds_NeverIntersectAnyPlayerLaneButtonBounds()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("HandDockGeometry_Bootstrap");

            RectTransform handDock = bootstrap.HandDockRectForTests;
            Assert.IsNotNull(handDock, "Setup: expected the Hand dock's RectTransform to exist.");
            Rect handDockBounds = WorldBounds(handDock);

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                RectTransform laneRect = bootstrap.PlayerLaneButtonRectForTests(lane);
                Assert.IsNotNull(laneRect, $"Setup: expected a Player {lane} lane button to exist.");
                Rect laneBounds = WorldBounds(laneRect);

                Assert.IsFalse(handDockBounds.Overlaps(laneBounds),
                    $"The Hand dock's real bounds must never overlap the Player {lane} lane button's bounds " +
                    $"(hand dock {handDockBounds}, {lane} lane {laneBounds}).");
            }
        }

        [Test]
        public void HandDockBackground_NeverBlocksRaycasts()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("HandDockRaycast_Bootstrap");

            Image background = bootstrap.HandDockBackgroundImageForTests;
            Assert.IsNotNull(background, "Setup: expected the Hand dock's own decorative background Image to exist.");
            Assert.IsFalse(background.raycastTarget,
                "A decorative background that sits above another intended target must never intercept its taps.");
        }

        [Test]
        public void GuidedTutorial_BackLaneAction_SucceedsAfterSelectingGoblinCaster()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("HandDockBackLane_Bootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            bootstrap.HandCardPressedForTests(controller.PlayerState.Hand.First(c => c.Id == "warrior"));
            bootstrap.LanePressedForTests(Lane.Front);
            bootstrap.HandCardPressedForTests(controller.PlayerState.Hand.First(c => c.Id == "novice_knight"));
            bootstrap.LanePressedForTests(Lane.Middle);
            Assert.AreEqual(TutorialStep.BackLane, bootstrap.TutorialStepForTests, "Setup: expected to have reached the Back-lane step.");

            bootstrap.HandCardPressedForTests(controller.PlayerState.Hand.First(c => c.Id == "goblin_caster"));
            Assert.AreEqual("goblin_caster", bootstrap.SelectedCardIdForTests, "Setup: expected Goblin Caster to be selectable and selected.");

            // The real production Back-lane action, through the same handler a real tap on the
            // real UI Button would call (OnLanePressed) - this is exactly the action the bug
            // report said was "impossible".
            bootstrap.LanePressedForTests(Lane.Back);

            Assert.AreEqual(TutorialStep.BeginBattle, bootstrap.TutorialStepForTests,
                "The Back-lane action must actually succeed and advance the guided step.");
            Assert.AreEqual(1, controller.PlayerState.Lanes[Lane.Back].Cards.Count);
            Assert.AreEqual("goblin_caster", controller.PlayerState.Lanes[Lane.Back].Cards[0].Definition.Id);
        }

        [Test]
        public void NoDecorativeBackgroundInTheActiveHierarchyBlocksRaycasts()
        {
            // Broader sweep than just the Hand dock: every decorative CreateAnchoredPanel
            // background anywhere in the live canvas (Enemy/Player board zones, header clusters,
            // activity rail, hand dock, selected-card box) must never be a raycast target - the
            // same class of bug could otherwise recur in any of them, not only the one already
            // reported.
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("NoBlockingBackgrounds_Bootstrap");
            GameObject canvasGo = GameObject.Find("Canvas");
            Assert.IsNotNull(canvasGo, "Setup: expected the battle canvas to exist.");

            foreach (Image image in canvasGo.GetComponentsInChildren<Image>(true))
            {
                bool isPlainDecorativeBackground = image.sprite == null
                    && image.gameObject.GetComponent<Button>() == null
                    && (image.gameObject.name.Contains("Panel") || image.gameObject.name.Contains("Cluster")
                        || image.gameObject.name.Contains("Background"));

                if (isPlainDecorativeBackground)
                {
                    Assert.IsFalse(image.raycastTarget,
                        $"Decorative background '{image.gameObject.name}' must not block raycasts to whatever is behind it.");
                }
            }
        }
    }
}
