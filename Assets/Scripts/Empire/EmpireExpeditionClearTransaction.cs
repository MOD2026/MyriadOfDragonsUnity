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
    /// Materials: computed AND PERSISTED to PlayerProfile.constructionMaterials (wired 2026-08-26).
    /// This comment previously said Materials were never written because the save had no field for
    /// them — that stopped being true on 2026-08-24 when the field landed, and the header outlived
    /// the blocker by two days. Rewritten rather than left standing: a stale "escalate" note is
    /// worse than none, because it tells the next reader a solved problem is still open.
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

            // Materials now PERSIST (2026-08-26). This grant was computed and then thrown away
            // because the message below said the field was frozen - but PlayerProfile.
            // constructionMaterials has existed since 2026-08-24 and its own doc comment names THIS
            // class: "EmpireExpeditionClearTransaction computes a Materials grant per clear
            // already; this is the field it had nowhere to persist to". The field arrived and the
            // wiring was never finished, so the stale message outlived the blocker by two days.
            //
            // Written directly with a floor, matching DailyLoginQuestsService.cs:207 - the
            // established grant path for this currency. NOT CurrencyManager.AddCurrency: there is
            // no CurrencyType.Materials (CurrencyDefinitions.cs:5-12 is Gold/Gems/EventMedal/
            // GuildContribution/DragonRelic), so routing it through there would need a new currency
            // type for a balance that already has a home.
            if (baseMaterials > 0)
                profile.constructionMaterials =
                    System.Math.Max(0, profile.constructionMaterials) + baseMaterials;

            if (persist)
                SaveSystem.Save(profile);

            result.Status = EmpireExpeditionClearStatus.Applied;
            result.StaminaSpent = staminaCost;
            result.GoldGranted = totalGold;
            result.GuildBonusGold = bonusGold;
            result.GuildBonusApplied = applyBonus && bonusGold > 0;
            result.MaterialsGranted = baseMaterials;
            result.MaterialsPersisted = baseMaterials > 0;
            result.Message = baseMaterials > 0
                ? "Clear applied (Stamina+Gold+Materials)."
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
