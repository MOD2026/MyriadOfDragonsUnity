using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Block O — Ch1 1-2 / 1-3 AF + mirrored AI spells ON winnability confidence.
    /// Prefer fixed-seed IsVictory when stable; otherwise resolve-only smokes stay in
    /// CampaignAfMirroredAiSpellSmokeTests and this fixture logs win rates (no flaky asserts).
    /// Does not touch Chapter1CampaignPlayabilityTests (spells-off taught path).
    /// </summary>
    public class CampaignAfMirroredAiSpellWinnabilityTests
    {
        /// <summary>
        /// Seed scan (Block O, 2026-08-23): seeds 0–39 for rate docs; Victory seed when stable.
        /// Stage 1-2: 0/40 AF+AI-on wins in that band → resolve-only (no Victory assert).
        /// Stage 1-3: wins at 9,11,32,34 → fixed Victory seed 11.
        /// </summary>
        public const int Stage1_2_AfAiOnVictorySeed = -1;
        public const int Stage1_3_AfAiOnVictorySeed = 11;

        private const int SeedScanCount = 40;

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
                "MyriadOfDragonsAfAiWin_" + System.Guid.NewGuid().ToString("N"));
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
        public void Stage1_2_AfAiOn_WinRateOverSeeds_Documented_NoFlakyAssert()
        {
            DocumentWinRate("1-2", new List<string> { "1-1", "1-2" }, Stage1_2_AfAiOnVictorySeed);
        }

        [Test]
        public void Stage1_3_AfAiOn_WinRateOverSeeds_Documented_NoFlakyAssert()
        {
            DocumentWinRate("1-3", new List<string> { "1-1", "1-2", "1-3" }, Stage1_3_AfAiOnVictorySeed);
        }

        [Test]
        public void Stage1_2_AfAiOn_FixedSeedVictory_WhenStableSeedConfigured()
        {
            if (Stage1_2_AfAiOnVictorySeed < 0)
            {
                Assert.Pass(
                    "No stable AF+AI-on Victory seed for 1-2 in Block O scan (seeds 0–39). " +
                    "Resolve-only remains CampaignAfMirroredAiSpellSmokeTests.Stage1_2_…_Resolves.");
                return;
            }

            MatchResult result = RunSingleAfAiOnMatch(
                "1-2", new List<string> { "1-1", "1-2" }, Stage1_2_AfAiOnVictorySeed);
            Assert.IsTrue(result.IsVictory,
                $"Stage 1-2 AF+AI-on must win under fixed seed {Stage1_2_AfAiOnVictorySeed}.");
        }

        [Test]
        public void Stage1_3_AfAiOn_FixedSeedVictory_WhenStableSeedConfigured()
        {
            if (Stage1_3_AfAiOnVictorySeed < 0)
            {
                Assert.Pass(
                    "No stable AF+AI-on Victory seed for 1-3 in Block O scan (seeds 0–39). " +
                    "Resolve-only remains CampaignAfMirroredAiSpellSmokeTests.Stage1_3_…_Resolves.");
                return;
            }

            MatchResult result = RunSingleAfAiOnMatch(
                "1-3", new List<string> { "1-1", "1-2", "1-3" }, Stage1_3_AfAiOnVictorySeed);
            Assert.IsTrue(result.IsVictory,
                $"Stage 1-3 AF+AI-on must win under fixed seed {Stage1_3_AfAiOnVictorySeed}.");
        }

        /// <summary>
        /// Block Q — root-cause probe for Stage 1-2 AF+AI-on 0/40 (Block O).
        /// Standing Review structural facts: 1-2 is the only Ch1 opener with uniform class
        /// (warrior×3) and uniform stats (3/2×3); raw ATK/HP totals alone do not explain the
        /// win-rate gap vs 1-3. Probe below: AI casts fire, 1-2 AI-off still wins (taught path),
        /// 1-3 AI-on still wins at seed 11 → no softlock/wrong-target bug. Verdict: BALANCE Soft
        /// (Option B + 1-2 roster shape). Keep Assert.Pass rate docs; do not force IsVictory.
        /// CC later: Stage2EnemyDeck retune and/or systemic same-class roster scan — not Block Q.
        /// </summary>
        [Test]
        public void Stage1_2_AfAiOn_RootCauseDiagnostics_DocumentsBalanceNotBug()
        {
            var databaseGo = new GameObject("AfAiWin_CardDatabase_RootCause");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            CampaignStageData stage1_1 = CampaignMapPresenter.GetStageForTests("1-1");
            CampaignStageData stage1_2 = CampaignMapPresenter.GetStageForTests("1-2");
            CampaignStageData stage1_3 = CampaignMapPresenter.GetStageForTests("1-3");
            Assert.NotNull(stage1_1);
            Assert.NotNull(stage1_2);
            Assert.NotNull(stage1_3);

            CollectionAssert.AreEquivalent(
                new[] { "giant_worms", "mountain_harpy", "snake_archer" },
                stage1_1.enemyDeckCardIds);
            CollectionAssert.AreEquivalent(
                new[] { "fire_worm", "butcher", "cursed_soldier" },
                stage1_2.enemyDeckCardIds);
            CollectionAssert.AreEquivalent(
                new[] { "ogre", "werewolf", "wood_wizard" },
                stage1_3.enemyDeckCardIds);
            CollectionAssert.IsEmpty(stage1_1.enemyDeckCardIds.Intersect(stage1_2.enemyDeckCardIds));
            CollectionAssert.IsEmpty(stage1_2.enemyDeckCardIds.Intersect(stage1_3.enemyDeckCardIds));

            // Standing Review Task 1 — structural uniqueness of 1-2 among Ch1 openers.
            Card[] cards12 = stage1_2.enemyDeckCardIds.Select(id => database.GetCard(id)).ToArray();
            Assert.IsTrue(cards12.All(c => c != null));
            Assert.IsTrue(cards12.All(c => c.Class == CardClass.Warrior),
                "1-2 is the only opener with warrior×3 (class-uniform roster).");
            Assert.IsTrue(cards12.All(c => c.Attack == 3 && c.Health == 2),
                "1-2 is the only opener with identical 3/2 stats on all three cards.");
            Card[] cards11 = stage1_1.enemyDeckCardIds.Select(id => database.GetCard(id)).ToArray();
            Card[] cards13 = stage1_3.enemyDeckCardIds.Select(id => database.GetCard(id)).ToArray();
            Assert.AreNotEqual(1, cards11.Select(c => c.Class).Distinct().Count(),
                "Setup: 1-1 must not be a single-class trio (contrast with 1-2).");
            Assert.Greater(cards13.Select(c => c.Class).Distinct().Count(), 1,
                "Setup: 1-3 must not be fully class-uniform (contrast).");

            SynergyBonus syn12 = FormationSynergy.Calculate(cards12);
            SynergyBonus syn13 = FormationSynergy.Calculate(cards13);
            // Warriors split by Element for tags: 1-2 = VenomStrike×1 + DivineHeal×2 → pair +1 ATK only.
            Debug.Log($"[AfAiRootCause] Enemy FormationSynergy 1-2={FormationSynergy.Describe(syn12)} " +
                      $"(atk={syn12.AttackBonus},hp={syn12.HealthBonus}) " +
                      $"1-3={FormationSynergy.Describe(syn13)} (atk={syn13.AttackBonus},hp={syn13.HealthBonus})");
            Assert.AreEqual(1, syn12.AttackBonus,
                "1-2 class-uniform warriors are not tag-uniform: Andras pair gives +1 ATK, not a trio.");
            Assert.AreEqual(0, syn12.HealthBonus);

            int atk1 = SumEnemyDeckAttack(database, stage1_1);
            int atk2 = SumEnemyDeckAttack(database, stage1_2);
            int atk3 = SumEnemyDeckAttack(database, stage1_3);
            int hp1 = SumEnemyDeckHealth(database, stage1_1);
            int hp2 = SumEnemyDeckHealth(database, stage1_2);
            int hp3 = SumEnemyDeckHealth(database, stage1_3);
            Assert.AreEqual(5, atk1);
            Assert.AreEqual(9, atk2);
            Assert.AreEqual(11, atk3);
            Assert.AreEqual(3, hp1);
            Assert.AreEqual(6, hp2);
            Assert.AreEqual(10, hp3);
            Debug.Log($"[AfAiRootCause] Roster ATK sums 1-1={atk1} 1-2={atk2} 1-3={atk3}; HP sums 1-1={hp1} 1-2={hp2} 1-3={hp3}");

            const int probeSeed = 11;
            AfAiMatchProbe p11On = RunProbe("1-1", new List<string> { "1-1" }, probeSeed, aiSpellsOn: true);
            AfAiMatchProbe p12On = RunProbe("1-2", new List<string> { "1-1", "1-2" }, probeSeed, aiSpellsOn: true);
            AfAiMatchProbe p13On = RunProbe("1-3", new List<string> { "1-1", "1-2", "1-3" }, probeSeed, aiSpellsOn: true);
            AfAiMatchProbe p12Off = RunProbe("1-2", new List<string> { "1-1", "1-2" }, probeSeed, aiSpellsOn: false);

            LogProbe("1-1 AI-on", p11On);
            LogProbe("1-2 AI-on", p12On);
            LogProbe("1-3 AI-on", p13On);
            LogProbe("1-2 AI-off", p12Off);

            Assert.Greater(p12On.Ticks, 0);
            Assert.Greater(p12On.EnemyCastCount, 0,
                "1-2 AF+AI-on must cast (rules out silent AI-off / softlock residual).");
            // AI Spell Cast Probability Gate (LOCKED 2026-08-24): the "0/40 in that band" claim
            // below was measured against an AI that cast on ~100% of legal opportunities. With the
            // gate now at 40%, seed 11 (pinned via SetAiSpellCastRngSeedForTests in RunProbe, so
            // this is a real reproducible re-measurement, not noise) flips to a WIN (35% vs 4% HP
            // on the tick cap - still a genuinely tight, tick-capped finish, not a blowout). Kept
            // as a logged observation rather than a new hardcoded Assert.IsTrue: one seed flipping
            // doesn't by itself re-baseline the whole 0-39 band the original Block O scan covered -
            // that needs a fresh seed scan (real follow-up, not done here) before this file's own
            // "0/40" language can be replaced with a new fixed number.
            Debug.Log($"[AfAiRootCause] Seed 11: 1-2 AF+AI-on is now victory={p12On.Result.IsVictory} " +
                      "post-probability-gate (was a documented defeat pre-gate) - flagged for a fresh Block O seed-scan re-baseline, not asserted here.");
            Assert.IsTrue(p12Off.Result.IsVictory,
                "Seed 11: 1-2 AF spells-off still wins — taught path intact; gap is Option B interaction.");
            Assert.IsTrue(p13On.Result.IsVictory,
                "Seed 11: 1-3 AF+AI-on wins — not a global AI softlock.");

            foreach (int seed in new[] { 0, 7, 22 })
            {
                AfAiMatchProbe probe = RunProbe("1-2", new List<string> { "1-1", "1-2" }, seed, aiSpellsOn: true);
                LogProbe($"1-2 AI-on seed={seed}", probe);
                // Same reasoning as seed 11 above - logged, not asserted, pending a fresh seed scan.
                Debug.Log($"[AfAiRootCause] Seed {seed}: 1-2 AF+AI-on is now victory={probe.Result.IsVictory} post-probability-gate.");
                Assert.Greater(probe.EnemyCastCount, 0);
                // KO paths leave MatchResult.OutcomeReason empty by design; do not assert it.
            }

            Assert.Pass(
                "Block Q verdict: BALANCE Soft (Option B + 1-2 uniform warrior×3 / 3-2×3 roster), not production bug - " +
                "AI Spell Cast Probability Gate (2026-08-24) has since changed this fixture's own measured seed-11 outcome; " +
                "see the logged observations above and re-baseline with a fresh seed scan before trusting the old 0/40 figure. " +
                $"Synergy 1-2={FormationSynergy.Describe(syn12)} 1-3={FormationSynergy.Describe(syn13)}. " +
                $"Seed11 1-2 AI-on: casts={p12On.EnemyCastCount} [{p12On.EnemyCastSummary}] " +
                $"HP p={p12On.PlayerHpAfter}/e={p12On.EnemyHpAfter} ticks={p12On.Ticks}. " +
                "Standing Review: keep Assert.Pass rate docs; systemic same-class roster scan ahead of Block P if CC confirms pattern risk.");
        }

        private struct AfAiMatchProbe
        {
            public MatchResult Result;
            public int Ticks;
            public int PlayerHpAfter;
            public int EnemyHpAfter;
            public int EnemyCastCount;
            public string EnemyCastSummary;
        }

        private static void LogProbe(string label, AfAiMatchProbe probe)
        {
            Debug.Log($"[AfAiRootCause] {label}: victory={probe.Result.IsVictory} ticks={probe.Ticks} " +
                      $"playerHp={probe.PlayerHpAfter} enemyHp={probe.EnemyHpAfter} " +
                      $"enemyCasts={probe.EnemyCastCount} [{probe.EnemyCastSummary}] " +
                      $"reason={probe.Result.OutcomeReason}");
        }

        private static int SumEnemyDeckAttack(CardDatabase database, CampaignStageData stage)
        {
            int sum = 0;
            foreach (string id in stage.enemyDeckCardIds)
            {
                Card card = database.GetCard(id);
                Assert.NotNull(card, $"Missing card id {id} for stage {stage.stageId}");
                sum += card.Attack;
            }
            return sum;
        }

        private static int SumEnemyDeckHealth(CardDatabase database, CampaignStageData stage)
        {
            int sum = 0;
            foreach (string id in stage.enemyDeckCardIds)
            {
                Card card = database.GetCard(id);
                Assert.NotNull(card, $"Missing card id {id} for stage {stage.stageId}");
                sum += card.Health;
            }
            return sum;
        }

        private AfAiMatchProbe RunProbe(string stageId, List<string> unlocked, int shuffleSeed, bool aiSpellsOn)
        {
            ClearSpawnedExceptDatabaseNamePrefix("AfAiWin_CardDatabase_RootCause");
            SaveSystem.ResetCurrentProfileForTests();
            PlayerBattleState.SetShuffleSeedForTests(shuffleSeed);
            SaveFreshStarterProfile(unlocked);

            GameBootstrap bootstrap = SpawnBootstrap($"AfAiWin_Probe_{stageId}_{shuffleSeed}_{(aiSpellsOn ? "on" : "off")}");
            HomePagePresenter home = SpawnHome(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
            Assert.NotNull(stage);

            home.LaunchCampaignStageForTests(stage);
            Assert.IsTrue(bootstrap.Battle.MirroredEnemySpellsEnabled,
                "Production launch enables AI spells before Soft toggle.");
            if (!aiSpellsOn)
                bootstrap.Battle.SetMirroredEnemySpellsEnabledForTests(false);

            // AI Spell Cast Probability Gate (LOCKED 2026-08-24): its own RNG stream is
            // independent of PlayerBattleState's shuffle-seed stream - without pinning it too,
            // this probe's outcome for a given shuffleSeed would be non-deterministic (production
            // StartMatch auto-generates a fresh Guid-based seed every call). Reusing shuffleSeed
            // here keeps this file's whole seed-scan methodology reproducible.
            bootstrap.Battle.SetAiSpellCastRngSeedForTests(shuffleSeed);

            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();

            MatchResult? result = null;
            bootstrap.Battle.OnMatchCompleted += r => result = r;

            int ticks = 0;
            while (bootstrap.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap.Battle.AdvanceCombatTick();
                ticks++;
                Assert.LessOrEqual(ticks, BattleController.MaxCombatTicks, "must not softlock");
            }

            Assert.IsTrue(result.HasValue, "must resolve");
            var enemyCasts = bootstrap.Battle.SpellCastLog.Where(c => !c.CastByPlayer).ToList();
            string castSummary = string.Join(", ",
                enemyCasts.Select(c => $"t{c.TickNumber}:{c.SpellName}@{c.TargetLane}+{c.AvatarDamageDealt}"));

            return new AfAiMatchProbe
            {
                Result = result.Value,
                Ticks = ticks,
                PlayerHpAfter = bootstrap.Battle.PlayerState.AvatarHealth,
                EnemyHpAfter = bootstrap.Battle.EnemyState.AvatarHealth,
                EnemyCastCount = enemyCasts.Count,
                EnemyCastSummary = castSummary,
            };
        }

        private void ClearSpawnedExceptDatabaseNamePrefix(string databaseName)
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                GameObject go = _spawned[i];
                if (go == null) { _spawned.RemoveAt(i); continue; }
                if (go.name == databaseName) continue;
                Object.DestroyImmediate(go);
                _spawned.RemoveAt(i);
            }
        }

        private void DocumentWinRate(string stageId, List<string> unlocked, int configuredVictorySeed)
        {
            var databaseGo = new GameObject($"AfAiWin_CardDatabase_{stageId}");
            _spawned.Add(databaseGo);
            databaseGo.AddComponent<CardDatabase>().Initialize();

            int wins = 0;
            var winningSeeds = new List<int>();
            var sb = new StringBuilder();
            sb.Append($"[AfAiWinConfidence] {stageId} AF+AI-on over seeds 0..{SeedScanCount - 1}: ");

            for (int seed = 0; seed < SeedScanCount; seed++)
            {
                // Fresh profile + bootstrap per seed (production launch path).
                ClearSpawnedExceptDatabase(databaseGo);
                SaveSystem.ResetCurrentProfileForTests();
                MatchResult result = RunSingleAfAiOnMatch(stageId, unlocked, seed, reuseDatabase: true);
                if (result.IsVictory)
                {
                    wins++;
                    winningSeeds.Add(seed);
                }
            }

            sb.Append($"{wins}/{SeedScanCount} wins");
            if (winningSeeds.Count > 0)
                sb.Append("; winningSeeds=[").Append(string.Join(",", winningSeeds)).Append(']');
            else
                sb.Append("; winningSeeds=[]");
            sb.Append("; configuredVictorySeed=").Append(configuredVictorySeed);
            Debug.Log(sb.ToString());

            // Document only — do not assert on rate (would flake / force balance retune).
            Assert.Pass(sb.ToString());
        }

        private void ClearSpawnedExceptDatabase(GameObject databaseGo)
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                GameObject go = _spawned[i];
                if (go == null || go == databaseGo) continue;
                Object.DestroyImmediate(go);
                _spawned.RemoveAt(i);
            }
        }

        private MatchResult RunSingleAfAiOnMatch(
            string stageId, List<string> unlocked, int shuffleSeed, bool reuseDatabase = false)
        {
            if (!reuseDatabase)
            {
                var databaseGo = new GameObject($"AfAiWin_CardDatabase_{stageId}_{shuffleSeed}");
                _spawned.Add(databaseGo);
                databaseGo.AddComponent<CardDatabase>().Initialize();
            }

            PlayerBattleState.SetShuffleSeedForTests(shuffleSeed);
            SaveFreshStarterProfile(unlocked);

            GameBootstrap bootstrap = SpawnBootstrap($"AfAiWin_Bootstrap_{stageId}_{shuffleSeed}");
            HomePagePresenter home = SpawnHome(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests(stageId);
            Assert.NotNull(stage);

            home.LaunchCampaignStageForTests(stage);
            Assert.IsTrue(bootstrap.Battle.MirroredEnemySpellsEnabled,
                $"Stage {stageId} must keep Option B AI spells ON.");

            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();

            MatchResult? result = null;
            bootstrap.Battle.OnMatchCompleted += r => result = r;

            int ticks = 0;
            while (bootstrap.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap.Battle.AdvanceCombatTick();
                ticks++;
                Assert.LessOrEqual(ticks, BattleController.MaxCombatTicks,
                    $"Stage {stageId} seed {shuffleSeed} must not softlock.");
            }

            Assert.IsTrue(result.HasValue, $"Stage {stageId} seed {shuffleSeed} must resolve.");
            return result.Value;
        }

        private static void SaveFreshStarterProfile(List<string> unlockedStages)
        {
            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(ApprovedStarterCollectionCardIds),
                activeDeckCardIds = new List<string>(ApprovedStarterCollectionCardIds),
                stamina = 100,
                unlockedStageIds = new List<string>(unlockedStages),
            };
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
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null && !_spawned.Contains(spawned)) _spawned.Add(spawned);
            }
            bootstrap.SetBattleCanvasVisible(false);
            return bootstrap;
        }

        private HomePagePresenter SpawnHome(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenter_AfAiWin");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BindBattleControllerForTests(bootstrap.Battle);
            home.BindGameBootstrapForTests(bootstrap);
            return home;
        }
    }
}
