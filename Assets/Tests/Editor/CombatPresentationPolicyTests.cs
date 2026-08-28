using MyriadOfDragons.Combat;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    public class CombatPresentationPolicyTests
    {
        [Test]
        public void Priority_StrongerFeedbackAlwaysOutranksWeakerFeedback()
        {
            Assert.Less(CombatPresentationPolicy.PriorityOf(CombatFeedbackKind.CardPlay),
                CombatPresentationPolicy.PriorityOf(CombatFeedbackKind.Heal));
            Assert.Less(CombatPresentationPolicy.PriorityOf(CombatFeedbackKind.Heal),
                CombatPresentationPolicy.PriorityOf(CombatFeedbackKind.Damage));
            Assert.Less(CombatPresentationPolicy.PriorityOf(CombatFeedbackKind.Damage),
                CombatPresentationPolicy.PriorityOf(CombatFeedbackKind.SpellImpact));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(650)]
        public void ReducedMotion_ResolvesEveryDurationImmediately(int requestedMs)
        {
            Assert.AreEqual(0, CombatPresentationPolicy.ResolveDurationMs(requestedMs, true));
        }

        [Test]
        public void NormalMotion_PreservesNonNegativeDurationAndClampsInvalidInput()
        {
            Assert.AreEqual(400, CombatPresentationPolicy.ResolveDurationMs(400, false));
            Assert.AreEqual(0, CombatPresentationPolicy.ResolveDurationMs(-1, false));
        }

        [TestCase(CombatPresentationBoundary.NewTick)]
        [TestCase(CombatPresentationBoundary.Reset)]
        [TestCase(CombatPresentationBoundary.Replay)]
        [TestCase(CombatPresentationBoundary.Teardown)]
        public void LifecycleBoundary_AlwaysCancelsPresentation(CombatPresentationBoundary boundary)
        {
            Assert.AreEqual(CombatPresentationInterruption.CancelAll,
                CombatPresentationPolicy.DecideInterruption(
                    CombatFeedbackKind.CardPlay, CombatFeedbackKind.SpellImpact, boundary));
        }

        [Test]
        public void SameFeedback_CoalescesInsteadOfStacking()
        {
            Assert.AreEqual(CombatPresentationInterruption.CoalesceIncoming,
                CombatPresentationPolicy.DecideInterruption(
                    CombatFeedbackKind.Damage, CombatFeedbackKind.Damage,
                    CombatPresentationBoundary.None));
        }

        [Test]
        public void HigherPriorityInterrupts_LowerPriorityDoesNot()
        {
            Assert.AreEqual(CombatPresentationInterruption.CancelCurrentAndPlayIncoming,
                CombatPresentationPolicy.DecideInterruption(
                    CombatFeedbackKind.CardPlay, CombatFeedbackKind.SpellImpact,
                    CombatPresentationBoundary.None));
            Assert.AreEqual(CombatPresentationInterruption.KeepCurrent,
                CombatPresentationPolicy.DecideInterruption(
                    CombatFeedbackKind.SpellImpact, CombatFeedbackKind.CardPlay,
                    CombatPresentationBoundary.None));
        }
    }
}
