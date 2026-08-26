using MyriadOfDragons.Story;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Ch4-7 beat locks, updated 2026-08-26 for the ST continuity packet.
    /// Untouched midpoints/finales keep the earlier Ch4-7 narrative-upgrade snippets;
    /// continuity-replaced keys assert the new verbatim lines instead.
    /// </summary>
    public class Chapter4To7NarrativeUpgradeTests
    {
        private static readonly (int chapter, string openerSnippet, string midpointSnippet, string finaleSnippet, string postHookSnippet)[] Chapters =
        {
            (4, "Ashfall reserve", "not soft", "ash ends", "coast will teach"),
            (5, "neither", "live beside the rubble", "sea can wait", "Heights make possession"),
            (6, "soft feet were never", "Let it watch", "take the view", "willing to enter alone"),
            (7, "Cross this gate as conqueror", "remember one more", "no throne decides", "Gates obey authority"),
        };

        [Test]
        public void RequiredBeats_ExistWithBespokeLinesAndUnknownVoicePostFinaleHooks()
        {
            foreach (var (chapter, openerSnippet, midpointSnippet, finaleSnippet, postHookSnippet) in Chapters)
            {
                AssertBeat($"{chapter}-1_pre", openerSnippet, expectedSpeakerId: null);
                AssertBeat($"{chapter}-15_pre", midpointSnippet, expectedSpeakerId: null);
                AssertBeat($"{chapter}-30_pre", finaleSnippet, expectedSpeakerId: null);

                StorySequence post = StoryDatabase.GetSequence($"{chapter}-30_post");
                Assert.IsNotNull(post, $"Expected {chapter}-30_post");
                Assert.GreaterOrEqual(post.lines.Count, 2, $"{chapter}-30_post should be a two-line post-finale hook");

                DialogueLine hook = post.lines[post.lines.Count - 1];
                Assert.AreEqual("unknown_voice", hook.speaker.speakerId, $"{chapter}-30_post last line must be Unknown Voice");
                StringAssert.Contains(postHookSnippet, hook.text);
            }
        }

        private static void AssertBeat(string key, string snippet, string expectedSpeakerId)
        {
            StorySequence seq = StoryDatabase.GetSequence(key);
            Assert.IsNotNull(seq, $"Expected StoryDatabase entry '{key}'.");
            Assert.Greater(seq.lines.Count, 0, $"'{key}' must have dialogue lines.");

            bool found = false;
            foreach (DialogueLine line in seq.lines)
            {
                if (line.text != null && line.text.IndexOf(snippet, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = true;
                    if (expectedSpeakerId != null)
                        Assert.AreEqual(expectedSpeakerId, line.speaker.speakerId);
                    break;
                }
            }

            Assert.IsTrue(found, $"Expected '{key}' to contain bespoke snippet '{snippet}'.");
        }
    }
}
