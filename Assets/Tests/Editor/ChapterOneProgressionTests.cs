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
    /// CHAPTER 1 PROGRESSION VERTICAL SLICE (release feature) - proves the whole contract
    /// through the real production owner, HomePagePresenter.HandleMatchCompleted, subscribed to
    /// a real BattleController exactly as Start() does (see BindBattleControllerForTests' own
    /// comment - Start() never fires in EditMode). SetActiveStageForTests reproduces exactly
    /// what OpenStoryCampaign's onLaunchBattle callback does when a real Story stage tile is
    /// tapped; it does not reimplement or bypass HandleMatchCompleted itself.
    ///
    /// All reward/progression fields (gold, gems, unlockedStageIds, claimedStageRewardIds) live
    /// on SaveManager.SaveData (a facade over SaveSystem.CurrentProfile) - the same profile
    /// instance HomePageTutorialRewardGuardTests already established as the one HandleMatchCompleted
    /// reads/writes, kept isolated per-test via SaveSystem.OverrideRootDirectoryForTests.
    /// </summary>
    public class ChapterOneProgressionTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsChapter1_" + System.Guid.NewGuid().ToString("N"));
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

        /// <summary>A normal (non-tutorial) match requires a confirmed valid saved deck to deal
        /// any hand at all (release repair: TryBuildSavedPlayerDeck blocks instead of falling
        /// back to a generated deck) - every stage-victory/defeat scenario in this file needs a
        /// real hand to play from, so this satisfies that prerequisite the same way
        /// NormalBattleSavedDeckIntegrationTests/DeckPersistenceTests already do, before
        /// spawning the bootstrap that will read it.</summary>
        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("Chapter1_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards.Select(c => c.Id)
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

            Object.DestroyImmediate(databaseGo);
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

        private HomePagePresenter SpawnHomePagePresenter(BattleController boundController)
        {
            var go = new GameObject("HomePagePresenterUnderTest");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(boundController);
            return presenter;
        }

        /// <summary>Same deterministic-win setup HomePageTutorialRewardGuardTests already relies
        /// on: leaves every enemy lane empty so the player's Front-lane Attack overflows to the
        /// enemy Avatar every tick, guaranteeing a fast, deterministic knockout inside the tick
        /// cap.</summary>
        private static void PlayOneCardAndWin(BattleController controller)
        {
            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front),
                "Setup: expected to be able to play at least one card into Front.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");

            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a knockout well inside the tick cap.");
            }
        }

        /// <summary>Mirror of PlayOneCardAndWin, roles reversed - the player deploys only its
        /// weakest card into Back, leaving Front/Middle undefended, while the enemy deploys its
        /// whole hand into Front/Middle, guaranteeing a player defeat inside the tick cap. Same
        /// technique HomePageTutorialRewardGuardTests' TutorialDefeat test already uses.</summary>
        private static void DeployWeaklyAndLose(BattleController controller)
        {
            Card weakestPlayerCard = controller.PlayerState.Hand.OrderBy(c => c.Attack).First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, weakestPlayerCard, Lane.Back),
                "Setup: expected the player's one deployed card to legally occupy Back.");

            foreach (Card enemyCard in controller.EnemyState.Hand.ToList())
            {
                Lane lane = controller.EnemyState.Lanes[Lane.Front].Cards.Count < LaneState.MaxSlots ? Lane.Front : Lane.Middle;
                controller.TryPlayCard(controller.EnemyState, enemyCard, lane);
            }
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");

            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a defeat well inside the tick cap.");
            }
        }

        /// <summary>The real, configured stage data from the production campaign list (the sole
        /// order authority) - not a hand-typed stand-in that could drift out of sync with the
        /// actual reward values.</summary>
        private static CampaignStageData FindStage(string stageId)
        {
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
            Assert.IsNotNull(stage, $"Setup: expected Stage {stageId} to exist in the production campaign list.");
            return stage;
        }

        [Test]
        public void FirstClear_GrantsConfiguredRewardExactlyOnce_AndClaimsStageId()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter1_FirstClearBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            presenter.SetActiveStageForTests(FindStage("1-1"));

            PlayerProfile profile = SaveManager.SaveData;
            int goldBefore = profile.gold;
            int gemsBefore = profile.gems;

            bool? isVictory = null;
            controller.OnMatchCompleted += result => isVictory = result.IsVictory;
            PlayOneCardAndWin(controller);

            Assert.IsTrue(isVictory, "Setup: expected the undefended enemy to produce a player victory.");
            Assert.AreEqual(goldBefore + 200, profile.gold, "A first clear must grant exactly the stage's configured gold reward.");
            Assert.AreEqual(gemsBefore + CampaignGemRewardRules.RegularStageGems, profile.gems,
                "A first clear must grant exactly the stage's configured gem reward (locked regular-stage grant).");
            CollectionAssert.Contains(profile.claimedStageRewardIds, "1-1", "The stage id must be recorded as claimed after its first clear.");
            CollectionAssert.Contains(profile.unlockedStageIds, "1-1", "The cleared stage itself must remain (or become) unlocked.");
        }

        [Test]
        public void ReplayingAClearedStage_GrantsNoDuplicateReward()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter1_ReplayBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            presenter.SetActiveStageForTests(FindStage("1-1"));

            PlayOneCardAndWin(controller);
            PlayerProfile profile = SaveManager.SaveData;
            int goldAfterFirstClear = profile.gold;
            int gemsAfterFirstClear = profile.gems;
            int claimedCountAfterFirstClear = profile.claimedStageRewardIds.Count;

            // Replay: "Play Again" restarts a fresh match without ever leaving Home/clearing
            // currentActiveStage - the same stage id is still active for the new match.
            bootstrap.PlayAgainForTests();
            controller = bootstrap.Battle;
            PlayOneCardAndWin(controller);

            Assert.AreEqual(goldAfterFirstClear, profile.gold, "Replaying an already-cleared stage must never grant duplicate gold.");
            Assert.AreEqual(gemsAfterFirstClear, profile.gems, "Replaying an already-cleared stage must never grant duplicate gems.");
            Assert.AreEqual(claimedCountAfterFirstClear, profile.claimedStageRewardIds.Count,
                "Replaying an already-cleared stage must not add a second claimed-reward entry.");
        }

        /// <summary>
        /// PROGRESSION-REPAIR REGRESSION, 2026-08-21 - owner report: won Campaign 1-2, returned
        /// to City, reopened Story, and Stage 1-3 still showed LOCKED. Traced to
        /// HandleMatchCompleted's already-claimed early-return: the next-stage unlock used to live
        /// only inside the never-replayed first-clear branch, so any save where
        /// claimedStageRewardIds already contained "1-2" without unlockedStageIds also containing
        /// "1-3" (e.g. an earlier session, before unlockedStageIds' current "1-1"-only fresh-
        /// profile default existed - see PlayerProfile.unlockedStageIds' own comment) could win
        /// this same stage forever afterward and never repair the missing next-stage unlock, since
        /// every subsequent victory took the early-return path and skipped the unlock check
        /// entirely. This reproduces that exact drifted-save shape directly (rather than depending
        /// on which historical code path could have produced it) and proves the fix: the next-
        /// stage unlock is now computed unconditionally on every victory of this stage, so a win
        /// against an already-claimed stage still repairs it, exactly the owner's real path
        /// (Launch -&gt; Win -&gt; Return to City -&gt; reopen Story).
        /// </summary>
        [Test]
        public void ReplayingAnAlreadyClaimedStage_RepairsAMissingNextStageUnlock()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter1_RepairBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            presenter.BindGameBootstrapForTests(bootstrap);
            presenter.SetActiveStageForTests(FindStage("1-2"));

            // Simulate the owner's real, already-drifted save: Stage 1-2 was already claimed
            // through some earlier path, but Stage 1-3 was never actually unlocked.
            PlayerProfile profile = SaveManager.SaveData;
            profile.claimedStageRewardIds.Add("1-2");
            Assert.IsFalse(profile.unlockedStageIds.Contains("1-3"),
                "Setup: expected Stage 1-3 to start locked despite Stage 1-2 already being claimed.");
            int goldBefore = profile.gold;
            int gemsBefore = profile.gems;

            PlayOneCardAndWin(controller);

            Assert.AreEqual(goldBefore, profile.gold, "A win against an already-claimed stage must still grant no duplicate gold.");
            Assert.AreEqual(gemsBefore, profile.gems, "A win against an already-claimed stage must still grant no duplicate gems.");
            CollectionAssert.Contains(profile.unlockedStageIds, "1-3",
                "A win against an already-claimed stage must repair a missing next-stage unlock, not skip it forever.");

            // Owner's exact reported path: Return to City, then reopen Story.
            bootstrap.ReturnToCityForTests();
            var campaignGo = new GameObject("RepairReopenedCampaignMap");
            _spawned.Add(campaignGo);
            var campaign = campaignGo.AddComponent<CampaignMapPresenter>();
            Assert.IsTrue(campaign.IsStageUnlockedForTests("1-3"),
                "Reopening Story after the repair must show Stage 1-3 as unlocked - the owner's exact reported symptom.");

            // A fresh disk reload proves the repair was actually saved, not only held in memory.
            PlayerProfile reloaded = SaveSystem.Load();
            CollectionAssert.Contains(reloaded.unlockedStageIds, "1-3", "The repaired next-stage unlock must survive a fresh reload.");
        }

        [Test]
        public void StageDefeat_GrantsNothingAndUnlocksNothing()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter1_DefeatBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            presenter.SetActiveStageForTests(FindStage("1-2"));

            PlayerProfile profile = SaveManager.SaveData;
            int goldBefore = profile.gold;
            int gemsBefore = profile.gems;
            int unlockedCountBefore = profile.unlockedStageIds.Count;
            int claimedCountBefore = profile.claimedStageRewardIds.Count;

            bool? isVictory = null;
            controller.OnMatchCompleted += result => isVictory = result.IsVictory;
            DeployWeaklyAndLose(controller);

            Assert.IsFalse(isVictory, "Setup: expected the undefended player lanes to produce a defeat.");
            Assert.AreEqual(goldBefore, profile.gold, "A stage defeat must grant no gold.");
            Assert.AreEqual(gemsBefore, profile.gems, "A stage defeat must grant no gems.");
            Assert.AreEqual(unlockedCountBefore, profile.unlockedStageIds.Count, "A stage defeat must unlock nothing.");
            Assert.AreEqual(claimedCountBefore, profile.claimedStageRewardIds.Count, "A stage defeat must claim nothing.");
        }

        [Test]
        public void Victory_UnlocksImmediatelyNextStage_UsingTheExistingOrderedList()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter1_NextStageBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);

            // 1-2 is unlocked by default (PlayerProfile's own starting set) but 1-3 is not -
            // clearing 1-2 is what must newly unlock 1-3, proving real forward progression
            // rather than a no-op against an already-unlocked default.
            PlayerProfile profile = SaveManager.SaveData;
            Assert.IsFalse(profile.unlockedStageIds.Contains("1-3"), "Setup: expected Stage 1-3 to start locked.");

            presenter.SetActiveStageForTests(FindStage("1-2"));
            PlayOneCardAndWin(controller);

            CollectionAssert.Contains(profile.unlockedStageIds, "1-3",
                "Clearing Stage 1-2 must unlock Stage 1-3, the immediately next stage in the existing ordered campaign list.");
        }

        [Test]
        public void RewardAndUnlockState_PersistsAcrossASaveReload()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter1_PersistenceBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            presenter.SetActiveStageForTests(FindStage("1-2"));

            PlayOneCardAndWin(controller);
            PlayerProfile beforeReload = SaveManager.SaveData;
            int goldAfterClear = beforeReload.gold;
            int gemsAfterClear = beforeReload.gems;

            // A fresh load from disk, bypassing the in-memory CurrentProfile entirely - proves
            // SaveManager.Save() (called synchronously inside HandleMatchCompleted, not deferred)
            // actually persisted the mutation, not just the in-memory instance.
            PlayerProfile reloaded = SaveSystem.Load();

            Assert.AreEqual(goldAfterClear, reloaded.gold, "Gold from the first-clear reward must survive a fresh reload.");
            Assert.AreEqual(gemsAfterClear, reloaded.gems, "Gems from the first-clear reward must survive a fresh reload.");
            CollectionAssert.Contains(reloaded.claimedStageRewardIds, "1-2", "The claimed-reward record must survive a fresh reload.");
            CollectionAssert.Contains(reloaded.unlockedStageIds, "1-3", "The next-stage unlock must survive a fresh reload.");
        }

        [Test]
        public void TutorialVictory_DoesNotClaimOrUnlockAnyCampaignStage()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter1_TutorialExclusionBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            // Even with an active campaign stage set (simulating a player who had one queued up
            // before a tutorial retry), the tutorial guard must still short-circuit first.
            presenter.SetActiveStageForTests(FindStage("1-1"));

            PlayerProfile profile = SaveManager.SaveData;
            int claimedCountBefore = profile.claimedStageRewardIds.Count;
            int unlockedCountBefore = profile.unlockedStageIds.Count;
            int goldBefore = profile.gold;

            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the tutorial Formation to lock legally.");
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a tutorial knockout inside the tick cap.");
            }

            Assert.AreEqual(claimedCountBefore, profile.claimedStageRewardIds.Count, "A tutorial victory must never claim a campaign stage reward.");
            Assert.AreEqual(unlockedCountBefore, profile.unlockedStageIds.Count, "A tutorial victory must never unlock a campaign stage.");
            Assert.AreEqual(goldBefore, profile.gold, "A tutorial victory must never grant gold.");
        }

        [Test]
        public void ReturnToHome_ThenReopenStory_VisiblyRetainsUnlockedStageState()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter1_ReopenStoryBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(controller);
            presenter.BindGameBootstrapForTests(bootstrap);
            presenter.SetActiveStageForTests(FindStage("1-2"));

            PlayOneCardAndWin(controller);

            // Home -> ... -> Home: the real return-to-city handoff, through the real handler.
            bootstrap.ReturnToCityForTests();
            Assert.IsNull(presenter.ActiveStageForTests, "Returning to Home must clear the stale active-stage reference.");

            // Reopen Story: a fresh CampaignMapPresenter, exactly as OpenStoryCampaign creates
            // one each time - its own RefreshStageUnlockStatus (via IsStageUnlockedForTests) must
            // show Stage 1-3 as unlocked now, proving the result is visibly retained, not just
            // present in the saved profile.
            var campaignGo = new GameObject("ReopenedCampaignMap");
            _spawned.Add(campaignGo);
            var campaign = campaignGo.AddComponent<CampaignMapPresenter>();

            Assert.IsTrue(campaign.IsStageUnlockedForTests("1-3"),
                "Reopening Story after the victory must show Stage 1-3 as unlocked.");

            // Returning to Home a second time and reopening Story again must not have changed
            // anything further - no duplicate mutation from merely viewing the map twice.
            PlayerProfile profile = SaveManager.SaveData;
            int claimedCountAfterFirstReopen = profile.claimedStageRewardIds.Count;
            bootstrap.ReturnToCityForTests();
            Assert.IsTrue(campaign.IsStageUnlockedForTests("1-3"), "Stage 1-3 must remain unlocked after a second Home return.");
            Assert.AreEqual(claimedCountAfterFirstReopen, profile.claimedStageRewardIds.Count,
                "Returning to Home a second time must not duplicate any claimed-reward entry.");
        }
    }
}
