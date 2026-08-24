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
    /// CHAPTER 1 DEPTH EXPANSION, 2026-08-22 - owner: "stickiness = many sequential fights",
    /// Chapter 1 must be twelve stages (1-1..1-12), not three. Proves the newly added Stages
    /// 1-4..1-12 (CampaignMapPresenter.chapterStages, inserted before every 2-x stage) through the
    /// same deterministic, entirely-production combat policy Chapter1CampaignPlayabilityTests
    /// already validates 1-1/1-2/1-3 against: real Campaign launch, real Auto Formation, real
    /// Start Battle (real enemy AI deployment), real tick-by-tick resolution - plus the full
    /// unlock chain through 1-12 -> 2-1 and stage-content contracts (distinct rosters, real ids,
    /// no placeholder, valid battle configuration, escalating rewards with headroom left for
    /// Chapter 2).
    /// </summary>
    public class Chapter1FullDepthTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        private static readonly string[] ApprovedStarterCollectionCardIds =
        {
            "warrior", "novice_knight", "goblin_caster",
            "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
        };

        private static readonly string[] NewChapter1StageIds =
        {
            "1-4", "1-5", "1-6", "1-7", "1-8", "1-9", "1-10", "1-11", "1-12",
        };

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsChapter1Depth_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            // Pinned draw order (same technique Chapter1CampaignPlayabilityTests.Stage1_1 already
            // uses): makes every stage's playability result reproducible instead of occasionally
            // flaky when combined with other suites in the same batch (found while retuning
            // Chapter 3/4's own depth-fill rosters).
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
            var go = new GameObject("HomePagePresenterUnderTest_Chapter1Depth");
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
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks,
                    $"Stage {stage.stageId} must resolve within the normal combat safety limit, never deadlock.");
            }

            Assert.IsTrue(result.HasValue, $"Stage {stage.stageId} must actually resolve (OnMatchCompleted must fire).");
            Debug.Log($"[Chapter1FullDepthTests] Stage {stage.stageId}: {(result.Value.IsVictory ? "VICTORY" : "DEFEAT")} in {ticksRun} tick(s).");
            return result.Value;
        }

        private static void PlayOneCardAndWin(BattleController controller)
        {
            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front), "Setup: expected to be able to play at least one card into Front.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a knockout well inside the tick cap.");
            }
        }

        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("Chapter1Depth_CardDatabase_Deck");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

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

        private static CampaignStageData FindStage(string stageId)
        {
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
            Assert.IsNotNull(stage, $"Setup: expected Stage {stageId} to exist in the production campaign list.");
            return stage;
        }

        // ---------- Order / unlock chain ----------

        [Test]
        public void ChapterStages_ContainsAllTwelveChapter1StagesBeforeAnyChapter2Stage_InOrder()
        {
            // Prefix-only check (not "...then the list ends"): Chapter 2's own depth fill
            // (2-4..2-21, a later task than this file) continues the chain well past 2-3 now -
            // see Chapter2FullDepthTests for the complete, current end-to-end chain assertion.
            // This test's own subject - Chapter 1 is twelve stages, in order, before any 2-x
            // stage - is unaffected by how long Chapter 2 itself later grew.
            string[] expectedOrderPrefix =
            {
                "1-1", "1-2", "1-3", "1-4", "1-5", "1-6", "1-7", "1-8", "1-9", "1-10", "1-11", "1-12",
                "2-1", "2-2", "2-3",
            };

            string cursor = "1-1";
            var actualOrder = new List<string> { cursor };
            for (int i = 0; i < expectedOrderPrefix.Length - 1; i++)
            {
                string next = CampaignMapPresenter.GetNextStageId(cursor);
                Assert.IsNotNull(next, $"Setup: expected a real next stage after {cursor}.");
                actualOrder.Add(next);
                cursor = next;
            }

            CollectionAssert.AreEqual(expectedOrderPrefix, actualOrder,
                "The ordered campaign list must begin with exactly 1-1..1-12 then 2-1..2-3, in that order, with no gaps or reordering.");
        }

        [Test]
        public void FreshProfile_StillUnlocksOnlyStage1_1_WithTwelveChapter1Stages()
        {
            var profile = new PlayerProfile();
            CollectionAssert.AreEqual(new[] { "1-1" }, profile.unlockedStageIds,
                "A brand-new profile must start with exactly Stage 1-1 unlocked, even after Chapter 1 grew to twelve stages.");
        }

        [Test]
        public void GetNextStageId_ChainsThroughAllTwelveChapter1Stages_ThenIntoChapter2()
        {
            Assert.AreEqual("1-4", CampaignMapPresenter.GetNextStageId("1-3"));
            Assert.AreEqual("1-5", CampaignMapPresenter.GetNextStageId("1-4"));
            Assert.AreEqual("1-6", CampaignMapPresenter.GetNextStageId("1-5"));
            Assert.AreEqual("1-7", CampaignMapPresenter.GetNextStageId("1-6"));
            Assert.AreEqual("1-8", CampaignMapPresenter.GetNextStageId("1-7"));
            Assert.AreEqual("1-9", CampaignMapPresenter.GetNextStageId("1-8"));
            Assert.AreEqual("1-10", CampaignMapPresenter.GetNextStageId("1-9"));
            Assert.AreEqual("1-11", CampaignMapPresenter.GetNextStageId("1-10"));
            Assert.AreEqual("1-12", CampaignMapPresenter.GetNextStageId("1-11"));
            Assert.AreEqual("2-1", CampaignMapPresenter.GetNextStageId("1-12"),
                "Stage 1-12 must chain into the existing Chapter 2 opener, Stage 2-1.");
        }

        [Test]
        public void WinningEachNewChapter1Stage_UnlocksExactlyTheNextOne()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter1Depth_UnlockChainBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(bootstrap);
            PlayerProfile profile = SaveManager.SaveData;

            for (int i = 0; i < NewChapter1StageIds.Length; i++)
            {
                string stageId = NewChapter1StageIds[i];
                presenter.SetActiveStageForTests(FindStage(stageId));
                PlayOneCardAndWin(controller);

                string expectedNext = CampaignMapPresenter.GetNextStageId(stageId);
                CollectionAssert.Contains(profile.unlockedStageIds, expectedNext, $"Winning Stage {stageId} must unlock Stage {expectedNext}.");

                if (i + 1 < NewChapter1StageIds.Length)
                {
                    bootstrap.PlayAgainForTests();
                    controller = bootstrap.Battle;
                }
            }

            CollectionAssert.Contains(profile.unlockedStageIds, "2-1", "Winning Stage 1-12 must unlock Chapter 2's opener, Stage 2-1.");
        }

        // ---------- Playability (Auto Formation) - at least 1-4, 1-8, 1-12; here, all nine ----------

        private void AssertStageIsWinnableUnderAutoFormation(string stageId, List<string> unlockedThroughThisStage)
        {
            var databaseGo = new GameObject($"Chapter1Depth_CardDatabase_{stageId}");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveFreshStarterProfile(unlockedStages: unlockedThroughThisStage);
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap($"Chapter1Depth_Bootstrap_{stageId}");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = FindStage(stageId);

            MatchResult result = RunDeterministicPolicy(bootstrap, home, stage);

            Assert.IsTrue(result.IsVictory,
                $"Stage {stageId} must be winnable by a fresh player using the approved starter collection, a valid saved deck, Auto Formation, and this deterministic legal combat policy.");
        }

        private static List<string> UnlockedThrough(string stageId)
        {
            var unlocked = new List<string> { "1-1", "1-2", "1-3" };
            foreach (string id in NewChapter1StageIds)
            {
                unlocked.Add(id);
                if (id == stageId) break;
            }
            return unlocked;
        }

        [Test]
        public void Stage1_4_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation() =>
            AssertStageIsWinnableUnderAutoFormation("1-4", UnlockedThrough("1-4"));

        [Test]
        public void Stage1_5_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation() =>
            AssertStageIsWinnableUnderAutoFormation("1-5", UnlockedThrough("1-5"));

        [Test]
        public void Stage1_6_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation() =>
            AssertStageIsWinnableUnderAutoFormation("1-6", UnlockedThrough("1-6"));

        [Test]
        public void Stage1_7_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation() =>
            AssertStageIsWinnableUnderAutoFormation("1-7", UnlockedThrough("1-7"));

        [Test]
        public void Stage1_8_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation() =>
            AssertStageIsWinnableUnderAutoFormation("1-8", UnlockedThrough("1-8"));

        [Test]
        public void Stage1_9_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation() =>
            AssertStageIsWinnableUnderAutoFormation("1-9", UnlockedThrough("1-9"));

        [Test]
        public void Stage1_10_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation() =>
            AssertStageIsWinnableUnderAutoFormation("1-10", UnlockedThrough("1-10"));

        [Test]
        public void Stage1_11_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation() =>
            AssertStageIsWinnableUnderAutoFormation("1-11", UnlockedThrough("1-11"));

        [Test]
        public void Stage1_12_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation() =>
            AssertStageIsWinnableUnderAutoFormation("1-12", UnlockedThrough("1-12"));

        // ---------- Content contracts ----------

        [Test]
        public void NewChapter1Stages_UseExactlyThreeRealCardDatabaseIds_NeverThePlaceholder()
        {
            var databaseGo = new GameObject("Chapter1Depth_CardDatabase_IdVerify");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            var realIds = new HashSet<string>(database.AllCards.Select(c => c.Id));

            foreach (string stageId in NewChapter1StageIds)
            {
                CampaignStageData stage = FindStage(stageId);
                Assert.AreEqual(3, stage.enemyDeckCardIds.Length, $"Stage {stageId} must field exactly three enemy card ids.");
                CollectionAssert.DoesNotContain(stage.enemyDeckCardIds, "dragon", $"Stage {stageId} must never use the excluded 'dragon' placeholder id.");
                foreach (string enemyId in stage.enemyDeckCardIds)
                {
                    Assert.IsTrue(realIds.Contains(enemyId), $"Stage {stageId}'s enemy id '{enemyId}' must exist in the real CardDatabase - no invented ids.");
                }
            }
        }

        [Test]
        public void AllChapter1AndChapter2Stages_NeverRepeatTheIdenticalThreeCardRoster()
        {
            // MEASURED CONSTRAINT, see Stage1_5EnemyDeck's own comment in CampaignMapPresenter.cs:
            // twelve Chapter 1 stages at three enemies each is 36 slots drawn from a usable pool
            // that only contains 16 genuinely low-power (rarity 1-2) cards, so an id appearing in
            // more than one stage is an arithmetic necessity once the AF-clearable power ceiling
            // (also measured, not guessed) is respected - "disjoint... where possible" explicitly
            // anticipated this. The real content contract at this scale is "never fight the exact
            // same three-card encounter twice", not "never see a unit type again" - proven here as
            // full-roster equality, not id-set intersection.
            string[] allStageIds = { "1-1", "1-2", "1-3", "1-4", "1-5", "1-6", "1-7", "1-8", "1-9", "1-10", "1-11", "1-12", "2-1", "2-2", "2-3" };
            var decks = allStageIds.Select(id => FindStage(id).enemyDeckCardIds.OrderBy(x => x).ToList()).ToList();

            for (int i = 0; i < decks.Count; i++)
            {
                for (int j = i + 1; j < decks.Count; j++)
                {
                    bool identicalRoster = decks[i].SequenceEqual(decks[j]);
                    Assert.IsFalse(identicalRoster,
                        $"Stage {allStageIds[i]} and Stage {allStageIds[j]} must never field the exact same three-card roster.");
                }
            }
        }

        [Test]
        public void EveryStage_FieldsThreeDistinctEnemyIdsWithinItsOwnRoster()
        {
            // The one uniqueness rule production code itself actually enforces
            // (IsCampaignStageBattleConfigValid rejects a duplicate id within one stage's own
            // three) - proven directly here as its own explicit contract, now that cross-stage
            // reuse is expected rather than forbidden.
            string[] allStageIds = { "1-1", "1-2", "1-3", "1-4", "1-5", "1-6", "1-7", "1-8", "1-9", "1-10", "1-11", "1-12", "2-1", "2-2", "2-3" };
            foreach (string stageId in allStageIds)
            {
                string[] deck = FindStage(stageId).enemyDeckCardIds;
                Assert.AreEqual(deck.Length, deck.Distinct().Count(), $"Stage {stageId}'s own three enemy ids must be pairwise distinct within that stage.");
            }
        }

        [Test]
        public void IsCampaignStageBattleConfigValid_TrueForAllNineNewStages()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter1Depth_ConfigValidBootstrap");
            foreach (string stageId in NewChapter1StageIds)
            {
                CampaignStageData stage = FindStage(stageId);
                Assert.IsTrue(bootstrap.IsCampaignStageBattleConfigValid(stage), $"Stage {stageId}'s battle configuration must be valid.");
            }
        }

        [Test]
        public void Rewards_GoldEscalatesAcross1_4Through1_12_GemsFollowLockedMilestoneFormula()
        {
            CampaignStageData stage1_3 = FindStage("1-3");
            CampaignStageData stage2_1 = FindStage("2-1");
            int previousGold = stage1_3.goldReward;

            Assert.AreEqual(CampaignGemRewardRules.ChapterFinaleGems, stage1_3.gemReward,
                "Stage 1-3 is the Chapter 1 finale — locked finale Gem grant.");

            foreach (string stageId in NewChapter1StageIds)
            {
                CampaignStageData stage = FindStage(stageId);
                Assert.Greater(stage.goldReward, previousGold, $"Stage {stageId}'s gold reward must exceed the previous stage's.");
                Assert.AreEqual(CampaignGemRewardRules.ForStage(stageId), stage.gemReward,
                    $"Stage {stageId} Gems must match CampaignGemRewardRules (OWNER_REVIEW_LOG recompute).");
                previousGold = stage.goldReward;
            }

            Assert.Less(previousGold, stage2_1.goldReward, "Stage 1-12's gold reward must stay below Chapter 2's own opener (2-1), leaving headroom for Chapter 2.");
            Assert.AreEqual(CampaignGemRewardRules.RegularStageGems, stage2_1.gemReward,
                "Stage 2-1 is a regular stage — locked regular Gem grant.");
        }

        // ---------- Story ----------

        [Test]
        public void StoryDatabase_DefinesPreAndPostVictorySequencesForAllNineNewStages()
        {
            foreach (string stageId in NewChapter1StageIds)
            {
                StorySequence pre = StoryDatabase.GetSequence($"{stageId}_pre");
                Assert.IsNotNull(pre, $"Expected StoryDatabase entry '{stageId}_pre'.");
                Assert.Greater(pre.lines.Count, 0, $"'{stageId}_pre' must have at least one dialogue line.");

                string postKey = HomePagePresenter.GetCampaignPostVictoryStoryKey(stageId);
                StorySequence post = StoryDatabase.GetSequence(postKey);
                Assert.IsNotNull(post, $"Expected StoryDatabase entry '{postKey}'.");
                Assert.Greater(post.lines.Count, 0, $"'{postKey}' must have at least one dialogue line.");
            }
        }
    }
}
