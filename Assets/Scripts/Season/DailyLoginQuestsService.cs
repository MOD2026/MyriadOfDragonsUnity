using System;
using MyriadOfDragons.Data;
using MyriadOfDragons.Economy;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Season
{
    /// <summary>
    /// Daily Login + Daily Quests — LOCKED 2026-08-24 structure
    /// (docs/LOCKED_DECISIONS_REGISTER.md). Streak pauses (never resets). UTC day keys.
    /// Empire/Avatar-side rewards only; no cards/packs/Evolution/Forge-Dust/Permits/Market Credits.
    /// </summary>
    public static class DailyLoginQuestsService
    {
        public const int QuestSlotCount = 3;
        public const int LoginWellCount = 6;

        // Small bounded Phase-1 floors (structure locked; magnitudes are implementation defaults).
        public const int LoginGoldBase = 40;
        public const int LoginGoldPerTier = 15;
        public const int LoginMaterialsBase = 3;
        public const int LoginStamina = 5;
        public const int LoginEventMedals = 1;
        public const int LoginPassSeasonXp = 15;

        public const int QuestGold = 25;
        public const int QuestMaterials = 2;
        public const int QuestStamina = 3;
        public const int QuestPassSeasonXp = 10;

        public static string UtcDayKey(DateTime utcNow) => utcNow.ToUniversalTime().Date.ToString("yyyy-MM-dd");

        public static DateTime ParseUtcDayKey(string dayKey)
        {
            if (string.IsNullOrEmpty(dayKey)) return DateTime.MinValue.Date;
            if (DateTime.TryParse(dayKey, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime parsed))
                return parsed.Date;
            return DateTime.MinValue.Date;
        }

        public static void EnsureQuestDay(PlayerProfile profile, DateTime utcNow)
        {
            if (profile == null) return;
            string today = UtcDayKey(utcNow);
            if (profile.dailyQuestUtcDate == today) return;

            profile.dailyQuestUtcDate = today;
            profile.dailyQuestCompletionMask = 0;
            // Snapshot match/win counters so "play/win" quests measure progress within this UTC day.
            profile.dailyQuestGenerationId = PackProgressSnapshot(profile.totalMatches, profile.totalWins);
        }

        public static bool IsLoginClaimedToday(PlayerProfile profile, DateTime utcNow)
        {
            if (profile == null) return false;
            string today = UtcDayKey(utcNow);
            return !string.IsNullOrEmpty(profile.lastLoginClaimUtcDate)
                   && string.CompareOrdinal(profile.lastLoginClaimUtcDate, today) >= 0;
        }

        /// <summary>True when the last claim was before yesterday UTC — streak is paused (index kept).</summary>
        public static bool IsStreakPaused(PlayerProfile profile, DateTime utcNow)
        {
            if (profile == null || string.IsNullOrEmpty(profile.lastLoginClaimUtcDate)) return false;
            DateTime last = ParseUtcDayKey(profile.lastLoginClaimUtcDate);
            DateTime today = utcNow.ToUniversalTime().Date;
            if (last >= today) return false; // claimed today (or clock skew into future stamp)
            return last < today.AddDays(-1);
        }

        public static string StatusCopy(PlayerProfile profile, DateTime utcNow)
        {
            if (IsLoginClaimedToday(profile, utcNow))
                return "CLAIMED TODAY";
            if (IsStreakPaused(profile, utcNow))
                return DailyLoginQuestsOpenValues.StreakPausedCopy;
            if (string.IsNullOrEmpty(profile?.lastLoginClaimUtcDate))
                return "READY — STREAK STARTS ON CLAIM";
            return "READY — STREAK ACTIVE";
        }

        public static int CurrentLoginTier(PlayerProfile profile)
        {
            if (profile == null) return 0;
            int index = Math.Max(0, profile.loginStreakIndex);
            return index % LoginWellCount;
        }

        public static DailyLoginQuestClaimResult ClaimLogin(PlayerProfile profile, DateTime utcNow, bool persist = true)
        {
            if (profile == null)
            {
                return Refuse(-1, DailyLoginQuestClaimStatus.InvalidProfile, "No profile.");
            }

            string today = UtcDayKey(utcNow);
            DateTime todayDate = utcNow.ToUniversalTime().Date;

            // No clock-manipulation double-claims: refuse if today is at or before the last claim day.
            if (!string.IsNullOrEmpty(profile.lastLoginClaimUtcDate)
                && string.CompareOrdinal(today, profile.lastLoginClaimUtcDate) <= 0)
            {
                return Refuse(CurrentLoginTier(profile), DailyLoginQuestClaimStatus.AlreadyClaimed,
                    "Already claimed for this UTC day (or clock is behind last claim).");
            }

            int tier = CurrentLoginTier(profile);
            GrantLoginRewards(profile, tier);
            profile.lastLoginClaimUtcDate = today;
            // Pause = do not reset index on a gap; always advance after a successful claim.
            profile.loginStreakIndex = Math.Max(0, profile.loginStreakIndex) + 1;

            EnsureQuestDay(profile, utcNow);
            if (persist) SaveManager.Save();

            return new DailyLoginQuestClaimResult
            {
                Status = DailyLoginQuestClaimStatus.Applied,
                SlotIndex = tier,
                Message = $"Login claimed (Day {tier + 1}). Streak index {profile.loginStreakIndex}.",
                GoldGranted = LoginGoldBase + tier * LoginGoldPerTier,
                MaterialsGranted = LoginMaterialsBase + tier,
                StaminaGranted = LoginStamina,
                EventMedalsGranted = LoginEventMedals,
                PassSeasonXpGranted = LoginPassSeasonXp,
            };
        }

        public static DailyLoginQuestDefinition GetQuest(PlayerProfile profile, int questIndex, DateTime utcNow)
        {
            EnsureQuestDay(profile, utcNow);
            int slot = ((questIndex % QuestSlotCount) + QuestSlotCount) % QuestSlotCount;
            int gen = profile?.dailyQuestGenerationId ?? 0;
            // Slot 0 is always "claim login"; slots 1–2 rotate through the play/win pool by UTC day.
            DailyLoginQuestDefinition def;
            if (slot == 0)
            {
                def = QuestPool[0];
            }
            else
            {
                int dayHash = StableDayHash(profile?.dailyQuestUtcDate ?? UtcDayKey(utcNow));
                int poolIndex = 1 + ((dayHash + slot) % (QuestPool.Length - 1));
                def = QuestPool[poolIndex];
            }
            UnpackProgressSnapshot(gen, out int matchesAtRoll, out int winsAtRoll);
            int progress = def.Kind switch
            {
                DailyLoginQuestKind.ClaimLogin => IsLoginClaimedToday(profile, utcNow) ? 1 : 0,
                DailyLoginQuestKind.PlayMatches => Math.Max(0, (profile?.totalMatches ?? 0) - matchesAtRoll),
                DailyLoginQuestKind.WinMatches => Math.Max(0, (profile?.totalWins ?? 0) - winsAtRoll),
                _ => 0,
            };
            bool claimed = profile != null && (profile.dailyQuestCompletionMask & (1 << slot)) != 0;
            return new DailyLoginQuestDefinition
            {
                SlotIndex = slot,
                Kind = def.Kind,
                Title = def.Title,
                Target = def.Target,
                Progress = Math.Min(progress, def.Target),
                IsComplete = progress >= def.Target,
                IsClaimed = claimed,
            };
        }

        public static DailyLoginQuestClaimResult ClaimQuest(PlayerProfile profile, int questIndex, DateTime utcNow, bool persist = true)
        {
            if (profile == null)
                return Refuse(questIndex, DailyLoginQuestClaimStatus.InvalidProfile, "No profile.");

            EnsureQuestDay(profile, utcNow);
            int slot = ((questIndex % QuestSlotCount) + QuestSlotCount) % QuestSlotCount;
            if ((profile.dailyQuestCompletionMask & (1 << slot)) != 0)
            {
                return Refuse(slot, DailyLoginQuestClaimStatus.AlreadyClaimed, "Quest already claimed today.");
            }

            DailyLoginQuestDefinition quest = GetQuest(profile, slot, utcNow);
            if (!quest.IsComplete)
            {
                return Refuse(slot, DailyLoginQuestClaimStatus.NotComplete,
                    $"Quest incomplete ({quest.Progress}/{quest.Target}).");
            }

            GrantQuestRewards(profile);
            profile.dailyQuestCompletionMask |= (1 << slot);
            if (persist) SaveManager.Save();

            return new DailyLoginQuestClaimResult
            {
                Status = DailyLoginQuestClaimStatus.Applied,
                SlotIndex = slot,
                Message = $"Quest claimed: {quest.Title}.",
                GoldGranted = QuestGold,
                MaterialsGranted = QuestMaterials,
                StaminaGranted = QuestStamina,
                PassSeasonXpGranted = QuestPassSeasonXp,
            };
        }

        private static void GrantLoginRewards(PlayerProfile profile, int tier)
        {
            int gold = LoginGoldBase + tier * LoginGoldPerTier;
            int materials = LoginMaterialsBase + tier;
            CurrencyManager.AddCurrency(profile, CurrencyType.Gold, gold, persist: false);
            profile.constructionMaterials = Math.Max(0, profile.constructionMaterials) + materials;
            CurrencyManager.RestoreStamina(profile, LoginStamina, persist: false);
            CurrencyManager.AddCurrency(profile, CurrencyType.EventMedal, LoginEventMedals, persist: false);
            profile.passSeasonXp = Math.Max(0, profile.passSeasonXp) + LoginPassSeasonXp;
        }

        private static void GrantQuestRewards(PlayerProfile profile)
        {
            CurrencyManager.AddCurrency(profile, CurrencyType.Gold, QuestGold, persist: false);
            profile.constructionMaterials = Math.Max(0, profile.constructionMaterials) + QuestMaterials;
            CurrencyManager.RestoreStamina(profile, QuestStamina, persist: false);
            profile.passSeasonXp = Math.Max(0, profile.passSeasonXp) + QuestPassSeasonXp;
        }

        private static DailyLoginQuestClaimResult Refuse(int slot, DailyLoginQuestClaimStatus status, string message) =>
            new DailyLoginQuestClaimResult { Status = status, SlotIndex = slot, Message = message };

        private static int PackProgressSnapshot(int matches, int wins) =>
            (Math.Max(0, matches) * 10000) + Math.Min(9999, Math.Max(0, wins));

        private static void UnpackProgressSnapshot(int packed, out int matches, out int wins)
        {
            matches = packed / 10000;
            wins = packed % 10000;
        }

        private static int StableDayHash(string dayKey)
        {
            if (string.IsNullOrEmpty(dayKey)) return 0;
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < dayKey.Length; i++)
                    hash = hash * 31 + dayKey[i];
                return Math.Abs(hash);
            }
        }

        private static readonly DailyLoginQuestDefinition[] QuestPool =
        {
            new DailyLoginQuestDefinition { Kind = DailyLoginQuestKind.ClaimLogin, Title = "Claim today's login", Target = 1 },
            new DailyLoginQuestDefinition { Kind = DailyLoginQuestKind.PlayMatches, Title = "Play 1 battle", Target = 1 },
            new DailyLoginQuestDefinition { Kind = DailyLoginQuestKind.WinMatches, Title = "Win 1 battle", Target = 1 },
            new DailyLoginQuestDefinition { Kind = DailyLoginQuestKind.PlayMatches, Title = "Play 2 battles", Target = 2 },
            new DailyLoginQuestDefinition { Kind = DailyLoginQuestKind.WinMatches, Title = "Win 2 battles", Target = 2 },
        };
    }

    public enum DailyLoginQuestKind
    {
        ClaimLogin,
        PlayMatches,
        WinMatches,
    }

    public sealed class DailyLoginQuestDefinition
    {
        public int SlotIndex;
        public DailyLoginQuestKind Kind;
        public string Title;
        public int Target;
        public int Progress;
        public bool IsComplete;
        public bool IsClaimed;
    }
}
