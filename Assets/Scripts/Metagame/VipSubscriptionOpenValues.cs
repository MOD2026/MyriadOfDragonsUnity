using System;
using MyriadOfDragons.Economy;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Metagame
{
    public enum VipSubscriptionActionStatus
    {
        Applied,
        InsufficientGems,
        AlreadySubscribed,
        InvalidPlan,
        NotSubscribed,
        /// <summary>Retained for older refuse paths; locked entitlement no longer returns this.</summary>
        OpenValuesNotLocked,
    }

    public enum VipPlanKind
    {
        Weekly = 0,
        Fortnight = 1,
        Monthly = 2,
    }

    public sealed class VipSubscriptionActionResult
    {
        public VipSubscriptionActionStatus Status;
        public int Index;
        public string Message;
        public int ClaimsApplied;
        public int ClaimsForfeited;
    }

    /// <summary>
    /// LOCKED 2026-08-26 VIP / Subscription real entitlement
    /// (docs/LOCKED_DECISIONS_REGISTER.md — "LOCKED: VIP/Subscription real entitlement spec").
    /// Convenience-only: Gem-priced plans, scheduled Stamina claims that consume the shared Shop
    /// rolling-24h refill slots. No Auto-Fight / combat / deck / card / spell / building / timer
    /// benefits. One active subscription; unused claims expire on lapse.
    /// </summary>
    public static class VipSubscriptionOpenValues
    {
        public static string RuntimePlaceholder => "[runtime]";

        public static string StatusNote =>
            "VIP grants scheduled Stamina claims only (convenience). " +
            $"Weekly {WeeklyGemPrice} Gems / 7d (1 claim), " +
            $"Fortnight {FortnightGemPrice} Gems / 14d (2 claims), " +
            $"Monthly {MonthlyGemPrice} Gems / 30d (4 claims). " +
            $"Each claim = +{ShopStaminaCatalog.StaminaGrantPerPotion} Stamina and uses one of the " +
            $"{ShopStaminaCatalog.MaxPurchasesPerRollingDay}/24h Shop refill slots. " +
            "One active plan; no stacking. No combat or progression power.";

        public const int BenefitWellCount = 6;
        public const int StateSocketCount = 3;

        public const int WeeklyGemPrice = 800;
        public const int FortnightGemPrice = 1500;
        public const int MonthlyGemPrice = 3000;

        public static bool ArePricesConfigured => true;

        public static readonly int[] PlanGemPrices = { WeeklyGemPrice, FortnightGemPrice, MonthlyGemPrice };
        public static readonly int[] PlanDurationDays = { 7, 14, 30 };
        public static readonly int[] PlanMaxClaims = { 1, 2, 4 };
        public static readonly string[] PlanIds = { "weekly", "fortnight", "monthly" };

        public static readonly long ClaimPeriodTicks = TimeSpan.FromDays(7).Ticks;

        private static long? _nowUtcTicksForTests;

        public static void SetNowUtcTicksForTests(long? ticks) => _nowUtcTicksForTests = ticks;

        public static long NowUtcTicks() => _nowUtcTicksForTests ?? DateTime.UtcNow.Ticks;

        public static string PlanDisplayName(VipPlanKind plan) => plan switch
        {
            VipPlanKind.Weekly => "Weekly",
            VipPlanKind.Fortnight => "Fortnight",
            VipPlanKind.Monthly => "Monthly",
            _ => "Unknown",
        };

        public static bool TryParsePlanId(string planId, out VipPlanKind plan)
        {
            plan = VipPlanKind.Weekly;
            if (string.IsNullOrEmpty(planId)) return false;
            for (int i = 0; i < PlanIds.Length; i++)
            {
                if (string.Equals(PlanIds[i], planId, StringComparison.Ordinal))
                {
                    plan = (VipPlanKind)i;
                    return true;
                }
            }
            return false;
        }

        public static bool IsSubscriptionActive(PlayerProfile profile, long nowUtcTicks)
        {
            if (profile == null) return false;
            if (string.IsNullOrEmpty(profile.vipPlanId) || profile.vipExpiresUtcTicks <= 0)
                return false;
            return nowUtcTicks < profile.vipExpiresUtcTicks;
        }

        /// <summary>Default Subscribe buys Weekly (plan index 0). Prefer <see cref="TrySubscribe(VipPlanKind)"/>.</summary>
        public static VipSubscriptionActionResult TrySubscribe() => TrySubscribe(VipPlanKind.Weekly);

        public static VipSubscriptionActionResult TrySubscribe(VipPlanKind plan)
        {
            int index = (int)plan;
            if (index < 0 || index >= PlanIds.Length)
            {
                return new VipSubscriptionActionResult
                {
                    Status = VipSubscriptionActionStatus.InvalidPlan,
                    Index = index,
                    Message = "Unknown VIP plan.",
                };
            }

            PlayerProfile profile = SaveSystem.CurrentProfile;
            if (profile == null)
            {
                return new VipSubscriptionActionResult
                {
                    Status = VipSubscriptionActionStatus.InvalidPlan,
                    Index = index,
                    Message = "No profile.",
                };
            }

            long now = NowUtcTicks();
            ExpireIfLapsed(profile, now);

            if (IsSubscriptionActive(profile, now))
            {
                return new VipSubscriptionActionResult
                {
                    Status = VipSubscriptionActionStatus.AlreadySubscribed,
                    Index = index,
                    Message = $"Already subscribed ({profile.vipPlanId}) until {FormatExpiry(profile.vipExpiresUtcTicks)}. One active plan only — no stacking.",
                };
            }

            int gemCost = PlanGemPrices[index];
            if (CurrencyManager.GetBalance(profile, CurrencyType.Gems) < gemCost)
            {
                return new VipSubscriptionActionResult
                {
                    Status = VipSubscriptionActionStatus.InsufficientGems,
                    Index = index,
                    Message = $"Need {gemCost} Gems for {PlanDisplayName(plan)} VIP (have {CurrencyManager.GetBalance(profile, CurrencyType.Gems)}).",
                };
            }

            if (!CurrencyManager.SpendCurrency(profile, CurrencyType.Gems, gemCost, persist: false))
            {
                return new VipSubscriptionActionResult
                {
                    Status = VipSubscriptionActionStatus.InsufficientGems,
                    Index = index,
                    Message = $"Need {gemCost} Gems for {PlanDisplayName(plan)} VIP.",
                };
            }

            profile.vipPlanId = PlanIds[index];
            profile.vipStartedUtcTicks = now;
            profile.vipExpiresUtcTicks = now + TimeSpan.FromDays(PlanDurationDays[index]).Ticks;
            profile.vipClaimsConsumed = 0;

            VipSubscriptionActionResult claims = ProcessDueClaims(profile, now);
            SaveSystem.Save(profile);

            return new VipSubscriptionActionResult
            {
                Status = VipSubscriptionActionStatus.Applied,
                Index = index,
                ClaimsApplied = claims.ClaimsApplied,
                ClaimsForfeited = claims.ClaimsForfeited,
                Message = $"{PlanDisplayName(plan)} VIP active ({gemCost} Gems). " +
                          $"Claims {profile.vipClaimsConsumed}/{PlanMaxClaims[index]}. " +
                          DescribeClaimResult(claims),
            };
        }

        public static VipSubscriptionActionResult TryRestore()
        {
            PlayerProfile profile = SaveSystem.CurrentProfile;
            if (profile == null)
            {
                return new VipSubscriptionActionResult
                {
                    Status = VipSubscriptionActionStatus.NotSubscribed,
                    Index = -2,
                    Message = "No profile.",
                };
            }

            long now = NowUtcTicks();
            bool hadPlan = !string.IsNullOrEmpty(profile.vipPlanId) && profile.vipExpiresUtcTicks > 0;
            ExpireIfLapsed(profile, now);

            if (!IsSubscriptionActive(profile, now))
            {
                if (hadPlan)
                    SaveSystem.Save(profile);
                return new VipSubscriptionActionResult
                {
                    Status = VipSubscriptionActionStatus.NotSubscribed,
                    Index = -2,
                    Message = hadPlan
                        ? "VIP lapsed. Unused claims expired. No compensation."
                        : "No VIP subscription to restore.",
                };
            }

            VipSubscriptionActionResult claims = ProcessDueClaims(profile, now);
            SaveSystem.Save(profile);

            TryParsePlanId(profile.vipPlanId, out VipPlanKind plan);
            int maxClaims = PlanMaxClaims[(int)plan];
            return new VipSubscriptionActionResult
            {
                Status = VipSubscriptionActionStatus.Applied,
                Index = (int)plan,
                ClaimsApplied = claims.ClaimsApplied,
                ClaimsForfeited = claims.ClaimsForfeited,
                Message = $"Restored {PlanDisplayName(plan)} VIP. Claims {profile.vipClaimsConsumed}/{maxClaims}. " +
                          DescribeClaimResult(claims),
            };
        }

        /// <summary>
        /// Unlocks one claim per 7-day period (capped by plan). Each successful attempt consumes one
        /// Shop Stamina rolling-24h purchase slot. Full Stamina -> forfeit grant, still consumes.
        /// Cap full -> defer (do not consume the VIP claim).
        /// </summary>
        public static VipSubscriptionActionResult ProcessDueClaims(PlayerProfile profile, long nowUtcTicks)
        {
            var result = new VipSubscriptionActionResult
            {
                Status = VipSubscriptionActionStatus.Applied,
                Index = -1,
                Message = string.Empty,
            };

            if (profile == null || !IsSubscriptionActive(profile, nowUtcTicks))
                return result;
            if (!TryParsePlanId(profile.vipPlanId, out VipPlanKind plan))
                return result;

            int maxClaims = PlanMaxClaims[(int)plan];
            int unlocked = UnlockedClaimCount(profile, nowUtcTicks, maxClaims);

            while (profile.vipClaimsConsumed < unlocked)
            {
                ShopStaminaCatalog.RefreshRollingWindow(profile, nowUtcTicks);
                if (profile.staminaShopPurchasesInWindow >= ShopStaminaCatalog.MaxPurchasesPerRollingDay)
                {
                    result.Message = "VIP claim deferred — Shop Stamina 24h refill cap reached.";
                    break;
                }

                bool atFull = profile.stamina >= profile.maxStamina && profile.maxStamina > 0;
                if (atFull)
                {
                    result.ClaimsForfeited++;
                }
                else
                {
                    CurrencyManager.RestoreStamina(profile, ShopStaminaCatalog.StaminaGrantPerPotion, persist: false);
                    result.ClaimsApplied++;
                }

                ShopStaminaCatalog.RecordSuccessfulPurchase(profile, nowUtcTicks);
                profile.vipClaimsConsumed++;
            }

            return result;
        }

        public static int UnlockedClaimCount(PlayerProfile profile, long nowUtcTicks, int maxClaims)
        {
            if (profile == null || profile.vipStartedUtcTicks <= 0 || maxClaims <= 0)
                return 0;
            long elapsed = nowUtcTicks - profile.vipStartedUtcTicks;
            if (elapsed < 0) elapsed = 0;
            int periodIndex = (int)(elapsed / ClaimPeriodTicks);
            int unlocked = periodIndex + 1;
            if (unlocked > maxClaims) unlocked = maxClaims;
            if (unlocked < 0) unlocked = 0;
            return unlocked;
        }

        public static void ExpireIfLapsed(PlayerProfile profile, long nowUtcTicks)
        {
            if (profile == null) return;
            if (string.IsNullOrEmpty(profile.vipPlanId) && profile.vipExpiresUtcTicks <= 0)
                return;
            if (nowUtcTicks < profile.vipExpiresUtcTicks)
                return;

            profile.vipPlanId = string.Empty;
            profile.vipStartedUtcTicks = 0;
            profile.vipExpiresUtcTicks = 0;
            profile.vipClaimsConsumed = 0;
        }

        private static string DescribeClaimResult(VipSubscriptionActionResult claims)
        {
            if (claims.ClaimsApplied <= 0 && claims.ClaimsForfeited <= 0)
                return string.IsNullOrEmpty(claims.Message) ? "No new claims due." : claims.Message;
            return $"+{claims.ClaimsApplied} Stamina claim(s), {claims.ClaimsForfeited} forfeited at cap.";
        }

        private static string FormatExpiry(long expiresUtcTicks)
        {
            try
            {
                return new DateTime(expiresUtcTicks, DateTimeKind.Utc).ToString("u");
            }
            catch
            {
                return "expiry";
            }
        }
    }
}
