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
        private static List<SoloCircuitDeployment> Deploys(params Lane[] lanes)
        {
            var list = new List<SoloCircuitDeployment>();
            for (int i = 0; i < lanes.Length; i++) list.Add(new SoloCircuitDeployment(lanes[i], i));
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
