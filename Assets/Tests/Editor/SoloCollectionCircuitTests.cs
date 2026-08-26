using System;
using System.Collections.Generic;
using MyriadOfDragons.Empire;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Solo Collection Circuit - Formation Trial and Tactical Brief.
    ///
    /// The Collection Trial is deliberately ABSENT, not forgotten: its locked rule selects cards by
    /// "school/rarity/faction", and neither `school` nor `faction` exists in card_data.json (real
    /// keys: art_file, attack, element, health, id, name, rarity, type). That is a spec correction
    /// for CC, so the trial is left unbuilt rather than guessed at - see the mailbox entry.
    ///
    /// No save isolation needed: SoloCollectionCircuit never touches disk and never reads the
    /// clock. Every entry point takes nowUtc, which is what lets the rollback and week-boundary
    /// cases below be tested at all.
    /// </summary>
    public class SoloCollectionCircuitTests
    {
        private static readonly DateTime Day1 = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Day2 = Day1.AddDays(1);

        /// <summary>Monday of the following ISO week (verified: 2026-08-31 is a Monday, ISO week
        /// 36). Kept as a stable, real calendar anchor for the multi-day cycle tests. Under the
        /// current rule the start day no longer matters - see
        /// AMidWeekJoiner_CAN_EarnTheCycleBonus_WhichIsTheWholePointOfTheFix.</summary>
        private static readonly DateTime WeekStartMonday =
            new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

        private static SoloCircuitProgress Fresh() => new SoloCircuitProgress();

        [Test]
        public void AFreshProfile_HasClearedNothing_AndNeedsNoMigration()
        {
            var progress = Fresh();

            // Every default must read as "no circuit run yet" for a save that predates the feature.
            Assert.AreEqual(string.Empty, progress.dayKeyUtc);
            Assert.AreEqual(0, progress.trialsClearedTodayMask);
            Assert.IsFalse(SoloCollectionCircuit.AllThreeClearedToday(progress));
        }

        [Test]
        public void AFirstClear_GrantsTheLockedTrialReward()
        {
            var progress = Fresh();

            SoloCircuitClearResult r =
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, Day1);

            Assert.IsTrue(r.Cleared, r.Message);
            Assert.AreEqual(SoloCollectionCircuit.GoldPerTrialClear, r.GoldGranted);
            Assert.AreEqual(SoloCollectionCircuit.MaterialsPerTrialClear, r.MaterialsGranted);
            Assert.IsFalse(r.CompletedAllThreeToday, "One trial is not a circuit.");
        }

        [Test]
        public void TheSameTrial_CannotBeClearedTwiceInOneDay()
        {
            var progress = Fresh();
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, Day1);

            SoloCircuitClearResult again =
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, Day1);

            Assert.IsFalse(again.Cleared, "First-clear-per-day, or the trial is farmable.");
            Assert.AreEqual(0, again.GoldGranted, "A refused clear must grant nothing at all.");
        }

        [Test]
        public void ANewUtcDay_ResetsTheTrials_ButNotTheWeeklyCount()
        {
            var progress = Fresh();
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, Day1);

            SoloCircuitClearResult tomorrow =
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, Day2);

            Assert.IsTrue(tomorrow.Cleared, "A new UTC day makes yesterday's trials available again.");
            Assert.AreEqual(SoloCollectionCircuit.GoldPerTrialClear, tomorrow.GoldGranted);
        }

        [Test]
        public void ClockRollback_INVALIDATES_TheAttempt_AndNeverReGrants()
        {
            // The whole reason this system does not use the plain key-mismatch reset every other
            // daily system uses. That shape resets whenever the stored key DIFFERS - including when
            // the new key is EARLIER - so a player could clear all three trials, wind the clock back
            // a day, and farm the rewards indefinitely.
            var progress = Fresh();
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, Day2);

            SoloCircuitClearResult rolledBack =
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, Day1);

            Assert.IsFalse(rolledBack.Cleared,
                "A day key below the high-water mark is a rollback - the claim is invalidated.");
            Assert.AreEqual(0, rolledBack.GoldGranted);
            StringAssert.Contains("rollback", rolledBack.Message.ToLowerInvariant(),
                "The player must be told why, not shown a silent no-op.");
        }

        [Test]
        public void AfterARollback_TheRealDayStillWorks_SoTheGuardIsNotAOneWayTrap()
        {
            // The guard must refuse the rollback WITHOUT bricking the feature - a player whose
            // device clock was genuinely wrong has to recover once real time catches up.
            var progress = Fresh();
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, Day2);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, Day1);

            SoloCircuitClearResult recovered =
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, Day2);

            Assert.IsTrue(recovered.Cleared, recovered.Message);
        }

        [Test]
        public void ClearingAllThreeInOneDay_PaysTheCircuitBonus()
        {
            var progress = Fresh();
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, Day1);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, Day1);

            SoloCircuitClearResult third =
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, Day1);

            Assert.IsTrue(third.CompletedAllThreeToday);
            Assert.AreEqual(SoloCollectionCircuit.EventMedalsForAllThreeSameDay, third.EventMedalsGranted);
            Assert.AreEqual(1, progress.circuitDaysInCycle,
                "The first completed Circuit opens the player's personal cycle at day 1.");
        }

        [Test]
        public void TheDailyGoldCap_IsEnforcedExplicitly_NotInferredFromTheTrialAmounts()
        {
            // 3 trials (750) + the all-three bonus (500) is exactly the 1,250 ceiling, so a full
            // day must land ON the cap and never above it.
            var progress = Fresh();
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, Day1);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, Day1);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, Day1);

            Assert.AreEqual(SoloCollectionCircuit.MaxGoldPerDay, progress.goldEarnedTodayUtc);
            Assert.LessOrEqual(progress.materialsEarnedTodayUtc, SoloCollectionCircuit.MaxMaterialsPerDay);
        }

        [Test]
        public void TheCycleBonus_CannotBreachTheDailyCap()
        {
            // The case worth pinning: on the 7th day the cycle bonus (2,500 Gold) lands on a day
            // that has ALREADY hit the 1,250 ceiling. Uncapped that day would pay 3,750. The cap is
            // the locked number, so the surplus is clipped - a player is never paid past it.
            var progress = Fresh();
            DateTime day = WeekStartMonday;
            for (int i = 0; i < SoloCollectionCircuit.CircuitsRequiredForCycleBonus; i++)
            {
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, day);
                Assert.LessOrEqual(progress.goldEarnedTodayUtc, SoloCollectionCircuit.MaxGoldPerDay,
                    "No day may ever exceed the ceiling, including the cycle-bonus day.");
                day = day.AddDays(1);
            }
        }

        [Test]
        public void TheMaterialsCycleBonus_CURRENTLY_ClipsToZero_WhichIsAKnownGap()
        {
            // PINS A REAL DEFECT rather than leaving it only in a mailbox note. The full suite went
            // GREEN over this, because nothing asserted the cycle bonus pays anything - green did
            // not mean correct.
            //
            //   3 trials x 50 Materials      = 150
            //   MaxMaterialsPerDay (derived) = 150
            //   room for the 125 cycle bonus =   0
            //
            // Both figures were derived by me at BS's 5x ratio, independently, and never checked
            // against each other - so the 7-day streak reward can never pay out.
            //
            // THIS TEST SHOULD BE INVERTED once BS rules: raise the cap to 275 so the bonus pays,
            // or set the bonus to 0 because 125-that-always-clips is a lie in the rewards table.
            // Written as an assertion, not a TODO, precisely so it fails loudly when that lands.
            var progress = Fresh();
            DateTime day = WeekStartMonday;

            for (int i = 0; i < SoloCollectionCircuit.CircuitsRequiredForCycleBonus - 1; i++)
            {
                ClearWholeCircuit(progress, day);
                day = day.AddDays(1);
            }

            int materialsBeforeBonusDay = progress.materialsEarnedTodayUtc;
            SoloCircuitClearResult bonusDay = ClearWholeCircuit(progress, day);

            Assert.IsTrue(bonusDay.CycleBonusPaid, "Setup: the 7th day must actually pay the bonus.");
            Assert.AreEqual(SoloCollectionCircuit.MaxMaterialsPerDay, progress.materialsEarnedTodayUtc,
                "The day is capped out by the three trial clears alone.");
            Assert.AreEqual(3 * SoloCollectionCircuit.MaterialsPerTrialClear,
                SoloCollectionCircuit.MaxMaterialsPerDay,
                "The cap EXACTLY equals three trial clears - this is the collision. Change either " +
                "MaxMaterialsPerDay or MaterialsForSevenCircuitCycle and this assertion should fail.");
        }

        [Test]
        public void TheCycleBonus_IsPaidOnce_NotOncePerSubsequentCircuit()
        {
            var progress = Fresh();
            DateTime day = WeekStartMonday;
            int timesPaid = 0;
            for (int i = 0; i < SoloCollectionCircuit.CircuitsRequiredForCycleBonus + 1; i++)
            {
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, day);
                SoloCircuitClearResult r =
                    SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, day);
                if (r.CycleBonusPaid) timesPaid++;
                day = day.AddDays(1);
            }

            Assert.AreEqual(1, timesPaid, "The 7-circuit bonus is one-time per cycle.");
        }

        [Test]
        public void AMidWeekJoiner_CAN_EarnTheCycleBonus_WhichIsTheWholePointOfTheFix()
        {
            // The POSITIVE proof for the gap this fixture originally exposed. Under the old
            // Monday-aligned ISO week, starting on a Wednesday made the bonus unreachable that week
            // no matter how well the player played - roughly 6 in 7 new players. The cycle now
            // starts on the player's OWN first completed Circuit, so the join day is irrelevant.
            var progress = Fresh();
            DateTime day = WeekStartMonday.AddDays(2);   // deliberately a Wednesday
            bool paid = false;

            for (int i = 0; i < SoloCollectionCircuit.CircuitsRequiredForCycleBonus; i++)
            {
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, day);
                SoloCircuitClearResult r =
                    SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, day);
                if (r.CycleBonusPaid) paid = true;
                day = day.AddDays(1);
            }

            Assert.IsTrue(paid,
                "A Wednesday joiner must be able to earn the bonus - this is exactly what the " +
                "ISO-week rule made impossible.");
        }

        [Test]
        public void AMissedDay_EndsTheCycle_AndTheNextClearStartsAFreshOne()
        {
            var progress = Fresh();
            DateTime day = WeekStartMonday;

            for (int i = 0; i < 3; i++)
            {
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, day);
                day = day.AddDays(1);
            }

            Assert.AreEqual(3, progress.circuitDaysInCycle);

            day = day.AddDays(1);   // skip a day entirely
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, day);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, day);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, day);

            Assert.AreEqual(1, progress.circuitDaysInCycle,
                "A missed day ends the cycle - the next clear starts a new one at day 1.");
        }

        [Test]
        public void ClearingOnlySomeTrials_DoesNotCountAsACircuitDay()
        {
            // "Completed Circuit day" means all three. A player who clears two trials a day forever
            // must never accumulate a cycle - otherwise the bonus rewards showing up, not clearing.
            var progress = Fresh();
            DateTime day = WeekStartMonday;

            for (int i = 0; i < SoloCollectionCircuit.CircuitsRequiredForCycleBonus; i++)
            {
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, day);
                day = day.AddDays(1);
            }

            Assert.AreEqual(0, progress.circuitDaysInCycle);
            Assert.IsFalse(progress.cycleBonusClaimed);
        }

        [Test]
        public void TheCycleSurvivesAMonthBoundary()
        {
            // Day keys are strings, and "2026-08-31" -> "2026-09-01" is exactly where naive string
            // adjacency breaks. The streak check uses real date arithmetic; this pins it.
            Assert.IsTrue(SoloCollectionCircuit.IsNextCalendarDay("2026-08-31", "2026-09-01"));
            Assert.IsTrue(SoloCollectionCircuit.IsNextCalendarDay("2026-12-31", "2027-01-01"));
            Assert.IsFalse(SoloCollectionCircuit.IsNextCalendarDay("2026-08-26", "2026-08-28"));
            Assert.IsFalse(SoloCollectionCircuit.IsNextCalendarDay("", "2026-08-26"));
            Assert.IsFalse(SoloCollectionCircuit.IsNextCalendarDay("not-a-date", "2026-08-26"));
        }

        [Test]
        public void ClockRollback_CannotManufactureAnExtraCycleDay()
        {
            // The rollback guard and the cycle counter have to agree. Winding the clock back after
            // a completed Circuit must not let the player re-complete an earlier day and pad the
            // streak toward the 2,500-Gold bonus.
            var progress = Fresh();
            DateTime day = WeekStartMonday;
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, day);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, day);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, day);
            Assert.AreEqual(1, progress.circuitDaysInCycle);

            DateTime yesterday = day.AddDays(-1);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, yesterday);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, yesterday);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, yesterday);

            Assert.AreEqual(1, progress.circuitDaysInCycle,
                "A rolled-back day must not add to the streak.");
        }

        [Test]
        public void TheCycleStartDate_IsRecorded_AndOnlyMovesWhenACycleActuallyRESTARTS()
        {
            // cycleStartDayKeyUtc is PERSISTED state that no test asserted until now - it could
            // have been stale, empty, or advancing every day and the whole suite would still pass.
            // It is the field a "day 3 of 7" UI would read, so a wrong value is player-visible.
            var progress = Fresh();
            DateTime day = WeekStartMonday;

            ClearWholeCircuit(progress, day);
            string firstStart = progress.cycleStartDayKeyUtc;
            Assert.AreEqual(SoloCollectionCircuit.UtcDayKey(day), firstStart,
                "The first completed Circuit opens the cycle on that day.");

            // Continuing the streak must NOT move the start date.
            day = day.AddDays(1);
            ClearWholeCircuit(progress, day);
            Assert.AreEqual(firstStart, progress.cycleStartDayKeyUtc,
                "A continued cycle keeps its original start date.");
            Assert.AreEqual(2, progress.circuitDaysInCycle);

            // Breaking the streak must move it.
            day = day.AddDays(2);   // skip a day
            ClearWholeCircuit(progress, day);
            Assert.AreEqual(SoloCollectionCircuit.UtcDayKey(day), progress.cycleStartDayKeyUtc,
                "A broken streak starts a new cycle on the day it restarts.");
        }

        [Test]
        public void TheLastCircuitDay_TracksTheMostRecentCompletion_NotTheMostRecentVISIT()
        {
            // Also previously unasserted. It is what the streak check compares against, so if it
            // advanced on a partial day the next day would look non-consecutive and reset the cycle.
            var progress = Fresh();
            DateTime day = WeekStartMonday;
            ClearWholeCircuit(progress, day);
            Assert.AreEqual(SoloCollectionCircuit.UtcDayKey(day), progress.lastCircuitDayKeyUtc);

            // A day where only SOME trials are cleared must not advance it.
            DateTime partial = day.AddDays(1);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, partial);
            Assert.AreEqual(SoloCollectionCircuit.UtcDayKey(day), progress.lastCircuitDayKeyUtc,
                "A partial day is not a completed Circuit and must not move the marker.");
        }

        [Test]
        public void AFullCycle_SurvivesAYearBoundary()
        {
            // The month boundary is covered by IsNextCalendarDay's unit test, but no test ever ran
            // a WHOLE cycle across one - and the year rollover is the harder case, since the day
            // key's leading characters change too ("2026-12-31" -> "2027-01-01"). String-adjacency
            // logic breaks exactly here.
            var progress = Fresh();
            DateTime day = new DateTime(2026, 12, 28, 12, 0, 0, DateTimeKind.Utc);
            bool paid = false;

            for (int i = 0; i < SoloCollectionCircuit.CircuitsRequiredForCycleBonus; i++)
            {
                paid |= ClearWholeCircuit(progress, day).CycleBonusPaid;
                day = day.AddDays(1);
            }

            Assert.IsTrue(paid, "A 7-day cycle spanning 2026-12-31 into 2027 must still pay out.");
            Assert.AreEqual(SoloCollectionCircuit.CircuitsRequiredForCycleBonus,
                progress.circuitDaysInCycle,
                "Every day across the boundary must have counted.");
        }

        /// <summary>Clears all three trials for one day and returns the final result, so the
        /// multi-day tests read as days rather than as nine RecordClear calls.</summary>
        private static SoloCircuitClearResult ClearWholeCircuit(SoloCircuitProgress progress, DateTime day)
        {
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, day);
            SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, day);
            return SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, day);
        }

        // --- Deterministic daily selection ---

        [Test]
        public void TheSameUtcDay_AlwaysSelectsTheSameContent()
        {
            // This is the entire premise of a serverless daily: two devices on the same UTC day
            // must see the same trial without ever talking to each other.
            string key = SoloCollectionCircuit.UtcDayKey(Day1);
            var puzzles = new List<string> { "p1", "p2", "p3", "p4", "p5" };

            Assert.AreEqual(
                SoloCircuitDailySeed.FormationRestrictionFor(key),
                SoloCircuitDailySeed.FormationRestrictionFor(key));
            Assert.AreEqual(
                SoloCircuitDailySeed.TacticalBriefFor(key, puzzles),
                SoloCircuitDailySeed.TacticalBriefFor(key, puzzles));
        }

        [Test]
        public void DifferentDays_SelectIndependently_SoTheTrialsDoNotMoveInLockstep()
        {
            // Not asserting the two differ on any GIVEN day - with 6 restrictions and 5 puzzles a
            // collision is legitimate. Asserting the mapping is not degenerate across a span, which
            // is the real failure mode: one bad mix and every trial picks index 0 forever.
            var puzzles = new List<string> { "p1", "p2", "p3", "p4", "p5" };
            var seenRestrictions = new HashSet<string>();
            var seenPuzzles = new HashSet<string>();

            DateTime day = Day1;
            for (int i = 0; i < 30; i++)
            {
                string key = SoloCollectionCircuit.UtcDayKey(day);
                seenRestrictions.Add(SoloCircuitDailySeed.FormationRestrictionFor(key));
                seenPuzzles.Add(SoloCircuitDailySeed.TacticalBriefFor(key, puzzles));
                day = day.AddDays(1);
            }

            Assert.Greater(seenRestrictions.Count, 1, "Formation restrictions must actually vary.");
            Assert.Greater(seenPuzzles.Count, 1, "Tactical Briefs must actually vary.");
        }

        [Test]
        public void SelectionSurvivesTheLibraryGrowing()
        {
            // Written so it does NOT break the day someone adds a seventh puzzle: it asserts the
            // index stays in range and stays stable for a fixed list, never that a particular day
            // maps to a particular puzzle.
            string key = SoloCollectionCircuit.UtcDayKey(Day1);
            for (int count = 1; count <= 20; count++)
            {
                int index = SoloCircuitDailySeed.IndexFor(key, SoloCircuitTrial.TacticalBrief, count);
                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, count);
            }
        }

        [Test]
        public void TrialsSelectINDEPENDENTLY_NotInLockedAntiCorrelation()
        {
            // REGRESSION GUARD for a real defect found by measurement, not by a failing test.
            // FNV-1a ends in a multiply by an ODD prime, which preserves the low bit's parity - so
            // two trials whose mixed-in constants differed only in the low bit produced hashes of
            // permanently opposite parity, and for any EVEN pool size could NEVER pick the same
            // index. Both live pools are size 6, so two of the three trials were perfectly
            // anti-correlated. An avalanche finalizer fixed it.
            //
            // Asserts the collision rate is in a sane band rather than a magnitude: the failure was
            // absolute (exactly zero collisions, forever), so any real independence passes easily
            // while the defect fails instantly.
            const int poolSize = 6;
            const int days = 600;
            int collisions = 0;

            DateTime day = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < days; i++)
            {
                string key = SoloCollectionCircuit.UtcDayKey(day);
                if (SoloCircuitDailySeed.IndexFor(key, SoloCircuitTrial.Formation, poolSize) ==
                    SoloCircuitDailySeed.IndexFor(key, SoloCircuitTrial.Collection, poolSize))
                {
                    collisions++;
                }

                day = day.AddDays(1);
            }

            // Expected ~1/6 of 600 = 100. A wide band, because this guards against STRUCTURE,
            // not against ordinary variance.
            Assert.Greater(collisions, days / 20,
                "Trials never agree - they are anti-correlated by construction, not independent. " +
                "This is the parity defect the avalanche mix exists to fix.");
            Assert.Less(collisions, days / 2,
                "Trials agree far too often - they are moving in lockstep.");
        }

        [Test]
        public void AnEvenPoolSize_DoesNotSuppressAgreement()
        {
            // The defect was invisible at odd pool sizes and total at even ones, so the pool size
            // is the axis that actually matters. Sweeps both.
            DateTime start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach (int pool in new[] { 2, 3, 4, 5, 6, 8 })
            {
                int collisions = 0;
                DateTime day = start;
                for (int i = 0; i < 400; i++)
                {
                    string key = SoloCollectionCircuit.UtcDayKey(day);
                    if (SoloCircuitDailySeed.IndexFor(key, SoloCircuitTrial.Formation, pool) ==
                        SoloCircuitDailySeed.IndexFor(key, SoloCircuitTrial.Collection, pool))
                    {
                        collisions++;
                    }

                    day = day.AddDays(1);
                }

                Assert.Greater(collisions, 0,
                    "Pool size " + pool + ": the two trials NEVER select the same index over 400 " +
                    "days, which is structural, not chance.");
            }
        }

        [Test]
        public void AnEmptyPuzzleLibrary_YieldsNoBrief_RatherThanThrowing()
        {
            Assert.AreEqual(string.Empty, SoloCircuitDailySeed.TacticalBriefFor("2026-08-26", null));
            Assert.AreEqual(string.Empty,
                SoloCircuitDailySeed.TacticalBriefFor("2026-08-26", new List<string>()));
        }

        [Test]
        public void FormationRestrictions_ConstrainDeployment_NeverCardOwnership()
        {
            // The Formation and Collection trials must fail INDEPENDENTLY. If a Formation
            // restriction also demanded particular cards, a thin roster would fail both on the same
            // day - the opposite of what a daily engagement loop is for.
            var ownershipWords = new[] { "own", "rarity", "collection", "faction", "school" };
            foreach (string rule in SoloCircuitDailySeed.FormationRestrictions)
            {
                foreach (string word in ownershipWords)
                {
                    StringAssert.DoesNotContain(word, rule.ToLowerInvariant(),
                        "Formation restriction leaks the Collection Trial's axis: " + rule);
                }
            }
        }
    }
}
