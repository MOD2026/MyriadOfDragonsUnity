# Campaign Narrative — Chapters 1-3 Continuation (source for StoryDatabase.cs implementation)

**Status:** CC-accepted, ready for Working Hands implementation. Replaces the thin `AddStageDialogue`
filler for the listed stages with real `StorySequence` entries, same pattern as 1-1/1-2/1-3.

## Tutorial framing line

Exact copy: **"Your first formation is a test of command. Place each card, learn the lanes, and survive the opening clash."**
Placement: opening caption in `GameBootstrap.cs`, before the existing `"Cards cost Energy to play..."` step.

## New recurring speakers (Chapters 1-3)

- **Thaleia, the Olympus Envoy** — tall woman in white-gold segmented armor, cracked halo device, blue lightning sigils
- **Rusk Ashrunner** — lean frontier scout, soot-stained cloak, red scarf, bronze prosthetic hand
- **Ione of the Glass Choir** — blindfolded priestess, translucent crystal mask, dark robes, floating glass shards

## Chapter 1 continuation (1-4 through 1-12)

| Stage | Pre-dialogue | Interaction prompt | Post-dialogue |
|---|---|---|---|
| 1-4 | **Rusk:** "Gorn is dead, but his soldiers are not. They are searching the ruins for the signal stone."<br>**Player:** "Then we reach it first." | Search the ruins or pursue fleeing soldiers? | **Rusk:** "You chose the ruins. Good. The stone is warm—and marked with an Olympus seal."<br>**Thaleia:** "You were not supposed to see that." |
| 1-5 | **Thaleia:** "Gorn's defeat was registered above. Withdraw, and Olympus may forget your name."<br>**Player:** "You came all this way to ask?" | Hear her warning or challenge her authority? | **Thaleia:** "You challenged it. Then hear this: the wound is widening beneath the old bridge."<br>**Rusk:** "And every raider in the foothills is being driven toward it." |
| 1-6 | **Rusk:** "The raiders are not attacking for land. They are being herded."<br>**Player:** "By whom?" | Follow the raiders or protect the villages? | **Rusk:** "You protected the villages. The survivors saw a woman with a cracked halo."<br>**Thaleia:** "That was not my order. Someone is wearing Olympus's authority." |
| 1-7 | **Ione:** "The glass showed me three fires: one below, one above, and one inside your own ranks."<br>**Player:** "Which fire do we extinguish first?" | Trust Ione's vision or trust your scouts? | **Ione:** "You trusted your scouts. They found an Olympus relay beneath the bridge."<br>**Thaleia:** "Destroy it, and the signal will become a summons." |
| 1-8 | **Thaleia:** "The relay is a gate-key. Gorn was never the true target—the valley was."<br>**Player:** "Then why warn me?" | Ask what she fears or demand her surrender? | **Thaleia:** "You demanded surrender. I will not give it—but I will tell you the truth."<br>**Thaleia:** "Olympus is divided, and one faction wants your victory turned into a beacon." |
| 1-9 | **Rusk:** "The beacon is active. Villages are seeing stars in daylight."<br>**Player:** "Then we cut its power." | Evacuate the villages or strike the beacon immediately? | **Rusk:** "You struck the beacon. It broke, but something answered from the mountain."<br>**Ione:** "The answer had a name: the Crown Below." |
| 1-10 | **Ione:** "The Crown Below was buried before Olympus had a throne."<br>**Player:** "What happens if it wakes?" | Bury the evidence or descend into the mountain? | **Ione:** "You descended. The chamber was empty except for Gorn's war-banner."<br>**Thaleia:** "His fall was staged. His last command is still moving armies." |
| 1-11 | **Thaleia:** "Gorn's surviving host is marching on the summit, carrying the Crown's broken seal."<br>**Player:** "And Olympus?" | Fight beside Thaleia or keep Olympus out of the valley? | **Thaleia:** "You kept Olympus out. For the first time, the valley stands by its own decision."<br>**Rusk:** "Then let the summit hear it." |
| 1-12 | **Rusk:** "The summit is burning. Gorn's banner flies above the gate, but Gorn is not there."<br>**Ione:** "The Crown Below is speaking through the dead." | Break the banner or confront the voice beneath it? | **Player:** "The banner falls. The voice remains."<br>**Thaleia:** "You have wounded Olympus's enemy—and now it knows your name. Chapter Two will not be a rescue. It will be an invasion." |

