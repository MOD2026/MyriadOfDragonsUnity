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
    /// Balance simulation, run headlessly through the normal EditMode test runner.
    ///
    /// This exists because balance questions were previously answered by reasoning, and reasoning
    /// got one badly wrong: the Avatar Health pool sat roughly 4x too high for months, which meant
    /// most matches never produced a knockout at all - they ran out the tick clock and were
    /// settled on a Health percentage. Nobody noticed, because every unit test passed. Unit tests
    /// verify that a rule is applied correctly; they say nothing about whether the resulting game
    /// is any good.
    ///
    /// The important property here is that this drives the REAL combat code - the same
    /// LaneBattleResolver, the same card database, the same slot weighting and elemental rules
    /// that ship. An external model (a spreadsheet, or a Python replica) has to duplicate all of
    /// that, and the duplicate silently drifts from the game the moment either side changes.
    ///
    /// The assertions deliberately check broad *ranges*, not exact figures. A balance change
    /// should not fail the build; a balance change that makes matches undecidable should.
    /// </summary>
    public class BalanceSimulationTests
    {
        private const int MatchesPerRun = 400;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            // Combat rule toggles are mutable statics. Leaving one flipped would make every later
            // test in the run measure a different game than the one that ships.
            LaneBattleResolver.ResetRulesToDefault();

            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private CardDatabase LoadDatabase()
        {
            var go = new GameObject("SimCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("SimBattleController");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        private struct SimResult
        {
            public int Matches;
            public int Knockouts;      // a match where an Avatar actually reached 0
            public int CapDecided;     // a match settled by the tick cap instead
            public float AverageTicks;

            public float KnockoutRate => Matches == 0 ? 0f : (float)Knockouts / Matches;
            public float CapRate => Matches == 0 ? 0f : (float)CapDecided / Matches;
        }

        /// <summary>
        /// Plays `count` complete matches at the supplied Empire profile and reports how they
        /// ended. Deployment uses the same archetype-driven AI for both sides, so neither side is
        /// handicapped by a worse deployment policy and the result reflects the *maths* rather
        /// than a lopsided opponent.
        /// </summary>
        /// <param name="pool">Loaded once by the caller. CardDatabase is a singleton - a second
        /// instance is rejected in its own Awake/Initialize guard and comes back with an empty
        /// card list, so a test that simulated two profiles used to silently get zero usable
        /// matches on the second one and report a 0% knockout rate. Passing the pool in makes
        /// that impossible rather than merely unlikely.</param>
        private SimResult Simulate(List<Card> pool, int avatarLevel, int castleLevel, int count)
        {

            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel: 25);
            empire.InitializeTCGModifiers();

            var economy = new BattleController.MatchEconomy(
                empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);

            int knockouts = 0;
            int capDecided = 0;
            int totalTicks = 0;

            for (int i = 0; i < count; i++)
            {
                BattleController controller = CreateController();

                // Fresh shuffled deck per side per match - PlayerBattleState shuffles internally.
                List<Card> playerDeck = pool.OrderBy(_ => Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => Random.value).Take(empire.DeckSlotCount).ToList();

                controller.StartMatch(playerDeck, enemyDeck, economy, economy);
                controller.DealFormationHand(controller.PlayerState);
                controller.DealFormationHand(controller.EnemyState);

                DeployWholeSquad(controller, controller.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

                if (!controller.ConfirmFormation())
                {
                    // Nothing deployable - not a meaningful sample, skip it rather than count it.
                    Object.DestroyImmediate(controller.gameObject);
                    continue;
                }

                while (controller.Phase == BattlePhase.Combat)
                {
                    controller.AdvanceCombatTick();
                }

                totalTicks += controller.TickCount;
                bool someoneDied = controller.PlayerState.IsDefeated || controller.EnemyState.IsDefeated;
                if (someoneDied) knockouts++; else capDecided++;

                Object.DestroyImmediate(controller.gameObject);
            }

            int matches = knockouts + capDecided;
            return new SimResult
            {
                Matches = matches,
                Knockouts = knockouts,
                CapDecided = capDecided,
                AverageTicks = matches == 0 ? 0f : (float)totalTicks / matches,
            };
        }

        /// <summary>
        /// Same simulation loop as <see cref="Simulate"/>, but takes the match economy directly
        /// instead of deriving it from Empire levels - needed to sweep a candidate starting Avatar
        /// Health in isolation (see the level-1 onboarding sweep below) without also having to
        /// invent a PlayerEmpireData formula for a number that might not survive the sweep.
        /// </summary>
        private SimResult SimulateWithEconomy(List<Card> pool, BattleController.MatchEconomy economy,
            int deckSize, int count)
        {
            int knockouts = 0;
            int capDecided = 0;
            int totalTicks = 0;

            for (int i = 0; i < count; i++)
            {
                BattleController controller = CreateController();

                List<Card> playerDeck = pool.OrderBy(_ => Random.value).Take(deckSize).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => Random.value).Take(deckSize).ToList();

                controller.StartMatch(playerDeck, enemyDeck, economy, economy);
                controller.DealFormationHand(controller.PlayerState);
                controller.DealFormationHand(controller.EnemyState);

                DeployWholeSquad(controller, controller.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

                if (!controller.ConfirmFormation())
                {
                    Object.DestroyImmediate(controller.gameObject);
                    continue;
                }

                while (controller.Phase == BattlePhase.Combat)
                {
                    controller.AdvanceCombatTick();
                }

                totalTicks += controller.TickCount;
                bool someoneDied = controller.PlayerState.IsDefeated || controller.EnemyState.IsDefeated;
                if (someoneDied) knockouts++; else capDecided++;

                Object.DestroyImmediate(controller.gameObject);
            }

            int matches = knockouts + capDecided;
            return new SimResult
            {
                Matches = matches,
                Knockouts = knockouts,
                CapDecided = capDecided,
                AverageTicks = matches == 0 ? 0f : (float)totalTicks / matches,
            };
        }

        /// <summary>
        /// The level-1 onboarding question from docs/Mechanics_Gap_Analysis.md §1.3 (T2c): a
        /// genuine 1/1/1 new-player profile resolves matches in ~3 ticks against 18 Energy/tick
        /// accrual and a 25-Energy cheapest spell, which leaves at most one tick of margin for a
        /// spell to ever come off cooldown before the match is already over. This sweeps candidate
        /// starting Avatar Health values, holding ResourceCap/DeckSlotCount at the real level-1
        /// values (only Health is a free variable here), to find out whether raising just the
        /// early-game Health floor buys a playable onboarding curve without needing to touch
        /// anything else. Does not decide anything by itself - same role as the siege sweep.
        /// </summary>
        [Test]
        public void Balance_Level1StartingHealth_SweepForOnboardingViability()
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();

            var level1 = new PlayerEmpireData();
            level1.SetLevelsForTesting(avatarLevel: 1, castleLevel: 1, barracksLevel: 1);
            level1.InitializeTCGModifiers();

            int[] candidateHealth = { 100, 150, 200, 250, 300 }; // 100 is the current shipped value

            foreach (int hp in candidateHealth)
            {
                var economy = new BattleController.MatchEconomy(level1.ResourceCap, level1.Turn1Resource, hp);
                SimResult result = SimulateWithEconomy(pool, economy, level1.DeckSlotCount, MatchesPerRun);

                Debug.Log($"[Level1HP] hp={hp}   KO {result.KnockoutRate:P1}   " +
                          $"length {result.AverageTicks:F1} ticks   (ResourceCap={level1.ResourceCap}, " +
                          $"DeckSlots={level1.DeckSlotCount})");
            }
        }

        /// <summary>Deploys the player's side the same way the AI deploys its own, so both sides
        /// are placed by an equivalent policy.</summary>
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

        [Test]
        public void Balance_MatchesAreUsuallyDecidedByAKnockout_NotByTheTickCap()
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();
            SimResult mid = Simulate(pool, avatarLevel: 25, castleLevel: 15, MatchesPerRun);

            Debug.Log($"[Balance] mid profile: {mid.Matches} matches, " +
                      $"KO {mid.KnockoutRate:P1}, cap {mid.CapRate:P1}, avg {mid.AverageTicks:F1} ticks");

            Assert.Greater(mid.Matches, 0, "The simulation produced no usable matches.");

            // The tick cap exists to guarantee a match ends, not to be how matches normally end.
            // At the original Health scale this sat at 23% knockouts - the cap was the rule and a
            // knockout the exception, which is the reverse of the intent.
            //
            // KNOWN OPEN BALANCE ITEM. The design target is >70% knockouts at the mid profile.
            // Measured in-engine: 1300 HP -> 23%, 400 HP -> ~50%, 260 HP -> ~55%. Note how flat
            // that is: cutting the pool by 35% bought only ~5 points, which says Avatar Health is
            // NOT the dominant lever and further cuts would only make early matches trivially
            // short (the level-1 profile already resolves in ~3 ticks).
            //
            // The likely real constraint is the overflow rule itself: damage only reaches an
            // Avatar once a lane is *completely* cleared, and with both sides able to hold a
            // lane, many matches simply never generate overflow. Fixing that means changing the
            // overflow condition or the reinforcement cadence - a design decision, not a tuning
            // one, so it is left open rather than papered over.
            //
            // This threshold is therefore a REGRESSION floor, not the target: it holds the line
            // at what is currently achieved so a future change cannot quietly make things worse,
            // while the gap to the real goal stays documented here and in docs/.
            Assert.Greater(mid.KnockoutRate, 0.45f,
                $"Knockout rate fell to {mid.KnockoutRate:P1}, below the regression floor. The " +
                "Avatar Health pool is too deep for the damage the board produces, and matches " +
                "are being settled on a percentage instead of won.");
        }

        [Test]
        public void Balance_MatchesUseAMeaningfulPartOfTheClock()
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();
            SimResult mid = Simulate(pool, avatarLevel: 25, castleLevel: 15, MatchesPerRun);

            Debug.Log($"[Balance] average length: {mid.AverageTicks:F1} of {BattleController.MaxCombatTicks} ticks");

            // Too short and the active-spell layer never comes online (Energy accrues per tick and
            // the cheapest spell costs 25). Too long and every match is a grind to the cap.
            Assert.Greater(mid.AverageTicks, 3.5f,
                $"Matches averaging {mid.AverageTicks:F1} ticks are too short for Energy to accrue " +
                "and for any spell to be cast - the whole active layer would be decorative.");
            Assert.Less(mid.AverageTicks, BattleController.MaxCombatTicks - 1f,
                $"Matches averaging {mid.AverageTicks:F1} ticks are running to the cap.");
        }

        [Test]
        public void Balance_ProgressionDoesNotMakeMatchesLessDecisive()
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();
            SimResult early = Simulate(pool, avatarLevel: 1, castleLevel: 1, MatchesPerRun);
            SimResult late = Simulate(pool, avatarLevel: 30, castleLevel: 30, MatchesPerRun);

            Debug.Log($"[Balance] early KO {early.KnockoutRate:P1} ({early.AverageTicks:F1} ticks) | " +
                      $"late KO {late.KnockoutRate:P1} ({late.AverageTicks:F1} ticks)");

            // This is the failure mode that made the old scale genuinely bad rather than merely
            // mistuned: knockouts fell from 23% to 5% between the mid and max profiles, so the
            // further a player progressed the more likely their matches ended on a technicality.
            Assert.Greater(late.KnockoutRate, 0.45f,
                $"At max progression only {late.KnockoutRate:P1} of matches end in a knockout. " +
                "Progression must not make matches less decisive - a stronger player should not be " +
                "pushed toward tie-breaker endings.");
        }

        /// <summary>
        /// Measures the exposed-Avatar siege rule (adopted 2026-08-07 at 6%, see
        /// docs/Mechanics_Gap_Analysis.md §1.1) against a rule-disabled baseline, at all three
        /// progression profiles, in one run.
        ///
        /// No longer an adoption decision - that decision is made and recorded in
        /// LaneBattleResolver.ExposedAvatarSiegeEnabled's own comment. What remains useful is the
        /// regression guard at the bottom (siege must never make matches LESS decisive than OFF)
        /// and the retuning capability: re-run this after any future Avatar Health rescale to
        /// re-validate 6% is still the right fraction, using the real combat code rather than a
        /// model of it - which is the lesson that cost a session last time (an external Python
        /// model predicted 89% knockouts where the game produced 50%, because it quietly ignored
        /// Resource cost).
        ///
        /// Read the results in the run log under [Siege].
        /// </summary>
        [Test]
        public void Balance_ExposedAvatarSiege_MeasuredAgainstDisabled()
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();

            (string label, int avatar, int castle)[] profiles =
            {
                ("early (1/1)", 1, 1),
                ("mid   (25/15)", 25, 15),
                ("max   (30/30)", 30, 30),
            };

            // Candidate values swept in the same run as the baseline, so the table below is
            // internally comparable - the deck draw is random, and comparing a number from this
            // run against one written in a document last week is how balance arguments go wrong.
            float[] candidateSiegeFractions = { 0.06f, 0.07f, 0.08f };

            float worstDelta = float.MaxValue;

            foreach ((string label, int avatar, int castle) in profiles)
            {
                // Explicit OFF, not ResetRulesToDefault() - the shipped default is now ON, so
                // "default" and "disabled baseline" are no longer the same state.
                LaneBattleResolver.ExposedAvatarSiegeEnabled = false;
                SimResult without = Simulate(pool, avatar, castle, MatchesPerRun);

                Debug.Log($"[Siege] {label}  OFF        KO {without.KnockoutRate:P1}   " +
                          $"length {without.AverageTicks:F1} ticks");

                foreach (float fraction in candidateSiegeFractions)
                {
                    LaneBattleResolver.ExposedAvatarSiegeEnabled = true;
                    LaneBattleResolver.ExposedAvatarSiegeFraction = fraction;

                    SimResult with = Simulate(pool, avatar, castle, MatchesPerRun);

                    float delta = with.KnockoutRate - without.KnockoutRate;
                    if (Mathf.Approximately(fraction, LaneBattleResolver.DefaultExposedAvatarSiegeFraction))
                    {
                        worstDelta = Mathf.Min(worstDelta, delta);
                    }

                    Debug.Log($"[Siege] {label}  siege={fraction:P0}   KO {with.KnockoutRate:P1} " +
                              $"({delta * 100f:+0.0;-0.0} pts)   length {with.AverageTicks:F1} ticks");
                }
            }

            // The only hard assertion: siege must never make matches LESS decisive. Any positive
            // effect is reported rather than asserted, because pinning a target number here would
            // turn a legitimate retune of ExposedAvatarSiegeDamage into a build failure.
            Assert.GreaterOrEqual(worstDelta, -0.02f,
                "The siege rule reduced the knockout rate at some progression profile. It exists " +
                "to break the mutual-wipe stalemate; if it is making matches less decisive, the " +
                "premise is wrong and it should not be adopted.");
        }
    }
}
