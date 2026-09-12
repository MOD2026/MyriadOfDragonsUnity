namespace MyriadOfDragons.Combat
{
    public enum CombatFeedbackKind
    {
        CardPlay,
        Heal,
        Damage,
        SpellImpact,
    }

    public enum CombatPresentationBoundary
    {
        None,
        NewTick,
        Reset,
        Replay,
        Teardown,
    }

    public enum CombatPresentationInterruption
    {
        KeepCurrent,
        CoalesceIncoming,
        CancelCurrentAndPlayIncoming,
        CancelAll,
    }

    /// <summary>Pure policy for the coroutine-driven battle presentation layer. Gameplay state
    /// is already authoritative before this policy is consulted.</summary>
    public static class CombatPresentationPolicy
    {
        public const int CardPlayMs = 280;
        public const int DamageMs = 400;
        public const int HealMs = 400;
        public const int SpellImpactMs = 650;

        /// <summary>BATTLE_ANIMATION_PACKAGE_V1's "Card draw / hand arrival" beat: "180-260 ms
        /// slide/fade" - midpoint of that range.</summary>
        public const int HandArrivalMs = 220;

        /// <summary>Same beat: "stagger at most 3 cards, 60 ms apart" - the per-card delay step,
        /// capped so a hand larger than 3 cards never grows a longer stagger tail than this.</summary>
        public const int HandArrivalStaggerMs = 60;

        /// <summary>Same beat: the stagger cap itself - only the first 3 cards (index 0, 1, 2) get
        /// an incrementally later delay; every card after that starts at the 3rd card's delay.</summary>
        public const int HandArrivalMaxStaggeredCards = 3;

        public static int PriorityOf(CombatFeedbackKind kind) => kind switch
        {
            CombatFeedbackKind.CardPlay => 10,
            CombatFeedbackKind.Heal => 20,
            CombatFeedbackKind.Damage => 30,
            CombatFeedbackKind.SpellImpact => 40,
            _ => 0,
        };

        public static int ResolveDurationMs(int requestedMs, bool reducedMotion) =>
            reducedMotion ? 0 : requestedMs < 0 ? 0 : requestedMs;

        public static CombatPresentationInterruption DecideInterruption(
            CombatFeedbackKind active,
            CombatFeedbackKind incoming,
            CombatPresentationBoundary boundary)
        {
            if (boundary != CombatPresentationBoundary.None)
                return CombatPresentationInterruption.CancelAll;

            if (active == incoming)
                return CombatPresentationInterruption.CoalesceIncoming;

            return PriorityOf(incoming) > PriorityOf(active)
                ? CombatPresentationInterruption.CancelCurrentAndPlayIncoming
                : CombatPresentationInterruption.KeepCurrent;
        }
    }
}
