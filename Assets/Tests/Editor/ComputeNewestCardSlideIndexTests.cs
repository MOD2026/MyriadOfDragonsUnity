using MyriadOfDragons.UI;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR-ANIMATION-CARD-SLIDE-INDEX-EXTRACT-001, 2026-08-28 - behavior-preserving extraction.
    /// SlideNewestCardIntoLane's own comment states the invariant ("occupied slots render before
    /// the empty-slot filler, so the newest real card is at index cardCount - 1") but the guard
    /// enforcing it lived inline inside a method gated behind Application.isPlaying -
    /// unreachable from EditMode, so the claim had never actually been tested.
    /// GameBootstrap.ComputeNewestCardSlideIndex is the single, pure, static source of truth now
    /// - this is its only direct coverage.
    /// </summary>
    public class ComputeNewestCardSlideIndexTests
    {
        [TestCase(0, 9, -1, TestName = "NoCardYet_ReturnsInvalid")]
        [TestCase(-1, 9, -1, TestName = "NegativeCardCount_ReturnsInvalid")]
        [TestCase(10, 9, -1, TestName = "CardCountExceedsContainer_StaleState_ReturnsInvalid")]
        [TestCase(1, 1, 0, TestName = "SingleCard_ReturnsZero")]
        [TestCase(3, 9, 2, TestName = "MidLane_ReturnsLastOccupiedIndex")]
        public void ComputeNewestCardSlideIndex_MatchesTheApprovedMapping(int cardCount, int containerChildCount, int expected)
        {
            Assert.AreEqual(expected, GameBootstrap.ComputeNewestCardSlideIndex(cardCount, containerChildCount),
                $"STATE UNREACHED: cardCount={cardCount}, containerChildCount={containerChildCount} must map to {expected}.");
        }
    }
}
