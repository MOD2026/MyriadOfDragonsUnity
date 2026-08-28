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
