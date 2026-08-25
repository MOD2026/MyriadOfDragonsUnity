using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.Season;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class BattlePassShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsBattlePass_" + System.Guid.NewGuid().ToString("N"));
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
        public void OpenValues_StayUnset_AndSeasonLengthIsLocked28Days()
        {
            Assert.AreEqual(28, BattlePassOpenValues.SeasonLengthDays);
            Assert.AreEqual("28-DAY SEASON", BattlePassOpenValues.SeasonLengthCopy);
            Assert.IsNull(BattlePassOpenValues.SeasonXpPerTier);
            Assert.IsNull(BattlePassOpenValues.PremiumUnlockPrice);
            Assert.IsFalse(BattlePassOpenValues.AreTierRewardsConfigured);
        }

        [Test]
        public void Presenter_BuildsDualTrackShell_AndClaimsRefuseWhileOpen()
        {
            var go = new GameObject("BattlePassHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<BattlePassPresenter>();
            presenter.Initialize(onBackToHome: null);

            Assert.IsTrue(BattlePassUiLibrary.HasBattlePassV1Pack);
            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);
            Assert.AreEqual(BattlePassUiLibrary.DualTrackShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.AreEqual("28-DAY SEASON",
                canvas.transform.Find("BattlePassHeader/SeasonLength")?.GetComponent<Text>()?.text);
            Assert.NotNull(canvas.transform.Find("TrackTable/FreeTrackRow/TierWell_0/RewardAmount"));
            Assert.NotNull(canvas.transform.Find("TrackTable/PremiumTrackRow/TierWell_7"));
            Assert.AreEqual(MetagameShellProfileBinding.OpenAmountLabel,
                canvas.transform.Find("TrackTable/FreeTrackRow/TierWell_0/RewardAmount")?.GetComponent<Text>()?.text);
            StringAssert.Contains("OPEN",
                canvas.transform.Find("SeasonXpRow/XpValues")?.GetComponent<Text>()?.text);

            BattlePassClaimResult claim = presenter.ClaimTierForTests(0, premiumTrack: false);
            Assert.AreEqual(BattlePassClaimStatus.OpenValuesNotLocked, claim.Status);
            BattlePassClaimResult unlock = presenter.UnlockPremiumForTests();
            Assert.AreEqual(BattlePassClaimStatus.OpenValuesNotLocked, unlock.Status);
        }

        [Test]
        public void Home_PassButton_OpensBattlePass_AndBackReturnsHome()
        {
            var go = new GameObject("HomeBattlePassReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            Button passBtn = homeCanvas.transform.Find("Btn_BattlePass")?.GetComponent<Button>();
            Assert.NotNull(passBtn);
            passBtn.onClick.Invoke();

            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(go.GetComponent<BattlePassPresenter>());
            Assert.NotNull(GameObject.Find(BattlePassPresenter.CanvasName));

            Button back = GameObject.Find(BattlePassPresenter.CanvasName).transform
                .Find("BattlePassHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<BattlePassPresenter>());
        }
    }
}
