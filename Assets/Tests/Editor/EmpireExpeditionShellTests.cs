using System.IO;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Metagame;
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
        public void ConfiguredClear_SpendsStamina_GrantsGold_FailClosedGuild_MaterialsPersist()
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
            // Stale since 2026-08-26: this asserted materials could never persist because
            // PlayerProfile.constructionMaterials didn't exist yet. It has existed since
            // 2026-08-24, and EmpireExpeditionClearTransaction.cs now writes to it (see its own
            // comment at the grant site). Fail-closed guild bonus is orthogonal to materials
            // persistence - updated to assert the real, current behavior.
            Assert.IsTrue(result.MaterialsPersisted,
                "Materials grant must persist to PlayerProfile.constructionMaterials.");
            Assert.AreEqual(10, profile.constructionMaterials);
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

        [Test]
        public void RealClearApplied_EmitsBothModeRunCompletedAndModeRewardClaimed()
        {
            // Real retention-telemetry wiring check (register: "Retention telemetry architecture
            // - LOCKED" / dispatch "wire the actual emit calls into real gameplay call sites").
            // The real production clear refuses while EmpireExpeditionOpenValues stays OPEN (see
            // EmpireScreen_ExpeditionRefusesWhileOpenValuesUnset above) - this reaches the real
            // "Applied" path (and therefore the real EmitClearTelemetry logic) through
            // SimulateClearWithConfiguredAmountsForTests, the same configured-amounts pattern
            // EmpireExpeditionClearTransactionTests already uses for the transaction class itself.
            var go = new GameObject("EmpireExpeditionTelemetryHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<EmpireExpeditionPresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratchDir = Path.Combine(Path.GetTempPath(), "MoDEmpireExpeditionTelemetry_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratchDir);
            presenter.Initialize(onBack: null, telemetryOutbox: outbox);

            EmpireExpeditionClearResult result = presenter.SimulateClearWithConfiguredAmountsForTests(
                "exp-1", staminaCost: 1, baseGold: 50, baseMaterials: 5, dailyGoldCap: 500);
            Assert.AreEqual(EmpireExpeditionClearStatus.Applied, result.Status, "Setup: expected a real successful clear.");

            Assert.AreEqual(2, fakeGateway.SentEvents.Count, "A real successful clear must emit both a run-completed and a reward-claimed event.");
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeModeRunCompleted, fakeGateway.SentEvents[0].eventType);
            Assert.AreEqual("empire_expedition", fakeGateway.SentEvents[0].mode);
            Assert.AreEqual("exp-1", fakeGateway.SentEvents[0].runId);
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeModeRewardClaimed, fakeGateway.SentEvents[1].eventType);
            Assert.AreEqual(0, outbox.QueuedEventsForTests.Count, "Successfully-sent events must not remain queued.");

            if (Directory.Exists(telemetryScratchDir)) Directory.Delete(telemetryScratchDir, recursive: true);
        }

        [Test]
        public void RealClearHittingTheDailyGoldCap_EmitsDailyCapReached()
        {
            var go = new GameObject("EmpireExpeditionTelemetryCapHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<EmpireExpeditionPresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratchDir = Path.Combine(Path.GetTempPath(), "MoDEmpireExpeditionTelemetryCap_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratchDir);
            presenter.Initialize(onBack: null, telemetryOutbox: outbox);

            // dailyGoldCap already fully earned today -> real DailyGoldCapWouldReject path.
            EmpireExpeditionClearResult result = presenter.SimulateClearWithConfiguredAmountsForTests(
                "exp-1", staminaCost: 1, baseGold: 50, baseMaterials: 0, dailyGoldCap: 100,
                expeditionGoldEarnedTodayUtc: 100);
            Assert.AreEqual(EmpireExpeditionClearStatus.DailyGoldCapWouldReject, result.Status, "Setup: expected a real cap refusal.");

            Assert.AreEqual(1, fakeGateway.SentEvents.Count);
            Assert.AreEqual(RetentionTelemetryEvents.EventTypeDailyCapReached, fakeGateway.SentEvents[0].eventType);

            if (Directory.Exists(telemetryScratchDir)) Directory.Delete(telemetryScratchDir, recursive: true);
        }

        [Test]
        public void RefusedClear_OpenValuesNotLocked_NeverEmitsTelemetry()
        {
            var go = new GameObject("EmpireExpeditionTelemetryRefuseHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<EmpireExpeditionPresenter>();
            var fakeGateway = new FakeRetentionTelemetryGateway();
            var telemetryScratchDir = Path.Combine(Path.GetTempPath(), "MoDEmpireExpeditionTelemetryRefuse_" + System.Guid.NewGuid().ToString("N"));
            var outbox = new RetentionTelemetryOutbox(fakeGateway, telemetryScratchDir);
            presenter.Initialize(onBack: null, telemetryOutbox: outbox);

            EmpireExpeditionClearResult refused = presenter.SimulateClearForTests("exp-1");
            Assert.AreEqual(EmpireExpeditionClearStatus.OpenValuesNotLocked, refused.Status, "Setup: real production clear must still refuse.");

            Assert.AreEqual(0, fakeGateway.SentEvents.Count, "An OpenValuesNotLocked refusal is not a real completion or cap event - must not emit telemetry.");

            if (Directory.Exists(telemetryScratchDir)) Directory.Delete(telemetryScratchDir, recursive: true);
        }

        private sealed class FixedGuildExpeditionBonusQuery : IGuildExpeditionBonusQuery
        {
            private readonly bool? _value;
            public FixedGuildExpeditionBonusQuery(bool? value) => _value = value;
            public bool? TryQueryExpeditionGoldBonusEligible() => _value;
        }
    }
}
