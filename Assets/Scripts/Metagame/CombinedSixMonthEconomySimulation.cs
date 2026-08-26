using System;
using MyriadOfDragons.Economy;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.Season;

namespace MyriadOfDragons.Metagame
{
    /// <summary>
    /// Combined six-month economy ledger — in-engine only (MOS non-negotiable #4).
    /// Drives Solo Collection Circuit, Empire Expedition clears, Battle Pass free-track claims,
    /// Shop Stamina ladder purchases, VIP Monthly subscribe/claims, and Loyalty accrue/claim
    /// against the real production APIs. Does not invent Gold conversion rates for VIP/Stamina.
    /// </summary>
    public static class CombinedSixMonthEconomySimulation
    {
        /// <summary>183 UTC days ≈ six months; matches the locked BS six-month window.</summary>
        public const int SimulationDays = 183;

        public enum PersonaKind
        {
            /// <summary>Max Circuit + Expedition + free Battle Pass; buys Stamina only as needed
            /// for Expedition; no VIP.</summary>
            F2PActive = 0,

            /// <summary>F2P farms + Monthly VIP renewals when lapsed.</summary>
            RegularSpender = 1,

            /// <summary>Regular + full daily Stamina ladder when gems allow; enough Gem spend to
            /// reach the top Loyalty rung (claims still subject to held-voucher queue).</summary>
            Whale = 2,
        }

        public struct Ledger
        {
            public PersonaKind Persona;
            public int DaysSimulated;

            public int GemsSpent;
            public int LoyaltyPointsEarned;
            public int LoyaltyGoldClaimed;
            public int LoyaltyStaminaClaimsApplied;

            /// <summary>VIP vouchers actually granted by loyalty milestones over the window.
            /// Zero before 2026-08-26, when every voucher rung refused.</summary>
            public int LoyaltyVouchersGranted;

            /// <summary>Times the ascending claim queue stopped at the cosmetic rung (500), which
            /// PlayerProfile still cannot represent. Renamed from LoyaltyClaimsBlockedAtHeldVoucher
            /// - the voucher hold is resolved, and keeping the old name would have kept reporting a
            /// blocker that no longer exists.</summary>
            public int LoyaltyClaimsBlockedAtCosmeticRung;

            public int VipGemsSpent;
            public int VipStaminaClaimsApplied;
            public int VipStaminaClaimsForfeited;
            public int VipSubscribeCount;

            public int ShopStaminaGemsSpent;
            public int ShopStaminaPurchases;
            public int ShopStaminaGranted;

            public int SoloCircuitGold;
            public int ExpeditionGold;
            public int BattlePassFreeGoldClaimed;
            public int BattlePassPaidGoldClaimed;

            /// <summary>Locked table totals for complete seasons (not claimable while premium
            /// unlock has no profile field).</summary>
            public int BattlePassConfiguredPaidTrackGold;

            public int EmpireL30GoldSink;
            public int TotalRepeatableGoldEarned;

            public int CompleteSeasons;
        }

        public static int SumFreeTierGold()
        {
            int total = 0;
            for (int i = 0; i < BattlePassOpenValues.FreeTierGold.Length; i++)
                total += BattlePassOpenValues.FreeTierGold[i];
            return total;
        }

        public static int SumPaidTierGold()
        {
            int total = 0;
            for (int i = 0; i < BattlePassOpenValues.PaidTierGold.Length; i++)
                total += BattlePassOpenValues.PaidTierGold[i];
            return total;
        }

        /// <summary>Castle + Barracks + Gate Gold from L1 to each building's paid L30 path.</summary>
        public static int EmpireL30GoldSinkFromL1()
        {
            int castle = PlayerEmpireData.RemainingGoldToMaxCastle(1);
            int barracks = PlayerEmpireData.RemainingGoldToMaxBarracks(1);
            int gate = 0;
            int level = 1;
            while (true)
            {
                int next = PlayerEmpireData.NextPaidGateMilestone(level);
                if (next == 0)
                    break;
                gate += PlayerEmpireData.GoldCostForGateUpgrade(level, next);
                level = next;
            }

            return castle + barracks + gate;
        }

