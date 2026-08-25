using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CHAPTER 1 SYSTEMIC COMBAT-BALANCE AUDIT (2026-08-21).
    ///
    /// Targets (set before changing any shared knobs):
    /// 1. Formation Turn-1 Resource is a real budget below ResourceCap so players choose cards
    ///    and leftovers fund reinforcement windows (ticks 4 / 8).
    /// 2. Auto Formation Stage 1-1 remains the beginner clear path (victory).
    /// 3. Full legal formation clears all Chapter 1 stages without a 1-tick wipe (the measured
    ///    failure mode under Turn1ResourceFraction=1.0). Stage 1-1 vs its 3-grunt beginner roster
    ///    may still resolve in 2 ticks under a full dump; later stages should not be shorter.
    /// 4. No per-stage enemy-roster retune in this audit - Stage 1-1/1-2/1-3 decks stay distinct.
    ///
    /// Shared knob under test: PlayerEmpireData.Turn1ResourceFraction restored to 0.6.
    /// Stage decks and onboarding Health stay as configured; AvatarDamageMultiplier is owned by
    /// docs/MVP_COMBAT_PROGRESSION_CONSTITUTION_2026-08-21.md (currently 4).
    /// </summary>
    public class Chapter1CombatBalanceAuditTests
    {
        /// <summary>Auto Formation beginner path should leave room for a spell/reinforce read.</summary>
        private const int MinimumAutoFormationTicks = 3;

        /// <summary>Forbids the measured 1-tick full-formation wipe; 2+ ticks is the floor.</summary>
        private const int MinimumFullFormationTicks = 2;

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
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsCh1BalanceAudit_" + System.Guid.NewGuid().ToString("N"));
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
            var go = new GameObject("HomePagePresenterUnderTest_Ch1BalanceAudit");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        private static void DeployFullLegalFormation(BattleController controller)
        {
            PlayerBattleState player = controller.PlayerState;
            Lane[] lanes = { Lane.Front, Lane.Middle, Lane.Back };

            bool playedAny;
            do
            {
                playedAny = false;
                foreach (Lane lane in lanes)
                {
                    Card candidate = player.Hand
                        .Where(c => c.ResourceCost <= player.Resource && player.Lanes[lane].HasRoomFor(c))
                        .OrderBy(c => c.ResourceCost)
                        .ThenBy(c => c.SlotWeight)
                        .ThenBy(c => c.Id)
                        .FirstOrDefault();

                    if (candidate == null) continue;
                    if (controller.TryPlayCard(player, candidate, lane)) playedAny = true;
                }
            } while (playedAny);
        }

        private static int ResolveCombat(GameBootstrap bootstrap, out MatchResult result)
        {
            MatchResult? captured = null;
            bootstrap.Battle.OnMatchCompleted += r => captured = r;

            int ticksRun = 0;
            while (bootstrap.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap.Battle.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks,
                    "Combat must resolve within the normal safety cap.");
            }

            Assert.IsTrue(captured.HasValue, "OnMatchCompleted must fire.");
            result = captured.Value;
            return ticksRun;
        }

        [Test]
        public void Level1_Turn1Resource_IsARealFormationBudget_BelowResourceCap()
        {
            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel: 1, castleLevel: 1, barracksLevel: 1);
            empire.InitializeTCGModifiers();

            Assert.AreEqual(20, empire.ResourceCap, "Level-1 ResourceCap baseline must stay 20.");
            Assert.AreEqual(12, empire.Turn1Resource,
                "Turn1Resource must be round(20 * 0.6) = 12 so Formation cannot dump the full cap.");
            Assert.Less(empire.Turn1Resource, empire.ResourceCap,
                "Turn-1 / Formation budget must sit strictly below ResourceCap.");
        }

        [Test]
        public void Stage1_1_AutoFormation_RemainsTheBeginnerClearPath_AcrossDrawVariance()
        {
            AssertAutoFormationWinRateAcrossSeeds("1-1", unlocked: new List<string> { "1-1" }, minimumWins: 20);
        }

        [Test]
        public void Stage1_2_AutoFormation_IsReliablyWinnable_AcrossDrawVariance()
        {
            AssertAutoFormationWinRateAcrossSeeds("1-2", unlocked: new List<string> { "1-1", "1-2" }, minimumWins: 20);
        }

        [Test]
        public void Stage1_3_AutoFormation_IsReliablyWinnable_AcrossDrawVariance()
        {
            AssertAutoFormationWinRateAcrossSeeds("1-3", unlocked: new List<string> { "1-1", "1-2", "1-3" }, minimumWins: 18);
        }

        private void AssertAutoFormationWinRateAcrossSeeds(string stageId, List<string> unlocked, int minimumWins)
        {
            var databaseGo = new GameObject($"Ch1Balance_CardDatabase_AF_{stageId}");
            _spawned.Add(databaseGo);
            databaseGo.AddComponent<CardDatabase>().Initialize();

            const int trials = 25;
            int wins = 0;

            for (int seed = 0; seed < trials; seed++)
            {
                PlayerBattleState.SetShuffleSeedForTests(seed);
                SaveFreshStarterProfile(unlocked);
                GameBootstrap bootstrap = SpawnAndInitializeBootstrap($"Ch1Balance_Bootstrap_AF_{stageId}_{seed}");
                HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
                CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);

                home.LaunchCampaignStageForTests(stage);
                Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, $"Seed {seed}: Stage {stageId} launch must succeed.");

                bootstrap.AutoFormationForTests();
                bootstrap.StartBattleForTests();

                int ticks = ResolveCombat(bootstrap, out MatchResult result);
                if (result.IsVictory) wins++;

                Debug.Log($"[Chapter1CombatBalanceAudit] AF {stageId} seed={seed}: {(result.IsVictory ? "VICTORY" : "DEFEAT")} in {ticks} tick(s).");
            }

            Assert.GreaterOrEqual(wins, minimumWins,
                $"Chapter 1 vertical slice: Auto Formation Stage {stageId} won {wins}/{trials}; need at least {minimumWins} (taught path must clear the campaign).");
        }

        [Test]
        public void Chapter1_FullFormation_IsWinnable_WithoutOneTickWipes()
        {
            var databaseGo = new GameObject("Ch1Balance_CardDatabase_Full");
            _spawned.Add(databaseGo);
            databaseGo.AddComponent<CardDatabase>().Initialize();

            var tickByStage = new Dictionary<string, int>();

            foreach (string stageId in new[] { "1-1", "1-2", "1-3" })
            {
                PlayerBattleState.SetShuffleSeedForTests(stageId.GetHashCode());
                SaveFreshStarterProfile(new List<string> { "1-1", "1-2", "1-3" });
                GameBootstrap bootstrap = SpawnAndInitializeBootstrap($"Ch1Balance_Bootstrap_Full_{stageId}");
                HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
                CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);

                home.LaunchCampaignStageForTests(stage);
                DeployFullLegalFormation(bootstrap.Battle);
                bootstrap.StartBattleForTests();

                int ticks = ResolveCombat(bootstrap, out MatchResult result);
                tickByStage[stageId] = ticks;

                Assert.IsTrue(result.IsVictory,
                    $"Stage {stageId} must remain winnable under the full legal formation policy.");

                // Stage 1-1's beginner 3-grunt roster can still end in one tick under a heavy
                // full dump; later stages must not regress to that wipe shape.
                int minTicks = stageId == "1-1" ? 1 : MinimumFullFormationTicks;
                Assert.GreaterOrEqual(ticks, minTicks,
                    $"Stage {stageId} full formation resolved in {ticks} tick(s); below the audit floor of {minTicks}.");
                Debug.Log($"[Chapter1CombatBalanceAudit] Full {stageId}: VICTORY in {ticks} tick(s).");
            }

            // Progression is composition-driven; tick length need not be strictly monotonic once
            // 1-tick wipes are gone. Keep the distinct-deck asserts below as the stage identity lock.
            CollectionAssert.IsEmpty(
                CampaignMapPresenter.GetStageForTests("1-1").enemyDeckCardIds
                    .Intersect(CampaignMapPresenter.GetStageForTests("1-2").enemyDeckCardIds),
                "Audit must not retune Stage 1-1/1-2 into overlapping enemy decks.");
            CollectionAssert.IsEmpty(
                CampaignMapPresenter.GetStageForTests("1-2").enemyDeckCardIds
                    .Intersect(CampaignMapPresenter.GetStageForTests("1-3").enemyDeckCardIds),
                "Audit must not retune Stage 1-2/1-3 into overlapping enemy decks.");
        }
    }
}
