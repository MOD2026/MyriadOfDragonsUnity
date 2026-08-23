using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>Collection schema V1 migration + permit caps (COLLECTION_SCHEMA_PROPOSAL_v1).</summary>
    public class CollectionSchemaMigrationTests
    {
        private string _scratchSaveDir;
        private GameObject _databaseGo;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsCollectionSchema_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            _databaseGo = new GameObject("CardDatabase_CollectionSchemaTests");
            _databaseGo.AddComponent<CardDatabase>().Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_databaseGo != null) Object.DestroyImmediate(_databaseGo);

            try
            {
                if (Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
            }
            catch (IOException) { }
        }

        [Test]
        public void Migration_UniqueLegacyIds_BecomeOneCopyEach()
        {
            var profile = new PlayerProfile();
            profile.cardCollection.Add("warrior");
            profile.cardCollection.Add("cleric");

            CollectionSchemaMigration.Apply(profile, id => id == "warrior" || id == "cleric");

            Assert.AreEqual(1, profile.collectionSchemaVersion);
            Assert.AreEqual(2, profile.cardProgression.Count);
            Assert.AreEqual(1, FindRecord(profile, "warrior").copyCount);
            Assert.AreEqual(1, FindRecord(profile, "cleric").copyCount);
            CollectionAssert.AreEqual(new[] { "warrior", "cleric" }, profile.cardCollection,
                "Legacy list must remain untouched for rollback.");
        }

        [Test]
        public void Migration_LegacyCardCollection_RemainsReadableAfterV1()
        {
            var profile = new PlayerProfile();
            profile.cardCollection.Add("warrior");
            profile.cardCollection.Add("cleric");
            profile.cardCollection.Add("warrior");

            CollectionSchemaMigration.Apply(profile, _ => true);

            Assert.IsTrue(profile.UsesCollectionV1);
            Assert.NotNull(profile.cardCollection);
            Assert.AreEqual(3, profile.cardCollection.Count,
                "V1 migration must not clear or rewrite the legacy cardCollection list.");
            CollectionAssert.AreEqual(new[] { "warrior", "cleric", "warrior" }, profile.cardCollection);

            // Readable snapshot: callers can still enumerate legacy ids after V1.
            Assert.IsTrue(profile.cardCollection.Contains("warrior"));
            Assert.IsTrue(profile.cardCollection.Contains("cleric"));
            Assert.AreEqual(2, FindRecord(profile, "warrior").copyCount);
            Assert.AreEqual(1, FindRecord(profile, "cleric").copyCount);
        }

        [Test]
        public void Migration_RepeatedLegacyIds_GroupIntoCopyCount()
        {
            var profile = new PlayerProfile();
            profile.cardCollection.Add("warrior");
            profile.cardCollection.Add("warrior");
            profile.cardCollection.Add("cleric");

            CollectionSchemaMigration.Apply(profile, _ => true);

            Assert.AreEqual(1, profile.collectionSchemaVersion);
            Assert.AreEqual(2, profile.cardProgression.Count);
            Assert.AreEqual(2, FindRecord(profile, "warrior").copyCount);
        }

        [Test]
        public void Migration_UnknownIds_QuarantinedNotProgression()
        {
            var profile = new PlayerProfile();
            profile.cardCollection.Add("not_a_real_card");
            profile.cardCollection.Add("warrior");

            CollectionSchemaMigration.Apply(profile, id => id == "warrior");

            CollectionAssert.Contains(profile.collectionMigrationUnknownIds, "not_a_real_card");
            Assert.AreEqual(1, profile.cardProgression.Count);
            Assert.AreEqual("warrior", profile.cardProgression[0].cardId);
        }

        [Test]
        public void Migration_SecondLoad_DoesNotDuplicateCopies()
        {
            var profile = new PlayerProfile();
            profile.cardCollection.Add("warrior");
            profile.cardCollection.Add("warrior");

            CollectionSchemaMigration.Apply(profile, _ => true);
            CollectionSchemaMigration.Apply(profile, _ => true);

            Assert.AreEqual(1, profile.cardProgression.Count);
            Assert.AreEqual(2, profile.cardProgression[0].copyCount);
        }

        [Test]
        public void SaveLoad_RoundTripsCollectionV1Fields()
        {
            var written = new PlayerProfile();
            written.cardCollection.Add("warrior");
            written.normalPityMisses = 2;
            written.highPityMissesSince5Star = 3;
            written.ascensionPermitBalance = 4;
            CollectionSchemaMigration.Apply(written, _ => true);

            Assert.IsTrue(SaveSystem.Save(written));

            PlayerProfile read = SaveSystem.Load(out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.Loaded, status);
            Assert.AreEqual(1, read.collectionSchemaVersion);
            Assert.AreEqual(2, read.normalPityMisses);
            Assert.AreEqual(3, read.highPityMissesSince5Star);
            Assert.AreEqual(4, read.ascensionPermitBalance);
            Assert.AreEqual(1, read.cardProgression.Count);
        }

        [Test]
        public void AscensionPermit_TrustedWeekKey_CapsAtSixteenHoard()
        {
            var profile = new PlayerProfile();
            int first = CollectionAscensionPermits.TryGrantWeekly(profile, 10, "week-1");
            int second = CollectionAscensionPermits.TryGrantWeekly(profile, 10, "week-1");

            Assert.AreEqual(8, first, "Weekly cap is 8 per trusted week.");
            Assert.AreEqual(0, second, "Weekly cap already exhausted.");
            Assert.AreEqual(8, profile.ascensionPermitBalance);
            Assert.AreEqual(8, profile.ascensionPermitsEarnedThisWeek);

            int week2 = CollectionAscensionPermits.TryGrantWeekly(profile, 10, "week-2");
            Assert.AreEqual(8, week2);
            Assert.AreEqual(16, profile.ascensionPermitBalance, "Hoard cap is 16.");
            Assert.AreEqual(8, profile.ascensionPermitsEarnedThisWeek,
                "New week key must reset the weekly earned counter before granting.");

            int overflow = CollectionAscensionPermits.TryGrantWeekly(profile, 1, "week-3");
            Assert.AreEqual(0, overflow, "Cannot exceed hoard cap.");
        }

        [Test]
        public void AscensionPermit_WeekKeyChange_ResetsWeeklyCounter_WithoutClearingHoard()
        {
            var profile = new PlayerProfile();
            Assert.AreEqual(8, CollectionAscensionPermits.TryGrantWeekly(profile, 8, "week-a"));
            Assert.AreEqual(8, profile.ascensionPermitsEarnedThisWeek);
            Assert.AreEqual(8, profile.ascensionPermitBalance);

            // Week key change: weekly counter resets; hoard balance is preserved.
            int granted = CollectionAscensionPermits.TryGrantWeekly(profile, 3, "week-b");
            Assert.AreEqual(3, granted);
            Assert.AreEqual("week-b", profile.ascensionPermitWeekKey);
            Assert.AreEqual(3, profile.ascensionPermitsEarnedThisWeek);
            Assert.AreEqual(11, profile.ascensionPermitBalance);

            int restOfWeek = CollectionAscensionPermits.TryGrantWeekly(profile, 10, "week-b");
            Assert.AreEqual(5, restOfWeek, "Weekly remaining is 8-3=5.");
            Assert.AreEqual(8, profile.ascensionPermitsEarnedThisWeek);
            Assert.AreEqual(16, profile.ascensionPermitBalance, "Hoard fills to 16.");
        }

        [Test]
        public void AscensionPermit_MilestoneGrant_DoesNotTouchWeeklyLedger()
        {
            var profile = new PlayerProfile();
            profile.ascensionPermitWeekKey = "week-locked";
            profile.ascensionPermitsEarnedThisWeek = 5;
            profile.ascensionPermitBalance = 2;

            int granted = CollectionAscensionPermits.TryGrantMilestone(profile, 3);

            Assert.AreEqual(3, granted);
            Assert.AreEqual(5, profile.ascensionPermitBalance);
            Assert.AreEqual(5, profile.ascensionPermitsEarnedThisWeek,
                "Milestone must not change weekly earned counter.");
            Assert.AreEqual("week-locked", profile.ascensionPermitWeekKey,
                "Milestone must not change week key.");
        }

        [Test]
        public void AscensionPermit_MilestoneGrant_RespectsHoardCapOnly()
        {
            var profile = new PlayerProfile();
            profile.ascensionPermitBalance = 15;
            profile.ascensionPermitsEarnedThisWeek = 0;
            profile.ascensionPermitWeekKey = string.Empty;

            Assert.AreEqual(1, CollectionAscensionPermits.TryGrantMilestone(profile, 5));
            Assert.AreEqual(16, profile.ascensionPermitBalance);
            Assert.AreEqual(0, profile.ascensionPermitsEarnedThisWeek);
            Assert.AreEqual(string.Empty, profile.ascensionPermitWeekKey);
            Assert.AreEqual(0, CollectionAscensionPermits.TryGrantMilestone(profile, 1));
        }

        [Test]
        public void AscensionPermit_MilestoneGrant_AtFullHoard_ReturnsZero()
        {
            var profile = new PlayerProfile();
            profile.ascensionPermitBalance = CollectionSchemaRules.AscensionPermitHoardCap;
            profile.ascensionPermitWeekKey = "week-x";
            profile.ascensionPermitsEarnedThisWeek = 3;

            Assert.AreEqual(0, CollectionAscensionPermits.TryGrantMilestone(profile, 1));
            Assert.AreEqual(CollectionSchemaRules.AscensionPermitHoardCap, profile.ascensionPermitBalance);
            Assert.AreEqual("week-x", profile.ascensionPermitWeekKey);
            Assert.AreEqual(3, profile.ascensionPermitsEarnedThisWeek);
        }

        [Test]
        public void AscensionPermit_EmptyWeekKey_RefusesGrant()
        {
            var profile = new PlayerProfile();
            Assert.AreEqual(0, CollectionAscensionPermits.TryGrantWeekly(profile, 5, string.Empty));
            Assert.AreEqual(0, profile.ascensionPermitBalance);
        }

        [Test]
        public void ShopInversePricing_LockedSkus_MonotonicGemPerCard()
        {
            Assert.Less(150f, 160f);
            Assert.Less(160f, 1650f / 9f);
            Assert.Less(1650f / 9f, 4000f / 20f);
        }

        [Test]
        public void ProgressionGrant_AfterMigration_WritesProgressionOnly()
        {
            var profile = new PlayerProfile();
            profile.cardCollection.Add("warrior");
            CollectionSchemaMigration.Apply(profile, _ => true);
            Assert.IsTrue(profile.UsesCollectionV1);

            Assert.IsTrue(CollectionProgression.TryGrantFirstCopy(profile, "cleric", _ => true));
            Assert.AreEqual(1, FindRecord(profile, "cleric").copyCount);
            CollectionAssert.DoesNotContain(profile.cardCollection, "cleric",
                "V1 grants must not mutate legacy list.");
        }

        [Test]
        public void ProgressionGrant_RefusesDuplicate()
        {
            var profile = new PlayerProfile();
            profile.cardCollection.Add("warrior");
            CollectionSchemaMigration.Apply(profile, _ => true);

            Assert.IsFalse(CollectionProgression.TryGrantFirstCopy(profile, "warrior", _ => true));
            Assert.AreEqual(1, FindRecord(profile, "warrior").copyCount);
        }

        private static CardProgressionRecord FindRecord(PlayerProfile profile, string cardId)
        {
            foreach (CardProgressionRecord record in profile.cardProgression)
            {
                if (record.cardId == cardId) return record;
            }

            Assert.Fail($"Expected progression record for {cardId}.");
            return null;
        }
    }
}
