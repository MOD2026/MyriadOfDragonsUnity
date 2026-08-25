using System;
using MyriadOfDragons.Data;
using MyriadOfDragons.Economy;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Metagame
{
    /// <summary>
    /// Metagame half of Memory Expedition: resume/tap/claim against
    /// <see cref="MemoryExpedition"/> + <see cref="PlayerProfile"/> fields (already shipped).
    /// No Save-shape changes. XP sinks to <c>passSeasonXp</c> (same Phase-1 pattern as Daily Login).
    /// </summary>
    public static class MemoryExpeditionService
    {
        /// <summary>Phase-1: no live event-ledger helper yet — medals stay 0 until one lands.</summary>
        public static bool EventLedgerActive => false;

        public static string AccountIdFor(PlayerProfile profile)
        {
            if (profile == null) return "local";
            return string.IsNullOrWhiteSpace(profile.playerName) ? "local" : profile.playerName.Trim();
        }

        public static MemoryExpeditionState EnsureRun(PlayerProfile profile, DateTime utcNow)
        {
            if (profile == null) return null;
            MemoryExpeditionState state = MemoryExpedition.StartOrResume(
                profile.ToMemoryExpeditionState(),
                AccountIdFor(profile),
                utcNow);
            profile.ApplyMemoryExpeditionState(state);
            return state;
        }

        public static MemoryExpeditionTapResult TapTile(PlayerProfile profile, int tileIndex, DateTime utcNow)
        {
            if (profile == null)
            {
                return new MemoryExpeditionTapResult
                {
                    Status = MemoryExpeditionTapStatus.RunAlreadyOver,
                    Message = "No profile.",
                    RunOver = true,
                };
            }

            MemoryExpeditionState state = EnsureRun(profile, utcNow);
            MemoryExpeditionTapResult result = MemoryExpedition.Tap(state, tileIndex);
            profile.ApplyMemoryExpeditionState(state);
            return result;
        }

        public static MemoryExpeditionClaimResult ClaimRewards(PlayerProfile profile, DateTime utcNow)
        {
            if (profile == null)
            {
                return new MemoryExpeditionClaimResult
                {
                    Status = MemoryExpeditionClaimStatus.WrongDay,
                    Message = "No profile.",
                };
            }

            MemoryExpeditionState state = EnsureRun(profile, utcNow);
            int staminaCap = profile.maxStamina > 0 ? profile.maxStamina : profile.stamina;
            MemoryExpeditionClaimResult result = MemoryExpedition.Claim(
                state,
                utcNow,
                profile.stamina,
                staminaCap,
                EventLedgerActive);

            if (result.Status == MemoryExpeditionClaimStatus.Granted)
            {
                if (result.Gold > 0)
                    CurrencyManager.AddCurrency(profile, CurrencyType.Gold, result.Gold, persist: false);
                if (result.StaminaGranted > 0)
                    CurrencyManager.RestoreStamina(profile, result.StaminaGranted, persist: false);
                if (result.EventMedals > 0)
                    CurrencyManager.AddCurrency(profile, CurrencyType.EventMedal, result.EventMedals, persist: false);
                // Phase-1 XP sink (same as Daily Login) — escalate if Avatar XP is preferred later.
                if (result.Xp > 0)
                    profile.passSeasonXp = Math.Max(0, profile.passSeasonXp) + result.Xp;
            }

            profile.ApplyMemoryExpeditionState(state);
            return result;
        }
    }
}
