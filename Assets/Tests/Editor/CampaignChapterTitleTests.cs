using MyriadOfDragons.UI;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Block T — Campaign map chapter banners for Ch3–9 use approved names (not generic CHAPTER N).
    /// Sources: Command Centre Block T list; aligned with StoryDatabase / CampaignMapPresenter
    /// chapter kits (Deep Ash, Ember Spine, Ash Coast, Climb, Beyond the Outer Gate,
    /// Crown of Storms, Aegis Citadel). Ch1/2/10 strings remain unchanged.
    /// </summary>
    public class CampaignChapterTitleTests
    {
        [Test]
        public void GetChapterTitle_Ch1_Ch2_Ch10_Unchanged()
        {
            Assert.AreEqual("CHAPTER 1: THE ORC INVASION", CampaignMapPresenter.GetChapterTitleForTests(1));
            Assert.AreEqual("CHAPTER 2: ASHES OF BOIOTIA", CampaignMapPresenter.GetChapterTitleForTests(2));
            Assert.AreEqual("CHAPTER 10: THE EMPTY THRONE", CampaignMapPresenter.GetChapterTitleForTests(10));
        }

        [Test]
        public void GetChapterTitle_Ch3ThroughCh9_UseApprovedNamedTitles_NotGeneric()
        {
            Assert.AreEqual("CHAPTER 3: THE DEEP ASH", CampaignMapPresenter.GetChapterTitleForTests(3));
            Assert.AreEqual("CHAPTER 4: EMBER SPINE", CampaignMapPresenter.GetChapterTitleForTests(4));
            Assert.AreEqual("CHAPTER 5: THE ASH COAST", CampaignMapPresenter.GetChapterTitleForTests(5));
            Assert.AreEqual("CHAPTER 6: THE CLIMB", CampaignMapPresenter.GetChapterTitleForTests(6));
            Assert.AreEqual("CHAPTER 7: BEYOND THE OUTER GATE", CampaignMapPresenter.GetChapterTitleForTests(7));
            Assert.AreEqual("CHAPTER 8: CROWN OF STORMS", CampaignMapPresenter.GetChapterTitleForTests(8));
            Assert.AreEqual("CHAPTER 9: THE AEGIS CITADEL", CampaignMapPresenter.GetChapterTitleForTests(9));

            for (int chapter = 3; chapter <= 9; chapter++)
            {
                string title = CampaignMapPresenter.GetChapterTitleForTests(chapter);
                Assert.AreNotEqual($"CHAPTER {chapter}", title,
                    $"Chapter {chapter} must not fall through to the generic CHAPTER N banner.");
                Assert.IsTrue(title.StartsWith($"CHAPTER {chapter}: "),
                    $"Chapter {chapter} banner must keep CHAPTER N: prefix style.");
            }
        }
    }
}
