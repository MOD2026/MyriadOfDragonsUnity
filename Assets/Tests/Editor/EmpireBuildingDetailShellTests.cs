using System.IO;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class EmpireBuildingDetailShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsBuildingDetail_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            var profile = new PlayerProfile { gold = 500_000 };
            Assert.IsTrue(SaveSystem.Save(profile));
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
        public void TappingCastleRow_OpensDetailPopup_WithoutDestroyingEmpire()
        {
            var go = new GameObject("EmpireDetailReach");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);

            GameObject empireCanvas = empire.CanvasObjectForTests;
            Assert.NotNull(empireCanvas);
            Button row = empireCanvas.transform.Find("EmpireConstructionRoot/CastleRow")?.GetComponent<Button>();
            Assert.NotNull(row, "Castle row must be tappable to open building detail.");
            row.onClick.Invoke();

            Assert.NotNull(empire.CanvasObjectForTests, "Empire screen must stay up under the popup.");
            Assert.NotNull(GameObject.Find(EmpireBuildingDetailPresenter.CanvasName));
            var detail = go.GetComponent<EmpireBuildingDetailPresenter>();
            Assert.AreEqual(EmpireBuildingKind.Castle, detail.KindForTests);
            Assert.IsTrue(detail.UpgradeButtonActiveForTests);
        }

        [Test]
        public void GuildHall_IsNonUpgrade_AndHidesUpgradeButton()
        {
            var go = new GameObject("GuildHallDetail");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);
            empire.OpenBuildingDetailForTests(EmpireBuildingKind.GuildHall);

            var detail = go.GetComponent<EmpireBuildingDetailPresenter>();
            Assert.IsFalse(detail.UpgradeButtonActiveForTests);
            StringAssert.Contains("NON-UPGRADE BUILDING",
                detail.CanvasObjectForTests.transform.Find("DetailPanel/VariantFraming")?.GetComponent<Text>()?.text);
            EmpireBuildingDetailUpgradeResult pressed = detail.PressUpgradeForTests();
            Assert.AreEqual(EmpireBuildingDetailUpgradeStatus.NonUpgradeBuilding, pressed.Status);
        }

        [Test]
        public void PrisonAndEmbassy_UsePendingServerFraming()
        {
            Assert.IsTrue(EmpireBuildingDetailCopy.FormatVariantFraming(EmpireBuildingKind.Prison)
                .Contains("GUILD FEATURES PENDING SERVER"));
            Assert.IsTrue(EmpireBuildingDetailCopy.FormatVariantFraming(EmpireBuildingKind.Embassy)
                .Contains("GUILD MODE PENDING SERVER"));

            var go = new GameObject("PrisonDetail");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);
            empire.OpenBuildingDetailForTests(EmpireBuildingKind.Prison);
            StringAssert.Contains("PENDING SERVER",
                go.GetComponent<EmpireBuildingDetailPresenter>().CanvasObjectForTests
                    .transform.Find("DetailPanel/VariantFraming")?.GetComponent<Text>()?.text);
        }

        [Test]
        public void DurationWell_DoesNotInventATimer_AndV2UpgradeRefusesPersist()
        {
            var go = new GameObject("StorageDetail");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);
            empire.OpenBuildingDetailForTests(EmpireBuildingKind.Storage);

            var detail = go.GetComponent<EmpireBuildingDetailPresenter>();
            StringAssert.Contains("[runtime]",
                detail.CanvasObjectForTests.transform.Find("DetailPanel/Duration")?.GetComponent<Text>()?.text);
            StringAssert.Contains("Materials",
                detail.CanvasObjectForTests.transform.Find("DetailPanel/UpgradeCost")?.GetComponent<Text>()?.text);

            EmpireBuildingDetailUpgradeResult result = detail.PressUpgradeForTests();
            Assert.AreEqual(EmpireBuildingDetailUpgradeStatus.V2PersistNotWired, result.Status);
        }

        [Test]
        public void ReturnClosesPopup_EmpireCanvasRemains()
        {
            var go = new GameObject("DetailClose");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);
            empire.OpenBuildingDetailForTests(EmpireBuildingKind.Gate);

            Button ret = GameObject.Find(EmpireBuildingDetailPresenter.CanvasName).transform
                .Find("DetailPanel/DetailHeader/Btn_Return")?.GetComponent<Button>();
            Assert.NotNull(ret);
            ret.onClick.Invoke();
            Assert.IsNull(GameObject.Find(EmpireBuildingDetailPresenter.CanvasName));
            Assert.NotNull(empire.CanvasObjectForTests);
        }
    }
}
