using System.IO;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class FriendsShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsFriendsShell_" + System.Guid.NewGuid().ToString("N"));
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
            StringAssert.Contains("OPEN", FriendsOpenValues.StatusNote.ToUpperInvariant());
            Assert.AreEqual("[runtime]", FriendsOpenValues.RuntimePlaceholder);
        }

        [Test]
        public void Presenter_BuildsArtShell_AndActionsRefuse()
        {
            var go = new GameObject("FriendsHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<FriendsPresenter>();
            presenter.Initialize(onBack: null);

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);

            Assert.IsTrue(FriendsUiLibrary.HasFriendsV1Pack);
            Assert.AreEqual(FriendsUiLibrary.ScreenShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.NotNull(canvas.transform.Find("FriendsList/FriendRow_5"));
            Assert.NotNull(canvas.transform.Find("NavRail/Nav_Friends"));
            Assert.IsNotNull(canvas.transform.Find("FriendsList/FriendRow_0/RelIcon")?.GetComponent<Image>()?.sprite,
                "Friend rows must show relationship atlas icons.");
            Assert.IsNotNull(canvas.transform.Find("ProfileDrawer/Btn_Gift/ActionIcon")?.GetComponent<Image>()?.sprite,
                "Gift must show profile-action atlas icon.");
            StringAssert.Contains("You:",
                canvas.transform.Find("ProfileDrawer/PublicSummary")?.GetComponent<Text>()?.text);
            var refuse = presenter.MessageForTests();
            Assert.AreEqual(FriendsActionStatus.OpenValuesNotLocked, refuse.Status);

        }

        [Test]
        public void Home_FriendsButton_OpensShell_AndBackReturnsHome()
        {
            // Real reachability path since the Home IA rebuild - Friends is a tab inside the
            // global Social drawer, not a direct Home button.
            var go = new GameObject("HomeFriendsReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            home.OpenSocialDrawerForTests();
            GameObject drawer = home.SocialDrawerObjectForTests;
            Assert.NotNull(drawer, "Setup: expected the Social drawer to open.");
            Button openBtn = drawer.transform.Find("TabBar/Dest_FRIENDS")?.GetComponent<Button>();
            Assert.NotNull(openBtn, "Setup: expected a FRIENDS tab in the Social drawer.");
            openBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(FriendsPresenter.CanvasName));

            Button back = GameObject.Find(FriendsPresenter.CanvasName).transform
                .Find("FriendsHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<FriendsPresenter>());
            Assert.IsNull(home.SocialDrawerObjectForTests,
                "The sub-presenter's own Back button must close the whole drawer, not strand it empty.");
        }

    }
}
