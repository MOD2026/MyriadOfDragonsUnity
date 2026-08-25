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
    /// CHAPTER 2 "ASHES OF BOIOTIA" CAMPAIGN CONTENT, 2026-08-22 - owner: expand playable content,
    /// not polish. Adds Stages 2-1/2-2/2-3 after 1-3 to the existing ordered campaign list (no
    /// second campaign system), each with a real, verified 3-card CardDatabase enemy roster (same
    /// Auto-Formation-clearable pattern as Chapter 1's own three stages), escalating rewards above
    /// 1-3's 500/100, and matching _pre/_post StoryDatabase dialogue. Proves the same deterministic,
    /// entirely-production combat policy Chapter1CampaignPlayabilityTests already uses against
    /// each new stage, plus the extended unlock chain and stage-distinctness contracts.
    /// </summary>
    public class Chapter2CampaignContentTests
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
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsChapter2Content_" + System.Guid.NewGuid().ToString("N"));
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

        /// <summary>Same fixed approved-starter-collection profile Chapter1CampaignPlayabilityTests
        /// already uses - a fresh player who owns and has confirmed exactly these ten cards.</summary>
        private static void SaveFreshStarterProfile(List<string> unlockedStages = null)
        {
            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(ApprovedStarterCollectionCardIds),
                activeDeckCardIds = new List<string>(ApprovedStarterCollectionCardIds),
                stamina = 100,
            };
            if (unlockedStages != null) profile.unlockedStageIds = unlockedStages;
            GateTestSupport.EnsureGateAllowsChapter(profile, 2);
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
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (candidate.name == spawnedName && !_spawned.Contains(candidate))
                        _spawned.Add(candidate);
                }
            }
            bootstrap.SetBattleCanvasVisible(false);
            return bootstrap;
        }

        private HomePagePresenter SpawnHomePagePresenter(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenterUnderTest_Chapter2Content");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        /// <summary>Same deterministic, entirely-production combat policy
        /// Chapter1CampaignPlayabilityTests already validates every Chapter 1 stage against: real
        /// Campaign launch, real Auto Formation, real Start Battle (real enemy AI deployment), real
        /// tick-by-tick resolution.</summary>
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
            Debug.Log($"[Chapter2CampaignContentTests] Stage {stage.stageId}: {(result.Value.IsVictory ? "VICTORY" : "DEFEAT")} in {ticksRun} tick(s).");
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

        private static CampaignStageData FindStage(string stageId)
        {
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
            Assert.IsNotNull(stage, $"Setup: expected Stage {stageId} to exist in the production campaign list.");
            return stage;
        }

        // ---------- Unlock chain ----------

        [Test]
        public void FreshProfile_StillUnlocksOnlyStage1_1_AfterAddingChapter2()
        {
            var profile = new PlayerProfile();
            CollectionAssert.AreEqual(new[] { "1-1" }, profile.unlockedStageIds,
                "Requirement 2: a brand-new profile must still start with exactly Stage 1-1 unlocked, even after Chapter 2 was added to the ordered list.");
        }

        [Test]
        public void GetNextStageId_ChainsThroughAllOfChapter2()
        {
            // Chapter 1 depth expansion (2026-08-22, a later task than this file's own): 1-3 no
            // longer chains directly into 2-1 - CampaignMapPresenter.chapterStages now inserts
            // 1-4..1-12 between them. This test's own subject is the Chapter 2 portion of the
            // chain (2-1 -> 2-2 -> 2-3), which is unaffected; only the Chapter 1 entry point into
            // it moved from "1-3" to "1-12" - see Chapter1FullDepthTests for full coverage of the
            // 1-4..1-12 links. 2-3 is also no longer the end of the list - Chapter 2's own depth
            // fill (2-4..2-21, a later task than this file) continues the chain further; see
            // Chapter2FullDepthTests for that complete, current chain assertion.
            Assert.AreEqual("2-1", CampaignMapPresenter.GetNextStageId("1-12"));
            Assert.AreEqual("2-2", CampaignMapPresenter.GetNextStageId("2-1"));
            Assert.AreEqual("2-3", CampaignMapPresenter.GetNextStageId("2-2"));
            Assert.AreEqual("2-4", CampaignMapPresenter.GetNextStageId("2-3"), "Stage 2-4 must be the real next stage after 2-3 in the existing ordered campaign list.");
        }

        [Test]
        public void WinningEachChapter2Stage_UnlocksExactlyTheNextOne()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter2Content_UnlockChainBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(bootstrap);

            presenter.SetActiveStageForTests(FindStage("2-1"));
            PlayOneCardAndWin(controller);
            PlayerProfile profile = SaveManager.SaveData;
            CollectionAssert.Contains(profile.unlockedStageIds, "2-2", "Winning Stage 2-1 must unlock Stage 2-2.");
            Assert.IsFalse(profile.unlockedStageIds.Contains("2-3"), "Winning Stage 2-1 must not unlock Stage 2-3 early.");

            bootstrap.PlayAgainForTests();
            controller = bootstrap.Battle;
            presenter.SetActiveStageForTests(FindStage("2-2"));
            PlayOneCardAndWin(controller);
            CollectionAssert.Contains(profile.unlockedStageIds, "2-3", "Winning Stage 2-2 must unlock Stage 2-3.");

            bootstrap.PlayAgainForTests();
            controller = bootstrap.Battle;
            presenter.SetActiveStageForTests(FindStage("2-3"));
            PlayOneCardAndWin(controller);
            // 2-3 is no longer the ordered campaign list's terminal stage - Chapter 2's own
            // depth fill (2-4..2-21, a later task than this file) continues the chain further;
            // see Chapter2FullDepthTests for that complete, current end-of-list contract.
            CollectionAssert.Contains(profile.unlockedStageIds, "2-4", "Winning Stage 2-3 must unlock Stage 2-4.");
        }

        /// <summary>Same helper pattern every Battle-adjacent test file in this suite already
        /// uses: a normal match requires a confirmed valid saved deck before it deals any hand.</summary>
        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("Chapter2Content_CardDatabase_Deck");
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

        // ---------- Playability (Auto Formation) ----------

        [Test]
        public void Stage2_1_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation()
        {
            var databaseGo = new GameObject("Chapter2Content_CardDatabase_2_1");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveFreshStarterProfile(unlockedStages: new List<string> { "1-1", "1-2", "1-3", "2-1" });
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter2Content_Bootstrap_2_1");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = FindStage("2-1");

            MatchResult result = RunDeterministicPolicy(bootstrap, home, stage);

            Assert.IsTrue(result.IsVictory,
                "Stage 2-1 must be winnable by a fresh player using the approved starter collection, a valid saved deck, Auto Formation, and this deterministic legal combat policy.");
        }

        [Test]
        public void Stage2_2_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation()
        {
            var databaseGo = new GameObject("Chapter2Content_CardDatabase_2_2");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveFreshStarterProfile(unlockedStages: new List<string> { "1-1", "1-2", "1-3", "2-1", "2-2" });
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter2Content_Bootstrap_2_2");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = FindStage("2-2");

            MatchResult result = RunDeterministicPolicy(bootstrap, home, stage);

            Assert.IsTrue(result.IsVictory,
                "Stage 2-2 must be winnable by a fresh player using the approved starter collection, a valid saved deck, Auto Formation, and this deterministic legal combat policy.");
        }

        [Test]
        public void Stage2_3_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation()
        {
            var databaseGo = new GameObject("Chapter2Content_CardDatabase_2_3");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveFreshStarterProfile(unlockedStages: new List<string> { "1-1", "1-2", "1-3", "2-1", "2-2", "2-3" });
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter2Content_Bootstrap_2_3");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = FindStage("2-3");

            MatchResult result = RunDeterministicPolicy(bootstrap, home, stage);

            Assert.IsTrue(result.IsVictory,
                "Stage 2-3 must be winnable by a fresh player using the approved starter collection, a valid saved deck, Auto Formation, and this deterministic legal combat policy.");
        }

        // ---------- Distinctness / configuration ----------

        [Test]
        public void Chapter2Stages_RemainDistinctConfiguredEncounters_FromEachOtherAndFromChapter1()
        {
            CampaignStageData stage1_1 = FindStage("1-1");
            CampaignStageData stage1_2 = FindStage("1-2");
            CampaignStageData stage1_3 = FindStage("1-3");
            CampaignStageData stage2_1 = FindStage("2-1");
            CampaignStageData stage2_2 = FindStage("2-2");
            CampaignStageData stage2_3 = FindStage("2-3");

            var allDecks = new[] { stage1_1, stage1_2, stage1_3, stage2_1, stage2_2, stage2_3 }
                .Select(s => s.enemyDeckCardIds).ToList();

            for (int i = 0; i < allDecks.Count; i++)
            {
                for (int j = i + 1; j < allDecks.Count; j++)
                {
                    CollectionAssert.IsEmpty(allDecks[i].Intersect(allDecks[j]),
                        $"Every configured stage's enemy deck must be distinct from every other stage's - overlap found between index {i} and {j}.");
                }
            }

            foreach (CampaignStageData stage in new[] { stage2_1, stage2_2, stage2_3 })
            {
                Assert.AreEqual(3, stage.enemyDeckCardIds.Length, $"Stage {stage.stageId} must field exactly three enemy card ids, the same Auto-Formation-clearable pattern as Chapter 1.");
                CollectionAssert.DoesNotContain(stage.enemyDeckCardIds, "dragon", $"Stage {stage.stageId} must never use the excluded 'dragon' placeholder id.");
            }
        }

        [Test]
        public void Chapter2Stages_UseOnlyRealCardDatabaseIds()
        {
            var databaseGo = new GameObject("Chapter2Content_CardDatabase_IdVerify");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            var realIds = new HashSet<string>(database.AllCards.Select(c => c.Id));

            foreach (string stageId in new[] { "2-1", "2-2", "2-3" })
            {
                CampaignStageData stage = FindStage(stageId);
                foreach (string enemyId in stage.enemyDeckCardIds)
                {
                    Assert.IsTrue(realIds.Contains(enemyId), $"Stage {stageId}'s enemy id '{enemyId}' must exist in the real CardDatabase (card_data.json) - no invented ids.");
                }
            }
        }

        [Test]
        public void IsCampaignStageBattleConfigValid_TrueForAllThreeChapter2Stages()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter2Content_ConfigValidBootstrap");

            foreach (string stageId in new[] { "2-1", "2-2", "2-3" })
            {
                CampaignStageData stage = FindStage(stageId);
                Assert.IsTrue(bootstrap.IsCampaignStageBattleConfigValid(stage), $"Stage {stageId}'s battle configuration must be valid.");
            }
        }

        [Test]
        public void Chapter2Rewards_EscalateAboveStage1_3()
        {
            CampaignStageData stage1_3 = FindStage("1-3");
            CampaignStageData stage2_1 = FindStage("2-1");
            CampaignStageData stage2_2 = FindStage("2-2");
            CampaignStageData stage2_3 = FindStage("2-3");

            Assert.Greater(stage2_1.goldReward, stage1_3.goldReward, "Stage 2-1's gold reward must exceed Stage 1-3's.");
            Assert.AreEqual(CampaignGemRewardRules.ChapterFinaleGems, stage1_3.gemReward,
                "Stage 1-3 is the Chapter 1 finale — locked finale Gem grant.");
            Assert.AreEqual(CampaignGemRewardRules.RegularStageGems, stage2_1.gemReward,
                "Stage 2-1 is a regular stage — locked regular Gem grant.");
            Assert.Greater(stage2_2.goldReward, stage2_1.goldReward, "Rewards must keep escalating through Chapter 2.");
            Assert.Greater(stage2_3.goldReward, stage2_2.goldReward, "Stage 2-3 must be the highest-rewarding stage so far.");
            Assert.AreEqual(CampaignGemRewardRules.RegularStageGems, stage2_2.gemReward);
            Assert.AreEqual(CampaignGemRewardRules.RegularStageGems, stage2_3.gemReward);
        }

        // ---------- Story ----------

        [Test]
        public void StoryDatabase_DefinesPreAndPostVictorySequencesForAllChapter2Stages()
        {
            foreach (string stageId in new[] { "2-1", "2-2", "2-3" })
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
