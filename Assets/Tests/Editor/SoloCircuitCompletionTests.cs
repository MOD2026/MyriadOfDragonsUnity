using System;
using System.Collections.Generic;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Empire;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Real completion signals feeding Circuit trial clears.
    ///
    /// The point of these: before this class existed, the screen granted a trial clear when the
    /// player TAPPED it. That made the rewards claimable by opening a screen. Everything below is
    /// about refusing a clear that was not actually earned.
    /// </summary>
    public class SoloCircuitCompletionTests
    {
        private static readonly DateTime NowUtc =
            new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

        private static readonly List<string> Puzzles =
            new List<string> { "p1", "p2", "p3", "p4", "p5" };

        private static string TodaysBrief() =>
            SoloCircuitDailySeed.TacticalBriefFor(SoloCollectionCircuit.UtcDayKey(NowUtc), Puzzles);

        [Test]
        public void SolvingTodaysBrief_ClearsTheTrial()
        {
            var progress = new SoloCircuitProgress();

            SoloCircuitClearResult r = SoloCircuitCompletion.ReportPuzzleSolved(
                progress, Puzzles, TodaysBrief(), NowUtc);

            Assert.IsTrue(r.Cleared, r.Message);
            Assert.AreEqual(SoloCircuitTrial.TacticalBrief, r.Trial);
            Assert.AreEqual(SoloCollectionCircuit.GoldPerTrialClear, r.GoldGranted);
        }

        [Test]
        public void SolvingADIFFERENTPuzzle_DoesNotClearTheBrief()
        {
            // The one that matters. War Room free-play opens the SAME puzzle library, so crediting
            // any solve would let a player clear the daily brief without ever engaging with the
            // day's selection - and they would do it by accident, constantly.
            var progress = new SoloCircuitProgress();
            string brief = TodaysBrief();
            string other = null;
            foreach (string id in Puzzles) { if (id != brief) { other = id; break; } }

            SoloCircuitClearResult r = SoloCircuitCompletion.ReportPuzzleSolved(
                progress, Puzzles, other, NowUtc);

            Assert.IsFalse(r.Cleared, "Solving some puzzle is not solving TODAY'S puzzle.");
            Assert.AreEqual(0, r.GoldGranted);
            StringAssert.Contains(brief, r.Message, "The refusal must name the actual brief.");
        }

        [Test]
        public void ReportingTheSameSolveTwice_OnlyClearsOnce()
        {
            // A presenter that rebuilds, or a double-tap on exit, must not pay twice.
            var progress = new SoloCircuitProgress();
            SoloCircuitCompletion.ReportPuzzleSolved(progress, Puzzles, TodaysBrief(), NowUtc);

            SoloCircuitClearResult again = SoloCircuitCompletion.ReportPuzzleSolved(
                progress, Puzzles, TodaysBrief(), NowUtc);

            Assert.IsFalse(again.Cleared);
            Assert.AreEqual(SoloCollectionCircuit.GoldPerTrialClear,
                progress.goldEarnedTodayUtc,
                "Exactly one trial's worth of Gold, not two.");
        }

        [Test]
        public void AnEmptyPuzzleLibrary_RefusesHonestly_RatherThanCreditingNothing()
        {
            var progress = new SoloCircuitProgress();

            SoloCircuitClearResult r = SoloCircuitCompletion.ReportPuzzleSolved(
                progress, new List<string>(), "p1", NowUtc);

            Assert.IsFalse(r.Cleared);
            StringAssert.Contains("library is empty", r.Message);
        }

        [Test]
        public void WinningUnderTodaysRestriction_ClearsTheFormationTrial()
        {
            var progress = new SoloCircuitProgress();
            string rule = SoloCircuitDailySeed.FormationRestrictionFor(
                SoloCollectionCircuit.UtcDayKey(NowUtc));

            // Build a play that actually satisfies whatever today's seed selected, rather than
            // hardcoding one restriction - otherwise this test silently stops exercising the pass
            // path the day the seed picks a different rule.
            var deployments = new List<SoloCircuitDeployment>();
            foreach (Lane lane in new[] { Lane.Front, Lane.Back, Lane.Middle })
            {
                var candidate = new List<SoloCircuitDeployment> { new SoloCircuitDeployment(lane, 0, 1) };
                if (SoloCircuitFormationRule.IsSatisfied(rule, candidate))
                {
                    deployments = candidate;
                    break;
                }
            }

            Assert.IsNotEmpty(deployments, "No single-unit play satisfies today's rule: " + rule);

            SoloCircuitClearResult r = SoloCircuitCompletion.ReportBattleFinished(
                progress, deployments, isVictory: true, nowUtc: NowUtc);

            Assert.IsTrue(r.Cleared, r.Message);
            Assert.AreEqual(SoloCircuitTrial.Formation, r.Trial);
        }

        [Test]
        public void LosingTheBattle_NeverClearsFormation_HoweverWellTheRuleWasObeyed()
        {
            var progress = new SoloCircuitProgress();
            var deployments = new List<SoloCircuitDeployment> { new SoloCircuitDeployment(Lane.Front, 0, 1) };

            SoloCircuitClearResult r = SoloCircuitCompletion.ReportBattleFinished(
                progress, deployments, isVictory: false, nowUtc: NowUtc);

            Assert.IsFalse(r.Cleared);
            Assert.AreEqual(0, progress.goldEarnedTodayUtc, "A loss must grant nothing.");
        }

        [Test]
        public void AnOrdinaryBattle_DoesNotAccidentallyClearFormation()
        {
            // Most battles are NOT played under the restriction. Reporting every match end must be
            // cheap and silent, not a stream of accidental daily clears - this is the common case,
            // and the one that would leak rewards if the rule were loose.
            var progress = new SoloCircuitProgress();
            string rule = SoloCircuitDailySeed.FormationRestrictionFor(
                SoloCollectionCircuit.UtcDayKey(NowUtc));

            // A sprawling 5-unit play across every lane: breaks every restriction in the pool
            // except the widest, and is what a normal match actually looks like.
            var sprawl = new List<SoloCircuitDeployment>
            {
                new SoloCircuitDeployment(Lane.Front, 0, 9),
                new SoloCircuitDeployment(Lane.Front, 1, 9),
                new SoloCircuitDeployment(Lane.Middle, 2, 9),
                new SoloCircuitDeployment(Lane.Back, 3, 9),
                new SoloCircuitDeployment(Lane.Middle, 4, 9),
            };

            if (SoloCircuitFormationRule.IsSatisfied(rule, sprawl))
            {
                Assert.Pass("Today's rule is permissive enough to allow a sprawling play: " + rule);
            }

            SoloCircuitClearResult r = SoloCircuitCompletion.ReportBattleFinished(
                progress, sprawl, isVictory: true, nowUtc: NowUtc);

            Assert.IsFalse(r.Cleared, "A normal win must not clear a restricted trial.");
            StringAssert.Contains("restriction", r.Message);
        }

        [Test]
        public void AQualifyingRoster_ClearsTheCollectionTrial()
        {
            var progress = new SoloCircuitProgress();
            SoloCircuitRarityBand band =
                SoloCircuitCollectionRule.BandFor(SoloCollectionCircuit.UtcDayKey(NowUtc));

            // Build a roster that satisfies whatever band today actually selected, rather than
            // hardcoding one - the test must not silently stop exercising the pass path when the
            // seed picks a different band.
            var owned = new List<string>();
            var rarity = new Dictionary<string, int>();
            for (int i = 0; i < band.RequiredCards; i++)
            {
                string id = "card" + i;
                owned.Add(id);
                rarity[id] = band.MinRarity;
            }

            SoloCircuitClearResult r = SoloCircuitCompletion.ReportCollectionChecked(
                progress, owned, id => rarity.TryGetValue(id, out int v) ? v : 0, NowUtc);

            Assert.IsTrue(r.Cleared, r.Message);
        }

        [Test]
        public void AThinRoster_IsRefused_AndToldWhatItNeeds()
        {
            var progress = new SoloCircuitProgress();

            SoloCircuitClearResult r = SoloCircuitCompletion.ReportCollectionChecked(
                progress, new List<string>(), id => 0, NowUtc);

            Assert.IsFalse(r.Cleared);
            StringAssert.Contains("of", r.Message, "The refusal must say how far short the roster is.");
            Assert.AreEqual(0, progress.goldEarnedTodayUtc);
        }

        [Test]
        public void ClockRollback_StillBlocksACompletionSignal()
        {
            // The completion path must not become a way around the rollback guard - it routes
            // through RecordClear for exactly this reason rather than setting the mask itself.
            var progress = new SoloCircuitProgress();
            SoloCircuitCompletion.ReportPuzzleSolved(progress, Puzzles, TodaysBrief(), NowUtc);

            DateTime yesterday = NowUtc.AddDays(-1);
            string yesterdaysBrief = SoloCircuitDailySeed.TacticalBriefFor(
                SoloCollectionCircuit.UtcDayKey(yesterday), Puzzles);

            SoloCircuitClearResult r = SoloCircuitCompletion.ReportPuzzleSolved(
                progress, Puzzles, yesterdaysBrief, yesterday);

            Assert.IsFalse(r.Cleared, "A rolled-back clock must not re-open a spent trial.");
        }
    }
}
