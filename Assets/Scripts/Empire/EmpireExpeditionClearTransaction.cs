using MyriadOfDragons.Economy;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Empire
{
    public enum EmpireExpeditionClearStatus
    {
        Applied,
        UnknownStage,
        OpenValuesNotLocked,
        InsufficientStamina,
        DailyGoldCapWouldReject,
        DailyAttemptCapWouldReject,
    }

    /// <summary>Result of one authoritative Expedition clear transaction (metagame wrapper only).</summary>
    public sealed class EmpireExpeditionClearResult
    {
        public EmpireExpeditionClearStatus Status;
        public string StageId;
        public int StaminaSpent;
        public int GoldGranted;
        public int GuildBonusGold;
        public bool GuildBonusApplied;
        public int MaterialsGranted;
        public bool MaterialsPersisted;
        public string Message;
    }

    /// <summary>
    /// One authoritative clear transaction: Stamina spend + Gold (+ optional guild bonus) +
    /// Materials intent. Combat outcome is assumed already decided by Battle seat — this only
    /// settles the metagame wallet. Live numbers come from <see cref="EmpireExpeditionOpenValues"/>;
    /// when those are null the production entry refuses cleanly (does not invent costs/rewards).
    ///
    /// Materials: computed when configured, but never written — PlayerProfile has no Materials
    /// balance yet (frozen save shape; escalate).
    /// </summary>
    public static class EmpireExpeditionClearTransaction
    {
        public static EmpireExpeditionClearResult TryApplyClear(
            PlayerProfile profile,
            string stageId,
            IGuildExpeditionBonusQuery guildBonusQuery,
            int expeditionGoldEarnedTodayUtc = 0,
            int expeditionAttemptsTodayUtc = 0,
            bool persist = true)
        {
            if (!EmpireExpeditionOpenValues.AreClearRewardsConfigured)
            {
                return new EmpireExpeditionClearResult
                {
                    StageId = stageId,
                    Status = EmpireExpeditionClearStatus.OpenValuesNotLocked,
                    Message = EmpireExpeditionOpenValues.StatusNote,
                };
            }

            return ApplyConfiguredClear(
                profile,
                stageId,
                guildBonusQuery,
                EmpireExpeditionOpenValues.StaminaCostPerClear.Value,
                EmpireExpeditionOpenValues.BaseGoldPerClear.Value,
                EmpireExpeditionOpenValues.BaseMaterialsPerClear.Value,
                EmpireExpeditionOpenValues.DailyExpeditionGoldCap.Value,
                EmpireExpeditionOpenValues.DailyAttemptCap,
                expeditionGoldEarnedTodayUtc,
                expeditionAttemptsTodayUtc,
                persist);
        }

        /// <summary>EditMode / harness entry that supplies explicit numbers without writing them into
        /// <see cref="EmpireExpeditionOpenValues"/> (those stay null until the owner locks them).</summary>
        public static EmpireExpeditionClearResult TryApplyClearWithConfiguredAmountsForTests(
            PlayerProfile profile,
            string stageId,
            IGuildExpeditionBonusQuery guildBonusQuery,
            int staminaCost,
            int baseGold,
            int baseMaterials,
            int dailyGoldCap,
            int? dailyAttemptCap = null,
            int expeditionGoldEarnedTodayUtc = 0,
            int expeditionAttemptsTodayUtc = 0,
            bool persist = true)
        {
            return ApplyConfiguredClear(
                profile, stageId, guildBonusQuery,
                staminaCost, baseGold, baseMaterials, dailyGoldCap, dailyAttemptCap,
                expeditionGoldEarnedTodayUtc, expeditionAttemptsTodayUtc, persist);
        }

        private static EmpireExpeditionClearResult ApplyConfiguredClear(
            PlayerProfile profile,
            string stageId,
            IGuildExpeditionBonusQuery guildBonusQuery,
            int staminaCost,
            int baseGold,
            int baseMaterials,
            int dailyGoldCap,
            int? dailyAttemptCap,
            int expeditionGoldEarnedTodayUtc,
            int expeditionAttemptsTodayUtc,
            bool persist)
        {
            var result = new EmpireExpeditionClearResult { StageId = stageId };

            if (EmpireExpeditionCatalog.Find(stageId) == null)
            {
                result.Status = EmpireExpeditionClearStatus.UnknownStage;
                result.Message = $"Unknown Expedition stage '{stageId}'.";
                return result;
            }

            if (dailyAttemptCap.HasValue && expeditionAttemptsTodayUtc >= dailyAttemptCap.Value)
            {
                result.Status = EmpireExpeditionClearStatus.DailyAttemptCapWouldReject;
                result.Message = "Daily Expedition attempt cap reached.";
                return result;
            }

            if (CurrencyManager.GetStamina(profile) < staminaCost)
            {
                result.Status = EmpireExpeditionClearStatus.InsufficientStamina;
                result.Message = "Not enough Stamina for this Expedition clear.";
                return result;
            }

            int remainingGoldCap = dailyGoldCap - expeditionGoldEarnedTodayUtc;
            if (remainingGoldCap <= 0)
            {
                result.Status = EmpireExpeditionClearStatus.DailyGoldCapWouldReject;
                result.Message = "Daily Expedition Gold cap already reached.";
                return result;
            }

            int goldBeforeBonus = System.Math.Min(baseGold, remainingGoldCap);
            bool? eligible = guildBonusQuery?.TryQueryExpeditionGoldBonusEligible();
            bool applyBonus = eligible == true;
            int bonusGold = 0;
            if (applyBonus)
            {
                bonusGold = (int)System.Math.Floor(goldBeforeBonus * EmpireExpeditionOpenValues.GuildGoldBonusFraction);
                int roomAfterBase = remainingGoldCap - goldBeforeBonus;
                if (bonusGold > roomAfterBase)
                    bonusGold = System.Math.Max(0, roomAfterBase);
            }

            int totalGold = goldBeforeBonus + bonusGold;

            if (!CurrencyManager.SpendStamina(profile, staminaCost, persist: false))
            {
                result.Status = EmpireExpeditionClearStatus.InsufficientStamina;
                result.Message = "Not enough Stamina for this Expedition clear.";
                return result;
            }

            if (totalGold > 0)
                CurrencyManager.AddCurrency(profile, CurrencyType.Gold, totalGold, persist: false);

            if (persist)
                SaveSystem.Save(profile);

            result.Status = EmpireExpeditionClearStatus.Applied;
            result.StaminaSpent = staminaCost;
            result.GoldGranted = totalGold;
            result.GuildBonusGold = bonusGold;
            result.GuildBonusApplied = applyBonus && bonusGold > 0;
            result.MaterialsGranted = baseMaterials;
            result.MaterialsPersisted = false;
            result.Message = baseMaterials > 0
                ? "Clear applied (Stamina+Gold). Materials grant pending PlayerProfile Materials field (frozen)."
                : "Clear applied (Stamina+Gold).";
            return result;
        }

        /// <summary>Preview Gold line for UI (no mutation). Fail-closed when query is null/unavailable.</summary>
        public static string FormatGuildBonusDisplayLine(IGuildExpeditionBonusQuery guildBonusQuery)
        {
            bool? eligible = guildBonusQuery?.TryQueryExpeditionGoldBonusEligible();
            if (eligible == null)
                return "Guild bonus: unavailable — base Gold only (fail closed).";
            if (eligible == true)
                return $"Guild bonus: +{EmpireExpeditionOpenValues.GuildGoldBonusFraction * 100f:0}% Gold (eligible).";
            return "Guild bonus: not eligible — base Gold only.";
        }
    }
}