## Chapter 2 — Ashes of Boiotia (2-4 through 2-21)

Same three speakers continue.

| Stage | Pre-dialogue | Interaction prompt | Post-dialogue |
|---|---|---|---|
| 2-4 | **Rusk:** "The invasion crossed the eastern ridge before dawn."<br>**Thaleia:** "That army bears Olympus colours, but not Olympus orders." | Warn the villages or secure the ridge? | **Rusk:** "You secured the ridge. The villages still stand—but the invaders now know we are watching." |
| 2-5 | **Ione:** "The ash falling from the sky is not ash. It is memory burned into dust."<br>**Player:** "Then someone is burning history." | Follow the ash or follow the army? | **Ione:** "You followed the ash. It led to a sealed Boiotian road beneath the battlefield." |
| 2-6 | **Thaleia:** "The road leads toward the old city. If it opens, the invasion will bypass every defence."<br>**Player:** "Then the road becomes our battlefield." | Seal the road or use it against the invaders? | **Thaleia:** "You sealed it. Something on the other side answered with a human voice." |
| 2-7 | **Rusk:** "Refugees are gathering at the river. The enemy is using them as cover."<br>**Player:** "No army hides behind civilians and keeps its honour." | Rescue the refugees or pursue the commanders? | **Rusk:** "You rescued them. One survivor carried a burned Olympus writ bearing Thaleia's seal." |
| 2-8 | **Thaleia:** "That writ is forged. Someone wants Boiotia to believe Olympus ordered the slaughter."<br>**Player:** "Then find the forger." | Trust Thaleia or confront her publicly? | **Thaleia:** "You confronted me. Good. Trust that cannot survive questions is not trust." |
| 2-9 | **Ione:** "The forged writ was copied from a voice-recording crystal."<br>**Player:** "Where is the original?" | Search the camp or question the captured courier? | **Ione:** "The courier spoke one name before the crystal broke: the Ash Regent." |
| 2-10 | **Rusk:** "The Ash Regent is no ruler. It is a title passed between bodies."<br>**Player:** "Then we stop the title, not the body." | Hunt the current bearer or destroy the title's records? | **Rusk:** "You destroyed the records. The bearer escaped—but now cannot inherit the next name." |
| 2-11 | **Thaleia:** "The invasion is feeding on Boiotia's old wars. Every burned banner gives it another soldier."<br>**Player:** "Then we deny it the dead." | Burn the banners or preserve them for proof? | **Thaleia:** "You preserved them. Proof matters—but so does knowing what proof can summon." |
| 2-12 | **Ione:** "The banners are not symbols. They are anchors."<br>**Player:** "Then the next battle is against the ground itself." | Break the anchors or bait the enemy into one place? | **Ione:** "You broke the anchors. The army lost its shape, and something beneath the city woke." |
| 2-13 | **Thaleia:** "Olympus has ordered me to return. If I stay, it will declare me an enemy."<br>**Player:** "Then choose where you stand." | Release Thaleia or ask her to defect? | **Thaleia:** "You asked me to stay. I will stand with Boiotia until the truth reaches Olympus." |
| 2-14 | **Rusk:** "The eastern garrison has opened its gates without a fight."<br>**Player:** "A surrender that easy is a trap." | Enter openly or infiltrate at night? | **Rusk:** "You infiltrated. The garrison was empty except for soldiers asleep beneath black ash." |
| 2-15 | **Ione:** "They are not asleep. They are listening."<br>**Player:** "To what?" | Wake them or follow the voice below? | **Ione:** "You followed the voice. It spoke in Gorn's voice and called the player 'the wound.'" |
| 2-16 | **Thaleia:** "The voice is using Gorn's memory to command the invasion."<br>**Player:** "Then we take the memory away." | Destroy Gorn's relics or use them to locate the source? | **Thaleia:** "You used the relics. The trail led to the Boiotian archive, already burning from within." |
| 2-17 | **Rusk:** "The archive holds every treaty between Boiotia and Olympus."<br>**Player:** "And someone wants both sides to forget the treaty." | Save the treaties or save the people trapped inside? | **Rusk:** "You saved the people. The treaties burned—but Ione found one surviving seal." |
| 2-18 | **Ione:** "The seal predates Olympus. It belongs to the first empire beneath the mountain."<br>**Player:** "So this invasion is older than the throne." | Take the seal or destroy it? | **Ione:** "You took the seal. It opened a map showing one final destination: the Ashen Gate." |
| 2-19 | **Thaleia:** "The Ashen Gate is not a fortress. It is a passage for whatever the Crown Below released."<br>**Player:** "Then Boiotia is not being conquered. It is being opened." | Close the gate or cross it? | **Thaleia:** "You closed the gate. The thing beyond it left one message in the stone: 'Olympus is next.'" |
| 2-20 | **Rusk:** "The invaders are retreating toward the summit. They are carrying the last anchor."<br>**Player:** "Then they intend to reopen the gate from above." | Pursue the army or defend the city? | **Rusk:** "You pursued them. The summit is lost—but the city has time to survive." |
| 2-21 | **Ione:** "The last anchor is broken. The invasion has failed, but the sky above Olympus is burning."<br>**Thaleia:** "Boiotia survives because you chose it. Now Olympus will answer." | — | **Player:** "Let it answer."<br>**Thaleia:** "Chapter Three begins where the smoke rises: at the gates of Olympus." |

