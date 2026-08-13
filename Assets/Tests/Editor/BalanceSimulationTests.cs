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

        /// <summary>
        /// Locks in the onboarding HP taper added to PlayerEmpireData (docs/Mechanics_Gap_Analysis.md
        /// section 1.3 / T2c): full bonus at Avatar level 1, linearly gone by
        /// PlayerEmpireData.LevelsPerResourceTier. Both halves matter - a regression that makes the
        /// taper too weak leaves level 1 unplayable for spells again, and a regression that leaks
        /// it past the taper level would silently re-inflate the mid/max profiles the siege rule
        /// was tuned against.
        /// </summary>
        [Test]
        public void Balance_OnboardingHealthTaper_AppliesAtLevel1AndIsGoneByTier1()
        {
            var level1 = new PlayerEmpireData();
            level1.SetLevelsForTesting(avatarLevel: 1, castleLevel: 1, barracksLevel: 1);
            level1.InitializeTCGModifiers();

            Assert.AreEqual(200, level1.StartingAvatarHealth,
                "Level 1 should be the base 100 plus the full 100 onboarding bonus - if this " +
                "drifts, re-check BalanceSimulationTests.Balance_Level1StartingHealth_SweepForOnboardingViability " +
                "against the new value before changing the constant.");

            var level5 = new PlayerEmpireData();
            level5.SetLevelsForTesting(avatarLevel: 5, castleLevel: 1, barracksLevel: 1);
            level5.InitializeTCGModifiers();

            // 120, not the bare 100: level 5 is also the level the FIRST per-level Health tier
            // starts (5 / LevelsPerResourceTier == 1), so avatarHealthBonus contributes its own
            // +20 here independent of onboarding. What this asserts is that the ONBOARDING
            // component specifically is gone - 120 total, not 220, is what proves the two never
            // double up at the boundary they share.
            Assert.AreEqual(120, level5.StartingAvatarHealth,
                "By Avatar level 5 the onboarding bonus must contribute nothing - the regular " +
                "per-level tier bonus (+20, since level 5 is also its own first tier) should be " +
                "the only thing raising this above the 100 base.");

            // The mid/max profiles the siege rule was measured and adopted against must be
            // byte-for-byte unchanged by this taper - it exists specifically so this stays true.
            var mid = new PlayerEmpireData();
            mid.SetLevelsForTesting(avatarLevel: 25, castleLevel: 15, barracksLevel: 25);
            mid.InitializeTCGModifiers();
            Assert.AreEqual(260, mid.StartingAvatarHealth,
                "The mid profile's Health must not move - this taper is onboarding-only.");

            var max = new PlayerEmpireData();
            max.SetLevelsForTesting(avatarLevel: 30, castleLevel: 30, barracksLevel: 25);
            max.InitializeTCGModifiers();
            Assert.AreEqual(340, max.StartingAvatarHealth,
                "The max profile's Health must not move - this taper is onboarding-only.");
        }

        /// <summary>
        /// Re-measures the level-1 profile at its new (200 HP) starting Health with real
        /// assertions, not just a logged sweep - this is the regression guard for T2c. Both halves
        /// of the original problem must hold: matches stay decisive (siege + overflow still work),
        /// AND they now run long enough for the cheapest spell to plausibly be cast.
        /// </summary>
        [Test]
        public void Balance_Level1Onboarding_IsBothDecisiveAndLongEnoughForASpell()
        {
            List<Card> pool = LoadDatabase().AllCards.ToList();
            SimResult level1 = Simulate(pool, avatarLevel: 1, castleLevel: 1, MatchesPerRun);

            Debug.Log($"[Onboarding] level 1 (200 HP): KO {level1.KnockoutRate:P1}, " +
                      $"avg {level1.AverageTicks:F1} ticks");

            Assert.Greater(level1.KnockoutRate, 0.70f,
                "Level 1 knockout rate fell below the design target after the onboarding HP " +
                "change - the taper should have made spells castable without hurting decisiveness.");

            // Mend (the cheapest spell) costs 25 Energy at 18/tick, so it cannot come off cooldown
            // before tick 2 at the very earliest (36 accrued). A floor of 4.0 leaves real margin
            // above that earliest-possible cast rather than merely clearing it by one tick.
            Assert.Greater(level1.AverageTicks, 4.0f,
                $"Level 1 matches average {level1.AverageTicks:F1} ticks, still too close to the " +
                "2-tick point a spell can first be cast - the onboarding bonus is too weak.");
        }

        private struct DetailedSimResult
        {
            public int Matches;
            public float PlayerWinRate;
            public float AverageTicks;
            public float AvgWinnerHealthFraction;
            public float AvgPlayerLanesClearedPerMatch;
            public float AvgEnemyLanesClearedPerMatch;
            public float AvgSiegeDamagePerMatch;
            public float AvgOverflowDamagePerMatch;
            public float AvgSiegeActiveTicksPerMatch;
            public float AvgPlayerResourceUtilization;
            public float AvgEnemyResourceUtilization;
        }

        /// <summary>
        /// Measures the enemy's effect on the player when played by a single AIArchetype, holding
        /// the player's own deployment at Balanced throughout - richer than the quick archetype
        /// check this replaced (win rate + ticks only): also winner's remaining Health fraction,
        /// lane-clear counts per side, siege vs. plain-overflow damage split, how many ticks siege
        /// was actually active, and Resource utilization during formation.
        ///
        /// Isolates deployment-policy effects only. The AI does not cast spells, has no Energy,
        /// gets no Back-lane bonus, and never reinforces here - the same permanent asymmetry
        /// documented in docs/Mechanics_Gap_Analysis.md section 1.2. This experiment measures what
        /// changing HOW the AI places cards does; it is not a re-litigation of THAT decision, and
        /// must not be read as one.
        ///
        /// Win/loss is read from OnMatchEnded, not PlayerState.IsDefeated after the loop - a
        /// tick-cap decision (settled on Health percentage) never actually sets either side's
        /// AvatarHealth to 0, so IsDefeated reads false for BOTH sides after one, which silently
        /// miscounted every tick-cap-decided match as a player win in an earlier version of this
        /// sweep. OnMatchEnded's bool is the one place the percentage tie-breaker's real result is
        /// exposed, including a true draw correctly folding into "not a win" - see
        /// BattleController.ResolveOnTickCap.
        /// </summary>
        private DetailedSimResult SweepArchetypeDetailed(List<Card> pool, PlayerEmpireData empire,
            AIArchetype enemyArchetype, int count)
        {
            var economy = new BattleController.MatchEconomy(
                empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);

            int matches = 0;
            int playerWins = 0;
            int totalTicks = 0;
            float totalWinnerHealthFraction = 0f;
            long playerLanesCleared = 0;
            long enemyLanesCleared = 0;
            long totalSiegeDamage = 0;
            long totalOverflowDamage = 0;
            long siegeActiveTicks = 0;
            float totalPlayerResourceUtilization = 0f;
            float totalEnemyResourceUtilization = 0f;

            for (int i = 0; i < count; i++)
            {
                BattleController controller = CreateController();

                bool? playerWonThisMatch = null;
                void OnEnded(bool won) => playerWonThisMatch = won;
                controller.OnMatchEnded += OnEnded;

                List<Card> playerDeck = pool.OrderBy(_ => Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => Random.value).Take(empire.DeckSlotCount).ToList();

                controller.StartMatch(playerDeck, enemyDeck, economy, economy);
                controller.DealFormationHand(controller.PlayerState);
                controller.DealFormationHand(controller.EnemyState);

                DeployWholeSquad(controller, controller.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(controller, enemyArchetype);

                if (!controller.ConfirmFormation())
                {
                    controller.OnMatchEnded -= OnEnded;
                    Object.DestroyImmediate(controller.gameObject);
                    continue;
                }

                // Resource spent building the formation, before combat or reinforcement can touch
                // it - the fraction of the cap an archetype's placement policy actually used.
                float playerUtilization = economy.ResourceCap > 0
                    ? 1f - (controller.PlayerState.Resource / (float)economy.ResourceCap) : 0f;
                float enemyUtilization = economy.ResourceCap > 0
                    ? 1f - (controller.EnemyState.Resource / (float)economy.ResourceCap) : 0f;
                totalPlayerResourceUtilization += playerUtilization;
                totalEnemyResourceUtilization += enemyUtilization;

                while (controller.Phase == BattlePhase.Combat)
                {
                    TurnResolutionResult tickResult = controller.AdvanceCombatTick();

                    foreach (LaneClashResult lane in tickResult.LaneResults)
                    {
                        if (lane.SideACleared) playerLanesCleared++;
                        if (lane.SideBCleared) enemyLanesCleared++;
                    }

                    // Reconstructed, not guessed: SiegeDamageFor reads the SAME post-clash
                    // HasLivingCards state ResolveTurn itself used for this tick (siege is
                    // evaluated after lane pruning - see LaneBattleResolver.ResolveTurn's own
                    // comment), so calling it again here with the same tick number reproduces the
                    // exact value already baked into tickResult.DamageDealtToSideA/B.
                    int tick = controller.TickCount;
                    int siegeToPlayer = LaneBattleResolver.SiegeDamageFor(controller.PlayerState, tick);
                    int siegeToEnemy = LaneBattleResolver.SiegeDamageFor(controller.EnemyState, tick);
                    totalSiegeDamage += siegeToPlayer + siegeToEnemy;
                    totalOverflowDamage += System.Math.Max(0, tickResult.DamageDealtToSideA - siegeToPlayer)
                                          + System.Math.Max(0, tickResult.DamageDealtToSideB - siegeToEnemy);
                    if (siegeToPlayer > 0 || siegeToEnemy > 0) siegeActiveTicks++;
                }

                matches++;
                totalTicks += controller.TickCount;

                bool playerWon = playerWonThisMatch ?? false;
                if (playerWon) playerWins++;

                if (playerWonThisMatch.HasValue)
                {
                    PlayerBattleState winnerState = playerWon ? controller.PlayerState : controller.EnemyState;
                    totalWinnerHealthFraction += winnerState.MaxAvatarHealth > 0
                        ? winnerState.AvatarHealth / (float)winnerState.MaxAvatarHealth : 0f;
                }
                // A true draw (OnMatchEnded never fires with a winner because playerWonThisMatch
                // stays consistent with "not a win" via the null-coalesce above) contributes 0 to
                // the winner-health average by design - there is no winner to measure.

                controller.OnMatchEnded -= OnEnded;
                Object.DestroyImmediate(controller.gameObject);
            }

            return new DetailedSimResult
            {
                Matches = matches,
                PlayerWinRate = matches == 0 ? 0f : (float)playerWins / matches,
                AverageTicks = matches == 0 ? 0f : (float)totalTicks / matches,
                AvgWinnerHealthFraction = matches == 0 ? 0f : totalWinnerHealthFraction / matches,
                AvgPlayerLanesClearedPerMatch = matches == 0 ? 0f : (float)playerLanesCleared / matches,
                AvgEnemyLanesClearedPerMatch = matches == 0 ? 0f : (float)enemyLanesCleared / matches,
                AvgSiegeDamagePerMatch = matches == 0 ? 0f : (float)totalSiegeDamage / matches,
                AvgOverflowDamagePerMatch = matches == 0 ? 0f : (float)totalOverflowDamage / matches,
                AvgSiegeActiveTicksPerMatch = matches == 0 ? 0f : (float)siegeActiveTicks / matches,
                AvgPlayerResourceUtilization = matches == 0 ? 0f : totalPlayerResourceUtilization / matches,
                AvgEnemyResourceUtilization = matches == 0 ? 0f : totalEnemyResourceUtilization / matches,
            };
        }

        /// <summary>
        /// GameBootstrap's only call to GenerateAIOpponent (StartNewMatch) never passes an
        /// archetype, so every real match uses the parameter's default - AIArchetype.Balanced -
        /// regardless of difficulty tier. Aggressive/Defensive/Tactical are fully implemented and
        /// exercised throughout this test file, but a real player has never faced any of them.
        ///
        /// n=2000 per cell (up from the usual 400 elsewhere in this file) specifically because the
        /// question here is the RANKING of four archetypes against each other, not just "does this
        /// one rule move the needle" - docs/Mechanics_Gap_Analysis.md's own methodology note gives
        /// n=400 roughly +/-5 points of run-to-run noise, so two archetypes 6-8 points apart at
        /// n=400 are not distinguishable from noise. This does not decide anything by itself - same
        /// role every other sweep in this file plays before a design decision.
        /// </summary>
        [Test]
        public void Balance_ArchetypeDeepSweep_AcrossFourProfilesAtHighSampleSize()
        {
            const int SampleSize = 2000;

            List<Card> pool = LoadDatabase().AllCards.ToList();

            (string label, int avatar, int castle)[] profiles =
            {
                ("Early(1/1)", 1, 1),
                ("Mid(25/15)", 25, 15),
                ("High(28/22)", 28, 22),
                ("Max(30/30)", 30, 30),
            };

            foreach ((string label, int avatarLevel, int castleLevel) in profiles)
            {
                var empire = new PlayerEmpireData();
                empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel: 25);
                empire.InitializeTCGModifiers();

                foreach (AIArchetype archetype in System.Enum.GetValues(typeof(AIArchetype)))
                {
                    DetailedSimResult r = SweepArchetypeDetailed(pool, empire, archetype, SampleSize);

                    Debug.Log($"[DeepSweep] {label,-11} enemy={archetype,-10} " +
                              $"winRate={r.PlayerWinRate:P1} avgTicks={r.AverageTicks:F1} " +
                              $"winnerHP={r.AvgWinnerHealthFraction:P0} " +
                              $"playerLanesCleared={r.AvgPlayerLanesClearedPerMatch:F2} " +
                              $"enemyLanesCleared={r.AvgEnemyLanesClearedPerMatch:F2} " +
                              $"siegeDmg={r.AvgSiegeDamagePerMatch:F1} overflowDmg={r.AvgOverflowDamagePerMatch:F1} " +
                              $"siegeActiveTicks={r.AvgSiegeActiveTicksPerMatch:F2} " +
                              $"playerResUtil={r.AvgPlayerResourceUtilization:P0} " +
                              $"enemyResUtil={r.AvgEnemyResourceUtilization:P0} (n={r.Matches})");
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

        /// <summary>
        /// NEW-PROFILE CASTLE/BARRACKS LEVEL-1 CORRECTION, 2026-08-13: confirms the approved first
        /// tutorial encounter - the same starter roster (warrior/Front, novice_knight/Middle,
        /// goblin_caster/Back) and enemy deck (butcher, cursed_soldier, giant_worms, tribal_warrior)
        /// BattleLogicTests' TutorialRoster_*/TutorialEnemyDeck_* tests already cover for legality -
        /// still legally forms AND resolves under the real combat tick cap now that a genuine new
        /// profile's deck/Resource/Health baseline is derived from Castle 1 / Barracks 1 rather than
        /// the old mid-test seed (Castle 15 / Barracks 25). Drives the real BattleController end to
        /// end, the same "simulate in-engine, not externally" discipline as every other test in this
        /// file - the smaller Resource cap and Health pool are exactly the kind of constraint an
        /// external model has silently ignored before (see this file's own class comment). Asserts
        /// only legality and existing playability boundaries (the tick cap already in force) - no
        /// new balance target is introduced here.
        /// </summary>
        [Test]
        public void Balance_ApprovedTutorialEncounter_FormsAndResolvesAtTheCastleOneBarracksOneBaseline()
        {
            CardDatabase db = LoadDatabase();
            Card warrior = db.GetCard("warrior");
            Card noviceKnight = db.GetCard("novice_knight");
            Card goblinCaster = db.GetCard("goblin_caster");
            var playerDeck = new List<Card> { warrior, noviceKnight, goblinCaster };
            var enemyDeck = new List<Card>
            {
                db.GetCard("butcher"), db.GetCard("cursed_soldier"),
                db.GetCard("giant_worms"), db.GetCard("tribal_warrior"),
            };
            Assert.That(playerDeck, Has.All.Not.Null, "Setup: all three approved starter card ids must resolve.");
            Assert.That(enemyDeck, Has.All.Not.Null, "Setup: all four approved enemy card ids must resolve.");

            var playerEmpire = new PlayerEmpireData();
            playerEmpire.SetLevelsForTesting(avatarLevel: 1, castleLevel: 1, barracksLevel: 1);
            playerEmpire.InitializeTCGModifiers();

            var economy = new BattleController.MatchEconomy(
                playerEmpire.ResourceCap, playerEmpire.Turn1Resource, playerEmpire.StartingAvatarHealth);

            BattleController controller = CreateController();
            controller.StartMatch(playerDeck, enemyDeck, economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);

            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, warrior, Lane.Front),
                "The approved starter Formation must still be legally affordable at the Castle 1 / Barracks 1 Resource cap.");
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, noviceKnight, Lane.Middle),
                "The approved starter Formation must still be legally affordable at the Castle 1 / Barracks 1 Resource cap.");
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, goblinCaster, Lane.Back),
                "The approved starter Formation must still be legally affordable at the Castle 1 / Barracks 1 Resource cap.");

            SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);
            Assert.IsTrue(controller.ConfirmFormation(),
                "The approved starter Formation must still lock legally at the Castle 1 / Barracks 1 baseline.");

            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks,
                    "The approved tutorial encounter must resolve within the existing combat tick cap, not hang past it.");
            }

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase,
                "The approved tutorial encounter must reach a Resolved phase within the existing tick cap - " +
                "the smaller Castle 1 / Barracks 1 deck/Resource/Health baseline must not leave it stuck mid-combat.");

            Debug.Log($"[TutorialEncounter] resolved in {controller.TickCount} of {BattleController.MaxCombatTicks} ticks " +
                      $"(ResourceCap={playerEmpire.ResourceCap}, DeckSlots={playerEmpire.DeckSlotCount}, " +
                      $"StartingHealth={playerEmpire.StartingAvatarHealth}).");
        }
    }
}
