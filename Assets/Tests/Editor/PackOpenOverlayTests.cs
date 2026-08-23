using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class PackOpenOverlayTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private GameObject _databaseGo;

        [SetUp]
        public void SetUp()
        {
            CollectionPackReceiptService.ClearCommittedReceiptsForTests();
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsPackOpen_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            _databaseGo = new GameObject("CardDatabase_PackOpenTests");
            _spawned.Add(_databaseGo);
            _databaseGo.AddComponent<CardDatabase>().Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            CollectionPackReceiptService.ClearCommittedReceiptsForTests();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            var canvas = GameObject.Find("ShopCanvas");
            if (canvas != null) Object.DestroyImmediate(canvas);
        }

        [Test]
        public void GemPackPurchase_ShowsOverlayWithDrawTiles()
        {
            var profile = NewMigratedProfile(gems: 500);
            var shopGo = new GameObject("ShopPackOpenHarness");
            _spawned.Add(shopGo);
            var shop = shopGo.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);

            Assert.IsTrue(shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId));

            Transform drawContent = shop.PackDrawContentForTests;
            Assert.NotNull(drawContent, "Gem pack purchase must open the reveal overlay.");
            Assert.AreEqual(1, drawContent.childCount);
        }

        [Test]
        public void GemPackPurchase_RevealAnimation_StartsWithOneVisibleTile()
        {
            var profile = NewMigratedProfile(gems: 5000);
            var shopGo = new GameObject("ShopPackOpenHarness");
            _spawned.Add(shopGo);
            var shop = shopGo.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);

            Assert.IsTrue(shop.PurchaseForTests(CollectionPackCatalog.ScoutCacheSkuId));

            PackOpenRevealRunner runner = shop.PackRevealRunnerForTests;
            Assert.NotNull(runner, "Overlay must attach a reveal runner.");
            Assert.AreEqual(5, runner.TotalTilesForTests);
            Assert.AreEqual(1, runner.RevealedCountForTests);
            Assert.AreEqual(1, runner.CountVisibleTilesForTests());
        }

        [Test]
        public void GemPackPurchase_RevealAnimation_StepsThroughRemainingTiles()
        {
            var profile = NewMigratedProfile(gems: 5000);
            var shopGo = new GameObject("ShopPackOpenHarness");
            _spawned.Add(shopGo);
            var shop = shopGo.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);

            Assert.IsTrue(shop.PurchaseForTests(CollectionPackCatalog.ScoutCacheSkuId));

            PackOpenRevealRunner runner = shop.PackRevealRunnerForTests;
            Assert.NotNull(runner);

            for (int step = 2; step <= 5; step++)
            {
                Assert.IsTrue(runner.RevealNextForTests(), $"expected reveal step {step}");
                Assert.AreEqual(step, runner.RevealedCountForTests);
                Assert.AreEqual(step, runner.CountVisibleTilesForTests());
            }

            Assert.IsFalse(runner.RevealNextForTests(), "no tiles left to reveal");
        }

        [Test]
        public void GemPackPurchase_OverlayShowsCollectionNextStep_AndDismissStatusKeepsIt()
        {
            var profile = NewMigratedProfile(gems: 500);
            var shopGo = new GameObject("ShopPackOpenHarness");
            _spawned.Add(shopGo);
            var shop = shopGo.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);

            Assert.IsTrue(shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId));

            Transform shopCanvas = GameObject.Find("ShopCanvas")?.transform;
            Assert.NotNull(shopCanvas);

            Text subtitle = PackOpenOverlayPresenter.SubtitleForTests(shopCanvas);
            Assert.NotNull(subtitle);
            StringAssert.Contains(PackOpenOverlayPresenter.CollectionNextStepCopy, subtitle.text);

            Text continueLabel = PackOpenOverlayPresenter.ContinueLabelForTests(shopCanvas);
            Assert.NotNull(continueLabel);
            StringAssert.Contains("Collection", continueLabel.text);

            PackOpenRevealRunner runner = shop.PackRevealRunnerForTests;
            Assert.NotNull(runner);
            while (runner.RevealNextForTests()) { }

            Button continueBtn = shopCanvas.Find($"{PackOpenOverlayPresenter.OverlayRootName}/PackOpenDim/ModalPanel/BtnContinue")
                ?.GetComponent<Button>();
            Assert.NotNull(continueBtn);
            Assert.IsTrue(continueBtn.interactable, "Continue must unlock after full reveal.");
            continueBtn.onClick.Invoke();

            Assert.AreEqual(PackOpenOverlayPresenter.CollectionNextStepCopy,
                PackOpenOverlayPresenter.LastDismissStatusForTests);
            Text dismissStatus = PackOpenOverlayPresenter.ShopDismissStatusForTests(shopCanvas);
            Assert.NotNull(dismissStatus);
            StringAssert.Contains("Collection", dismissStatus.text);
        }

        private static PlayerProfile NewMigratedProfile(int gems)
        {
            var profile = new PlayerProfile { gems = gems };
            CollectionSchemaMigration.Apply(profile);
            return profile;
        }
    }
}