## Chapter 3 — The Gates of Olympus (3-1 through 3-30, fresh — no existing seed content)

Same cast continues.

| Stage | Pre-dialogue | Interaction prompt | Post-dialogue |
|---|---|---|---|
| 3-1 | **Thaleia:** "The gates of Olympus are burning, but the city behind them is silent."<br>**Rusk:** "Then we do not wait for permission." | Enter through the main gate or climb the outer wall? | **Ione:** "You entered through the main gate. The silence was a welcome—and a warning." |
| 3-2 | **Rusk:** "No guards. No civilians. Only fresh footprints leading uphill."<br>**Player:** "Someone cleared the road for us." | — | **Rusk:** "The footprints end at the Hall of Oaths." |
| 3-3 | **Ione:** "The stones remember thousands of vows. Tonight, they remember only one word: exile."<br>**Thaleia:** "Olympus has already judged us." | — | **Thaleia:** "The judgement seal is false, but the doors obey it." |
| 3-4 | **Rusk:** "A city that locks its own doors is already under siege."<br>**Player:** "Find another entrance." | — | **Rusk:** "We found a servants' passage beneath the western colonnade." |
| 3-5 | **Ione:** "The passage descends below the city, toward the first throne."<br>**Player:** "Then the gates are only the beginning." | — | **Thaleia:** "The throne chamber is empty. Someone removed the crown before we arrived." |
| 3-6 | **Thaleia:** "The crown-bearer was the only voice capable of stopping the invasion."<br>**Player:** "Then we find the voice." | Search the palace or follow the fleeing attendants? | **Thaleia:** "You followed the attendants. They were carrying ashes from the Senate." |
| 3-7 | **Rusk:** "The Senate was not attacked. It was erased."<br>**Player:** "By whom?" | — | **Rusk:** "By soldiers wearing Olympus masks." |
| 3-8 | **Ione:** "Masks do not hide faces here. They replace them."<br>**Player:** "Then we break every mask we find." | — | **Ione:** "The broken masks whispered the same name: Eryx." |
| 3-9 | **Thaleia:** "Eryx was Olympus's First Witness. He disappeared before my initiation."<br>**Player:** "And now he rules from the shadows." | — | **Thaleia:** "He does not rule. He prepares a coronation." |
| 3-10 | **Rusk:** "The coronation bells are ringing below us."<br>**Player:** "Then the city still has a pulse." | — | **Ione:** "It has a pulse—and it is not human." |
| 3-11 | **Ione:** "The bells are waking the buried colossi beneath Olympus."<br>**Player:** "Do we silence them or use their awakening?" | Silence the bells or redirect them toward the enemy? | **Ione:** "You silenced the bells. One colossus still opened its eye." |
| 3-12 | **Rusk:** "The colossus is walking toward the lower districts."<br>**Player:** "Then every minute matters." | — | **Rusk:** "The lower districts are empty. Eryx evacuated them before the bells rang." |
| 3-13 | **Thaleia:** "He wants us to chase the colossus while he takes the upper city."<br>**Player:** "Then we split his attention." | — | **Thaleia:** "Your signal reached the palace. Someone answered from inside." |
| 3-14 | **Ione:** "The answer came through a sealed mirror."<br>**Player:** "Who is behind it?" | — | **Ione:** "A child wearing the crown of Olympus." |
| 3-15 | **Thaleia:** "The child is the last legitimate heir. Eryx needs the heir alive."<br>**Player:** "Then the crown has a hostage." | — | **Thaleia:** "The mirror shattered. The heir is being taken to the upper sanctum." |
| 3-16 | **Rusk:** "We have two paths: rescue the heir or strike Eryx's command tower."<br>**Player:** "A throne without an heir is still a weapon." | Rescue the heir or destroy the command tower? | **Rusk:** "You rescued the heir. Eryx's tower remains, but the city now has a witness." |
| 3-17 | **Ione:** "The heir says Eryx is not seeking the throne."<br>**Player:** "Then what does he want?" | — | **Ione:** "He wants Olympus to kneel voluntarily." |
| 3-18 | **Thaleia:** "Every faction that resisted him has received the same offer: surrender your name, and keep your life."<br>**Player:** "That is not peace." | — | **Thaleia:** "No. It is obedience wearing peace's face." |
| 3-19 | **Rusk:** "The command tower is broadcasting the offer across the city."<br>**Player:** "Then let Olympus hear a refusal." | — | **Rusk:** "The refusal is heard. So is our location." |
| 3-20 | **Ione:** "The tower has marked us as the new enemy of Olympus."<br>**Player:** "We were already marked." | — | **Thaleia:** "Not like this. The city's remaining armies are marching toward us." |
| 3-21 | **Thaleia:** "They are not Eryx's armies. They are frightened citizens wearing armour."<br>**Player:** "Then we must make them choose." | Break their formation or expose Eryx's broadcast? | **Thaleia:** "You exposed the broadcast. The soldiers lowered their weapons—but Eryx opened the sky." |
| 3-22 | **Rusk:** "A black sun is forming above the palace."<br>**Player:** "Another gate." | — | **Rusk:** "The gate is not opening outward. Something is trying to enter Olympus." |
| 3-23 | **Ione:** "The Crown Below is answering the black sun."<br>**Player:** "The thing from Boiotia followed us." | — | **Ione:** "It followed the seal, not us. The seal is inside the heir's crown." |
| 3-24 | **Thaleia:** "The heir must remove the crown before Eryx reaches the sanctum."<br>**Player:** "And if removal kills them?" | — | **Thaleia:** "The crown came free. The heir survived—but the black sun has found a new host." |
| 3-25 | **Rusk:** "Eryx is standing beneath it."<br>**Player:** "Then the First Witness is finally visible." | — | **Ione:** "Visible, yes. Human, no longer." |
| 3-26 | **Ione:** "Eryx offers one final bargain: leave Olympus, and the gates will close behind you."<br>**Player:** "What does he demand in return?" | Accept exile or challenge Eryx before the gate opens? | **Player:** "We challenge him."<br>**Thaleia:** "Then Olympus will witness what its silence created." |
| 3-27 | **Thaleia:** "Eryx has chained the palace to the black sun."<br>**Player:** "Cut the chains." | — | **Thaleia:** "The first chain broke. The palace began to collapse around the sanctum." |
| 3-28 | **Rusk:** "The heir is trapped beneath the throne."<br>**Player:** "Get them out. I will hold the gate." | — | **Rusk:** "The heir is free. Eryx has stepped through the opening." |
| 3-29 | **Ione:** "Beyond the gate is not darkness. It is a road lined with dead stars."<br>**Player:** "Then we close it from both sides." | — | **Ione:** "Eryx is gone, but the road remains open for one breath." |
| 3-30 | **Thaleia:** "The gates of Olympus stand, but the old throne is broken."<br>**Player:** "Then we build no new throne." | — | **Ione:** "The road beyond the gate leads to a sea of dead stars."<br>**Thaleia:** "Chapter Four begins there. Olympus has survived—but something beyond it has learned how to return." |
