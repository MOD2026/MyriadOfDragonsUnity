using System;
using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Empire Expedition's daily-scope reset (2026-08-24, owner-authorized additive save fields) -
    /// mirrors CollectionAscensionPermits.TryGrantWeekly's own reset-on-key-mismatch shape,
    /// generalized from ISO week to UTC calendar day.
    /// </summary>
    public class EmpireExpeditionDailyResetTests
    {
        [Test]
        public void EnsureCurrentDay_NullProfile_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EmpireExpeditionDailyReset.EnsureCurrentDay(null));
        }

        [Test]
        public void EnsureCurrentDay_FreshProfile_StampsTodaysRealUtcDayKey()
        {
            var profile = new PlayerProfile();
            Assert.AreEqual(string.Empty, profile.expeditionDayKeyUtc, "Setup: a fresh profile starts with no day key.");

            EmpireExpeditionDailyReset.EnsureCurrentDay(profile);

            Assert.AreEqual(DateTime.UtcNow.ToString("yyyy-MM-dd"), profile.expeditionDayKeyUtc);
        }

        [Test]
        public void EnsureCurrentDay_SameDayKeyAlreadyStored_LeavesBothCountersUntouched()
        {
            var profile = new PlayerProfile
            {
                expeditionDayKeyUtc = EmpireExpeditionDailyReset.CurrentUtcDayKey(),
                expeditionGoldEarnedTodayUtc = 900,
                expeditionAttemptsTodayUtc = 3,
            };

            EmpireExpeditionDailyReset.EnsureCurrentDay(profile);

            Assert.AreEqual(900, profile.expeditionGoldEarnedTodayUtc, "Same real UTC day must not reset an in-progress daily total.");
            Assert.AreEqual(3, profile.expeditionAttemptsTodayUtc);
        }

        [Test]
        public void EnsureCurrentDay_StaleDayKeyFromYesterday_ResetsBothCountersAndStampsToday()
        {
            var profile = new PlayerProfile
            {
                expeditionDayKeyUtc = "2020-01-01",
                expeditionGoldEarnedTodayUtc = 900,
                expeditionAttemptsTodayUtc = 3,
            };

            EmpireExpeditionDailyReset.EnsureCurrentDay(profile);

            Assert.AreEqual(EmpireExpeditionDailyReset.CurrentUtcDayKey(), profile.expeditionDayKeyUtc);
            Assert.AreEqual(0, profile.expeditionGoldEarnedTodayUtc, "A day boundary crossed must reset the daily Gold-earned counter.");
            Assert.AreEqual(0, profile.expeditionAttemptsTodayUtc, "A day boundary crossed must reset the daily attempt counter.");
        }

        [Test]
        public void CurrentUtcDayKey_IsStableAcrossRepeatedCallsWithinTheSameProcessTick()
        {
            // Not a flaky-clock test - two calls microseconds apart within the same real UTC
            // calendar day must always agree, which is the entire premise the reset check relies on.
            Assert.AreEqual(EmpireExpeditionDailyReset.CurrentUtcDayKey(), EmpireExpeditionDailyReset.CurrentUtcDayKey());
        }
    }
}
