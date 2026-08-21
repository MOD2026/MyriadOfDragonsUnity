using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CHAPTER 1 CAMPAIGN PLAYABILITY VALIDATION (release feature) - proves each Chapter 1 stage
    /// through a single, deterministic, entirely-production combat policy: a fresh profile with
    /// exactly the approved starter collection as its confirmed deck, launched via the real
    /// Campaign stamina-entry gate (HomePagePresenter.LaunchCampaignStageForTests), formed up via
    /// the real Auto Formation handler (GameBootstrap.AutoFormationForTests), started via the
    /// real Start Battle handler (GameBootstrap.StartBattleForTests - which deploys the enemy
    /// through the real SimpleAIOpponent.TakeTurn, not a test-authored placement), then resolved
    /// tick-by-tick through the real AdvanceCombatTick(). No health is edited, no victory is
    /// forced, and the enemy is never left deliberately undeployed - every actor on both sides of
    /// the board is placed by real production code.
    /// </summary>
    public class Chapter1CampaignPlayabilityTests
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
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsCampaignPlayability_" + System.Guid.NewGuid().ToString("N"));
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

        /// <summary>A fresh player who has only ever received the approved starter collection -
        /// owns exactly these ten cards, and has confirmed exactly these ten as their deck (the
        /// only legal size at level 1). Deliberately not built from CardDatabase.AllCards - these
        /// are the real, fixed ids GameBootstrap.GrantApprovedStarterCardsIfMissing grants.</summary>
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
            var go = new GameObject("HomePagePresenterUnderTest_CampaignPlayability");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        /// <summary>The one deterministic, entirely-production combat policy every stage in this
        /// file is validated against: real Campaign launch (stage config + unlock + deck +
        /// Stamina gate), real Auto Formation for the player, real Start Battle (which deploys
        /// the real enemy AI), real tick-by-tick resolution. Returns the real MatchResult.</summary>
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
                    $"Requirement 3: Stage {stage.stageId} must resolve within the normal combat safety limit, never deadlock.");
            }

            Assert.IsTrue(result.HasValue, $"Requirement 3: Stage {stage.stageId} must actually resolve (OnMatchCompleted must fire).");
            Debug.Log($"[Chapter1CampaignPlayabilityTests] Stage {stage.stageId}: {(result.Value.IsVictory ? "VICTORY" : "DEFEAT")} in {ticksRun} tick(s).");
            return result.Value;
        }

        [Test]
        public void Stage1_1_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation()
        {
            var databaseGo = new GameObject("CampaignPlayability_CardDatabase_1_1");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            // Pin draw order so this contract asserts the beginner path, not shuffle luck.
            PlayerBattleState.SetShuffleSeedForTests(11);
            SaveFreshStarterProfile();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignPlayability_Bootstrap_1_1");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");

            MatchResult result = RunDeterministicPolicy(bootstrap, home, stage);

            Assert.IsTrue(result.IsVictory,
                "Requirement 1: Stage 1-1 must be winnable by a fresh player using the approved starter collection, a valid saved deck, Auto Formation, and this deterministic legal combat policy.");
        }

        [Test]
        public void Stage1_2_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation()
        {
            var databaseGo = new GameObject("CampaignPlayability_CardDatabase_1_2");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            SaveFreshStarterProfile(unlockedStages: new List<string> { "1-1", "1-2" });
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignPlayability_Bootstrap_1_2");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-2");

            MatchResult result = RunDeterministicPolicy(bootstrap, home, stage);

            Assert.IsTrue(result.IsVictory,
                "Chapter 1 vertical slice: Stage 1-2 must be winnable with starter deck + Auto Formation (the taught path).");
        }

        [Test]
        public void Stage1_3_IsWinnable_ByAFreshPlayer_UsingApprovedStarterCollection_AndAutoFormation()
        {
            var databaseGo = new GameObject("CampaignPlayability_CardDatabase_1_3");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            SaveFreshStarterProfile(unlockedStages: new List<string> { "1-1", "1-2", "1-3" });
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignPlayability_Bootstrap_1_3");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-3");

            MatchResult result = RunDeterministicPolicy(bootstrap, home, stage);

            Assert.IsTrue(result.IsVictory,
                "Chapter 1 vertical slice: Stage 1-3 must be winnable with starter deck + Auto Formation (the taught path).");
        }

        [Test]
        public void Stage1_2_And1_3_RemainDistinctConfiguredEncounters()
        {
            CampaignStageData stage1_2 = CampaignMapPresenter.GetStageForTests("1-2");
            CampaignStageData stage1_3 = CampaignMapPresenter.GetStageForTests("1-3");

            CollectionAssert.IsEmpty(
                stage1_2.enemyDeckCardIds.Intersect(stage1_3.enemyDeckCardIds),
                "Requirement 4: Stage 1-2 and Stage 1-3 must remain distinct configured encounters (no shared enemy card ids).");
        }
    }
}
