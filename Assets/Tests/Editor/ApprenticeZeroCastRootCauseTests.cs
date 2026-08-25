using System;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Empire;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// AI-balance diagnose-before-tune, real root-cause protocol for the zero-cast tick-length
    /// finding (LOCKED 2026-08-25, GPT): (1) split zero-cast trials into no-opportunity-ever /
    /// opportunity-existed-but-every-roll-failed / match-ended-before-opportunity-could-appear;
    /// (2) a forced-no-cast control (decision loop runs, casting forcibly disabled, distinct from
    /// spells not being enabled at all) alongside baseline and the normal gate; (3) first-
    /// divergent-tick + state-hash comparison between baseline and forced-no-cast; (4) checked
    /// separately (BattleController.cs source read, not simulated) whether combat and spell
    /// decisions share one RNG stream - they do NOT: LaneBattleResolver/AISpellCaster/
    /// SimpleAIOpponent have zero RNG usage anywhere (combat resolution is fully deterministic -
    /// Attack/Health are fixed stats), and _aiSpellCastRng is its own dedicated System.Random
    /// instance, entirely separate from PlayerBattleState's own deck-shuffle RNG. No shared-stream
    /// desync risk exists architecturally - nothing to escalate for that specific concern.
    /// </summary>
    public class ApprenticeZeroCastRootCauseTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            LaneBattleResolver.ResetRulesToDefault();
            PlayerBattleState.ClearShuffleSeedForTests();
            foreach (GameObject go in _spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private CardDatabase LoadDatabase()
        {
            var go = new GameObject("RootCauseCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            db = CardDatabase.Instance;
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("RootCauseBattleController");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        private static void DeployWholeSquad(BattleController controller, PlayerBattleState side, AIArchetype archetype)
        {
            bool placed = true;
            while (placed)
            {
                placed = false;
                foreach (Card card in side.Hand.OrderByDescending(c => c.Attack + c.Health).ToList())
                {
                    if (card.ResourceCost > side.Resource) continue;
                    foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
                    {
                        if (!side.Lanes[lane].HasRoomFor(card)) continue;
                        if (controller.TryPlayCard(side, card, lane)) { placed = true; break; }
                    }
                    if (placed) break;
                }
            }
        }

        /// <summary>Deterministic hash of everything observable that combat/spells could mutate -
        /// both Avatars' Health, both sides' Energy, every living unit's id+CurrentHealth per
        /// lane, and the enemy spellbook's own cooldown state. Used to find the first tick two
        /// matched-seed runs diverge.</summary>
        private static int ComputeStateHash(BattleController controller)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + controller.PlayerState.AvatarHealth;
                hash = hash * 31 + controller.EnemyState.AvatarHealth;
                hash = hash * 31 + controller.Energy;
                hash = hash * 31 + controller.EnemyEnergy;
                foreach (PlayerBattleState side in new[] { controller.PlayerState, controller.EnemyState })
                {
                    foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
                    {
                        foreach (BattleCardInstance card in side.Lanes[lane].Cards)
                        {
                            hash = hash * 31 + card.Definition.Id.GetHashCode();
                            hash = hash * 31 + card.CurrentHealth;
                            hash = hash * 31 + (card.IsAlive ? 1 : 0);
                        }
                    }
                }
                foreach (AvatarSpell spell in controller.EnemySpellbook)
                {
                    hash = hash * 31 + spell.CooldownRemaining;
                }
                return hash;
            }
        }

        private static string DiagSnapshot(BattleController controller)
        {
            string units = string.Join(";", new[] { Lane.Front, Lane.Middle, Lane.Back }
                .SelectMany(l => controller.PlayerState.Lanes[l].Cards.Concat(controller.EnemyState.Lanes[l].Cards))
                .Select(c => $"{c.Definition.Id}:{c.CurrentHealth}:{c.IsAlive}"));
            string spellbook = string.Join(";", controller.EnemySpellbook.Select(s => $"{s.Id}:{s.CooldownRemaining}"));
            return $"AvatarHP={controller.PlayerState.AvatarHealth}/{controller.EnemyState.AvatarHealth} " +
                   $"Energy={controller.Energy}/{controller.EnemyEnergy} units=[{units}] spellbook=[{spellbook}]";
        }

        private enum ZeroCastCategory { NotZeroCast, NoOpportunityEver, OpportunityButEveryRollFailed, MatchEndedBeforeOpportunity }

        private sealed class TrialRecord
        {
            public int Seed;
            public long BaselineTicks;
            public long ForcedNoCastTicks;
            public long NormalTicks;
            public bool NormalHadAnyCast;
            public ZeroCastCategory Category;
            public int FirstDivergentTick = -1; // baseline vs forcedNoCast, -1 = never diverged within the shorter match's length
        }

        [Test]
        public void RootCause_ApprenticeZeroCast()
        {
            const int trials = 1500;
            const int baseSeed = 900001;
            const AIDifficultyTier tier = AIDifficultyTier.Apprentice;
            const int avatarLevel = 25;
            const int castleLevel = 15;

            List<Card> pool = LoadDatabase().AllCards.ToList();
            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel: 25);
            empire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);
            List<string> equippedIds = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Id).ToList();

            var records = new List<TrialRecord>();

            for (int i = 0; i < trials; i++)
            {
                if (i % 200 == 0) Debug.Log($"[RootCause] Apprentice: trial {i}/{trials}");
                int seed = baseSeed + i;
                var record = new TrialRecord { Seed = seed };

                // ---- Condition A: baseline, spells fully disabled ----
                var baselineHashes = new List<int>();
                UnityEngine.Random.InitState(seed);
                PlayerBattleState.SetShuffleSeedForTests(seed);
                BattleController baseController = CreateController();
                {
                    List<Card> pDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                    List<Card> eDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                    baseController.StartMatch(pDeck, eDeck, economy, economy, avatarLevel, unlockedStageIds: null, equippedSpellIds: equippedIds, enemyTier: tier, rngSeed: seed);
                    baseController.DealFormationHand(baseController.PlayerState);
                    baseController.DealFormationHand(baseController.EnemyState);
                    DeployWholeSquad(baseController, baseController.PlayerState, AIArchetype.Balanced);
                    SimpleAIOpponent.TakeTurn(baseController, AIArchetype.Balanced);
                    if (baseController.ConfirmFormation())
                    {
                        bool first = true;
                        while (baseController.Phase == BattlePhase.Combat)
                        {
                            if (first && i == 0) Debug.Log($"[RootCause] DIAG seed={seed} baseline tick0: {DiagSnapshot(baseController)}");
                            first = false;
                            baselineHashes.Add(ComputeStateHash(baseController));
                            baseController.AdvanceCombatTick();
                        }
                        record.BaselineTicks = baseController.TickCount;
                    }
                }
                UnityEngine.Object.DestroyImmediate(baseController.gameObject);

                // ---- Condition B: forced-no-cast, decision loop active, casting forcibly disabled ----
                var forcedHashes = new List<int>();
                UnityEngine.Random.InitState(seed);
                PlayerBattleState.SetShuffleSeedForTests(seed);
                BattleController forcedController = CreateController();
                {
                    List<Card> pDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                    List<Card> eDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                    forcedController.StartMatch(pDeck, eDeck, economy, economy, avatarLevel, unlockedStageIds: null, equippedSpellIds: equippedIds, enemyTier: tier, rngSeed: seed);
                    forcedController.EnableMirroredEnemySpellsForPvE();
                    forcedController.SetForceAiSpellCastGateAlwaysFailForTests(true);
                    forcedController.DealFormationHand(forcedController.PlayerState);
                    forcedController.DealFormationHand(forcedController.EnemyState);
                    DeployWholeSquad(forcedController, forcedController.PlayerState, AIArchetype.Balanced);
                    SimpleAIOpponent.TakeTurn(forcedController, AIArchetype.Balanced);
                    if (forcedController.ConfirmFormation())
                    {
                        bool first = true;
                        while (forcedController.Phase == BattlePhase.Combat)
                        {
                            if (first && i == 0) Debug.Log($"[RootCause] DIAG seed={seed} forced   tick0: {DiagSnapshot(forcedController)}");
                            first = false;
                            forcedHashes.Add(ComputeStateHash(forcedController));
                            forcedController.AdvanceCombatTick();
                        }
                        record.ForcedNoCastTicks = forcedController.TickCount;
                        Assert.AreEqual(0, forcedController.SpellCastLog.Count(c => !c.CastByPlayer),
                            $"HARD FAILURE: forced-no-cast control cast something anyway at seed {seed}.");
                    }
                }
                UnityEngine.Object.DestroyImmediate(forcedController.gameObject);

                int compareLength = Math.Min(baselineHashes.Count, forcedHashes.Count);
                for (int t = 0; t < compareLength; t++)
                {
                    if (baselineHashes[t] != forcedHashes[t]) { record.FirstDivergentTick = t; break; }
                }

                // ---- Condition C: normal, real production gate ----
                UnityEngine.Random.InitState(seed);
                PlayerBattleState.SetShuffleSeedForTests(seed);
                BattleController normalController = CreateController();
                bool hadOpportunityEver = false;
                {
                    List<Card> pDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                    List<Card> eDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                    normalController.StartMatch(pDeck, eDeck, economy, economy, avatarLevel, unlockedStageIds: null, equippedSpellIds: equippedIds, enemyTier: tier, rngSeed: seed);
                    normalController.EnableMirroredEnemySpellsForPvE();
                    normalController.DealFormationHand(normalController.PlayerState);
                    normalController.DealFormationHand(normalController.EnemyState);
                    DeployWholeSquad(normalController, normalController.PlayerState, AIArchetype.Balanced);
                    SimpleAIOpponent.TakeTurn(normalController, AIArchetype.Balanced);
                    if (normalController.ConfirmFormation())
                    {
                        while (normalController.Phase == BattlePhase.Combat)
                        {
                            int predictedTick = normalController.TickCount + 1;
                            int predictedEnergy = Math.Min(normalController.MaxEnergy,
                                normalController.EnemyEnergy + normalController.EnergyPerTick + BattleController.BackLaneEnergy(normalController.EnemyState));
                            bool predictedCast = AISpellCaster.TrySelectCast(
                                normalController.EnemySpellbook, predictedEnergy, predictedTick,
                                normalController.EnemyState, normalController.PlayerState, out _, out _, out _, normalController.EnemyDifficultyTier);
                            if (predictedCast) hadOpportunityEver = true;

                            normalController.AdvanceCombatTick();
                        }
                        record.NormalTicks = normalController.TickCount;
                        record.NormalHadAnyCast = normalController.SpellCastLog.Any(c => !c.CastByPlayer);
                    }
                }
                UnityEngine.Object.DestroyImmediate(normalController.gameObject);

                if (record.NormalHadAnyCast)
                {
                    record.Category = ZeroCastCategory.NotZeroCast;
                }
                else if (record.NormalTicks < AISpellCaster.MinTickForAnySpell)
                {
                    record.Category = ZeroCastCategory.MatchEndedBeforeOpportunity;
                }
                else if (!hadOpportunityEver)
                {
                    record.Category = ZeroCastCategory.NoOpportunityEver;
                }
                else
                {
                    record.Category = ZeroCastCategory.OpportunityButEveryRollFailed;
                }

                records.Add(record);
            }

            // ---------- Report ----------

            int neverDiverged = records.Count(r => r.FirstDivergentTick == -1);
            Debug.Log($"[RootCause] Apprentice: {records.Count} trials. baseline-vs-forcedNoCast NEVER diverged in {neverDiverged}/{records.Count} trials.");
            var diverged = records.Where(r => r.FirstDivergentTick >= 0).ToList();
            if (diverged.Count > 0)
            {
                Debug.Log($"[RootCause] Apprentice: {diverged.Count} trial(s) DID diverge - first divergent ticks: {string.Join(",", diverged.Take(20).Select(r => r.FirstDivergentTick))}, seeds: {string.Join(",", diverged.Take(5).Select(r => r.Seed))}");
            }

            double baselineAvgAll = records.Average(r => r.BaselineTicks);
            double forcedAvgAll = records.Average(r => r.ForcedNoCastTicks);
            Debug.Log($"[RootCause] Apprentice: OVERALL (all {records.Count} trials, matched seeds) baselineAvg={baselineAvgAll:F2} forcedNoCastAvg={forcedAvgAll:F2} ratio={(baselineAvgAll == 0 ? 1 : forcedAvgAll / baselineAvgAll):F3}");

            foreach (ZeroCastCategory category in new[] { ZeroCastCategory.NoOpportunityEver, ZeroCastCategory.OpportunityButEveryRollFailed, ZeroCastCategory.MatchEndedBeforeOpportunity })
            {
                List<TrialRecord> subset = records.Where(r => r.Category == category).ToList();
                if (subset.Count == 0)
                {
                    Debug.Log($"[RootCause] Apprentice: category={category} n=0");
                    continue;
                }

                double baselineAvg = subset.Average(r => r.BaselineTicks);
                double forcedAvg = subset.Average(r => r.ForcedNoCastTicks);
                double normalAvg = subset.Average(r => r.NormalTicks);
                Debug.Log($"[RootCause] Apprentice: category={category} n={subset.Count} " +
                          $"baselineAvg={baselineAvg:F2} forcedNoCastAvg={forcedAvg:F2} normalAvg={normalAvg:F2} " +
                          $"forcedVsBaselineRatio={(baselineAvg == 0 ? 1 : forcedAvg / baselineAvg):F3} normalVsBaselineRatio={(baselineAvg == 0 ? 1 : normalAvg / baselineAvg):F3}");
            }

            int anyCastCount = records.Count(r => r.Category == ZeroCastCategory.NotZeroCast);
            Debug.Log($"[RootCause] Apprentice: any-cast trials n={anyCastCount} (excluded from the category breakdown above, already covered by RunPairedZeroCastSplit's any-cast bucket).");
        }
    }
}