        public static Ledger Run(PersonaKind persona, DateTime startUtc)
        {
            if (startUtc.Kind != DateTimeKind.Utc)
                startUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);

            var ledger = new Ledger
            {
                Persona = persona,
                DaysSimulated = SimulationDays,
                EmpireL30GoldSink = EmpireL30GoldSinkFromL1(),
            };

            var profile = new PlayerProfile
            {
                gold = 0,
                gems = 0,
                stamina = 0,
                maxStamina = 200,
            };
            // Seed gems so spend personas can actually exercise VIP/Stamina/Loyalty paths.
            // F2P only needs enough for Expedition Stamina top-ups over the window.
            int gemSeed = persona switch
            {
                PersonaKind.F2PActive => 20_000,
                PersonaKind.RegularSpender => 80_000,
                PersonaKind.Whale => 250_000,
                _ => 20_000,
            };
            CurrencyManager.AddCurrency(profile, CurrencyType.Gems, gemSeed, persist: false);

            SaveSystem.CurrentProfile = profile;
            VipSubscriptionOpenValues.SetNowUtcTicksForTests(startUtc.Ticks);

            var circuit = new SoloCircuitProgress();
            int completeSeasons = SimulationDays / BattlePassOpenValues.SeasonLengthDays;
            ledger.CompleteSeasons = completeSeasons;
            ledger.BattlePassConfiguredPaidTrackGold = completeSeasons * SumPaidTierGold();

            try
            {
                for (int day = 0; day < SimulationDays; day++)
                {
                    DateTime dayUtc = startUtc.AddDays(day);
                    long nowTicks = dayUtc.Ticks;
                    VipSubscriptionOpenValues.SetNowUtcTicksForTests(nowTicks);

                    // Season boundary: claim free track for the season that just completed, then
                    // reset claim counters. Live code has no season-rollover writer yet; the harness
                    // resets so multi-season free-track payouts exercise TryClaimTier for real.
                    if (day > 0 && day % BattlePassOpenValues.SeasonLengthDays == 0)
                        ClaimFullFreeBattlePassSeason(profile, ref ledger);

                    RunSoloCircuitDay(circuit, profile, dayUtc, ref ledger);
                    EnsureExpeditionStamina(profile, nowTicks, ref ledger);
                    RunExpeditionDay(profile, ref ledger);
                    RunVipDay(profile, persona, nowTicks, ref ledger);
                    RunOptionalWhaleStaminaLadder(profile, persona, nowTicks, ref ledger);
                    TryClaimLoyalty(profile, nowTicks, ref ledger);
                }

                // Partial 7th season is held unclaimable (BS six-month rule). Top up only if a
                // season boundary was missed (e.g. SimulationDays exactly on a multiple).
                int expectedFree = completeSeasons * SumFreeTierGold();
                while (ledger.BattlePassFreeGoldClaimed < expectedFree)
                    ClaimFullFreeBattlePassSeason(profile, ref ledger);
            }
            finally
            {
                VipSubscriptionOpenValues.SetNowUtcTicksForTests(null);
            }

            ledger.TotalRepeatableGoldEarned =
                ledger.SoloCircuitGold
                + ledger.ExpeditionGold
                + ledger.BattlePassFreeGoldClaimed
                + ledger.LoyaltyGoldClaimed;

            return ledger;
        }

