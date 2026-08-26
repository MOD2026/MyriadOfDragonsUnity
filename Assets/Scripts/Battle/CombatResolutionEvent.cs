using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// What kind of beat the resolution stage should present.
    ///
    /// These are PRESENTATION MAPPINGS, not new gameplay events - the design doc is explicit about
    /// that, and it matters: nothing here may ever influence combat. Every value is derived from
    /// records the controller has already resolved.
    /// </summary>
    public enum CombatResolutionEventType
    {
        ClashResolved,
        SpellResolved,
        CardDefeated,
        LaneStateChanged,
        AvatarHealthChanged,
        AvatarStrikeResolved,
    }

    /// <summary>Which side a beat originates from. Drives entry anchor and trail colour.</summary>
    public enum CombatResolutionSide
    {
        Player,
        Enemy,
        Neutral,
    }

    /// <summary>
    /// Visual weight, mapped by PRESENTATION RULES ONLY.
    ///
    /// The doc forbids deriving this from raw damage ("visual tier must never be calculated from
    /// raw damage") - otherwise a big number silently invents a new combat classification that the
    /// game does not have. Heavy is reserved for effects already classified heavy, and AvatarStrike
    /// has its own exclusive tier.
    /// </summary>
    public enum CombatResolutionTier
    {
        Medium,
        Heavy,
        AvatarStrike,
    }

    /// <summary>
    /// One immutable, already-resolved beat for the Combat Resolution Stage.
    ///
    /// Deliberately a readonly struct carrying only resolved facts: the presentation layer must
    /// never recalculate combat, so there is nothing here it could recalculate FROM. Numbers are
    /// final. If a value is not in this payload, the stage does not get to display it.
    /// </summary>
    public readonly struct CombatResolutionEvent
    {
        public readonly CombatResolutionEventType Type;
        public readonly CombatResolutionSide Source;
        public readonly CombatResolutionTier Tier;

        /// <summary>Lane the beat belongs to. Meaningless for avatar-only beats; callers check Type.</summary>
        public readonly Lane Lane;
        public readonly bool HasLane;

        /// <summary>Signed result the player reads: negative for damage taken, positive for
        /// healing/gain. Already resolved - never computed here.</summary>
        public readonly int SignedValue;

        /// <summary>Overflow carried past a cleared lane, when the source data supplies it.</summary>
        public readonly int Overflow;

        /// <summary>Slots left standing on the affected side, for the lane pip display. -1 when the
        /// beat says nothing about slot state - NOT 0, because 0 is a real value meaning "lane
        /// emptied" and conflating the two would extinguish pips on every unrelated beat.</summary>
        public readonly int RemainingSlots;

        /// <summary>Card or spell identifier for the proxy thumbnail. Empty when the beat has no
        /// single source (an aggregate lane result).</summary>
        public readonly string SourceId;

        /// <summary>School/element for palette selection on spell beats.</summary>
        public readonly CardElement School;
        public readonly bool HasSchool;

        /// <summary>Authoritative tick this beat came from. Used for coalescing and ordering, never
        /// for timing playback.</summary>
        public readonly int TickIndex;

        public CombatResolutionEvent(
            CombatResolutionEventType type,
            CombatResolutionSide source,
            int tickIndex,
            int signedValue = 0,
            Lane lane = Lane.Front,
            bool hasLane = false,
            int overflow = 0,
            int remainingSlots = -1,
            string sourceId = "",
            CardElement school = CardElement.Andras,
            bool hasSchool = false,
            CombatResolutionTier tier = CombatResolutionTier.Medium)
        {
            Type = type;
            Source = source;
            TickIndex = tickIndex;
            SignedValue = signedValue;
            Lane = lane;
            HasLane = hasLane;
            Overflow = overflow;
            RemainingSlots = remainingSlots;
            SourceId = sourceId ?? string.Empty;
            School = school;
            HasSchool = hasSchool;
            Tier = tier;
        }

        /// <summary>
        /// Beats that must NEVER be dropped when the queue catches up.
        ///
        /// The doc names these explicitly: defeat, avatar-health and AvatarStrike. They are the
        /// beats that tell a player something irreversible happened, so coalescing them away would
        /// lose the only feedback that a card died or their Avatar took a hit.
        /// </summary>
        public bool IsCriticalToPreserve =>
            Type == CombatResolutionEventType.CardDefeated ||
            Type == CombatResolutionEventType.AvatarHealthChanged ||
            Type == CombatResolutionEventType.AvatarStrikeResolved;
    }
}
