using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Working-area composition for screens that have no V3/V4 handoff table (Empire, Avatar,
    /// Settings, Expedition). Battle/Formation stay on BattleReleaseLayoutTests vs the V4 anchors.
    /// Shop and Campaign map are excluded — pending GPT art.
    /// </summary>
    public class MetagameWorkingAreaLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsWorkingArea_" + System.Guid.NewGuid().ToString("N"));
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
        public void Empire_ConstructionRoot_FillsBelowHeader_AndBuildingRowsDoNotOverlapQueue()
        {
            var go = new GameObject("EmpireWorkingAreaHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<EmpirePresenter>();
            presenter.Initialize(onBackToHome: null, onOpenAvatar: () => { });
            PrepareCanvas(presenter.CanvasObjectForTests);

            RectTransform header = (RectTransform)presenter.CanvasObjectForTests.transform.Find("EmpireHeader");
            RectTransform root = (RectTransform)presenter.CanvasObjectForTests.transform.Find("EmpireConstructionRoot");
            Assert.NotNull(header);
            Assert.NotNull(root);

            Rect headerBounds = WorldBounds(header);
            Rect rootBounds = WorldBounds(root);
            Assert.Greater(rootBounds.height, 850f,
                "Construction working area must use the space under the header, not a floating 0.12–0.82 island.");
            Assert.Greater(rootBounds.yMax + 1f, headerBounds.yMin - 40f,
                "Construction panel top must sit just under the header, not leave a decorative gap.");

            string[] regions =
            {
                "EmpireConstructionRoot/VariantStrip",
                "EmpireConstructionRoot/CastleRow",
                "EmpireConstructionRoot/BarracksRow",
                "EmpireConstructionRoot/GateRow",
                "EmpireConstructionRoot/EmpireStatus",
                "EmpireConstructionRoot/ActiveProjectDetail",
                "EmpireConstructionRoot/CollectConstructionButton",
            };
            AssertNoPairOverlaps(presenter.CanvasObjectForTests.transform, regions);
        }

        [Test]
        public void Avatar_BodyFillsWorkingArea_AndDoesNotReserveEmptyCrestWell()
        {
            var go = new GameObject("AvatarWorkingAreaHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<AvatarPresenter>();
            presenter.Initialize(onBackToHome: null, onOpenEmpire: () => { });
            PrepareCanvas(presenter.CanvasObjectForTests);

            RectTransform body = (RectTransform)presenter.CanvasObjectForTests.transform.Find("AvatarBody");
            Assert.NotNull(body);
            Rect bodyBounds = WorldBounds(body);
            Assert.Greater(bodyBounds.width, 1700f, "Avatar body must use the working width, not a 0.12–0.88 island.");
            Assert.Greater(bodyBounds.height, 850f, "Avatar body must fill under the header.");
            Assert.IsNull(presenter.CanvasObjectForTests.transform.Find("AvatarBody/Crest"),
                "Empty unsprited crest well is dead space — combat copy uses that column instead.");
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("AvatarBody/CombatStats"));
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("AvatarBody/Btn_OpenEmpire"));
        }

        [Test]
        public void Settings_BodyUsesWorkingWidth_AndRowLabelDoesNotOverlapValue()
        {
            var go = new GameObject("SettingsWorkingAreaHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<SettingsPresenter>();
            presenter.Initialize(onBackToHome: null);
            PrepareCanvas(presenter.CanvasObjectForTests);

            RectTransform body = (RectTransform)presenter.CanvasObjectForTests.transform.Find("SettingsBody");
            Assert.NotNull(body);
            Assert.Greater(WorldBounds(body).width, 1600f,
                "Settings must not sit in a decorative 0.22–0.78 centre panel.");

            foreach (string row in new[] { "AudioRow", "NotificationsRow", "LanguageRow" })
            {
                Transform rowTf = presenter.CanvasObjectForTests.transform.Find("SettingsBody/" + row);
                Assert.NotNull(rowTf);
                RectTransform label = (RectTransform)rowTf.Find("Label");
                RectTransform value = (RectTransform)rowTf.Find("Value");
                Assert.NotNull(label);
                Assert.NotNull(value);
                Assert.IsFalse(Inset(WorldBounds(label), 2f).Overlaps(Inset(WorldBounds(value), 2f)),
                    $"{row} label must not overlap its value.");
            }
        }

        [Test]
        public void Expedition_StageScrollUsesLowerBand_AndNodeCopyDoesNotStack()
        {
            var go = new GameObject("ExpeditionWorkingAreaHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<EmpireExpeditionPresenter>();
            presenter.Initialize(onBack: null);
            PrepareCanvas(presenter.CanvasObjectForTests);

            RectTransform scroll = (RectTransform)presenter.CanvasObjectForTests.transform.Find("StageScrollView");
            Assert.NotNull(scroll);
            Rect scrollBounds = WorldBounds(scroll);
            Assert.Less(scrollBounds.yMin, 80f, "Stage list must use the lower working band, not stop at y=0.18.");
            Assert.Greater(scrollBounds.height, 700f);

            Transform node = presenter.CanvasObjectForTests.transform
                .Find("StageScrollView/Viewport/StageNodesContent/StageNode_exp-1");
            Assert.NotNull(node);
            AssertNoPairOverlaps(node, new[] { "StageId", "Title", "Hint" });
        }

        [Test]
        public void BattlePass_TrackTableFillsShell_AndSeasonXpLabelDoesNotOverlapBar()
        {
            var go = new GameObject("BattlePassWorkingAreaHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<BattlePassPresenter>();
            presenter.Initialize(onBackToHome: null);
            PrepareCanvas(presenter.CanvasObjectForTests);

            RectTransform tracks = (RectTransform)presenter.CanvasObjectForTests.transform.Find("TrackTable");
            Assert.NotNull(tracks);
            Rect trackBounds = WorldBounds(tracks);
            Assert.Greater(trackBounds.width, 1600f, "Dual-track table must use the shell width, not a centre island.");
            Assert.Greater(trackBounds.height, 500f, "Dual-track table must use the mid-screen well.");

            RectTransform xpLabel = (RectTransform)presenter.CanvasObjectForTests.transform.Find("SeasonXpRow/Label");
            RectTransform xpBar = (RectTransform)presenter.CanvasObjectForTests.transform.Find("SeasonXpRow/XpBar");
            Assert.IsFalse(Inset(WorldBounds(xpLabel), 2f).Overlaps(Inset(WorldBounds(xpBar), 2f)));
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("PremiumBar/Btn_UnlockPremium"));
        }

        [Test]
        public void DailyLogin_BothPanelsUseWorkingColumns_AndQuestCopyDoesNotOverlapClaim()
        {
            var go = new GameObject("DailyLoginWorkingAreaHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<DailyLoginQuestsPresenter>();
            presenter.Initialize(onBackToHome: null);
            PrepareCanvas(presenter.CanvasObjectForTests);

            RectTransform login = (RectTransform)presenter.CanvasObjectForTests.transform.Find("DailyLoginPanel");
            RectTransform quests = (RectTransform)presenter.CanvasObjectForTests.transform.Find("DailyQuestsPanel");
            Assert.Greater(WorldBounds(login).height, 800f);
            Assert.Greater(WorldBounds(quests).height, 800f);
            Assert.Greater(WorldBounds(login).width + WorldBounds(quests).width, 1500f);

            Transform row = presenter.CanvasObjectForTests.transform.Find("DailyQuestsPanel/QuestRow_0");
            AssertNoPairOverlaps(row, new[] { "QuestCopy", "Btn_Claim" });
        }

        [Test]
        public void BuildingDetail_PanelFillsOverlay_AndReturnDoesNotOverlapTitle()
        {
            var go = new GameObject("BuildingDetailWorkingAreaHarness");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null, onOpenAvatar: () => { });
            empire.OpenBuildingDetailForTests(EmpireBuildingKind.Castle);
            GameObject canvas = GameObject.Find(EmpireBuildingDetailPresenter.CanvasName);
            PrepareCanvas(canvas);

            RectTransform panel = (RectTransform)canvas.transform.Find("DetailPanel");
            Assert.Greater(WorldBounds(panel).width, 1500f, "Detail popup must use the overlay, not a small centre card.");
            Assert.Greater(WorldBounds(panel).height, 800f);
            AssertNoPairOverlaps(canvas.transform.Find("DetailPanel/DetailHeader"),
                new[] { "Btn_Return", "Title", "Btn_Close" });
        }

        [Test]
        public void Home_PassAndLogin_DoNotCoverResourcePills()
        {
            var go = new GameObject("HomeSeasonHudHarness");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            PrepareCanvas(home.HomeCanvasObjectForTests);

            Rect pills = WorldBounds((RectTransform)home.HomeCanvasObjectForTests.transform.Find("ResourceRow"));
            Rect pass = WorldBounds((RectTransform)home.HomeCanvasObjectForTests.transform.Find("Btn_BattlePass"));
            Rect login = WorldBounds((RectTransform)home.HomeCanvasObjectForTests.transform.Find("Btn_DailyLogin"));
            Rect settings = WorldBounds((RectTransform)home.HomeCanvasObjectForTests.transform.Find("Btn_Settings"));
            Assert.IsFalse(Inset(pills, 2f).Overlaps(Inset(pass, 2f)), "PASS must not sit on Gold/Gems/Stamina pills.");
            Assert.IsFalse(Inset(pills, 2f).Overlaps(Inset(login, 2f)), "LOGIN must not sit on resource pills.");
            Assert.IsFalse(Inset(pills, 2f).Overlaps(Inset(settings, 2f)));
        }

        private static void PrepareCanvas(GameObject canvasGo)
        {
            Assert.NotNull(canvasGo);
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
        }

        private static void AssertNoPairOverlaps(Transform root, string[] relativePaths)
        {
            var bounds = new Dictionary<string, Rect>();
            foreach (string path in relativePaths)
            {
                Transform target = root.Find(path);
                Assert.NotNull(target, $"Missing '{path}'.");
                bounds[path] = Inset(WorldBounds((RectTransform)target), 2f);
            }

            string[] keys = bounds.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                for (int j = i + 1; j < keys.Length; j++)
                {
                    Assert.IsFalse(bounds[keys[i]].Overlaps(bounds[keys[j]]),
                        $"'{keys[i]}' must not overlap '{keys[j]}' ({bounds[keys[i]]} vs {bounds[keys[j]]}).");
                }
            }
        }

        private static Rect WorldBounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float xMin = corners.Min(c => c.x);
            float xMax = corners.Max(c => c.x);
            float yMin = corners.Min(c => c.y);
            float yMax = corners.Max(c => c.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static Rect Inset(Rect rect, float pad)
        {
            return new Rect(rect.x + pad, rect.y + pad,
                Mathf.Max(0f, rect.width - pad * 2f), Mathf.Max(0f, rect.height - pad * 2f));
        }
    }
}