        private static void ClaimFullFreeBattlePassSeason(PlayerProfile profile, ref Ledger ledger)
        {
            profile.battlePassClaimedFreeTier = 0;
            for (int tier = 0; tier < BattlePassOpenValues.ShellTierWellCount; tier++)
            {
                BattlePassClaimResult claim =
                    BattlePassOpenValues.TryClaimTier(profile, tier, premiumTrack: false);
                if (claim.Status != BattlePassClaimStatus.Applied)
                    break;
                ledger.BattlePassFreeGoldClaimed += claim.GoldGranted;
            }

            // Paid track: engine refuses (no premium-unlocked profile field). Count attempts so
            // the ledger records real claimable paid Gold (expected 0 today).
            profile.battlePassClaimedPaidTier = 0;
            for (int tier = 0; tier < BattlePassOpenValues.ShellTierWellCount; tier++)
            {
                BattlePassClaimResult paid =
                    BattlePassOpenValues.TryClaimTier(profile, tier, premiumTrack: true);
                if (paid.Status == BattlePassClaimStatus.Applied)
                    ledger.BattlePassPaidGoldClaimed += paid.GoldGranted;
            }
        }

        private static void RunSoloCircuitDay(
            SoloCircuitProgress circuit, PlayerProfile profile, DateTime dayUtc, ref Ledger ledger)
        {
            SoloCircuitTrial[] trials =
            {
                SoloCircuitTrial.Formation,
                SoloCircuitTrial.Collection,
                SoloCircuitTrial.TacticalBrief,
            };
            for (int i = 0; i < trials.Length; i++)
            {
                SoloCircuitClearResult clear =
                    SoloCollectionCircuit.RecordClear(circuit, trials[i], dayUtc);
                if (!clear.Cleared || clear.GoldGranted <= 0)
                    continue;
                CurrencyManager.AddCurrency(profile, CurrencyType.Gold, clear.GoldGranted, persist: false);
                ledger.SoloCircuitGold += clear.GoldGranted;
            }
        }

        private static void EnsureExpeditionStamina(
            PlayerProfile profile, long nowTicks, ref Ledger ledger)
        {
            int cost = EmpireExpeditionOpenValues.StaminaCostPerClear ?? 0;
            int needForCap = cost * 3; // three clears hit the 900 Gold daily cap
            if (needForCap <= 0)
                return;

            while (profile.stamina < needForCap)
            {
                if (!TryBuyNextStaminaPotion(profile, nowTicks, ref ledger))
                    break;
            }
        }

        private static void RunOptionalWhaleStaminaLadder(
            PlayerProfile profile, PersonaKind persona, long nowTicks, ref Ledger ledger)
        {
            if (persona != PersonaKind.Whale)
                return;

            while (TryBuyNextStaminaPotion(profile, nowTicks, ref ledger))
            {
                // Drain until the rolling 24h cap refuses.
            }
        }

        private static bool TryBuyNextStaminaPotion(
            PlayerProfile profile, long nowTicks, ref Ledger ledger)
        {
            if (!ShopStaminaCatalog.TryGetNextGemCost(profile, nowTicks, out int gemCost, out _))
                return false;
            if (CurrencyManager.GetBalance(profile, CurrencyType.Gems) < gemCost)
                return false;
            if (!CurrencyManager.SpendCurrency(profile, CurrencyType.Gems, gemCost, persist: false))
                return false;

            CurrencyManager.RestoreStamina(profile, ShopStaminaCatalog.StaminaGrantPerPotion, persist: false);
            ShopStaminaCatalog.RecordSuccessfulPurchase(profile, nowTicks);

            ledger.GemsSpent += gemCost;
            ledger.ShopStaminaGemsSpent += gemCost;
            ledger.ShopStaminaPurchases++;
            ledger.ShopStaminaGranted += ShopStaminaCatalog.StaminaGrantPerPotion;

            int points = ShopLoyaltyService.PointsForGemsSpent(gemCost);
            if (points > 0)
            {
                ShopLoyaltyService.Accrue(profile, points);
                ledger.LoyaltyPointsEarned += points;
            }

            return true;
        }

