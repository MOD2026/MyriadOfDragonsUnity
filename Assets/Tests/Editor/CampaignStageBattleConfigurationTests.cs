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
    /// CAMPAIGN-STAGE BATTLE-CONFIGURATION CONTRACT (release feature) - proves the whole handoff
    /// through the real production owners: GameBootstrap.SetPendingCampaignStageForNextMatch /
    /// TryResolveCampaignEnemyDeck / IsCampaignStageBattleConfigValid (the existing Battle startup
    /// path's own optional campaign configuration, not a second battle system), and
    /// HomePagePresenter.OnToBattleClickedForTests (the exact private handler the real "To
    /// Battle"/Campaign-launch call sites use). Stage data itself comes from
    /// CampaignMapPresenter.GetStageForTests, the sole production stage-order/config authority -
    /// never a hand-typed stand-in that could drift from what actually ships.
    /// </summary>
    public class CampaignStageBattleConfigurationTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsCampaignBattle_" + System.Guid.NewGuid().ToString("N"));
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

        /// <summary>Same prerequisite ChapterOneProgressionTests/NormalBattleEntryContractTests
        /// already rely on: a normal-path match (campaign or not) needs a real confirmed player
        /// deck before it will deal any hand at all.</summary>
        private static List<string> SaveValidDeckForNormalMatch(CardDatabase database)
        {
            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");

            var profile = new PlayerProfile();
            profile.cardCollection = new List<string>(deckIds);
            profile.activeDeckCardIds = new List<string>(deckIds);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();
            return deckIds;
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
            bootstrap.SetBattleCanvasVisible(false); // mirrors HomePagePresenter.Start()'s own boot state
            return bootstrap;
        }

        private HomePagePresenter SpawnHomePagePresenter(BattleController boundController)
        {
            var go = new GameObject("HomePagePresenterUnderTest_Campaign");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(boundController);
            return presenter;
        }

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

        [Test]
        public void EachChapter1Stage_ResolvesItsOwnDistinctConfiguredEnemyDeck()
        {
            var databaseGo = new GameObject("CampaignBattle_CardDatabase");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database);

            var dealtByStage = new Dictionary<string, HashSet<string>>();

            foreach (string stageId in new[] { "1-1", "1-2", "1-3" })
            {
                CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
                Assert.IsNotNull(stage, $"Setup: expected Stage {stageId} to exist in the production campaign list.");

                GameBootstrap bootstrap = SpawnAndInitializeBootstrap($"CampaignBattle_Bootstrap_{stageId}");

                // Mirrors the real single-call handoff exactly: HomePagePresenter.OnToBattleClicked
                // attaches the stage then reveals Battle - SetPendingCampaignStageForNextMatch
                // followed by SetBattleCanvasVisible(true), nothing else.
                bootstrap.SetPendingCampaignStageForNextMatch(stage);
                bootstrap.SetBattleCanvasVisible(true);

                Assert.IsNull(bootstrap.NormalMatchStatusForTests, $"Stage {stageId}: a valid configured enemy deck must not produce a blocked-start status.");

                HashSet<string> dealtIds = new HashSet<string>(bootstrap.Battle.EnemyState.Hand
                    .Concat(bootstrap.Battle.EnemyState.DrawPile)
                    .Select(c => c.Id));
                CollectionAssert.AreEquivalent(stage.enemyDeckCardIds, dealtIds,
                    $"Stage {stageId} must deal exactly its own data-defined enemy deck.");
                dealtByStage[stageId] = dealtIds;
            }

            CollectionAssert.IsEmpty(dealtByStage["1-1"].Intersect(dealtByStage["1-2"]), "Stage 1-1 and 1-2 enemy decks must be distinct compositions.");
            CollectionAssert.IsEmpty(dealtByStage["1-1"].Intersect(dealtByStage["1-3"]), "Stage 1-1 and 1-3 enemy decks must be distinct compositions.");
            CollectionAssert.IsEmpty(dealtByStage["1-2"].Intersect(dealtByStage["1-3"]), "Stage 1-2 and 1-3 enemy decks must be distinct compositions.");
        }

        [Test]
        public void NormalToBattle_IgnoresAnyLeftoverCampaignConfiguration()
        {
            var databaseGo = new GameObject("CampaignBattle_CardDatabase_Normal");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignBattle_NormalBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap.Battle);

            // Simulate a leftover campaign stage from an earlier session/battle.
            CampaignStageData leftoverStage = CampaignMapPresenter.GetStageForTests("1-3");
            bootstrap.SetPendingCampaignStageForNextMatch(leftoverStage);

            // The ordinary "To Battle" tile's own real handler - requirement 4: unchanged, still
            // called with no stage argument, and must clear/ignore the leftover config.
            home.OnToBattleClickedForTests();

            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "A valid saved deck must still enter normal Battle.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "A normal match must not be blocked.");

            HashSet<string> dealtIds = new HashSet<string>(bootstrap.Battle.EnemyState.Hand
                .Concat(bootstrap.Battle.EnemyState.DrawPile)
                .Select(c => c.Id));
            CollectionAssert.AreNotEquivalent(leftoverStage.enemyDeckCardIds, dealtIds,
                "The ordinary 'To Battle' path must never deal a leftover campaign stage's configured enemy deck.");
        }

        [Test]
        public void Tutorial_IgnoresCampaignConfiguration_RegardlessOfPendingStage()
        {
            var databaseGo = new GameObject("CampaignBattle_CardDatabase_Tutorial");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignBattle_TutorialBootstrap");

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-2");
            bootstrap.SetPendingCampaignStageForNextMatch(stage);

            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SetBattleCanvasVisible(true);

            HashSet<string> tutorialDealtIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand
                .Concat(bootstrap.Battle.PlayerState.DrawPile)
                .Select(c => c.Id));
            CollectionAssert.AreEquivalent(new[] { "warrior", "novice_knight", "goblin_caster" }, tutorialDealtIds,
                "Tutorial must keep its fixed scripted starter formation regardless of any pending campaign stage.");

            HashSet<string> tutorialEnemyIds = new HashSet<string>(bootstrap.Battle.EnemyState.Hand
                .Concat(bootstrap.Battle.EnemyState.DrawPile)
                .Select(c => c.Id));
            CollectionAssert.AreNotEquivalent(stage.enemyDeckCardIds, tutorialEnemyIds,
                "Tutorial's enemy roster must never be replaced by a pending campaign stage's configured deck.");
        }

        [Test]
        public void InvalidCampaignStageBattleConfig_CannotLaunch()
        {
            var databaseGo = new GameObject("CampaignBattle_CardDatabase_Invalid");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignBattle_InvalidBootstrap");

            var missingConfigStage = new CampaignStageData("x-missing", "Unconfigured Stage", "Nobody", "UI/Portraits/Paladin", "No battle configuration set.", 0, 0);
            var unresolvableIdStage = new CampaignStageData("x-badid", "Bad Id Stage", "Nobody", "UI/Portraits/Paladin", "Contains an id CardDatabase cannot resolve.", 0, 0,
                enemyDeckCardIds: new[] { "warrior", "not_a_real_card_id", "fox", "bunny", "forest", "cleric", "archer_elf", "tribal_warrior", "undead_soldier", "griffin" });
            var duplicateIdStage = new CampaignStageData("x-dup", "Duplicate Id Stage", "Nobody", "UI/Portraits/Paladin", "Contains a duplicate id.", 0, 0,
                enemyDeckCardIds: new[] { "warrior", "warrior", "fox", "bunny", "forest", "cleric", "archer_elf", "tribal_warrior", "undead_soldier", "griffin" });

            Assert.IsFalse(bootstrap.IsCampaignStageBattleConfigValid(missingConfigStage), "A stage with no enemy-deck configuration must be invalid.");
            Assert.IsFalse(bootstrap.IsCampaignStageBattleConfigValid(unresolvableIdStage), "A stage referencing an id CardDatabase cannot resolve must be invalid.");
            Assert.IsFalse(bootstrap.IsCampaignStageBattleConfigValid(duplicateIdStage), "A stage with a duplicate id in its configured deck must be invalid.");

            // Defense-in-depth backstop inside the existing Battle startup path itself: even if a
            // caller bypassed the pre-launch check above, the match must still block safely rather
            // than silently falling back to a randomly generated enemy deck.
            bootstrap.SetPendingCampaignStageForNextMatch(unresolvableIdStage);
            bootstrap.SetBattleCanvasVisible(false);
            bootstrap.SetBattleCanvasVisible(true);

            Assert.IsNotNull(bootstrap.NormalMatchStatusForTests, "An invalid campaign stage configuration must block the match with a clear status, never a silent fallback deck.");
            Assert.AreEqual(0, bootstrap.Battle.PlayerState.Hand.Count + bootstrap.Battle.PlayerState.DrawPile.Count,
                "A blocked campaign match must deal no player hand at all - the whole match is blocked, not just the enemy side.");
        }

        [Test]
        public void ProgressionRewardAndUnlock_RemainCorrect_ForACampaignConfiguredStageMatch()
        {
            var databaseGo = new GameObject("CampaignBattle_CardDatabase_Progression");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignBattle_ProgressionBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap.Battle);

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.SetActiveStageForTests(stage);
            bootstrap.SetPendingCampaignStageForNextMatch(stage);
            bootstrap.SetBattleCanvasVisible(false);
            bootstrap.SetBattleCanvasVisible(true);
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "Setup: expected a valid, unblocked campaign match.");

            PlayerProfile profile = SaveManager.SaveData;
            int goldBefore = profile.gold;
            int gemsBefore = profile.gems;

            bool? isVictory = null;
            bootstrap.Battle.OnMatchCompleted += result => isVictory = result.IsVictory;
            PlayOneCardAndWin(bootstrap.Battle);

            Assert.IsTrue(isVictory, "Setup: expected the undefended enemy to produce a player victory.");
            Assert.AreEqual(goldBefore + stage.goldReward, profile.gold, "A campaign-configured stage win must still grant exactly its configured gold reward.");
            Assert.AreEqual(gemsBefore + stage.gemReward, profile.gems, "A campaign-configured stage win must still grant exactly its configured gem reward.");
            CollectionAssert.Contains(profile.claimedStageRewardIds, stage.stageId, "The launched stage id must still be recorded as claimed.");
            CollectionAssert.Contains(profile.unlockedStageIds, stage.stageId, "The cleared stage itself must still remain (or become) unlocked.");
        }
    }
}
