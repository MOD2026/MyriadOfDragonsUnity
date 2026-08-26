using MyriadOfDragons.Battle;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Combat Resolution Stage queue policy.
    ///
    /// The stage's visuals are not tested here - pixels are not where the risk is. The risk is the
    /// catch-up policy: presentation must never delay combat, so it merges beats instead, and a
    /// merge that swallows the wrong beat silently destroys the only feedback a player gets that
    /// something irreversible happened.
    /// </summary>
    public class CombatResolutionQueueTests
    {
        private static CombatResolutionEvent Clash(int tick, int value, Lane lane = Lane.Front,
            CombatResolutionSide side = CombatResolutionSide.Player) =>
            new CombatResolutionEvent(CombatResolutionEventType.ClashResolved, side, tick,
                signedValue: value, lane: lane, hasLane: true);

        private static CombatResolutionEvent Defeat(int tick, Lane lane = Lane.Front) =>
            new CombatResolutionEvent(CombatResolutionEventType.CardDefeated,
                CombatResolutionSide.Enemy, tick, lane: lane, hasLane: true, remainingSlots: 0);

        private static CombatResolutionEvent AvatarHit(int tick, int value) =>
            new CombatResolutionEvent(CombatResolutionEventType.AvatarHealthChanged,
                CombatResolutionSide.Player, tick, signedValue: value);

        [Test]
        public void RedundantBeatsFromTheSameTick_CoalesceIntoOneAggregate()
        {
            // The doc's rule: "do not play nine individual full animations; aggregate into one lane
            // beat". Nine separate 1-damage beats would take longer to present than the match takes
            // to resolve.
            var queue = new CombatResolutionQueue();
            queue.Enqueue(Clash(tick: 3, value: -2));
            queue.Enqueue(Clash(tick: 3, value: -3));
            queue.Enqueue(Clash(tick: 3, value: -1));

            Assert.AreEqual(1, queue.PendingCount, "Three redundant beats must present as one.");
            queue.TryAdvance();
            Assert.AreEqual(-6, queue.Active.SignedValue,
                "The aggregate must equal what ACTUALLY happened - showing only the last number " +
                "would under-report the tick.");
        }

        [Test]
        public void BeatsFromDIFFERENTTicks_NeverCoalesce()
        {
            // Merging across ticks would tell the player two separate exchanges were one.
            var queue = new CombatResolutionQueue();
            queue.Enqueue(Clash(tick: 1, value: -2));
            queue.Enqueue(Clash(tick: 2, value: -2));

            Assert.AreEqual(2, queue.PendingCount);
        }

        [Test]
        public void BeatsFromDifferentSidesOrLanes_NeverCoalesce()
        {
            // Direction and lane are load-bearing in the visual grammar - the doc requires motion
            // direction and source to AGREE with the numbers. Merging across them would produce a
            // beat that points the wrong way.
            var queue = new CombatResolutionQueue();
            queue.Enqueue(Clash(tick: 1, value: -2, side: CombatResolutionSide.Player));
            queue.Enqueue(Clash(tick: 1, value: -2, side: CombatResolutionSide.Enemy));
            Assert.AreEqual(2, queue.PendingCount, "Opposite sides must stay separate beats.");

            var laneQueue = new CombatResolutionQueue();
            laneQueue.Enqueue(Clash(tick: 1, value: -2, lane: Lane.Front));
            laneQueue.Enqueue(Clash(tick: 1, value: -2, lane: Lane.Back));
            Assert.AreEqual(2, laneQueue.PendingCount, "Different lanes must stay separate beats.");
        }

        [Test]
        public void DefeatAvatarAndStrikeBeats_AreNEVERCoalescedAway()
        {
            // THE ONE THAT MATTERS. These carry irreversible information - a card died, the Avatar
            // was hit. Catching up must never be allowed to swallow them, however far behind the
            // presentation falls.
            var queue = new CombatResolutionQueue();
            queue.Enqueue(Defeat(tick: 5));
            queue.Enqueue(Defeat(tick: 5));
            queue.Enqueue(AvatarHit(tick: 5, value: -4));
            queue.Enqueue(AvatarHit(tick: 5, value: -3));

            Assert.AreEqual(4, queue.PendingCount,
                "Critical beats must survive individually - merging them loses the only feedback " +
                "that something irreversible happened.");
        }

        [Test]
        public void ACriticalBeat_DoesNotAbsorbAnOrdinaryOneQueuedAfterIt()
        {
            var queue = new CombatResolutionQueue();
            queue.Enqueue(Defeat(tick: 2));
            queue.Enqueue(Clash(tick: 2, value: -1));

            Assert.AreEqual(2, queue.PendingCount);
        }

        [Test]
        public void TheQueueIndicator_ShowsAtMostTheActiveBeatPlusTwo()
        {
            // "Keep at most two future beats visibly queued; the internal queue may hold more."
            // The backlog is real; advertising all of it would turn a three-diamond indicator into
            // a progress bar for something the player cannot influence.
            var queue = new CombatResolutionQueue();
            for (int i = 0; i < 9; i++) queue.Enqueue(Clash(tick: i, value: -1));
            queue.TryAdvance();

            Assert.AreEqual(3, queue.VisibleQueueIndicators,
                "Active + 2 queued, never the full backlog.");
            Assert.Greater(queue.PendingCount, CombatResolutionQueue.VisibleQueuedBeats,
                "The internal queue genuinely holds more than it advertises.");
        }

        [Test]
        public void AdvancingAnEmptyQueue_ReportsNothingRatherThanRepeatingTheLastBeat()
        {
            var queue = new CombatResolutionQueue();
            Assert.IsFalse(queue.TryAdvance());
            Assert.IsFalse(queue.HasActive,
                "A drained queue must go idle, not hold a stale beat on screen.");
        }

        [Test]
        public void Clear_DropsEverythingIncludingCriticalBeats()
        {
            // Deliberately unconditional. On scene exit or replay skip nothing will be presented,
            // so preserving critical beats would only leak them into the NEXT match's stage.
            var queue = new CombatResolutionQueue();
            queue.Enqueue(Defeat(tick: 1));
            queue.Enqueue(AvatarHit(tick: 1, value: -5));
            queue.TryAdvance();

            queue.Clear();

            Assert.AreEqual(0, queue.PendingCount);
            Assert.IsFalse(queue.HasActive);
        }

        [Test]
        public void MergingTakesTheHIGHERVisualTier_NeverAveragesIt()
        {
            // Tier is a classification, not a magnitude. A heavy effect merged with an ordinary one
            // must not be downgraded to ordinary - the doc reserves heavy for effects already
            // classified heavy.
            var queue = new CombatResolutionQueue();
            queue.Enqueue(new CombatResolutionEvent(CombatResolutionEventType.ClashResolved,
                CombatResolutionSide.Player, 1, signedValue: -1, lane: Lane.Front, hasLane: true,
                tier: CombatResolutionTier.Medium));
            queue.Enqueue(new CombatResolutionEvent(CombatResolutionEventType.ClashResolved,
                CombatResolutionSide.Player, 1, signedValue: -1, lane: Lane.Front, hasLane: true,
                tier: CombatResolutionTier.Heavy));

            queue.TryAdvance();
            Assert.AreEqual(CombatResolutionTier.Heavy, queue.Active.Tier);
        }

        [Test]
        public void UnknownSlotState_IsMinusOne_NotZero()
        {
            // 0 is a REAL value meaning "lane emptied". If unknown were also 0, every unrelated
            // beat would extinguish the lane pips.
            var beat = Clash(tick: 1, value: -2);
            Assert.AreEqual(-1, beat.RemainingSlots);
            Assert.AreEqual(0, Defeat(tick: 1).RemainingSlots);
        }
    }
}
