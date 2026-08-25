using NUnit.Framework;
using MyriadOfDragons.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Per-chapter battle backdrop selection. Added 2026-08-25 - every match used a single
    /// hardcoded arena (Lava_Fortress) regardless of chapter, which is the real gap behind wanting
    /// different battle screens per chapter/event. This is the SELECTION MECHANISM only - which
    /// arena actually suits which chapter's story is a real content decision, not tested or
    /// invented here. All 10 real files under Resources/UI/Backdrops/Arenas/ are used in the
    /// rotation so nothing sits unused.
    /// </summary>
    public class BattleBackdropSelectionTests
    {
        [Test]
        public void SameChapter_AlwaysResolvesToTheSameArena()
        {
            string first = GameBootstrap.ArenaBackdropNameForTests("3-5");
            string second = GameBootstrap.ArenaBackdropNameForTests("3-17");
            Assert.AreEqual(first, second,
                "The same chapter must always pick the same arena - stable per chapter, not per stage or per match.");
        }

        [Test]
        public void NullOrUnparseableStageId_FallsBackToTheOriginalFixedArena()
        {
            // Non-campaign matches (_pendingCampaignStage null - "To Battle", PvP) must keep the
            // exact prior behaviour rather than silently aliasing to some chapter's arena.
            Assert.AreEqual("Lava_Fortress", GameBootstrap.ArenaBackdropNameForTests(null));
            Assert.AreEqual("Lava_Fortress", GameBootstrap.ArenaBackdropNameForTests(""));
            Assert.AreEqual("Lava_Fortress", GameBootstrap.ArenaBackdropNameForTests("not-a-number"));
        }

        [Test]
        public void ChapterNumberParsing_ReadsTheLeadingIntegerBeforeTheDash()
        {
            Assert.AreEqual(16, GameBootstrap.ChapterNumberForStageIdForTests("16-24"));
            Assert.AreEqual(1, GameBootstrap.ChapterNumberForStageIdForTests("1-1"));
            Assert.IsNull(GameBootstrap.ChapterNumberForStageIdForTests(null));
            Assert.IsNull(GameBootstrap.ChapterNumberForStageIdForTests(""));
        }

        [Test]
        public void EveryRealArenaFile_IsReachableBySomeChapter()
        {
            // Guards against a typo silently orphaning one of the real files - if a name in the
            // rotation array doesn't match a real asset, this test can't catch that directly (it
            // only checks the rotation logic), but it does prove the rotation actually cycles
            // through all 10 rather than collapsing onto a smaller set.
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int chapter = 1; chapter <= 20; chapter++)
                seen.Add(GameBootstrap.ArenaBackdropNameForTests(chapter + "-1"));

            Assert.AreEqual(10, seen.Count,
                "20 chapters over a 10-arena rotation must reach all 10 distinct names.");
        }
    }
}
