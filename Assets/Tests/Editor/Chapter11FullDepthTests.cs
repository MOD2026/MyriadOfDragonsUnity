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
    /// CHAPTER 11 DEPTH FILL, 2026-08-25 - CORE_SYSTEMS_CONSTITUTION §0 wartime doctrine. Proves the
    /// newly added Stages 11-1..11-30 (appended after 10-30)
    /// through the same deterministic, entirely-production combat policy every prior campaign-
    /// depth fixture uses, plus the complete full chain 1-1..1-12 -> ... -> 9-1..9-30 ->
    /// 10-1..10-30 -> 11-1..11-30 -> null and content contracts.
    /// </summary>
    public class Chapter11FullDepthTests
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
            for (int i = 1; i <= 30; i++) ids.Add($"5-{i}");
            for (int i = 1; i <= 30; i++) ids.Add($"6-{i}");
            for (int i = 1; i <= 30; i++) ids.Add($"7-{i}");
            for (int i = 1; i <= 30; i++) ids.Add($"8-{i}");
            for (int i = 1; i <= 30; i++) ids.Add($"9-{i}");
            for (int i = 1; i <= 30; i++) ids.Add($"10-{i}");
            for (int i = 1; i <= 30; i++) ids.Add($"11-{i}");
            return ids.ToArray();
        }

        private static readonly string[] NewChapter11StageIds =
            Enumerable.Range(1, 30).Select(n => $"11-{n}").ToArray();

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsChapter11Depth_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
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
            GateTestSupport.EnsureGateAllowsChapter(profile, 11);
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
            var go = new GameObject("HomePagePresenterUnderTest_Chapter11Depth");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        private static MatchResult RunDeterministicPolicy(GameBootstrap bootstrap, HomePagePresenter home, CampaignStageData stage)
        {
            home.LaunchCampaignStageForTests(stage);
            bootstrap.Battle.SetAiSpellCastRngSeedForTests(42); // AI Spell Cast Probability Gate (LOCKED 2026-08-24): pin the AI-cast RNG stream too, not just PlayerBattleState.SetShuffleSeedForTests - otherwise this chapter's winnability check is non-deterministic (confirmed empirically: same code, different failing stage across separate runs).
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
            Debug.Log($"[Chapter11FullDepthTests] Stage {stage.stageId}: {(result.Value.IsVictory ? "VICTORY" : "DEFEAT")} in {ticksRun} tick(s).");
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
            var databaseGo = new GameObject("Chapter11Depth_CardDatabase_Deck");
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
        public void ChapterStages_ContainsTheCompleteChainFrom1_1through11_30_EndingThere()
        {
            string cursor = "1-1";
            var actualOrder = new List<string> { cursor };
            while (true)
            {
                string next = CampaignMapPresenter.GetNextStageId(cursor);
                if (next == null) break;
                actualOrder.Add(next);
                cursor = next;
                Assert.LessOrEqual(actualOrder.Count, AllStageIdsInOrder.Length,
                    "Setup: the ordered campaign list grew unexpectedly long - possible infinite chain or duplicate id.");
            }

            CollectionAssert.AreEqual(AllStageIdsInOrder, actualOrder,
                "The complete ordered campaign list must be exactly 1-1..1-12 then 2-1..2-21 then 3-1..3-30 then 4-1..4-30 then 5-1..5-30 then 6-1..6-30 then 7-1..7-30 then 8-1..8-30 then 9-1..9-30 then 10-1..10-30 then 11-1..11-30, in that order, with no gaps.");
        }

        [Test]
        public void GetNextStageId_11_30IsTerminal()
        {
            Assert.AreEqual("11-1", CampaignMapPresenter.GetNextStageId("10-30"));
            Assert.AreEqual("11-30", CampaignMapPresenter.GetNextStageId("11-29"));
            Assert.IsNull(CampaignMapPresenter.GetNextStageId("11-30"), "Stage 11-30 is the campaign terminal.");
        }

        [Test]
        public void FreshProfile_StillUnlocksOnlyStage1_1_AfterChapter11DepthFill()
        {
            var profile = new PlayerProfile();
            CollectionAssert.AreEqual(new[] { "1-1" }, profile.unlockedStageIds,
                "A brand-new profile must start with exactly Stage 1-1 unlocked, even after the full campaign grew to three hundred three stages.");
        }

        [Test]
        public void WinningEachNewChapter11Stage_UnlocksExactlyTheNextOne()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter11Depth_UnlockChainBootstrap");
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHomePagePresenter(bootstrap);
            PlayerProfile profile = SaveManager.SaveData;

            for (int i = 0; i < NewChapter11StageIds.Length; i++)
            {
                string stageId = NewChapter11StageIds[i];
                presenter.SetActiveStageForTests(FindStage(stageId));
                // AI Spell Cast Probability Gate: BattleController.StartMatch rebuilds
                // _aiSpellCastRng from a fresh Guid every call (see StartMatch's
                // rngSeed ?? Guid.NewGuid() line), and PlayAgainForTests() starts a new
                // match each iteration - so pinning once outside this loop is not enough,
                // and RunDeterministicPolicy's pin (e5f2ea1) never applied on this path at
                // all. Re-pin per match or the unlock chain fails at a different stage
                // every run (observed 7-29 -> 7-14 -> 7-12 across three runs).
                controller.SetAiSpellCastRngSeedForTests(42);
                PlayOneCardAndWin(controller);

                string expectedNext = CampaignMapPresenter.GetNextStageId(stageId);
                if (expectedNext != null)
                {
                    CollectionAssert.Contains(profile.unlockedStageIds, expectedNext, $"Winning Stage {stageId} must unlock Stage {expectedNext}.");
                }
                else
                {
                    Assert.AreEqual(stageId, "11-30", "Only Stage 11-30 should have no next stage.");
                }

                if (i + 1 < NewChapter11StageIds.Length)
                {
                    bootstrap.PlayAgainForTests();
                    controller = bootstrap.Battle;
                }
            }
        }

        // ---------- Playability (Auto Formation) - all thirty new stages, one test ----------

        [Test]
        public void AllNewChapter11Stages_AreWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation()
        {
            var unlockedThroughAll = AllStageIdsInOrder.ToList();

            var databaseGo = new GameObject("Chapter11Depth_CardDatabase_AllStages");
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
            foreach (string stageId in NewChapter11StageIds)
            {
                SaveFreshStarterProfile(unlockedStages: unlockedThroughAll);
                GameBootstrap bootstrap = SpawnAndInitializeBootstrap($"Chapter11Depth_Bootstrap_{stageId}");
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
        public void NewChapter11Stages_UseExactlyThreeRealCardDatabaseIds_NeverThePlaceholder()
        {
            var databaseGo = new GameObject("Chapter11Depth_CardDatabase_IdVerify");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            var realIds = new HashSet<string>(database.AllCards.Select(c => c.Id));

            foreach (string stageId in NewChapter11StageIds)
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
        public void NoStageAcrossTheEntireCampaign_RepeatsAnotherStagesFullRoster()
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
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Chapter11Depth_ConfigValidBootstrap");
            foreach (string stageId in NewChapter11StageIds)
            {
                CampaignStageData stage = FindStage(stageId);
                Assert.IsTrue(bootstrap.IsCampaignStageBattleConfigValid(stage), $"Stage {stageId}'s battle configuration must be valid.");
            }
        }

        [Test]
        public void Rewards_GoldEscalates_GemsFollowLockedMilestoneFormula()
        {
            CampaignStageData stage9_30 = FindStage("10-30");
            int previousGold = stage9_30.goldReward;

            foreach (string stageId in NewChapter11StageIds)
            {
                CampaignStageData stage = FindStage(stageId);
                Assert.Greater(stage.goldReward, previousGold, $"Stage {stageId}'s gold reward must exceed the previous stage's.");
                Assert.AreEqual(CampaignGemRewardRules.ForStage(stageId), stage.gemReward,
                    $"Stage {stageId} Gems must match CampaignGemRewardRules (OWNER_REVIEW_LOG recompute).");
                previousGold = stage.goldReward;
            }

            Assert.AreEqual(CampaignGemRewardRules.ChapterFinaleGems, FindStage("11-30").gemReward,
                "Stage 11-30 is the Chapter 11 finale — locked finale Gem grant.");
        }

        [Test]
        public void CampaignCh1Through11_GemTotal_MatchesLockedMilestoneRecompute()
        {
            var stages = CampaignMapPresenter.GetAllStagesForTests()
                .Where(s =>
                {
                    CampaignMapPresenter.TryParseStageChapter(s.stageId, out int chapter);
                    return chapter <= 11;
                })
                .ToList();
            Assert.AreEqual(303, stages.Count, "Ch1-11 spine must remain 303 stages.");

            int total = 0;
            int finales = 0;
            foreach (CampaignStageData stage in stages)
            {
                Assert.AreEqual(CampaignGemRewardRules.ForStage(stage.stageId), stage.gemReward,
                    $"Stage {stage.stageId} Gems must match CampaignGemRewardRules.");
                total += stage.gemReward;
                if (HomePagePresenter.IsChapterFinalePermitStage(stage.stageId))
                    finales++;
            }

            Assert.AreEqual(11, finales, "Expected exactly 11 chapter finales in Ch1–11.");
            Assert.AreEqual(CampaignGemRewardRules.LockedTotalCh1Through11, total,
                "OWNER_REVIEW_LOG: 292×8 + 11×440 = 7,176.");
        }

        // ---------- Story ----------

        [Test]
        public void StoryDatabase_DefinesPreAndPostVictorySequencesForAllThirtyNewStages()
        {
            foreach (string stageId in NewChapter11StageIds)
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
