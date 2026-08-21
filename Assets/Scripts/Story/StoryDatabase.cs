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
            var ashOverseer = new StorySpeaker("ash_overseer", "Ash Road Overseer", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var veinWarden = new StorySpeaker("vein_warden", "Vein-Warden Thessos", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var legionCommander = new StorySpeaker("legion_commander", "Legion Commander Ares", "UI/Portraits/Paladin", SpeakerPosition.Right);

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

            // Stage 1-1 / 1-2 / 1-3 post-victory bridges (first clear only - see HomePagePresenter).
            var stage1_1Post = new StorySequence("1-1_post", "Stage 1-1: Path Cleared");
            stage1_1Post.lines.Add(new DialogueLine(orcScout, "Fall back! The border is lost!"));
            stage1_1Post.lines.Add(new DialogueLine(playerSpeaker, "The mountain path is ours. Push on to the Volcanic Ridge."));
            sequences["1-1_post"] = stage1_1Post;

            var stage1_2Post = new StorySequence("1-2_post", "Stage 1-2: Ridge Secured");
            stage1_2Post.lines.Add(new DialogueLine(kaelen, "My wyverns... broken. Gorn will not forgive this."));
            stage1_2Post.lines.Add(new DialogueLine(playerSpeaker, "Then we take the fight to his citadel before he recovers."));
            sequences["1-2_post"] = stage1_2Post;

            var stage1_3Post = new StorySequence("1-3_post", "Stage 1-3: Citadel Falls");
            stage1_3Post.lines.Add(new DialogueLine(gorn, "The citadel... falls. Olympus will notice this wound."));
            stage1_3Post.lines.Add(new DialogueLine(playerSpeaker, "Let them. Chapter one ends here - but the war for Boiotia has only begun."));
            sequences["1-3_post"] = stage1_3Post;

            // Chapter 2 "Ashes of Boiotia" (2026-08-22) - Stage 2-1/2-2/2-3 pre/post-battle
            // dialogue, same short two-line pattern every Chapter 1 stage already uses.

            var stage2_1 = new StorySequence("2-1_pre", "Stage 2-1: Ashfall Outpost");
            stage2_1.lines.Add(new DialogueLine(ashOverseer, "The vein burns for Olympus now, Sovereign. Turn back or choke on the ash."));
            stage2_1.lines.Add(new DialogueLine(playerSpeaker, "Boiotia does not choke. Clear the outpost - the vein is ours to free."));
            sequences["2-1_pre"] = stage2_1;

            var stage2_2 = new StorySequence("2-2_pre", "Stage 2-2: Titan-Vein Camp");
            stage2_2.lines.Add(new DialogueLine(veinWarden, "You freed the outpost. The camp itself will not fall so easily."));
            stage2_2.lines.Add(new DialogueLine(playerSpeaker, "Every miner you chained is another reason we don't stop here, Warden."));
            sequences["2-2_pre"] = stage2_2;

            var stage2_3 = new StorySequence("2-3_pre", "Stage 2-3: Legion of Ash");
            stage2_3.lines.Add(new DialogueLine(legionCommander, "Olympus sent its Ashfall Legion to end this rebellion. It ends with you."));
            stage2_3.lines.Add(new DialogueLine(playerSpeaker, "Then let Olympus watch its legion break on Boiotian ground."));
            sequences["2-3_pre"] = stage2_3;

            var stage2_1Post = new StorySequence("2-1_post", "Stage 2-1: Outpost Cleared");
            stage2_1Post.lines.Add(new DialogueLine(ashOverseer, "Fall back to the camp! Warn the Warden!"));
            stage2_1Post.lines.Add(new DialogueLine(playerSpeaker, "The outer ash road is ours. Now for the vein itself."));
            sequences["2-1_post"] = stage2_1Post;

            var stage2_2Post = new StorySequence("2-2_post", "Stage 2-2: Vein Freed");
            stage2_2Post.lines.Add(new DialogueLine(veinWarden, "The miners... turn on their chains. This vein is lost to Olympus."));
            stage2_2Post.lines.Add(new DialogueLine(playerSpeaker, "Free, and armed. The legion marches next - let it come."));
            sequences["2-2_post"] = stage2_2Post;

            var stage2_3Post = new StorySequence("2-3_post", "Stage 2-3: Legion Broken");
            stage2_3Post.lines.Add(new DialogueLine(legionCommander, "Impossible. The Ashfall Legion does not break before mortals."));
            stage2_3Post.lines.Add(new DialogueLine(playerSpeaker, "It just did. Chapter two ends here - Olympus will send worse next."));
            sequences["2-3_post"] = stage2_3Post;
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