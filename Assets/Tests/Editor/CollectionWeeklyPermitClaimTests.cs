using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>Option C weekly Permit stopgap — ManualTrustedWeekKey claim helper (no battle harness).</summary>
    public class CollectionWeeklyPermitClaimTests
    {
        [Test]
        public void ManualTrustedWeekKey_IsDistinctFromDevPlaceholder()
        {
            Assert.IsFalse(string.IsNullOrEmpty(CollectionAscensionPermits.ManualTrustedWeekKey));
            Assert.AreNotEqual(
                CollectionAscensionPermits.DevTrustedWeekKeyPlaceholder,
                CollectionAscensionPermits.ManualTrustedWeekKey);
        }

        [Test]
        public void FirstClaim_EmptyWeek_GrantsUpToFourOrHoardRemaining()
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            Assert.AreEqual(string.Empty, profile.ascensionPermitWeekKey);

            int granted = HomePagePresenter.TryClaimManualWeeklyPermits(profile, out string status);

            Assert.AreEqual(CollectionSchemaRules.AscensionPermitsPerTrustedWeek, granted);
            Assert.AreEqual(4, profile.ascensionPermitBalance);
            Assert.AreEqual(4, profile.ascensionPermitsEarnedThisWeek);
            Assert.AreEqual(CollectionAscensionPermits.ManualTrustedWeekKey, profile.ascensionPermitWeekKey);
            StringAssert.Contains("Granted 4", status);
        }

        [Test]
        public void FirstClaim_NearHoardCap_GrantsOnlyRemainingHoard()
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            profile.ascensionPermitBalance = 6;

            int granted = HomePagePresenter.TryClaimManualWeeklyPermits(profile, out string status);

            Assert.AreEqual(2, granted);
            Assert.AreEqual(8, profile.ascensionPermitBalance);
            Assert.AreEqual(2, profile.ascensionPermitsEarnedThisWeek);
            StringAssert.Contains("Granted 2", status);
        }

        [Test]
        public void SecondClaim_SameManualWeekKey_ReturnsZero_AlreadyClaimed()
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);

            Assert.AreEqual(4, HomePagePresenter.TryClaimManualWeeklyPermits(profile, out _));
            int second = HomePagePresenter.TryClaimManualWeeklyPermits(profile, out string status);

            Assert.AreEqual(0, second);
            Assert.AreEqual(4, profile.ascensionPermitBalance);
            Assert.AreEqual(4, profile.ascensionPermitsEarnedThisWeek);
            StringAssert.StartsWith("Already claimed this week.", status);
        }

        [Test]
        public void AfterWeekKeyChange_CounterResets_CanGrantAgain()
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);

            Assert.AreEqual(4, HomePagePresenter.TryClaimManualWeeklyPermits(profile, out _));

            // Simulate CC rotating the stopgap key (test-only — production constant unchanged).
            const string nextWeek = "cc-permit-week-TEST-ROTATION";
            int granted = CollectionAscensionPermits.TryGrantWeekly(
                profile, CollectionSchemaRules.AscensionPermitsPerTrustedWeek, nextWeek);

            Assert.AreEqual(4, granted);
            Assert.AreEqual(8, profile.ascensionPermitBalance);
            Assert.AreEqual(4, profile.ascensionPermitsEarnedThisWeek);
            Assert.AreEqual(nextWeek, profile.ascensionPermitWeekKey);
        }

        [Test]
        public void HoardFull_StatusMessage_NoGrant()
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            profile.ascensionPermitBalance = CollectionSchemaRules.AscensionPermitHoardCap;

            int granted = HomePagePresenter.TryClaimManualWeeklyPermits(profile, out string status);

            Assert.AreEqual(0, granted);
            StringAssert.StartsWith("Hoard full", status);
            Assert.AreEqual(CollectionSchemaRules.AscensionPermitHoardCap, profile.ascensionPermitBalance);
        }

        [Test]
        public void MilestoneFinale_StillDoesNotTouchWeeklyLedger_AfterWeeklyClaim()
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            Assert.AreEqual(4, HomePagePresenter.TryClaimManualWeeklyPermits(profile, out _));

            string weekBefore = profile.ascensionPermitWeekKey;
            int weeklyBefore = profile.ascensionPermitsEarnedThisWeek;
            int balanceBefore = profile.ascensionPermitBalance;

            Assert.AreEqual(1, HomePagePresenter.TryGrantChapterFinalePermit(profile, "5-30"));
            Assert.AreEqual(balanceBefore + 1, profile.ascensionPermitBalance);
            Assert.AreEqual(weeklyBefore, profile.ascensionPermitsEarnedThisWeek);
            Assert.AreEqual(weekBefore, profile.ascensionPermitWeekKey);
        }
    }
}
