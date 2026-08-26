using MyriadOfDragons.Economy;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Season
{
    public enum BattlePassClaimStatus
    {
        Applied,
        OpenValuesNotLocked,
        PremiumLocked,
        /// <summary>Tiers claim in strictly ascending order per track (ascending-guard shape,
        /// same reasoning as Loyalty) - covers both "already claimed" and "not reached yet",
        /// and an out-of-range tier index.</summary>
        NotNextTierInOrder,
    }

    public sealed class BattlePassClaimResult
    {
        public BattlePassClaimStatus Status;
        public int TierIndex;
        public bool PremiumTrack;
        public int GoldGranted;
        public string Message;
    }

    /// <summary>
    /// Battle Pass / Season Pass — LOCKED 2026-08-24 structure only
    /// (docs/LOCKED_DECISIONS_REGISTER.md). Season length and dual-track rules are locked;
    /// Season XP per tier and the free-track Gold table LOCKED 2026-08-26 - see
    /// PlayerProfile.battlePassClaimedFreeTier/battlePassClaimedPaidTier (owner sign-off
    /// "OWNER SIGN-OFF x2 + protocol extension locked", 2026-08-26). Premium price and claim
    /// grace stay OPEN (null) - premium can never actually be unlocked yet, so every paid-track
    /// claim refuses with PremiumLocked regardless of Gold table readiness.
    /// </summary>
    public static class BattlePassOpenValues
    {
        public const string RegisterCitation =
            "docs/LOCKED_DECISIONS_REGISTER.md — Battle Pass / Season Pass (LOCKED 2026-08-24, structure only)";

        /// <summary>Locked: 28-day season, UTC-week-anchored.</summary>
        public const int SeasonLengthDays = 28;

        /// <summary>Visual tier wells from Battle Pass Screen V1 - also the real tier count now
        /// that the Gold table is locked to this exact shape.</summary>
        public const int ShellTierWellCount = 8;

        /// <summary>LOCKED 2026-08-26 (BS) — flat XP per tier.</summary>
        public static readonly int? SeasonXpPerTier = 1400;

        /// <summary>LOCKED 2026-08-26 (BS) — free-track Gold per tier, index-aligned with the
        /// tier wells. Total 15,000.</summary>
        public static readonly int[] FreeTierGold = { 500, 1000, 1500, 2000, 2000, 2500, 2500, 3000 };

        /// <summary>LOCKED 2026-08-26 (BS) — paid-track Gold per tier, exactly double the
        /// free-track amount at the same index. Total 30,000.</summary>
        public static readonly int[] PaidTierGold = { 1000, 2000, 3000, 4000, 4000, 5000, 5000, 6000 };

        /// <summary>LOCKED 2026-08-26 (BS, benchmarked against Genshin/Snap $9.99 season-pass
        /// pricing) — Gem price to unlock premium. Locking the number alone does NOT make premium
        /// unlock work: TryUnlockPremium/paid-track claims still refuse, because there is no
        /// PlayerProfile field to persist "premium unlocked" - same class of gap as
        /// battlePassClaimedFreeTier/PaidTier before their sign-off, just not yet raised. See
        /// TryUnlockPremium/TryClaimTier.</summary>
        public static readonly int? PremiumUnlockPrice = 800;

        /// <summary>LOCKED 2026-08-26 (BS) — claim grace after season end, in days.</summary>
        public static readonly int? ClaimGraceDays = 7;

        /// <summary>Whether the free track can pay out for real: structure + Season XP + Gold
        /// table. Deliberately does NOT include PremiumUnlockPrice - that gate is independent and
        /// only affects the paid track (see TryClaimTier), so free-track claims aren't held
        /// hostage by an unrelated Gem price never having been decided.</summary>
        public static bool AreFreeTierRewardsConfigured => SeasonXpPerTier.HasValue;

        /// <summary>Whether EVERYTHING, including premium, is configured - kept for callers that
        /// genuinely need the combined gate (e.g. a future "season fully live" check).</summary>
        public static bool AreTierRewardsConfigured => SeasonXpPerTier.HasValue && PremiumUnlockPrice.HasValue;

        public static string RuntimePlaceholder => "[runtime]";

        public static string SeasonLengthCopy => "28-DAY SEASON";

        public static string StatusNote =>
            "Battle Pass structure, Season XP per tier, and both Gold tables are locked " +
            "(28-day UTC-week season, free+paid tracks, no cards/packs/Evolution/Forge-Dust/Permits, " +
            "1400 XP/tier, 800-Gem premium unlock, 7-day claim grace). Paid-track claims still " +
            "refuse - premium unlock has no PlayerProfile field to persist to yet — " + RegisterCitation;

        /// <summary>Number of free-track tiers already claimed, from tier 0 upward.</summary>
        public static int ClaimedFreeTierCount(PlayerProfile profile) =>
            profile == null ? 0 : System.Math.Max(0, profile.battlePassClaimedFreeTier);

        /// <summary>Number of paid-track tiers already claimed, from tier 0 upward.</summary>
        public static int ClaimedPaidTierCount(PlayerProfile profile) =>
            profile == null ? 0 : System.Math.Max(0, profile.battlePassClaimedPaidTier);

        /// <summary>
        /// Claims one tier on one track. Free track pays out for real once configured; paid
        /// track always refuses with PremiumLocked, since nothing in this codebase can ever mark
        /// premium as unlocked while PremiumUnlockPrice stays null. Ascending-only per track,
        /// same shape as ShopLoyaltyService.ClaimNext - does NOT save; the caller persists.
        /// </summary>
        public static BattlePassClaimResult TryClaimTier(PlayerProfile profile, int tierIndex, bool premiumTrack)
        {
            var result = new BattlePassClaimResult { TierIndex = tierIndex, PremiumTrack = premiumTrack };

            if (profile == null)
            {
                result.Status = BattlePassClaimStatus.NotNextTierInOrder;
                result.Message = "No profile.";
                return result;
            }

            if (!AreFreeTierRewardsConfigured)
            {
                result.Status = BattlePassClaimStatus.OpenValuesNotLocked;
                result.Message = StatusNote;
                return result;
            }

            if (tierIndex < 0 || tierIndex >= ShellTierWellCount)
            {
                result.Status = BattlePassClaimStatus.NotNextTierInOrder;
                result.Message = $"Tier {tierIndex} is out of range (0-{ShellTierWellCount - 1}).";
                return result;
            }

            if (premiumTrack)
            {
                result.Status = BattlePassClaimStatus.PremiumLocked;
                result.Message = "Premium unlock price is not locked yet - paid-track claims must refuse.";
                return result;
            }

            int claimed = ClaimedFreeTierCount(profile);
            if (tierIndex != claimed)
            {
                result.Status = BattlePassClaimStatus.NotNextTierInOrder;
                result.Message = claimed > tierIndex
                    ? $"Tier {tierIndex} was already claimed."
                    : $"Tier {tierIndex} is not claimable yet - tier {claimed} is next.";
                return result;
            }

            int gold = FreeTierGold[tierIndex];
            CurrencyManager.AddCurrency(profile, CurrencyType.Gold, gold, persist: false);
            profile.battlePassClaimedFreeTier = claimed + 1;

            result.Status = BattlePassClaimStatus.Applied;
            result.GoldGranted = gold;
            result.Message = $"Tier {tierIndex} claimed: +{gold} Gold.";
            return result;
        }

        /// <summary>Always refuses right now: PremiumUnlockPrice is locked, but there is no
        /// PlayerProfile field to persist "premium unlocked" on, so this genuinely cannot pay out
        /// yet (not a stub oversight - a real, still-open frozen-file blocker).</summary>
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
                Status = BattlePassClaimStatus.PremiumLocked,
                Message = "Premium unlock price is locked (800 Gems), but no PlayerProfile field " +
                          "exists yet to persist an unlocked state - needs a frozen-file sign-off first.",
            };
        }
    }
}
