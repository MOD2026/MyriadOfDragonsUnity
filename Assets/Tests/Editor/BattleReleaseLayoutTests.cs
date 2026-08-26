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
    /// BATTLE RELEASE LAYOUT PASS - structural/geometry regression coverage for the new
    /// BattlePresentationRoot consolidation. Reuses the same real (post-layout) world-bounds
    /// technique TutorialHandDockGeometryTests already proved out (force a bottom-up
    /// LayoutRebuilder pass against a real 1920x1080 canvas size, then compare
    /// RectTransform.GetWorldCorners() bounds) rather than inventing a second one - this file
    /// only extends that coverage to the whole-screen six-region contract instead of just the
    /// hand dock.
    /// </summary>
    public class BattleReleaseLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsLayout_" + System.Guid.NewGuid().ToString("N"));
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

            // Same two EditMode-only corrections TutorialHandDockGeometryTests already
            // established as necessary (Canvas.scaleFactor needs a real Screen size that does
            // not exist in headless EditMode, and a single root-level rebuild does not resolve
            // every nested LayoutGroup/ContentSizeFitter) - see that file's own comment for why.
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
        /// corners - resolution/anchor-independent (same helper as TutorialHandDockGeometryTests).</summary>
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
        public void SingleBattlePresentationRoot_OwnsEveryNamedRegion()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Layout_RootOwnershipBootstrap");

            RectTransform root = bootstrap.BattlePresentationRootForTests;
            Assert.IsNotNull(root, "Setup: expected a single BattlePresentationRoot to exist.");

            GameObject canvasGo = GameObject.Find("Canvas");
            Assert.IsNotNull(canvasGo);
            Assert.AreEqual(1, canvasGo.transform.childCount,
                "Canvas must have exactly one direct child - the BattlePresentationRoot. " +
                "No panel (header, board, rail, hand dock, action well, backdrop, or any modal) may sit outside it.");
            Assert.AreSame(root.gameObject, canvasGo.transform.GetChild(0).gameObject);

            foreach (string regionName in new[]
                     {
                         "TopHud", "ActivityRail", "SpellRail", "HandAndPlacementPanel", "PrimaryActionPanel",
                     })
            {
                Transform region = root.Find(regionName);
                Assert.IsNotNull(region, $"Expected named region '{regionName}' to exist as a descendant of BattlePresentationRoot.");
            }

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                RectTransform playerLane = bootstrap.PlayerLaneButtonRectForTests(lane);
                RectTransform enemyLane = bootstrap.EnemyLaneButtonRectForTests(lane);
                Assert.IsNotNull(playerLane, $"Expected a Player {lane} lane button to exist.");
                Assert.IsNotNull(enemyLane, $"Expected an Enemy {lane} lane button to exist.");
                Assert.IsTrue(playerLane.IsChildOf(root), $"Player {lane} lane button must be inside BattlePresentationRoot.");
                Assert.IsTrue(enemyLane.IsChildOf(root), $"Enemy {lane} lane button must be inside BattlePresentationRoot.");
            }
        }

        [Test]
        public void BackdropImages_NeverBlockRaycasts()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Layout_BackdropRaycastBootstrap");

            // Scoped to this test's own BattlePresentationRoot, not a global GameObject.Find -
            // 18+ other presenters (Shop, DeckBuilder, Mail, etc.) also name a child "Background",
            // and a leftover from an earlier test in the same run could resolve first, making this
            // assertion order-dependent instead of deterministic (real repro, 2026-08-26: passed
            // clean alone/in small groups all night, failed only inside the full 1762-test suite).
            RectTransform root = bootstrap.BattlePresentationRootForTests;
            Transform backgroundTransform = root.Find("Background");
            Assert.IsNotNull(backgroundTransform, "Setup: expected the arena backdrop GameObject to exist.");
            GameObject background = backgroundTransform.gameObject;
            Image backgroundImage = background.GetComponent<Image>();
            Assert.IsNotNull(backgroundImage);
            Assert.IsFalse(backgroundImage.raycastTarget,
                "The full-screen arena backdrop must never intercept a tap meant for a real control drawn above it.");

            // The dim wash (only built when the real backdrop sprite loads) is a sibling Image
            // under the same root with no sprite of its own - same requirement, if present.
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.gameObject == background || image.sprite != null) continue;
                if (image.gameObject.GetComponent<Button>() != null) continue;
                if (image.transform.parent != root) continue; // only the root's own direct children (backdrop/dim), not every plain-color Image screen-wide

                Assert.IsFalse(image.raycastTarget,
                    $"Decorative root-level Image '{image.gameObject.name}' must not block raycasts.");
            }
        }

        [Test]
        public void NamedRegions_NeverOverlapEachOtherOrEitherBoard()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Layout_NoOverlapBootstrap");

            var regionBounds = new Dictionary<string, Rect>();
            foreach (string regionName in new[] { "TopHud", "ActivityRail", "SpellRail", "HandAndPlacementPanel", "PrimaryActionPanel" })
            {
                Transform region = bootstrap.BattlePresentationRootForTests.Find(regionName);
                Assert.IsNotNull(region, $"Setup: expected named region '{regionName}' to exist.");
                regionBounds[regionName] = WorldBounds((RectTransform)region);
            }

            string[] names = regionBounds.Keys.ToArray();
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = i + 1; j < names.Length; j++)
                {
                    Assert.IsFalse(regionBounds[names[i]].Overlaps(regionBounds[names[j]]),
                        $"Region '{names[i]}' ({regionBounds[names[i]]}) must never overlap region '{names[j]}' ({regionBounds[names[j]]}).");
                }
            }

            // Both boards, and the board rows against every other region - the same non-overlap
            // guarantee TutorialHandDockGeometryTests already proved for the Hand dock alone,
            // extended here to the header, rail, and action well too, and to the enemy board.
            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Rect playerBounds = WorldBounds(bootstrap.PlayerLaneButtonRectForTests(lane));
                Rect enemyBounds = WorldBounds(bootstrap.EnemyLaneButtonRectForTests(lane));

                foreach (string regionName in names)
                {
                    Assert.IsFalse(regionBounds[regionName].Overlaps(playerBounds),
                        $"Region '{regionName}' must never overlap the Player {lane} lane ({playerBounds}).");
                    Assert.IsFalse(regionBounds[regionName].Overlaps(enemyBounds),
                        $"Region '{regionName}' must never overlap the Enemy {lane} lane ({enemyBounds}).");
                }

                foreach (Lane otherLane in System.Enum.GetValues(typeof(Lane)))
                {
                    Rect otherPlayerBounds = WorldBounds(bootstrap.PlayerLaneButtonRectForTests(otherLane));
                    Assert.IsFalse(enemyBounds.Overlaps(otherPlayerBounds),
                        $"The Enemy {lane} lane must never overlap the Player {otherLane} lane ({enemyBounds} vs {otherPlayerBounds}).");
                }
            }
        }

        [Test]
        public void AutoFormationAndStartBattle_RemainReachableAndReadable_WithinTheActionWell()
        {
            CardDatabase database = SpawnDatabase();
            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards
                .Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");

            PlayerProfile profile = new PlayerProfile();
            profile.cardCollection = new List<string>(deckIds);
            profile.ApplyDataToEmpire();
            profile.activeDeckCardIds = new List<string>(deckIds);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Layout_ActionWellBootstrap");

            Assert.IsTrue(bootstrap.RecommendedLineupButtonActiveForTests, "Auto Formation must be reachable for a valid saved deck.");
            Assert.IsTrue(bootstrap.PrimaryActionButtonActiveForTests, "Start Battle must be reachable for a valid saved deck.");

            Transform actionWell = bootstrap.BattlePresentationRootForTests.Find("PrimaryActionPanel");
            Assert.IsNotNull(actionWell, "Setup: expected the action well region to exist.");
            Rect actionWellBounds = WorldBounds((RectTransform)actionWell);

            bootstrap.AutoFormationForTests();

            Assert.IsTrue(bootstrap.PrimaryActionButtonInteractableForTests, "Start Battle must be enabled (readable and usable) once Auto Formation has run.");
            Assert.AreEqual("START BATTLE", bootstrap.PrimaryActionLabelForTests);

            // "Reachable and readable" as real geometry: the same rebuild pass, re-run after
            // Auto Formation changed the board state, confirms the action well's own bounds are
            // unchanged (a fixed region, not something that shifts as the board fills) and still
            // a real, positive-area, on-screen rect - not collapsed or clipped to zero.
            GameObject canvasGo = GameObject.Find("Canvas");
            foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
            Rect actionWellBoundsAfter = WorldBounds((RectTransform)actionWell);
            Assert.Greater(actionWellBoundsAfter.width, 0f);
            Assert.Greater(actionWellBoundsAfter.height, 0f);
            const float tolerance = 0.5f; // fixed-anchor region; allow only sub-pixel float noise between rebuild passes
            Assert.AreEqual(actionWellBounds.x, actionWellBoundsAfter.x, tolerance);
            Assert.AreEqual(actionWellBounds.y, actionWellBoundsAfter.y, tolerance);
            Assert.AreEqual(actionWellBounds.width, actionWellBoundsAfter.width, tolerance);
            Assert.AreEqual(actionWellBounds.height, actionWellBoundsAfter.height, tolerance,
                "The action well's own region bounds must stay fixed regardless of board/formation state.");
        }

        private CardDatabase SpawnDatabase()
        {
            var go = new GameObject("Layout_CardDatabase");
            _spawned.Add(go);
            CardDatabase database = go.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            return database;
        }
    }
}
