using System.IO;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    public class CollectionEvolutionTests
    {
        private string _scratchSaveDir;
        private GameObject _databaseGo;

        [SetUp]
        public void SetUp()
        {
            CollectionEvolutionService.ClearCommittedReceiptsForTests();
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsEvolve_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            _databaseGo = new GameObject("CardDatabase_EvolveTests");
            _databaseGo.AddComponent<CardDatabase>().Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            CollectionEvolutionService.ClearCommittedReceiptsForTests();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_databaseGo != null) Object.DestroyImmediate(_databaseGo);
        }

        [Test]
        public void Evolve_AppliesForgeAndDustOffsets_ReducingGoldSpent()
        {
            PlayerProfile profile = NewMigratedProfile(gold: 10000);
            Card warrior = CardDatabase.Instance.GetCard("warrior");
            Assert.NotNull(warrior);
            int rarity = warrior.Rarity;
            int baseGold = CollectionEvolutionRules.GoldCostForNextStep(rarity, 0);

            profile.collectionWallet.forgeCredits = 5000;
            profile.collectionWallet.dustByRarity.Add(new RarityMaterialBalance { rarity = rarity, dust = 5000 });
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2, evolutionStep = 0 });

            int goldBefore = profile.gold;
            int forgeBefore = profile.collectionWallet.forgeCredits;

            Assert.IsTrue(CollectionEvolutionService.TryEvolve(profile, "warrior", receiptId: null, out CollectionEvolutionReceiptResult result));

            Assert.Less(result.GoldSpent, baseGold, "Forge/dust should reduce gold due below full recipe.");
            Assert.Greater(result.ForgeCreditsSpent, 0);
            Assert.Greater(result.DustSpent, 0);
            Assert.Less(profile.gold, goldBefore);
            Assert.Less(profile.collectionWallet.forgeCredits, forgeBefore);
        }

        [Test]
        public void FirstEvolutionStep_SpendsGold_NoPermit()
        {
            PlayerProfile profile = NewMigratedProfile(gold: 5000);
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2, evolutionStep = 0 });

            bool ok = CollectionEvolutionService.TryEvolve(profile, "warrior", receiptId: null, out CollectionEvolutionReceiptResult result);

            Assert.IsTrue(ok, result.Error.ToString());
            Assert.AreEqual(1, profile.cardProgression[0].evolutionStep);
            Assert.AreEqual(1, profile.cardProgression[0].copyCount);
            Assert.IsFalse(result.PermitSpent);
            Assert.Less(profile.gold, 5000);
        }

        [Test]
        public void SecondEvolutionStep_RequiresPermit()
        {
            PlayerProfile profile = NewMigratedProfile(gold: 10000);
            profile.ascensionPermitBalance = 0;
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2, evolutionStep = 1 });

            bool ok = CollectionEvolutionService.TryEvolve(profile, "warrior", receiptId: null, out CollectionEvolutionReceiptResult result);

            Assert.IsFalse(ok);
            Assert.AreEqual(CollectionEvolutionError.PermitRequired, result.Error);
            Assert.AreEqual(1, profile.cardProgression[0].evolutionStep);
        }

        [Test]
        public void SecondEvolutionStep_WithPermit_Succeeds()
        {
            PlayerProfile profile = NewMigratedProfile(gold: 10000);
            profile.ascensionPermitBalance = 1;
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2, evolutionStep = 1 });

            Assert.IsTrue(CollectionEvolutionService.TryEvolve(profile, "warrior", receiptId: null, out CollectionEvolutionReceiptResult result));

            Assert.AreEqual(2, profile.cardProgression[0].evolutionStep);
            Assert.AreEqual(0, profile.ascensionPermitBalance);
            Assert.IsTrue(result.PermitSpent);
            Assert.AreEqual(0, result.SacrificeCreditsSpent, "Sacrifice credits only gate step 0→1.");
        }

        [Test]
        public void FirstEvolutionStep_RequiresGenericSacrificeCredit()
        {
            PlayerProfile profile = NewMigratedProfile(gold: 5000);
            profile.collectionWallet.genericSacrificeCredits = 0;
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2, evolutionStep = 0 });

            bool ok = CollectionEvolutionService.TryEvolve(profile, "warrior", receiptId: null, out CollectionEvolutionReceiptResult result);

            Assert.IsFalse(ok);
            Assert.AreEqual(CollectionEvolutionError.InsufficientSacrificeCredits, result.Error);
            Assert.AreEqual(0, profile.cardProgression[0].evolutionStep);
        }

        [Test]
        public void FirstEvolutionStep_SpendsSacrificeCredit_NotOnLaterSteps()
        {
            PlayerProfile profile = NewMigratedProfile(gold: 5000);
            profile.collectionWallet.genericSacrificeCredits = 3;
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2, evolutionStep = 0 });

            Assert.IsTrue(CollectionEvolutionService.TryEvolve(profile, "warrior", receiptId: null, out CollectionEvolutionReceiptResult first));
            Assert.AreEqual(1, first.SacrificeCreditsSpent);
            Assert.AreEqual(2, profile.collectionWallet.genericSacrificeCredits);
        }

        [Test]
        public void SaveFailure_RollsBackGoldCopiesPermitAndWallet()
        {
            PlayerProfile profile = NewMigratedProfile(gold: 10000);
            profile.ascensionPermitBalance = 2;
            profile.collectionWallet.forgeCredits = 100;
            profile.collectionWallet.dustByRarity.Add(new RarityMaterialBalance { rarity = 1, dust = 50 });
            profile.cardProgression.Add(new CardProgressionRecord
            {
                cardId = "warrior",
                copyCount = 2,
                evolutionStep = 1,
                cardLevel = 3,
                trainingXp = 10,
            });

            int goldBefore = profile.gold;
            int copiesBefore = profile.cardProgression[0].copyCount;
            int stepBefore = profile.cardProgression[0].evolutionStep;
            int permitsBefore = profile.ascensionPermitBalance;
            int forgeBefore = profile.collectionWallet.forgeCredits;

            bool ok = CollectionEvolutionService.TryEvolve(
                profile,
                "warrior",
                receiptId: null,
                out CollectionEvolutionReceiptResult result,
                saveFn: _ => false);

            Assert.IsFalse(ok);
            Assert.AreEqual(CollectionEvolutionError.SaveFailed, result.Error);
            Assert.AreEqual(goldBefore, profile.gold);
            Assert.AreEqual(copiesBefore, profile.cardProgression[0].copyCount);
            Assert.AreEqual(stepBefore, profile.cardProgression[0].evolutionStep);
            Assert.AreEqual(permitsBefore, profile.ascensionPermitBalance);
            Assert.AreEqual(forgeBefore, profile.collectionWallet.forgeCredits);
            Assert.AreEqual(3, profile.cardProgression[0].cardLevel);
            Assert.AreEqual(10, profile.cardProgression[0].trainingXp);
        }

        [Test]
        public void SaveThrows_RollsBackGoldCopiesPermitAndWallet()
        {
            // Same defect as the burn path: restore ran only on a FALSE return, never on a throw,
            // leaving copy, gold, materials and any spent permit consumed with nothing granted.
            PlayerProfile profile = NewMigratedProfile(gold: 10000);
            profile.ascensionPermitBalance = 2;
            profile.collectionWallet.forgeCredits = 100;
            profile.collectionWallet.dustByRarity.Add(new RarityMaterialBalance { rarity = 1, dust = 50 });
            profile.cardProgression.Add(new CardProgressionRecord
            {
                cardId = "warrior",
                copyCount = 2,
                evolutionStep = 1,
                cardLevel = 3,
                trainingXp = 10,
            });

            int goldBefore = profile.gold;
            int copiesBefore = profile.cardProgression[0].copyCount;
            int stepBefore = profile.cardProgression[0].evolutionStep;
            int permitsBefore = profile.ascensionPermitBalance;
            int forgeBefore = profile.collectionWallet.forgeCredits;

            Assert.Throws<IOException>(() => CollectionEvolutionService.TryEvolve(
                profile,
                "warrior",
                receiptId: null,
                out CollectionEvolutionReceiptResult _,
                saveFn: _ => throw new IOException("disk full mid-save")));

            Assert.AreEqual(goldBefore, profile.gold);
            Assert.AreEqual(copiesBefore, profile.cardProgression[0].copyCount, "consumed copy was not restored after a throwing save");
            Assert.AreEqual(stepBefore, profile.cardProgression[0].evolutionStep);
            Assert.AreEqual(permitsBefore, profile.ascensionPermitBalance, "spent ascension permit was not restored after a throwing save");
            Assert.AreEqual(forgeBefore, profile.collectionWallet.forgeCredits);
            Assert.AreEqual(3, profile.cardProgression[0].cardLevel);
            Assert.AreEqual(10, profile.cardProgression[0].trainingXp);
        }

        private static PlayerProfile NewMigratedProfile(int gold = 1000)
        {
            var profile = new PlayerProfile { gold = gold };
            CollectionSchemaMigration.Apply(profile);
            profile.collectionWallet.genericSacrificeCredits =
                CollectionSchemaRules.GenericSacrificeCreditsFirstStep;
            return profile;
        }
    }
}
