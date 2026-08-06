# Story & Tutorial - contributor brief

This folder is the home for the storyline and the new-player tutorial. It is **scaffolding
only** right now: the data shapes and the loader exist and are tested, but no actual story
content has been written and nothing is wired into the game's startup flow yet.

Hand this folder (`Assets/Scripts/Story/`) plus `Assets/Resources/Data/Story/` to whoever is
writing the content.

## What this game actually is (read before writing anything)

Single-player, lane-based card battler. Greek-mythology-adjacent setting; the three card
elements are **Andras**, **Ktini** and **Pnevmas**. The player commands an **Avatar**
("Lightbringer") who fields a squad of cards against an enemy Avatar.

A match runs in three phases:
1. **Formation** - the player places their whole squad across a 3x3 board (3 lanes x 3 slots).
   Front lane grants +1 Attack, Middle grants +1 Health, Back is where draw-hook cards belong.
2. **Combat** - the board fights automatically on a timer. The player's only input is casting
   Avatar spells with Energy that accrues each tick.
3. **Resolved** - one Avatar's Health reaches 0.

Card classes: **Warrior** (+1 Attack), **Knight** (Taunt - absorbs lane damage first),
**Strategist** (draws a card on play), **Perfect** (draws only when played to the Back lane).

The long-term direction is a guild-focused game, but **multiplayer and guilds do not exist
yet** - do not write story that depends on other players, trading, or guild features.

## What already exists here

- `StoryData.cs` - the serialisable shapes (`StoryChapter`, `StoryBeat`, `TutorialStep`) and
  `StoryDatabase`, which loads them from JSON in `Resources/Data/Story/`.
- `Resources/Data/Story/story_chapters.json` - one placeholder chapter, as a format example.
- `Resources/Data/Story/tutorial_steps.json` - the tutorial step list, also placeholder.

The JSON format deliberately mirrors `Resources/Data/card_data.json`, which the game already
loads successfully - same `JsonUtility` wrapper-array pattern, so it works the same way.

## What still needs doing

1. **Write the content.** The two JSON files are the deliverable. No C# changes needed for this.
2. **A story/tutorial UI.** Nothing renders these yet. The battle UI is built procedurally in
   `Assets/Scripts/UI/GameBootstrap.cs` (legacy uGUI - `Canvas`/`Image`/`Text`/`Button`, **not**
   TextMeshPro or UI Toolkit; match that or it won't fit the existing setup).
3. **Hook into startup.** `GameBootstrap.Initialize()` currently drops straight into a battle.
   A first-run check and a tutorial/story entry point would go there.
4. **Persistence.** There is **no save system in this project at all** - progress resets on every
   restart. "Has the player seen chapter 3?" cannot currently be answered across sessions. This
   is the single biggest blocker for a story mode and probably needs solving first.

## Conventions used throughout this project

- Comments explain *why*, not *what*. Several existing comments record a bug that was actually
  hit and why the code guards against it - keep that style.
- Every behavioural change gets an EditMode test in `Assets/Tests/Editor/BattleLogicTests.cs`.
  The suite is run headlessly and is currently green; keep it that way.
- Anything driven by `Update()` or coroutines **cannot** be covered by the EditMode suite. Put
  real logic in plain testable methods and let the MonoBehaviour only supply timing - see
  `BattleController.AdvanceCombatTick()` for the pattern.
