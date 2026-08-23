using System.IO;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>Block J — Collection Forge/Dust UI honesty (label + wallet dust row).</summary>
    public class CollectionForgeDustUiTests
    {
        private string _scratchSaveDir;
        private GameObject _databaseGo;
        private GameObject _presenterGo;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsForgeDustUi_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            _databaseGo = new GameObject("CardDatabase_ForgeDustUi");
            _databaseGo.AddComponent<CardDatabase>().Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenterGo != null) Object.DestroyImmediate(_presenterGo);
            if (_databaseGo != null) Object.DestroyImmediate(_databaseGo);
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
            {
                try { Directory.Delete(_scratchSaveDir, true); }
                catch (IOException) { }
            }
        }

        [Test]
        public void FormatEvolveButtonLabel_WithForgeAndDust_ShowsDiscountedDueNotOnlyBase()
        {
            PlayerProfile profile = NewMigratedProfile(gold: 50_000);
            Card warrior = CardDatabase.Instance.GetCard("warrior");
            Assert.NotNull(warrior);
            int rarity = warrior.Rarity;
            int baseGold = CollectionEvolutionRules.GoldCostForNextStep(rarity, 0);

            profile.collectionWallet.forgeCredits = 5000;
            profile.collectionWallet.dustByRarity.Add(new RarityMaterialBalance { rarity = rarity, dust = 5000 });

            int due = CollectionEvolutionRules.ComputeGoldDue(profile, rarity, baseGold, out int forge, out int dust);
            Assert.Less(due, baseGold);
            Assert.Greater(forge, 0);
            Assert.Greater(dust, 0);

            string label = CollectionPresenter.FormatEvolveButtonLabel(profile, rarity, 0);
            StringAssert.Contains($"{due}g", label);
            StringAssert.Contains($"was {baseGold}g", label);
            StringAssert.DoesNotContain($"Evolve ({baseGold}g)", label);
            StringAssert.Contains("Sacrifice", label);
        }

        [Test]
        public void DetailBody_ShowsMatchingRarityDust_AndWalletCaption()
        {
            PlayerProfile profile = SaveManager.SaveData;
            CollectionSchemaMigration.Apply(profile);
            profile.collectionWallet.genericSacrificeCredits =
                CollectionSchemaRules.GenericSacrificeCreditsFirstStep;
            profile.collectionWallet.forgeCredits = 40;
            Card warrior = CardDatabase.Instance.GetCard("warrior");
            int rarity = warrior.Rarity;
            profile.collectionWallet.dustByRarity.Add(new RarityMaterialBalance { rarity = rarity, dust = 17 });
            profile.cardProgression.Add(new CardProgressionRecord
            {
                cardId = "warrior",
                copyCount = 2,
                evolutionStep = 0,
                cardLevel = 1,
            });
            SaveManager.Save();

            CollectionPresenter presenter = SpawnCollection();
            presenter.ShowDetailForTests("warrior");

            string body = presenter.DetailBodyTextForTests;
            Assert.NotNull(body);
            StringAssert.Contains($"Dust ({rarity}★): 17", body);
            StringAssert.Contains("Forge: 40", body);
            StringAssert.Contains(CollectionPlayerCopy.ForgeDustWalletCaption, body);

            string evolveLabel = presenter.EvolveButtonLabelForTests();
            Assert.NotNull(evolveLabel);
            int baseGold = CollectionEvolutionRules.GoldCostForNextStep(rarity, 0);
            int due = CollectionEvolutionRules.ComputeGoldDue(profile, rarity, baseGold, out _, out _);
            StringAssert.Contains($"{due}g", evolveLabel);
            if (due < baseGold)
                StringAssert.Contains($"was {baseGold}g", evolveLabel);
        }

        [Test]
        public void GetDustBalance_PublicHelper_ReadsMatchingRarityOnly()
        {
            var wallet = new CollectionMaterialWallet();
            wallet.dustByRarity.Add(new RarityMaterialBalance { rarity = 2, dust = 9 });
            wallet.dustByRarity.Add(new RarityMaterialBalance { rarity = 4, dust = 3 });

            Assert.AreEqual(9, CollectionEvolutionRules.GetDustBalance(wallet, 2));
            Assert.AreEqual(3, CollectionEvolutionRules.GetDustBalance(wallet, 4));
            Assert.AreEqual(0, CollectionEvolutionRules.GetDustBalance(wallet, 1));
            Assert.AreEqual(0, CollectionEvolutionRules.GetDustBalance(null, 2));
        }

        private CollectionPresenter SpawnCollection()
        {
            _presenterGo = new GameObject("CollectionForgeDustUiHarness");
            var presenter = _presenterGo.AddComponent<CollectionPresenter>();
            presenter.Initialize(onBackToHome: null, onOpenDeckBuilder: null);
            return presenter;
        }

        private static PlayerProfile NewMigratedProfile(int gold)
        {
            var profile = new PlayerProfile { gold = gold };
            CollectionSchemaMigration.Apply(profile);
            profile.collectionWallet.genericSacrificeCredits =
                CollectionSchemaRules.GenericSacrificeCreditsFirstStep;
            return profile;
        }
    }
}
