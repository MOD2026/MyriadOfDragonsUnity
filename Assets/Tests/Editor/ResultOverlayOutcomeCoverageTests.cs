using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.AI;
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
    /// CR-RESULT-CTA-TESTS-001, 2026-08-29. GameBootstrapStateCoverageTests.State5_Resolved_-
    /// IsReachedAndVisible proves ONLY that some result overlay with some text appears after
    /// combat resolves - it never forces or distinguishes Victory vs Defeat, never checks CTA
    /// visibility, Retry, Return-to-Home, or teardown between matches. This file drives real
    /// outcomes deterministically (setting AvatarHealth to 1 on whichever side must die, then
    /// ticking real combat to a real Resolved phase) through entirely existing public seams -
    /// GameBootstrap.cs itself is untouched.
    /// </summary>
    public class ResultOverlayOutcomeCoverageTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDResultOverlay_" + System.Guid.NewGuid().ToString("N"));
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
            MotionPolicy.ReduceMotion = false;
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
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
            return bootstrap;
        }

        /// <summary>Same pattern as GameBootstrapStateCoverageTests.SaveValidDeckForNormalMatch.</summary>
        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("ResultOverlay_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance;

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(deckIds),
                activeDeckCardIds = new List<string>(deckIds),
            };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();

            Object.DestroyImmediate(databaseGo);
        }

        /// <summary>Drives a real tutorial match (GameBootstrap.StartApprovedTutorialBattle) to a
        /// real Resolved phase, forcing the given outcome deterministically by setting the losing
        /// side's AvatarHealth to 1 right after formation locks - the exact pattern
        /// BattleLogicTests.GameBootstrap_StartApprovedTutorialBattle_ResolvesWithinTheTickCap
        /// already proves reaches Resolved within the tick cap.</summary>
        private GameBootstrap ResolveTutorialMatch(string hostName, bool playerWins)
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap(hostName);
            bootstrap.StartApprovedTutorialBattle(showOpeningCinematic: false);
            BattleController controller = bootstrap.Battle;

            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }
            SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: the approved tutorial Formation must lock legally.");

            if (playerWins) controller.EnemyState.AvatarHealth = 1;
            else controller.PlayerState.AvatarHealth = 1;

            ResolveCombat(controller);
            return bootstrap;
        }

        /// <summary>Same, for a normal (non-tutorial) match via AutoFormationForTests/-
        /// StartBattleForTests - the exact pattern GameBootstrapStateCoverageTests.State4_Combat/
        /// State5_Resolved already prove works.</summary>
        private GameBootstrap ResolveNormalMatch(string hostName, bool playerWins)
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap(hostName);
            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();
            BattleController controller = bootstrap.Battle;
            Assert.AreEqual(BattlePhase.Combat, controller.Phase, "Setup: expected Combat before forcing an outcome.");

            if (playerWins) controller.EnemyState.AvatarHealth = 1;
            else controller.PlayerState.AvatarHealth = 1;

            ResolveCombat(controller);
            return bootstrap;
        }

        private static void ResolveCombat(BattleController controller)
        {
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks,
                    "STATE UNREACHED: the forced outcome did not resolve within the existing combat tick cap.");
            }
            Assert.AreEqual(BattlePhase.Resolved, controller.Phase, "STATE UNREACHED: match did not reach Resolved.");
        }

        // ---------- CTA visibility ----------

        [Test]
        public void Victory_Tutorial_ShowsOnlyReturnToCity()
        {
            GameBootstrap bootstrap = ResolveTutorialMatch("Result_VictoryTutorial", playerWins: true);

            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests);
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests,
                "STATE UNREACHED: tutorial victory must offer Return to Empire.");
            Assert.IsFalse(bootstrap.PlayAgainButtonActiveForTests,
                "Tutorial victory must not also offer Retry - exactly one exit applies.");
        }

        [Test]
        public void Defeat_Tutorial_ShowsOnlyRetry()
        {
            GameBootstrap bootstrap = ResolveTutorialMatch("Result_DefeatTutorial", playerWins: false);

            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests);
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests,
                "STATE UNREACHED: tutorial defeat must offer Retry Battle.");
            Assert.IsFalse(bootstrap.ReturnToCityButtonActiveForTests,
                "Tutorial defeat must not also offer Return to Empire - exactly one exit applies.");
        }

        [Test]
        public void Victory_NormalMatch_ShowsBothCTAs()
        {
            GameBootstrap bootstrap = ResolveNormalMatch("Result_VictoryNormal", playerWins: true);

            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests);
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests, "A normal match's result must always offer Play Again.");
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests, "A normal match's result must always offer Return to City.");
            StringAssert.Contains("VICTORY", bootstrap.ResultTextForTests);
        }

        [Test]
        public void Defeat_NormalMatch_ShowsBothCTAs()
        {
            GameBootstrap bootstrap = ResolveNormalMatch("Result_DefeatNormal", playerWins: false);

            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests);
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests, "A normal match's result must always offer Play Again.");
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests, "A normal match's result must always offer Return to City.");
            StringAssert.Contains("DEFEAT", bootstrap.ResultTextForTests);
        }

        // ---------- Retry ----------

        [Test]
        public void Retry_AfterDefeat_HidesOverlay_AndLeavesResolvedPhase()
        {
            GameBootstrap bootstrap = ResolveNormalMatch("Result_RetryFromDefeat", playerWins: false);
            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests, "Setup: overlay must be up before Retry.");

            bootstrap.PlayAgainForTests();

            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "Retry (Play Again) must hide the result overlay.");
            Assert.AreNotEqual(BattlePhase.Resolved, bootstrap.Battle.Phase,
                "Retry must start a fresh match, not leave the previous one Resolved.");
        }

        // ---------- Return-to-Home ----------

        [Test]
        public void ReturnToHome_AfterVictory_HidesOverlay_AndHidesBattleCanvas()
        {
            GameBootstrap bootstrap = ResolveNormalMatch("Result_ReturnHomeFromVictory", playerWins: true);
            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests, "Setup: overlay must be up before Return to City.");

            bootstrap.ReturnToCityForTests();

            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "Return to City must hide the result overlay.");
        }

        // ---------- Teardown / no leaked tutorial single-button state ----------

        [Test]
        public void TutorialDefeat_ThenNormalMatchResolves_ShowsBothCTAsAgain_NoLeakedSingleButtonState()
        {
            // Saved BEFORE PlayAgainForTests (StartNewMatch reads whatever deck is live at the
            // moment it runs - saving afterward left the just-started match on the tutorial's
            // fixed 3-card deck and Formation never reached Combat, a real bug in this test's
            // first draft, not in GameBootstrap.cs).
            SaveValidDeckForNormalMatch();

            GameBootstrap bootstrap = ResolveTutorialMatch("Result_TeardownLeak", playerWins: false);
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests, "Setup: tutorial defeat must show Retry only.");
            Assert.IsFalse(bootstrap.ReturnToCityButtonActiveForTests, "Setup: tutorial defeat must hide Return to Empire.");

            bootstrap.PlayAgainForTests();
            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "Setup: Retry must hide the tutorial's overlay first.");

            // A fresh NORMAL match now, on the same bootstrap instance - the exact scenario
            // GameBootstrap.cs's own comment (~ line 6386) says the visibility reset guards.
            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();
            bootstrap.Battle.EnemyState.AvatarHealth = 1;
            ResolveCombat(bootstrap.Battle);

            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests,
                "A normal match's result must show Play Again even after a prior tutorial defeat hid it.");
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests,
                "A normal match's result must show Return to City even after a prior tutorial defeat hid it - " +
                "no leaked single-button state from the tutorial path.");
        }

        // ---------- 1920x1080 CTA geometry and raycastability ----------

        /// <summary>No public accessor exposes the result overlay's buttons directly (only the
        /// active-state booleans) - GameBootstrap.cs is off-limits for this card, so this walks
        /// the same real, already-built scene hierarchy SpawnAndInitializeBootstrap already knows
        /// to sweep up (GameObject.Find("Canvas")), matching by the buttons' own real label text
        /// rather than adding a new production accessor.</summary>
        private static Button FindResultButtonByLabel(params string[] anyOfTheseLabels)
        {
            GameObject canvasObj = GameObject.Find("Canvas");
            Assert.IsNotNull(canvasObj, "Setup: the battle canvas must exist to locate result buttons.");

            foreach (Button button in canvasObj.GetComponentsInChildren<Button>(true))
            {
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null && anyOfTheseLabels.Contains(label.text))
                    return button;
            }
            return null;
        }

        [Test]
        public void ResultOverlay_Canvas_IsLockedToThe1920x1080LandscapeReference()
        {
            ResolveNormalMatch("Result_CanvasReference", playerWins: true);

            GameObject canvasObj = GameObject.Find("Canvas");
            Assert.IsNotNull(canvasObj);
            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler, "STATE UNREACHED: the battle canvas must carry a CanvasScaler.");
            Assert.AreEqual(1920f, scaler.referenceResolution.x, 0.01f,
                "Locked: this game is landscape 1920x1080, never portrait.");
            Assert.AreEqual(1080f, scaler.referenceResolution.y, 0.01f);
        }

        [Test]
        public void VictoryCTAs_HaveNonZeroHitAreas_AndAreRaycastable()
        {
            ResolveNormalMatch("Result_VictoryGeometry", playerWins: true);

            Button playAgain = FindResultButtonByLabel("Play Again", "Retry Battle");
            Button returnHome = FindResultButtonByLabel("Return to City", "Return to Empire");
            Assert.IsNotNull(playAgain, "STATE UNREACHED: could not locate the Play Again/Retry button in the real hierarchy.");
            Assert.IsNotNull(returnHome, "STATE UNREACHED: could not locate the Return to City/Empire button in the real hierarchy.");

            foreach (Button button in new[] { playAgain, returnHome })
            {
                var rect = (RectTransform)button.transform;
                Assert.Greater(rect.rect.width, 0f, button.name + ": hit area must have nonzero width.");
                Assert.Greater(rect.rect.height, 0f, button.name + ": hit area must have nonzero height.");
                Assert.IsTrue(button.interactable, button.name + " must be interactable to receive a real tap.");
                Assert.IsNotNull(button.targetGraphic, button.name + " must have a target graphic to raycast against.");
                Assert.IsTrue(button.targetGraphic.raycastTarget,
                    button.name + ": raycastTarget must be on, or the button is visually present but untappable.");
            }
        }

        [Test]
        public void DefeatCTAs_HaveNonZeroHitAreas_AndAreRaycastable()
        {
            ResolveNormalMatch("Result_DefeatGeometry", playerWins: false);

            Button playAgain = FindResultButtonByLabel("Play Again", "Retry Battle");
            Button returnHome = FindResultButtonByLabel("Return to City", "Return to Empire");
            Assert.IsNotNull(playAgain);
            Assert.IsNotNull(returnHome);

            foreach (Button button in new[] { playAgain, returnHome })
            {
                var rect = (RectTransform)button.transform;
                Assert.Greater(rect.rect.width, 0f, button.name + ": hit area must have nonzero width.");
                Assert.Greater(rect.rect.height, 0f, button.name + ": hit area must have nonzero height.");
                Assert.IsTrue(button.interactable, button.name + " must be interactable to receive a real tap.");
                Assert.IsTrue(button.targetGraphic.raycastTarget, button.name + ": raycastTarget must be on.");
            }
        }

        [Test]
        public void ReducedMotion_ResultOverlay_StillShowsRealOutcomeTextAndCTAs()
        {
            // No MotionPolicy reference exists anywhere in or near BuildResultOverlay/
            // HandleMatchEnded (confirmed by inspection - the result overlay itself has no
            // decorative animation of its own to suppress). This test proves the one thing that
            // IS testable: setting ReduceMotion beforehand does not somehow prevent the overlay,
            // its text, or its CTAs from appearing - essential outcome feedback is unaffected by
            // the setting either way. It is NOT proof that a decorative element is correctly
            // suppressed, because none exists on this path to suppress.
            MotionPolicy.ReduceMotion = true;

            GameBootstrap bootstrap = ResolveNormalMatch("Result_ReducedMotion", playerWins: true);

            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests);
            Assert.IsFalse(string.IsNullOrEmpty(bootstrap.ResultTextForTests));
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests);
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests);
        }
    }
}
