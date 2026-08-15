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
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// TUTORIAL GUIDANCE, 2026-08-15 - proves the approved tutorial-only copy/single-button
    /// flow through the REAL private paths (RefreshPhaseControls, RefreshLanePicker,
    /// HandleMatchEnded, OnPlayAgainOrRetryPressed), exactly as production does, and that none
    /// of it leaks into or alters a normal match.
    ///
    /// Two separate profiles, same as the other Battle-adjacent test files in this suite -
    /// GameBootstrap's own `bootstrap.Profile` versus `SaveManager.SaveData` (HomePagePresenter's
    /// reward guard's own profile). See HomePageTutorialRewardGuardTests.cs's own doc comment
    /// for why.
    /// </summary>
    public class TutorialGuidanceTests
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
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
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
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }
            return bootstrap;
        }

        private static void RunToResolutionWithUndefendedEnemy(BattleController controller)
        {
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a knockout well inside the tick cap.");
            }
        }

        /// <summary>Same deterministic-defeat setup as HomePageTutorialRewardGuardTests'
        /// TutorialDefeat test - player deploys only its weakest card into Back; enemy deploys
        /// its whole hand into Front/Middle.</summary>
        private static void RunToResolutionWithUndefendedPlayer(BattleController controller)
        {
            Card weakestPlayerCard = controller.PlayerState.Hand.OrderBy(c => c.Attack).First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, weakestPlayerCard, Lane.Back),
                "Setup: expected the player's one deployed card to legally occupy Back.");

            foreach (Card enemyCard in controller.EnemyState.Hand.ToList())
            {
                Lane lane = controller.EnemyState.Lanes[Lane.Front].Cards.Count < LaneState.MaxSlots ? Lane.Front : Lane.Middle;
                controller.TryPlayCard(controller.EnemyState, enemyCard, lane);
            }
            int enemyDeployedCount = controller.EnemyState.Lanes[Lane.Front].Cards.Count + controller.EnemyState.Lanes[Lane.Middle].Cards.Count;
            Assert.Greater(enemyDeployedCount, 0, "Setup: expected at least one enemy card to deploy into Front/Middle.");

            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a knockout well inside the tick cap.");
            }
        }

        private static HashSet<string> DealtDeckIds(PlayerBattleState side)
        {
            return new HashSet<string>(side.Hand.Concat(side.DrawPile).Select(c => c.Id));
        }

        [Test]
        public void NormalMatch_ResultOverlay_LabelsAndBothButtonsUnchanged()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_NormalVictoryBootstrap");
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a freshly-initialized match to not be tutorial-flagged.");
            BattleController controller = bootstrap.Battle;

            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front));
            RunToResolutionWithUndefendedEnemy(controller);

            StringAssert.Contains("VICTORY", bootstrap.ResultTextForTests,
                "A normal victory's existing result text must be unaffected by tutorial guidance.");
            Assert.AreEqual("Play Again", bootstrap.PlayAgainLabelForTests);
            Assert.AreEqual("Return to City", bootstrap.ReturnToCityLabelForTests);
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests, "A normal match must always show both result buttons.");
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests, "A normal match must always show both result buttons.");
        }

        [Test]
        public void NormalMatch_NoTutorialGuidanceCaptionOrLaneGuidanceAppears()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_NormalNoLeakBootstrap");

            Assert.IsFalse(bootstrap.TutorialGuidanceCaptionActiveForTests,
                "A normal match's Formation phase must never show the tutorial Ready/Combat caption.");

            string title = bootstrap.OpenLanePickerAndGetTitleForTests(Lane.Front);
            StringAssert.DoesNotContain("Place cards in the Front row", title,
                "A normal match's lane picker must never show tutorial lane guidance.");
        }

        [Test]
        public void TutorialFormation_ShowsReadyCaptionAndPerLaneGuidance()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_TutorialFormationBootstrap");
            bootstrap.StartApprovedTutorialBattle();

            Assert.IsTrue(bootstrap.TutorialGuidanceCaptionActiveForTests);
            Assert.AreEqual("Ready: Review your formation, then begin the battle.", bootstrap.TutorialGuidanceCaptionTextForTests);

            StringAssert.Contains("Front: Place cards in the Front row.", bootstrap.OpenLanePickerAndGetTitleForTests(Lane.Front));
            StringAssert.Contains("Middle: Place cards in the Middle row.", bootstrap.OpenLanePickerAndGetTitleForTests(Lane.Middle));
            StringAssert.Contains("Back: Place cards in the Back row.", bootstrap.OpenLanePickerAndGetTitleForTests(Lane.Back));
        }

        [Test]
        public void TutorialFormation_WithOneCardDeployed_StaysInFormationAndDoesNotDeployEnemy()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_TutorialIncompleteBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;

            Card onlyCard = controller.PlayerState.Hand.First(c => c.Id == "warrior");
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, onlyCard, Lane.Front),
                "Setup: expected warrior to legally deploy into Front.");

            bootstrap.StartBattleForTests();

            Assert.AreEqual(BattlePhase.Formation, controller.Phase,
                "Start Battle must not enter Combat until all three tutorial starter cards are deployed.");
            Assert.AreEqual(0, controller.EnemyState.Lanes.Values.Sum(l => l.Cards.Count),
                "The enemy must not deploy while the tutorial formation is still incomplete.");
        }

        [Test]
        public void TutorialFormation_WithAllThreeDeployed_EntersCombat()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_TutorialCompleteBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;

            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }

            bootstrap.StartBattleForTests();

            Assert.AreEqual(BattlePhase.Combat, controller.Phase,
                "Start Battle must enter Combat once all three tutorial starter cards are deployed.");
            Assert.Greater(controller.EnemyState.Lanes.Values.Sum(l => l.Cards.Count), 0,
                "The enemy must deploy once Start Battle actually proceeds.");
        }

        [Test]
        public void NormalMatch_WithOneCardDeployed_StillEntersCombat()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_NormalOneCardStartsBattleBootstrap");
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a freshly-initialized match to not be tutorial-flagged.");
            BattleController controller = bootstrap.Battle;

            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front),
                "Setup: expected to be able to play at least one card into Front.");

            bootstrap.StartBattleForTests();

            Assert.AreEqual(BattlePhase.Combat, controller.Phase,
                "A normal match must still be able to start battle with only one card deployed - the tutorial gate must not apply here.");
        }

        [Test]
        public void NormalFormation_DirectHandCardSelectionThenLaneTap_DeploysTheCard()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_DirectSelectDeploysBootstrap");
            BattleController controller = bootstrap.Battle;
            Card card = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);

            bootstrap.HandCardPressedForTests(card);
            Assert.AreEqual(card.Id, bootstrap.SelectedCardIdForTests,
                "Tapping an affordable hand card during Formation must select it directly.");

            bootstrap.LanePressedForTests(Lane.Front);

            Assert.IsNull(bootstrap.SelectedCardIdForTests, "Deploying the selected card must clear the selection.");
            Assert.IsTrue(controller.PlayerState.Lanes[Lane.Front].Cards.Any(c => c.Definition.Id == card.Id),
                "A lane tap with a card selected must deploy that exact card into the tapped lane.");
        }

        [Test]
        public void FormationLaneFirstFlow_StillOpensThePickerAndCanDeployACard()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_LaneFirstFlowBootstrap");
            BattleController controller = bootstrap.Battle;

            bootstrap.LanePressedForTests(Lane.Front);

            Assert.IsTrue(bootstrap.IsLanePickerOpenForTests,
                "A lane tap with nothing selected must still open the lane-first picker flow, unchanged.");
            Assert.IsNull(bootstrap.SelectedCardIdForTests, "Opening the picker must not itself select a card.");

            // The picker's own available-row tap deploys through exactly this call
            // (RefreshLanePicker's inline onClick handler, untouched by this fix) - proving it
            // still succeeds proves the lane-first flow's underlying deploy path remains intact.
            Card card = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, card, Lane.Front));
            Assert.IsTrue(controller.PlayerState.Lanes[Lane.Front].Cards.Any(c => c.Definition.Id == card.Id));
        }

        [Test]
        public void TutorialFormation_DirectSelectionForAllThreeStarters_PassesTheStartBattleGate()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_TutorialDirectSelectAllThreeBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;

            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                bootstrap.HandCardPressedForTests(card);
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                bootstrap.LanePressedForTests(lane);
            }

            bootstrap.StartBattleForTests();

            Assert.AreEqual(BattlePhase.Combat, controller.Phase,
                "Direct hand-card selection must deploy all three tutorial starters and pass the existing Start Battle gate.");
        }

        [Test]
        public void TutorialFormation_HidesResetAndRecommendedLineupButtons()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_TutorialHidesLineupBootstrap");
            bootstrap.StartApprovedTutorialBattle();

            Assert.IsFalse(bootstrap.ResetLineupButtonActiveForTests,
                "A tutorial match must hide Reset Lineup - it is the one remaining unguarded way to silently leave the tutorial.");
            Assert.IsFalse(bootstrap.RecommendedLineupButtonActiveForTests,
                "A tutorial match must hide Recommended Lineup for the same reason.");
        }

        [Test]
        public void NormalMatchFormation_ShowsResetAndRecommendedLineupButtons()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_NormalShowsLineupBootstrap");
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a freshly-initialized match to not be tutorial-flagged.");

            Assert.IsTrue(bootstrap.ResetLineupButtonActiveForTests,
                "A normal match must keep showing Reset Lineup exactly as before this fix.");
            Assert.IsTrue(bootstrap.RecommendedLineupButtonActiveForTests,
                "A normal match must keep showing Recommended Lineup exactly as before this fix.");
        }

        [Test]
        public void NormalMatch_ResetAndRecommendedHooksStillFunction_AfterTheVisibilityChange()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_NormalLineupHooksStillWorkBootstrap");

            Assert.DoesNotThrow(() => bootstrap.ResetLineupForTests());
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Reset Lineup on a normal match must remain a normal match.");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Reset Lineup must still start a fresh Formation-phase match.");
            Assert.IsTrue(bootstrap.ResetLineupButtonActiveForTests, "The buttons must still be visible for the new normal match.");

            Assert.DoesNotThrow(() => bootstrap.UseRecommendedLineupForTests());
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Recommended Lineup on a normal match must remain a normal match.");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Recommended Lineup must still start a fresh Formation-phase match.");
            Assert.IsTrue(bootstrap.RecommendedLineupButtonActiveForTests, "The buttons must still be visible for the new normal match.");
        }

        [Test]
        public void TutorialCombat_ShowsCombatObjectiveCaption()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_TutorialCombatBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;

            // Deploy all three tutorial cards across distinct lanes (not just one, undefended) -
            // EndTurnForTests advances one real combat tick below, and a well-defended formation
            // is far less likely to resolve the whole match in that single tick than a
            // one-card-only board would.
            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }

            // EndTurnForTests is the real production path (SimpleAIOpponent formation +
            // ConfirmFormation + RefreshAll), not a direct BattleController.ConfirmFormation()
            // call. RefreshPhaseControls (which owns the tutorial caption) only ever runs
            // through RefreshAll() - exactly what OnPrimaryActionPressed does for the real
            // "Start Battle" button - so calling ConfirmFormation() directly bypasses the
            // refresh entirely, which is what left the caption stuck on "Ready" before this fix.
            bootstrap.EndTurnForTests();

            Assert.AreEqual(BattlePhase.Combat, controller.Phase,
                "Setup: expected a fully-defended tutorial formation to still be in Combat after one tick.");
            Assert.IsTrue(bootstrap.TutorialGuidanceCaptionActiveForTests);
            Assert.AreEqual("Hold your formation and overcome the enemy.", bootstrap.TutorialGuidanceCaptionTextForTests);
        }

        [Test]
        public void TutorialVictory_ShowsApprovedCopyAndOnlyReturnToEmpireButton()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_TutorialVictoryBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;

            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }
            RunToResolutionWithUndefendedEnemy(controller);

            Assert.AreEqual("Victory. The first threat has been driven back.", bootstrap.ResultTextForTests);
            Assert.AreEqual("Return to Empire", bootstrap.ReturnToCityLabelForTests);
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests, "Tutorial victory must show Return to Empire.");
            Assert.IsFalse(bootstrap.PlayAgainButtonActiveForTests, "Tutorial victory must not show Retry Battle.");
        }

        [Test]
        public void TutorialDefeat_ShowsApprovedCopyAndOnlyRetryBattleButton()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_TutorialDefeatBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;

            RunToResolutionWithUndefendedPlayer(controller);

            Assert.AreEqual("Defeat. Adjust your formation and try again.", bootstrap.ResultTextForTests);
            Assert.AreEqual("Retry Battle", bootstrap.PlayAgainLabelForTests);
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests, "Tutorial defeat must show Retry Battle.");
            Assert.IsFalse(bootstrap.ReturnToCityButtonActiveForTests, "Tutorial defeat must not show Return to Empire.");
        }

        [Test]
        public void Retry_ActuallyCallsStartApprovedTutorialBattle_NotStartNewMatch()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_TutorialRetryRosterBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controllerBeforeRetry = bootstrap.Battle;
            RunToResolutionWithUndefendedPlayer(controllerBeforeRetry);
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests, "Setup: expected Retry Battle to be the active button after a tutorial defeat.");

            bootstrap.RetryForTests();

            Assert.IsTrue(bootstrap.IsTutorialMatch, "Retry after a tutorial defeat must remain a tutorial match.");
            CollectionAssert.AreEquivalent(new[] { "warrior", "novice_knight", "goblin_caster" },
                DealtDeckIds(bootstrap.Battle.PlayerState),
                "Retry must restart the approved tutorial roster, not a normal StartNewMatch deck.");
        }

        [Test]
        public void TutorialGuidanceFlow_ChangesNoRewardOrProgressionField()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guidance_TutorialNoSideEffectBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;

            PlayerProfile rewardProfile = SaveManager.SaveData;
            int goldBefore = rewardProfile.gold;
            int gemsBefore = rewardProfile.gems;
            int stageCountBefore = rewardProfile.unlockedStageIds.Count;

            PlayerProfile progressionProfile = bootstrap.Profile;
            int avatarLevelBefore = progressionProfile.avatarLevel;
            int totalMatchesBefore = progressionProfile.totalMatches;
            int totalWinsBefore = progressionProfile.totalWins;
            int winStreakBefore = progressionProfile.winStreak;

            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }
            RunToResolutionWithUndefendedEnemy(controller);
            bootstrap.OpenLanePickerAndGetTitleForTests(Lane.Front); // exercises the guidance-append path too

            Assert.AreEqual(goldBefore, rewardProfile.gold);
            Assert.AreEqual(gemsBefore, rewardProfile.gems);
            Assert.AreEqual(stageCountBefore, rewardProfile.unlockedStageIds.Count);
            Assert.AreEqual(avatarLevelBefore, progressionProfile.avatarLevel);
            Assert.AreEqual(totalMatchesBefore, progressionProfile.totalMatches);
            Assert.AreEqual(totalWinsBefore, progressionProfile.totalWins);
            Assert.AreEqual(winStreakBefore, progressionProfile.winStreak);
        }

        [Test]
        public void HomeBanner_ShowsApprovedCopy()
        {
            var go = new GameObject("HomePagePresenterUnderTest");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BuildHomePageUIForTests();

            GameObject homeCanvas = presenter.HomeCanvasObjectForTests;
            Assert.IsNotNull(homeCanvas, "Setup: expected Home's canvas to exist.");

            Transform banner = homeCanvas.transform.Find("TutorialGuidanceBanner");
            Assert.IsNotNull(banner, "Home must contain the tutorial guidance banner.");

            Text bannerText = banner.GetComponent<Text>();
            Assert.IsNotNull(bannerText);
            Assert.AreEqual("The Empire stands wounded. Learn to form your ranks and face the first threat.", bannerText.text);
        }
    }
}
