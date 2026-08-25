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
        public void CuratedChapters_MatchTheStoryBenchmarkedMapping()
        {
            // Spot-checks BS's 2026-08-25 curated table (docs/LOCKED_DECISIONS_REGISTER.md) - the
            // real content decision for chapters 1-18, not invented here.
            Assert.AreEqual("Castle_Valley", GameBootstrap.ArenaBackdropNameForTests("1-1"));
            Assert.AreEqual("Frozen_Citadel", GameBootstrap.ArenaBackdropNameForTests("9-3"));
            Assert.AreEqual("Infernal_Hellscape", GameBootstrap.ArenaBackdropNameForTests("17-5"));
            Assert.AreEqual("Haunted_Citadel", GameBootstrap.ArenaBackdropNameForTests("18-1"));
        }

        [Test]
        public void CuratedChapters_DeliberatelyNeverUseTheReservedForestArena()
        {
            // BS explicitly flagged Enchanted_Forest as the weakest fit for chapters 1-18 and
            // reserved it rather than forcing it onto a chapter it doesn't suit - so it must not
            // appear anywhere in the curated range, only in the fallback rotation beyond it.
            for (int chapter = 1; chapter <= 18; chapter++)
                Assert.AreNotEqual("Enchanted_Forest", GameBootstrap.ArenaBackdropNameForTests(chapter + "-1"),
                    $"Chapter {chapter} must not use the reserved Enchanted_Forest arena.");
        }

        [Test]
        public void ChaptersBeyondTheCuratedTable_FallBackToTheDeterministicRotation()
        {
            // Chapters 19-28 is exactly one full cycle of the 10-arena modulo rotation (indices
            // (chapter-1)%10 run 8,9,0,1,...,7), proving the fallback still reaches every real
            // arena file - including the reserved Enchanted_Forest - once a chapter isn't in the
            // curated table, without forcing it into the curated story mapping above.
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int chapter = 19; chapter <= 28; chapter++)
                seen.Add(GameBootstrap.ArenaBackdropNameForTests(chapter + "-1"));

            Assert.AreEqual(10, seen.Count,
                "10 consecutive post-curated chapters over a 10-arena rotation must reach all 10 distinct names.");
            CollectionAssert.Contains(seen, "Enchanted_Forest",
                "The reserved forest arena must still be reachable once chapters run past the curated table.");
        }
    }
}
