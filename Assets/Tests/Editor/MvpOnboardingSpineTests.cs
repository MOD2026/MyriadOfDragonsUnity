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
    /// BLOCK K — Claude must-have #1: chained MVP onboarding spine (2026-08-23).
    ///
    /// One EditMode fixture from a truly fresh profile:
    /// Tutorial victory (real handlers + real HandleMatchCompleted reward guard) → Return Home →
    /// starter owned / no valid confirmed deck → Deck Builder confirm (intentional CC gate) →
    /// Launch Campaign 1-1 (real TryLaunchCampaignStage) → AF win → 1-2 unlock + first-clear reward.
    ///
    /// Reuses production seams from TutorialEncounterWinTests, TutorialStarterEntitlementPersistenceTests,
    /// Chapter1CampaignPlayabilityTests, FreshProfileChapterOneStageAccessTests, and
    /// ChapterOneProgressionTests. Does not invent battle math.
    /// </summary>
    public class MvpOnboardingSpineTests
    {
        private static readonly string[] ApprovedStarterCollectionCardIds =
        {
            "warrior", "novice_knight", "goblin_caster",
            "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
        };

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsMvpSpine_" + System.Guid.NewGuid().ToString("N"));
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

        private HomePagePresenter SpawnAndBindHome(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenter_MvpOnboardingSpine");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            home.BindGameBootstrapForTests(bootstrap);
            home.BindBattleControllerForTests(bootstrap.Battle);
            return home;
        }

        private static int CountUniqueOwned(PlayerProfile profile)
        {
            if (profile == null) return 0;
            if (profile.UsesCollectionV1 && profile.cardProgression != null)
            {
                return profile.cardProgression.Count(r => r != null && !string.IsNullOrEmpty(r.cardId) && r.copyCount > 0);
            }

            return profile.cardCollection == null
                ? 0
                : profile.cardCollection.Where(id => !string.IsNullOrEmpty(id)).Distinct().Count();
        }

        /// <summary>Guided tutorial victory — same production handlers as TutorialEncounterWinTests.</summary>
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
            Assert.AreEqual("Victory. The first threat has been driven back.", bootstrap.ResultTextForTests);
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests);
        }

        /// <summary>
        /// AF + Start Battle + tick resolve — same production policy as Chapter1CampaignPlayabilityTests.
        /// </summary>
        private static MatchResult WinCampaignStageWithAutoFormation(GameBootstrap bootstrap)
        {
            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();

            MatchResult? result = null;
            bootstrap.Battle.OnMatchCompleted += r => result = r;

            int ticksRun = 0;
            while (bootstrap.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap.Battle.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks,
                    "Stage 1-1 must resolve within the combat tick cap under AF.");
            }

            Assert.IsTrue(result.HasValue, "OnMatchCompleted must fire for the campaign match.");
            return result.Value;
        }

        [Test]
        public void FreshProfile_Tutorial_Home_DeckConfirm_LaunchAndWinCampaign1_1_Unlocks1_2()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("MvpSpine_Bootstrap");
            HomePagePresenter home = SpawnAndBindHome(bootstrap);
            GameObject homeCanvas = home.HomeCanvasObjectForTests;
            Assert.IsNotNull(homeCanvas);

            PlayerProfile wallet = SaveManager.SaveData;
            int goldBeforeTutorial = wallet.gold;
            int gemsBeforeTutorial = wallet.gems;
            int claimedBeforeTutorial = wallet.claimedStageRewardIds?.Count ?? 0;
            var unlocksBeforeTutorial = new List<string>(wallet.unlockedStageIds);

            // --- 1) Approved tutorial → win via real guided path ---
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            homeCanvas.SetActive(false);

            CompleteApprovedTutorialVictory(bootstrap);

            // --- 2) Real HandleMatchCompleted reward guard (tutorial: no economy) ---
            Assert.AreEqual(goldBeforeTutorial, wallet.gold,
                "Tutorial victory must leave gold unchanged (HandleMatchCompleted tutorial guard).");
            Assert.AreEqual(gemsBeforeTutorial, wallet.gems,
                "Tutorial victory must leave gems unchanged.");
            Assert.AreEqual(claimedBeforeTutorial, wallet.claimedStageRewardIds?.Count ?? 0,
                "Tutorial victory must never claim a campaign stage reward.");
            CollectionAssert.AreEquivalent(unlocksBeforeTutorial, wallet.unlockedStageIds,
                "Tutorial victory must not unlock campaign stages.");

            // --- 4) Starter collection present; no valid confirmed deck yet (CC: Deck Builder detour) ---
            Assert.GreaterOrEqual(CountUniqueOwned(bootstrap.Profile), 10,
                "After tutorial, starter entitlement must leave ≥10 unique owned cards.");
            foreach (string id in ApprovedStarterCollectionCardIds)
            {
                Assert.IsTrue(CollectionProgression.OwnsAnyCopy(bootstrap.Profile, id),
                    $"Starter id '{id}' must be owned after tutorial grant.");
            }
            Assert.IsFalse(bootstrap.HasValidConfirmedDeckForNormalBattle(),
                "Grant must not auto-confirm a valid saved deck — Deck Builder remains the intentional gate.");

            // --- 3) Return to Home / city as production does ---
            bootstrap.ReturnToCityForTests();
            Assert.IsTrue(homeCanvas.activeSelf, "Return to City must restore Home.");
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Return to City must hide battle canvas.");
            Assert.IsFalse(bootstrap.IsTutorialMatch,
                "Return to City must clear the tutorial session so the next Campaign entry can StartNewMatch.");

            // --- 5) Open Deck Builder via real launch gate (BlockedNoDeck), then confirm ---
            CampaignStageData stage1_1 = CampaignMapPresenter.GetStageForTests("1-1");
            Assert.IsNotNull(stage1_1);
            Assert.AreEqual(CampaignLaunchOutcome.BlockedNoDeck, home.LaunchCampaignStageForTests(stage1_1),
                "Pre-deck: TryLaunchCampaignStage must BlockedNoDeck and open Deck Builder (CC lock).");

            DeckBuilderPresenter deckBuilder = home.GetComponent<DeckBuilderPresenter>();
            Assert.IsNotNull(deckBuilder, "BlockedNoDeck must attach DeckBuilderPresenter.");
            deckBuilder.SetAndConfirmDeckForTests(ApprovedStarterCollectionCardIds);
            CollectionAssert.AreEqual(ApprovedStarterCollectionCardIds, bootstrap.Profile.activeDeckCardIds);
            Assert.IsTrue(bootstrap.HasValidConfirmedDeckForNormalBattle());

            if (homeCanvas != null) homeCanvas.SetActive(true);

            int goldBeforeCampaign = wallet.gold;
            int gemsBeforeCampaign = wallet.gems;

            // --- 6) Launch Campaign 1-1 via real TryLaunchCampaignStage ---
            PlayerBattleState.SetShuffleSeedForTests(11);
            Assert.AreEqual(CampaignLaunchOutcome.Launched, home.LaunchCampaignStageForTests(stage1_1),
                "With a confirmed starter deck, Campaign 1-1 must launch.");
            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests);
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Campaign 1-1 must not remain a tutorial match.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests);

            // --- 7) Win 1-1 (AF harness) → 1-2 unlock + first-clear reward ---
            MatchResult campaignResult = WinCampaignStageWithAutoFormation(bootstrap);
            Assert.IsTrue(campaignResult.IsVictory,
                "Stage 1-1 must be winnable with starter deck + Auto Formation (Chapter1CampaignPlayability policy).");

            CollectionAssert.AreEquivalent(new[] { "1-1", "1-2" }, wallet.unlockedStageIds,
                "Winning 1-1 must unlock exactly 1-2 (FreshProfileChapterOneStageAccess / HandleMatchCompleted).");
            Assert.AreEqual(goldBeforeCampaign + stage1_1.goldReward, wallet.gold,
                "First clear of 1-1 must grant the stage's configured gold reward.");
            Assert.AreEqual(gemsBeforeCampaign + stage1_1.gemReward, wallet.gems,
                "First clear of 1-1 must grant the stage's configured gem reward.");
            CollectionAssert.Contains(wallet.claimedStageRewardIds, "1-1",
                "First clear must record claimedStageRewardIds for 1-1.");
        }
    }
}
