using System.IO;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    public class CollectionBurnTests
    {
        private string _scratchSaveDir;
        private GameObject _databaseGo;

        [SetUp]
        public void SetUp()
        {
            CollectionBurnService.ClearCommittedReceiptsForTests();
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsBurn_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            _databaseGo = new GameObject("CardDatabase_BurnTests");
            _databaseGo.AddComponent<CardDatabase>().Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            CollectionBurnService.ClearCommittedReceiptsForTests();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_databaseGo != null) Object.DestroyImmediate(_databaseGo);
        }

        [Test]
        public void BurnTrainingXp_CanLevelUp_WhenThresholdMet()
        {
            PlayerProfile profile = NewMigratedProfile();
            profile.cardProgression.Add(new CardProgressionRecord
            {
                cardId = "warrior",
                copyCount = 2,
                cardLevel = 1,
                trainingXp = 95,
            });

            Assert.IsTrue(CollectionBurnService.TryBurnCopy(
                profile, "warrior", CollectionBurnPath.TrainingXp, receiptId: null, out _));

            Assert.GreaterOrEqual(profile.cardProgression[0].cardLevel, 2);
        }

        [Test]
        public void ApplyLevelUps_CapsCardLevelAtOneHundred()
        {
            var record = new CardProgressionRecord
            {
                cardId = "warrior",
                cardLevel = 99,
                trainingXp = 1_000_000,
            };

            int gained = CollectionTrainingRules.ApplyLevelUps(record);

            Assert.AreEqual(1, gained);
            Assert.AreEqual(CollectionSchemaRules.MaxCardLevel, record.cardLevel);
            Assert.Greater(record.trainingXp, 0, "Surplus XP remains once the level cap is hit.");

            Assert.AreEqual(0, CollectionTrainingRules.ApplyLevelUps(record),
                "Already at max level — no further level-ups.");
            Assert.AreEqual(CollectionSchemaRules.MaxCardLevel, record.cardLevel);
        }

        [Test]
        public void BurnDuplicate_DecrementsCopy_AndGrantsTrainingXp()
        {
            PlayerProfile profile = NewMigratedProfile();
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 3, trainingXp = 5 });
            int goldBefore = profile.gold;

            bool ok = CollectionBurnService.TryBurnCopy(
                profile, "warrior", CollectionBurnPath.TrainingXp, receiptId: null, out CollectionBurnReceiptResult result);

            Assert.IsTrue(ok, result.Error.ToString());
            Assert.AreEqual(2, profile.cardProgression[0].copyCount);
            Assert.Greater(profile.cardProgression[0].trainingXp, 5);
            Assert.AreEqual(goldBefore, profile.gold, "Burn must never grant gold.");
        }

        [Test]
        public void BurnLastCopy_Refused()
        {
            PlayerProfile profile = NewMigratedProfile();
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 1 });

            bool ok = CollectionBurnService.TryBurnCopy(
                profile, "warrior", CollectionBurnPath.ForgeCredit, receiptId: null, out CollectionBurnReceiptResult result);

            Assert.IsFalse(ok);
            Assert.AreEqual(CollectionBurnError.InsufficientCopies, result.Error);
            Assert.AreEqual(1, profile.cardProgression[0].copyCount);
        }

        [Test]
        public void BurnForge_IncrementsWallet()
        {
            PlayerProfile profile = NewMigratedProfile();
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2 });

            Assert.IsTrue(CollectionBurnService.TryBurnCopy(
                profile, "warrior", CollectionBurnPath.ForgeCredit, receiptId: null, out _));

            Assert.Greater(profile.collectionWallet.forgeCredits, 0);
        }

        [Test]
        public void BurnDust_IncrementsDustByRarity()
        {
            PlayerProfile profile = NewMigratedProfile();
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2 });
            int dustBefore = GetDustTotal(profile);

            Assert.IsTrue(CollectionBurnService.TryBurnCopy(
                profile, "warrior", CollectionBurnPath.Dust, receiptId: null, out CollectionBurnReceiptResult result));

            Assert.Greater(GetDustTotal(profile), dustBefore);
            Assert.AreEqual(1, profile.cardProgression[0].copyCount);
        }

        [Test]
        public void SaveFailure_Rollback()
        {
            PlayerProfile profile = NewMigratedProfile();
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2, trainingXp = 0 });

            bool ok = CollectionBurnService.TryBurnCopy(
                profile,
                "warrior",
                CollectionBurnPath.TrainingXp,
                receiptId: null,
                out CollectionBurnReceiptResult result,
                saveFn: _ => false);

            Assert.IsFalse(ok);
            Assert.AreEqual(CollectionBurnError.SaveFailed, result.Error);
            Assert.AreEqual(2, profile.cardProgression[0].copyCount);
            Assert.AreEqual(0, profile.cardProgression[0].trainingXp);
        }

        [Test]
        public void SaveThrows_RollsBackJustLikeSaveReturningFalse()
        {
            // A save that THROWS used to leave the profile mutated with the copy already consumed,
            // so a later save from any path could persist the loss. Restore must not depend on the
            // save reporting failure politely.
            PlayerProfile profile = NewMigratedProfile();
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2, trainingXp = 0 });

            Assert.Throws<IOException>(() => CollectionBurnService.TryBurnCopy(
                profile,
                "warrior",
                CollectionBurnPath.TrainingXp,
                receiptId: null,
                out CollectionBurnReceiptResult _,
                saveFn: _ => throw new IOException("disk full mid-save")));

            Assert.AreEqual(2, profile.cardProgression[0].copyCount, "burned copy was not restored after a throwing save");
            Assert.AreEqual(0, profile.cardProgression[0].trainingXp);
        }

        private static PlayerProfile NewMigratedProfile()
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            return profile;
        }

        private static int GetDustTotal(PlayerProfile profile)
        {
            int total = 0;
            foreach (RarityMaterialBalance entry in profile.collectionWallet.dustByRarity)
                total += entry.dust;
            return total;
        }
    }
}
