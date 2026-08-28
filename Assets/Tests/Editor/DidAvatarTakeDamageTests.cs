using MyriadOfDragons.UI;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR-ANIMATION-DAMAGE-FLASH-GATE-EXTRACT-001, 2026-08-28 - behavior-preserving extraction.
    /// RefreshEnemyHealthSegments' own doc comment calls this comparison the load-bearing "did a
    /// hit land" signal that gates the enemy health bar's damage flash, but it lived inline
    /// inside a method gated behind Application.isPlaying - unreachable from EditMode, so the
    /// claim had never actually been tested. GameBootstrap.DidAvatarTakeDamage is the single,
    /// pure, static source of truth now - this is its only direct coverage.
    /// </summary>
    public class DidAvatarTakeDamageTests
    {
        [TestCase(-1, 100, false, TestName = "NoPriorObservation_AlwaysFalse")]
        [TestCase(100, 100, false, TestName = "UnchangedHp_False")]
        [TestCase(100, 80, true, TestName = "HpDropped_True")]
        [TestCase(80, 100, false, TestName = "HpIncreased_HealOrRegen_False")]
        [TestCase(0, 0, false, TestName = "BothZero_False")]
        public void DidAvatarTakeDamage_MatchesTheApprovedMapping(int previousObservedHp, int currentHp, bool expected)
        {
            Assert.AreEqual(expected, GameBootstrap.DidAvatarTakeDamage(previousObservedHp, currentHp),
                $"STATE UNREACHED: previousObservedHp={previousObservedHp}, currentHp={currentHp} must map to {expected}.");
        }
    }
}
