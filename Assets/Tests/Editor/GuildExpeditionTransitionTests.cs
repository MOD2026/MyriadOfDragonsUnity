using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// WH-UI-METAGAME-TRANSITION-PROTOTYPE-001 — Guild Expedition canvas fade open/close.
    /// </summary>
    public class GuildExpeditionTransitionTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            MotionPolicy.ReduceMotion = false;
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MoDGuildExpeditionTransition_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            MotionPolicy.ReduceMotion = false;
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

        private GuildExpeditionPresenter Open()
        {
            var go = new GameObject("GuildExpeditionTransitionHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<GuildExpeditionPresenter>();
            presenter.Initialize(onBack: null, gateway: new FakeGateway());
            return presenter;
        }

        [Test]
        public async Task BackButton_HasLandscapeHitArea_AndReturnsViaCallback()
        {
            bool backFired = false;
            var go = new GameObject("GuildExpeditionBackHitHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<GuildExpeditionPresenter>();
            presenter.Initialize(onBack: () => backFired = true, gateway: new FakeGateway());

            Transform back = presenter.CanvasObjectForTests.transform
                .Find("GuildExpeditionHeader/Btn_Back");
            Assert.NotNull(back, "Btn_Back must exist.");
            RectTransform rect = back.GetComponent<RectTransform>();
            Assert.GreaterOrEqual(rect.sizeDelta.x, 200f,
                "Landscape Back width must match Settings hit-target floor.");
            Assert.GreaterOrEqual(rect.sizeDelta.y, 72f,
                "Landscape Back height must match Settings hit-target floor.");
            Assert.AreEqual(presenter.CanvasObjectForTests.transform.Find("GuildExpeditionHeader").childCount - 1,
                back.GetSiblingIndex(),
                "BACK must be last sibling so title/status cannot steal clicks.");
            Assert.IsFalse(presenter.CanvasObjectForTests.transform
                .Find("GuildExpeditionHeader/Title").GetComponent<Text>().raycastTarget);
            Assert.IsFalse(presenter.CanvasObjectForTests.transform
                .Find("GuildExpeditionHeader/StatusLine").GetComponent<Text>().raycastTarget);
            Assert.IsFalse(back.Find("Text").GetComponent<Text>().raycastTarget);

            await presenter.WaitForOpenTransitionForTests();
            await presenter.PressBackForTests();
            Assert.IsTrue(backFired, "Back must invoke the navigation callback.");
            Assert.IsNull(presenter.CanvasObjectForTests);
        }

        [Test]
        public void ObjectiveTiles_LongestLabelFitsAtFloor_AndStageIconClearsLabel()
        {
            GuildExpeditionPresenter presenter = Open();
            Transform canvas = presenter.CanvasObjectForTests.transform;
            ForceFullLayoutRebuild(canvas);

            Transform grid = canvas.Find("ExpeditionPanel/ObjectiveGrid");
            Assert.NotNull(grid, "ObjectiveGrid must exist.");

            RectTransform gridRt = grid.GetComponent<RectTransform>();
            Assert.GreaterOrEqual(gridRt.anchorMax.x - gridRt.anchorMin.x, 0.65f,
                "Grid must be wide enough for 22px wrap of provisional labels.");
            Assert.GreaterOrEqual(gridRt.anchorMax.y - gridRt.anchorMin.y, 0.45f,
                "Grid must be tall enough for multi-line provisional labels.");

            for (int i = 0; i < GuildExpeditionPresenter.PlayerFacingObjectiveLabels.Length; i++)
            {
                Transform well = grid.Find($"Objective_{i}");
                Assert.NotNull(well, $"Objective_{i} missing.");
                Assert.IsNull(well.GetComponent<Button>(),
                    $"Objective_{i} must stay passive (Image only).");

                Text label = well.Find("Label")?.GetComponent<Text>();
                Assert.NotNull(label);
                Assert.AreEqual(GuildExpeditionPresenter.PlayerFacingObjectiveLabels[i], label.text,
                    $"Objective_{i} label must match provisional copy exactly.");
                Assert.GreaterOrEqual(label.fontSize, GuildExpeditionPresenter.ObjectiveTileLabelFontSize,
                    $"Objective_{i} must not shrink below the 22px floor.");
                Assert.AreEqual(HorizontalWrapMode.Wrap, label.horizontalOverflow);
                Assert.IsFalse(label.raycastTarget);

                Transform icon = well.Find("StageIcon");
                Assert.NotNull(icon);
                Assert.GreaterOrEqual(icon.GetComponent<RectTransform>().anchorMin.y,
                    label.rectTransform.anchorMax.y - 0.02f,
                    $"Objective_{i}: StageIcon must sit above the label band.");
            }

            int longest = GuildExpeditionPresenter.LongestObjectiveLabelIndex;
            Transform longestWell = grid.Find($"Objective_{longest}");
            Text longestLabel = longestWell.Find("Label").GetComponent<Text>();
            ForceFullLayoutRebuild(longestWell);

            float boxW = longestLabel.rectTransform.rect.width;
            float boxH = longestLabel.rectTransform.rect.height;
            Assert.Greater(boxW, 180f, "Longest label must have a real laid-out width after canvas rebuild.");
            Assert.Greater(boxH, 40f, "Longest label must have a real laid-out height after canvas rebuild.");

            TextGenerationSettings settings = longestLabel.GetGenerationSettings(new Vector2(boxW, 0f));
            float wrappedH = longestLabel.cachedTextGeneratorForLayout.GetPreferredHeight(
                longestLabel.text, settings) / longestLabel.pixelsPerUnit;
            Assert.LessOrEqual(wrappedH, boxH + 1f,
                $"Longest label wrapped height {wrappedH:F1} overflows box height {boxH:F1} at fontSize {longestLabel.fontSize}.");

            Rect labelWorld = WorldRect(longestLabel.rectTransform);
            Rect iconWorld = WorldRect(longestWell.Find("StageIcon").GetComponent<RectTransform>());
            Assert.IsFalse(labelWorld.Overlaps(iconWorld),
                $"Longest-tile StageIcon {iconWorld} must not overlap Label {labelWorld}.");
        }

        private static void ForceFullLayoutRebuild(Transform root)
        {
            Canvas.ForceUpdateCanvases();
            foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float xMin = corners[0].x, xMax = corners[0].x;
            float yMin = corners[0].y, yMax = corners[0].y;
            for (int i = 1; i < 4; i++)
            {
                if (corners[i].x < xMin) xMin = corners[i].x;
                if (corners[i].x > xMax) xMax = corners[i].x;
                if (corners[i].y < yMin) yMin = corners[i].y;
                if (corners[i].y > yMax) yMax = corners[i].y;
            }
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        [Test]
        public async Task OpenTransition_ReachesVisibleState()
        {
            GuildExpeditionPresenter presenter = Open();
            await presenter.WaitForOpenTransitionForTests();
            Assert.IsNotNull(presenter.CanvasObjectForTests);
            Assert.AreEqual(1f, presenter.CanvasAlphaForTests, 0.001f);
        }

        [Test]
        public async Task BackTransition_CompletesBeforeTeardown()
        {
            GuildExpeditionPresenter presenter = Open();
            await presenter.WaitForOpenTransitionForTests();
            Assert.IsNotNull(presenter.CanvasObjectForTests);

            await presenter.PressBackForTests();
            Assert.IsTrue(presenter.BackFadeCompletedBeforeTeardownForTests,
                "Fade-out must finish (alpha 0) while the canvas still exists, before TeardownUI.");
            Assert.IsNull(presenter.CanvasObjectForTests);
        }

        [Test]
        public void DuplicateTeardown_IsHarmless()
        {
            GuildExpeditionPresenter presenter = Open();
            Assert.IsNotNull(presenter.CanvasObjectForTests);
            Assert.DoesNotThrow(() =>
            {
                presenter.TeardownUI();
                presenter.TeardownUI();
                presenter.TeardownUI();
            });
            Assert.IsNull(presenter.CanvasObjectForTests);
            Assert.AreEqual(-1f, presenter.CanvasAlphaForTests);
        }

        [Test]
        public void ReducedMotion_CompletesImmediately()
        {
            Assert.AreEqual(0f,
                GuildExpeditionPresenter.ResolveTransitionDurationSeconds(
                    GuildExpeditionPresenter.TransitionDurationSeconds, reduceMotion: true));
            Assert.Greater(
                GuildExpeditionPresenter.ResolveTransitionDurationSeconds(
                    GuildExpeditionPresenter.TransitionDurationSeconds, reduceMotion: false),
                0f);

            MotionPolicy.ReduceMotion = true;
            GuildExpeditionPresenter presenter = Open();
            Assert.AreEqual(1f, presenter.CanvasAlphaForTests, 0.001f,
                "With ReduceMotion, open fade must snap to visible without waiting.");
        }

        private sealed class FakeGateway : IGuildExpeditionGateway
        {
            public Task<GuildExpeditionAttemptResult> ConsumeAttemptAsync(
                System.Threading.CancellationToken cancellationToken) =>
                Task.FromResult(new GuildExpeditionAttemptResult { success = true, remaining = 1 });

            public Task<GuildExpeditionObjectiveResult> SubmitObjectiveResultAsync(
                string objectiveId, System.Threading.CancellationToken cancellationToken) =>
                Task.FromResult(new GuildExpeditionObjectiveResult { success = true });

            public Task<GuildExpeditionMilestoneResult> ClaimMilestoneAsync(
                int threshold, System.Threading.CancellationToken cancellationToken) =>
                Task.FromResult(new GuildExpeditionMilestoneResult { success = true, threshold = threshold });
        }
    }
}
