using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Real playtest bug (2026-08-23): after the guided tutorial, the owner cleared Campaign
    /// stages 1-1 through 1-4, then got stuck at 1-5 - the 1-5 Formation screen was visible and
    /// readable underneath, but a modal reading "Victory. The first threat has been driven back."
    /// with only a "Return to Empire" button sat on top of it, blocking all input. That exact
    /// string is VictoryCinematicCopy/the tutorial-only branch of HandleMatchEnded - it must never
    /// show for a real Campaign stage. Traces the real click-through path (tutorial win -> Return
    /// to City -> Deck Builder confirm -> real stage launches via the real production
    /// LaunchCampaignStageForTests/AF-win/ReturnToCityForTests handlers, exactly what a player
    /// does) through several stages, checking after every single transition rather than only at
    /// the end, so a failure pinpoints exactly which hop leaks the stale state.
    /// </summary>
    public class TutorialResultOverlayLeakTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        private static readonly string[] ApprovedStarterCollectionCardIds =
        {
            "warrior", "novice_knight", "goblin_caster",
            "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
        };

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsResultOverlayLeak_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerBattleState.ClearShuffleSeedForTests();
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
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

        private HomePagePresenter SpawnAndBindHome(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenter_ResultOverlayLeak");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            home.BindGameBootstrapForTests(bootstrap);
            home.BindBattleControllerForTests(bootstrap.Battle);
            return home;
        }

        private static void CompleteApprovedTutorialVictory(GameBootstrap bootstrap)
        {
            BattleController controller = bootstrap.Battle;

            bootstrap.HandCardPressedForTests(controller.PlayerState.Hand.First(c => c.Id == "warrior"));
            bootstrap.LanePressedForTests(Lane.Front);
            bootstrap.HandCardPressedForTests(controller.PlayerState.Hand.First(c => c.Id == "novice_knight"));
            bootstrap.LanePressedForTests(Lane.Middle);
            bootstrap.HandCardPressedForTests(controller.PlayerState.Hand.First(c => c.Id == "goblin_caster"));
            bootstrap.LanePressedForTests(Lane.Back);

            bootstrap.StartBattleForTests();
            Assert.AreEqual(BattlePhase.Combat, controller.Phase);

            bootstrap.TutorialContinueForTests();
            bootstrap.SpellTappedForTests(0);
            bootstrap.SpellTargetLanePressedForTests(Lane.Middle);

            int continuesRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                bootstrap.TutorialContinueForTests();
                continuesRun++;
                Assert.LessOrEqual(continuesRun, BattleController.MaxCombatTicks);
            }

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase);
        }

        /// <summary>Resolves whatever match is currently on-screen via Auto Formation + tick loop.
        /// Outcome (win/lose) is not asserted here - unlockedStageIds is pre-seeded through 1-5 so
        /// stage-to-stage navigation does not depend on actually winning each one; this test is
        /// about overlay/flag state leaking across the launch/return cycle, not AF winnability.</summary>
        private static void ResolveCurrentMatchViaAutoFormation(GameBootstrap bootstrap)
        {
            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();

            int ticks = 0;
            while (bootstrap.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap.Battle.AdvanceCombatTick();
                ticks++;
                Assert.LessOrEqual(ticks, BattleController.MaxCombatTicks, "Must not softlock.");
            }

            Assert.AreEqual(BattlePhase.Resolved, bootstrap.Battle.Phase);
        }

        [Test]
        public void TutorialVictory_ThenFourRealStageLaunches_NeverLeaksTheTutorialOverlayIntoStage5()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ResultOverlayLeak_Bootstrap");
            HomePagePresenter home = SpawnAndBindHome(bootstrap);
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            // --- Tutorial: real guided victory, exactly as a fresh player experiences it ---
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            homeCanvas.SetActive(false);
            CompleteApprovedTutorialVictory(bootstrap);

            Assert.IsTrue(bootstrap.IsTutorialMatch, "Setup: the tutorial match itself must be flagged as tutorial.");
            Assert.AreEqual("Victory. The first threat has been driven back.", bootstrap.ResultTextForTests);
            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests, "Setup: the tutorial's own result overlay must be showing.");

            bootstrap.ReturnToCityForTests();
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Return to City must clear IsTutorialMatch (existing fix).");

            // --- Deck Builder: confirm a real starter deck, same CC-mandated detour as any fresh player ---
            homeCanvas.SetActive(true);
            CampaignStageData stage1_1 = CampaignMapPresenter.GetStageForTests("1-1");
            Assert.AreEqual(CampaignLaunchOutcome.BlockedNoDeck, home.LaunchCampaignStageForTests(stage1_1));
            DeckBuilderPresenter deckBuilder = home.GetComponent<DeckBuilderPresenter>();
            Assert.IsNotNull(deckBuilder);
            deckBuilder.SetAndConfirmDeckForTests(ApprovedStarterCollectionCardIds);
            homeCanvas.SetActive(true);

            // Pre-seed unlocked stages so each launch below is legal regardless of AF win/loss -
            // isolates the overlay/flag-leak mechanism from stage winnability, which is tracked
            // separately (Balance Soft, not this bug).
            PlayerProfile wallet = SaveManager.SaveData;
            wallet.unlockedStageIds = new List<string> { "1-1", "1-2", "1-3", "1-4", "1-5" };

            string[] stageSequence = { "1-1", "1-2", "1-3", "1-4" };
            foreach (string stageId in stageSequence)
            {
                CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
                Assert.IsNotNull(stage, $"Setup: expected stage {stageId} to be a real registered stage.");

                CampaignLaunchOutcome outcome = home.LaunchCampaignStageForTests(stage);
                Assert.AreEqual(CampaignLaunchOutcome.Launched, outcome, $"Stage {stageId} must launch with a confirmed deck.");

                // Check immediately after this stage's OWN Formation is built, before resolving
                // combat - this is the exact moment the owner's screenshot was taken for 1-5, so
                // check it for every hop, not just the last one.
                Assert.IsFalse(bootstrap.IsTutorialMatch, $"Stage {stageId}: IsTutorialMatch must be false on a real Campaign launch.");
                Assert.IsFalse(bootstrap.ResultOverlayActiveForTests,
                    $"Stage {stageId}: the result overlay must not still be showing over a freshly-launched Formation screen.");
                Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase);

                ResolveCurrentMatchViaAutoFormation(bootstrap);

                // The real match's own result screen is expected here - confirm it is the REAL
                // (non-tutorial) branch, not stale tutorial content.
                Assert.IsTrue(bootstrap.ResultOverlayActiveForTests, $"Stage {stageId}: a real result overlay should show after resolving.");
                StringAssert.DoesNotContain("Victory. The first threat has been driven back.", bootstrap.ResultTextForTests,
                    $"Stage {stageId}: must never show the tutorial-only victory copy for a real Campaign stage.");
                Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests, $"Stage {stageId}: Return to City must be available (real match, not tutorial single-button flow).");

                bootstrap.ReturnToCityForTests();
                homeCanvas.SetActive(true);
            }

            // --- The exact reported failure point: launching Stage 1-5 after four real stage cycles ---
            CampaignStageData stage1_5 = CampaignMapPresenter.GetStageForTests("1-5");
            Assert.IsNotNull(stage1_5, "Setup: expected 1-5 to be a real registered stage.");
            CampaignLaunchOutcome launch1_5 = home.LaunchCampaignStageForTests(stage1_5);
            Assert.AreEqual(CampaignLaunchOutcome.Launched, launch1_5, "Stage 1-5 must launch with a confirmed deck.");

            Assert.IsFalse(bootstrap.IsTutorialMatch, "Stage 1-5: IsTutorialMatch must be false.");
            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests,
                "Stage 1-5: the tutorial's stale result overlay must not be blocking the Formation screen - this is the exact reported bug.");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase,
                "Stage 1-5: the real Formation screen must be interactive, not blocked by a stuck modal.");
        }
    }
}
