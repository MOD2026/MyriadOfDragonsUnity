using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// MVP_PLAYABLE_GATE_v1.md drift closed (2026-08-23): the gate doc had no line at all for the
    /// Avatar/Empire construction screen, even though it's real, shipped, and reachable from Home.
    /// Spine-level EditMode coverage, matching how Tutorial→Home / Campaign 1-1 are already proven
    /// in that doc - not a unit test of any one screen's content (EmpireConstructionHomeTests
    /// already covers EmpirePresenter's construction panel in isolation), but proof the real
    /// click-through path never dead-ends: Home → Empire → Avatar → Empire → Home, and the direct
    /// Home → Avatar tile, using the same production button clicks a player would use, not direct
    /// method calls that skip navigation.
    /// </summary>
    public class EmpireAvatarScreenReachabilityTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned = new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsEmpireAvatarReach_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        private HomePagePresenter SpawnAndBuildHome()
        {
            var go = new GameObject("HomePagePresenter_EmpireAvatarReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            return home;
        }

        private static Button FindButton(GameObject canvas, string path)
        {
            Transform target = canvas.transform.Find(path);
            Assert.NotNull(target, $"Missing expected control at '{path}'.");
            Button button = target.GetComponent<Button>();
            Assert.NotNull(button, $"Expected a Button component on '{path}'.");
            return button;
        }

        [Test]
        public void FromHome_AvatarTile_OpensAvatar_AndBackReturnsHome_WithoutGoingThroughEmpire()
        {
            HomePagePresenter home = SpawnAndBuildHome();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;
            Assert.IsNotNull(homeCanvas, "Setup: Home canvas must exist.");

            Button avatarTile = FindButton(homeCanvas, "NavigationStage/Btn_Avatar");
            Assert.IsTrue(avatarTile.interactable, "Home must expose a direct Avatar hero tile.");
            avatarTile.onClick.Invoke();

            Assert.IsFalse(homeCanvas.activeSelf, "Opening Avatar from Home must hide Home.");
            var avatar = home.GetComponent<AvatarPresenter>();
            Assert.IsNotNull(avatar, "Home Avatar tile must attach a real AvatarPresenter.");
            Assert.IsNotNull(avatar.CanvasObjectForTests, "Avatar screen must have a real canvas.");
            Assert.IsTrue(avatar.CanvasObjectForTests.activeInHierarchy, "Avatar canvas must be visible.");
            Assert.IsNotNull(avatar.CanvasObjectForTests.transform.Find("AvatarBody/AvatarName"),
                "Avatar screen must show real Avatar content.");
            Assert.IsNull(home.GetComponent<EmpirePresenter>(),
                "Direct Home → Avatar must not open Empire first.");

            Button backButton = FindButton(avatar.CanvasObjectForTests, "AvatarHeader/Btn_Back");
            Assert.IsTrue(backButton.interactable, "Avatar Back must be clickable.");
            backButton.onClick.Invoke();

            Assert.IsTrue(homeCanvas.activeSelf, "Backing from direct Avatar entry must restore Home.");
            Assert.IsNull(home.GetComponent<AvatarPresenter>(),
                "Leaving Avatar must tear down the AvatarPresenter, not leave a stale screen.");
        }

        [Test]
        public void FromHome_OpeningEmpire_ThenAvatar_ThenBackToEmpire_ThenBackToHome_NeverDeadEnds()
        {
            HomePagePresenter home = SpawnAndBuildHome();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;
            Assert.IsNotNull(homeCanvas, "Setup: Home canvas must exist.");
            Assert.IsTrue(homeCanvas.activeSelf, "Setup: Home canvas should start active.");

            // --- Home -> Empire, via the real production nav hook (same call the Empire tile uses) ---
            home.OpenEmpireForTests();

            Assert.IsFalse(homeCanvas.activeSelf, "Opening Empire must hide Home, not stack on top of it.");
            var empire = home.GetComponent<EmpirePresenter>();
            Assert.IsNotNull(empire, "Opening Empire must attach a real EmpirePresenter.");
            Assert.IsNotNull(empire.CanvasObjectForTests, "Empire screen must have a real canvas, not a dead click.");
            Assert.IsTrue(empire.CanvasObjectForTests.activeInHierarchy, "Empire canvas must actually be visible.");
            Assert.IsNotNull(empire.CanvasObjectForTests.transform.Find("EmpireConstructionRoot"),
                "Empire screen must show real construction content, not a blank screen.");

            // --- Empire -> Avatar, via the real on-screen "AVATAR" nav button ---
            Button openAvatarButton = FindButton(empire.CanvasObjectForTests, "EmpireHeader/OpenAvatarButton");
            Assert.IsTrue(openAvatarButton.interactable, "The Avatar nav button on the Empire screen must be clickable.");
            openAvatarButton.onClick.Invoke();

            var avatar = home.GetComponent<AvatarPresenter>();
            Assert.IsNotNull(avatar, "Opening Avatar from Empire must attach a real AvatarPresenter.");
            Assert.IsNotNull(avatar.CanvasObjectForTests, "Avatar screen must have a real canvas, not a dead click.");
            Assert.IsTrue(avatar.CanvasObjectForTests.activeInHierarchy, "Avatar canvas must actually be visible.");
            Assert.IsNotNull(avatar.CanvasObjectForTests.transform.Find("AvatarBody/AvatarName"),
                "Avatar screen must show real Avatar content, not a blank screen.");

            // --- Avatar -> Empire, via the real on-screen "OPEN EMPIRE" button (round trip, not a one-way door) ---
            Button openEmpireButton = FindButton(avatar.CanvasObjectForTests, "AvatarBody/Btn_OpenEmpire");
            Assert.IsTrue(openEmpireButton.interactable, "The Empire nav button on the Avatar screen must be clickable.");
            openEmpireButton.onClick.Invoke();

            empire = home.GetComponent<EmpirePresenter>();
            Assert.IsNotNull(empire, "Returning to Empire from Avatar must produce a real EmpirePresenter again.");
            Assert.IsNotNull(empire.CanvasObjectForTests, "Empire screen must be real again after the round trip, not stuck.");
            Assert.IsTrue(empire.CanvasObjectForTests.activeInHierarchy, "Empire canvas must be visible again after the round trip.");

            // --- Empire -> Home, via the real "< BACK" button ---
            Button backButton = FindButton(empire.CanvasObjectForTests, "EmpireHeader/Btn_Back");
            Assert.IsTrue(backButton.interactable, "The Empire screen's Back button must be clickable.");
            backButton.onClick.Invoke();

            Assert.IsTrue(homeCanvas.activeSelf, "Backing out of Empire must restore Home - the whole path must be a round trip, not a dead end.");
        }
    }
}
