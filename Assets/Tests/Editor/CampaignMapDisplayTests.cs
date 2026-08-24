using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class CampaignMapDisplayTests
    {
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsCampaignDisplay_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        [Test]
        public void FreshProfile_ShowsChapterOneStagesOnly()
        {
            var profile = new PlayerProfile();
            Assert.AreEqual(1, CampaignMapPresenter.ResolveDisplayChapterForTests(profile));

            var chapterOne = CampaignMapPresenter.GetStageForTests("1-1");
            Assert.NotNull(chapterOne);
            Assert.AreEqual(12, CountChapterStages(1), "Chapter 1 should expose its own stage row, not all 273 nodes.");
        }

        // Scroll/MVP node rendering: see CampaignMvpProgressionTests.CampaignMap_RendersMvpNodeCount_NotFullChapter

        private static int CountChapterStages(int chapter)
        {
            int count = 0;
            for (int i = 1; i <= 30; i++)
            {
                if (CampaignMapPresenter.GetStageForTests($"{chapter}-{i}") != null)
                    count++;
            }

            return count;
        }

        [Test]
        public void CampaignMapV1Pack_IsPresentAndMockupIsNotARuntimeSprite()
        {
            Assert.IsTrue(CampaignMapUiLibrary.HasCampaignMapV1Pack,
                "Campaign Map V1 must include path backdrop, modal chrome, and 3 sliced node states.");
            Assert.IsNull(Resources.Load<Sprite>("UI/CampaignMapV1/campaign_map_landscape_mockup_v1"),
                "Reference mockup must not be imported as a live screen sprite.");
        }

        [Test]
        public void CampaignMap_UsesPathBackdropAndThreeStateNodeSprites()
        {
            var profile = new PlayerProfile
            {
                unlockedStageIds = new List<string> { "1-1" },
                claimedStageRewardIds = new List<string>(),
            };
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var go = new GameObject("CampaignArtHarness");
            var map = go.AddComponent<CampaignMapPresenter>();
            map.Initialize(null, _ => CampaignLaunchOutcome.BlockedLocked);

            GameObject canvas = GameObject.Find("CampaignMapCanvas");
            Assert.IsNotNull(canvas);

            Image backdrop = canvas.transform.Find("MapBackdrop")?.GetComponent<Image>();
            Assert.IsNotNull(backdrop);
            Assert.AreEqual(CampaignMapUiLibrary.PathBackdropName, backdrop.sprite.name);
            Assert.IsFalse(backdrop.raycastTarget);
            Assert.IsTrue(backdrop.preserveAspect);

            Transform playable = canvas.transform.Find("StageScrollView/Viewport/StageNodesContent/StageNode_1-1");
            Assert.IsNotNull(playable);
            Image playableImg = playable.GetComponent<Image>();
            Assert.AreEqual(CampaignMapUiLibrary.LoadNodeSprite(CampaignStageNodeVisualState.Playable), playableImg.sprite);
            Assert.IsTrue(playable.GetComponent<Button>().interactable);

            Transform locked = canvas.transform.Find("StageScrollView/Viewport/StageNodesContent/StageNode_1-2");
            Assert.IsNotNull(locked);
            Image lockedImg = locked.GetComponent<Image>();
            Assert.AreEqual(CampaignMapUiLibrary.LoadNodeSprite(CampaignStageNodeVisualState.Locked), lockedImg.sprite);
            Assert.IsFalse(locked.GetComponent<Button>().interactable, "Locked nodes must not expose a playable action.");

            map.OpenStageDetailsForTests(CampaignMapPresenter.GetStageForTests("1-1"));
            Transform panel = canvas.transform.Find("StageDetailModal/DetailPanel");
            Assert.IsNotNull(panel);
            Image chrome = panel.Find("ModalChrome")?.GetComponent<Image>();
            Assert.IsNotNull(chrome);
            Assert.AreEqual(CampaignMapUiLibrary.ModalChromeName, chrome.sprite.name);
            Assert.IsFalse(chrome.raycastTarget, "Modal chrome is decorative; Launch/Close own hit targets.");
            Assert.IsTrue(chrome.preserveAspect);

            Assert.IsNotNull(panel.Find("Btn_Launch"));
            Assert.IsTrue(panel.Find("Btn_Launch").gameObject.activeSelf);
            Assert.IsNotNull(panel.Find("Title")?.GetComponent<Text>());
            Assert.IsFalse(string.IsNullOrEmpty(panel.Find("Title").GetComponent<Text>().text));

            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(go);
        }
    }
}
