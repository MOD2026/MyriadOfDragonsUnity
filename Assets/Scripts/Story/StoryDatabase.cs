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
            AddAuthoredStageDialogue(stageId, title,
                new[]
                {
                    new DialogueLine(enemy, preEnemyLine),
                    new DialogueLine(playerSpeaker, prePlayerLine),
                },
                new[]
                {
                    new DialogueLine(enemy, postEnemyLine),
                    new DialogueLine(playerSpeaker, postPlayerLine),
                });
        }

        /// <summary>Authored multi-speaker pre/post for Ch1-3 continuation
        /// (docs/CAMPAIGN_NARRATIVE_CH1_3_2026-08-23.md).</summary>
        private static void AddAuthoredStageDialogue(string stageId, string title,
            DialogueLine[] preLines, DialogueLine[] postLines)
        {
            var pre = new StorySequence($"{stageId}_pre", $"Stage {stageId}: {title}");
            pre.lines.AddRange(preLines);
            sequences[$"{stageId}_pre"] = pre;

            var post = new StorySequence($"{stageId}_post", $"Stage {stageId}: Cleared");
            post.lines.AddRange(postLines);
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

            // Chapter 1 continuation, Stages 1-4..1-12 (docs/CAMPAIGN_NARRATIVE_CH1_3_2026-08-23.md).
            // Recurring cast: Thaleia / Rusk / Ione. Interaction prompts are resolved in the
            // post-dialogue lines (no separate choice UI in Phase 1).
            var thaleia = new StorySpeaker("thaleia", "Thaleia, the Olympus Envoy", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var rusk = new StorySpeaker("rusk", "Rusk Ashrunner", "UI/Portraits/Paladin", SpeakerPosition.Right);
            var ione = new StorySpeaker("ione", "Ione of the Glass Choir", "UI/Portraits/Paladin", SpeakerPosition.Right);

            AddAuthoredStageDialogue("1-4", "Ashen Foothills",
                new[]
                {
                    new DialogueLine(rusk, "Gorn is dead, but his soldiers are not. They are searching the ruins for the signal stone."),
                    new DialogueLine(playerSpeaker, "Then we reach it first."),
                },
                new[]
                {
                    new DialogueLine(rusk, "You chose the ruins. Good. The stone is warm—and marked with an Olympus seal."),
                    new DialogueLine(thaleia, "You were not supposed to see that."),
                });

            AddAuthoredStageDialogue("1-5", "Sundered Bridge",
                new[]
                {
                    new DialogueLine(thaleia, "Gorn's defeat was registered above. Withdraw, and Olympus may forget your name."),
                    new DialogueLine(playerSpeaker, "You came all this way to ask?"),
                },
                new[]
                {
                    new DialogueLine(thaleia, "You challenged it. Then hear this: the wound is widening beneath the old bridge."),
                    new DialogueLine(rusk, "And every raider in the foothills is being driven toward it."),
                });

            AddAuthoredStageDialogue("1-6", "Whispering Grove",
                new[]
                {
                    new DialogueLine(rusk, "The raiders are not attacking for land. They are being herded."),
                    new DialogueLine(playerSpeaker, "By whom?"),
                },
                new[]
                {
                    new DialogueLine(rusk, "You protected the villages. The survivors saw a woman with a cracked halo."),
                    new DialogueLine(thaleia, "That was not my order. Someone is wearing Olympus's authority."),
                });

            AddAuthoredStageDialogue("1-7", "Iron Quarry",
                new[]
                {
                    new DialogueLine(ione, "The glass showed me three fires: one below, one above, and one inside your own ranks."),
                    new DialogueLine(playerSpeaker, "Which fire do we extinguish first?"),
                },
                new[]
                {
                    new DialogueLine(ione, "You trusted your scouts. They found an Olympus relay beneath the bridge."),
                    new DialogueLine(thaleia, "Destroy it, and the signal will become a summons."),
                });

            AddAuthoredStageDialogue("1-8", "Wolfsbane Pass",
                new[]
                {
                    new DialogueLine(thaleia, "The relay is a gate-key. Gorn was never the true target—the valley was."),
                    new DialogueLine(playerSpeaker, "Then why warn me?"),
                },
                new[]
                {
                    new DialogueLine(thaleia, "You demanded surrender. I will not give it—but I will tell you the truth."),
                    new DialogueLine(thaleia, "Olympus is divided, and one faction wants your victory turned into a beacon."),
                });

            AddAuthoredStageDialogue("1-9", "Sunken Aqueduct",
                new[]
                {
                    new DialogueLine(rusk, "The beacon is active. Villages are seeing stars in daylight."),
                    new DialogueLine(playerSpeaker, "Then we cut its power."),
                },
                new[]
                {
                    new DialogueLine(rusk, "You struck the beacon. It broke, but something answered from the mountain."),
                    new DialogueLine(ione, "The answer had a name: the Crown Below."),
                });

            AddAuthoredStageDialogue("1-10", "Obsidian Watchtower",
                new[]
                {
                    new DialogueLine(ione, "The Crown Below was buried before Olympus had a throne."),
                    new DialogueLine(playerSpeaker, "What happens if it wakes?"),
                },
                new[]
                {
                    new DialogueLine(ione, "You descended. The chamber was empty except for Gorn's war-banner."),
                    new DialogueLine(thaleia, "His fall was staged. His last command is still moving armies."),
                });

            AddAuthoredStageDialogue("1-11", "Ember Hollow",
                new[]
                {
                    new DialogueLine(thaleia, "Gorn's surviving host is marching on the summit, carrying the Crown's broken seal."),
                    new DialogueLine(playerSpeaker, "And Olympus?"),
                },
                new[]
                {
                    new DialogueLine(thaleia, "You kept Olympus out. For the first time, the valley stands by its own decision."),
                    new DialogueLine(rusk, "Then let the summit hear it."),
                });

            AddAuthoredStageDialogue("1-12", "Boiotia's Gate",
                new[]
                {
                    new DialogueLine(rusk, "The summit is burning. Gorn's banner flies above the gate, but Gorn is not there."),
                    new DialogueLine(ione, "The Crown Below is speaking through the dead."),
                },
                new[]
                {
                    new DialogueLine(playerSpeaker, "The banner falls. The voice remains."),
                    new DialogueLine(thaleia, "You have wounded Olympus's enemy—and now it knows your name. Chapter Two will not be a rescue. It will be an invasion."),
                });

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

            // Chapter 2 continuation, Stages 2-4..2-21 (docs/CAMPAIGN_NARRATIVE_CH1_3_2026-08-23.md).
            // Same Thaleia / Rusk / Ione cast. Titles match CampaignMapPresenter.Chapter2DepthFlavor.
            AddAuthoredStageDialogue("2-4", "Cinder Watch",
                new[]
                {
                    new DialogueLine(rusk, "The invasion crossed the eastern ridge before dawn."),
                    new DialogueLine(thaleia, "That army bears Olympus colours, but not Olympus orders."),
                },
                new[]
                {
                    new DialogueLine(rusk, "You secured the ridge. The villages still stand—but the invaders now know we are watching."),
                });

            AddAuthoredStageDialogue("2-5", "Sulfur Flats",
                new[]
                {
                    new DialogueLine(ione, "The ash falling from the sky is not ash. It is memory burned into dust."),
                    new DialogueLine(playerSpeaker, "Then someone is burning history."),
                },
                new[]
                {
                    new DialogueLine(ione, "You followed the ash. It led to a sealed Boiotian road beneath the battlefield."),
                });

            AddAuthoredStageDialogue("2-6", "Broken Kiln",
                new[]
                {
                    new DialogueLine(thaleia, "The road leads toward the old city. If it opens, the invasion will bypass every defence."),
                    new DialogueLine(playerSpeaker, "Then the road becomes our battlefield."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "You sealed it. Something on the other side answered with a human voice."),
                });

            AddAuthoredStageDialogue("2-7", "Slagpour Ridge",
                new[]
                {
                    new DialogueLine(rusk, "Refugees are gathering at the river. The enemy is using them as cover."),
                    new DialogueLine(playerSpeaker, "No army hides behind civilians and keeps its honour."),
                },
                new[]
                {
                    new DialogueLine(rusk, "You rescued them. One survivor carried a burned Olympus writ bearing Thaleia's seal."),
                });

            AddAuthoredStageDialogue("2-8", "Charcoal Hollow",
                new[]
                {
                    new DialogueLine(thaleia, "That writ is forged. Someone wants Boiotia to believe Olympus ordered the slaughter."),
                    new DialogueLine(playerSpeaker, "Then find the forger."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "You confronted me. Good. Trust that cannot survive questions is not trust."),
                });

            AddAuthoredStageDialogue("2-9", "Ember Causeway",
                new[]
                {
                    new DialogueLine(ione, "The forged writ was copied from a voice-recording crystal."),
                    new DialogueLine(playerSpeaker, "Where is the original?"),
                },
                new[]
                {
                    new DialogueLine(ione, "The courier spoke one name before the crystal broke: the Ash Regent."),
                });

            AddAuthoredStageDialogue("2-10", "Grey Ash Fields",
                new[]
                {
                    new DialogueLine(rusk, "The Ash Regent is no ruler. It is a title passed between bodies."),
                    new DialogueLine(playerSpeaker, "Then we stop the title, not the body."),
                },
                new[]
                {
                    new DialogueLine(rusk, "You destroyed the records. The bearer escaped—but now cannot inherit the next name."),
                });

            AddAuthoredStageDialogue("2-11", "Titan's Cradle",
                new[]
                {
                    new DialogueLine(thaleia, "The invasion is feeding on Boiotia's old wars. Every burned banner gives it another soldier."),
                    new DialogueLine(playerSpeaker, "Then we deny it the dead."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "You preserved them. Proof matters—but so does knowing what proof can summon."),
                });

            AddAuthoredStageDialogue("2-12", "Smouldering Vault",
                new[]
                {
                    new DialogueLine(ione, "The banners are not symbols. They are anchors."),
                    new DialogueLine(playerSpeaker, "Then the next battle is against the ground itself."),
                },
                new[]
                {
                    new DialogueLine(ione, "You broke the anchors. The army lost its shape, and something beneath the city woke."),
                });

            AddAuthoredStageDialogue("2-13", "Cracked Foundry",
                new[]
                {
                    new DialogueLine(thaleia, "Olympus has ordered me to return. If I stay, it will declare me an enemy."),
                    new DialogueLine(playerSpeaker, "Then choose where you stand."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "You asked me to stay. I will stand with Boiotia until the truth reaches Olympus."),
                });

            AddAuthoredStageDialogue("2-14", "Pale Ash Crossing",
                new[]
                {
                    new DialogueLine(rusk, "The eastern garrison has opened its gates without a fight."),
                    new DialogueLine(playerSpeaker, "A surrender that easy is a trap."),
                },
                new[]
                {
                    new DialogueLine(rusk, "You infiltrated. The garrison was empty except for soldiers asleep beneath black ash."),
                });

            AddAuthoredStageDialogue("2-15", "Blackrock Descent",
                new[]
                {
                    new DialogueLine(ione, "They are not asleep. They are listening."),
                    new DialogueLine(playerSpeaker, "To what?"),
                },
                new[]
                {
                    new DialogueLine(ione, "You followed the voice. It spoke in Gorn's voice and called the player 'the wound.'"),
                });

            AddAuthoredStageDialogue("2-16", "Cinderfall Bastion",
                new[]
                {
                    new DialogueLine(thaleia, "The voice is using Gorn's memory to command the invasion."),
                    new DialogueLine(playerSpeaker, "Then we take the memory away."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "You used the relics. The trail led to the Boiotian archive, already burning from within."),
                });

            AddAuthoredStageDialogue("2-17", "Ruined Signal Tower",
                new[]
                {
                    new DialogueLine(rusk, "The archive holds every treaty between Boiotia and Olympus."),
                    new DialogueLine(playerSpeaker, "And someone wants both sides to forget the treaty."),
                },
                new[]
                {
                    new DialogueLine(rusk, "You saved the people. The treaties burned—but Ione found one surviving seal."),
                });

            AddAuthoredStageDialogue("2-18", "Molten Scar",
                new[]
                {
                    new DialogueLine(ione, "The seal predates Olympus. It belongs to the first empire beneath the mountain."),
                    new DialogueLine(playerSpeaker, "So this invasion is older than the throne."),
                },
                new[]
                {
                    new DialogueLine(ione, "You took the seal. It opened a map showing one final destination: the Ashen Gate."),
                });

            AddAuthoredStageDialogue("2-19", "Last Ember Camp",
                new[]
                {
                    new DialogueLine(thaleia, "The Ashen Gate is not a fortress. It is a passage for whatever the Crown Below released."),
                    new DialogueLine(playerSpeaker, "Then Boiotia is not being conquered. It is being opened."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "You closed the gate. The thing beyond it left one message in the stone: 'Olympus is next.'"),
                });

            AddAuthoredStageDialogue("2-20", "Ashen Threshold",
                new[]
                {
                    new DialogueLine(rusk, "The invaders are retreating toward the summit. They are carrying the last anchor."),
                    new DialogueLine(playerSpeaker, "Then they intend to reopen the gate from above."),
                },
                new[]
                {
                    new DialogueLine(rusk, "You pursued them. The summit is lost—but the city has time to survive."),
                });

            AddAuthoredStageDialogue("2-21", "Legion's End",
                new[]
                {
                    new DialogueLine(ione, "The last anchor is broken. The invasion has failed, but the sky above Olympus is burning."),
                    new DialogueLine(thaleia, "Boiotia survives because you chose it. Now Olympus will answer."),
                },
                new[]
                {
                    new DialogueLine(playerSpeaker, "Let it answer."),
                    new DialogueLine(thaleia, "Chapter Three begins where the smoke rises: at the gates of Olympus."),
                });

            // Chapter 3 — The Gates of Olympus, Stages 3-1..3-30
            // (docs/CAMPAIGN_NARRATIVE_CH1_3_2026-08-23.md). Fresh authored content; titles match
            // CampaignMapPresenter.Chapter3DepthFlavor.
            AddAuthoredStageDialogue("3-1", "Blackglass Shore",
                new[]
                {
                    new DialogueLine(thaleia, "The gates of Olympus are burning, but the city behind them is silent."),
                    new DialogueLine(rusk, "Then we do not wait for permission."),
                },
                new[]
                {
                    new DialogueLine(ione, "You entered through the main gate. The silence was a welcome—and a warning."),
                });

            AddAuthoredStageDialogue("3-2", "Sundered Causeway",
                new[]
                {
                    new DialogueLine(rusk, "No guards. No civilians. Only fresh footprints leading uphill."),
                    new DialogueLine(playerSpeaker, "Someone cleared the road for us."),
                },
                new[]
                {
                    new DialogueLine(rusk, "The footprints end at the Hall of Oaths."),
                });

            AddAuthoredStageDialogue("3-3", "Cinder Marsh",
                new[]
                {
                    new DialogueLine(ione, "The stones remember thousands of vows. Tonight, they remember only one word: exile."),
                    new DialogueLine(thaleia, "Olympus has already judged us."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "The judgement seal is false, but the doors obey it."),
                });

            AddAuthoredStageDialogue("3-4", "Iron Spine Ridge",
                new[]
                {
                    new DialogueLine(rusk, "A city that locks its own doors is already under siege."),
                    new DialogueLine(playerSpeaker, "Find another entrance."),
                },
                new[]
                {
                    new DialogueLine(rusk, "We found a servants' passage beneath the western colonnade."),
                });

            AddAuthoredStageDialogue("3-5", "Hollow Cistern",
                new[]
                {
                    new DialogueLine(ione, "The passage descends below the city, toward the first throne."),
                    new DialogueLine(playerSpeaker, "Then the gates are only the beginning."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "The throne chamber is empty. Someone removed the crown before we arrived."),
                });

            AddAuthoredStageDialogue("3-6", "Ashwind Bluffs",
                new[]
                {
                    new DialogueLine(thaleia, "The crown-bearer was the only voice capable of stopping the invasion."),
                    new DialogueLine(playerSpeaker, "Then we find the voice."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "You followed the attendants. They were carrying ashes from the Senate."),
                });

            AddAuthoredStageDialogue("3-7", "Charred Vineyard",
                new[]
                {
                    new DialogueLine(rusk, "The Senate was not attacked. It was erased."),
                    new DialogueLine(playerSpeaker, "By whom?"),
                },
                new[]
                {
                    new DialogueLine(rusk, "By soldiers wearing Olympus masks."),
                });

            AddAuthoredStageDialogue("3-8", "Obsidian Trench",
                new[]
                {
                    new DialogueLine(ione, "Masks do not hide faces here. They replace them."),
                    new DialogueLine(playerSpeaker, "Then we break every mask we find."),
                },
                new[]
                {
                    new DialogueLine(ione, "The broken masks whispered the same name: Eryx."),
                });

            AddAuthoredStageDialogue("3-9", "Smokeveil Pass",
                new[]
                {
                    new DialogueLine(thaleia, "Eryx was Olympus's First Witness. He disappeared before my initiation."),
                    new DialogueLine(playerSpeaker, "And now he rules from the shadows."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "He does not rule. He prepares a coronation."),
                });

            AddAuthoredStageDialogue("3-10", "Ruined Aquifer",
                new[]
                {
                    new DialogueLine(rusk, "The coronation bells are ringing below us."),
                    new DialogueLine(playerSpeaker, "Then the city still has a pulse."),
                },
                new[]
                {
                    new DialogueLine(ione, "It has a pulse—and it is not human."),
                });

            AddAuthoredStageDialogue("3-11", "Ember Terrace",
                new[]
                {
                    new DialogueLine(ione, "The bells are waking the buried colossi beneath Olympus."),
                    new DialogueLine(playerSpeaker, "Do we silence them or use their awakening?"),
                },
                new[]
                {
                    new DialogueLine(ione, "You silenced the bells. One colossus still opened its eye."),
                });

            AddAuthoredStageDialogue("3-12", "Grey Salt Flats",
                new[]
                {
                    new DialogueLine(rusk, "The colossus is walking toward the lower districts."),
                    new DialogueLine(playerSpeaker, "Then every minute matters."),
                },
                new[]
                {
                    new DialogueLine(rusk, "The lower districts are empty. Eryx evacuated them before the bells rang."),
                });

            AddAuthoredStageDialogue("3-13", "Cracked Aqueduct Span",
                new[]
                {
                    new DialogueLine(thaleia, "He wants us to chase the colossus while he takes the upper city."),
                    new DialogueLine(playerSpeaker, "Then we split his attention."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "Your signal reached the palace. Someone answered from inside."),
                });

            AddAuthoredStageDialogue("3-14", "Molten Foothills",
                new[]
                {
                    new DialogueLine(ione, "The answer came through a sealed mirror."),
                    new DialogueLine(playerSpeaker, "Who is behind it?"),
                },
                new[]
                {
                    new DialogueLine(ione, "A child wearing the crown of Olympus."),
                });

            AddAuthoredStageDialogue("3-15", "Ashfall Watchpost",
                new[]
                {
                    new DialogueLine(thaleia, "The child is the last legitimate heir. Eryx needs the heir alive."),
                    new DialogueLine(playerSpeaker, "Then the crown has a hostage."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "The mirror shattered. The heir is being taken to the upper sanctum."),
                });

            AddAuthoredStageDialogue("3-16", "Titan's Rib",
                new[]
                {
                    new DialogueLine(rusk, "We have two paths: rescue the heir or strike Eryx's command tower."),
                    new DialogueLine(playerSpeaker, "A throne without an heir is still a weapon."),
                },
                new[]
                {
                    new DialogueLine(rusk, "You rescued the heir. Eryx's tower remains, but the city now has a witness."),
                });

            AddAuthoredStageDialogue("3-17", "Cindergate Hollow",
                new[]
                {
                    new DialogueLine(ione, "The heir says Eryx is not seeking the throne."),
                    new DialogueLine(playerSpeaker, "Then what does he want?"),
                },
                new[]
                {
                    new DialogueLine(ione, "He wants Olympus to kneel voluntarily."),
                });

            AddAuthoredStageDialogue("3-18", "Scorched Reliquary",
                new[]
                {
                    new DialogueLine(thaleia, "Every faction that resisted him has received the same offer: surrender your name, and keep your life."),
                    new DialogueLine(playerSpeaker, "That is not peace."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "No. It is obedience wearing peace's face."),
                });

            AddAuthoredStageDialogue("3-19", "Ember Palisade",
                new[]
                {
                    new DialogueLine(rusk, "The command tower is broadcasting the offer across the city."),
                    new DialogueLine(playerSpeaker, "Then let Olympus hear a refusal."),
                },
                new[]
                {
                    new DialogueLine(rusk, "The refusal is heard. So is our location."),
                });

            AddAuthoredStageDialogue("3-20", "Deep Ash Descent",
                new[]
                {
                    new DialogueLine(ione, "The tower has marked us as the new enemy of Olympus."),
                    new DialogueLine(playerSpeaker, "We were already marked."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "Not like this. The city's remaining armies are marching toward us."),
                });

            AddAuthoredStageDialogue("3-21", "Cinderfall Chasm",
                new[]
                {
                    new DialogueLine(thaleia, "They are not Eryx's armies. They are frightened citizens wearing armour."),
                    new DialogueLine(playerSpeaker, "Then we must make them choose."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "You exposed the broadcast. The soldiers lowered their weapons—but Eryx opened the sky."),
                });

            AddAuthoredStageDialogue("3-22", "Obsidian Colonnade",
                new[]
                {
                    new DialogueLine(rusk, "A black sun is forming above the palace."),
                    new DialogueLine(playerSpeaker, "Another gate."),
                },
                new[]
                {
                    new DialogueLine(rusk, "The gate is not opening outward. Something is trying to enter Olympus."),
                });

            AddAuthoredStageDialogue("3-23", "Ashen Necropolis",
                new[]
                {
                    new DialogueLine(ione, "The Crown Below is answering the black sun."),
                    new DialogueLine(playerSpeaker, "The thing from Boiotia followed us."),
                },
                new[]
                {
                    new DialogueLine(ione, "It followed the seal, not us. The seal is inside the heir's crown."),
                });

            AddAuthoredStageDialogue("3-24", "Molten Crown Ridge",
                new[]
                {
                    new DialogueLine(thaleia, "The heir must remove the crown before Eryx reaches the sanctum."),
                    new DialogueLine(playerSpeaker, "And if removal kills them?"),
                },
                new[]
                {
                    new DialogueLine(thaleia, "The crown came free. The heir survived—but the black sun has found a new host."),
                });

            AddAuthoredStageDialogue("3-25", "Titan's Last Forge",
                new[]
                {
                    new DialogueLine(rusk, "Eryx is standing beneath it."),
                    new DialogueLine(playerSpeaker, "Then the First Witness is finally visible."),
                },
                new[]
                {
                    new DialogueLine(ione, "Visible, yes. Human, no longer."),
                });

            AddAuthoredStageDialogue("3-26", "Cindersea Shallows",
                new[]
                {
                    new DialogueLine(ione, "Eryx offers one final bargain: leave Olympus, and the gates will close behind you."),
                    new DialogueLine(playerSpeaker, "What does he demand in return?"),
                },
                new[]
                {
                    new DialogueLine(playerSpeaker, "We challenge him."),
                    new DialogueLine(thaleia, "Then Olympus will witness what its silence created."),
                });

            AddAuthoredStageDialogue("3-27", "Legion's Deep Camp",
                new[]
                {
                    new DialogueLine(thaleia, "Eryx has chained the palace to the black sun."),
                    new DialogueLine(playerSpeaker, "Cut the chains."),
                },
                new[]
                {
                    new DialogueLine(thaleia, "The first chain broke. The palace began to collapse around the sanctum."),
                });

            AddAuthoredStageDialogue("3-28", "Boiotia's Ember Core",
                new[]
                {
                    new DialogueLine(rusk, "The heir is trapped beneath the throne."),
                    new DialogueLine(playerSpeaker, "Get them out. I will hold the gate."),
                },
                new[]
                {
                    new DialogueLine(rusk, "The heir is free. Eryx has stepped through the opening."),
                });

            AddAuthoredStageDialogue("3-29", "Olympus' Ashen Gate",
                new[]
                {
                    new DialogueLine(ione, "Beyond the gate is not darkness. It is a road lined with dead stars."),
                    new DialogueLine(playerSpeaker, "Then we close it from both sides."),
                },
                new[]
                {
                    new DialogueLine(ione, "Eryx is gone, but the road remains open for one breath."),
                });

            AddAuthoredStageDialogue("3-30", "Boiotia's Last Stand",
                new[]
                {
                    new DialogueLine(thaleia, "The gates of Olympus stand, but the old throne is broken."),
                    new DialogueLine(playerSpeaker, "Then we build no new throne."),
                },
                new[]
                {
                    new DialogueLine(ione, "The road beyond the gate leads to a sea of dead stars."),
                    new DialogueLine(thaleia, "Chapter Four begins there. Olympus has survived—but something beyond it has learned how to return."),
                });

            // Shared post-finale voice for Ch4–7 / Ch10+ hooks (LOCKED Ch4-7 narrative upgrade 2026-08-25).
            var unknownVoice = new StorySpeaker("unknown_voice", "Unknown Voice", "UI/Portraits/Paladin", SpeakerPosition.Right);

            // Chapter 4 depth fill, Stages 4-1..4-30. Ordinary stages stay templated; required
            // beats (4-1 / 4-15 / 4-30 pre + 4-30 post hook) match Ch8–11 treatment shape.
            (string stageId, string title, string enemyName)[] chapter4Stages =
            {
                ("4-1", "Ashroad Gatehouse", "Gatehouse Watch"),
                ("4-2", "Cinderbrook Ford", "Ford Sentries"),
                ("4-3", "Titanfall Ravine", "Ravine Legionnaires"),
                ("4-4", "Scorched Terracing", "Terrace Remnant"),
                ("4-5", "Ashen Millworks", "Millworks Guard"),
                ("4-6", "Smolder Hollow", "Hollow Sentries"),
                ("4-7", "Emberlit Colonnade", "Colonnade Wardens"),
                ("4-8", "Blackash Quarry", "Quarry Legion"),
                ("4-9", "Cindermoor", "Moor Stalkers"),
                ("4-10", "Ruined Watergate", "Watergate Guard"),
                ("4-11", "Ashfall Barrows", "Barrow Wardens"),
                ("4-12", "Titan's Forgehall", "Forgehall Keepers"),
                ("4-13", "Cinderspire Base", "Spire Garrison"),
                ("4-14", "Molten Stairwell", "Stairwell Guard"),
                ("4-15", "Ember Threshing Floor", "Threshing Guard"),
                ("4-16", "Scorched Reservoir", "Reservoir Wardens"),
                ("4-17", "Ashen Palisade Line", "Palisade Legion"),
                ("4-18", "Deep Cinder Vault", "Vault Legion"),
                ("4-19", "Titan's Buried Anvil", "Anvil Keepers"),
                ("4-20", "Ashfall Colossus Base", "Colossus Guard"),
                ("4-21", "Cinderveil Crossing", "Crossing Legion"),
                ("4-22", "Molten Bastion Wall", "Bastion Wall Guard"),
                ("4-23", "Scorchfield Camp", "Scorchfield Legion"),
                ("4-24", "Ember Sepulcher", "Sepulcher Wardens"),
                ("4-25", "Titan's Hollow Vein", "Vein Legion"),
                ("4-26", "Ashen Siegeworks", "Siegeworks Legion"),
                ("4-27", "Cinderfall Approach", "Approach Legion"),
                ("4-28", "Molten Command Post", "Command Post Guard"),
                ("4-29", "Boiotia's Ember Spine", "Ember Spine Legion"),
                ("4-30", "Ashfall Legion Reserve", "Legion Reserve Command"),
            };

            foreach ((string stageId, string title, string enemyName) in chapter4Stages)
            {
                var enemy = new StorySpeaker(stageId + "_enemy", enemyName, "UI/Portraits/Paladin", SpeakerPosition.Right);
                string preEnemyLine = stageId switch
                {
                    "4-1" => "The ember spine does not open for the unburned.",
                    "4-15" => "Threshing ash takes everything soft. You included.",
                    "4-30" => "The reserve is Olympus's last ash before the coast.",
                    _ => $"{enemyName} bar the road through {title}. Olympus does not forgive trespassers.",
                };
                string prePlayerLine = stageId switch
                {
                    "4-1" => "Then I walk already burning.",
                    "4-15" => "I am not soft. Clear the floor.",
                    "4-30" => "Then this is where ash ends and marching begins.",
                    _ => $"Olympus isn't here. Clear {title} and keep moving.",
                };

                if (stageId == "4-30")
                {
                    var pre = new StorySequence($"{stageId}_pre", $"Stage {stageId}: {title}");
                    pre.lines.Add(new DialogueLine(enemy, preEnemyLine));
                    pre.lines.Add(new DialogueLine(playerSpeaker, prePlayerLine));
                    sequences[$"{stageId}_pre"] = pre;

                    var post = new StorySequence($"{stageId}_post", $"Stage {stageId}: Cleared");
                    post.lines.Add(new DialogueLine(playerSpeaker, "The spine is broken. The coast can hear us coming."));
                    post.lines.Add(new DialogueLine(unknownVoice, "Hear carefully, Sovereign — coasts answer with tides, not with mercy."));
                    sequences[$"{stageId}_post"] = post;
                    continue;
                }

                AddStageDialogue(stageId, title, enemy,
                    preEnemyLine,
                    prePlayerLine,
                    $"{enemyName} scatter, broken.",
                    $"{title} is behind us. Boiotia's ash still stretches on.");
            }

            // Chapter 5 depth fill, Stages 5-1..5-30. Ordinary stages stay templated; required
            // beats (5-1 / 5-15 / 5-30 pre + 5-30 post hook) match Ch8–11 treatment shape.
            (string stageId, string title, string enemyName)[] chapter5Stages =
            {
                ("5-1", "Cindertide Shoals", "Shoal Wardens"),
                ("5-2", "Salt-Ash Harbor", "Harbor Legion"),
                ("5-3", "Driftwood Palisade", "Palisade Guard"),
                ("5-4", "Greywater Inlet", "Inlet Sentries"),
                ("5-5", "Sunken Pier Row", "Pier Legion"),
                ("5-6", "Cindercliff Stair", "Cliffside Wardens"),
                ("5-7", "Foglit Cove", "Cove Stalkers"),
                ("5-8", "Brinewreck Shallows", "Wreck Guard"),
                ("5-9", "Ashen Lighthouse", "Lighthouse Watch"),
                ("5-10", "Stormwrack Point", "Wrack Legion"),
                ("5-11", "Tideglass Reef", "Reef Sentinels"),
                ("5-12", "Coastal Redoubt", "Redoubt Garrison"),
                ("5-13", "Ember Surf Break", "Surf Guard"),
                ("5-14", "Ruined Sea Gate", "Sea Gate Legion"),
                ("5-15", "Windward Bastion", "Bastion Watch"),
                ("5-16", "Cindersalt Flats", "Flat Legion"),
                ("5-17", "Longshore Outpost", "Outpost Guard"),
                ("5-18", "Ashen Skiff Yard", "Skiff Yard Legion"),
                ("5-19", "Rockbound Cove", "Cove Legion"),
                ("5-20", "Greyfoam Straits", "Strait Wardens"),
                ("5-21", "Cindermist Harbor", "Mist Harbor Legion"),
                ("5-22", "Seaward Watchtower", "Watchtower Legion"),
                ("5-23", "Brackish Delta", "Delta Guard"),
                ("5-24", "Ashfall Naval Yard", "Naval Yard Legion"),
                ("5-25", "Stormward Bluff", "Bluff Legion"),
                ("5-26", "Cindergale Anchorage", "Anchorage Guard"),
                ("5-27", "Olympus Approach Road", "Approach Legion"),
                ("5-28", "Godsreach Landing", "Landing Command"),
                ("5-29", "Threshold of Olympus", "Threshold Legion Command"),
                ("5-30", "Boiotia's Sea Wall", "Sea Wall High Command"),
            };

            foreach ((string stageId, string title, string enemyName) in chapter5Stages)
            {
                var enemy = new StorySpeaker(stageId + "_enemy", enemyName, "UI/Portraits/Paladin", SpeakerPosition.Right);
                string preEnemyLine = stageId switch
                {
                    "5-1" => "The ash coast takes ships and soldiers the same way.",
                    "5-15" => "Windward holds. Your climb dies here if we choose.",
                    "5-30" => "The sea wall is Olympus's shoreline. Break it and drown.",
                    _ => $"{enemyName} bar the road through {title}. Olympus does not forgive trespassers.",
                };
                string prePlayerLine = stageId switch
                {
                    "5-1" => "I am neither. Clear the shoals.",
                    "5-15" => "Then choose wrong.",
                    "5-30" => "I break walls. The sea can wait.",
                    _ => $"Olympus isn't here. Clear {title} and keep moving.",
                };

                if (stageId == "5-30")
                {
                    var pre = new StorySequence($"{stageId}_pre", $"Stage {stageId}: {title}");
                    pre.lines.Add(new DialogueLine(enemy, preEnemyLine));
                    pre.lines.Add(new DialogueLine(playerSpeaker, prePlayerLine));
                    sequences[$"{stageId}_pre"] = pre;

                    var post = new StorySequence($"{stageId}_post", $"Stage {stageId}: Cleared");
                    post.lines.Add(new DialogueLine(playerSpeaker, "The coast is ours. The climb begins where the wall ends."));
                    post.lines.Add(new DialogueLine(unknownVoice, "Begin carefully, Sovereign. Heights punish the hurried."));
                    sequences[$"{stageId}_post"] = post;
                    continue;
                }

                AddStageDialogue(stageId, title, enemy,
                    preEnemyLine,
                    prePlayerLine,
                    $"{enemyName} scatter, broken.",
                    $"{title} is behind us. Boiotia's ash still stretches on.");
            }

            // Chapter 6 depth fill, Stages 6-1..6-30. Ordinary stages stay templated; required
            // beats (6-1 / 6-15 / 6-30 pre + 6-30 post hook) match Ch8–11 treatment shape.
            (string stageId, string title, string enemyName)[] chapter6Stages =
            {
                ("6-1", "Ashfoot Trailhead", "Trailhead Watch"),
                ("6-2", "Switchback Cinderpath", "Cinderpath Legion"),
                ("6-3", "Craggy Overlook", "Overlook Sentries"),
                ("6-4", "Thin Air Camp", "Camp Wardens"),
                ("6-5", "Boulderfall Pass", "Pass Legion"),
                ("6-6", "Ashen Timberline", "Timberline Guard"),
                ("6-7", "Cloudbreak Ridge", "Ridge Sentinels"),
                ("6-8", "Frostash Shelf", "Shelf Wardens"),
                ("6-9", "Windhowl Saddle", "Saddle Legion"),
                ("6-10", "Stonefall Traverse", "Traverse Guard"),
                ("6-11", "Hanging Cinderfield", "Cinderfield Legion"),
                ("6-12", "Echo Chasm Bridge", "Chasm Bridge Guard"),
                ("6-13", "Greyrock Bivouac", "Bivouac Wardens"),
                ("6-14", "Ashen Col", "Col Legion"),
                ("6-15", "Skyline Watchpost", "Watchpost Command"),
                ("6-16", "Cinderglass Face", "Face Sentries"),
                ("6-17", "Highfrost Camp", "Camp Legion"),
                ("6-18", "Precipice Trail", "Trail Guard"),
                ("6-19", "Stormline Ridge", "Ridge Legion"),
                ("6-20", "Ashfall Summit Camp", "Summit Camp Command"),
                ("6-21", "Thundercleft Pass", "Cleft Legion"),
                ("6-22", "Godsview Overlook", "Overlook Legion Command"),
                ("6-23", "Ashen Crown Ridge", "Crown Ridge Guard"),
                ("6-24", "Skyward Cinderpath", "Cinderpath Legion Command"),
                ("6-25", "Highaltar Approach", "Altar Approach Guard"),
                ("6-26", "Cloudsplit Ridge", "Ridge High Command"),
                ("6-27", "Threshold Camp", "Threshold Legion"),
                ("6-28", "Godsgate Approach", "Gate Approach Command"),
                ("6-29", "Olympus Outer Gate", "Outer Gate Legion Command"),
                ("6-30", "Boiotia's Summit Stand", "Summit High Command"),
            };

            foreach ((string stageId, string title, string enemyName) in chapter6Stages)
            {
                var enemy = new StorySpeaker(stageId + "_enemy", enemyName, "UI/Portraits/Paladin", SpeakerPosition.Right);
                string preEnemyLine = stageId switch
                {
                    "6-1" => "The climb begins where soft feet stop.",
                    "6-15" => "From this watchpost you can see Olympus — and it can see you.",
                    "6-30" => "The summit stand does not fall. Turn back.",
                    _ => $"{enemyName} bar the road through {title}. Olympus does not forgive trespassers.",
                };
                string prePlayerLine = stageId switch
                {
                    "6-1" => "Then soft feet were never the point.",
                    "6-15" => "Good. Let it watch.",
                    "6-30" => "I came to take the view — and the gate beyond it.",
                    _ => $"Olympus isn't here. Clear {title} and keep moving.",
                };

                if (stageId == "6-30")
                {
                    var pre = new StorySequence($"{stageId}_pre", $"Stage {stageId}: {title}");
                    pre.lines.Add(new DialogueLine(enemy, preEnemyLine));
                    pre.lines.Add(new DialogueLine(playerSpeaker, prePlayerLine));
                    sequences[$"{stageId}_pre"] = pre;

                    var post = new StorySequence($"{stageId}_post", $"Stage {stageId}: Cleared");
                    post.lines.Add(new DialogueLine(playerSpeaker, "The summit is ours. The outer gate is next."));
                    post.lines.Add(new DialogueLine(unknownVoice, "Gates open both ways, Sovereign. Remember that."));
                    sequences[$"{stageId}_post"] = post;
                    continue;
                }

                AddStageDialogue(stageId, title, enemy,
                    preEnemyLine,
                    prePlayerLine,
                    $"{enemyName} scatter, broken.",
                    $"{title} is behind us. Boiotia's ash still stretches on.");
            }

            // Chapter 7 depth fill, Stages 7-1..7-30. Ordinary stages stay templated; required
            // beats (7-1 / 7-15 / 7-30 pre + 7-30 post hook) match Ch8–11 treatment shape.
            (string stageId, string title, string enemyName)[] chapter7Stages =
            {
                ("7-1", "Beyond the Outer Gate", "Inner Gate Watch"),
                ("7-2", "Godsroad Switchback", "Switchback Command"),
                ("7-3", "Marble Terrace", "Terrace Wardens"),
                ("7-4", "Broken Colossus Field", "Colossus Field Legion"),
                ("7-5", "Skyforge Approach", "Skyforge Legion"),
                ("7-6", "Cindered Grand Stair", "Grand Stair Guard"),
                ("7-7", "Ashbound Colonnade", "Colonnade Legion"),
                ("7-8", "Divine Foundry Ruins", "Foundry Ruins Guard"),
                ("7-9", "Cloudpiercer Spire Base", "Spire Base Legion"),
                ("7-10", "Shattered Pantheon Court", "Pantheon Court Guard"),
                ("7-11", "Ember-Lit Processional", "Processional Legion"),
                ("7-12", "Highvault Antechamber", "Antechamber Guard"),
                ("7-13", "Ashfall Oracle Ruins", "Oracle Ruins Legion"),
                ("7-14", "Sundered Throne Approach", "Throne Approach Guard"),
                ("7-15", "Cinderlit Amphitheater", "Amphitheater Legion"),
                ("7-16", "Godsforge Threshold", "Forge Threshold Guard"),
                ("7-17", "Marble Ashfields", "Ashfield Legion Command"),
                ("7-18", "Broken Aegis Wall", "Aegis Wall Guard"),
                ("7-19", "Highforge Bastion", "Bastion Legion"),
                ("7-20", "Ashen Processional Gate", "Processional Gate Command"),
                ("7-21", "Cindered Reliquary Vault", "Reliquary Vault Legion"),
                ("7-22", "Skyward Colossus Ruins", "Colossus Ruins Command"),
                ("7-23", "Godsroad Terminus", "Terminus Legion"),
                ("7-24", "Divine Armory Ruins", "Armory Ruins Guard"),
                ("7-25", "Ashfall Inner Sanctum Approach", "Sanctum Approach Command"),
                ("7-26", "Cinderlit Grand Hall", "Grand Hall Legion"),
                ("7-27", "Shattered Throne Room", "Throne Room Command"),
                ("7-28", "Highest Ashfall Gate", "Highest Gate Legion"),
                ("7-29", "Godsreach Sanctum", "Sanctum High Command"),
                ("7-30", "Boiotia's Divine Threshold", "Divine Threshold High Command"),
            };

            foreach ((string stageId, string title, string enemyName) in chapter7Stages)
            {
                var enemy = new StorySpeaker(stageId + "_enemy", enemyName, "UI/Portraits/Paladin", SpeakerPosition.Right);
                string preEnemyLine = stageId switch
                {
                    "7-1" => "Beyond the outer gate, Olympus stops pretending to be distant.",
                    "7-15" => "The amphitheater remembers every army that died for applause.",
                    "7-30" => "This threshold answers for Olympus. You will not cross it.",
                    _ => $"{enemyName} bar the road through {title}. Olympus does not forgive trespassers.",
                };
                string prePlayerLine = stageId switch
                {
                    "7-1" => "Good. Distance was never the problem.",
                    "7-15" => "Then it can remember one more.",
                    "7-30" => "I cross what I break. Stand aside.",
                    _ => $"Olympus isn't here. Clear {title} and keep moving.",
                };

                if (stageId == "7-30")
                {
                    var pre = new StorySequence($"{stageId}_pre", $"Stage {stageId}: {title}");
                    pre.lines.Add(new DialogueLine(enemy, preEnemyLine));
                    pre.lines.Add(new DialogueLine(playerSpeaker, prePlayerLine));
                    sequences[$"{stageId}_pre"] = pre;

                    var post = new StorySequence($"{stageId}_post", $"Stage {stageId}: Cleared");
                    post.lines.Add(new DialogueLine(playerSpeaker, "The divine threshold is open. Whatever answers for Olympus — answer me."));
                    post.lines.Add(new DialogueLine(unknownVoice, "It will, Sovereign. Crowns of storms do not ignore open doors."));
                    sequences[$"{stageId}_post"] = post;
                    continue;
                }

                AddStageDialogue(stageId, title, enemy,
                    preEnemyLine,
                    prePlayerLine,
                    $"{enemyName} scatter, broken.",
                    $"{title} is behind us. Boiotia's ash still stretches on.");
            }

            // Chapter 8 depth fill, Stages 8-1..8-30 (2026-08-22, CORE_SYSTEMS_CONSTITUTION §0
            // wartime doctrine). Names come from the ChatGPT naming kit
            // (CAMPAIGN_10_CHAPTER_NAMING_AND_BEAT_DIALOGUE_KIT_2026-08-22.md, Chapter 8 - Crown
            // of Storms table) - not invented; title/enemyName match
            // CampaignMapPresenter.Chapter8DepthFlavor. Required beat dialogue applied verbatim
            // for 8-1_pre, 8-15_pre, 8-30_pre, 8-30_post per the kit's §3 table; every other
            // stage stays lightly templated, per the kit's own "do not broaden this into dialogue
            // for every stage" instruction.
            (string stageId, string title, string enemyName)[] chapter8Stages =
            {
                ("8-1", "Stormward Terrace", "Stormward Guard"),
                ("8-2", "Sun-Split Causeway", "Sun Guard Patrol"),
                ("8-3", "Thunderhead Court", "Thunder Court Wardens"),
                ("8-4", "Gilded Rainstairs", "Rainstairs Legion"),
                ("8-5", "Cloudharrow Bridge", "Bridge Sentinels"),
                ("8-6", "Dawnfire Bastion", "Dawnfire Guard"),
                ("8-7", "Tempest Orchard", "Orchard Cohort"),
                ("8-8", "Brasswind Gallery", "Gallery Wardens"),
                ("8-9", "Lightning Well", "Well Keepers"),
                ("8-10", "Sunforge Ramp", "Sunforge Legion"),
                ("8-11", "Stormglass Arcade", "Arcade Guard"),
                ("8-12", "High Noon Redoubt", "Noon Redoubt Command"),
                ("8-13", "Thunderstep Rise", "Thunderstep Sentinels"),
                ("8-14", "Goldcloud Parapet", "Parapet Cohort"),
                ("8-15", "Apollo's Broken Court", "Sun Court Guard"),
                ("8-16", "Boltfall Stair", "Boltfall Legion"),
                ("8-17", "Skyfire Reservoir", "Reservoir Wardens"),
                ("8-18", "Tempest Reliquary", "Reliquary Guard"),
                ("8-19", "Whitecloud Bastion", "Bastion Cohort"),
                ("8-20", "Sunward Processional", "Processional Command"),
                ("8-21", "Stormcrown Gate", "Stormcrown Guard"),
                ("8-22", "Lightning Choir Hall", "Choir Hall Legion"),
                ("8-23", "Ash-and-Aurum Span", "Aurum Span Wardens"),
                ("8-24", "Dawnspire Foot", "Dawnspire Guard"),
                ("8-25", "Thundercliff Traverse", "Traverse Cohort"),
                ("8-26", "Solar Watchfire", "Watchfire Legion"),
                ("8-27", "Tempest Crown Wall", "Crown Wall Guard"),
                ("8-28", "The Sun Gate", "Sun Gate Command"),
                ("8-29", "Apollo's Stormworks", "Stormworks High Guard"),
                ("8-30", "Crown of Storms", "Sun Guard High Command"),
            };

            foreach ((string stageId, string title, string enemyName) in chapter8Stages)
            {
                var enemy = new StorySpeaker(stageId + "_enemy", enemyName, "UI/Portraits/Paladin", SpeakerPosition.Right);
                bool isFinal = stageId == "8-30";
                string preEnemyLine = stageId switch
                {
                    "8-1" => "Apollo's light burns invaders clean.",
                    "8-15" => "The Sun Guard has never yielded its court.",
                    "8-30" => "The crown belongs to the gods.",
                    _ => $"{enemyName} bar the road through {title}. The storm crown does not forgive trespassers.",
                };
                string prePlayerLine = stageId switch
                {
                    "8-1" => "Light only reveals what deserves to fall.",
                    "8-15" => "Then it has never been tested.",
                    "8-30" => "Crowns belong to whoever can keep them.",
                    _ => $"Olympus isn't here. Clear {title} and keep moving.",
                };
                AddStageDialogue(stageId, title, enemy,
                    preEnemyLine,
                    prePlayerLine,
                    isFinal ? "Athena's citadel will close around you." : $"{enemyName} scatter, broken.",
                    isFinal ? "A closed citadel is only a trapped army." : $"{title} is behind us. The storm still rages ahead.");
            }

            // Chapter 9 — The Aegis Citadel (naming kit + required beats 9-1/9-15/9-30).
            (string stageId, string title, string enemyName)[] chapter9Stages =
            {
                ("9-1", "Aegis Outer Court", "Aegis Court Watch"),
                ("9-2", "Spearline Causeway", "Spearline Cohort"),
                ("9-3", "Owlstone Gatehouse", "Gatehouse Wardens"),
                ("9-4", "Bronze Verdict Hall", "Verdict Guard"),
                ("9-5", "Strategos' Walk", "Strategos Legion"),
                ("9-6", "Shieldwall Arcade", "Shieldwall Cohort"),
                ("9-7", "War Map Gallery", "Gallery Command"),
                ("9-8", "Iron Laurel Yard", "Laurel Guard"),
                ("9-9", "Silent Phalanx Court", "Phalanx Wardens"),
                ("9-10", "Aegis Foundry", "Foundry Cohort"),
                ("9-11", "Marble Muster Field", "Muster Command"),
                ("9-12", "Bronze Archive", "Archive Guard"),
                ("9-13", "Spearpoint Stair", "Spearpoint Legion"),
                ("9-14", "Citadel Cistern", "Cistern Wardens"),
                ("9-15", "Athena's War Hall", "War Hall Command"),
                ("9-16", "Nine-Shield Passage", "Nine-Shield Guard"),
                ("9-17", "Gorgon Banner Court", "Banner Cohort"),
                ("9-18", "Oathbound Barracks", "Oathbound Legion"),
                ("9-19", "Aegis Bastion", "Bastion Guard"),
                ("9-20", "Iron Verdict Gate", "Verdict Gate Command"),
                ("9-21", "Tactical Reliquary", "Reliquary Wardens"),
                ("9-22", "Owlspire Ascent", "Owlspire Guard"),
                ("9-23", "Shielded Processional", "Processional Cohort"),
                ("9-24", "Bronze Throne Annex", "Throne Annex Command"),
                ("9-25", "The War Council Chamber", "War Council Guard"),
                ("9-26", "Spearwall Rampart", "Spearwall Legion"),
                ("9-27", "Aegis Inner Gate", "Inner Gate Cohort"),
                ("9-28", "Athena's Last Redoubt", "Redoubt Command"),
                ("9-29", "Citadel Heart", "Citadel High Guard"),
                ("9-30", "The Aegis Citadel", "Aegis High Command"),
            };

            foreach ((string stageId, string title, string enemyName) in chapter9Stages)
            {
                var enemy = new StorySpeaker(stageId + "_enemy", enemyName, "UI/Portraits/Paladin", SpeakerPosition.Right);
                bool isFinal = stageId == "9-30";
                string preEnemyLine = stageId switch
                {
                    "9-1" => "Athena does not lose wars.",
                    "9-15" => "You cannot outthink the goddess of war.",
                    "9-30" => "The shield-wall holds the throne.",
                    _ => $"{enemyName} bar the road through {title}. The Aegis admits no undisciplined force.",
                };
                string prePlayerLine = stageId switch
                {
                    "9-1" => "Every war begins with someone believing that.",
                    "9-15" => "I do not need to. I need you to make one mistake.",
                    "9-30" => "Then I break the shield before I take the throne.",
                    _ => $"Olympus isn't here. Clear {title} and keep moving.",
                };
                AddStageDialogue(stageId, title, enemy,
                    preEnemyLine,
                    prePlayerLine,
                    isFinal ? "Zeus will judge this." : $"{enemyName} scatter, broken.",
                    isFinal ? "At last. Bring me his judgment." : $"{title} is behind us. The citadel still stands ahead.");
            }

            // Chapter 10 — The Empty Throne (CAMPAIGN_CH10_NAMING_AND_BEATS_v1.md).
            // unknownVoice declared once above Ch4 (shared post-finale hook speaker).
            (string stageId, string title, string enemyName)[] chapter10Stages =
            {
                ("10-1", "Throneward Causeway", "Throneward Watch"),
                ("10-2", "Storm-King's Gate", "Storm Gate Guard"),
                ("10-3", "Eagle Standard Court", "Eagle Cohort"),
                ("10-4", "Cloudbound Archive", "Archive Wardens"),
                ("10-5", "Lightning Rod Hall", "Rod Hall Legion"),
                ("10-6", "High Throne Stair", "Throne Stair Guard"),
                ("10-7", "Zeus's Empty Forum", "Forum Command"),
                ("10-8", "Thunderchain Bridge", "Thunderchain Cohort"),
                ("10-9", "Skyvault Antechamber", "Skyvault Guard"),
                ("10-10", "Storm Eagle Roost", "Eagle Legion"),
                ("10-11", "Crownbolt Gallery", "Crownbolt Wardens"),
                ("10-12", "The Judgment Steps", "Judgment Guard"),
                ("10-13", "Cloudbreaker Hall", "Cloudbreaker Command"),
                ("10-14", "Thunder Oath Chamber", "Oath Cohort"),
                ("10-15", "The Empty Throne Court", "Throne Court Guard"),
                ("10-16", "Boltscar Processional", "Processional Legion"),
                ("10-17", "Skyfire Treasury", "Treasury Wardens"),
                ("10-18", "Eaglewatch Parapet", "Eaglewatch Guard"),
                ("10-19", "Tempest Engine Room", "Engine Cohort"),
                ("10-20", "Zeus's War Balcony", "Balcony Command"),
                ("10-21", "Stormseal Reliquary", "Reliquary Guard"),
                ("10-22", "Cloud Crown Rampart", "Crown Rampart Legion"),
                ("10-23", "Thunderbrand Hall", "Thunderbrand Wardens"),
                ("10-24", "The Last Aegis", "Last Aegis Cohort"),
                ("10-25", "Thronefire Vestibule", "Vestibule Guard"),
                ("10-26", "Sky King's Bastion", "Bastion Command"),
                ("10-27", "The Broken Scepter", "Scepter Legion"),
                ("10-28", "Stormheart Gate", "Stormheart Guard"),
                ("10-29", "The Throne Dais", "Throne High Command"),
                ("10-30", "The Empty Throne", "Zeus's Final Guard"),
            };

            foreach ((string stageId, string title, string enemyName) in chapter10Stages)
            {
                var enemy = new StorySpeaker(stageId + "_enemy", enemyName, "UI/Portraits/Paladin", SpeakerPosition.Right);
                string preEnemyLine = stageId switch
                {
                    "10-1" => "Kneel before the Sky King.",
                    "10-15" => "The throne is eternal.",
                    "10-30" => "One mortal cannot end Olympus.",
                    _ => $"{enemyName} holds {title}. The Sky Throne will not yield.",
                };
                string prePlayerLine = stageId switch
                {
                    "10-1" => "I came to see whether he is still sitting.",
                    "10-15" => "Nothing empty is eternal.",
                    "10-30" => "One mortal can show it can end.",
                    _ => "Clear the path. The empty throne still waits.",
                };

                if (stageId == "10-30")
                {
                    var pre = new StorySequence($"{stageId}_pre", $"Stage {stageId}: {title}");
                    pre.lines.Add(new DialogueLine(enemy, preEnemyLine));
                    pre.lines.Add(new DialogueLine(playerSpeaker, prePlayerLine));
                    sequences[$"{stageId}_pre"] = pre;

                    var post = new StorySequence($"{stageId}_post", $"Stage {stageId}: Cleared");
                    post.lines.Add(new DialogueLine(playerSpeaker, "The throne is empty. The war is not."));
                    post.lines.Add(new DialogueLine(unknownVoice, "Then take the storm, Sovereign — and pay its price."));
                    sequences[$"{stageId}_post"] = post;
                    continue;
                }

                AddStageDialogue(stageId, title, enemy,
                    preEnemyLine,
                    prePlayerLine,
                    $"{enemyName} scatter, broken.",
                    $"{title} is behind us. The empty throne still waits.");
            }

            // Chapter 11 — The Storm's Price (post–Empty Throne; Unknown Voice "take the storm… pay its price").
            (string stageId, string title, string enemyName)[] chapter11Stages =
            {
                ("11-1", "Stormprice Causeway", "Stormprice Watch"),
                ("11-2", "Thunder Tithe Gate", "Tithe Gate Guard"),
                ("11-3", "Bolt-Debt Court", "Bolt-Debt Cohort"),
                ("11-4", "Sky Levy Archive", "Levy Archive Wardens"),
                ("11-5", "Tempest Toll Hall", "Toll Hall Legion"),
                ("11-6", "Price of Clouds Stair", "Cloud Stair Guard"),
                ("11-7", "The Mortal Storm Forum", "Forum Command"),
                ("11-8", "Oathprice Bridge", "Oathprice Cohort"),
                ("11-9", "Taken Thunder Antechamber", "Thunder Antechamber Guard"),
                ("11-10", "Storm Eagle Debt-Roost", "Debt-Roost Legion"),
                ("11-11", "Crownlevy Gallery", "Crownlevy Wardens"),
                ("11-12", "The Reckoning Steps", "Reckoning Guard"),
                ("11-13", "Cloudbreaker Tithe Hall", "Tithe Hall Command"),
                ("11-14", "Storm Oath Chamber", "Storm Oath Cohort"),
                ("11-15", "The Price Court", "Price Court Guard"),
                ("11-16", "Boltscar Tithe Road", "Tithe Road Legion"),
                ("11-17", "Skyfire Levy Vault", "Levy Vault Wardens"),
                ("11-18", "Eaglewatch Debt Parapet", "Debt Parapet Guard"),
                ("11-19", "Tempest Engine Toll", "Engine Toll Cohort"),
                ("11-20", "War Balcony of Storms", "Storm Balcony Command"),
                ("11-21", "Stormseal Price Reliquary", "Price Reliquary Guard"),
                ("11-22", "Cloud Crown Levy Rampart", "Levy Rampart Legion"),
                ("11-23", "Thunderbrand Tithe Hall", "Thunderbrand Tithe Wardens"),
                ("11-24", "The Last Storm Debt", "Last Debt Cohort"),
                ("11-25", "Thronefire Price Vestibule", "Price Vestibule Guard"),
                ("11-26", "Sky King's Taken Bastion", "Taken Bastion Command"),
                ("11-27", "The Broken Storm Scepter", "Broken Scepter Legion"),
                ("11-28", "Stormheart Tithe Gate", "Stormheart Tithe Guard"),
                ("11-29", "The Reckoning Dais", "Reckoning High Command"),
                ("11-30", "The Storm's Price", "Storm Price High Guard"),
            };

            foreach ((string stageId, string title, string enemyName) in chapter11Stages)
            {
                var enemy = new StorySpeaker(stageId + "_enemy", enemyName, "UI/Portraits/Paladin", SpeakerPosition.Right);
                string preEnemyLine = stageId switch
                {
                    "11-1" => "The storm was never free.",
                    "11-15" => "Every bolt has a price.",
                    "11-30" => "Pay what you took, Sovereign.",
                    _ => $"{enemyName} holds {title}. The taken storm will be paid for.",
                };
                string prePlayerLine = stageId switch
                {
                    "11-1" => "Then name the price. I am still walking.",
                    "11-15" => "I will pay in victories, not knees.",
                    "11-30" => "I took the storm. I will finish the debt.",
                    _ => "Clear the path. The storm's price still waits.",
                };

                if (stageId == "11-30")
                {
                    var pre = new StorySequence($"{stageId}_pre", $"Stage {stageId}: {title}");
                    pre.lines.Add(new DialogueLine(enemy, preEnemyLine));
                    pre.lines.Add(new DialogueLine(playerSpeaker, prePlayerLine));
                    sequences[$"{stageId}_pre"] = pre;

                    var post = new StorySequence($"{stageId}_post", $"Stage {stageId}: Cleared");
                    post.lines.Add(new DialogueLine(playerSpeaker, "The price is paid. The storm is mine to keep."));
                    post.lines.Add(new DialogueLine(unknownVoice, "Keep it carefully, Sovereign — storms remember their debtors."));
                    sequences[$"{stageId}_post"] = post;
                    continue;
                }

                AddStageDialogue(stageId, title, enemy,
                    preEnemyLine,
                    prePlayerLine,
                    $"{enemyName} scatter, broken.",
                    $"{title} is behind us. The storm's price still waits.");
            }

            // Chapter 12 — The Mortal Host.
            (string stageId, string title, string enemyName)[] chapter12Stages =
            {
                ("12-1", "Hostward March", "Hostward Watch"),
                ("12-2", "Banner of Embers Gate", "Ember Gate Guard"),
                ("12-3", "Ash Cohort Court", "Ash Cohort"),
                ("12-4", "Mortal Levy Yard", "Levy Yard Wardens"),
                ("12-5", "Spear-Tithe Hall", "Spear-Tithe Legion"),
                ("12-6", "War-Debt Stair", "War-Debt Guard"),
                ("12-7", "The Sovereign Muster", "Muster Command"),
                ("12-8", "Oathbound Causeway", "Oathbound Cohort"),
                ("12-9", "Host Antechamber", "Host Antechamber Guard"),
                ("12-10", "Eagle-and-Ash Roost", "Ash Roost Legion"),
                ("12-11", "Crown Host Gallery", "Crown Host Wardens"),
                ("12-12", "The Gathering Steps", "Gathering Guard"),
                ("12-13", "Stormborn Barracks", "Stormborn Command"),
                ("12-14", "Mortal Oath Chamber", "Mortal Oath Cohort"),
                ("12-15", "The Host Court", "Host Court Guard"),
                ("12-16", "Ashscar Road", "Ashscar Legion"),
                ("12-17", "Levy Vault of Spears", "Spear Vault Wardens"),
                ("12-18", "Bannerwatch Parapet", "Bannerwatch Guard"),
                ("12-19", "War Engine Yard", "War Engine Cohort"),
                ("12-20", "Balcony of Banners", "Banner Balcony Command"),
                ("12-21", "Hostseal Reliquary", "Host Reliquary Guard"),
                ("12-22", "Crown Rampart Muster", "Muster Rampart Legion"),
                ("12-23", "Thunderbrand Barracks", "Thunderbrand Barracks Wardens"),
                ("12-24", "The Last Host Debt", "Last Host Cohort"),
                ("12-25", "Emberfire Vestibule", "Emberfire Vestibule Guard"),
                ("12-26", "Sky King's Mortal Bastion", "Mortal Bastion Command"),
                ("12-27", "The Broken Host Scepter", "Broken Host Legion"),
                ("12-28", "Stormheart Muster Gate", "Muster Gate Guard"),
                ("12-29", "The Host Dais", "Host High Command"),
                ("12-30", "The Mortal Host", "Mortal Host High Guard"),
            };

            foreach ((string stageId, string title, string enemyName) in chapter12Stages)
            {
                var enemy = new StorySpeaker(stageId + "_enemy", enemyName, "UI/Portraits/Paladin", SpeakerPosition.Right);
                string preEnemyLine = stageId switch
                {
                    "12-1" => "The host gathers. Kneel or march.",
                    "12-15" => "Every banner costs blood.",
                    "12-30" => "The mortal host will not break.",
                    _ => $"{enemyName} holds {title}. The host answers only strength.",
                };
                string prePlayerLine = stageId switch
                {
                    "12-1" => "I march. Make room.",
                    "12-15" => "Then I pay in victories.",
                    "12-30" => "Then I become its tip.",
                    _ => "Clear the path. The host still waits.",
                };

                if (stageId == "12-30")
                {
                    var pre = new StorySequence($"{stageId}_pre", $"Stage {stageId}: {title}");
                    pre.lines.Add(new DialogueLine(enemy, preEnemyLine));
                    pre.lines.Add(new DialogueLine(playerSpeaker, prePlayerLine));
                    sequences[$"{stageId}_pre"] = pre;

                    var post = new StorySequence($"{stageId}_post", $"Stage {stageId}: Cleared");
                    post.lines.Add(new DialogueLine(playerSpeaker, "The host is mine to lead — or to end."));
                    post.lines.Add(new DialogueLine(unknownVoice, "Lead carefully, Sovereign. Hosts outgrow their kings."));
                    sequences[$"{stageId}_post"] = post;
                    continue;
                }

                AddStageDialogue(stageId, title, enemy,
                    preEnemyLine,
                    prePlayerLine,
                    $"{enemyName} scatter, broken.",
                    $"{title} is behind us. The mortal host still waits.");
            }

            // Chapter 13 — The Olympian Answer.
            (string stageId, string title, string enemyName)[] chapter13Stages =
            {
                ("13-1", "Olympian Outrider Post", "Outrider Watch"),
                ("13-2", "Godsworn Beacon Gate", "Godsworn Gate Guard"),
                ("13-3", "Aegis Answer Court", "Aegis Answer Cohort"),
                ("13-4", "Thunder Decree Yard", "Decree Yard Wardens"),
                ("13-5", "Sky-Oath Tithe Hall", "Sky-Oath Legion"),
                ("13-6", "Divine Levy Stair", "Divine Levy Guard"),
                ("13-7", "The Oracle Muster", "Oracle Muster Command"),
                ("13-8", "Boltbound Causeway", "Boltbound Cohort"),
                ("13-9", "Answer Antechamber", "Answer Antechamber Guard"),
                ("13-10", "Eagle-of-Olympus Roost", "Olympus Roost Legion"),
                ("13-11", "Crown Decree Gallery", "Decree Gallery Wardens"),
                ("13-12", "The Judgment Steps", "Judgment Guard"),
                ("13-13", "Stormgod Barracks", "Stormgod Command"),
                ("13-14", "Olympian Oath Chamber", "Olympian Oath Cohort"),
                ("13-15", "The Answer Court", "Answer Court Guard"),
                ("13-16", "Godscar Road", "Godscar Legion"),
                ("13-17", "Levy Vault of Bolts", "Bolt Vault Wardens"),
                ("13-18", "Aegiswatch Parapet", "Aegiswatch Guard"),
                ("13-19", "War Engine of Heaven", "Heaven Engine Cohort"),
                ("13-20", "Balcony of Edicts", "Edict Balcony Command"),
                ("13-21", "Godseal Reliquary", "Godseal Reliquary Guard"),
                ("13-22", "Crown Rampart Decree", "Decree Rampart Legion"),
                ("13-23", "Thunderbrand Sanctum", "Thunderbrand Sanctum Wardens"),
                ("13-24", "The Last Divine Debt", "Last Divine Cohort"),
                ("13-25", "Skyfire Vestibule", "Skyfire Vestibule Guard"),
                ("13-26", "Sky King's Answer Bastion", "Answer Bastion Command"),
                ("13-27", "The Broken God Scepter", "Broken God Legion"),
                ("13-28", "Stormheart Decree Gate", "Decree Gate Guard"),
                ("13-29", "The Judgment Dais", "Judgment High Command"),
                ("13-30", "The Olympian Answer", "Olympian Answer High Guard"),
            };

            foreach ((string stageId, string title, string enemyName) in chapter13Stages)
            {
                var enemy = new StorySpeaker(stageId + "_enemy", enemyName, "UI/Portraits/Paladin", SpeakerPosition.Right);
                string preEnemyLine = stageId switch
                {
                    "13-1" => "Olympus answers. Kneel or burn.",
                    "13-15" => "Every edict costs a kingdom.",
                    "13-30" => "The Olympian answer will not wait.",
                    _ => $"{enemyName} holds {title}. The gods answer only defiance.",
                };
                string prePlayerLine = stageId switch
                {
                    "13-1" => "Then answer me standing.",
                    "13-15" => "Then I take the kingdom.",
                    "13-30" => "Then I rewrite the answer.",
                    _ => "Clear the path. Olympus still waits.",
                };

                if (stageId == "13-30")
                {
                    var pre = new StorySequence($"{stageId}_pre", $"Stage {stageId}: {title}");
                    pre.lines.Add(new DialogueLine(enemy, preEnemyLine));
                    pre.lines.Add(new DialogueLine(playerSpeaker, prePlayerLine));
                    sequences[$"{stageId}_pre"] = pre;

                    var post = new StorySequence($"{stageId}_post", $"Stage {stageId}: Cleared");
                    post.lines.Add(new DialogueLine(playerSpeaker, "The answer is mine — and Olympus heard it."));
                    post.lines.Add(new DialogueLine(unknownVoice, "Heard, Sovereign. Hearing is not surrender."));
                    sequences[$"{stageId}_post"] = post;
                    continue;
                }

                AddStageDialogue(stageId, title, enemy,
                    preEnemyLine,
                    prePlayerLine,
                    $"{enemyName} scatter, broken.",
                    $"{title} is behind us. The Olympian answer still waits.");
            }

            // Chapter 14 — The Fallen Pantheon.
            (string stageId, string title, string enemyName)[] chapter14Stages =
            {
                ("14-1", "Pantheon Ruin March", "Ruin March Watch"),
                ("14-2", "Fallen Idol Gate", "Idol Gate Guard"),
                ("14-3", "Broken Aegis Court", "Broken Aegis Cohort"),
                ("14-4", "Godfall Decree Yard", "Godfall Yard Wardens"),
                ("14-5", "Ash-of-Olympus Hall", "Ash Hall Legion"),
                ("14-6", "Toppled Levy Stair", "Toppled Levy Guard"),
                ("14-7", "The Silent Oracle", "Silent Oracle Command"),
                ("14-8", "Shattered Bolt Causeway", "Bolt Causeway Cohort"),
                ("14-9", "Fallen Antechamber", "Fallen Antechamber Guard"),
                ("14-10", "Eagle-Without-Sky Roost", "Skyless Roost Legion"),
                ("14-11", "Crownless Gallery", "Crownless Gallery Wardens"),
                ("14-12", "The Empty Judgment", "Empty Judgment Guard"),
                ("14-13", "Stormgod Tomb Barracks", "Tomb Barracks Command"),
                ("14-14", "Broken Oath Chamber", "Broken Oath Cohort"),
                ("14-15", "The Pantheon Court", "Pantheon Court Guard"),
                ("14-16", "Godfall Scar Road", "Godfall Scar Legion"),
                ("14-17", "Vault of Fallen Bolts", "Fallen Bolt Wardens"),
                ("14-18", "Aegis-Cracked Parapet", "Cracked Parapet Guard"),
                ("14-19", "War Engine of Ruins", "Ruin Engine Cohort"),
                ("14-20", "Balcony of Dead Edicts", "Dead Edict Command"),
                ("14-21", "Broken Seal Reliquary", "Broken Seal Guard"),
                ("14-22", "Rampart of Fallen Crowns", "Fallen Crown Legion"),
                ("14-23", "Thunderbrand Crypt", "Thunderbrand Crypt Wardens"),
                ("14-24", "The Last God Debt", "Last God Cohort"),
                ("14-25", "Skyfire Grave Vestibule", "Grave Vestibule Guard"),
                ("14-26", "Sky King's Fallen Bastion", "Fallen Bastion Command"),
                ("14-27", "The Broken Pantheon Scepter", "Broken Pantheon Legion"),
                ("14-28", "Stormheart Ruin Gate", "Ruin Gate Guard"),
                ("14-29", "The Fallen Dais", "Fallen High Command"),
                ("14-30", "The Fallen Pantheon", "Fallen Pantheon High Guard"),
            };

            foreach ((string stageId, string title, string enemyName) in chapter14Stages)
            {
                var enemy = new StorySpeaker(stageId + "_enemy", enemyName, "UI/Portraits/Paladin", SpeakerPosition.Right);
                string preEnemyLine = stageId switch
                {
                    "14-1" => "The pantheon falls. Walk the ruins or join them.",
                    "14-15" => "Every fallen god leaves a throne of ash.",
                    "14-30" => "The fallen pantheon has no mercy left.",
                    _ => $"{enemyName} holds {title}. The gods are already broken.",
                };
                string prePlayerLine = stageId switch
                {
                    "14-1" => "I walk. Make a path.",
                    "14-15" => "Then I take the ash.",
                    "14-30" => "Then I end what remains.",
                    _ => "Clear the path. The pantheon still falls.",
                };

                if (stageId == "14-30")
                {
                    var pre = new StorySequence($"{stageId}_pre", $"Stage {stageId}: {title}");
                    pre.lines.Add(new DialogueLine(enemy, preEnemyLine));
                    pre.lines.Add(new DialogueLine(playerSpeaker, prePlayerLine));
                    sequences[$"{stageId}_pre"] = pre;

                    var post = new StorySequence($"{stageId}_post", $"Stage {stageId}: Cleared");
                    post.lines.Add(new DialogueLine(playerSpeaker, "The pantheon is fallen — and I still stand."));
                    post.lines.Add(new DialogueLine(unknownVoice, "Stand carefully, Sovereign. Fallen gods leave hungry voids."));
                    sequences[$"{stageId}_post"] = post;
                    continue;
                }

                AddStageDialogue(stageId, title, enemy,
                    preEnemyLine,
                    prePlayerLine,
                    $"{enemyName} scatter, broken.",
                    $"{title} is behind us. The fallen pantheon still waits.");
            }
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