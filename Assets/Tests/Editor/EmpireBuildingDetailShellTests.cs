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
            var profile = new PlayerProfile { gold = 500_000, constructionMaterials = 500_000 };
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
            // These two are UNCHANGED and deliberately kept: Guild Hall still has no upgrade
            // LADDER. What changed on 2026-08-26 is that it now carries a real persisted structure
            // LEVEL. Those are different things, and the old copy conflated them.
            Assert.IsFalse(detail.UpgradeButtonActiveForTests);
            EmpireBuildingDetailUpgradeResult pressed = detail.PressUpgradeForTests();
            Assert.AreEqual(EmpireBuildingDetailUpgradeStatus.NonUpgradeBuilding, pressed.Status);

            // COPY UPDATED BY DECISION (BS, locked): "NON-UPGRADE BUILDING" was already untrue here
            // once Guild Hall gained a real level, so it was replaced rather than reverted.
            StringAssert.Contains("Supports Embassy interlock progression",
                detail.CanvasObjectForTests.transform.Find("DetailPanel/VariantFraming")?.GetComponent<Text>()?.text);
        }

        [Test]
        public void PrisonAndEmbassy_UsePendingServerFraming()
        {
            // Wording relocked 2026-08-26. The MEANING is unchanged - structure progression is
            // real, the online feature is not live - but each line now says both halves explicitly
            // instead of only the pending half, because these buildings now have real levels.
            Assert.IsTrue(EmpireBuildingDetailCopy.FormatVariantFraming(EmpireBuildingKind.Prison)
                .Contains("Capture systems unavailable"));
            Assert.IsTrue(EmpireBuildingDetailCopy.FormatVariantFraming(EmpireBuildingKind.Embassy)
                .Contains("Player-help network unavailable"));
            // Each line is asserted against ITS OWN locked wording. An earlier draft of this
            // looped all three against "Structure progression active" and concatenated that string
            // for Guild Hall so the loop would pass - a test that cannot fail for one of its cases
            // is worse than no test, and Guild Hall's locked line genuinely does not say that.
            StringAssert.Contains("Guild functions coming later",
                EmpireBuildingDetailCopy.FormatVariantFraming(EmpireBuildingKind.GuildHall));
            foreach (EmpireBuildingKind kind in new[]
                     { EmpireBuildingKind.Prison, EmpireBuildingKind.Embassy })
            {
                StringAssert.Contains("Structure progression active",
                    EmpireBuildingDetailCopy.FormatVariantFraming(kind),
                    kind + " must state that structure progression is real.");
            }

            var go = new GameObject("PrisonDetail");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);
            empire.OpenBuildingDetailForTests(EmpireBuildingKind.Prison);
            StringAssert.Contains("online services ship",
                go.GetComponent<EmpireBuildingDetailPresenter>().CanvasObjectForTests
                    .transform.Find("DetailPanel/VariantFraming")?.GetComponent<Text>()?.text);
        }

        [Test]
        public void TheThreeInterlockBuildings_ShowStructureLevel_NotABareLevelNumber()
        {
            // BS's reasoning, which is the whole point of the wording: "Do not show simply
            // 'LEVEL 12' - that would imply the building has a functioning Level-12 feature set."
            // The level is REAL and drives Empire interlocks; the online FEATURE is not live.
            var profile = new PlayerProfile();

            foreach (EmpireBuildingKind kind in new[]
                     {
                         EmpireBuildingKind.GuildHall, EmpireBuildingKind.Embassy,
                         EmpireBuildingKind.Prison,
                     })
            {
                Assert.IsTrue(EmpireBuildingDetailCopy.UsesStructureLevelWording(kind),
                    kind + " should use the structure-level wording.");

                string line = EmpireBuildingDetailCopy.FormatLevelLine(kind, profile);
                StringAssert.Contains("STRUCTURE LEVEL", line, kind + " must be labelled a STRUCTURE level.");
                StringAssert.Contains("1", line, kind + " must show its real Day-1 level.");
                StringAssert.DoesNotContain("(flat)", line,
                    kind + " must not claim a flat level - it has a real persisted one now.");
            }

            // The other eight are unchanged: a plain LEVEL, because their features are real.
            StringAssert.DoesNotContain("STRUCTURE LEVEL",
                EmpireBuildingDetailCopy.FormatLevelLine(EmpireBuildingKind.Castle, profile),
                "Castle's feature set is live - it should stay a plain LEVEL.");
        }

        [Test]
        public void TheStructureLevelTooltip_IsLockedCopy_ButHasNoHostYet()
        {
            // Records a real gap rather than letting locked copy quietly not exist: this popup has
            // no tooltip mechanism at all, so the string is captured and ready and nothing shows
            // it. If a tooltip host is ever added, this test is where to notice the wiring is owed.
            StringAssert.Contains("does not imply", EmpireBuildingDetailCopy.StructureLevelTooltip);
            StringAssert.Contains("Empire interlocks", EmpireBuildingDetailCopy.StructureLevelTooltip);
        }

        [Test]
        public void DurationWell_ShowsLockedTimerCurve_NotRuntimePlaceholder()
        {
            var go = new GameObject("CastleDetail");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);
            empire.OpenBuildingDetailForTests(EmpireBuildingKind.Castle);

            var detail = go.GetComponent<EmpireBuildingDetailPresenter>();
            StringAssert.Contains("30m",
                detail.CanvasObjectForTests.transform.Find("DetailPanel/Duration")?.GetComponent<Text>()?.text);
            StringAssert.Contains("Materials",
                detail.CanvasObjectForTests.transform.Find("DetailPanel/UpgradeCost")?.GetComponent<Text>()?.text);

            EmpireBuildingDetailUpgradeResult result = detail.PressUpgradeForTests();
            Assert.AreEqual(EmpireBuildingDetailUpgradeStatus.StartedV1, result.Status);
        }

        [Test]
        public void StorageDetail_V2UpgradeStillRefusesPersist()
        {
            var go = new GameObject("StorageDetail");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);
            empire.OpenBuildingDetailForTests(EmpireBuildingKind.Storage);

            EmpireBuildingDetailUpgradeResult result = go.GetComponent<EmpireBuildingDetailPresenter>().PressUpgradeForTests();
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
