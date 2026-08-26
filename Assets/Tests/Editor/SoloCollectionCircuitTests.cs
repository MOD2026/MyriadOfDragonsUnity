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
        /// 36). The weekly tests MUST start on a Monday - see
        /// TheWeeklyBonus_IsUnobtainable_WhenTheWeekIsJoinedMidWeek for why that is a real property
        /// of the feature and not a convenience of the fixture.</summary>
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
            Assert.AreEqual(SoloCollectionCircuit.AvatarXpPerTrialClear, r.AvatarXpGranted);
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
            Assert.AreEqual(1, progress.completedCircuitsThisWeek);
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
            Assert.LessOrEqual(progress.avatarXpEarnedTodayUtc, SoloCollectionCircuit.MaxAvatarXpPerDay);
        }

        [Test]
        public void TheWeeklyBonus_CannotBreachTheDailyCap()
        {
            // The case worth pinning: on the 7th day the weekly bonus (2,500 Gold) lands on a day
            // that has ALREADY hit the 1,250 ceiling. Uncapped that day would pay 3,750. The cap is
            // the locked number, so the surplus is clipped - a player is never paid past it.
            var progress = Fresh();
            DateTime day = WeekStartMonday;
            for (int i = 0; i < SoloCollectionCircuit.CircuitsRequiredForWeeklyBonus; i++)
            {
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, day);
                Assert.LessOrEqual(progress.goldEarnedTodayUtc, SoloCollectionCircuit.MaxGoldPerDay,
                    "No day may ever exceed the ceiling, including the weekly-bonus day.");
                day = day.AddDays(1);
            }
        }

        [Test]
        public void TheWeeklyBonus_IsPaidOnce_NotOncePerSubsequentCircuit()
        {
            var progress = Fresh();
            DateTime day = WeekStartMonday;
            int timesPaid = 0;
            for (int i = 0; i < SoloCollectionCircuit.CircuitsRequiredForWeeklyBonus + 1; i++)
            {
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, day);
                SoloCircuitClearResult r =
                    SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, day);
                if (r.WeeklyBonusPaid) timesPaid++;
                day = day.AddDays(1);
            }

            Assert.AreEqual(1, timesPaid, "The 7-circuit bonus is one-time per UTC week.");
        }

        [Test]
        public void TheWeeklyBonus_IsUnobtainable_WhenTheWeekIsJoinedMidWeek()
        {
            // A REAL DESIGN CONSEQUENCE, found because this fixture originally started on a
            // Wednesday and the bonus never paid. The week key is ISO-8601, so weeks begin Monday.
            // 7 completed circuits in one UTC week therefore requires a PERFECT week starting
            // Monday - a player who discovers the Circuit on any later day cannot earn that week's
            // bonus at all, no matter how well they play.
            //
            // Pinned rather than worked around: the code is behaving as specified, and this is the
            // kind of thing that reads as a bug in a support ticket. If the intent was "any 7 days"
            // or "7 in a rolling window", that is a spec change, and this test is where it surfaces.
            var progress = Fresh();
            DateTime day = WeekStartMonday.AddDays(2);   // joins on the Wednesday

            for (int i = 0; i < SoloCollectionCircuit.CircuitsRequiredForWeeklyBonus; i++)
            {
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Formation, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.Collection, day);
                SoloCollectionCircuit.RecordClear(progress, SoloCircuitTrial.TacticalBrief, day);
                day = day.AddDays(1);
            }

            Assert.IsFalse(progress.weeklyBonusClaimed,
                "Joining mid-week makes the 7-circuit bonus unreachable that week - by design, " +
                "but worth surfacing before a player reports it as broken.");
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
