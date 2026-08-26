using MyriadOfDragons.Story;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// LOCKED 2026-08-26: ST Chapter 3-18 continuity dialogue packet — verbatim replace targets.
    /// Asserts replacement (not append): sequence is exactly two lines containing the locked text,
    /// with no leftover enemy/combat-flavour first line.
    /// </summary>
    public class ContinuityDialoguePacketTests
    {
        private static readonly (string key, string line0Snippet, string line1Snippet, string line0SpeakerId, string line1DisplayContains)[] Beats =
        {
            ("3-30_post", "dead-star road is closing", "hold Olympus, Rusk secures", "ione", null),
            ("4-1_pre", "doubled back through Boiotia", "dead-star road came back", "rusk", null),
            ("4-30_post", "Ione's last signal", "coast will teach", "rusk", "Unknown Voice"),
            ("5-15_pre", "live beside the rubble", "without making its people our enemy", "rusk", null),
            ("5-30_post", "do not claim the coast", "Heights make possession", "player", "Unknown Voice"),
            ("6-30_post", "liberation from becoming occupation", "willing to enter alone", "rusk", "Unknown Voice"),
            ("7-1_pre", "Cross this gate as conqueror", "coalition's commander", "thaleia", null),
            ("7-30_pre", "claim to rule", "no throne decides", "thaleia", null),
            ("7-30_post", "voice crosses this threshold", "Gates obey authority", "ione", "Unknown Voice"),
            ("8-15_pre", "Apollo's court deserves defeat", "break the court's command", "thaleia", null),
            ("9-15_pre", "permanent authority", "not her throne", "thaleia", null),
            ("10-15_pre", "build no new throne", "promise proves its worth", "thaleia", null),
            ("10-30_post", "left the throne empty", "abandoned storm", "ione", "Unknown Voice"),
            ("11-1_pre", "Custody becomes possession", "until it is safe", "ione", null),
            ("11-15_pre", "without counsel", "answer for what I chose", "ione", null),
            ("11-30_post", "not under my crown", "surviving its power with deserving it", "player", null),
            ("12-1_pre", "swear themselves to you", "shared command", "rusk", null),
            ("12-15_pre", "Host Court offers obedience", "cannot legitimise", "rusk", null),
            ("12-30_post", "surrender command when that mandate ends", "soldiers can hear", "player", null),
            ("13-1_pre", "throne disguised as a document", "through witnesses", "thaleia", null),
            ("13-15_pre", "decree binding before anyone hears", "evidence speaks first", "thaleia", null),
            ("13-30_post", "First Witness archive", "wants us to reach that conclusion", "thaleia", null),
            ("14-1_pre", "pantheon is falling", "decide together", "unknown_voice", "Unknown Voice"),
            ("14-15_pre", "archive names you", "I am Eryx", "ione", "Unknown Voice"),
            ("14-30_post", "One sovereign can end it", "will not accept your answer", "unknown_voice", "Eryx, the First Witness"),
            ("15-1_pre", "lawful writ", "godless dawn together", "rusk", null),
            ("15-15_pre", "coronation written in smaller letters", "only what this crisis requires", "thaleia", null),
            ("15-30_post", "Record the limit and the reason", "trust without proof", "player", null),
            ("16-1_pre", "crown is hollow", "no right beyond the mandate", "unknown_voice", "Eryx, the First Witness"),
            ("16-15_pre", "symbol is already commanding", "will not leave it for Eryx", "rusk", null),
            ("16-30_post", "Hollow Crown comes with me", "crossed the line we drew together", "player", null),
            ("17-1_pre", "None waited for the coalition", "protect the settlements", "rusk", null),
            ("17-15_pre", "collecting tribute", "does not deserve to survive", "rusk", null),
            ("17-30_post", "every claim a sovereign needs", "every borrowed power is returned", "ione", null),
            ("18-1_pre", "Only the seat remains", "stop your design", "unknown_voice", "Eryx, the First Witness"),
            ("18-15_pre", "commander's willing claim", "build it for me", "ione", "Eryx, the First Witness"),
            ("18-30_pre", "bind every oath", "stand without me", "unknown_voice", "Eryx, the First Witness"),
            ("18-30_post", "every oath you gathered will eventually fracture", "Your design ends here", "unknown_voice", "Eryx, the First Witness"),
        };

        [Test]
        public void ContinuityPacket_ReplacesListedSequences_VerbatimTwoLineScenes()
        {
            foreach (var (key, line0Snippet, line1Snippet, line0SpeakerId, line1DisplayContains) in Beats)
            {
                StorySequence seq = StoryDatabase.GetSequence(key);
                Assert.IsNotNull(seq, $"Expected continuity key '{key}'.");
                Assert.AreEqual(2, seq.lines.Count,
                    $"'{key}' must be a full replace (exactly two lines), not an append onto enemy flavour.");

                DialogueLine a = seq.lines[0];
                DialogueLine b = seq.lines[1];
                Assert.AreEqual(line0SpeakerId, a.speaker.speakerId, $"{key} line0 speaker");
                StringAssert.Contains(line0Snippet, a.text);
                StringAssert.Contains(line1Snippet, b.text);

                if (line1DisplayContains != null)
                {
                    // For keys where line0 is also the named display (14-1 UV opener), check the
                    // relevant speaker display on whichever line carries that voice.
                    bool onLine0 = a.speaker.displayName != null &&
                        a.speaker.displayName.IndexOf(line1DisplayContains, System.StringComparison.Ordinal) >= 0;
                    bool onLine1 = b.speaker.displayName != null &&
                        b.speaker.displayName.IndexOf(line1DisplayContains, System.StringComparison.Ordinal) >= 0;
                    Assert.IsTrue(onLine0 || onLine1,
                        $"{key} should surface displayName containing '{line1DisplayContains}'.");
                }
            }
        }

        [Test]
        public void AfterEryxReveal_UnknownVoiceDisplayBecomesFirstWitness_PriorHooksStayUnknown()
        {
            StorySequence reveal = StoryDatabase.GetSequence("14-15_pre");
            Assert.AreEqual("Unknown Voice", reveal.lines[1].speaker.displayName);

            StorySequence postReveal = StoryDatabase.GetSequence("14-30_post");
            Assert.AreEqual("Eryx, the First Witness", postReveal.lines[0].speaker.displayName);
            Assert.AreEqual("unknown_voice", postReveal.lines[0].speaker.speakerId);

            StorySequence earlyHook = StoryDatabase.GetSequence("4-30_post");
            Assert.AreEqual("Unknown Voice", earlyHook.lines[1].speaker.displayName);
        }
    }
}
