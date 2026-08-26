using System.Collections.Generic;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Empire;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Formation Trial rule evaluation.
    ///
    /// Every restriction in the shipped pool is exercised, because an unmatched restriction string
    /// silently returns false - a rule nobody tested would make that day's trial permanently
    /// unclearable, and the player would have no way to tell that from "you played it wrong".
    /// </summary>
    public class SoloCircuitFormationRuleTests
    {
        /// <summary>Cheap deployments (1 Resource each), so lane-based rules are never
        /// accidentally decided by the Resource ceiling instead of the lane they are testing.</summary>
        private static List<SoloCircuitDeployment> Deploys(params Lane[] lanes)
        {
            var list = new List<SoloCircuitDeployment>();
            for (int i = 0; i < lanes.Length; i++) list.Add(new SoloCircuitDeployment(lanes[i], i, 1));
            return list;
        }

        private static List<SoloCircuitDeployment> Costing(params int[] costs)
        {
            var list = new List<SoloCircuitDeployment>();
            for (int i = 0; i < costs.Length; i++)
                list.Add(new SoloCircuitDeployment(Lane.Front, i, costs[i]));
            return list;
        }

        [Test]
        public void EveryShippedRestriction_IsRecognised_AndClearableBySomeRealPlay()
        {
            // The guard that matters most. IsSatisfied returns false for any string it does not
            // recognise, so a restriction in the display pool with no matching branch here would be
            // impossible to clear - and it would look like a player mistake, not a bug.
            foreach (string restriction in SoloCircuitDailySeed.FormationRestrictions)
            {
                bool clearableBySomething =
                    SoloCircuitFormationRule.IsSatisfied(restriction, Deploys(Lane.Front)) ||
                    SoloCircuitFormationRule.IsSatisfied(restriction, Deploys(Lane.Back)) ||
                    SoloCircuitFormationRule.IsSatisfied(restriction, Deploys(Lane.Front, Lane.Back));

                Assert.IsTrue(clearableBySomething,
                    "No real play satisfies this restriction, so its day is unclearable: " + restriction);
            }
        }

        [Test]
        public void FrontLaneOnly_RejectsAnyUnitElsewhere()
        {
            const string rule = "Front lane only - no units may be deployed to the Middle or Back lane.";
            Assert.IsTrue(SoloCircuitFormationRule.IsSatisfied(rule, Deploys(Lane.Front, Lane.Front)));
            Assert.IsFalse(SoloCircuitFormationRule.IsSatisfied(rule, Deploys(Lane.Front, Lane.Middle)));
        }

        [Test]
        public void OneUnitPerLane_CountsDEPLOYMENTS_NotSurvivors()
        {
            // THE CUMULATIVE CASE, and the reason this takes a log rather than the final board.
            // Two units deployed into Front breaks the rule even if one of them died before the
            // battle ended - the end-state board would show a single unit and wrongly pass.
            const string rule = "No more than one unit per lane.";
            Assert.IsTrue(SoloCircuitFormationRule.IsSatisfied(rule, Deploys(Lane.Front, Lane.Back)));
            Assert.IsFalse(SoloCircuitFormationRule.IsSatisfied(rule, Deploys(Lane.Front, Lane.Front)),
                "A unit that was deployed and then lost still counts as having been deployed.");
        }

        [Test]
        public void AtMostThreeUnits_IsATotalAcrossTheWholeBattle()
        {
            const string rule = "Deploy at most three units for the whole battle.";
            Assert.IsTrue(SoloCircuitFormationRule.IsSatisfied(
                rule, Deploys(Lane.Front, Lane.Middle, Lane.Back)));
            Assert.IsFalse(SoloCircuitFormationRule.IsSatisfied(
                rule, Deploys(Lane.Front, Lane.Middle, Lane.Back, Lane.Front)));
        }

        [Test]
        public void TheResourceCeiling_CountsTotalSPENT_NotUnitsOnTheBoard()
        {
            // The replacement for the duplicate rule, and it is a genuinely different axis: it can
            // be broken by ONE expensive unit while a lane rule is perfectly obeyed.
            const string rule = "Clear using no more than one full bar of Resource.";
            int cap = SoloCircuitFormationRule.MaxResourceForThriftRestriction;

            Assert.IsTrue(SoloCircuitFormationRule.IsSatisfied(rule, Costing(cap)));
            Assert.IsTrue(SoloCircuitFormationRule.IsSatisfied(rule, Costing(cap / 2, cap / 2)));
            Assert.IsFalse(SoloCircuitFormationRule.IsSatisfied(rule, Costing(cap, 1)));
        }

        [Test]
        public void TheResourceCeiling_CountsUnitsThatDied_BecauseTheResourceWasStillSpent()
        {
            // Same cumulative reasoning as the unit-count rule: Resource paid for a unit that later
            // died is still Resource the player committed. Only a spend log can see it.
            const string rule = "Clear using no more than one full bar of Resource.";
            int cap = SoloCircuitFormationRule.MaxResourceForThriftRestriction;

            Assert.IsFalse(SoloCircuitFormationRule.IsSatisfied(rule, Costing(cap - 1, cap - 1)),
                "Two units costing nearly a bar each exceed the ceiling even if neither survives.");
        }

        [Test]
        public void ThePoolHasNoDuplicateConstraints()
        {
            // The bug this replacement fixed: two differently-worded entries that evaluated
            // identically, so the pool advertised six rules while offering five. Compares
            // BEHAVIOUR across a spread of plays rather than comparing strings - the duplicate
            // was invisible to string comparison and only detectable by what the rules DID.
            var probes = new List<List<SoloCircuitDeployment>>
            {
                Deploys(Lane.Front),
                Deploys(Lane.Back),
                Deploys(Lane.Middle),
                Deploys(Lane.Front, Lane.Front),
                Deploys(Lane.Front, Lane.Middle, Lane.Back),
                Deploys(Lane.Front, Lane.Middle, Lane.Back, Lane.Front),
                Costing(SoloCircuitFormationRule.MaxResourceForThriftRestriction + 5),
            };

            var seen = new Dictionary<string, string>();
            foreach (string rule in SoloCircuitDailySeed.FormationRestrictions)
            {
                string signature = string.Empty;
                foreach (List<SoloCircuitDeployment> probe in probes)
                    signature += SoloCircuitFormationRule.IsSatisfied(rule, probe) ? "1" : "0";

                if (seen.TryGetValue(signature, out string twin))
                {
                    Assert.Fail("Two restrictions are the same rule in different words: '" +
                                twin + "' and '" + rule + "'");
                }

                seen[signature] = rule;
            }
        }

        [Test]
        public void AnUnrecognisedRestriction_FAILS_RatherThanHandingOutAFreeClear()
        {
            // The safe default for a reward gate is refusal. A typo that passed would be a silent
            // free daily for everyone.
            Assert.IsFalse(SoloCircuitFormationRule.IsSatisfied("Do a barrel roll.", Deploys(Lane.Front)));
            Assert.IsFalse(SoloCircuitFormationRule.IsSatisfied(null, Deploys(Lane.Front)));
            Assert.IsFalse(SoloCircuitFormationRule.IsSatisfied("", Deploys(Lane.Front)));
        }

        [Test]
        public void ObeyingTheFormationButLosing_IsNotAClear()
        {
            // "Win under the day's restriction" is the locked wording - both halves are required.
            const string rule = "Front lane only - no units may be deployed to the Middle or Back lane.";
            var deployments = Deploys(Lane.Front);

            Assert.IsTrue(SoloCircuitFormationRule.IsCleared(rule, deployments, isVictory: true));
            Assert.IsFalse(SoloCircuitFormationRule.IsCleared(rule, deployments, isVictory: false),
                "A loss is never a clear, however well the formation was obeyed.");
        }

        [Test]
        public void WinningWhileBreakingTheRestriction_IsNotAClear()
        {
            const string rule = "Win without deploying to the Middle lane.";
            Assert.IsFalse(SoloCircuitFormationRule.IsCleared(
                rule, Deploys(Lane.Middle), isVictory: true));
        }

        [Test]
        public void NoDeploymentsAtAll_DoesNotThrow()
        {
            foreach (string restriction in SoloCircuitDailySeed.FormationRestrictions)
            {
                Assert.DoesNotThrow(() =>
                    SoloCircuitFormationRule.IsSatisfied(restriction, new List<SoloCircuitDeployment>()));
                Assert.DoesNotThrow(() => SoloCircuitFormationRule.IsSatisfied(restriction, null));
            }
        }
    }
}
