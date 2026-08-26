using System.Collections.Generic;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// Maps already-resolved combat records into presentation beats.
    ///
    /// PURE, and that is the whole point: the design doc's hard rule is "build these from the
    /// existing combat ledger/results rather than recalculating combat in UI code". Nothing here
    /// reads live battle state, decides an outcome, or does arithmetic that gameplay has not
    /// already done - every number below is copied, never derived.
    ///
    /// SIDE CONVENTION, verified rather than assumed: BattleController calls
    /// LaneBattleResolver.ResolveTurn(PlayerState, EnemyState, ...) at BattleController.cs:951, so
    /// SideA is the PLAYER and SideB is the ENEMY throughout. Getting this backwards would point
    /// every trail and proxy the wrong way while the numbers stayed correct - a bug that looks like
    /// a visual glitch and is actually a lie about who hit whom.
    /// </summary>
    public static class CombatResolutionEventMapper
    {
        /// <summary>
        /// Beats for one resolved tick, in presentation order.
        ///
        /// Order is deliberate: lane clashes first, then defeats, then avatar health. It matches
        /// causality - cards trade, cards die, the Avatar takes what got through - so a player
        /// reading the stage sees the same story the tick actually told.
        /// </summary>
        public static List<CombatResolutionEvent> MapTick(CombatTickRecord tick)
        {
            var beats = new List<CombatResolutionEvent>();

            if (tick.LaneResults != null)
            {
                foreach (LaneClashResult lane in tick.LaneResults)
                {
                    AddLaneBeats(beats, tick.TickNumber, lane);
                }
            }

            // Avatar health LAST, and only when it actually changed. A zero-damage tick must not
            // emit an avatar beat - "nothing happened to you" is not worth 0.85 seconds of stage
            // time, and the doc reserves this beat for a real signed HP delta.
            if (tick.DamageToPlayerAvatar > 0)
            {
                beats.Add(new CombatResolutionEvent(
                    CombatResolutionEventType.AvatarHealthChanged,
                    CombatResolutionSide.Player,
                    tick.TickNumber,
                    signedValue: -tick.DamageToPlayerAvatar));
            }

            if (tick.DamageToEnemyAvatar > 0)
            {
                beats.Add(new CombatResolutionEvent(
                    CombatResolutionEventType.AvatarHealthChanged,
                    CombatResolutionSide.Enemy,
                    tick.TickNumber,
                    signedValue: -tick.DamageToEnemyAvatar));
            }

            return beats;
        }

        private static void AddLaneBeats(List<CombatResolutionEvent> beats, int tickNumber, LaneClashResult lane)
        {
            int playerDefeated = lane.DefeatedCardNamesA?.Count ?? 0;
            int enemyDefeated = lane.DefeatedCardNamesB?.Count ?? 0;

            // One aggregate clash beat per lane, never one per card. The doc is explicit: "do not
            // play nine individual full animations". Overflow is reported on the side that RECEIVES
            // it, which is why the two overflow fields map to opposite sides.
            beats.Add(new CombatResolutionEvent(
                CombatResolutionEventType.ClashResolved,
                CombatResolutionSide.Neutral,
                tickNumber,
                lane: lane.Lane,
                hasLane: true,
                overflow: lane.OverflowToA + lane.OverflowToB));

            // Defeats are emitted individually and never merged (see
            // CombatResolutionEvent.IsCriticalToPreserve). A card dying is irreversible, so it gets
            // its own beat even when the queue is catching up.
            for (int i = 0; i < playerDefeated; i++)
            {
                beats.Add(new CombatResolutionEvent(
                    CombatResolutionEventType.CardDefeated,
                    CombatResolutionSide.Player,
                    tickNumber,
                    lane: lane.Lane,
                    hasLane: true,
                    remainingSlots: lane.SideACleared ? 0 : -1,
                    sourceId: lane.DefeatedCardNamesA[i]));
            }

            for (int i = 0; i < enemyDefeated; i++)
            {
                beats.Add(new CombatResolutionEvent(
                    CombatResolutionEventType.CardDefeated,
                    CombatResolutionSide.Enemy,
                    tickNumber,
                    lane: lane.Lane,
                    hasLane: true,
                    remainingSlots: lane.SideBCleared ? 0 : -1,
                    sourceId: lane.DefeatedCardNamesB[i]));
            }

            // A lane emptying is a state change the board itself shows, but the stage still marks
            // it - the doc wants the lane crest split and the floor light going dark. Only emitted
            // when the lane ACTUALLY cleared, never inferred from defeat counts, because a lane can
            // lose cards without emptying.
            if (lane.SideACleared)
            {
                beats.Add(new CombatResolutionEvent(
                    CombatResolutionEventType.LaneStateChanged,
                    CombatResolutionSide.Player,
                    tickNumber,
                    lane: lane.Lane,
                    hasLane: true,
                    remainingSlots: 0));
            }

            if (lane.SideBCleared)
            {
                beats.Add(new CombatResolutionEvent(
                    CombatResolutionEventType.LaneStateChanged,
                    CombatResolutionSide.Enemy,
                    tickNumber,
                    lane: lane.Lane,
                    hasLane: true,
                    remainingSlots: 0));
            }
        }

        /// <summary>
        /// Beat for one resolved spell cast.
        ///
        /// Tier stays Medium here on purpose. The doc forbids computing visual tier from raw damage
        /// ("visual tier must never be calculated from raw damage"), and SpellCastRecord carries no
        /// heavy/ordinary classification - so inventing one from AvatarDamageDealt would create a
        /// combat classification this game does not have. Heavy must come from real effect data
        /// when that exists.
        /// </summary>
        public static CombatResolutionEvent MapSpell(SpellCastRecord cast) =>
            new CombatResolutionEvent(
                CombatResolutionEventType.SpellResolved,
                cast.CastByPlayer ? CombatResolutionSide.Player : CombatResolutionSide.Enemy,
                cast.TickNumber,
                signedValue: -cast.AvatarDamageDealt,
                lane: cast.TargetLane,
                hasLane: true,
                sourceId: cast.SpellName ?? string.Empty);
    }
}