        private static void RunExpeditionDay(PlayerProfile profile, ref Ledger ledger)
        {
            if (!EmpireExpeditionOpenValues.AreClearRewardsConfigured)
                return;

            int goldToday = 0;
            int attempts = 0;
            string[] stages = { "exp-1", "exp-2", "exp-3" };
            for (int i = 0; i < stages.Length; i++)
            {
                EmpireExpeditionClearResult clear = EmpireExpeditionClearTransaction.TryApplyClear(
                    profile,
                    stages[i],
                    UnavailableGuildExpeditionBonusQuery.Instance,
                    expeditionGoldEarnedTodayUtc: goldToday,
                    expeditionAttemptsTodayUtc: attempts,
                    persist: false);
                if (clear.Status != EmpireExpeditionClearStatus.Applied)
                    break;
                goldToday += clear.GoldGranted;
                attempts++;
                ledger.ExpeditionGold += clear.GoldGranted;
            }
        }

        private static void RunVipDay(
            PlayerProfile profile, PersonaKind persona, long nowTicks, ref Ledger ledger)
        {
            if (persona == PersonaKind.F2PActive)
                return;

            VipSubscriptionOpenValues.ExpireIfLapsed(profile, nowTicks);
            VipSubscriptionActionResult due =
                VipSubscriptionOpenValues.ProcessDueClaims(profile, nowTicks);
            ledger.VipStaminaClaimsApplied += due.ClaimsApplied;
            ledger.VipStaminaClaimsForfeited += due.ClaimsForfeited;

            if (VipSubscriptionOpenValues.IsSubscriptionActive(profile, nowTicks))
                return;

            int price = VipSubscriptionOpenValues.MonthlyGemPrice;
            if (CurrencyManager.GetBalance(profile, CurrencyType.Gems) < price)
                return;

            // TrySubscribe spends gems and saves via SaveSystem — profile must be CurrentProfile.
            VipSubscriptionActionResult sub =
                VipSubscriptionOpenValues.TrySubscribe(VipPlanKind.Monthly);
            if (sub.Status != VipSubscriptionActionStatus.Applied)
                return;

            ledger.VipSubscribeCount++;
            ledger.VipGemsSpent += price;
            ledger.GemsSpent += price;
            ledger.VipStaminaClaimsApplied += sub.ClaimsApplied;
            ledger.VipStaminaClaimsForfeited += sub.ClaimsForfeited;

            int points = ShopLoyaltyService.PointsForGemsSpent(price);
            if (points > 0)
            {
                ShopLoyaltyService.Accrue(profile, points);
                ledger.LoyaltyPointsEarned += points;
            }
        }

        private static void TryClaimLoyalty(PlayerProfile profile, long nowTicks, ref Ledger ledger)
        {
            // Keep pulling until the next rung refuses or nothing is claimable.
            for (int guard = 0; guard < ShopLoyaltyService.Milestones.Length + 1; guard++)
            {
                int next = ShopLoyaltyService.NextClaimableMilestone(profile);
                if (next < 0)
                    return;

                ShopLoyaltyClaimResult claim = ShopLoyaltyService.ClaimNext(profile, nowTicks);
                if (!claim.Claimed)
                {
                    // The voucher hold is GONE (durations locked 2026-08-26), so the only rung that
                    // can still block the ascending queue is the cosmetic one - PlayerProfile has
                    // no cosmetic ownership model. Counting a voucher refusal here would now
                    // over-report blockage and silently understate Loyalty Gold, which is the exact
                    // number this simulation exists to produce.
                    if (ShopLoyaltyService.CosmeticGrantsUnsupported(next))
                        ledger.LoyaltyClaimsBlockedAtCosmeticRung++;
                    return;
                }

                ledger.LoyaltyGoldClaimed += claim.GoldGranted;
                ledger.LoyaltyStaminaClaimsApplied += claim.StaminaClaimsApplied;
                if (!string.IsNullOrEmpty(claim.VoucherPlanGranted)) ledger.LoyaltyVouchersGranted++;
            }
        }
    }
}
