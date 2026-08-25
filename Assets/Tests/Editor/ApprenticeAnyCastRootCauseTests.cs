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
    /// AI-balance diagnose-before-tune, real root-cause protocol for the any-cast tick-length
    /// finding (LOCKED, GPT/BS): c7f6467 measured Apprentice's anyCastTickRatio at 1.355 once
    /// matched seeds were fixed, and BS's methodological point is that this population is a
    /// post-treatment outcome (the AI casting is an event, not a pre-condition) - 1.355 could be
    /// the spell's legitimate gameplay effect (a heal keeping units alive longer, a buff
    /// prolonging a clash), not an AI-timing bug. This protocol isolates which:
    ///
    /// 1. Record the first tick the AI casts (per any-cast trial).
    /// 2. Compare baseline vs AI-on state hashes every tick BEFORE that cast - a real residual
    ///    defect would show up here even before the effect happens.
    /// 3. Classify the first cast by spell/effect type.
    /// 4. Shadow/no-op control (BattleController.SetShadowModeSuppressEnemySpellEffectForTests):
    ///    the AI's real live decision loop keeps running - same candidate selection, same gate
    ///    roll, same Energy spend, same cooldown, same SpellCastLog entry - but the actual
    ///    battlefield effect (spell.Cast's lane damage/heal/buff/reposition/silence) never
    ///    happens. Deliberately NOT a forced-no-cast control (that changes the cast schedule
    ///    arbitrarily and confounds spell impact with decision behavior, per GPT's explicit
    ///    instruction) and NOT a pre-recorded trace replay (the shadow run's own decision loop
    ///    evolves live against whatever state actually exists, which may legitimately select a
    ///    different later cast than the real run once effects are suppressed - that divergence is
    ///    real data, not a bug in the harness).
    /// 5. Compare normal AI-on vs shadow on the same matched seeds.
    ///
    /// Interpretation (GPT's own branches, not presupposed):
    /// - State diverges before first cast -> residual defect, not the spell's effect.
    /// - State matches until cast AND shadow matches baseline -> the ratio IS the spell's real
    ///   gameplay effect, not AI timing (the metric concept itself may need reconsidering as a
    ///   balance gate, not this AI code).
    /// - Shadow still longer than baseline -> investigate cast-selection bookkeeping/resource-
    ///   cooldown mutation/tick-order (a real AI-mechanics defect, distinct from gameplay effect).
    /// - Only one spell/effect type drives it -> per-spell ablation before touching any global
    ///   metric (this run's per-effect-type breakdown at the end is exactly that check).
    /// </summary>
    public class ApprenticeAnyCastRootCauseTests
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
            var go = new GameObject("AnyCastRootCauseCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            db = CardDatabase.Instance;
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("AnyCastRootCauseBattleController");
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

        /// <summary>Same deterministic state hash as the zero-cast root-cause harness
        /// (ApprenticeZeroCastRootCauseTests.ComputeStateHash) - duplicated here rather than
        /// shared since both are private to their own EditMode test fixture.</summary>
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

        private sealed class TrialRecord
        {
            public int Seed;
            public long BaselineTicks;
            public long NormalTicks;
            public long ShadowTicks;
            public int FirstCastTick = -1;
            public string FirstCastSpellName;
            public SpellEffect FirstCastEffect;
            public int PreCastDivergentTick = -1; // baseline vs normal, before FirstCastTick; -1 = no divergence found
        }

        [Test]
        public void RootCause_ApprenticeAnyCast()
        {
            const int trials = 1500;
            const int baseSeed = 910001;
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
                if (i % 200 == 0) Debug.Log($"[AnyCastRootCause] Apprentice: trial {i}/{trials}");
                int seed = baseSeed + i;

                // ---- Condition A: baseline, spells fully disabled ----
                var baselineHashes = new List<int>();
                UnityEngine.Random.InitState(seed);
                PlayerBattleState.SetShuffleSeedForTests(seed);
                BattleController baseController = CreateController();
                long baselineTicks = 0;
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
                        while (baseController.Phase == BattlePhase.Combat)
                        {
                            baselineHashes.Add(ComputeStateHash(baseController));
                            baseController.AdvanceCombatTick();
                        }
                        baselineTicks = baseController.TickCount;
                    }
                }
                UnityEngine.Object.DestroyImmediate(baseController.gameObject);

                // ---- Condition B: normal, real production gate ----
                var normalHashes = new List<int>();
                UnityEngine.Random.InitState(seed);
                PlayerBattleState.SetShuffleSeedForTests(seed);
                BattleController normalController = CreateController();
                long normalTicks = 0;
                int firstCastTick = -1;
                string firstCastSpellName = null;
                SpellEffect firstCastEffect = default;
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
                            normalHashes.Add(ComputeStateHash(normalController));
                            normalController.AdvanceCombatTick();
                        }
                        normalTicks = normalController.TickCount;

                        if (normalController.SpellCastLog.Any(c => !c.CastByPlayer))
                        {
                            SpellCastRecord firstCast = normalController.SpellCastLog.First(c => !c.CastByPlayer);
                            firstCastTick = firstCast.TickNumber;
                            firstCastSpellName = firstCast.SpellName;
                            AvatarSpell matched = normalController.EnemySpellbook.FirstOrDefault(s => s.Name == firstCastSpellName);
                            firstCastEffect = matched?.Effect ?? default;
                        }
                    }
                }
                UnityEngine.Object.DestroyImmediate(normalController.gameObject);

                if (firstCastTick < 0)
                {
                    // Zero-cast trial - out of scope for this protocol (already covered by
                    // ApprenticeZeroCastRootCauseTests / RunForcedNoCastMatchesBaseline).
                    continue;
                }

                var record = new TrialRecord
                {
                    Seed = seed,
                    BaselineTicks = baselineTicks,
                    NormalTicks = normalTicks,
                    FirstCastTick = firstCastTick,
                    FirstCastSpellName = firstCastSpellName,
                    FirstCastEffect = firstCastEffect,
                };

                int preCastLength = Math.Min(baselineHashes.Count, Math.Min(normalHashes.Count, firstCastTick));
                for (int t = 0; t < preCastLength; t++)
                {
                    if (baselineHashes[t] != normalHashes[t]) { record.PreCastDivergentTick = t; break; }
                }

                // ---- Condition C: shadow, real decision loop + bookkeeping, effect suppressed ----
                UnityEngine.Random.InitState(seed);
                PlayerBattleState.SetShuffleSeedForTests(seed);
                BattleController shadowController = CreateController();
                {
                    List<Card> pDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                    List<Card> eDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                    shadowController.StartMatch(pDeck, eDeck, economy, economy, avatarLevel, unlockedStageIds: null, equippedSpellIds: equippedIds, enemyTier: tier, rngSeed: seed);
                    shadowController.EnableMirroredEnemySpellsForPvE();
                    shadowController.SetShadowModeSuppressEnemySpellEffectForTests(true);
                    shadowController.DealFormationHand(shadowController.PlayerState);
                    shadowController.DealFormationHand(shadowController.EnemyState);
                    DeployWholeSquad(shadowController, shadowController.PlayerState, AIArchetype.Balanced);
                    SimpleAIOpponent.TakeTurn(shadowController, AIArchetype.Balanced);
                    if (shadowController.ConfirmFormation())
                    {
                        while (shadowController.Phase == BattlePhase.Combat) shadowController.AdvanceCombatTick();
                        record.ShadowTicks = shadowController.TickCount;
                    }
                }
                UnityEngine.Object.DestroyImmediate(shadowController.gameObject);

                records.Add(record);
            }

            // ---------- Report ----------

            Debug.Log($"[AnyCastRootCause] Apprentice: {records.Count} any-cast trials out of {trials} total.");

            int preCastDiverged = records.Count(r => r.PreCastDivergentTick >= 0);
            Debug.Log($"[AnyCastRootCause] Apprentice: pre-cast baseline-vs-normal divergence found in {preCastDiverged}/{records.Count} trials.");
            if (preCastDiverged > 0)
            {
                var sample = records.Where(r => r.PreCastDivergentTick >= 0).Take(5);
                foreach (TrialRecord r in sample)
                    Debug.LogError($"[AnyCastRootCause] Apprentice: PRE-CAST DIVERGENCE seed={r.Seed} tick={r.PreCastDivergentTick} (firstCastTick={r.FirstCastTick}) - residual defect, not the spell's effect.");
            }

            double baselineAvg = records.Average(r => r.BaselineTicks);
            double normalAvg = records.Average(r => r.NormalTicks);
            double shadowAvg = records.Average(r => r.ShadowTicks);
            double normalRatio = baselineAvg == 0 ? 1 : normalAvg / baselineAvg;
            double shadowRatio = baselineAvg == 0 ? 1 : shadowAvg / baselineAvg;
            Debug.Log($"[AnyCastRootCause] Apprentice: OVERALL n={records.Count} baselineAvg={baselineAvg:F2} normalAvg={normalAvg:F2} (ratio={normalRatio:F3}) shadowAvg={shadowAvg:F2} (ratio={shadowRatio:F3})");
            Debug.Log($"[AnyCastRootCause] Apprentice: INTERPRETATION - " +
                      (preCastDiverged > 0
                          ? "pre-cast divergence found = residual defect, investigate before trusting the ratio at all."
                          : shadowRatio >= 0.85 && shadowRatio <= 1.15
                              ? "shadow matches baseline within the ±15% reference band = the normal ratio is the spell's real gameplay effect, not AI timing."
                              : "shadow still diverges from baseline beyond ±15% = investigate cast-selection bookkeeping/resource-cooldown mutation/tick-order, a real AI-mechanics issue distinct from gameplay effect."));

            Debug.Log("[AnyCastRootCause] Apprentice: per-first-cast-effect-type breakdown (checks whether one spell type alone drives the ratio):");
            foreach (var group in records.GroupBy(r => r.FirstCastEffect).OrderByDescending(g => g.Count()))
            {
                List<TrialRecord> subset = group.ToList();
                double gBaseline = subset.Average(r => r.BaselineTicks);
                double gNormal = subset.Average(r => r.NormalTicks);
                double gShadow = subset.Average(r => r.ShadowTicks);
                Debug.Log($"[AnyCastRootCause] Apprentice: effect={group.Key} n={subset.Count} " +
                          $"baselineAvg={gBaseline:F2} normalAvg={gNormal:F2} (ratio={(gBaseline == 0 ? 1 : gNormal / gBaseline):F3}) " +
                          $"shadowAvg={gShadow:F2} (ratio={(gBaseline == 0 ? 1 : gShadow / gBaseline):F3}) " +
                          $"sampleSpells=[{string.Join(",", subset.Select(r => r.FirstCastSpellName).Distinct().Take(5))}]");
            }
        }
    }
}
