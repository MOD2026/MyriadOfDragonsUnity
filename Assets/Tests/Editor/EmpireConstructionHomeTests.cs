using System.IO;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class EmpireConstructionHomeTests
    {
        private GameObject _spawned;
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsEmpireHome_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            var profile = new PlayerProfile { gold = 500_000, constructionMaterials = 500_000 };
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            _spawned = new GameObject("EmpireConstructionHarness");
        }

        [TearDown]
        public void TearDown()
        {
            EmpireConstructionTimer.ClearTestClock();

            if (_spawned != null)
                Object.DestroyImmediate(_spawned);

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void EmpireScreen_IncludesConstructionPanel_WithUpgradeAndCollectControls()
        {
            var presenter = _spawned.AddComponent<EmpirePresenter>();
            presenter.BuildUIForTests();

            Transform empireRoot = presenter.CanvasObjectForTests.transform.Find("EmpireConstructionRoot");
            Assert.NotNull(empireRoot, "Expected EmpireConstructionRoot on Empire screen.");
            Assert.NotNull(empireRoot.Find("CastleRow/UpgradeCastleButton"));
            Assert.NotNull(empireRoot.Find("BarracksRow/UpgradeBarracksButton"));
            Assert.NotNull(empireRoot.Find("GateRow/UpgradeGateButton"));
            Assert.NotNull(empireRoot.Find("CollectConstructionButton"));
        }

        [Test]
        public void CastleRow_ShowsResourceAndHpPayoff_AtLevelWithCastleBonus()
        {
            PlayerProfile profile = SaveSystem.CurrentProfile;
            profile.castleLevel = 5;
            profile.ApplyDataToEmpire();
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var presenter = _spawned.AddComponent<EmpirePresenter>();
            presenter.BuildUIForTests();
            presenter.RefreshPanelForTests();

            Text castleRow = presenter.CanvasObjectForTests.transform
                .Find("EmpireConstructionRoot/CastleRow/RowSummary")?.GetComponent<Text>();
            Assert.NotNull(castleRow);

            int resourceBonus = PlayerEmpireData.CastleResourceBonusForLevel(5);
            int healthBonus = PlayerEmpireData.CastleHealthBonusForLevel(5);
            Assert.Greater(resourceBonus, 0, "Setup: Castle L5 must grant a Resource bonus.");
            Assert.Greater(healthBonus, 0, "Setup: Castle L5 must grant an HP bonus.");

            // "Cap" is the row's own live terminology throughout (matches "Cap {live.Empire.ResourceCap}"
            // asserted below, and FormatCastleRow's own "+{dCap} Cap" wording) - "Resource" was a stale
            // label from before that terminology settled, never updated here.
            StringAssert.Contains($"+{resourceBonus} Cap", castleRow.text);
            StringAssert.Contains($"+{healthBonus} HP", castleRow.text);
            StringAssert.Contains("Castle L5", castleRow.text);

            PlayerProfile live = SaveSystem.CurrentProfile;
            live.ApplyDataToEmpire();
            StringAssert.Contains($"Cap {live.Empire.ResourceCap}", castleRow.text,
                "Castle row must show live ResourceCap from Empire after ApplyDataToEmpire.");
            StringAssert.Contains($"Start HP {live.Empire.StartingAvatarHealth}", castleRow.text,
                "Castle row must show live StartingAvatarHealth from Empire after ApplyDataToEmpire.");
        }

        [Test]
        public void GateRow_ShowsCampaignChapterOpen_AndContainsCh()
        {
            var presenter = _spawned.AddComponent<EmpirePresenter>();
            presenter.Initialize(onBackToHome: null);

            Text gateRow = presenter.CanvasObjectForTests.transform
                .Find("EmpireConstructionRoot/GateRow/RowSummary")?.GetComponent<Text>();
            Assert.NotNull(gateRow);
            Assert.IsFalse(string.IsNullOrWhiteSpace(gateRow.text), "Gate summary must be non-empty.");
            StringAssert.Contains("Ch", gateRow.text);
            StringAssert.Contains("Campaign open:", gateRow.text);

            PlayerProfile profile = SaveSystem.CurrentProfile;
            int openChapter = PlayerEmpireData.GetHighestCampaignChapterAllowed(profile.gateLevel);
            StringAssert.Contains($"Ch{openChapter}", gateRow.text);
            if (openChapter < 10)
                StringAssert.Contains($"Upgrade Gate to open Ch{openChapter + 1}", gateRow.text);
        }

        [Test]
        public void FormatGateRowSummary_FreshGate_MentionsOpenCh1AndNextCh2()
        {
            string summary = EmpirePresenter.FormatGateRowSummary(
                gateLevel: 1,
                highestChapterAllowed: 1,
                nextGateMilestone: 3,
                gateUpgradeGold: 1000);
            StringAssert.Contains("Campaign open: Ch1", summary);
            StringAssert.Contains("Upgrade Gate to open Ch2", summary);
            StringAssert.Contains("Gate L1", summary);
        }

        [Test]
        public void UpgradeButtons_AreNonInteractable_WhileProjectBuildingOrReady()
        {
            var presenter = _spawned.AddComponent<EmpirePresenter>();
            presenter.BuildUIForTests();

            ClickEmpireButton(presenter, "CastleRow/UpgradeCastleButton");
            presenter.RefreshPanelForTests();

            Assert.AreEqual(EmpireConstructionStatus.Building, SaveSystem.CurrentProfile.empireConstruction.status);

            Button castle = FindEmpireButton(presenter, "CastleRow/UpgradeCastleButton");
            Button barracks = FindEmpireButton(presenter, "BarracksRow/UpgradeBarracksButton");
            Button gate = FindEmpireButton(presenter, "GateRow/UpgradeGateButton");
            Assert.IsFalse(castle.interactable);
            Assert.IsFalse(barracks.interactable);
            Assert.IsFalse(gate.interactable);
        }

        [Test]
        public void UpgradeCastle_FromEmpireScreen_RaisesLevelAfterTimerAndCollect()
        {
            var presenter = _spawned.AddComponent<EmpirePresenter>();
            presenter.BuildUIForTests();

            PlayerProfile profile = SaveSystem.CurrentProfile;
            int goldBefore = profile.gold;
            int materialsBefore = profile.constructionMaterials;

            ClickEmpireButton(presenter, "CastleRow/UpgradeCastleButton");
            presenter.RefreshPanelForTests();

            Assert.AreEqual(EmpireConstructionStatus.Building, profile.empireConstruction.status);
            Assert.Less(profile.gold, goldBefore);
            Assert.Less(profile.constructionMaterials, materialsBefore);

            EmpireConstructionTimer.UtcNowMsOverrideForTests = profile.empireConstruction.endsAtUtcMs;
            presenter.RefreshPanelForTests();
            Assert.AreEqual(EmpireConstructionStatus.ReadyToCollect, profile.empireConstruction.status);

            ClickEmpireButton(presenter, "CollectConstructionButton");
            presenter.RefreshPanelForTests();

            Assert.AreEqual(2, profile.castleLevel);
            Assert.AreEqual(EmpireConstructionStatus.CompleteClaimed, profile.empireConstruction.status);

            Button castle = FindEmpireButton(presenter, "CastleRow/UpgradeCastleButton");
            Assert.IsTrue(castle.interactable, "After collect, upgrade buttons must be usable again.");
        }

        private static Button FindEmpireButton(EmpirePresenter presenter, string relativePath)
        {
            Transform target = presenter.CanvasObjectForTests.transform.Find($"EmpireConstructionRoot/{relativePath}");
            Assert.NotNull(target, $"Missing Empire control at EmpireConstructionRoot/{relativePath}.");
            Button button = target.GetComponent<Button>();
            Assert.NotNull(button, $"Expected Button on EmpireConstructionRoot/{relativePath}.");
            return button;
        }

        private static void ClickEmpireButton(EmpirePresenter presenter, string relativePath)
        {
            FindEmpireButton(presenter, relativePath).onClick.Invoke();
        }
    }
}
