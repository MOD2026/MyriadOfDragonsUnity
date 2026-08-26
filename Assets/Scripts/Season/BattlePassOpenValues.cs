namespace MyriadOfDragons.Season
{
    public enum BattlePassClaimStatus
    {
        Applied,
        OpenValuesNotLocked,
        PremiumLocked,
    }

    public sealed class BattlePassClaimResult
    {
        public BattlePassClaimStatus Status;
        public int TierIndex;
        public bool PremiumTrack;
        public string Message;
    }

    /// <summary>
    /// Battle Pass / Season Pass — LOCKED 2026-08-24 structure only
    /// (docs/LOCKED_DECISIONS_REGISTER.md). Season length and dual-track rules are locked;
    /// XP curve, tier amounts, price, and claim grace stay OPEN (null). Production claims refuse.
    /// No PlayerProfile fields yet (frozen save).
    /// </summary>
    public static class BattlePassOpenValues
    {
        public const string RegisterCitation =
            "docs/LOCKED_DECISIONS_REGISTER.md — Battle Pass / Season Pass (LOCKED 2026-08-24, structure only)";

        /// <summary>Locked: 28-day season, UTC-week-anchored.</summary>
        public const int SeasonLengthDays = 28;

        /// <summary>Visual tier wells from Battle Pass Screen V1 — not a locked reward-count rule.</summary>
        public const int ShellTierWellCount = 8;

        /// <summary>LOCKED 2026-08-26 (BS) — flat XP per tier.</summary>
        public static readonly int? SeasonXpPerTier = 1400;

        /// <summary>OPEN — Gem (or IAP) price to unlock premium. Null = Unlock Premium must refuse.</summary>
        public static readonly int? PremiumUnlockPrice = null;

        /// <summary>OPEN — claim grace after season end.</summary>
        public static readonly int? ClaimGraceDays = null;

        public static bool AreTierRewardsConfigured => SeasonXpPerTier.HasValue && PremiumUnlockPrice.HasValue;

        public static string RuntimePlaceholder => "[runtime]";

        public static string SeasonLengthCopy => "28-DAY SEASON";

        public static string StatusNote =>
            "Battle Pass structure and Season XP per tier are locked (28-day UTC-week season, " +
            "free+paid tracks, no cards/packs/Evolution/Forge-Dust/Permits, 1400 XP/tier). Tier Gold " +
            "amounts, premium price, and claim grace are still OPEN — " + RegisterCitation;

        public static BattlePassClaimResult TryClaimTier(int tierIndex, bool premiumTrack)
        {
            return new BattlePassClaimResult
            {
                TierIndex = tierIndex,
                PremiumTrack = premiumTrack,
                Status = BattlePassClaimStatus.OpenValuesNotLocked,
                Message = StatusNote,
            };
        }

        public static BattlePassClaimResult TryUnlockPremium()
        {
            if (!PremiumUnlockPrice.HasValue)
            {
                return new BattlePassClaimResult
                {
                    Status = BattlePassClaimStatus.OpenValuesNotLocked,
                    Message = StatusNote,
                };
            }

            return new BattlePassClaimResult
            {
                Status = BattlePassClaimStatus.OpenValuesNotLocked,
                Message = StatusNote,
            };
        }
    }
}
