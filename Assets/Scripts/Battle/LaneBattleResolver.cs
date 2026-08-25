using System;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Battle
{
    public struct LaneClashResult
    {
        public Lane Lane;
        public bool SideACleared;
        public bool SideBCleared;
        public int OverflowToA;
        public int OverflowToB;

        /// <summary>Combat Tick Feed data (2026-08-22): display names of cards that died IN THIS
        /// clash specifically, captured before ResolveLaneClash prunes them - a lane always starts
        /// a clash with only-alive cards (dead ones were pruned at the end of the previous clash),
        /// so "dead after this clash's damage" and "died this clash" are the same set. Purely
        /// additive read of already-resolved state; changes no damage/death rule.</summary>
        public IReadOnlyList<string> DefeatedCardNamesA;
        public IReadOnlyList<string> DefeatedCardNamesB;
    }

    public struct TurnResolutionResult
    {
        public List<LaneClashResult> LaneResults;
        public int DamageDealtToSideA;
        public int DamageDealtToSideB;
        public bool SideADefeated;
        public bool SideBDefeated;

        /// <summary>Combat Tick Feed data (2026-08-22): the siege portion of DamageDealtToSideA/B,
        /// broken out separately so the feed can say "siege" rather than folding it silently into
        /// overflow. Already computed inside ResolveTurn either way - this just reports it instead
        /// of discarding it after the sum.</summary>
        public int SiegeDamageToSideA;
        public int SiegeDamageToSideB;
    }

    /// <summary>
    /// Resolves lane combat per Game Mechanics v2, Part II §2.2-2.4. Both sides' lanes clash
    /// simultaneously each turn (the Snap-style "both commit, then reveal together" reading of
    /// the open question in Part II §2.2 - not sequential turn-by-turn like Hearthstone).
    /// Front/Middle lane stat bonuses are baked into each BattleCardInstance when it's played
    /// (see BattleController.PlayCard), not applied here - this class only resolves combat
    /// once cards are already on the board.
    /// </summary>
    public static class LaneBattleResolver
    {
        private static readonly Lane[] ResolutionOrder = { Lane.Front, Lane.Middle, Lane.Back };

        /// <summary>
        /// Both lanes' total Attack is computed first (a snapshot, before either side takes
        /// damage), so which lane happens to be processed "first" in code never affects the
        /// result - the clash is genuinely simultaneous, not order-dependent.
        /// </summary>
        public static LaneClashResult ResolveLaneClash(LaneState laneA, LaneState laneB)
        {
            int attackFromA = laneA.AliveCards.Sum(c => c.Attack);
            int attackFromB = laneB.AliveCards.Sum(c => c.Attack);

            // Elemental advantage is applied to the lane's *total* attack, not per card. A 25%
            // bonus on a single 3-Attack card is 0.75, which rounds to +1 - exactly the flat
            // bonus it was meant to replace, and unevenly (a 9-Attack card would get +2). Lane
            // totals are 15-30, so the same percentage survives a single rounding step and lands
            // as a meaningful +4 to +7. This is also the granularity the lane picker asks the
            // player to think at, so composition decisions and their payoff sit in the same place.
            attackFromA = ApplyElementalAdvantage(attackFromA, laneA, laneB);
            attackFromB = ApplyElementalAdvantage(attackFromB, laneB, laneA);

            int overflowToB = ApplyDamageToLane(laneB, attackFromA);
            int overflowToA = ApplyDamageToLane(laneA, attackFromB);

            // Wave 3 lock (Vulnerability): "consumed on trigger, expires next clash if unused".
            // A mark that triggered this clash was already cleared inside ApplyDamage, so this
            // sweep is a no-op for it; a mark that never triggered (its unit wasn't hit, or its
            // lane wasn't the one under attack) gets cleared here - it had its one window.
            foreach (BattleCardInstance unit in laneA.Cards.Where(c => c.IsAlive))
                unit.ExpireVulnerabilityMarkAtClashEnd();
            foreach (BattleCardInstance unit in laneB.Cards.Where(c => c.IsAlive))
                unit.ExpireVulnerabilityMarkAtClashEnd();

            // Capture cleared-state before pruning dead cards below - once pruned, an all-dead
            // lane becomes genuinely empty (Cards.Count == 0), which would otherwise make
            // IsFullyCleared read false and misreport what actually just happened this turn.
            bool sideACleared = laneA.IsFullyCleared;
            bool sideBCleared = laneB.IsFullyCleared;

            // Combat Tick Feed (2026-08-22): captured here, before the prune below removes them -
            // every card still in Cards at this point was alive at the start of this clash (see
            // the struct's own comment), so filtering to !IsAlive is exactly "died this clash".
            List<string> defeatedA = laneA.Cards.Where(c => !c.IsAlive).Select(c => c.Definition.DisplayName).ToList();
            List<string> defeatedB = laneB.Cards.Where(c => !c.IsAlive).Select(c => c.Definition.DisplayName).ToList();

            // Real bug fixed 2026-08-05: dead cards used to stay in Cards forever. That meant
            // (a) a "cleared" lane still counted as full for HasOpenSlot, permanently blocking
            // new plays there, and (b) IsFullyCleared kept reading true on every later turn
            // too (the same corpses, still all dead), so a lane that cleared once leaked its
            // full overflow damage again on every subsequent turn even with zero new activity -
            // this was the actual cause of "we do zero damage to the enemy" (their lanes likely
            // never fully cleared even once) and Avatar HP crashing fast (one lane clearing
            // early kept re-leaking full damage every turn after). Pruning here makes a cleared
            // lane genuinely empty again: slots free up, and overflow can only fire once per
            // actual clear, not once per turn forever after.
            laneA.Cards.RemoveAll(c => !c.IsAlive);
            laneB.Cards.RemoveAll(c => !c.IsAlive);

            return new LaneClashResult
            {
                Lane = laneA.Lane,
                SideACleared = sideACleared,
                SideBCleared = sideBCleared,
                OverflowToA = overflowToA,
                OverflowToB = overflowToB,
                DefeatedCardNamesA = defeatedA,
                DefeatedCardNamesB = defeatedB,
            };
        }

        /// <summary>Bonus applied to a lane's total attack when it holds the elemental advantage.
        /// 25% of a lane sum, rounded once - see the note at the call site for why this is not
        /// applied per card.</summary>
        public const float ElementalAdvantageBonus = 0.25f;

        /// <summary>
        /// The elemental cycle: Ktini beats Andras beats Pnevmas beats Ktini. A lane's element is
        /// whichever its living cards mostly are; a tie means no lane identity and therefore no
        /// advantage either way, which keeps mixed lanes a genuine (if bland) choice rather than
        /// an accidental win.
        /// </summary>
        public static bool Counters(CardElement attacker, CardElement defender) =>
            (attacker == CardElement.Ktini && defender == CardElement.Andras)
            || (attacker == CardElement.Andras && defender == CardElement.Pnevmas)
            || (attacker == CardElement.Pnevmas && defender == CardElement.Ktini);

        /// <summary>Returns the element the majority of a lane's living cards share, or null when
        /// the lane is empty or evenly split.</summary>
        public static CardElement? DominantElement(LaneState lane)
        {
            var counts = lane.AliveCards
                .GroupBy(c => c.Definition.Element)
                .Select(g => (element: g.Key, count: g.Count()))
                .OrderByDescending(t => t.count)
                .ToList();

            if (counts.Count == 0) return null;
            if (counts.Count > 1 && counts[0].count == counts[1].count) return null; // no majority
            return counts[0].element;
        }

        private static int ApplyElementalAdvantage(int laneAttack, LaneState attackingLane, LaneState defendingLane)
        {
            if (laneAttack <= 0) return laneAttack;

            CardElement? attackerElement = DominantElement(attackingLane);
            CardElement? defenderElement = DominantElement(defendingLane);
            if (attackerElement == null || defenderElement == null) return laneAttack;
            if (!Counters(attackerElement.Value, defenderElement.Value)) return laneAttack;

            return (int)Math.Round(laneAttack * (1f + ElementalAdvantageBonus), MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Spends `incomingDamage` against a lane's cards - Taunt (Knight) cards absorb damage
        /// first, then the rest in placement order. Returns leftover damage, but only once the
        /// lane has no living defenders left (per Part II §2.4's "clear the lane, then overflow"
        /// reading); a lane that survives with cards still alive blocks all overflow, even if
        /// the incoming damage technically exceeded its total Health.
        /// </summary>
        private static int ApplyDamageToLane(LaneState lane, int incomingDamage)
        {
            var taunts = lane.Cards.Where(c => c.IsAlive && c.HasTaunt);
            var others = lane.Cards.Where(c => c.IsAlive && !c.HasTaunt);

            int remaining = incomingDamage;
            foreach (BattleCardInstance defender in taunts.Concat(others))
            {
                if (remaining <= 0) break;
                int dealt = Math.Min(remaining, defender.CurrentHealth);
                defender.ApplyDamage(dealt);
                remaining -= dealt;
            }

            // Real bug fixed 2026-08-05 ("I deal zero damage to the Avatar after 2 rounds"):
            // this used to gate on lane.IsFullyCleared, which is defined as
            // "Cards.Count > 0 && all dead" - so a lane that was simply *empty* (never defended
            // at all) reported false and swallowed the entire attack. Attacking a completely
            // undefended lane therefore dealt zero damage to anything, which is the opposite of
            // what an undefended lane should mean. Overflow now keys off "no living defenders",
            // which covers both an emptied lane and a never-occupied one.
            bool laneIsUndefended = !lane.Cards.Any(c => c.IsAlive);
            return laneIsUndefended ? remaining : 0;
        }

        // Card combat (Attack vs Health, computed above) is deliberately still small integers -
        // the "Integer Model" from Part II §3 stays mentally computable mid-match. Avatar HP
        // pools were rescaled 2026-08-05 to the hundreds/thousands (see PlayerEmpireData), which
        // would otherwise mean a match takes 10-50x longer to resolve, since overflow damage
        // itself is still single-digit-to-low-double-digit per lane. This multiplier is what
        // reconciles the two: only the final "overflow hits the Avatar" step is scaled up, so
        // card-vs-card math and lane-clash rules are completely untouched. Chosen to roughly
        // track how much bigger Avatar HP got overall (base 20->100 is 5x, but the mid-test
        // profile's total went ~36->1300, ~36x) - 25 keeps match length in the same ballpark as
        // before the HP rescale rather than 5x or 36x longer.
        // Cut 25 -> 6 on 2026-08-06 for pacing; cut 6 -> 4 on 2026-08-21 (MVP constitution) so
        // Chapter 1 / early matches reliably last long enough to bank Energy for a spell while
        // keeping the Integer Model card stats untouched. Overtime stays ~1.5x / 2x of this base.
        public const int AvatarDamageMultiplier = 4;

        // Overtime. The multiplier escalates in the back half of a match so the tick cap acts as
        // a *deadline* rather than a result: previously a fight that hadn't resolved by tick 12
        // was simply decided on Health percentage, which rewards turtling - hold a lead, refuse
        // to trade, and collect the win. Escalating damage makes the late game increasingly
        // lethal, so a match reaches a real conclusion instead of running out the clock.
        public const int OvertimeStartTick = 7;
        public const int LateOvertimeStartTick = 10;
        private const int OvertimeMultiplier = 6;      // 1.5x of base 4
        private const int LateOvertimeMultiplier = 8; // 2.0x of base 4

        /// <summary>The Avatar damage multiplier in force on a given combat tick.</summary>
        public static int AvatarDamageMultiplierForTick(int tickNumber)
        {
            if (tickNumber >= LateOvertimeStartTick) return LateOvertimeMultiplier;
            if (tickNumber >= OvertimeStartTick) return OvertimeMultiplier;
            return AvatarDamageMultiplier;
        }

        // ---------- Siege: the exposed-Avatar rule (EXPERIMENT, off by default) ----------

        /// <summary>
        /// Raw damage an Avatar takes each tick while its side has nothing alive on the board.
        /// Expressed in the same units as lane overflow so it passes through
        /// <see cref="AvatarDamageMultiplierForTick"/> and escalates in overtime like everything
        /// else - a flat post-multiplier number would be mistuned by 2x the moment it mattered.
        /// </summary>
        // 6% chosen from the sweep in BalanceSimulationTests, measured over 400 matches per cell:
        //
        //   profile        OFF     4%      6%      7%      8%
        //   early (1/1)    88.0%   93.8%   98.3%   100.0%  100.0%
        //   mid   (25/15)  51.3%   57.8%   83.3%    99.0%  100.0%
        //   max   (30/30)  58.3%   54.8%   69.0%    88.5%  100.0%
        //
        // 6% clears the >70% design target at the early and mid profiles and sits on it at max
        // (69.0%, inside the +/-5 point run-to-run noise at n=400 - the OFF baseline for that same
        // profile measured 49.8%, 50.3% and 58.3% across three runs). 7% and 8% clear the target
        // everywhere but saturate: at 8% every single match at every profile ends in a knockout,
        // which means the tick-cap tie-breaker never fires and the board has stopped deciding
        // anything. 6% leaves roughly one match in six at the mid profile still going to a
        // decision, which is the intended shape - a rare outcome, not the normal one.
        public const float DefaultExposedAvatarSiegeFraction = 0.06f;

        /// <summary>
        /// Siege damage per tick, as a fraction of that Avatar's OWN maximum Health.
        ///
        /// Expressed as a fraction rather than in raw damage units after measuring the raw version
        /// and finding it behaves as a cliff rather than a dial. Raw siege has to out-race a fixed
        /// Health pool inside a fixed number of remaining ticks, so it does nothing until it can
        /// finish the job and then finishes it every time: at the mid profile, raw 2 -> 3 -> 4
        /// moved the knockout rate 61.5% -> 97.0% -> 100.0%, while the same three values at the
        /// max profile (a deeper pool) gave 53.5% -> 58.8% -> 90.5%. One number could not be
        /// correct for both, and any number chosen would drift the moment Avatar Health was
        /// retuned - which it has been, three times.
        ///
        /// A fraction of the pool is profile-independent by construction: it takes the same number
        /// of ticks to close out a stalled match at level 1 as at level 30, and it stays correct
        /// through the next Health rescale without being touched.
        ///
        /// A static field rather than a const so the simulation can sweep candidates in one run.
        /// Anything that changes it must restore it via <see cref="ResetRulesToDefault"/>.
        /// </summary>
        public static float ExposedAvatarSiegeFraction = DefaultExposedAvatarSiegeFraction;

        /// <summary>
        /// Siege does not begin until this tick. Without the delay the rule is far too strong, and
        /// this was measured rather than guessed: siege applied from tick 1 took the knockout rate
        /// to 100% at every progression profile and cut the early-game match from 3.2 ticks to
        /// 1.7. A game where every single match ends in a knockout is as broken as one where
        /// almost none do - it means the board no longer decides anything, and 1.7 ticks is over
        /// before Energy has accrued enough to cast the cheapest spell.
        ///
        /// Gating it to the overtime tick is what makes it a stalemate-breaker rather than a
        /// damage source. A wiped board on tick 2 is a fight in progress, and reinforcement
        /// windows at ticks 4 and 8 exist precisely so it can be rebuilt; a wiped board on tick 7
        /// with neither side able to act is the dead position this rule is aimed at. Deliberately
        /// the same tick the damage multiplier already escalates on, so a match has exactly one
        /// "the gloves come off" moment rather than two.
        /// </summary>
        public const int SiegeStartTick = OvertimeStartTick;

        /// <summary>
        /// Whether the siege rule is in force. ADOPTED 2026-08-07 at the measured 6% fraction -
        /// see docs/Mechanics_Gap_Analysis.md §1.1. Mid-profile knockouts went 51.3% -> 83.3% with
        /// average match length essentially unchanged (7.9 -> 7.6 ticks), and it was the only one
        /// of five tested fractions (OFF/4/6/7/8%) that cleared the >70% target without saturating
        /// the mid profile to 99%+, which would have stopped the tie-breaker from ever mattering.
        ///
        /// A mutable static rather than a const specifically so the in-engine simulation can still
        /// measure the game with and without it in one run (e.g. to re-validate after a future
        /// Avatar Health rescale). Anything that flips it MUST restore it (see
        /// <see cref="ResetRulesToDefault"/>) or every later test in the run silently measures a
        /// different game.
        /// </summary>
        public static bool ExposedAvatarSiegeEnabled = true;

        public static void ResetRulesToDefault()
        {
            ExposedAvatarSiegeEnabled = true;
            ExposedAvatarSiegeFraction = DefaultExposedAvatarSiegeFraction;
        }

        /// <summary>
        /// Why this rule is proposed at all.
        ///
        /// The knockout rate sits around 55% against a design target of 70%+, and Avatar Health
        /// has already been shown not to be the lever: cutting the pool 400 -> 260 (a 35% cut)
        /// moved the rate about 5 points. The reason is structural rather than numeric. Overflow
        /// only reaches an Avatar through a lane with no living defenders, so once BOTH sides'
        /// boards are wiped - which is the normal end state of two evenly matched formations that
        /// never replenish - total Attack on both sides is zero and no further damage of any kind
        /// is possible. The match cannot be won from that position by anyone; it can only run out
        /// the clock. <see cref="BattleController.MaxCombatTicks"/> exists purely to stop that
        /// state hanging the game forever, and its comment records hitting it in real play with
        /// both Avatars still above half Health.
        ///
        /// Siege makes an empty board a losing position instead of a safe one: an Avatar with no
        /// army in front of it is under siege and bleeds every tick. Two wiped boards then race,
        /// and the side that preserved more Health wins by actually killing the other rather than
        /// by being declared the winner on a percentage.
        ///
        /// Deliberately NOT chosen over the alternatives, and why:
        /// - Partial overflow (damage leaks through a lane that survives) directly removes the
        ///   point of Taunt and of holding a lane, which is the trade the whole formation layer is
        ///   built on.
        /// - More reinforcement windows let a player chump-block indefinitely, which is the
        ///   stalling behaviour the tick cap was added to prevent.
        /// - Raising card Attack shortens every match, including the early-game ones that are
        ///   already too short for a spell to ever be cast.
        /// Siege changes only the state in which nothing can currently happen, and leaves every
        /// contested-board rule exactly as it is.
        /// </summary>
        /// <summary>Siege damage against one side this tick, already final - it does NOT pass
        /// through the overflow multiplier, because it is derived from the Health pool directly
        /// rather than from card Attack.</summary>
        public static int SiegeDamageFor(PlayerBattleState side, int tickNumber)
        {
            if (!ExposedAvatarSiegeEnabled) return 0;
            if (tickNumber < SiegeStartTick) return 0;
            if (side.HasLivingCards) return 0;

            // Escalates on exactly the same curve as overflow damage (1.0x / 1.5x / 2.0x) so the
            // late game has one "gloves come off" moment rather than two competing ones. Derived
            // from the multiplier constants rather than restating 1.5 and 2.0, so retuning
            // overtime cannot leave siege escalating on a curve that no longer exists.
            float overtimeScale = AvatarDamageMultiplierForTick(tickNumber) / (float)AvatarDamageMultiplier;

            // At least 1, so a rounding-down at a very small pool can never leave siege doing
            // literally nothing - which would silently restore the stalemate this exists to break.
            return Math.Max(1,
                (int)Math.Ceiling(side.MaxAvatarHealth * ExposedAvatarSiegeFraction * overtimeScale));
        }

        /// <summary>
        /// Resolves a full turn: Front, Middle, then Back lanes clash between the two sides,
        /// with each cleared lane's overflow damage carrying into that side's Avatar Health.
        /// </summary>
        /// <param name="tickNumber">Which combat tick this is, for overtime escalation. Defaults
        /// to 1 so the many existing call sites that resolve a single clash in isolation (tests,
        /// the old turn flow) keep the base multiplier.</param>
        public static TurnResolutionResult ResolveTurn(PlayerBattleState sideA, PlayerBattleState sideB,
            int tickNumber = 1)
        {
            var laneResults = new List<LaneClashResult>();
            int totalOverflowToA = 0;
            int totalOverflowToB = 0;

            foreach (Lane lane in ResolutionOrder)
            {
                LaneClashResult result = ResolveLaneClash(sideA.Lanes[lane], sideB.Lanes[lane]);
                laneResults.Add(result);
                totalOverflowToA += result.OverflowToA;
                totalOverflowToB += result.OverflowToB;
            }

            int multiplier = AvatarDamageMultiplierForTick(tickNumber);

            // Siege is evaluated after the lane loop because ResolveLaneClash prunes dead cards -
            // "this side has nothing left alive" has to mean after this tick's fighting, not
            // before it, or a board wiped on tick 7 would not come under siege until tick 8.
            // Added after the multiplier, not before: it is already a fraction of the Health pool
            // and carries its own overtime scaling.
            int siegeToA = SiegeDamageFor(sideA, tickNumber);
            int siegeToB = SiegeDamageFor(sideB, tickNumber);
            int avatarDamageToA = (totalOverflowToA * multiplier) + siegeToA;
            int avatarDamageToB = (totalOverflowToB * multiplier) + siegeToB;
            sideA.AvatarHealth = Math.Max(0, sideA.AvatarHealth - avatarDamageToA);
            sideB.AvatarHealth = Math.Max(0, sideB.AvatarHealth - avatarDamageToB);

            return new TurnResolutionResult
            {
                LaneResults = laneResults,
                DamageDealtToSideA = avatarDamageToA,
                DamageDealtToSideB = avatarDamageToB,
                SideADefeated = sideA.IsDefeated,
                SideBDefeated = sideB.IsDefeated,
                SiegeDamageToSideA = siegeToA,
                SiegeDamageToSideB = siegeToB,
            };
        }
    }
}
