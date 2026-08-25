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
    /// AI-balance diagnose-before-tune, step 4 (LOCKED 2026-08-25, GPT): the per-spell impact
    /// diagnostic on the dead-loadout-slot-fixed VeteranPlus loadout found Windstep (Reposition),
    /// not Stone Judgment, is the dominant spell (78.1% win-share, 75.2% win-rate-when-cast) - but
    /// win-rate-when-cast alone does not prove causation (GPT: Windstep might just be selected
    /// mostly when the AI is already winning - availability bias - not actually deciding the
    /// match). Four conditions, identical per-trial seeds across all of them (both
    /// UnityEngine.Random for deck/hand and BattleController's own MatchRngSeed for AI-cast
    /// rolls), so any measured difference between conditions is attributable to the loadout
    /// change itself, not trial-to-trial variance:
    ///
    /// A) Current corrected loadout, Windstep included (real baseline for this ablation).
    /// B) Windstep removed, slot left empty (4 spells) - simplest test of "does removing it change
    ///    the outcome."
    /// C) ALL Reposition spells excluded from both the equipped list AND AIEnemySpellbookResolver's
    ///    own candidate pool (not just Windstep specifically) - rules out "a different Reposition
    ///    spell like Seismic Swap would have silently taken over the slot" as a confound. For
    ///    VeteranPlus specifically this is expected to converge on the same 4-spell list as B,
    ///    since SelectHighestMagnitudePerEffect only ever lets ONE spell win a given effect type's
    ///    slot - reported explicitly, not assumed.
    /// D) Windstep replaced with Ember Wave (LaneDamage, energyCost 26/cooldown 3 - exact cost/
    ///    cadence match to Windstep's own 26/3, the closest real cost/role-matched substitute in
    ///    the catalog) - tests whether ANY cheap, fast-cadence spell in Windstep's slot would
    ///    dominate similarly, or whether Windstep's Reposition mechanic specifically is the cause.
    /// </summary>
    public class VeteranPlusWindstepAblationTests
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
            var go = new GameObject("AblationCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            db = CardDatabase.Instance;
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("AblationBattleController");
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

        private static int LivingCount(PlayerBattleState side) =>
            side.Lanes[Lane.Front].Cards.Count(c => c.IsAlive) +
            side.Lanes[Lane.Middle].Cards.Count(c => c.IsAlive) +
            side.Lanes[Lane.Back].Cards.Count(c => c.IsAlive);

        /// <summary>Same id-resolution rule as BattleController.ResolveMatchSpellbook (resolve
        /// each id against the full catalog, skip one that doesn't resolve) - duplicated here since
        /// that method is private, needed so SetEnemySpellbookForTests gets real AvatarSpell
        /// instances instead of just an id list.</summary>
        private static List<AvatarSpell> ResolveSpellIds(List<string> ids)
        {
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            return ids.Select(id => catalog.FirstOrDefault(s => s.Id == id)).Where(s => s != null).ToList();
        }

        private sealed class ConditionResult
        {
            public string Label;
            public int Trials;
            public int AiWins;
            public int PlayerWins;
            public long TotalTicks;
            public long TicksObserved;
            public long OrdinaryAvailableTicks;
            public int TotalCasts;
            public int TrialsWithAnyCast;
            public int TrialsWithZeroCasts;
            public int SlotSpellCasts;
            public int SlotSpellTrialsCastIn;
            public int SlotSpellWinsWhenCast;

            public void Log(string slotSpellName)
            {
                double aiWinRate = Trials == 0 ? 0 : (double)AiWins / Trials;
                double playerWinRate = Trials == 0 ? 0 : (double)PlayerWins / Trials;
                double avgTicks = Trials == 0 ? 0 : (double)TotalTicks / Trials;
                double spellsPerMatch = Trials == 0 ? 0 : (double)TotalCasts / Trials;
                double castConversion = Trials == 0 ? 0 : (double)TrialsWithAnyCast / Trials;
                double noSpellFallback = Trials == 0 ? 0 : (double)TrialsWithZeroCasts / Trials;
                double candidateAvailability = TicksObserved == 0 ? 0 : (double)OrdinaryAvailableTicks / TicksObserved;
                double slotWinRateWhenCast = SlotSpellTrialsCastIn == 0 ? 0 : (double)SlotSpellWinsWhenCast / SlotSpellTrialsCastIn;
                double slotShareOfAiWins = AiWins == 0 ? 0 : (double)SlotSpellWinsWhenCast / AiWins;

                Debug.Log($"[Ablation] {Label}: trials={Trials} aiWinRate={aiWinRate:P1} playerWinRate={playerWinRate:P1} avgTicks={avgTicks:F2} " +
                          $"spellsPerMatch={spellsPerMatch:F2} castConversion={castConversion:P1} noSpellFallback={noSpellFallback:P1} " +
                          $"legalCandidateAvailability={candidateAvailability:P1}");
                Debug.Log($"[Ablation] {Label}: SLOT_SPELL={slotSpellName} casts={SlotSpellCasts} trialsCastIn={SlotSpellTrialsCastIn} " +
                          $"winRateWhenCast={slotWinRateWhenCast:P1} shareOfAiWins={slotShareOfAiWins:P1}");
            }
        }

        private ConditionResult RunCondition(List<Card> pool, string label, List<string> equippedIds, string slotSpellName, int trials, int baseSeed)
        {
            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel: 50, castleLevel: 30, barracksLevel: 25);
            empire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);
            const AIDifficultyTier tier = AIDifficultyTier.Veteran;

            var result = new ConditionResult { Label = label };

            for (int i = 0; i < trials; i++)
            {
                if (i % 200 == 0) Debug.Log($"[Ablation] {label}: trial {i}/{trials}");

                int seed = baseSeed + i;
                UnityEngine.Random.InitState(seed);

                BattleController controller = CreateController();
                List<Card> playerDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => UnityEngine.Random.value).Take(empire.DeckSlotCount).ToList();

                // BUG FIX (real, found while implementing SpellRemovalWinRateDelta, facfe8a):
                // enemyTier: tier here made BattleController.StartMatch re-resolve EnemySpellbook
                // from AIEnemySpellbookResolver, IGNORING equippedIds entirely for the enemy side
                // (see StartMatch's own `enemyTier.HasValue ? ResolveSpellbook(...) : ...` branch) -
                // so every condition (A/B/C/D) secretly ran the AI's real, unmodified loadout the
                // whole time. "Windstep removed" never removed it. This invalidates every number
                // this method previously produced.
                //
                // SECOND BUG FIX (also real, found immediately after the first): the naive fix of
                // passing enemyTier: null so equippedIds resolves for the enemy ALSO nulls
                // EnemyDifficultyTier, which silently changes the AI's own cast-probability gate to
                // the tier-agnostic default (0.40) instead of Veteran's real 0.45
                // (NonAvatarStrikeGateProbability switches on EnemyDifficultyTier) - a second,
                // separate confound stacked on top of the first. Fixed by keeping enemyTier real
                // (so EnemyDifficultyTier/gate probability stay genuine) and overwriting
                // EnemySpellbook afterward via the new SetEnemySpellbookForTests seam instead.
                controller.StartMatch(playerDeck, enemyDeck, economy, economy,
                    avatarLevel: 50, unlockedStageIds: null, equippedSpellIds: null, enemyTier: tier, rngSeed: seed);
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

                var castsThisTrial = new List<string>();

                while (controller.Phase == BattlePhase.Combat)
                {
                    int predictedTick = controller.TickCount + 1;
                    int predictedEnergy = Math.Min(controller.MaxEnergy,
                        controller.EnemyEnergy + controller.EnergyPerTick + BattleController.BackLaneEnergy(controller.EnemyState));

                    AISpellCaster.DiagnoseCandidates(
                        controller.EnemySpellbook, predictedEnergy, predictedTick,
                        controller.EnemyState, controller.PlayerState,
                        out int ordinaryAvailable, out _, out _, out _,
                        out _, out _, out _, out _, tier);
                    result.TicksObserved++;
                    if (ordinaryAvailable > 0) result.OrdinaryAvailableTicks++;

                    int castLogBefore = controller.SpellCastLog.Count;
                    int livingBefore = LivingCount(controller.PlayerState);
                    controller.AdvanceCombatTick();
                    int livingAfter = LivingCount(controller.PlayerState);
                    int castLogAfter = controller.SpellCastLog.Count;

                    if (castLogAfter > castLogBefore)
                    {
                        SpellCastRecord record = controller.SpellCastLog[castLogAfter - 1];
                        if (!record.CastByPlayer) castsThisTrial.Add(record.SpellName);
                    }
                }

                result.Trials++;
                result.TotalTicks += controller.TickCount;
                bool aiWon = controller.PlayerState.IsDefeated && !controller.EnemyState.IsDefeated;
                bool playerWon = controller.EnemyState.IsDefeated && !controller.PlayerState.IsDefeated;
                if (aiWon) result.AiWins++;
                if (playerWon) result.PlayerWins++;

                result.TotalCasts += castsThisTrial.Count;
                if (castsThisTrial.Count > 0) result.TrialsWithAnyCast++;
                else result.TrialsWithZeroCasts++;

                if (slotSpellName != null)
                {
                    int slotCasts = castsThisTrial.Count(c => c == slotSpellName);
                    result.SlotSpellCasts += slotCasts;
                    if (slotCasts > 0)
                    {
                        result.SlotSpellTrialsCastIn++;
                        if (aiWon) result.SlotSpellWinsWhenCast++;
                    }
                }

                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }

            result.Log(slotSpellName ?? "(none)");
            return result;
        }

        [Test]
        public void WindstepAblation_FourConditions()
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();
            const int trials = 1500;
            const int baseSeed = 700001;

            List<string> baseIds = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Veteran).Select(s => s.Id).ToList();
            Debug.Log($"[Ablation] Condition A base loadout (should include windstep): {string.Join(", ", baseIds)}");
            Assert.Contains("windstep", baseIds, "Setup: this ablation assumes the dead-slot-fixed VeteranPlus loadout still includes Windstep.");

            // A) current corrected loadout, Windstep included.
            RunCondition(pool, "A_CurrentWithWindstep", baseIds, "Windstep", trials, baseSeed);

            // B) Windstep removed, slot left empty.
            List<string> idsNoWindstep = baseIds.Where(id => id != "windstep").ToList();
            RunCondition(pool, "B_WindstepRemoved", idsNoWindstep, null, trials, baseSeed);

            // C) ALL Reposition spells excluded from equipped AND candidate pool - re-derive via
            // the resolver's own selection with Reposition unavailable, not just hand-edited.
            List<string> idsNoReposition = AllCastableIdsExcluding(SpellEffect.Reposition, AIDifficultyTier.Veteran);
            Debug.Log($"[Ablation] Condition C loadout (Reposition excluded at the candidate-pool level): {string.Join(", ", idsNoReposition)}");
            RunCondition(pool, "C_AllRepositionExcludedFromCandidatePool", idsNoReposition, null, trials, baseSeed);

            // D) Windstep replaced with Ember Wave (cost/cadence-matched: both 26 energy / 3-tick cooldown).
            List<string> idsWithEmberWave = idsNoWindstep.Concat(new[] { "ember_wave" }).ToList();
            RunCondition(pool, "D_WindstepReplacedWithEmberWave", idsWithEmberWave, "Ember Wave", trials, baseSeed);
        }

        /// <summary>Re-measurement (BS-vetted, LOCKED) after facfe8a found the enemyTier bug that
        /// invalidated WindstepAblation_FourConditions' original A/B numbers (and the same bug's
        /// second half - EnemyDifficultyTier/gate-probability confound - fixed in RunCondition
        /// above). Apprentice's corrected re-measurement reversed its own "availability bias"
        /// conclusion (facfe8a: 4.2pp real delta, z=4.75) - don't assume VeteranPlus's original
        /// &lt;1pp answer transfers either way; measure it properly. Same rigor as Apprentice's
        /// corrected version: 3 independent repeats x 2000 matched-seed trials/condition, distinct
        /// base seeds, pooled two-proportion z-score.</summary>
        [Test]
        public void WindstepAblation_VeteranPlusCorrectedTwoConditions()
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();
            const int trialsPerRepeat = 2000;
            const int repeats = 3;
            int[] baseSeeds = { 710001, 740001, 770001 };

            List<string> baseIds = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Veteran).Select(s => s.Id).ToList();
            Assert.Contains("windstep", baseIds, "Setup: this ablation assumes VeteranPlus's loadout still includes Windstep.");
            List<string> idsNoWindstep = baseIds.Where(id => id != "windstep").ToList();

            int aiWinsAPooled = 0, trialsAPooled = 0, aiWinsBPooled = 0, trialsBPooled = 0;
            var perRepeatDeltas = new List<double>();

            for (int r = 0; r < repeats; r++)
            {
                ConditionResult a = RunCondition(pool, $"A_VeteranPlusWithWindstep_r{r}", baseIds, "Windstep", trialsPerRepeat, baseSeeds[r]);
                ConditionResult b = RunCondition(pool, $"B_VeteranPlusWindstepRemoved_r{r}", idsNoWindstep, null, trialsPerRepeat, baseSeeds[r]);

                double winRateA = a.Trials == 0 ? 0 : (double)a.AiWins / a.Trials;
                double winRateB = b.Trials == 0 ? 0 : (double)b.AiWins / b.Trials;
                double deltaPp = winRateB - winRateA;
                perRepeatDeltas.Add(deltaPp);
                Debug.Log($"[Ablation] VeteranPlus: repeat {r + 1}/{repeats} A(withWindstep) winRate={winRateA:P1} ({a.AiWins}/{a.Trials}) vs " +
                          $"B(windstepRemoved) winRate={winRateB:P1} ({b.AiWins}/{b.Trials}) - delta={deltaPp:P1}.");

                aiWinsAPooled += a.AiWins; trialsAPooled += a.Trials;
                aiWinsBPooled += b.AiWins; trialsBPooled += b.Trials;
            }

            double pooledWinRateA = trialsAPooled == 0 ? 0 : (double)aiWinsAPooled / trialsAPooled;
            double pooledWinRateB = trialsBPooled == 0 ? 0 : (double)aiWinsBPooled / trialsBPooled;
            double pooledDeltaPp = pooledWinRateB - pooledWinRateA;
            double seA = trialsAPooled == 0 ? 0 : Math.Sqrt(pooledWinRateA * (1 - pooledWinRateA) / trialsAPooled);
            double seB = trialsBPooled == 0 ? 0 : Math.Sqrt(pooledWinRateB * (1 - pooledWinRateB) / trialsBPooled);
            double seDelta = Math.Sqrt(seA * seA + seB * seB);
            double zScore = seDelta == 0 ? 0 : pooledDeltaPp / seDelta;

            Debug.Log($"[Ablation] VeteranPlus: POOLED across {repeats} repeats ({trialsAPooled} trials/condition) " +
                      $"A(withWindstep)={pooledWinRateA:P1} B(windstepRemoved)={pooledWinRateB:P1} delta={pooledDeltaPp:P1} " +
                      $"SE(delta)={seDelta:P1} z={zScore:F2}.");
            Debug.Log($"[Ablation] VeteranPlus: per-repeat deltas=[{string.Join(", ", perRepeatDeltas.Select(d => d.ToString("P1")))}].");
            Debug.Log($"[Ablation] VeteranPlus: INTERPRETATION - " +
                      (Math.Abs(zScore) < 1.96
                          ? $"pooled delta {pooledDeltaPp:P1} is within ~2 SE of zero (z={zScore:F2}) - not distinguishable from noise " +
                            "at this trial count, now on a methodologically valid (real EnemyDifficultyTier + real spellbook override) measurement."
                          : $"pooled delta {pooledDeltaPp:P1} is {zScore:F2} SE from zero - distinguishable from noise, a real signal " +
                            "that needs escalation."));
        }

        /// <summary>Re-derives the AvatarLevel/stage-gated pool AIEnemySpellbookResolver itself
        /// uses, but with one SpellEffect entirely excluded before slot selection - lets condition
        /// C prove no other spell of the excluded effect (e.g. Seismic Swap) would have silently
        /// taken over the freed slot, rather than assuming it from B's hand-edited list alone.</summary>
        private static List<string> AllCastableIdsExcluding(SpellEffect excluded, AIDifficultyTier tier)
        {
            List<AvatarSpell> catalog = AvatarSpell.CreatePhase1Catalog();
            int repLevel = tier switch
            {
                AIDifficultyTier.Novice => 10,
                AIDifficultyTier.Apprentice => 25,
                AIDifficultyTier.Veteran => 50,
                AIDifficultyTier.Master => 80,
                AIDifficultyTier.Titan => 999,
                _ => 1,
            };
            List<AvatarSpell> avatarLevelPool = SpellUnlockResolver.ResolveUnlockedSpells(repLevel, unlockedStageIds: null);
            string[] stageGatedIds = tier switch
            {
                AIDifficultyTier.Novice => new[] { "cinder_lash", "vital_spark" },
                AIDifficultyTier.Apprentice => new[] { "cinder_lash", "vital_spark", "fault_line", "renewal" },
                AIDifficultyTier.Veteran => new[] { "cinder_lash", "vital_spark", "fault_line", "renewal", "sun_lance", "banner_of_ashes" },
                AIDifficultyTier.Master or AIDifficultyTier.Titan => new[] { "cinder_lash", "vital_spark", "fault_line", "renewal", "sun_lance", "banner_of_ashes", "tempest_brand" },
                _ => Array.Empty<string>(),
            };
            List<AvatarSpell> stageGatedPool = catalog.Where(s => stageGatedIds.Contains(s.Id)).ToList();

            var aiCastableEffects = new HashSet<SpellEffect>
            {
                SpellEffect.LaneHeal, SpellEffect.LaneDamage, SpellEffect.LaneAttackBuff, SpellEffect.AvatarStrike, SpellEffect.Reposition,
            };
            aiCastableEffects.Remove(excluded);

            List<AvatarSpell> filteredPool = avatarLevelPool.Concat(stageGatedPool).Distinct()
                .Where(s => aiCastableEffects.Contains(s.Effect)).ToList();
            int slotCount = SpellLoadoutAutoEquip.RequiredSlotCount(repLevel);
            return SpellLoadoutAutoEquip.SelectHighestMagnitudePerEffect(filteredPool, slotCount).Select(s => s.Id).ToList();
        }
    }
}
