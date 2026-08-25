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
    /// Campaign AF + mirrored AI spells ON smoke (Option B).
    /// Asserts production launch enables spells and the match resolves (no softlock).
    /// Does not weaken AISpellCaster; does not touch Chapter1CampaignPlayabilityTests
    /// (AF taught-path spells-off) or BalanceSimulation AI-off baselines.
    /// </summary>
    public class CampaignAfMirroredAiSpellSmokeTests
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
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsAfAiSpellSmoke_" + System.Guid.NewGuid().ToString("N"));
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

        [Test]
        public void Stage1_1_ProductionLaunch_AutoFormation_WithMirroredAiSpellsOn_Resolves()
        {
            RunAfMirroredSpellSmoke(
                stageId: "1-1",
                unlocked: null,
                shuffleSeed: 11,
                assertVictory: false);
        }

        [Test]
        public void Stage1_2_ProductionLaunch_AutoFormation_WithMirroredAiSpellsOn_Resolves()
        {
            // Resolve-only (Block O): AF+AI-on seed scan 0–39 = 0/40 wins. Taught-path AF wins
            // stay spells-off in Chapter1CampaignPlayabilityTests. Winnability confidence:
            // CampaignAfMirroredAiSpellWinnabilityTests (rate log + optional Victory when seeded).
            RunAfMirroredSpellSmoke(
                stageId: "1-2",
                unlocked: new List<string> { "1-1", "1-2" },
                shuffleSeed: 11,
                assertVictory: false);
        }

        [Test]
        public void Stage1_3_ProductionLaunch_AutoFormation_WithMirroredAiSpellsOn_Resolves()
        {
            // Resolve-only smoke kept; Victory under seed 11 is asserted in
            // CampaignAfMirroredAiSpellWinnabilityTests (Block O: 4/40 wins including seed 11).
            RunAfMirroredSpellSmoke(
                stageId: "1-3",
                unlocked: new List<string> { "1-1", "1-2", "1-3" },
                shuffleSeed: 11,
                assertVictory: false);
        }

        private void RunAfMirroredSpellSmoke(
            string stageId, List<string> unlocked, int shuffleSeed, bool assertVictory)
        {
            var databaseGo = new GameObject($"AfAiSpellSmoke_CardDatabase_{stageId}");
            _spawned.Add(databaseGo);
            databaseGo.AddComponent<CardDatabase>().Initialize();

            PlayerBattleState.SetShuffleSeedForTests(shuffleSeed);
            SaveFreshStarterProfile(unlocked);

            GameBootstrap bootstrap = SpawnBootstrap($"AfAiSpellSmoke_Bootstrap_{stageId}");
            HomePagePresenter home = SpawnHome(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
            Assert.NotNull(stage, $"Setup: expected Stage {stageId}.");

            home.LaunchCampaignStageForTests(stage);
            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests);
            Assert.IsTrue(bootstrap.Battle.MirroredEnemySpellsEnabled,
                $"Campaign StartNewMatch must enable mirrored PvE AI spells (Option B) for Stage {stageId}.");

            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();
            Assert.AreEqual(BattlePhase.Combat, bootstrap.Battle.Phase);

            MatchResult? result = null;
            bootstrap.Battle.OnMatchCompleted += r => result = r;

            int ticksRun = 0;
            while (bootstrap.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap.Battle.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks,
                    $"Stage {stageId} with mirrored AI spells must not softlock past the combat tick cap.");
            }

            Assert.AreEqual(BattlePhase.Resolved, bootstrap.Battle.Phase);
            Assert.IsTrue(result.HasValue, $"OnMatchCompleted must fire for Stage {stageId} — no softlock.");

            if (assertVictory)
            {
                Assert.IsTrue(result.Value.IsVictory,
                    $"Stage {stageId} AF + AI spells ON must be a player victory under seed {shuffleSeed}.");
            }

            Debug.Log($"[AfAiSpellSmoke] {stageId} {(result.Value.IsVictory ? "VICTORY" : "DEFEAT")} in {ticksRun} ticks; " +
                      $"enemyCasts={bootstrap.Battle.SpellCastLog.Count(c => !c.CastByPlayer)}");
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
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();
        }

        private GameBootstrap SpawnBootstrap(string name)
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

        private HomePagePresenter SpawnHome(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenter_AfAiSpellSmoke");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BindBattleControllerForTests(bootstrap.Battle);
            home.BindGameBootstrapForTests(bootstrap);
            return home;
        }
    }
}
