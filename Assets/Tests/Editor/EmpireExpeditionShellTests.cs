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
    /// Empire Expedition metagame wrapper (LOCKED 2026-08-24 structure only). No invented
    /// Stamina/reward numbers — production clear refuses while OPEN; harness proves wiring.
    /// </summary>
    public class EmpireExpeditionShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsEmpireExpedition_" + System.Guid.NewGuid().ToString("N"));
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
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        [Test]
        public void Catalog_UsesExpeditionIds_NotCampaignStageIds()
        {
            Assert.GreaterOrEqual(EmpireExpeditionCatalog.Stages.Count, 1);
            foreach (EmpireExpeditionStageDefinition stage in EmpireExpeditionCatalog.Stages)
            {
                Assert.IsFalse(stage.StageId.Contains("-") && char.IsDigit(stage.StageId[0]),
                    "Expedition ids must stay separate from Campaign N-M ids.");
                Assert.IsTrue(stage.StageId.StartsWith("exp-"),
                    $"Expected exp-* shell id, got '{stage.StageId}'.");
            }
        }

        [Test]
        public void OpenValues_RemainUnset_UntilOwnerLocks()
        {
            Assert.IsNull(EmpireExpeditionOpenValues.StaminaCostPerClear);
            Assert.IsNull(EmpireExpeditionOpenValues.BaseGoldPerClear);
            Assert.IsNull(EmpireExpeditionOpenValues.BaseMaterialsPerClear);
            Assert.IsNull(EmpireExpeditionOpenValues.DailyExpeditionGoldCap);
            Assert.IsNull(EmpireExpeditionOpenValues.DailyAttemptCap);
            Assert.IsFalse(EmpireExpeditionOpenValues.AreClearRewardsConfigured);
            Assert.AreEqual(0.10f, EmpireExpeditionOpenValues.GuildGoldBonusFraction);
        }

        [Test]
        public void ProductionClear_RefusesWhileOpenValuesUnset()
        {
            var profile = new PlayerProfile { stamina = 100, gold = 0 };
            EmpireExpeditionClearResult result = EmpireExpeditionClearTransaction.TryApplyClear(
                profile, "exp-1", UnavailableGuildExpeditionBonusQuery.Instance);

            Assert.AreEqual(EmpireExpeditionClearStatus.OpenValuesNotLocked, result.Status);
            Assert.AreEqual(100, profile.stamina);
            Assert.AreEqual(0, profile.gold);
        }

        [Test]
        public void GuildBonusDisplay_FailClosedWhenServiceUnavailable()
        {
            string line = EmpireExpeditionClearTransaction.FormatGuildBonusDisplayLine(
                UnavailableGuildExpeditionBonusQuery.Instance);
            StringAssert.Contains("fail closed", line.ToLowerInvariant());
        }

        [Test]
        public void ConfiguredClear_SpendsStamina_GrantsGold_FailClosedGuild_NoMaterialsPersist()
        {
            var profile = new PlayerProfile { stamina = 50, gold = 100, maxStamina = 100 };
            EmpireExpeditionClearResult result =
                EmpireExpeditionClearTransaction.TryApplyClearWithConfiguredAmountsForTests(
                    profile,
                    "exp-1",
                    UnavailableGuildExpeditionBonusQuery.Instance,
                    staminaCost: 5,
                    baseGold: 40,
                    baseMaterials: 10,
                    dailyGoldCap: 1000,
                    persist: false);

            Assert.AreEqual(EmpireExpeditionClearStatus.Applied, result.Status);
            Assert.AreEqual(45, profile.stamina);
            Assert.AreEqual(140, profile.gold);
            Assert.IsFalse(result.GuildBonusApplied, "Unavailable guild query must fail closed.");
            Assert.AreEqual(10, result.MaterialsGranted);
            Assert.IsFalse(result.MaterialsPersisted,
                "Materials must not invent a PlayerProfile field.");
        }

        [Test]
        public void ConfiguredClear_EligibleGuild_AddsTenPercent_WithoutRaisingDailyCap()
        {
            var profile = new PlayerProfile { stamina = 50, gold = 0, maxStamina = 100 };
            var eligible = new FixedGuildExpeditionBonusQuery(true);

            EmpireExpeditionClearResult underCap =
                EmpireExpeditionClearTransaction.TryApplyClearWithConfiguredAmountsForTests(
                    profile, "exp-1", eligible,
                    staminaCost: 1, baseGold: 100, baseMaterials: 0, dailyGoldCap: 1000,
                    persist: false);
            Assert.AreEqual(110, underCap.GoldGranted);
            Assert.IsTrue(underCap.GuildBonusApplied);

            profile.gold = 0;
            profile.stamina = 50;
            EmpireExpeditionClearResult nearCap =
                EmpireExpeditionClearTransaction.TryApplyClearWithConfiguredAmountsForTests(
                    profile, "exp-2", eligible,
                    staminaCost: 1, baseGold: 100, baseMaterials: 0, dailyGoldCap: 105,
                    expeditionGoldEarnedTodayUtc: 0,
                    persist: false);
            // Base 100 + bonus room 5 only (cap 105) — bonus cannot raise the cap.
            Assert.AreEqual(105, nearCap.GoldGranted);
            Assert.AreEqual(5, nearCap.GuildBonusGold);
        }

        [Test]
        public void Presenter_BuildsRotationNodes_AndShowsOpenStatus()
        {
            var go = new GameObject("EmpireExpeditionHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<EmpireExpeditionPresenter>();
            presenter.Initialize(onBack: null);

            Assert.IsNotNull(presenter.CanvasObjectForTests);
            Assert.IsNotNull(presenter.CanvasObjectForTests.transform.Find("StageScrollView/Viewport/StageNodesContent/StageNode_exp-1"));
            StringAssert.Contains("OPEN", presenter.StatusTextForTests.ToUpperInvariant());
            StringAssert.Contains("fail closed", presenter.GuildBonusTextForTests.ToLowerInvariant());

            EmpireExpeditionClearResult refused = presenter.SimulateClearForTests("exp-1");
            Assert.AreEqual(EmpireExpeditionClearStatus.OpenValuesNotLocked, refused.Status);
        }

        [Test]
        public void EmpireScreen_OpenExpeditionButton_ReachesExpeditionCanvas()
        {
            var go = new GameObject("EmpireHarness_Expedition");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);

            Button expeditionBtn = empire.CanvasObjectForTests.transform
                .Find("EmpireHeader/OpenExpeditionButton")?.GetComponent<Button>();
            Assert.NotNull(expeditionBtn, "Empire header must expose EXPEDITION.");
            expeditionBtn.onClick.Invoke();

            Assert.IsNotNull(go.GetComponent<EmpireExpeditionPresenter>());
            Assert.IsNotNull(GameObject.Find(EmpireExpeditionPresenter.CanvasName));
        }

        private sealed class FixedGuildExpeditionBonusQuery : IGuildExpeditionBonusQuery
        {
            private readonly bool? _value;
            public FixedGuildExpeditionBonusQuery(bool? value) => _value = value;
            public bool? TryQueryExpeditionGoldBonusEligible() => _value;
        }
    }
}
