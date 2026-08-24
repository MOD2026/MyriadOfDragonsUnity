using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.Story;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CHAPTER 4 DEPTH FILL, 2026-08-22 - CORE_SYSTEMS_CONSTITUTION §0 wartime doctrine. Proves the
    /// newly added Stages 4-1..4-30 (CampaignMapPresenter.chapterStages, appended after 3-30)
    /// through the same deterministic, entirely-production combat policy every prior campaign-
    /// depth fixture uses, plus the complete full chain 1-1..1-12 -> 2-1..2-21 -> 3-1..3-30 ->
    /// 4-1..4-30 -> null (no Chapter 5 invented yet) and content contracts.
    /// </summary>
    public class Chapter4FullDepthTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        private static readonly string[] ApprovedStarterCollectionCardIds =
        {
            "warrior", "novice_knight", "goblin_caster",
            "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
        };

        private static readonly string[] AllStageIdsInOrder = BuildAllStageIdsInOrder();

        private static string[] BuildAllStageIdsInOrder()
        {
            var ids = new List<string>();
            for (int i = 1; i <= 12; i++) ids.Add($"1-{i}");
            for (int i = 1; i <= 21; i++) ids.Add($"2-{i}");
            for (int i = 1; i <= 30; i++) ids.Add($"3-{i}");
            for (int i = 1; i <= 30; i++) ids.Add($"4-{i}");
            return ids.ToArray();
        }

        private static readonly string[] NewChapter4StageIds =
            Enumerable.Range(1, 30).Select(n => $"4-{n}").ToArray();

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsChapter4Depth_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            // Pinned draw order (same technique Chapter1CampaignPlayabilityTests.Stage1_1 already
            // uses): a borderline-tuned roster can flip between win/lose across runs purely on
            // shuffle luck (measured directly - Stage 3-6 passed in isolation, then failed once
            // combined with other suites in the same batch) - this makes every stage's result
            // reproducible instead of occasionally flaky.
            PlayerBattleState.SetShuffleSeedForTests(42);
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

        private static void SaveFreshStarterProfile(List<string> unlockedStages = null)
        {
            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(ApprovedStarterCollectionCardIds),
                activeDeckCardIds = new List<string>(ApprovedStarterCollectionCardIds),
                stamina = 100,
            };
            if (unlockedStages != null) profile.unlockedStageIds = unlockedStages;
            GateTestSupport.EnsureGateAllowsChapter(profile, 4);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the fresh starter profile to save.");
            SaveSystem.ResetCurrentProfileForTests();
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
            bootstrap.SetBattleCanvasVisible(false);
            return bootstrap;
        }

        private HomePagePresenter SpawnHomePagePresenter(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenterUnderTest_Chapter4Depth");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        private static MatchResult RunDeterministicPolicy(GameBootstrap bootstrap, HomePagePresenter home, CampaignStageData stage)
        {
            home.LaunchCampaignStageForTests(stage);
            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, $"Setup: expected Stage {stage.stageId} to launch successfully under this policy.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, $"Setup: expected Stage {stage.stageId} to be a valid, unblocked campaign match.");

            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();

            MatchResult? result = null;
            bootstrap.Battle.OnMatchCompleted += r => result = r;

            int ticksRun = 0;
            while (bootstrap.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap.Battle.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, $"Stage {stage.stageId} must resolve within the normal combat safety limit, never deadlock.");
            }

            Assert.IsTrue(result.HasValue, $"Stage {stage.stageId} must actually resolve (OnMatchCompleted must fire).");
            Debug.Log($"[Chapter4FullDepthTests] Stage {stage.stageId}: {(result.Value.IsVictory ? "VICTORY" : "DEFEAT")} in {ticksRun} tick(s).");
            return result.Value;
        }

        private static void PlayOneCardAndWin(BattleController controller)
        {
            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks);
            }
        }

        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("Chapter4Depth_CardDatabase_Deck");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;
            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize).ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");
            var profile = new PlayerProfile { cardCollection = new List<string>(deckIds), activeDeckCardIds = new List<string>(deckIds) };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();
            Object.DestroyImmediate(databaseGo);
        }

        private static CampaignStageData FindStage(string stageId)
        {
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
            Assert.IsNotNull(stage, $"Setup: expected Stage {stageId} to exist in the production campaign list.");
            return stage;
        }

        // ---------- Order / unlock chain ----------

        [Test]
        public void ChapterStages_ContainsChapters1Through4InOrder()
        {
            // Prefix-only check (not "...then the list ends"): Chapter 5's own depth fill
            // (5-1..5-30, a later task than this file) continues the chain past 4-30 now - see
            // Chapter5FullDepthTests for the complete, current end-to-end chain assertion.
            string cursor = "1-1";
            var actualOrder = new List<string> { cursor };
            for (int i = 0; i < AllStageIdsInOrder.Length - 1; i++)
            {
                string next = CampaignMapPresenter.GetNextStageId(cursor);
                Assert.IsNotNull(next, $"Setup: expected a real next stage after {cursor}.");
                actualOrder.Add(next);
                cursor = next;
            }

            CollectionAssert.AreEqual(AllStageIdsInOrder, actualOrder,
                "The ordered campaign list must begin with exactly 1-1..1-12 then 2-1..2-21 then 3-1..3-30 then 4-1..4-30, in that order, with no gaps.");
        }

        [Test]
        public void GetNextStageId_ChainsThroughAllOfChapter4()
        {
            Assert.AreEqual("4-1", CampaignMapPresenter.GetNextStageId("3-30"));
            Assert.AreEqual("4-11", CampaignMapPresenter.GetNextStageId("4-10"));
            Assert.AreEqual("4-30", CampaignMapPresenter.GetNextStageId("4-29"));
            // 4-30 is no longer the end of the list - Chapter 5's own depth fill (5-1..5-30, a
            // later task) continues it further; see Chapter5FullDepthTests for that complete,
            // current end-of-list contract.
            Assert.AreEqual("5-1", CampaignMapPresenter.GetNextStageId("4-30"), "Stage 5-1 must be the real next stage after 4-30.");
        }

        [Test]
        public void FreshProfile_StillUnlocksOnlyStage1_1_AfterChapter4DepthFill()
        {
            var profile = new PlayerProfile();
            CollectionAssert.AreEqual(new[] { "1-1" }, profile.unlockedStageIds,
                "A brand-new profile must start with exactly Stage 1-1 unlocked, even after the full campaign grew to ninety-three stages.");
        }

        [Test]
        public void WinningEachNewChapter4Stage_UnlocksExactlyTheNextOne()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter4Depth_UnlockChainBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(bootstrap);
            PlayerProfile profile = SaveManager.SaveData;

            for (int i = 0; i < NewChapter4StageIds.Length; i++)
            {
                string stageId = NewChapter4StageIds[i];
                presenter.SetActiveStageForTests(FindStage(stageId));
                PlayOneCardAndWin(controller);

                string expectedNext = CampaignMapPresenter.GetNextStageId(stageId);
                if (expectedNext != null)
                {
                    CollectionAssert.Contains(profile.unlockedStageIds, expectedNext, $"Winning Stage {stageId} must unlock Stage {expectedNext}.");
                }
                else
                {
                    Assert.AreEqual(stageId, "4-30", "Only Stage 4-30 should have no next stage to unlock.");
                }

                if (i + 1 < NewChapter4StageIds.Length)
                {
                    bootstrap.PlayAgainForTests();
                    controller = bootstrap.Battle;
                }
            }
        }

        // ---------- Playability (Auto Formation) - all thirty new stages, one test ----------

        [Test]
        public void AllNewChapter4Stages_AreWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation()
        {
            var unlockedThroughAll = AllStageIdsInOrder.ToList();

            // CardDatabase is a singleton - Initialize() once for the whole test, not once per
            // loop iteration (re-initializing it mid-loop tries to replace the live singleton via
            // Destroy(), which is illegal outside Play Mode).
            var databaseGo = new GameObject("Chapter4Depth_CardDatabase_AllStages");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            // Collect-all rather than abort-on-first-failure: a per-stage Assert.IsTrue inside
            // this loop would throw and stop at the first losing stage, hiding whether every
            // later stage in the chapter is winnable, unwinnable, or mixed - see the CC-owned
            // AF/AI-on Balance Soft investigation (2026-08-23). This keeps the exact same
            // per-stage check and reports one final assertion covering the whole chapter, but
            // never masks data for stages after the first loss.
            var unwinnableStages = new List<string>();
            foreach (string stageId in NewChapter4StageIds)
            {
                SaveFreshStarterProfile(unlockedStages: unlockedThroughAll);
                GameBootstrap bootstrap = SpawnAndInitializeBootstrap($"Chapter4Depth_Bootstrap_{stageId}");
                HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
                CampaignStageData stage = FindStage(stageId);

                MatchResult result = RunDeterministicPolicy(bootstrap, home, stage);
                if (!result.IsVictory)
                {
                    unwinnableStages.Add(stageId);
                }
            }

            Assert.IsEmpty(unwinnableStages,
                $"Stage(s) not winnable by a fresh player using the approved starter collection, a valid saved deck, Auto Formation, and this deterministic legal combat policy: {string.Join(", ", unwinnableStages)}.");
        }

        // ---------- Content contracts ----------

        [Test]
        public void NewChapter4Stages_UseExactlyThreeRealCardDatabaseIds_NeverThePlaceholder()
        {
            var databaseGo = new GameObject("Chapter4Depth_CardDatabase_IdVerify");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            var realIds = new HashSet<string>(database.AllCards.Select(c => c.Id));

            foreach (string stageId in NewChapter4StageIds)
            {
                CampaignStageData stage = FindStage(stageId);
                Assert.AreEqual(3, stage.enemyDeckCardIds.Length, $"Stage {stageId} must field exactly three enemy card ids.");
                CollectionAssert.DoesNotContain(stage.enemyDeckCardIds, "dragon", $"Stage {stageId} must never use the excluded 'dragon' placeholder id.");
                Assert.AreEqual(3, stage.enemyDeckCardIds.Distinct().Count(), $"Stage {stageId}'s own three enemy ids must be pairwise distinct within that stage.");
                foreach (string enemyId in stage.enemyDeckCardIds)
                {
                    Assert.IsTrue(realIds.Contains(enemyId), $"Stage {stageId}'s enemy id '{enemyId}' must exist in the real CardDatabase - no invented ids.");
                }
            }
        }

        [Test]
        public void NoStageAcrossTheEntireNinetyThreeStageCampaign_RepeatsAnotherStagesFullRoster()
        {
            var decks = AllStageIdsInOrder.Select(id => FindStage(id).enemyDeckCardIds.OrderBy(x => x).ToList()).ToList();
            for (int i = 0; i < decks.Count; i++)
            {
                for (int j = i + 1; j < decks.Count; j++)
                {
                    Assert.IsFalse(decks[i].SequenceEqual(decks[j]),
                        $"Stage {AllStageIdsInOrder[i]} and Stage {AllStageIdsInOrder[j]} must never field the exact same three-card roster.");
                }
            }
        }

        [Test]
        public void IsCampaignStageBattleConfigValid_TrueForAllThirtyNewStages()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter4Depth_ConfigValidBootstrap");
            foreach (string stageId in NewChapter4StageIds)
            {
                CampaignStageData stage = FindStage(stageId);
                Assert.IsTrue(bootstrap.IsCampaignStageBattleConfigValid(stage), $"Stage {stageId}'s battle configuration must be valid.");
            }
        }

        [Test]
        public void Rewards_GoldEscalates_GemsFollowLockedMilestoneFormula()
        {
            CampaignStageData stage3_30 = FindStage("3-30");
            int previousGold = stage3_30.goldReward;

            foreach (string stageId in NewChapter4StageIds)
            {
                CampaignStageData stage = FindStage(stageId);
                Assert.Greater(stage.goldReward, previousGold, $"Stage {stageId}'s gold reward must exceed the previous stage's.");
                Assert.AreEqual(CampaignGemRewardRules.ForStage(stageId), stage.gemReward,
                    $"Stage {stageId} Gems must match CampaignGemRewardRules (OWNER_REVIEW_LOG recompute).");
                previousGold = stage.goldReward;
            }

            Assert.AreEqual(CampaignGemRewardRules.ChapterFinaleGems, FindStage("4-30").gemReward,
                "Stage 4-30 is the Chapter 4 finale — locked finale Gem grant.");
        }

        // ---------- Story ----------

        [Test]
        public void StoryDatabase_DefinesPreAndPostVictorySequencesForAllThirtyNewStages()
        {
            foreach (string stageId in NewChapter4StageIds)
            {
                StorySequence pre = StoryDatabase.GetSequence($"{stageId}_pre");
                Assert.IsNotNull(pre, $"Expected StoryDatabase entry '{stageId}_pre'.");
                Assert.Greater(pre.lines.Count, 0);

                string postKey = HomePagePresenter.GetCampaignPostVictoryStoryKey(stageId);
                StorySequence post = StoryDatabase.GetSequence(postKey);
                Assert.IsNotNull(post, $"Expected StoryDatabase entry '{postKey}'.");
                Assert.Greater(post.lines.Count, 0);
            }
        }
    }
}
