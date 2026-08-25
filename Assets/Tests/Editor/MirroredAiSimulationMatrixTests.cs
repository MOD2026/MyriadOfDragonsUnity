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
    /// Mirrored AI-Spellcasting Simulation Matrix (LOCKED, retroactively written 2026-08-24,
    /// docs/LOCKED_DECISIONS_REGISTER.md) - the harness this spec describes, built and run for the
    /// first time here. Drives the real production code (BattleController, AISpellCaster,
    /// AIEnemySpellbookResolver), same style as BalanceSimulationTests: no external model, no new
    /// seed framework.
    ///
    /// Real gaps the locked spec itself does not resolve, bridged here with documented, non-guessed
    /// judgment calls rather than silently invented numbers:
    ///
    /// 1. "Chapter-archetype group" has no real code mapping - nothing in this codebase ties a
    ///    campaign chapter to an avatar level or AIDifficultyTier (grepped for it, confirmed absent).
    ///    AIDifficultyTier IS the real, locked granularity the AI's own config actually varies by
    ///    (SoloAIScalingSystem). Used that directly instead of the spec's literal Ch1/Ch2-3/Ch4-6/
    ///    Ch7-10 labels. Further collapsed to 3 groups, not 5 tiers: AIEnemySpellbookResolverTests
    ///    already proves Veteran/Master/Titan resolve to an IDENTICAL final spellbook (Fault Line,
    ///    Renewal, Banner of Ashes, Stone Judgment - Master's own Tempest Brand addition never wins
    ///    a slot) - the spec's own collapsing rule ("only if chapters genuinely share identical AI
    ///    config") applies to those three by an already-verified fact, not an assumption. Groups:
    ///    Novice, Apprentice, VeteranPlus (Veteran/Master/Titan).
    /// 2. The "player" side has no headless equivalent in this codebase - AISpellCaster.
    ///    TrySelectCast is a side-agnostic pure function (spellbook/energy/tickCount/self/opponent
    ///    in, no hardcoded "enemy" bias), so the Full Match (on/on) scenario reuses it symmetrically
    ///    for the player. This is a deliberate modeling choice, not shipped behaviour - production
    ///    players are human.
    /// 3. Both sides use the SAME tier-resolved loadout (AIEnemySpellbookResolver.ResolveSpellbook)
    ///    for symmetry/fairness of the casting-mechanic test itself - there's no real "expected
    ///    player loadout at avatar level X" curve to draw from either (same missing-mapping problem
    ///    as #1), so a synthetic-but-fair symmetric loadout was the least-invented option available.
    /// 4. Fallback stress ("tests unaffordable/invalid handling") needed a real lever:
    ///    EnergyPerTick is a fixed, non-public-settable production constant (18), not touched.
    ///    Instead both sides are equipped with only the 2 highest-EnergyCost spells from the tier's
    ///    resolved pool via equippedSpellIds - genuinely harder to afford, same production Energy
    ///    generation, no synthetic hack.
    /// 5. "AI cast rate ... of matches with >=1 legal opportunity" and "no-spell fallback rate ...
    ///    of matches" use different denominators (spec's own wording) - implemented literally:
    ///    opportunity is predicted read-only via AISpellCaster.TrySelectCast before each
    ///    AdvanceCombatTick call (never mutates state), which doubles as the invalid-cast hard-
    ///    failure check (see RunScenario).
    ///
    /// Failure policy per the lock: any missed band is a real test failure, logged and reported -
    /// this harness never adjusts an assertion to make a bad number pass.
    ///
    /// UPDATED 2026-08-24 for the AI Spell Cast Probability Gate (LOCKED, docs/
    /// LOCKED_DECISIONS_REGISTER.md): the first full run of this matrix found AI cast rate ~99.9%
    /// (band 25-70%) and traced it, after a real §5-heuristic audit, to §5's own tactical clauses
    /// having no frequency limit at all - not a coding gap. The register now locks a separate
    /// 40%-cast/60%-pass roll (match-seeded, reproducible) on top of §5's candidate selection.
    /// StartMatch's real production MatchRngSeed is recorded per trial here (ScenarioResult.
    /// RecordedSeeds) rather than duplicated - this harness's own opportunity-prediction logic is
    /// unaffected by the gate (it still predicts candidacy only, exactly matching what §5 itself
    /// gates), so AiCastRateOfOpportunity should now land near the locked 40% roll itself. The
    /// gate is applied only inside the real AI path (AISpellCaster.TryCastDuringCombatTick) - the
    /// Full Match(on,on) scenario's own player-side modeling (judgment call #2 above) deliberately
    /// stays ungated, since the lock is titled "AI Spell Cast Probability Gate," not a general
    /// spellcaster gate, and a human player doesn't have a coin-flip pass either.
    /// </summary>
    public class MirroredAiSimulationMatrixTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private const double Z95 = 1.959963985;

        [TearDown]
        public void TearDown()
        {
            LaneBattleResolver.ResetRulesToDefault();
            foreach (GameObject go in _spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private CardDatabase LoadDatabase()
        {
            var go = new GameObject("SimMatrixCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            db = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("SimMatrixBattleController");
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

        // ---------- Wilson 95% CI ----------

        private readonly struct Wilson
        {
            public readonly double Center, Low, High;
            public Wilson(int successes, int n)
            {
                if (n == 0) { Center = Low = High = 0; return; }
                double p = (double)successes / n;
                double denom = 1 + Z95 * Z95 / n;
                double centre = (p + Z95 * Z95 / (2 * n)) / denom;
                double margin = Z95 * Math.Sqrt(p * (1 - p) / n + Z95 * Z95 / (4.0 * n * n)) / denom;
                Center = centre;
                Low = Math.Max(0, centre - margin);
                High = Math.Min(1, centre + margin);
            }
            public override string ToString() => $"{Center:P1} [{Low:P1}, {High:P1}]";
        }

        // ---------- Groups ----------

        private enum TierGroup { Novice, Apprentice, VeteranPlus }

        private static (int avatarLevel, int castleLevel, AIDifficultyTier tier) GroupConfig(TierGroup g) => g switch
        {
            // avatarLevel/castleLevel pairs reuse BalanceSimulationTests' own existing precedent
            // ratios (1/1, 25/15, 30/30), not invented fresh.
            TierGroup.Novice => (10, 10, AIDifficultyTier.Novice),
            TierGroup.Apprentice => (25, 15, AIDifficultyTier.Apprentice),
            TierGroup.VeteranPlus => (50, 30, AIDifficultyTier.Veteran),
            _ => throw new ArgumentOutOfRangeException(nameof(g)),
        };

        // ---------- Result ----------

        private sealed class ScenarioResult
        {
            public string Label;
            public int Trials;
            public int AiWins;
            public int PlayerWins;
            public int Undecided;
            public long TotalTicks;
            public int EarlyKOs;
            public int TrialsWithOpportunity;
            public int TrialsWithAiCast;
            public int TotalAiCasts;
            public int TrialsWithZeroAiCasts;
            public readonly Dictionary<string, int> AiWinContributionBySpell = new Dictionary<string, int>();
            /// <summary>AI Spell Cast Probability Gate (LOCKED 2026-08-24): "recorded seeds" - one
            /// per trial, StartMatch's own MatchRngSeed (real production seed, not a harness-side
            /// duplicate), so any specific trial can be replayed exactly by passing it back in.</summary>
            public readonly List<int> RecordedSeeds = new List<int>();

            public double AverageTicks => Trials == 0 ? 0 : (double)TotalTicks / Trials;
            public Wilson AiWinRate => new Wilson(AiWins, Trials);
            public Wilson PlayerWinRate => new Wilson(PlayerWins, Trials);
            public Wilson EarlyKORate => new Wilson(EarlyKOs, Trials);
            public Wilson AiCastRateOfOpportunity => new Wilson(TrialsWithAiCast, TrialsWithOpportunity);
            public Wilson NoSpellFallbackRate => new Wilson(TrialsWithZeroAiCasts, Trials);
            public double SpellsPerMatch => Trials == 0 ? 0 : (double)TotalAiCasts / Trials;
            public double MaxSingleSpellWinShare => AiWins == 0 || AiWinContributionBySpell.Count == 0
                ? 0 : (double)AiWinContributionBySpell.Values.Max() / AiWins;

            public void Log()
            {
                Debug.Log($"[SimMatrix] {Label}: trials={Trials} aiWin={AiWinRate} playerWin={PlayerWinRate} " +
                          $"avgTicks={AverageTicks:F2} earlyKO={EarlyKORate} aiCastRate(opportunity)={AiCastRateOfOpportunity} " +
                          $"(opportunity trials={TrialsWithOpportunity}) spellsPerMatch={SpellsPerMatch:F2} " +
                          $"noSpellFallback={NoSpellFallbackRate} maxSingleSpellWinShare={MaxSingleSpellWinShare:P1}");
                string seedSample = string.Join(",", RecordedSeeds.Take(10));
                Debug.Log($"[SimMatrix] {Label}: recordedSeeds count={RecordedSeeds.Count} first10=[{seedSample}]");
            }
        }

        /// <summary>Real harness core. equippedSpellIdsOverride, when set, is used for BOTH sides
        /// (fallback-stress lever - see class doc #4); otherwise both sides get the tier's own
        /// AIEnemySpellbookResolver loadout (see class doc #2/#3), the AI via enemyTier (real
        /// production wiring), the player via equippedSpellIds (also real production wiring).</summary>
        private ScenarioResult RunScenario(List<Card> pool, TierGroup group, string label, int trials,
            bool aiSpellsOn, bool playerSpellsOn, bool resourceStress = false, List<string> equippedSpellIdsOverride = null)
        {
            (int avatarLevel, int castleLevel, AIDifficultyTier tier) = GroupConfig(group);
            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel: 25);
            empire.InitializeTCGModifiers();

            int resourceCap = resourceStress ? Mathf.Max(4, empire.ResourceCap / 3) : empire.ResourceCap;
            int turn1Resource = resourceStress ? Mathf.Max(2, empire.Turn1Resource / 3) : empire.Turn1Resource;
            var economy = new BattleController.MatchEconomy(resourceCap, turn1Resource, empire.StartingAvatarHealth);

            List<string> sharedEquippedIds = equippedSpellIdsOverride ??
                AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Id).ToList();

            var result = new ScenarioResult { Label = label };
            int predictionMissedCandidateCount = 0;

            for (int i = 0; i < trials; i++)
            {
                if (i % 100 == 0) Debug.Log($"[SimMatrix] {label}: trial {i}/{trials}");

                BattleController controller = CreateController();
                List<Card> playerDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();

                controller.StartMatch(playerDeck, enemyDeck, economy, economy,
                    avatarLevel, unlockedStageIds: null, equippedSpellIds: sharedEquippedIds,
                    enemyTier: equippedSpellIdsOverride == null ? tier : (AIDifficultyTier?)null);
                result.RecordedSeeds.Add(controller.MatchRngSeed);

                if (aiSpellsOn) controller.EnableMirroredEnemySpellsForPvE();

                controller.DealFormationHand(controller.PlayerState);
                controller.DealFormationHand(controller.EnemyState);
                DeployWholeSquad(controller, controller.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

                if (!controller.ConfirmFormation())
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                    continue;
                }

                bool hadOpportunity = false;

                while (controller.Phase == BattlePhase.Combat)
                {
                    // Predicts the exact inputs AdvanceCombatTick's own real AI decision will use -
                    // TickCount++ and the Energy regen step both happen BEFORE AISpellCaster runs
                    // inside AdvanceCombatTick, so checking with today's (pre-tick) TickCount/Energy
                    // would systematically undercount opportunity (confirmed by a smoke run: it
                    // produced an impossible >100% "cast rate of opportunity", i.e. more actual
                    // casts than predicted opportunities). BackLaneEnergy is the same public,
                    // real production method AdvanceCombatTick itself calls - not reimplemented.
                    int predictedTick = controller.TickCount + 1;
                    int predictedEnemyEnergy = Math.Min(controller.MaxEnergy,
                        controller.EnemyEnergy + controller.EnergyPerTick + BattleController.BackLaneEnergy(controller.EnemyState));
                    bool aiPredictedCast = aiSpellsOn && AISpellCaster.TrySelectCast(
                        controller.EnemySpellbook, predictedEnemyEnergy, predictedTick,
                        controller.EnemyState, controller.PlayerState, out _, out _, out _, controller.EnemyDifficultyTier);
                    if (aiPredictedCast) hadOpportunity = true;
                    int castLogBefore = controller.SpellCastLog.Count;

                    controller.AdvanceCombatTick();

                    // Invalid-cast hard failure (AI side): the only invariant a read-only,
                    // pre-tick PREDICTION can honestly assert is the per-tick cap (provable
                    // regardless of prediction accuracy) - not "candidate exists <=> cast
                    // happens" in either direction. This prediction is deliberately imprecise at
                    // the cooldown boundary (it can't shadow-advance TickCooldown() without
                    // mutating the real spell - see the earlier comment on predictedEnemyEnergy
                    // for why Energy could be predicted exactly but cooldown can't the same way),
                    // so both false positives (predicted candidate, gate rolled a pass - expected,
                    // 60% of the time) and false negatives (missed a spell 1 tick from ready) are
                    // possible and are not bugs. Mismatches are counted and logged, not asserted.
                    int aiCastLogDelta = controller.SpellCastLog.Count - castLogBefore;
                    Assert.LessOrEqual(aiCastLogDelta, 1,
                        $"HARD FAILURE [{label}]: more than one AI cast logged in a single tick {controller.TickCount} - per-tick cap violated.");
                    if (!aiPredictedCast && aiCastLogDelta > 0) predictionMissedCandidateCount++;

                    if (playerSpellsOn && controller.Phase == BattlePhase.Combat)
                    {
                        if (AISpellCaster.TrySelectCast(controller.Spellbook, controller.Energy, controller.TickCount,
                                controller.PlayerState, controller.EnemyState, out int spellIndex, out Lane targetLane,
                                out RepositionTarget repositionTarget))
                        {
                            bool cast = controller.TryCastSpell(spellIndex, targetLane, out _, repositionTarget);
                            // Invalid-cast hard failure (player side).
                            Assert.IsTrue(cast, $"HARD FAILURE [{label}]: player-side selected cast was illegal at tick {controller.TickCount}.");
                        }
                    }
                }

                result.Trials++;
                result.TotalTicks += controller.TickCount;
                bool resolved = controller.PlayerState.IsDefeated || controller.EnemyState.IsDefeated;
                if (resolved && controller.TickCount < BattleController.MinimumCombatTickForAvatarStrike) result.EarlyKOs++;

                if (controller.EnemyState.IsDefeated && !controller.PlayerState.IsDefeated) result.PlayerWins++;
                else if (controller.PlayerState.IsDefeated && !controller.EnemyState.IsDefeated) result.AiWins++;
                else result.Undecided++;

                if (hadOpportunity) result.TrialsWithOpportunity++;

                List<SpellCastRecord> aiCasts = controller.SpellCastLog.Where(c => !c.CastByPlayer).ToList();
                if (aiCasts.Count > 0) result.TrialsWithAiCast++;
                else result.TrialsWithZeroAiCasts++;
                result.TotalAiCasts += aiCasts.Count;

                // Real bug found post-fix-verification: a >100% maxSingleSpellWinShare surfaced,
                // which is mathematically impossible under this method's own definition unless
                // this condition disagrees with AiWins' own (PlayerState.IsDefeated &&
                // !EnemyState.IsDefeated) - it did, missing the !EnemyState.IsDefeated half, so a
                // double-KO trial (counted as Undecided, not a win) could still credit a spell here.
                if (controller.PlayerState.IsDefeated && !controller.EnemyState.IsDefeated && aiCasts.Count > 0)
                {
                    foreach (string spellName in aiCasts.Select(c => c.SpellName).Distinct())
                    {
                        result.AiWinContributionBySpell.TryGetValue(spellName, out int cur);
                        result.AiWinContributionBySpell[spellName] = cur + 1;
                    }
                }

                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }

            if (predictionMissedCandidateCount > 0)
            {
                Debug.Log($"[SimMatrix] {label}: prediction missed {predictionMissedCandidateCount} real cast(s) it didn't foresee " +
                          "(known cooldown-boundary prediction gap, not a hard failure - see RunScenario's own comment).");
            }
            result.Log();
            return result;
        }

        /// <summary>Escalates a scenario to 2,000 trials if the given rate came back below 5% at
        /// 1,000 (LOCKED spec's own "2,000 if early-KO or spell-use rate is below 5%").</summary>
        private ScenarioResult RunWithEscalation(List<Card> pool, TierGroup group, string label,
            bool aiSpellsOn, bool playerSpellsOn, bool resourceStress = false, List<string> equippedSpellIdsOverride = null)
        {
            ScenarioResult first = RunScenario(pool, group, label, 1000, aiSpellsOn, playerSpellsOn, resourceStress, equippedSpellIdsOverride);
            bool lowEarlyKo = first.EarlyKORate.Center < 0.05;
            bool lowSpellUse = aiSpellsOn && first.TrialsWithAiCast < first.Trials * 0.05;
            if (!lowEarlyKo && !lowSpellUse) return first;

            Debug.Log($"[SimMatrix] {label}: escalating to 2000 trials (earlyKO={first.EarlyKORate.Center:P1}, aiCastFraction={(double)first.TrialsWithAiCast / Mathf.Max(1, first.Trials):P1}).");
            return RunScenario(pool, group, label + " (2000)", 2000, aiSpellsOn, playerSpellsOn, resourceStress, equippedSpellIdsOverride);
        }

        // ---------- Zero-cast / any-cast tick-ratio split (LOCKED 2026-08-25) ----------

        private sealed class SplitTickResult
        {
            public int ZeroCastN;
            public double ZeroCastBaselineAvg;
            public double ZeroCastOnAvg;
            public int AnyCastN;
            public double AnyCastBaselineAvg;
            public double AnyCastOnAvg;

            public double ZeroCastTickRatio => ZeroCastBaselineAvg == 0 ? 1 : ZeroCastOnAvg / ZeroCastBaselineAvg;
            public double AnyCastTickRatio => AnyCastBaselineAvg == 0 ? 1 : AnyCastOnAvg / AnyCastBaselineAvg;

            public void Log(string label)
            {
                Debug.Log($"[SimMatrix] {label}: zeroCastSplit n={ZeroCastN} baselineAvg={ZeroCastBaselineAvg:F2} onAvg={ZeroCastOnAvg:F2} ratio={ZeroCastTickRatio:F3}");
                Debug.Log($"[SimMatrix] {label}: anyCastSplit n={AnyCastN} baselineAvg={AnyCastBaselineAvg:F2} onAvg={AnyCastOnAvg:F2} ratio={AnyCastTickRatio:F3}");
            }
        }

        /// <summary>Runs baseline (spells off) and AI-casting (spells on) TOGETHER, per trial,
        /// under the identical seed for both (same seed-pairing methodology as the Windstep
        /// ablation - UnityEngine.Random.InitState(seed) before each side's own deck/hand draw,
        /// StartMatch's own rngSeed: seed) - so any tick-count difference between the two is
        /// attributable to AI casting itself, not trial-to-trial deck/hand variance. Buckets each
        /// matched pair by whether the ON side cast anything at all, so the zero-cast and any-cast
        /// populations (which behave oppositely - see the class-level LOCKED comment at the call
        /// site) each get compared against their OWN matched-seed baseline average, not a single
        /// blended aggregate.</summary>
        private SplitTickResult RunPairedZeroCastSplit(List<Card> pool, TierGroup group, int trials, int baseSeed)
        {
            (int avatarLevel, int castleLevel, AIDifficultyTier tier) = GroupConfig(group);
            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel: 25);
            empire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);
            List<string> equippedIds = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Id).ToList();

            var zeroCastBaseline = new List<long>();
            var zeroCastOn = new List<long>();
            var anyCastBaseline = new List<long>();
            var anyCastOn = new List<long>();

            for (int i = 0; i < trials; i++)
            {
                if (i % 200 == 0) Debug.Log($"[SimMatrix] {group}/ZeroCastSplit: trial {i}/{trials}");
                int seed = baseSeed + i;

                UnityEngine.Random.InitState(seed);
                BattleController baseController = CreateController();
                List<Card> basePlayerDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> baseEnemyDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                baseController.StartMatch(basePlayerDeck, baseEnemyDeck, economy, economy,
                    avatarLevel, unlockedStageIds: null, equippedSpellIds: equippedIds, enemyTier: tier, rngSeed: seed);
                baseController.DealFormationHand(baseController.PlayerState);
                baseController.DealFormationHand(baseController.EnemyState);
                DeployWholeSquad(baseController, baseController.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(baseController, AIArchetype.Balanced);
                bool baseConfirmed = baseController.ConfirmFormation();
                long baselineTicks = 0;
                if (baseConfirmed)
                {
                    while (baseController.Phase == BattlePhase.Combat) baseController.AdvanceCombatTick();
                    baselineTicks = baseController.TickCount;
                }
                UnityEngine.Object.DestroyImmediate(baseController.gameObject);

                UnityEngine.Random.InitState(seed);
                BattleController onController = CreateController();
                List<Card> onPlayerDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> onEnemyDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                onController.StartMatch(onPlayerDeck, onEnemyDeck, economy, economy,
                    avatarLevel, unlockedStageIds: null, equippedSpellIds: equippedIds, enemyTier: tier, rngSeed: seed);
                onController.EnableMirroredEnemySpellsForPvE();
                onController.DealFormationHand(onController.PlayerState);
                onController.DealFormationHand(onController.EnemyState);
                DeployWholeSquad(onController, onController.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(onController, AIArchetype.Balanced);
                bool onConfirmed = onController.ConfirmFormation();
                long onTicks = 0;
                bool hadAnyCast = false;
                if (onConfirmed)
                {
                    while (onController.Phase == BattlePhase.Combat) onController.AdvanceCombatTick();
                    onTicks = onController.TickCount;
                    hadAnyCast = onController.SpellCastLog.Any(c => !c.CastByPlayer);
                }
                UnityEngine.Object.DestroyImmediate(onController.gameObject);

                if (!baseConfirmed || !onConfirmed) continue; // pair unusable without both sides.

                if (hadAnyCast)
                {
                    anyCastBaseline.Add(baselineTicks);
                    anyCastOn.Add(onTicks);
                }
                else
                {
                    zeroCastBaseline.Add(baselineTicks);
                    zeroCastOn.Add(onTicks);
                }
            }

            var result = new SplitTickResult
            {
                ZeroCastN = zeroCastOn.Count,
                ZeroCastBaselineAvg = zeroCastBaseline.Count == 0 ? 0 : zeroCastBaseline.Average(),
                ZeroCastOnAvg = zeroCastOn.Count == 0 ? 0 : zeroCastOn.Average(),
                AnyCastN = anyCastOn.Count,
                AnyCastBaselineAvg = anyCastBaseline.Count == 0 ? 0 : anyCastBaseline.Average(),
                AnyCastOnAvg = anyCastOn.Count == 0 ? 0 : anyCastOn.Average(),
            };
            result.Log(group.ToString());
            return result;
        }

        /// <summary>The 5 locked scenarios for one tier group, asserted against the locked
        /// acceptance bands. A band miss fails loudly here - this method never adjusts a threshold
        /// to make a bad number pass (LOCKED spec's own failure policy).</summary>
        private void RunGroup(TierGroup group)
        {
            CardDatabase db = LoadDatabase();
            List<Card> pool = db.AllCards.ToList();
            string g = group.ToString();

            ScenarioResult baseline = RunWithEscalation(pool, group, $"{g}/Baseline(off,off)", aiSpellsOn: false, playerSpellsOn: false);
            ScenarioResult aiOn = RunWithEscalation(pool, group, $"{g}/AiCasting(on,off)", aiSpellsOn: true, playerSpellsOn: false);
            ScenarioResult full = RunWithEscalation(pool, group, $"{g}/FullMatch(on,on)", aiSpellsOn: true, playerSpellsOn: true);

            List<string> expensiveIds = AIEnemySpellbookResolver.ResolveSpellbook(GroupConfig(group).tier)
                .OrderByDescending(s => s.EnergyCost).Take(2).Select(s => s.Id).ToList();
            ScenarioResult fallbackStress = RunWithEscalation(pool, group, $"{g}/FallbackStress(on,off)",
                aiSpellsOn: true, playerSpellsOn: false, equippedSpellIdsOverride: expensiveIds);
            ScenarioResult resourceStress = RunWithEscalation(pool, group, $"{g}/ResourceStress(on,off)",
                aiSpellsOn: true, playerSpellsOn: false, resourceStress: true);

            // ---------- AI casting scenario, relative to Baseline parity ----------

            double aiWinDeltaPp = aiOn.AiWinRate.Center - baseline.AiWinRate.Center;
            Assert.That(aiWinDeltaPp, Is.InRange(-0.05, 0.08),
                $"[{g}] AI win-rate delta {aiWinDeltaPp:P1} outside the locked -5pp..+8pp band (baseline {baseline.AiWinRate}, on {aiOn.AiWinRate}). ESCALATE TO CC.");

            double playerWinDropPp = baseline.PlayerWinRate.Center - aiOn.PlayerWinRate.Center;
            Assert.LessOrEqual(playerWinDropPp, 0.08,
                $"[{g}] Player win-rate dropped {playerWinDropPp:P1} vs baseline, exceeding the locked 8pp cap (baseline {baseline.PlayerWinRate}, on {aiOn.PlayerWinRate}). ESCALATE TO CC.");

            // Tick-ratio metric REFACTORED (LOCKED 2026-08-25, GPT decision after CR root-caused
            // the Apprentice ±15% failure): the OLD single aggregate ratio blended two populations
            // that behave oppositely - trials where the AI casts nothing run structurally LONGER
            // (close/grindy fights correlate with both running long AND offering fewer legal
            // spell windows) while trials where it casts at least once run structurally SHORTER.
            // Apprentice's aggregate crossed ±15% only because its zero-cast trials happen to
            // deviate from ITS OWN baseline by a wider margin than VeteranPlus's do, not because
            // of a real per-tier defect - widening the shared band would have hidden a real signal
            // for every OTHER tier instead of fixing the metric. Split into zero-cast and any-cast
            // populations, matched-seed against baseline (RunPairedZeroCastSplit - same
            // seed-pairing methodology as the Windstep ablation), each gated at the original ±15%
            // independently. Zero-cast FREQUENCY remains its own separate, already-tier-specific
            // metric (NoSpellFallbackRate, asserted below) - this split only concerns match
            // LENGTH within each population, not how often each population occurs.
            SplitTickResult split = RunPairedZeroCastSplit(pool, group, trials: 2000, baseSeed: 800001);
            if (split.ZeroCastN > 0)
            {
                Assert.That(split.ZeroCastTickRatio, Is.InRange(0.85, 1.15),
                    $"[{g}] Zero-cast-trial ticks {split.ZeroCastOnAvg:F2} vs matched-seed baseline {split.ZeroCastBaselineAvg:F2} " +
                    $"({split.ZeroCastN} trials) outside the locked ±15% band. ESCALATE TO CC.");
            }
            if (split.AnyCastN > 0)
            {
                Assert.That(split.AnyCastTickRatio, Is.InRange(0.85, 1.15),
                    $"[{g}] Any-cast-trial ticks {split.AnyCastOnAvg:F2} vs matched-seed baseline {split.AnyCastBaselineAvg:F2} " +
                    $"({split.AnyCastN} trials) outside the locked ±15% band. ESCALATE TO CC.");
            }

            Assert.LessOrEqual(aiOn.EarlyKORate.Center, 0.10,
                $"[{g}] Early-KO rate {aiOn.EarlyKORate} exceeds the locked 10% ceiling. ESCALATE TO CC.");
            Assert.LessOrEqual(aiOn.EarlyKORate.Center - baseline.EarlyKORate.Center, 0.05,
                $"[{g}] Early-KO rate rose {(aiOn.EarlyKORate.Center - baseline.EarlyKORate.Center):P1} above baseline, exceeding the locked 5pp cap. ESCALATE TO CC.");

            // Novice LOCKED 15-35% (2026-08-25, GPT confirmed independently after CR flagged the
            // metric mismatch): GPT's original "cast rate 0-5%" was decided against CR's forced-
            // 100%-roll ceiling diagnostic, which measures casts-per-TICK across the whole match
            // (2.55% ceiling) - this test's own AiCastRateOfOpportunity is a DIFFERENT metric,
            // trials-that-got-a-cast divided by trials-that-ever-had-an-opportunity (per-MATCH
            // conditioned on opportunity, not per-tick), so 0-5% never applied to it. GPT's real
            // reply locked 15-35% for THIS metric, centered on the ~22.7% observed - re-measured
            // here on the dead-slot-filtered loadout (not the old contaminated numbers) before
            // finalizing: real result 21.6% [19.4%, 23.7%], comfortably inside the band.
            double castRateFloor = group == TierGroup.Novice ? 0.15 : 0.25;
            double castRateCeiling = group == TierGroup.Novice ? 0.35 : 0.70;
            Assert.That(aiOn.AiCastRateOfOpportunity.Center, Is.InRange(castRateFloor, castRateCeiling),
                $"[{g}] AI cast rate (of {aiOn.TrialsWithOpportunity} opportunity trials) {aiOn.AiCastRateOfOpportunity} outside the locked {castRateFloor:P0}-{castRateCeiling:P0} band. ESCALATE TO CC.");

            // Apprentice floor lowered 0.5->0.30 (LOCKED 2026-08-24, GPT decision after the
            // candidate-rejection diagnostic proved 0.5 was mathematically unreachable: Apprentice's
            // theoretical max even at a 100% ordinary roll is ~0.393 - not a probability-gate
            // defect, a real ceiling from how rarely its pool produces a §5-legal target).
            // VeteranPlus given the same treatment (LOCKED 2026-08-24): the per-spell impact
            // diagnostic confirmed VeteranPlus is the same candidate-scarcity class as Apprentice
            // (not Novice's impact problem) - its win-rate delta already stays within the 8pp cap
            // at the current 45% gate, the real problem is frequency. Novice given its own tight
            // 0.10-0.20 band (LOCKED 2026-08-25, same ceiling-diagnostic evidence as the cast-rate
            // band above) instead of Apprentice/VeteranPlus's floor-only relaxation - Novice's
            // measured 0.166 sits inside this band already. Master/Titan keep the original 0.5
            // floor/2.5 ceiling until separately disproven.
            double spellsPerMatchFloor = group switch
            {
                TierGroup.Novice => 0.10,
                TierGroup.Apprentice or TierGroup.VeteranPlus => 0.30,
                _ => 0.5,
            };
            double spellsPerMatchCeiling = group == TierGroup.Novice ? 0.20 : 2.5;
            Assert.That(aiOn.SpellsPerMatch, Is.InRange(spellsPerMatchFloor, spellsPerMatchCeiling),
                $"[{g}] Spells/match {aiOn.SpellsPerMatch:F2} outside the locked {spellsPerMatchFloor:F2}-{spellsPerMatchCeiling:F2} ordinary band. ESCALATE TO CC.");

            // Apprentice/VeteranPlus-specific fallback ceiling raised 45%->70% (LOCKED 2026-08-24,
            // GPT decision): same root cause as the spells/match floor above - only a small
            // fraction of ticks produce a legal ordinary candidate at these two tiers, so a high
            // zero-cast rate is structurally expected, not a defect. 70% gives a modest margin
            // above the observed result without declaring zero-cast matches desirable. Novice
            // raised further to 95% (LOCKED 2026-08-25) - its own ceiling diagnostic showed 16.4%
            // of trials never had even one legal ordinary opportunity in the whole match, so a
            // near-universal no-cast fallback rate is the structurally expected norm for this
            // tier, not a defect. Master/Titan keep 45%.
            double noSpellFallbackCeiling = group switch
            {
                TierGroup.Novice => 0.95,
                TierGroup.Apprentice or TierGroup.VeteranPlus => 0.70,
                _ => 0.45,
            };
            Assert.That(aiOn.NoSpellFallbackRate.Center, Is.InRange(0.10, noSpellFallbackCeiling),
                $"[{g}] No-spell fallback rate {aiOn.NoSpellFallbackRate} outside the locked 10-{noSpellFallbackCeiling:P0} band. ESCALATE TO CC.");

            Assert.LessOrEqual(aiOn.MaxSingleSpellWinShare, 0.40,
                $"[{g}] A single spell contributed {aiOn.MaxSingleSpellWinShare:P1} of AI wins, exceeding the locked 40% cap. ESCALATE TO CC.");

            // ---------- Full Match scenario, relative to AiCasting(on,off) ----------

            Assert.LessOrEqual(full.EarlyKORate.Center, 0.10,
                $"[{g}] Full Match early-KO rate {full.EarlyKORate} exceeds the locked 10% ceiling. ESCALATE TO CC.");
            Assert.LessOrEqual(full.EarlyKORate.Center - aiOn.EarlyKORate.Center, 0.05,
                $"[{g}] Full Match early-KO rate rose {(full.EarlyKORate.Center - aiOn.EarlyKORate.Center):P1} above AiCasting(on,off), exceeding the locked 5pp cap. ESCALATE TO CC.");

            double fullTickRatio = aiOn.AverageTicks == 0 ? 1 : full.AverageTicks / aiOn.AverageTicks;
            Assert.That(fullTickRatio, Is.InRange(0.80, 1.20),
                $"[{g}] Full Match average ticks {full.AverageTicks:F2} vs AiCasting(on,off) {aiOn.AverageTicks:F2} outside the locked ±20% band. ESCALATE TO CC.");

            // AI cast rate/win rates for Full Match are descriptive only per the lock - logged, not gated.
            double fullMaxShift = Math.Abs(full.MaxSingleSpellWinShare - aiOn.MaxSingleSpellWinShare);
            Debug.Log($"[SimMatrix] {g}/FullMatch spell-contribution shift vs AiCasting(on,off): {fullMaxShift:P1} " +
                      (fullMaxShift > 0.15 ? "- FLAGGED for CC review (descriptive only, not auto-retuned)." : "- within normal variation."));

            // Fallback/Resource stress: same correctness gate as AI casting (zero invalid casts,
            // already asserted inline in RunScenario) - every other metric is descriptive/diagnostic
            // only per the lock, so nothing further is gated here; both are logged above.
        }

        [Test]
        public void SimulationMatrix_Novice() => RunGroup(TierGroup.Novice);

        [Test]
        public void SimulationMatrix_Apprentice() => RunGroup(TierGroup.Apprentice);

        [Test]
        public void SimulationMatrix_VeteranPlus() => RunGroup(TierGroup.VeteranPlus);
    }
}
