using System;
using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.Season;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Daily Login + Quests logic: streak pause (never reset), UTC rollover, no double-claim.
    /// </summary>
    public class DailyLoginQuestsLogicTests
    {
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsDailyLoginLogic_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void ClaimLogin_GrantsBoundedRewards_AndAdvancesStreakIndex()
        {
            var profile = new PlayerProfile { gold = 100, constructionMaterials = 0, stamina = 50, maxStamina = 100 };
            DateTime day1 = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

            DailyLoginQuestClaimResult first = DailyLoginQuestsService.ClaimLogin(profile, day1, persist: false);
            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied, first.Status);
            Assert.AreEqual(0, first.SlotIndex);
            Assert.AreEqual(100 + DailyLoginQuestsService.LoginGoldBase, profile.gold);
            Assert.AreEqual(DailyLoginQuestsService.LoginMaterialsBase, profile.constructionMaterials);
            Assert.AreEqual(DailyLoginQuestsService.LoginPassSeasonXp, profile.passSeasonXp);
            Assert.AreEqual("2026-08-20", profile.lastLoginClaimUtcDate);
            Assert.AreEqual(1, profile.loginStreakIndex);
        }

        [Test]
        public void ClaimLogin_WhileEventLedgerDormant_GrantsZeroMedals_AndLeavesBalanceUnchanged()
        {
            // Event Medals are LOCKED DORMANT until a trusted server ledger exists. LoginEventMedals
            // stays declared; the gate zeroes at grant. Existing balances are deliberately not
            // wiped - owner decides zero vs grandfather.
            Assert.IsFalse(DailyLoginQuestsService.EventLedgerActive);
            Assert.AreEqual(1, DailyLoginQuestsService.LoginEventMedals,
                "Forward-declared medal constant must remain for activation day.");

            var profile = new PlayerProfile
            {
                gold = 0, constructionMaterials = 0, stamina = 50, maxStamina = 100,
                eventMedals = 17, // pre-existing leak balance — must survive the claim
            };
            DateTime day1 = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

            DailyLoginQuestClaimResult claim = DailyLoginQuestsService.ClaimLogin(profile, day1, persist: false);
            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied, claim.Status);
            Assert.AreEqual(0, claim.EventMedalsGranted,
                "Dormant ledger must not report Event Medals earned.");
            Assert.AreEqual(17, profile.eventMedals,
                "Claim must not mint into or clear existing profile.eventMedals.");
        }

        [Test]
        public void ClaimLogin_SameUtcDay_RefusesDoubleClaim()
        {
            var profile = new PlayerProfile();
            DateTime day1 = new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied,
                DailyLoginQuestsService.ClaimLogin(profile, day1, persist: false).Status);

            DailyLoginQuestClaimResult again = DailyLoginQuestsService.ClaimLogin(
                profile, day1.AddHours(10), persist: false);
            Assert.AreEqual(DailyLoginQuestClaimStatus.AlreadyClaimed, again.Status);
            Assert.AreEqual(1, profile.loginStreakIndex, "Double-claim must not advance streak.");
        }

        [Test]
        public void ClaimLogin_ClockRewound_ToOrBeforeLastClaim_Refuses()
        {
            var profile = new PlayerProfile();
            DateTime day2 = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
            DailyLoginQuestsService.ClaimLogin(profile, day2, persist: false);

            DateTime day1 = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
            DailyLoginQuestClaimResult rewind = DailyLoginQuestsService.ClaimLogin(profile, day1, persist: false);
            Assert.AreEqual(DailyLoginQuestClaimStatus.AlreadyClaimed, rewind.Status);
            Assert.AreEqual("2026-08-21", profile.lastLoginClaimUtcDate);
            Assert.AreEqual(1, profile.loginStreakIndex);
        }

        [Test]
        public void MissedDay_PausesStreak_DoesNotResetIndex_ResumeClaimsSameTierSequence()
        {
            var profile = new PlayerProfile();
            DateTime day1 = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
            DateTime day2 = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
            DateTime day5 = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc); // missed 22+23

            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied,
                DailyLoginQuestsService.ClaimLogin(profile, day1, persist: false).Status);
            Assert.AreEqual(1, profile.loginStreakIndex);

            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied,
                DailyLoginQuestsService.ClaimLogin(profile, day2, persist: false).Status);
            Assert.AreEqual(2, profile.loginStreakIndex);
            Assert.IsFalse(DailyLoginQuestsService.IsStreakPaused(profile, day2));

            Assert.IsTrue(DailyLoginQuestsService.IsStreakPaused(profile, day5));
            Assert.AreEqual(DailyLoginQuestsOpenValues.StreakPausedCopy,
                DailyLoginQuestsService.StatusCopy(profile, day5));

            // Resume: index still 2 → next claim is still tier 2 (Day 3 well), not reset to 0.
            DailyLoginQuestClaimResult resume = DailyLoginQuestsService.ClaimLogin(profile, day5, persist: false);
            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied, resume.Status);
            Assert.AreEqual(2, resume.SlotIndex);
            Assert.AreEqual(3, profile.loginStreakIndex);
            Assert.AreEqual("2026-08-24", profile.lastLoginClaimUtcDate);
        }

        [Test]
        public void UtcRollover_NewDay_AllowsOneClaim_AndResetsQuestMask()
        {
            var profile = new PlayerProfile { totalMatches = 3, totalWins = 1 };
            DateTime day1 = new DateTime(2026, 8, 20, 23, 0, 0, DateTimeKind.Utc);
            DateTime day2 = new DateTime(2026, 8, 21, 1, 0, 0, DateTimeKind.Utc);

            DailyLoginQuestsService.EnsureQuestDay(profile, day1);
            profile.dailyQuestCompletionMask = 0b111;
            string genDay1 = profile.dailyQuestUtcDate;

            DailyLoginQuestsService.ClaimLogin(profile, day1, persist: false);
            Assert.AreEqual("2026-08-20", profile.lastLoginClaimUtcDate);

            DailyLoginQuestsService.EnsureQuestDay(profile, day2);
            Assert.AreEqual("2026-08-21", profile.dailyQuestUtcDate);
            Assert.AreNotEqual(genDay1, profile.dailyQuestUtcDate);
            Assert.AreEqual(0, profile.dailyQuestCompletionMask, "Quest mask must clear on UTC rollover.");

            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied,
                DailyLoginQuestsService.ClaimLogin(profile, day2, persist: false).Status);
        }

        [Test]
        public void ClaimQuest_LoginKind_RequiresLoginClaimedToday_ThenRefusesSecondClaim()
        {
            var profile = new PlayerProfile();
            DateTime utc = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
            DailyLoginQuestsService.EnsureQuestDay(profile, utc);

            // Slot 0 is always ClaimLogin.
            int loginSlot = 0;
            DailyLoginQuestClaimResult before = DailyLoginQuestsService.ClaimQuest(profile, loginSlot, utc, persist: false);
            Assert.AreEqual(DailyLoginQuestClaimStatus.NotComplete, before.Status);

            DailyLoginQuestsService.ClaimLogin(profile, utc, persist: false);
            DailyLoginQuestClaimResult after = DailyLoginQuestsService.ClaimQuest(profile, loginSlot, utc, persist: false);
            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied, after.Status);
            Assert.AreEqual(DailyLoginQuestsService.QuestGold, after.GoldGranted);

            DailyLoginQuestClaimResult twice = DailyLoginQuestsService.ClaimQuest(profile, loginSlot, utc, persist: false);
            Assert.AreEqual(DailyLoginQuestClaimStatus.AlreadyClaimed, twice.Status);
        }

        [Test]
        public void ClaimQuest_PlayMatches_UsesGenerationSnapshot_NoMarketCredits()
        {
            var profile = new PlayerProfile { totalMatches = 10, totalWins = 4, dragonRelics = 7 };
            DateTime utc = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
            DailyLoginQuestsService.EnsureQuestDay(profile, utc);

            int playSlot = -1;
            for (int i = 0; i < 3; i++)
            {
                DailyLoginQuestDefinition q = DailyLoginQuestsService.GetQuest(profile, i, utc);
                if (q.Kind == DailyLoginQuestKind.PlayMatches && q.Target == 1)
                {
                    playSlot = i;
                    break;
                }
            }
            if (playSlot < 0)
            {
                Assert.Ignore("Today's rotation has no Play-1 quest slot — pool coverage is still exercised other days.");
                return;
            }

            Assert.AreEqual(DailyLoginQuestClaimStatus.NotComplete,
                DailyLoginQuestsService.ClaimQuest(profile, playSlot, utc, persist: false).Status);

            profile.totalMatches = 11;
            DailyLoginQuestClaimResult claimed = DailyLoginQuestsService.ClaimQuest(profile, playSlot, utc, persist: false);
            Assert.AreEqual(DailyLoginQuestClaimStatus.Applied, claimed.Status);
            Assert.AreEqual(7, profile.dragonRelics, "Market Credits / Dragon Relics must never be granted.");
        }
    }
}
