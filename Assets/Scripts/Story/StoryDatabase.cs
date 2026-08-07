using System.Collections.Generic;

namespace MyriadOfDragons.Story
{
    public static class StoryDatabase
    {
        private static readonly Dictionary<string, StorySequence> sequences = new Dictionary<string, StorySequence>();

        static StoryDatabase()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            // Speakers (NPC Avatars & Player)
            var playerSpeaker = new StorySpeaker("player", "Sovereign", "UI/Portraits/Paladin", SpeakerPosition.Left);
            var orcScout = new StorySpeaker("orc_scout", "Orc Scout Patrol", "UI/Portraits/OrcScout", SpeakerPosition.Right);
            var kaelen = new StorySpeaker("kaelen", "Wyvern Tamer Kaelen", "UI/Portraits/Kaelen", SpeakerPosition.Right);
            var gorn = new StorySpeaker("gorn", "High Warlord Gorn", "UI/Portraits/Gorn", SpeakerPosition.Right);

            // Intro Prologue
            var prologue = new StorySequence("intro_prologue", "Prologue: Awakening");
            prologue.lines.Add(new DialogueLine(playerSpeaker, "The border realms are falling into chaos. We must breach the pass."));
            prologue.lines.Add(new DialogueLine(orcScout, "Halt! None shall enter the Volcanic Ridge under High Warlord Gorn's decree!"));
            sequences["intro_prologue"] = prologue;

            // Stage 1-1 Pre-Battle Dialogue
            var stage1_1 = new StorySequence("1-1_pre", "Stage 1-1: Border Patrol Encounter");
            stage1_1.lines.Add(new DialogueLine(orcScout, "You dare trespass on Orc territory? Prepare yourself, human!"));
            stage1_1.lines.Add(new DialogueLine(playerSpeaker, "Clear the mountain path, vanguard! For the Sovereign!"));
            sequences["1-1_pre"] = stage1_1;

            // Stage 1-2 Pre-Battle Dialogue
            var stage1_2 = new StorySequence("1-2_pre", "Stage 1-2: Volcanic Ridge");
            stage1_2.lines.Add(new DialogueLine(kaelen, "My wyverns will burn your troops to ash before you reach the citadel!"));
            stage1_2.lines.Add(new DialogueLine(playerSpeaker, "Form up shielding rows! We strike down the tamer first!"));
            sequences["1-2_pre"] = stage1_2;

            // Stage 1-3 Pre-Battle Dialogue
            var stage1_3 = new StorySequence("1-3_pre", "Stage 1-3: Stronghold Citadel");
            stage1_3.lines.Add(new DialogueLine(gorn, "So you reached my citadel gates... Your journey ends here!"));
            stage1_3.lines.Add(new DialogueLine(playerSpeaker, "Yield, Gorn, or face the full strength of our army!"));
            sequences["1-3_pre"] = stage1_3;
        }

        public static StorySequence GetSequence(string sequenceId)
        {
            if (sequences.TryGetValue(sequenceId, out var seq))
            {
                return seq;
            }
            return null;
        }
    }
}