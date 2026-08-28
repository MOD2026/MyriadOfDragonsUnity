using MyriadOfDragons.Battle;
using MyriadOfDragons.UI;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR-ANIMATION-SPELL-TARGET-EXTRACT-001, 2026-08-28 - behavior-preserving extraction.
    /// Friendly-vs-enemy spell targeting was duplicated inline at three call sites
    /// (IsArmedSpellFriendlyTargeted, PlayCastImpact, and ArmSpellTargeting - the last left
    /// untouched, out of this card's authorized scope) with no test coverage anywhere, since all
    /// three sites lived inside coroutine/MonoBehaviour methods EditMode cannot exercise directly.
    /// GameBootstrap.SpellTargetsFriendlyLane is the single, pure, static source of truth now -
    /// this is its only direct coverage.
    /// </summary>
    public class SpellTargetsFriendlyLaneTests
    {
        [TestCase(SpellEffect.LaneDamage, false)]
        [TestCase(SpellEffect.LaneHeal, true)]
        [TestCase(SpellEffect.LaneAttackBuff, true)]
        [TestCase(SpellEffect.AvatarStrike, false)]
        public void SpellTargetsFriendlyLane_MatchesTheApprovedMapping(SpellEffect effect, bool expectedFriendly)
        {
            Assert.AreEqual(expectedFriendly, GameBootstrap.SpellTargetsFriendlyLane(effect),
                $"STATE UNREACHED: {effect} must map to friendly={expectedFriendly}.");
        }
    }
}
