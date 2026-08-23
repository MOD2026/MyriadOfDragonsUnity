namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// Read-only guild eligibility for Empire Expedition's +10% Gold bonus
    /// (LOCKED_DECISIONS_REGISTER.md). Eligibility rules (3+ validated Guild Contribution
    /// actions/UTC week, server-recorded) live on the trusted guild backend — this client
    /// must not reimplement them. Unavailable / null → fail closed to base Gold.
    /// </summary>
    public interface IGuildExpeditionBonusQuery
    {
        /// <summary>
        /// true = eligible for +10% Gold; false = known ineligible; null = service unavailable
        /// (fail closed — treat as no bonus).
        /// </summary>
        bool? TryQueryExpeditionGoldBonusEligible();
    }

    /// <summary>Default stand-in until the guild backend exists — always unavailable (fail closed).</summary>
    public sealed class UnavailableGuildExpeditionBonusQuery : IGuildExpeditionBonusQuery
    {
        public static readonly UnavailableGuildExpeditionBonusQuery Instance = new UnavailableGuildExpeditionBonusQuery();

        public bool? TryQueryExpeditionGoldBonusEligible() => null;
    }
}
