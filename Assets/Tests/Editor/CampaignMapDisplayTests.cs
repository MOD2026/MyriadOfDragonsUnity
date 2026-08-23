using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    public class CampaignMapDisplayTests
    {
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
    }
}
