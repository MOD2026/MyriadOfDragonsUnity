using System.Collections.Generic;

namespace MyriadOfDragons.Story
{
    public static class StoryDatabase
    {
        private static readonly Dictionary<string, StorySequence> sequences = new Dictionary<string, StorySequence>();

        // The one recurring speaker every stage's dialogue uses for the player's own line - a
        // static field (not a local inside InitializeDatabase) so AddStageDialogue's light
        // template below can reach it without every call site re-passing it.
        private static readonly StorySpeaker playerSpeaker = new StorySpeaker("player", "Sovereign", "UI/Portraits/Paladin", SpeakerPosition.Left);

        static StoryDatabase()
        {
            InitializeDatabase();
        }

        /// <summary>Light template (2026-08-22, Chapter 1 depth expansion) for a stage whose
        /// dialogue is just "enemy line, player line" before and after the fight - exactly the
        /// shape every existing Chapter 1/2 sequence already uses, without re-typing the
        /// StorySequence/DialogueLine/sequences[...] boilerplate nine more times.</summary>
        private static void AddStageDialogue(string stageId, string title, StorySpeaker enemy,
            string preEnemyLine, string prePlayerLine, string postEnemyLine, string postPlayerLine)
        {
            var pre = new StorySequence($"{stageId}_pre", $"Stage {stageId}: {title}");
            pre.lines.Add(new DialogueLine(enemy, preEnemyLine));
            pre.lines.Add(new DialogueLine(playerSpeaker, prePlayerLine));
            sequences[$"{stageId}_pre"] = pre;

            var post = new StorySequence($"{stageId}_post", $"Stage {stageId}: Cleared");
            post.lines.Add(new DialogueLine(enemy, postEnemyLine));
            post.lines.Add(new DialogueLine(playerSpeaker, postPlayerLine));
            sequences[$"{stageId}_post"] = post;
        }

        private static void InitializeDatabase()
        {
            // Speakers (NPC Avatars & Player)
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

            // Chapter 1 depth expansion, Stages 1-4..1-12 (2026-08-22, owner: "stickiness = many
            // sequential fights") - one speaker per stage (matching that stage's enemyName in
            // CampaignMapPresenter), short two-line pre/post dialogue via the light template above.
            var foothillRaiders = new StorySpeaker("foothill_raiders", "Foothill Raiders", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var bridgeWardens = new StorySpeaker("bridge_wardens", "Bridge Wardens", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var groveCultists = new StorySpeaker("grove_cultists", "Grove Cultists", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var quarryOverseers = new StorySpeaker("quarry_overseers", "Quarry Overseers", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var passMarauders = new StorySpeaker("pass_marauders", "Pass Marauders", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var aqueductGuard = new StorySpeaker("aqueduct_guard", "Aqueduct Guard", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var watchtowerGarrison = new StorySpeaker("watchtower_garrison", "Watchtower Garrison", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var hollowBornVanguard = new StorySpeaker("hollow_born_vanguard", "Hollow-Born Vanguard", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var gatekeeper = new StorySpeaker("gatekeeper", "Gatekeeper of Boiotia", "UI/Portraits/Paladin", SpeakerPosition.Right);

            AddStageDialogue("1-4", "Ashen Foothills", foothillRaiders,
                "Gorn is dead, but his foothills are still ours! You'll not pass unchallenged.",
                "Scatter them. The citadel's fall means nothing while stragglers still bar the road.",
                "Fall back - regroup at the bridge!",
                "The foothills are clear. On to the crossing.");

            AddStageDialogue("1-5", "Sundered Bridge", bridgeWardens,
                "This bridge is the only crossing left standing. You'll not take it from us.",
                "Then we take it anyway. Hold the line and push them off the span!",
                "The bridge... it's yours. May it hold your weight better than it held ours.",
                "It will. Forward, into the grove.");

            AddStageDialogue("1-6", "Whispering Grove", groveCultists,
                "The Warlord's spirit still commands these oaks. Turn back, or feed the roots.",
                "Superstition won't stop steel. Clear the grove.",
                "The oaks... go silent. Our cult dies with them.",
                "Let it. The quarry is next.");

            AddStageDialogue("1-7", "Iron Quarry", quarryOverseers,
                "This quarry feeds what's left of Gorn's war machine. You'll not shut it down.",
                "Every chain you're holding here is a reason to end this quickly.",
                "The overseers... routed. The slaves are free.",
                "Free, and armed with what we find here. Onward to the pass.");

            AddStageDialogue("1-8", "Wolfsbane Pass", passMarauders,
                "Few of us are left, but we hold the only pass north. Come and see how few is enough.",
                "Few or not, you stand between us and the aqueduct. Move.",
                "The pass... falls. There's nothing left to hold it with.",
                "Then hold nothing. We march on.");

            AddStageDialogue("1-9", "Sunken Aqueduct", aqueductGuard,
                "This old aqueduct still moves supplies no one was meant to see. Turn back now.",
                "All the more reason to see it sealed. Clear the guard.",
                "The aqueduct is yours. Whatever moved through it moves no longer.",
                "Good. Now the watchtower stands between us and the hollow.");

            AddStageDialogue("1-10", "Obsidian Watchtower", watchtowerGarrison,
                "This tower still signals for reinforcements that will never come. Try your luck anyway.",
                "Then let's make sure that signal never reaches anyone. Silence it.",
                "The tower falls silent. No one is coming to relieve us.",
                "No one is coming for any of you. Ember Hollow awaits.");

            AddStageDialogue("1-11", "Ember Hollow", hollowBornVanguard,
                "We are what remains of Gorn's true vanguard. We do not break, Sovereign.",
                "Everything breaks eventually. Today, it's your turn.",
                "The vanguard... breaks. Gorn's line ends here, truly.",
                "Then only the gate remains. Boiotia is almost ours.");

            AddStageDialogue("1-12", "Boiotia's Gate", gatekeeper,
                "Beyond this gate lies Boiotia itself - and ash enough to bury an army. You first.",
                "Open it, or we open it for you. Chapter one ends at this gate.",
                "The gate... gives way. Boiotia's ashes are yours to walk through now.",
                "Chapter one ends here - but the war for Boiotia has only begun.");

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