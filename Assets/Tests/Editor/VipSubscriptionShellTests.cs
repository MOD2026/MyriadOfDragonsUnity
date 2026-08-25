using System.IO;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class VipSubscriptionShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsVipShell_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void OpenValues_RefuseWhileUnset()
        {
            Assert.IsNull(VipSubscriptionOpenValues.WeeklyGemPrice);
            Assert.IsNull(VipSubscriptionOpenValues.FortnightGemPrice);
            Assert.IsNull(VipSubscriptionOpenValues.MonthlyGemPrice);
            Assert.IsFalse(VipSubscriptionOpenValues.ArePricesConfigured);
            StringAssert.Contains("OPEN", VipSubscriptionOpenValues.StatusNote.ToUpperInvariant());
            Assert.AreEqual("[runtime]", VipSubscriptionOpenValues.RuntimePlaceholder);
        }

        [Test]
        public void Presenter_BuildsArtShell_AndActionsRefuse()
        {
            var go = new GameObject("VipHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<VipSubscriptionPresenter>();
            presenter.Initialize(onBack: null);

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);
            Assert.IsTrue(VipSubscriptionUiLibrary.HasVipSubscriptionV1Pack);
            Assert.AreEqual(VipSubscriptionUiLibrary.ScreenShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.IsFalse(canvas.transform.Find("Background").GetComponent<Image>().raycastTarget);
            Assert.NotNull(canvas.transform.Find("BenefitGrid/BenefitWell_5"));
            Assert.NotNull(canvas.transform.Find("IdentityColumn/StateSocket_2"));
            StringAssert.Contains("OPEN",
                canvas.transform.Find("BenefitGrid/BenefitWell_0/Label")?.GetComponent<Text>()?.text);
            StringAssert.Contains("Not subscribed",
                canvas.transform.Find("IdentityColumn/EntitlementState")?.GetComponent<Text>()?.text);

            VipSubscriptionActionResult subscribe = presenter.SubscribeForTests();
            Assert.AreEqual(VipSubscriptionActionStatus.OpenValuesNotLocked, subscribe.Status);
            VipSubscriptionActionResult restore = presenter.RestoreForTests();
            Assert.AreEqual(VipSubscriptionActionStatus.OpenValuesNotLocked, restore.Status);
        }

        [Test]
        public void Home_VipButton_OpensShell_AndBackReturnsHome()
        {
            var go = new GameObject("HomeVipReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            Button openBtn = homeCanvas.transform.Find("TopHud/Btn_Vip")?.GetComponent<Button>();
            Assert.NotNull(openBtn);
            openBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(VipSubscriptionPresenter.CanvasName));

            Button back = GameObject.Find(VipSubscriptionPresenter.CanvasName).transform
                .Find("VipHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<VipSubscriptionPresenter>());
        }
    }
}
