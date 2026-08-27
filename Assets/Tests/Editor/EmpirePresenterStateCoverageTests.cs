using System.IO;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// COVERAGE, not correctness — same discipline as GameBootstrapStateCoverageTests, applied to
    /// EmpirePresenter's construction queue (its only real multi-state surface).
    ///
    /// DECLARED STATES (a distinct branch producing different visible output, reachable in normal
    /// play): profile.empireConstruction.status is Idle / Building / ReadyToCollect. All three are
    /// ordinary play — a player opens Empire with nothing queued, starts an upgrade, and eventually
    /// collects it. Driven via the REAL production call chain (EmpireConstructionService.TryStart /
    /// AdvanceIfDue — the exact calls EmpirePresenter.TryStartUpgrade and RefreshPanel already make),
    /// not by hand-setting private fields.
    ///
    /// EXCLUDED, and why: EmpireHeader's OpenAvatarButton (_onOpenAvatar != null) is a real branch in
    /// the source, but HomePagePresenter.OpenEmpire() — the only production call site — always passes
    /// a non-null onOpenAvatar (HomePagePresenter.cs:1067-1079). The null branch is not reachable in
    /// normal play, so per this project's own state-coverage criteria it is not a declared state and
    /// is deliberately not tested here.
    /// </summary>
    public class EmpirePresenterStateCoverageTests
    {
        private System.Collections.Generic.List<GameObject> _spawned = new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsEmpireCoverage_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        private EmpirePresenter SpawnAndInitializePresenter(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            EmpirePresenter presenter = go.AddComponent<EmpirePresenter>();
            presenter.Initialize(onBackToHome: null, onOpenAvatar: () => { });
            return presenter;
        }

        private static Text FindStatus(EmpirePresenter presenter) =>
            presenter.CanvasObjectForTests.transform
                .Find("EmpireConstructionRoot/EmpireStatus")?.GetComponent<Text>();

        private static GameObject FindCollectButton(EmpirePresenter presenter) =>
            presenter.CanvasObjectForTests.transform
                .Find("EmpireConstructionRoot/CollectConstructionButton")?.gameObject;

        private static Button FindCastleUpgradeButton(EmpirePresenter presenter) =>
            presenter.CanvasObjectForTests.transform
                .Find("EmpireConstructionRoot/CastleRow/UpgradeCastleButton")?.GetComponent<Button>();

        [Test]
        public void State1_Idle_ShowsEmptyQueueAndHidesCollectButton()
        {
            // Default fresh profile: empireConstruction defaults to Idle, nothing queued.
            var profile = new PlayerProfile();
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the fresh profile.");
            SaveSystem.ResetCurrentProfileForTests();

            EmpirePresenter presenter = SpawnAndInitializePresenter("Coverage_EmpireIdle");

            Text status = FindStatus(presenter);
            Assert.IsNotNull(status, "STATE UNREACHED: EmpireStatus text was not built.");
            StringAssert.Contains("empty", status.text,
                "STATE UNREACHED: Idle queue did not render its 'empty' status.");

            GameObject collectButton = FindCollectButton(presenter);
            Assert.IsNotNull(collectButton, "STATE UNREACHED: CollectConstructionButton was not built.");
            Assert.IsFalse(collectButton.activeSelf,
                "STATE UNREACHED: Collect button must stay hidden with no project queued.");

            Button upgradeButton = FindCastleUpgradeButton(presenter);
            Assert.IsTrue(upgradeButton.interactable,
                "STATE UNREACHED: Idle must leave upgrade buttons interactable.");
        }

        [Test]
        public void State2_Building_ShowsProgressAndBlocksNewUpgrades()
        {
            var profile = new PlayerProfile { gold = 999999, constructionMaterials = 999999 };
            // Real production call — the exact call EmpirePresenter.TryStartUpgrade makes.
            bool started = EmpireConstructionService.TryStart(profile, EmpireBuildingId.Castle, "coverage-building", out string error);
            Assert.IsTrue(started, $"Setup: expected TryStart to succeed with abundant resources, got '{error}'.");
            Assert.AreEqual(EmpireConstructionStatus.Building, profile.empireConstruction.status, "Setup: expected Building.");

            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the Building state.");
            SaveSystem.ResetCurrentProfileForTests();

            EmpirePresenter presenter = SpawnAndInitializePresenter("Coverage_EmpireBuilding");

            Text status = FindStatus(presenter);
            StringAssert.Contains("Building", status.text,
                "STATE UNREACHED: an in-progress project did not render its Building status.");

            GameObject collectButton = FindCollectButton(presenter);
            Assert.IsFalse(collectButton.activeSelf,
                "STATE UNREACHED: Collect button must stay hidden while still Building.");

            Button upgradeButton = FindCastleUpgradeButton(presenter);
            Assert.IsFalse(upgradeButton.interactable,
                "STATE UNREACHED: an active project must block starting a second upgrade.");
        }

        [Test]
        public void State3_ReadyToCollect_ShowsDoneAndRevealsCollectButton()
        {
            var profile = new PlayerProfile { gold = 999999, constructionMaterials = 999999 };
            bool started = EmpireConstructionService.TryStart(profile, EmpireBuildingId.Castle, "coverage-ready", out string error);
            Assert.IsTrue(started, $"Setup: expected TryStart to succeed with abundant resources, got '{error}'.");

            // Client-clock pacing timer: force the end time into the past so the real
            // AdvanceIfDue call (the same one RefreshPanel makes) ticks Building -> ReadyToCollect,
            // exactly as it would once a player's wall clock passes the real timer.
            profile.empireConstruction.endsAtUtcMs = 1L;
            bool advanced = EmpireConstructionService.AdvanceIfDue(profile);
            Assert.IsTrue(advanced, "Setup: expected AdvanceIfDue to tick a past-due project to ReadyToCollect.");
            Assert.AreEqual(EmpireConstructionStatus.ReadyToCollect, profile.empireConstruction.status, "Setup: expected ReadyToCollect.");

            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the ReadyToCollect state.");
            SaveSystem.ResetCurrentProfileForTests();

            EmpirePresenter presenter = SpawnAndInitializePresenter("Coverage_EmpireReady");

            Text status = FindStatus(presenter);
            StringAssert.Contains("DONE", status.text,
                "STATE UNREACHED: a ready project did not render its DONE status.");

            GameObject collectButton = FindCollectButton(presenter);
            Assert.IsTrue(collectButton.activeSelf,
                "STATE UNREACHED: Collect button must become visible once the project is ready to collect.");

            Button upgradeButton = FindCastleUpgradeButton(presenter);
            Assert.IsFalse(upgradeButton.interactable,
                "STATE UNREACHED: a project awaiting collection must still block starting a second upgrade.");
        }
    }
}
