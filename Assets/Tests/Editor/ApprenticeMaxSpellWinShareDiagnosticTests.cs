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
    /// Diagnose-before-tune for the newly-surfaced Apprentice MaxSingleSpellWinShare finding
    /// (77.4% vs the locked 40% cap, e6c3923's own run) - previously masked because Apprentice
    /// failed on the zero-cast/any-cast tick-ratio gates FIRST in every prior run of this suite
    /// (NUnit's Assert throws on first failure), so this assertion never got a chance to execute
    /// until the zero-cast/any-cast thread was fully closed.
    ///
    /// CC's ask (same rigor as the Windstep ablation): (1) identify which spell is actually
    /// driving the share - already known NOT to be Windstep, since the earlier Windstep ablation
    /// (VeteranPlusWindstepAblationTests) ruled it out as a win-rate driver; (2) get a real trial
    /// count worth trusting; (3) check whether the number is stable across reruns or just noisy at
    /// this sample size. Deliberately no seeding here (unlike the matched-seed root-cause tests) -
    /// RunScenario itself (the thing that produced 77.4%) never seeds StartMatch's rngSeed either,
    /// so an unseeded run is the faithful reproduction of what actually produced the real number,
    /// not an artificial control.
    /// </summary>
    public class ApprenticeMaxSpellWinShareDiagnosticTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

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
            var go = new GameObject("MaxShareDiagCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            db = CardDatabase.Instance;
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("MaxShareDiagBattleController");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        /// <summary>Same id-resolution rule as BattleController.ResolveMatchSpellbook (resolve
        /// each id against the full catalog, skip one that doesn't resolve) - duplicated here since
        /// that method is private, needed so SetEnemySpellbookForTests gets real AvatarSpell
        /// instances instead of just an id list.</summary>
        private static List<AvatarSpell> ResolveSpellIds(List<string> ids)
        {
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            return ids.Select(id => catalog.FirstOrDefault(s => s.Id == id)).Where(s => s != null).ToList();
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

        private sealed class RunResult
        {
            public int Trials;
            public int AiWins;
            public readonly Dictionary<string, int> WinContributionBySpell = new Dictionary<string, int>();

            public double MaxShare => AiWins == 0 || WinContributionBySpell.Count == 0
                ? 0 : (double)WinContributionBySpell.Values.Max() / AiWins;
            public string MaxSpellName => WinContributionBySpell.Count == 0
                ? "(none)" : WinContributionBySpell.OrderByDescending(kv => kv.Value).First().Key;
        }

        /// <summary>Same AiCasting(on,off) scenario and same AiWinContributionBySpell attribution
        /// rule as MirroredAiSimulationMatrixTests.RunScenario/ScenarioResult - every distinct
        /// spell the AI cast during a trial it went on to win gets +1 credit for that spell,
        /// matching the exact real definition MaxSingleSpellWinShare gates on.</summary>
        private RunResult RunApprenticeAiCastingScenario(List<Card> pool, int trials)
        {
            const int avatarLevel = 25;
            const int castleLevel = 15;
            const AIDifficultyTier tier = AIDifficultyTier.Apprentice;

            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel: 25);
            empire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);
            List<string> equippedIds = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Id).ToList();

            var result = new RunResult();

            for (int i = 0; i < trials; i++)
            {
                BattleController controller = CreateController();
                List<Card> playerDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                controller.StartMatch(playerDeck, enemyDeck, economy, economy,
                    avatarLevel, unlockedStageIds: null, equippedSpellIds: equippedIds, enemyTier: tier);
                controller.EnableMirroredEnemySpellsForPvE();
                controller.DealFormationHand(controller.PlayerState);
                controller.DealFormationHand(controller.EnemyState);
                DeployWholeSquad(controller, controller.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

                if (!controller.ConfirmFormation())
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                    continue;
                }

                while (controller.Phase == BattlePhase.Combat) controller.AdvanceCombatTick();

                result.Trials++;
                List<SpellCastRecord> aiCasts = controller.SpellCastLog.Where(c => !c.CastByPlayer).ToList();
                bool aiWon = controller.PlayerState.IsDefeated && !controller.EnemyState.IsDefeated;
                if (aiWon)
                {
                    result.AiWins++;
                    if (aiCasts.Count > 0)
                    {
                        foreach (string spellName in aiCasts.Select(c => c.SpellName).Distinct())
                        {
                            result.WinContributionBySpell.TryGetValue(spellName, out int cur);
                            result.WinContributionBySpell[spellName] = cur + 1;
                        }
                    }
                }

                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }

            return result;
        }

        [Test]
        public void Diagnose_ApprenticeMaxSingleSpellWinShare()
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();
            const int trialsPerRepeat = 3000;
            const int repeats = 5;

            var repeatResults = new List<RunResult>();
            for (int r = 0; r < repeats; r++)
            {
                Debug.Log($"[MaxShareDiag] Apprentice: repeat {r + 1}/{repeats}, {trialsPerRepeat} trials...");
                RunResult result = RunApprenticeAiCastingScenario(pool, trialsPerRepeat);
                repeatResults.Add(result);

                string breakdown = string.Join(", ", result.WinContributionBySpell
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}={kv.Value}/{result.AiWins} ({(result.AiWins == 0 ? 0 : (double)kv.Value / result.AiWins):P1})"));
                Debug.Log($"[MaxShareDiag] Apprentice: repeat {r + 1} trials={result.Trials} aiWins={result.AiWins} " +
                          $"maxShare={result.MaxShare:P1} maxSpell={result.MaxSpellName} | full breakdown: [{breakdown}]");
            }

            // ---------- Aggregate across all repeats (the real trial-count-confident number) ----------
            var pooled = new RunResult { Trials = repeatResults.Sum(r => r.Trials), AiWins = repeatResults.Sum(r => r.AiWins) };
            foreach (RunResult r in repeatResults)
            {
                foreach (var kv in r.WinContributionBySpell)
                {
                    pooled.WinContributionBySpell.TryGetValue(kv.Key, out int cur);
                    pooled.WinContributionBySpell[kv.Key] = cur + kv.Value;
                }
            }
            string pooledBreakdown = string.Join(", ", pooled.WinContributionBySpell
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}={kv.Value}/{pooled.AiWins} ({(pooled.AiWins == 0 ? 0 : (double)kv.Value / pooled.AiWins):P1})"));
            Debug.Log($"[MaxShareDiag] Apprentice: POOLED across {repeats} repeats, trials={pooled.Trials} aiWins={pooled.AiWins} " +
                      $"maxShare={pooled.MaxShare:P1} maxSpell={pooled.MaxSpellName} | full breakdown: [{pooledBreakdown}]");

            // ---------- Stability across repeats (is 77.4% typical, or a high-variance outlier?) ----------
            double[] shares = repeatResults.Select(r => r.MaxShare).ToArray();
            double meanShare = shares.Average();
            double variance = shares.Select(s => (s - meanShare) * (s - meanShare)).Average();
            double stdDev = System.Math.Sqrt(variance);
            Debug.Log($"[MaxShareDiag] Apprentice: per-repeat maxShare values=[{string.Join(", ", shares.Select(s => s.ToString("P1")))}] " +
                      $"mean={meanShare:P1} stdDev={stdDev:P1} min={shares.Min():P1} max={shares.Max():P1}");
            Debug.Log($"[MaxShareDiag] Apprentice: aiWins per repeat=[{string.Join(", ", repeatResults.Select(r => r.AiWins))}] " +
                      "(small aiWins denominator inflates variance in MaxSingleSpellWinShare independent of any real behavior change).");
        }

        /// <summary>Empirical validation (BS-vetted task, LOCKED) of AIEnemySpellbookResolver.
        /// ApplyApprenticeWindstepRemoval's Mend replacement: does the real, production
        /// AIEnemySpellbookResolver.ResolveSpellbook(Apprentice) loadout (Windstep gone, Mend
        /// added - no manual override needed here, this uses the exact same tier-resolution
        /// path a real match does) actually cast Mend a real, non-shadowed amount, and does
        /// its overall AI win rate stay inside the healthy range rather than regressing back
        /// toward the old Windstep-included number?</summary>
        [Test]
        public void WindstepAblation_ApprenticeReplacementValidation()
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();
            const int trials = 3000;
            const int baseSeed = 790001;

            List<string> newLoadoutNames = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Apprentice).Select(s => s.Name).ToList();
            Debug.Log($"[ReplacementValidation] Apprentice real resolved loadout: {string.Join(", ", newLoadoutNames)}.");
            Assert.IsFalse(newLoadoutNames.Contains("Windstep"), "Setup: Windstep should be removed from Apprentice's real resolved loadout.");
            Assert.Contains("Mend", newLoadoutNames, "Setup: Mend should be the real replacement.");
            Assert.LessOrEqual(newLoadoutNames.Count(n => n == "Divine Bolt" || n == "Stone Judgment" || n == "Blood Price"), 1,
                "MOS's max-1-AvatarStrike-equipped lock must hold for the replacement too.");

            (double winRate, int aiWins, int completedTrials, Dictionary<string, int> castCounts, int totalCasts) =
                RunRealApprenticeLoadout(pool, trials, baseSeed);

            Debug.Log($"[ReplacementValidation] Apprentice: winRate={winRate:P1} ({aiWins}/{completedTrials}), totalAiCasts={totalCasts}, " +
                      $"perSpellCasts=[{string.Join(", ", castCounts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}"))}].");

            castCounts.TryGetValue("Mend", out int mendCasts);
            castCounts.TryGetValue("Renewal", out int renewalCasts);
            Debug.Log($"[ReplacementValidation] Apprentice: Mend casts={mendCasts} ({(totalCasts == 0 ? 0 : (double)mendCasts / totalCasts):P1} of all AI casts), " +
                      $"Renewal casts={renewalCasts} - Mend NOT shadowed to zero means this is a real replacement, not a dead slot like Ember Wave was.");
            Assert.Greater(mendCasts, 0, "Mend got zero real casts - it's shadowed exactly like Ember Wave was in the earlier ablation's condition D, not a real replacement.");

            // The earlier corrected ablation measured baseline (spells off) at ~31.4-33.7% across
            // its own repeats/seeds for Apprentice - reusing that same ballpark rather than
            // re-running a fresh baseline here, since this test's real question is "does the NEW
            // loadout's win rate look like the healthy 'Windstep removed' result (~38%) or drift
            // back toward the old 'Windstep included' result (~31-34%)."
            Debug.Log($"[ReplacementValidation] Apprentice: INTERPRETATION - new loadout winRate={winRate:P1} vs the corrected ablation's " +
                      "own reference points (0fdd193: ~31-34% with Windstep, ~35-38% without) - " +
                      (winRate >= 0.35
                          ? "consistent with the healthy 'Windstep removed' range, not a regression back toward the old number."
                          : "LOWER than the healthy range - Mend's addition may be dragging win rate back down, needs a closer look before treating this as settled."));
        }

        private (double winRate, int aiWins, int completedTrials, Dictionary<string, int> castCounts, int totalCasts) RunRealApprenticeLoadout(
            List<Card> pool, int trials, int baseSeed)
        {
            const int avatarLevel = 25;
            const int castleLevel = 15;
            const AIDifficultyTier tier = AIDifficultyTier.Apprentice;

            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel: 25);
            empire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);

            int aiWins = 0;
            int completedTrials = 0;
            int totalCasts = 0;
            var castCounts = new Dictionary<string, int>();

            for (int i = 0; i < trials; i++)
            {
                if (i % 500 == 0) Debug.Log($"[ReplacementValidation] trial {i}/{trials}");
                int seed = baseSeed + i;
                UnityEngine.Random.InitState(seed);

                BattleController controller = CreateController();
                List<Card> playerDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                // Real production path: enemyTier: Apprentice resolves the enemy spellbook via
                // AIEnemySpellbookResolver.ResolveSpellbook, which now includes the Windstep
                // removal/Mend replacement internally - no test-only override needed here.
                controller.StartMatch(playerDeck, enemyDeck, economy, economy,
                    avatarLevel, unlockedStageIds: null, equippedSpellIds: null, enemyTier: tier, rngSeed: seed);
                controller.EnableMirroredEnemySpellsForPvE();
                controller.DealFormationHand(controller.PlayerState);
                controller.DealFormationHand(controller.EnemyState);
                DeployWholeSquad(controller, controller.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

                if (!controller.ConfirmFormation())
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                    continue;
                }

                while (controller.Phase == BattlePhase.Combat) controller.AdvanceCombatTick();

                completedTrials++;
                if (controller.PlayerState.IsDefeated && !controller.EnemyState.IsDefeated) aiWins++;

                foreach (SpellCastRecord cast in controller.SpellCastLog.Where(c => !c.CastByPlayer))
                {
                    totalCasts++;
                    castCounts.TryGetValue(cast.SpellName, out int cur);
                    castCounts[cast.SpellName] = cur + 1;
                }

                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }

            double winRate = completedTrials == 0 ? 0 : (double)aiWins / completedTrials;
            return (winRate, aiWins, completedTrials, castCounts, totalCasts);
        }

        /// <summary>Causal check, same pattern as VeteranPlusWindstepAblationTests
        /// (WindstepAblation_FourConditions, conditions A/B): the diagnostic above shows Windstep
        /// dominates Apprentice's MaxSingleSpellWinShare (76.0% pooled, stable), but co-occurring
        /// with a win is not the same as causing it - the earlier VeteranPlus ablation found
        /// exactly that gap (79.4% win-share, &lt;1pp real win-rate impact when removed). This
        /// re-runs that same causal test for Apprentice specifically rather than assuming the
        /// VeteranPlus result transfers: matched-seed, current loadout (Windstep included) vs
        /// Windstep removed (slot left empty, not backfilled), same AI win-rate comparison.</summary>
        [Test]
        public void WindstepAblation_ApprenticeTwoConditions()
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();
            const int trialsPerRepeat = 2000;
            const int repeats = 3;
            int[] baseSeeds = { 720001, 750001, 780001 };

            List<string> baseIds = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Apprentice).Select(s => s.Id).ToList();
            Debug.Log($"[Ablation] Apprentice condition A base loadout: {string.Join(", ", baseIds)}");
            Assert.Contains("windstep", baseIds, "Setup: this ablation assumes Apprentice's loadout still includes Windstep.");
            List<string> idsNoWindstep = baseIds.Where(id => id != "windstep").ToList();

            int aiWinsAPooled = 0, trialsAPooled = 0, aiWinsBPooled = 0, trialsBPooled = 0;
            var perRepeatDeltas = new List<double>();

            for (int r = 0; r < repeats; r++)
            {
                (int aiWinsA, int trialsA) = RunAblationCondition(pool, $"A_ApprenticeWithWindstep_r{r}", baseIds, trialsPerRepeat, baseSeeds[r]);
                (int aiWinsB, int trialsB) = RunAblationCondition(pool, $"B_ApprenticeWindstepRemoved_r{r}", idsNoWindstep, trialsPerRepeat, baseSeeds[r]);

                double winRateA = trialsA == 0 ? 0 : (double)aiWinsA / trialsA;
                double winRateB = trialsB == 0 ? 0 : (double)aiWinsB / trialsB;
                double deltaPp = winRateB - winRateA;
                perRepeatDeltas.Add(deltaPp);
                Debug.Log($"[Ablation] Apprentice: repeat {r + 1}/{repeats} A(withWindstep) winRate={winRateA:P1} ({aiWinsA}/{trialsA}) vs " +
                          $"B(windstepRemoved) winRate={winRateB:P1} ({aiWinsB}/{trialsB}) - delta={deltaPp:P1}.");

                aiWinsAPooled += aiWinsA; trialsAPooled += trialsA;
                aiWinsBPooled += aiWinsB; trialsBPooled += trialsB;
            }

            double pooledWinRateA = trialsAPooled == 0 ? 0 : (double)aiWinsAPooled / trialsAPooled;
            double pooledWinRateB = trialsBPooled == 0 ? 0 : (double)aiWinsBPooled / trialsBPooled;
            double pooledDeltaPp = pooledWinRateB - pooledWinRateA;
            // Wilson-adjacent normal-approximation SE on the pooled two-proportion difference -
            // enough to judge "is this delta distinguishable from zero," not a formal hypothesis test.
            double seA = trialsAPooled == 0 ? 0 : System.Math.Sqrt(pooledWinRateA * (1 - pooledWinRateA) / trialsAPooled);
            double seB = trialsBPooled == 0 ? 0 : System.Math.Sqrt(pooledWinRateB * (1 - pooledWinRateB) / trialsBPooled);
            double seDelta = System.Math.Sqrt(seA * seA + seB * seB);
            double zScore = seDelta == 0 ? 0 : pooledDeltaPp / seDelta;

            Debug.Log($"[Ablation] Apprentice: POOLED across {repeats} repeats ({trialsAPooled} trials/condition) " +
                      $"A(withWindstep)={pooledWinRateA:P1} B(windstepRemoved)={pooledWinRateB:P1} delta={pooledDeltaPp:P1} " +
                      $"SE(delta)={seDelta:P1} z={zScore:F2}.");
            Debug.Log($"[Ablation] Apprentice: per-repeat deltas=[{string.Join(", ", perRepeatDeltas.Select(d => d.ToString("P1")))}].");
            Debug.Log($"[Ablation] Apprentice: INTERPRETATION - " +
                      (System.Math.Abs(zScore) < 1.96
                          ? $"pooled delta {pooledDeltaPp:P1} is within ~2 SE of zero (z={zScore:F2}) - not distinguishable from noise " +
                            "at this trial count. Same practical conclusion as before (Windstep's win-share dominance is availability " +
                            "bias, not a proven causal win-rate driver) but now on a methodologically valid measurement, not the broken one."
                          : $"pooled delta {pooledDeltaPp:P1} is {zScore:F2} SE from zero - distinguishable from noise. This is a real " +
                            "signal that needs escalation, not something to fold into the earlier availability-bias conclusion."));
        }

        private (int aiWins, int trials) RunAblationCondition(List<Card> pool, string label, List<string> equippedIds, int trials, int baseSeed)
        {
            const int avatarLevel = 25;
            const int castleLevel = 15;
            const AIDifficultyTier tier = AIDifficultyTier.Apprentice;

            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel: 25);
            empire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);

            int aiWins = 0;
            int completedTrials = 0;

            for (int i = 0; i < trials; i++)
            {
                if (i % 300 == 0) Debug.Log($"[Ablation] {label}: trial {i}/{trials}");
                int seed = baseSeed + i;
                UnityEngine.Random.InitState(seed);

                BattleController controller = CreateController();
                List<Card> playerDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                // BUG FIX #1 (real, found while implementing SpellRemovalWinRateDelta): enemyTier:
                // tier here made BattleController.StartMatch re-resolve EnemySpellbook from
                // AIEnemySpellbookResolver, IGNORING equippedIds entirely for the enemy side (see
                // StartMatch's own `enemyTier.HasValue ? ResolveSpellbook(...) : ...` branch) - so
                // "Windstep removed" never actually removed it from the AI.
                //
                // BUG FIX #2 (also real, found immediately after #1 while fixing VeteranPlus's copy
                // of the same pattern): the naive fix of enemyTier: null so equippedIds resolves for
                // the enemy ALSO nulls EnemyDifficultyTier, which silently changes the AI's cast-
                // probability gate to the tier-agnostic default (0.40) instead of Apprentice's real
                // 0.85 - a second, separate confound stacked on top of the first, present in every
                // number this method produced between the #1 fix and this one. Fixed by keeping
                // enemyTier real (genuine EnemyDifficultyTier/gate probability) and overwriting
                // EnemySpellbook afterward via BattleController.SetEnemySpellbookForTests instead.
                controller.StartMatch(playerDeck, enemyDeck, economy, economy,
                    avatarLevel, unlockedStageIds: null, equippedSpellIds: null, enemyTier: tier, rngSeed: seed);
                controller.SetEnemySpellbookForTests(ResolveSpellIds(equippedIds));
                controller.EnableMirroredEnemySpellsForPvE();
                controller.DealFormationHand(controller.PlayerState);
                controller.DealFormationHand(controller.EnemyState);
                DeployWholeSquad(controller, controller.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

                if (!controller.ConfirmFormation())
                {
                    UnityEngine.Object.DestroyImmediate(controller.gameObject);
                    continue;
                }

                while (controller.Phase == BattlePhase.Combat) controller.AdvanceCombatTick();

                completedTrials++;
                if (controller.PlayerState.IsDefeated && !controller.EnemyState.IsDefeated) aiWins++;

                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }

            return (aiWins, completedTrials);
        }
    }
}
