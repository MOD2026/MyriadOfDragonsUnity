# Myriad of Dragons - Unity project

This is the Unity/C# rebuild, replacing the earlier Godot prototype per the engine decision
in `Game Mechanics v2` (see `G:\My Drive\card game\Game mech\MOD\Phase 1\Game mech\
Myriad_of_Dragons_Game_Mechanics_v2.docx` - that's the source-of-truth design doc, and stays
in Drive since it's a small, low-churn file; the live project itself stays on local disk on
purpose - see below).

## Two copies exist, on purpose - this one is the one to actually open

- **This folder** (`C:\Users\zihan\Downloads\MyriadOfDragonsUnity`, local disk) is the live copy -
  open, edit, and run Unity from here. 45 characters, well under the MAX_PATH danger zone; see
  `docs/START_HERE.md` §1 for what a longer one costs.
- **`G:\My Drive\MOD\MyriadOfDragonsUnity`** is the cross-device transfer copy. Move source
  between the two with `G:\My Drive\MOD\Sync-MOD.ps1 -Direction Push|Pull`; setup instructions for
  a second machine are in `G:\My Drive\MOD\OPEN_ME_FIRST.md`.
- The Drive copy **excludes** `Library/`, `Temp/`, `Logs/`, `UserSettings/` - these are
  Unity's own regeneratable caches, not source. Unity rebuilds them automatically on first open
  wherever the project lives. Syncing them is what corrupted the project the first time.
- **Player saves are not synced.** Unity writes `player_profile.json` to that machine's
  `Application.persistentDataPath`, so each device has its own separate progression. Cloud save is
  not built - see `docs/Mechanics_Gap_Analysis.md`.
- **Don't open/run Unity directly from the Drive copy.** Its `Library/`/`Temp/` caches would
  write and rewrite tens of thousands of small files while the Editor runs - pointed at a
  cloud-sync folder, that's a well-known way to get sync conflicts or corrupted caches, and
  this Drive is already slow for simple folder listings. If the Drive copy drifts out of sync
  with this one, treat this local folder as the source of truth and re-copy.
- **Design docs** stay in `G:\My Drive\card game\Game mech\...` - small, infrequent writes,
  genuinely fine for Drive, unlike a live Unity project.
- **Source art** is read from `G:\My Drive\card game\Drawing\...` and copied in (not synced
  live) - see below.

## What's here

```
Assets/
  Scripts/
    Cards/    Card.cs, CardDatabase.cs      - card data model + loader (v2 integer stats)
    Battle/   BattleController.cs,          - the actual match logic: lanes, turns, resource
              LaneBattleResolver.cs,          spending, simultaneous lane clashes, Avatar HP
              LaneState.cs, PlayerBattleState.cs, BattleCardInstance.cs
    Items/    Item.cs, ItemDatabase.cs      - currencies/consumables, real names + icons
    Empire/   PlayerEmpireData.cs           - Castle/Barracks/Gate meta-progression
    Combat/   CardCombatAnimator.cs         - passive-aura event hook for VFX artists
    AI/       SimpleAIOpponent.cs           - placeholder opponent: plays whatever it can
                                               afford, not real decision-making
    UI/       GameBootstrap.cs              - builds the entire first-playable prototype at
                                               runtime (Canvas, EventSystem, every panel) via
                                               [RuntimeInitializeOnLoadMethod] - no hand-authored
                                               scene file needed, same idea as Main.gd in Godot
  Resources/
    CardArt/  44 card portraits              - from the Godot project's validated art/ folder
    Branding/ title_screen.jpg               - same title art used in the Godot version
    Data/     card_data.json                 - same 44-card roster, unchanged from Godot
    UI/Icons/ 15 item/resource icons          - Gold, Food, Dragon Coin, Gem, Flag, HP/Mana
                                                Potion, Spell Book, Dragon Egg, Peace Treaty,
                                                plus a few UI-category icons (attack, card,
                                                empire, knighted) - from Drawing/Jackie/UI/Icons
    UI/Borders/ ornate gold frame graphics    - from Drawing/Jackie/UI/Borders, matches the
                                                title screen's visual language
    Fonts/    ENDORALT.ttf, ENDOR___.ttf     - from Drawing/Reign/Card border - thematic
                                                display font, not yet applied anywhere
  Scenes/     (empty - no scenes built yet)
Server/
  matchmakingValidator.js                    - 10-Level Loot Lock reference implementation
```

## What's actually wired up vs. not

- **Card data loads and computes stats**: `CardDatabase.cs` reads `card_data.json` and builds
  a `Card` per entry using the v2 integer model - resource cost, Attack, Health by rarity,
  Class bonuses. Real, working logic.
- **The battle system is real logic, not stubs**: `BattleController`/`LaneBattleResolver`
  implement the full turn loop and simultaneous lane-clash resolution described in Part II of
  the design doc. I traced every cross-file reference by hand since the compiler can't run
  yet (see below) - there could still be a typo I didn't catch by eye.
- **Items have real names and icons now**, not generic placeholders - `ItemDatabase.cs` maps
  each currency/consumable to an actual illustrated icon. "High Security" was renamed to
  "Peace Treaty" to fit the setting - see the design doc's Part III §3 for why.
- **There is now something to press Play on.** `GameBootstrap.cs` fires automatically on Play
  (via `[RuntimeInitializeOnLoadMethod]`) and builds a full match end to end: enemy lanes
  (read-only), your lanes (tap to place a selected card), your hand (tap to select, disabled
  when unaffordable), an End Turn button, and a win/lose overlay. No scene file was
  hand-authored - once the compile blocker (below) clears, open any scene (even a blank
  default one) and press Play.
- **Both sides currently draft from the same shared 44-card pool** - there's no curated,
  harder-to-beat enemy deck like the Godot prototype's hand-picked formation. A simplification
  to get a match running, not a design decision - revisit once this is actually playable.
- **The opponent (`SimpleAIOpponent`) is a placeholder** - it greedily plays whatever it can
  afford into whatever lane has room. Not real decision-making, just enough for a match to
  resolve.
- **Hand cards now show real card art**, not text only - `Assets/Editor/CardArtImportSettings.cs`
  automatically imports everything under `Resources/CardArt`, `Resources/UI`, and
  `Resources/Branding` as UI Sprites (Texture Type "Sprite (2D and UI)") on import, so there's
  no manual per-image Inspector step. If art was already imported *before* this script existed,
  right-click `Assets/Resources` in the Project window and choose **Reimport** once to pick it
  up retroactively.
- **Lane slots (yours and the enemy's) now show mini card art too**, not text summaries -
  `CreateMiniCardDisplay`/`CreateLaneButton` replaced the old single-line-of-stats-per-lane
  approach. Enemy/Player panels also grew (24%/28% of screen height, up from 20%/26%) to fit
  art at a legible size instead of cramming it into the old text-sized bands. Actual animation
  (particle auras via `CardCombatAnimator`, camera work) is still ahead - this round was about
  the static presentation, not motion.
- **Fixed the resource-curve bug properly this time, with real math behind it**: the first
  attempt (offset +1, Turn 1 = 2 resource) wasn't enough - only 1 of the 44 real cards costs
  2, so a 4-card starting hand still had roughly a 91% chance of zero playable cards. The
  actual rarity counts are 1x2*, 7x3*, 13x4*, 12x5*, 6x6*, 5x7* - `ResourceStartOffset` is now
  +3 (Turn 1 = 4 resource), which covers 21 of 44 cards (~48%) and drops the dead-opening-hand
  odds to roughly 6%.
- **Card selection is now a two-stage "zoom in" flow**, not a single tap: tapping any hand
  card (affordable or not) opens a large centered detail view - full-size art, name, and full
  stats (Attack/Health/Cost/Element/Class) - with a "Select for Play" button that's disabled
  and shows "Need X, have Y" if you can't afford it, or "Deselect" if it's already selected.
  Tapping outside the card, or "Close," cancels without selecting. `ShowCardDetail`/
  `OnCardDetailActionPressed`/`CloseCardDetail` in `GameBootstrap.cs`.
- **Fixed a real bug, not just a guess**: `Button`'s built-in `ColorTint` transition was
  silently overwriting every manual `Image.color` assignment on every state check (creation,
  `interactable` changes) - which is almost certainly why cards looked flat/blank instead of
  showing their dark-red/gold/selected states. `CreateButton` now sets
  `transition = Selectable.Transition.None` so manual colors are the only source of truth;
  `RefreshHand`/`RefreshLaneButtons` now handle the "disabled" look explicitly since Unity's
  automatic graying no longer does it for us.
- **`matchmakingValidator.js` and the Empire/Combat scripts are still standalone reference
  code** - nothing calls them yet; they're for the Kingdom/loot-lock backend and passive-aura
  VFX systems respectively, neither of which this prototype touches.
- **Clarity fixes for "I can't click the cards" / "I don't know how to win"**: cards being
  unclickable was mostly *correct* behavior (not enough Grip of Titans this turn) that wasn't
  communicated - the cost line on an unaffordable card now reads "Need X, have Y" in red
  instead of just going dim, and a "No affordable cards left - press End Turn" hint appears
  above the End Turn button when true. The Enemy panel header now states the win condition
  directly ("bring their Avatar HP to 0 to win"), and Enemy Avatar HP is bigger/bold/gold
  instead of blending in with everything else.

## 2026-08-05 incident: local Assets folder and package manifest got wiped

During an attempt to open the project interactively, something reset the local project close to
a bare default state: `Assets/` was emptied entirely, and `Packages/manifest.json` was reduced
to a handful of default modules (missing `com.unity.ugui`, `com.unity.test-framework`,
`com.unity.timeline`, `com.unity.purchasing`, and others). The exact trigger isn't confirmed,
but it coincided with a series of failed attempts to launch the Editor's GUI from an automation
context. **Lesson for next time: don't attempt interactive (non-batch) Unity launches from a
context that can't host a visible window** - it doesn't just fail cleanly, per this incident.

Recovery:
- `Assets/` was restored from the Drive backup, which was intact.
- `Assets/Editor/CardArtImportSettings.cs` had to be recreated by hand - it was never synced to
  Drive mid-development, so the backup didn't have it either.
- `Packages/manifest.json` was reconstructed by hand from what had been directly verified
  earlier in the same working session.
- `Assets/Scripts/UI/GameBootstrap.cs` turned out to be a **stale** version in the Drive backup
  too (missing the Y-band layout fix, the card art/detail-overlay work, the always-interactable
  hand-card fix, and more) - restoring from Drive silently reintroduced several already-fixed
  bugs, only caught because a manual test screenshot showed the original overlapping-text bug
  again. It was rebuilt from the documented history of each fix, not recovered verbatim.
- Added `GameBootstrap_Initialize_BuildsAPlayableMatchWithARenderedHand` to the test suite (see
  below) so a UI-building regression like this is caught by the automated run next time, not
  only by a human screenshot.
- **Takeaway that actually matters going forward: every file that changes needs to be synced to
  Drive in the same round it changes, not just the ones a given task happens to touch** - this
  whole incident was only possible because `GameBootstrap.cs`, `CardArtImportSettings.cs`, and
  the project root's `README.md`/`Server/` folder had drifted out of sync with Drive over
  multiple earlier rounds. A full `Assets/` sync (not just changed files) was done after this
  incident specifically to close that gap.

## Match economy now scales with Avatar/Castle progression, not a flat constant

Follow-up to the incident above: re-reading every restored battle script against documented
history turned up two more silent regressions from the same stale Drive restore -
`Card.cs` still had Perfect's old flat `cost += 1` tax (should've been removed), and
`BattleController.cs` was missing both `DrawsPerTurn = 2` and Perfect's Back-lane draw hook
entirely. Neither broke anything visibly and neither was caught by the existing test suite,
since nothing asserted on draw count or Perfect's behavior specifically - both are fixed now,
**and** covered by two new regression tests (`BattleController_StartMatch_DrawsTwoCardsPerTurn`,
`BattleController_PerfectCardInBackLane_TriggersDrawHook`) so this can't silently slip through
again.

While in there, `PlayerBattleState.ResourceCap` stopped being a shared constant (10 for every
player) and is now sourced per-match from `PlayerEmpireData`:

- **Deck size**: 10-20 cards (rescaled down from an old 30+ baseline), from Barracks levels
  (`+1 per 5 levels`, capped at 20). Match decks are currently a **random N-card subset of the
  full pool** - a placeholder until a real deck-builder/preset-lineup UI exists, but it makes
  the deck-size number actually observable in-game now instead of just being a formula nobody
  can see.
- **Resource cap**: `8 (base) + AvatarBonus (0-6) + CastleBonus (0-6)`, each bonus `+1 per 5
  levels` and independently capped - Avatar level rewards battle mastery (playing/winning
  matches), Castle level rewards economic investment (base-building), and neither track alone
  can trivialize the match economy. Ranges 8 (level 1) to 20 (maxed).
- **Turn 1 resource**: `round(ResourceCap * 0.6)`, ramping +1/turn after that up to the cap -
  replaces the old flat `+3` offset, so a higher-level player's matches start meaningfully
  stronger instead of every player beginning from the same fixed point.
- **Current test/dev default** (`GameBootstrap.Initialize()`, `PlayerEmpireData.SetLevelsForTesting`):
  Avatar 25, Castle 15, Barracks 25 - a deliberate mid-progression profile, not a fresh level-1
  new player, per direct request. Gives ResourceCap 16, Turn 1 resource 10 (62.5%), deck size 15.
  No real save/progression system exists yet, so this is the only place these levels are set.
- **Card detail overlay** now shows the resource spend before/after playing a card
  (`Resource: 10/16 -> 6/16 after playing`), not just the raw cost number.

**Deliberately not done this round** (scoped, not implemented): lane slot count (`LaneState.MaxSlots`,
currently a fixed 3) does not scale with Castle level - only affordability does. A real
deck-builder/preset-lineup UI ("recommended lineup", "preset lineup") also doesn't exist yet;
the prerequisites (deck size - now decided, 10-20 - and card-ownership rules) needed answering
before that could be scoped further.

## Real combat bug found and fixed: "zero damage to enemy" / Avatar HP crashing fast

Reported directly from a playtest screenshot (Turn 2, player Avatar HP already at 0 while the
enemy's was untouched at 20). Root cause: `LaneBattleResolver` never removed dead cards from a
lane's `Cards` list after combat. That meant a lane, once fully cleared, stayed "full" of 0-HP
corpses forever - which (a) permanently blocked new plays there (`HasOpenSlot` counts all cards,
dead or alive) and (b) kept re-evaluating as `IsFullyCleared` on every later turn too, so a lane
that cleared once **leaked its full overflow damage again on every subsequent turn**, even with
zero new activity in it. That's the actual mechanism behind both symptoms: a player's Avatar HP
crashing fast (one early-cleared lane kept re-leaking full damage every turn after), and zero
damage ever reaching the enemy (their lanes likely never fully cleared even once, so overflow
never fired at all).

Fixed by pruning dead cards from a lane immediately after its clash resolves. Covered by a new
regression test (`LaneBattleResolver_ClearedLaneIsPrunedAndDoesNotLeakOverflowAgain`) that
resolves the same two lanes twice and asserts the second resolution doesn't re-leak the first
turn's overflow - this is a correctness bug, not a balance one, so it's tested as one.

Also added, same request: **StartingAvatarHealth now scales with Avatar/Castle level** the same
way ResourceCap does (20 at level 1, up to 44 at max level, `+2 HP per tier` instead of `+1`
since HP needs to absorb real combat swings where resource just gates spending). This is a
deliberate reversal of `PlayerEmpireData`'s original "Empire levels never touch live-match
stats" principle - see the comment at the top of that file for why.

## Battle scene UI: health bars, resource bar, lane bonus labels

Requested with reference images (AI-generated concept art - useful for the *structural* ideas,
but most of the on-image text is generation-garbled, not a literal spec, so it wasn't read as
one). What's actually feasible in legacy uGUI without commissioned art was implemented:

- Enemy/Player Avatar HP and the Resource readout are now **fill-bar Images** (`Image.Type.Filled`,
  horizontal fill) with a bold text label on top (`HP: 14/20`), not plain text.
- Each lane's tag now shows its actual mechanical bonus (`Front / +1 ATK`, `Middle / +1 HP`,
  `Back` - no bonus) pulled from one shared `LaneBonusLabel()` method, so the label can't drift
  out of sync with what `BattleController.TryPlayCard` actually applies.

**Not attempted** (would need commissioned art/a shader pipeline, not just layout code): custom
illustrated backgrounds, character portraits, glowing rune borders, a turn timer, or an
animated "simultaneous reveal" splash screen - the reference images show real fantasy-game
production art, which this prototype's programmer-art/legacy-uGUI approach can't replicate
without new assets.

## Card detail overlay rebuilt - real bug, not a design nitpick

A playtest screenshot showed the card art going straight to the action buttons with no name,
stats, or cost visible anywhere in between. Root cause: the overlay's internal layout used
fixed pixel sizes/offsets (a 340x520 art box, text positioned at e.g. `y = -548`) computed
against one assumed portrait aspect ratio - on the actual (much wider/shorter) window, that
520px of art alone overflowed past the real panel height, pushing every element below it
completely off-screen. Nothing in the code looked wrong; it only broke at actual runtime sizes.

Rebuilt so every element (class tag, art, name, stats, skill text, buttons) is anchored as a
*fraction* of the panel's actual size via a new `AnchorBand()` helper - never a fixed pixel
value - so it scales correctly at any window size instead of only the one size it happened to
be eyeballed against. **This specific fix could not be verified automatically** (my tests catch
crashes and missing data, not "is this visually laid out correctly") - it needs a look on your
end before I'd call it actually fixed, not just theoretically fixed.

Also added to the same overlay, from the same feedback round:
- **Skill/ability text** per card, from a new `CardSkillDescription()` - describes what the
  card's Class actually does (Knight: Taunt, Strategist: draw on play, Perfect: draw only in
  Back lane), pulled from the same source of truth as `BattleController.ApplyOnPlayClassHook`
  so the description can't drift from the real behavior.
- **Cost is now its own clearly-labeled line** (`Cost 7   Attack 9   Health 6`), not buried in
  a longer stats string.
- **Compact pill buttons**, not full-width bars - the action/close buttons are now inset and
  proportionally sized instead of spanning the full panel width with a lot of empty click area.
- **Deck count** (`Deck: 15`) added to the status row - cards remaining in the draw pile weren't
  shown anywhere before.
- **Card Class badge** on hand card thumbnails (top-left corner, e.g. "Perfect") - the card's
  type wasn't visible anywhere until you tapped in for the detail view before this.

## Match economy, difficulty, and a real "get stronger" loop

Direct feedback after a defeat where the player's Avatar died while the enemy's was still at
30/36 HP - barely scratched. Three separate contributing factors, all addressed:

- **Turn 1 resource is now the full ResourceCap** (`Turn1ResourceFraction = 1.0`, was 0.6) - a
  match starts at full strength instead of ramping into it, per direct request.
- **Deck split is now rarity-balanced, not a pure random slice** (`BuildBalancedDecks()`).
  `SimpleAIOpponent` was already working exactly as documented (greedy, spends its whole hand
  every turn - not a bug), but the old `fullPool.Take(N)` / `.Skip(N).Take(N)` split had no
  fairness guarantee: pure chance could hand one side noticeably more high-rarity cards, which
  reads as "the enemy is just stronger" even though it's sampling variance. The new split
  processes rarity tiers highest-first and always feeds whichever deck is currently smaller, so
  both sides get comparable access to the pool's best cards. Still a placeholder for real
  deck-building (which specific cards, not just how many) - just a fairer placeholder.
- **The actual missing piece: there was no way to get stronger at all.** Avatar/Castle levels
  were hardcoded test constants with nothing feeding into them - winning or losing changed
  nothing. `PlayerEmpireData.ApplyMatchResult(won)` now raises Avatar level on every match end
  (+3 for a win, +1 for a loss - placeholder numbers, not sourced from the design doc), which
  feeds directly into the next match's ResourceCap/HP/deck size. A new **"Play Again" button**
  on the result overlay (replacing the old no-op "Close") actually starts a new match with the
  updated level, and the result text now shows the new Avatar level and what it grants
  (`Avatar Level 28 - next match: 17 Resource, 22 HP`) so the loop is visible, not just a number
  that changed somewhere off-screen. This only persists for the current Play session - there's
  still no save system for it to survive a restart.

**Not addressed this round**: `SimpleAIOpponent` itself is still the documented greedy
placeholder (spends everything affordable, no real strategy) - a genuinely smarter/tunable AI
is separate work from the fairness/progression fixes above.

## 2026-08-05 (later same day): still unwinnable - the enemy was quietly matching the player's own growth

The Play Again/leveling loop above was real, but had a design flaw that made it pointless:
`StartMatch` was called with **the same `MatchEconomy` object for both sides** - meaning every
time `ApplyMatchResult()` raised the player's Avatar level, the enemy's ResourceCap/HP grew by
exactly the same amount, since they were reading the same numbers. Winning never actually got
closer; the target moved with you. Fixed by giving the enemy its own separate, deliberately
low, **fixed** `PlayerEmpireData` profile (pinned at level 1: 8 Resource, 20 HP, 10-card deck)
that does not grow with the player's own progression. `SimpleAIOpponent`'s "spend everything
every turn" aggression is a real challenge on its own without also matching the player's
growing stat totals on top of it. `BuildBalancedDecks` was also extended to fairly split decks
of *different* sizes (player and enemy no longer share one deck-size number either), using each
side's fill-fraction rather than raw card count to decide who gets the next high-rarity card.

This should make an actual win reachable now, but **I can't verify a match outcome myself** -
my tests check that decks/economies are constructed correctly, not that a full multi-turn match
plays out to a specific result, since that requires the same AI-decision replay infrastructure
Unity's headless test runner isn't set up for here. This one needs an actual playtest.

## Combat Design V4 implemented - overtime, Back-lane Energy, elemental advantage, slot weighting

All five items from `docs/Combat_Design_V4_Counter_Proposal.md`, in the order proposed.

**1. Overtime escalation.** `AvatarDamageMultiplier` was flat 6 for all 12 ticks, which made the
tick cap a *result* - hold a lead, refuse to trade, win on Health percentage. It now steps
6 -> 9 (tick 7) -> 12 (tick 10), turning the cap into a deadline. `ResolveTurn` takes a
`tickNumber` (defaulting to 1, so the many call sites resolving a clash in isolation are
unaffected). Spell magnitudes deliberately stay on the *base* multiplier - a printed "150 to
Avatar" should mean the same on tick 11 as on tick 2, or its cost stops being comparable.

**2. Back lane generates Energy.** It previously granted nothing at all - Front gives +1 Attack,
Middle +1 Health, Back neither - so it was pure overflow space. Each living Back card now adds
+2 Energy per clash (three of them take regen 18 -> 24). Back cards still fight; they trade a
stat bonus for spell tempo.

**3. Elemental advantage at lane scale.** Ktini > Andras > Pnevmas, +25% - applied to the lane's
**total** Attack, not per card. A per-card 25% on a 1-12 stat rounds to +1, which is the flat
bonus it was meant to replace, and unevenly (+2 on a 9-Attack card). Lane totals are 15-30, so
the same percentage survives one rounding step and lands at +4 to +7. An evenly-split lane has no
dominant element and gets no advantage, so mixed lanes don't win ties by accident.

**4. Slot weighting.** Rarity 1-4 takes one slot, rarity 5-7 takes two (`Card.SlotWeight`). This
is the fix for high rarity being a strict upgrade: board space, not Resource, is the binding
constraint, so before this the correct play was always "fill 9 slots with the highest rarity you
own". Attack-per-slot now peaks in the middle - rarity 4 at 5.00, rarity 7 at 4.75 with roughly
double the Health per body. `LaneState` gained `SlotsUsed`/`FreeSlots`/`HasRoomFor(card)`;
`HasOpenSlot` now means "room for something", which is not the same question. Two-slot cards
render double width on the board and in the picker so the cost is visible.

**5. Draws locked as unfarmable.** A tie-breaker draw already counted as a loss for progression;
a regression test now pins that, so no future change can make stalling to the cap a way to farm
levels. This closes the V3 proposal's "partial rating to both" exploit before a live-ops economy
exists to be farmed.

### Fallout worth recording

Slot weighting broke three existing tests, all of which were asserting the *old* model rather
than finding real bugs - they counted cards where they now need to count slots. One of my own new
tests was also wrong: it asserted damage dealt to a defender, but damage is capped by that
defender's remaining Health, so a bonus that merely kills it harder is invisible there. Rewritten
to measure *overflow*, with both defenders normalised to exactly 1 Health so the two runs differ
only by element.

A fourth test started silently skipping itself ("could not deploy 3 Knights into one lane") -
correct behaviour under slot weighting, but a test that skips asserts nothing, so it was fixed to
use one-slot Knights rather than left hollow.

Slot weighting also exposed a genuine flaw in **Recommended**: a single strongest-first pass with
per-card lane fallback let an early powerful Warrior spill into the Back lane before any
Strategist was considered, and the tighter slot budget made that common. Now two passes - every
card into its *ideal* lane first, then the remainder anywhere - so class fit beats raw ordering,
which is what "recommended" ought to mean.

45/45 tests passing, none skipped.

## Avatar-portrait regression fixed, bar art salvaged, story rebuilt with artwork and arrows

### The recurring "I can't see what I deploy" - it was a regression I introduced

Reported several times and each time treated as a layout complaint. It was one bug, and it was
mine. The previous round added an `AspectRatioFitter` to `CreatePortrait` to fix ring alignment.
**`AspectRatioFitter` sizes a RectTransform relative to its parent, not to its own anchors** - so
`FitInParent` measured against the entire panel and discarded the anchors above it, inflating the
Avatar portrait into a huge centred square sitting on top of the lane slots.

That single mistake produced three separate reports: deployed cards invisible, the Avatar
appearing in the centre of the screen, and "the cards went missing again". Fixed by moving the
fitter onto an inner child, so it squares against the correctly-anchored box instead of the
panel. The ring stays concentric, which was the point of the original change.

Lesson worth keeping: a Unity layout component that reads from the parent will silently override
anchors set on the same object, and the symptom shows up somewhere else entirely.

### The bar art, finally used

Held back twice because every one of those PNGs has its own name painted across it ("FULL HEALTH
BAR", "MANA BAR", "EMPTY HEALTH BAR"). Rather than keep refusing them, each was rebuilt by
compositing its ornate left cap and arrow right cap around a slice taken from the clear
right-hand stretch, which skips the caption entirely. Results in `Resources/UI/Bars/`
(Health_Fill, Health_Empty, Mana_Fill), imported with a horizontal-only 9-slice border so only
the middle stretches. Bar labels gained an outline, since the art is far busier than the flat
procedural fill it replaced.

### Story rebuilt - artwork, typewriter, pointing arrows

"The story is just clicking Next with no animation or change of background - this is not what I'm
looking for." Correct, and now:
- **Full-screen landscape artwork** per beat, fading in, loaded from
  `Resources/Story/Backgrounds/`. A beat with an empty `background` keeps the previous one, so a
  run of dialogue in one location only names it once. The modal dim dropped from 0.72 to 0.38 -
  the artwork is the point now.
- **Typewriter text.** Tapping mid-line completes it rather than skipping it, the usual
  convention - a typewriter you can't skip is worse than none.
- **A bouncing arrow that points at the actual UI element** each tutorial step describes,
  resolved by GameObject name from the step's `highlight` field. Built from the uploaded
  `Target_Arrow.png` with its near-black backing keyed out to transparency
  (`Resources/UI/Icons/Tutorial_Arrow.png`). It nudges horizontally, not vertically, because the
  art points right - a vertical bounce would read as unrelated motion.

**Backgrounds are currently the arena landscapes already in the project.** Walking
`G:\My Drive\card game\Drawing` timed out (large network drive), so rather than stall, the system
is wired and seeded with what was to hand. Drop chosen landscapes into
`Assets/Resources/Story/Backgrounds/` and name them in `story_chapters.json` - no code change
needed.

### Also this round

- **START BATTLE** is a corner button rather than a centred bar.
- The `^ +1 ATK  = +1 HP` legend removed again - substituting ASCII glyphs for icons produced
  something that read as neither. Those rules are taught by the tutorial now, which is where a
  rule that never changes belongs.
- **Name-plate rectangles removed** from behind the Avatar names, replaced by an outline - a dark
  rectangle behind a circular portrait is the boxiness being designed out.
- **Spell buttons** given a proper square icon and 15pt left-aligned text filling the button,
  instead of a thumbnail and a small centred label on a mostly-empty bar.

37/37 tests passing.

## Lane picker replaces card-then-lane selection; story and tutorial now actually play

### The lane picker

Tap a lane, see its three slots and every card you could put in them, fill it, close. This
replaces select-a-card-then-tap-a-lane, which made the *card* the unit of decision and the lane
an afterthought - two taps per card, with the hand and the board at opposite ends of the screen.

Lane choice is the real strategic decision here (Front trades Health for Attack, Middle is where
Taunt belongs, Back is where draw hooks fire), so the lane is now what you commit to, with its
three cards chosen together and comparable side by side.

This needed a new `BattleController.TryRecallCard(lane, slot)`: a deployment UI you can't undo
isn't a picker, it's a one-way commit. Recall refunds the card's Resource and returns it to hand,
Formation-only - the squad still locks the moment combat starts, which is what makes the
reinforcement windows meaningful. One deliberate asymmetry: an on-play draw that already fired
(Strategist) is **not** un-drawn, because that would mean choosing which card to put back and in
what order, and recalling a Strategist to try another lane isn't abusive.

### Story and tutorial - playing, not just scaffolded

Written from the uploaded campaign document: the prologue plus Acts I-III of Season 1 (The Broken
Throne, Ashes of Boiotia, The Sun God's Wrath), following Kain's destruction of Artazostre and
the Olympian power vacuum. Ten tutorial steps cover the goal, lanes, the picker, Resource,
synergy, starting the battle, automated combat, spells, reinforcement windows, and the tick cap.

Both play through one overlay - they're the same shape on screen (portrait, speaker, text, Next),
so building two would have meant maintaining the same layout twice. Content lives in
`Resources/Data/Story/*.json`, so it can be rewritten without touching C#. Tapping anywhere
advances; Skip is honoured permanently.

**On the persistence blocker**: first-run gating uses `PlayerPrefs`. That does *not* solve the
missing save system - it can't hold a card collection or match history - but "has this player
seen the intro" is exactly the single flag PlayerPrefs exists for, and gating on it beats
replaying the intro on every launch. A real save layer is still needed for anything else.

A test now asserts the shipped story and tutorial text contains no `PLACEHOLDER` - the scaffold
content was written in capitals specifically so that check could catch it.

37/37 tests passing.

## Match stall fixed, reinforcement windows, leftover cards and Resource given a purpose

### The match could hang forever - a real bug, caught in play

Once both squads wipe each other out, every lane is empty, total Attack on both sides is 0, and
no further damage is possible. The match simply never ended. Observed directly: Clash 7, both
boards empty, both Avatars stuck above half Health, nothing happening. Spells looked broken for
the same reason - `LaneDamage` into an empty lane hit nothing, so the enemy's health "did not
move after casting".

Two fixes:
- **`MaxCombatTicks = 12` with a Health-fraction tie-breaker.** Compared as a *fraction* of each
  side's own maximum, not raw HP - raw HP would hand the win to whoever started with the bigger
  pool, which the AI routinely does at higher tiers. A cap-decided match now says so
  ("VICTORY on Health after 12 clashes - 91% vs 76%") instead of claiming an Avatar fell.
- **Damage spells reach the Avatar through an undefended lane**, scaled by the same
  `AvatarDamageMultiplier` lane overflow uses. This is the rule lane combat already follows; the
  spell was the odd one out.

### Leftover cards and Resource now do something

Both were dead weight the moment formation locked - your own observation. **Reinforcement
windows** open on Clash 4 and Clash 8: the hand reappears, and cards can be deployed into open
slots for **Resource** (not Energy, so reinforcing never competes with casting for the same
pool). Reinforcements go through `TryPlayCard`, so they still get lane bonuses and class hooks,
and synergy is recalculated so a reinforcement can complete a pair or trio - but only the
arriving unit is buffed, since re-buffing the standing squad would stack the same bonus again
every window.

Gated to two specific ticks rather than always-open on purpose: a player who can top up a lane
every tick can chump-block indefinitely, which is the exact stalling problem the cap exists to
stop.

### Screenshot round 6

- **Lane labels replaced by one legend** next to the deck counter. Six copies of three static
  rules (both boards, three rows each) was clutter, not help.
- **"Your Lanes (tap a card...)" removed** - another permanent instruction banner, and it was
  overlapping the top lane row.
- **Avatar ring alignment fixed.** Portrait and ring each applied `preserveAspect` inside a
  *non-square* box; since the two sprites have different native aspects they centred at
  different sizes, so the ring sat visibly off the face. Both are now forced to the same square
  via `AspectRatioFitter`, making them concentric by construction rather than by luck.
- **Lineup buttons moved right** (asymmetric padding) - secondary actions, so they sit under the
  player's corner and leave the centre for the board.
- **Spell buttons now carry icons** from the uploaded StatusIcons set, dimmed rather than hidden
  while unavailable so the bar's shape stays stable.

35/35 tests passing, including four new ones covering the stall, the tie-breaker, the
reinforcement window gate, and spell damage reaching an Avatar through an empty lane.

## Art cropping solved properly, HUD bands removed, impactful spell casts

### The art stretching - third attempt, and this one is actually right

`preserveAspect = true` fits art *inside* its box and leaves blank margins ("unused space").
`preserveAspect = false` fills the box but squashes the picture ("way too overstretched"). Both
were tried and both were reported. Neither is what a card game does.

New `CreateCroppedArt()` uses Unity's own `AspectRatioFitter` in **EnvelopeParent** mode inside a
`Mask`: the image scales until it *covers* the box at its true aspect ratio, and the mask crops
the overhang. Fill and undistorted. Applied to hand cards, deployed units, and the detail popup
(where the fitter is re-pointed per card, since one popup shows every card and they aren't all
the same shape).

### Screenshot round 5

- **HUD panel bands removed entirely** (`HudPanelAlpha` 0.6 -> 0). Every element that needs
  contrast now carries its own dark plate, so the tinted bands were only covering the arena art.
  `CreateGradientBandPanel` skips drawing at 0 rather than adding an invisible full-screen
  Graphic for the canvas to batch.
- **Card names back on hand cards**, readable at 13pt on their own plate. They were dropped last
  round to buy font size for the stats; a hand you can't identify by name is worse than tight
  numbers. Class badge also 8pt -> 11pt.
- **Bars and buttons narrowed again** - `BarXInset` 0.22 -> 0.30, lineup padding 170 -> 240,
  spell bar padding 10 -> 150.
- **Lane slots spaced** 14 -> 26.
- **Popup is now just the frame PNG.** The "purple area" was the panel's own rectangular fill
  showing outside the frame's ornate silhouette; the frame art already has transparent corners,
  so nothing is drawn behind it any more.
- **Leftover hand cards hidden once formation locks** - they can never be played after that
  point, so they were pure clutter over the battle.
- **Synergy text no longer overlaps the HP bar.** With Wrap it broke "TacticalCommand" across
  three lines and spilled upward; now best-fit and truncated, since the status row is one line
  tall by design.

### Impactful spell casts

A single small sprite fading over one lane was reported as "not appealing at all". A cast now
lands as three layered beats - a full-screen colour wash tinted per spell school, a much larger
effect burst (80 -> 190) over the target lane, and the spell's name punched over the board. Heals
and buffs fire over the *player's* lane; firing a heal effect over the enemy board would read as
damage. All built on the existing PlayEffect/PlayFloatingText coroutine template, no new system.

### On the attached "Mobile UI Canvas Layout Controller" doc

The `MobileCanvasLayoutController.cs` in it is a scene-authored component - it expects
`[SerializeField]` RectTransforms wired up in a prefab. This project builds its entire UI
procedurally at runtime from `GameBootstrap`, with no scene assets at all, so it cannot be
dropped in as written. **The safe-area idea it covers is genuinely worth having for notched
phones** and would need re-expressing as procedural anchor maths; noting it here rather than
half-adding it.

The doc's 12-Act / 4-Season live-ops narrative is real, usable story material - it belongs in
`Assets/Resources/Data/Story/`, which is exactly what that scaffold is for. Its file index also
lists a number of paths that don't match this project (`SoloPlayerProgression.cs`,
`PreBattleSpellEngine.cs`, `FormationSkillSynergyEngine.cs`) because those designs were adapted
into the existing files rather than added alongside them - see the sections below.

31/31 tests passing.

## Combat pacing fixed, formation synergy added, Story/tutorial scaffold

### "The battle happens too fast - the player can't even click on the energy"

Root cause: `LaneBattleResolver.AvatarDamageMultiplier` was still 25, a value tuned when two
things were true that no longer are. (a) An undefended lane used to absorb everything and deal
no damage at all - that was fixed on 2026-08-05. (b) The turn-based model refilled the board
every turn - the formation model never replenishes a dead unit. Together those mean that once a
side's lanes empty they *stay* empty and take the full attack every tick: roughly
3 lanes x ~12 Attack x 25 = ~900 damage a tick into a 1300 HP pool, so a match ended in about
two ticks. Cut to **6**, tick interval 1.6s -> 2.2s, Energy 12 -> 18 per tick. A match should now
run ~10-14 ticks, which is the minimum for the active-spell layer to be usable at all - it's
pointless if the fight is over before the first spell comes off cooldown.

The test that covered this multiplier hardcoded `25` as a literal and failed the moment it was
retuned, making a deliberate balance change look like a regression. The constant is now public
and the test asserts the *relationship* instead.

### "What are the 4 buttons?"

They're the Avatar spells - but they were rendering **under the victory screen**, where four
unlabelled fantasy nouns sat below a finished match with nothing to cast at. Now Combat-phase
only, and each button says what it does: "Firestorm / 4 dmg to a lane / 30 Energy".

### Skill tags and formation synergy - `Assets/Scripts/Cards/CardSkills.cs`

Adapted from the `CardEvolutionFusionEngine` spec. Field 2+ cards sharing an affinity tag and
the whole squad gets stronger, resolved once when formation locks and shown live in the status
row (a preview while you're still placing, the applied value afterwards).

Deliberate departures from that spec:
- **Bonuses are flat Attack/Health, not `GlobalSkillTriggerBonus` / `UltimateMeterGainMultiplier`.**
  Neither of those exists here: every class hook is deterministic (Knight always taunts,
  Strategist always draws) so there is no trigger *chance* to raise, and there is no Ultimate
  meter at all. A percentage bonus on a system that doesn't exist is invisible; and on a
  3-Attack card a percentage rounds away to nothing under the Integer Model anyway.
- **Tags are derived from Class and Element**, not stored per card. `card_data.json` has no tag
  field and hand-authoring one for 85 cards would be guesswork.
- **Per-card skill XP/levelling and duplicate fusion (level caps 25 -> 100) were left out.** Both
  need a collection and a save system to mean anything, and there is no persistence in this
  project at all - a card's level would reset every restart. They belong with the collection
  layer, not the battle layer.
- The spec's `TryFuseDuplicate` and `FeedCardForSkillXP` were declared as instance methods inside
  a `static class`, which does not compile in C#.

### Screenshot round 4

- **Lane row shading removed entirely** - even at 0.12 alpha the tint spanned the full row and
  read as a long empty bar. Rows are now fully transparent (an alpha-0 Image still receives
  clicks, which is what keeps lanes tappable) and only tint when they're a valid target.
- **"Enemy - bring their Avatar HP to 0 to win" removed** - that's tutorial text and belongs in
  the tutorial, not permanently across the top of every battle.
- **Avatar name plates** under both portraits, per the reference mockup's DREADLORD/LIGHTBRINGER
  labels; player portrait dropped further down.
- **Deployed units rebuilt** ("how crap the cards are"): they were the flat slot plate with art
  letterboxed into the top 70% and a 9pt "3/5" strip underneath - a smudge in a box at 66x78.
  Now the same rarity frame as the hand card, art filling the tile, and Attack/Health on corner
  chips like the reference. Ownership moved to a coloured rim.

### Story / tutorial scaffold - `Assets/Scripts/Story/`

Data shapes, a JSON loader, placeholder content, and `README_FOR_CONTRIBUTORS.md` explaining the
game, the format and the conventions - ready to hand to someone else. **Scaffolding only: no
content is written and nothing is wired into startup.** The blocker worth knowing up front is
that there is no save system anywhere in this project, so "has this player seen chapter 3"
cannot survive a restart; that probably needs solving before story mode is real work.

31/31 tests passing. A second test was found to be flaky along the way - it asserted a lane
*count* while `PlayerBattleState` shuffles with a time-seeded `System.Random`, so it passed or
failed on the draw rather than on the behaviour. Now asserts the lane *ordering* instead.

## Recommended now deploys the squad; screenshot round 3; battle + selection animations

### "Recommended is not working" - it was doing an invisible thing

It only ever rebuilt the *deck*, which the player never sees: the board still came up empty and
all nine cards still had to be placed by hand. Under the formation model the useful thing to
recommend is the formation itself, so `AutoDeployRecommendedFormation()` now actually places the
squad: strongest cards first (so if resource runs out it's the weakest left behind), each into
the lane that suits its class - Knights to Middle where +1 Health compounds Taunt, Strategists
and Perfects to Back where Perfect's draw hook fires at all, everything else to Front for
+1 Attack, with the other lanes as fallbacks so nothing is stranded in hand.

### Screenshot feedback (attachment 1 - battle scene)

- **Title removed.** A permanent "Myriad of Dragons" banner across the battle screen is menu
  chrome, not gameplay information, and it was eating a band the board could use.
  `BuildTitlePanel` is kept for a future main menu rather than deleted.
- **Every full-width bar inset** via a single `BarXInset` constant (both Avatar HP bars, START
  BATTLE, Reset/Recommended). A bar pinned edge to edge reads as a UI band rather than a discrete
  element.
- **Player avatar dropped lower** - it sat level with the lane rows and read as part of the board
  rather than as the player's own corner.
- **Hand cards reworked**: art now runs almost the full card (was 0.44-0.94 of the height, leaving
  a large blank strip - the "unused space" mark), with stats on a dark plate overlaying its lower
  edge. Dropped the card name from the thumbnail (it's on the badge row and in the popup) which
  bought the font size to actually read the numbers, 10pt -> 12pt.
- **Slots spaced out** - spacing 6 -> 14 and slot size 58x68 -> 66x78, for the cramped board in
  attachment 3.

### Screenshot feedback (attachment 2 - card detail popup)

- **Panel narrowed again**, 0.26-0.74 -> 0.33-0.67, removing the blank frame either side.
- **`preserveAspect` turned off on the art.** With it on, portrait art inside a wider frame box
  letterboxed itself and left blank frame either side - the remaining "unused space" marks. The
  box is now close enough to card-shaped that filling it distorts far less than the margins it
  removes.
- **Blurry card name fixed**: it was using the decorative ENDORALT display font, which renders
  soft at that size. Now the default font. A display face is worth it on a title; on the line
  that tells you which card you're looking at, legibility wins.
- Buttons narrowed further (0.16 -> 0.22 / 0.30 -> 0.34 inset).

### Animations

- **Card pop on selection** - a scale punch when a card is chosen, so picking has a visible
  reaction rather than only a colour change. It has to be started *after* `RefreshAll()`, since
  that destroys and rebuilds every hand button and any reference captured beforehand is dead.
- **Clash effects during combat** - every lane that traded damage gets an impact effect each
  tick, with a heavier one when a lane actually breaks (the moment damage reaches an Avatar).
  Directly addresses "the mid game play can't be seen": the automated phase previously had no
  visible activity at all between health-bar updates.

2 new tests (26 -> 28, all passing) covering that Recommended actually populates the board and
respects class lane preferences. **The animation coroutines remain Play-Mode-only and unverified
by me** - same standing caveat.

## New battle model: pre-battle Formation + automated combat + Avatar spells

Replaces turn-by-turn manual card loading, which was the main source of play fatigue - you
deployed a couple of cards, hit End Turn, and repeated that a dozen times. Adapted from an
external design spec (`PreBattleSpellEngine.cs`), but **built onto the existing systems rather
than dropped in as a parallel engine**, which is the explicitly chosen approach: `Card`,
`CardDatabase`, decks, the resource economy, the AI archetypes and all existing tests keep
working, and the class skills and lane bonuses are untouched.

**The flow now**: `BattlePhase.Formation` -> `Combat` -> `Resolved`.
- Formation deals the whole squad hand at once (up to the 3x3 board capacity) and lets you place
  freely. Lane bonuses (+1 ATK Front / +1 HP Middle) and class hooks (Taunt, Strategist/Perfect
  draws) apply exactly as before - this is still `TryPlayCard`.
- `ConfirmFormation()` locks both squads in; the AI commits its whole formation at the same
  moment, keeping the simultaneous-commit principle the lane clash already used.
- `AdvanceCombatTick()` then runs the fight on a 1.6s timer: energy accrues, cooldowns count
  down, lanes clash. No more drawing or resource gain - the squad is locked.
- Avatar spells are the active layer: Firestorm (lane damage), Mend (lane heal), War Cry (lane
  +2 Attack), Divine Bolt (150 direct to the enemy Avatar).

**Deliberate changes from the source spec, all for concrete reasons:**
- **Energy accrues per combat tick, not per frame.** The spec's
  `CurrentEnergy + Mathf.RoundToInt(5.0f * Time.deltaTime)` rounds to **0 every single frame at
  60fps** and never accrues anything at all - spells would have been permanently uncastable.
  Per-tick accrual is also deterministic and EditMode-testable; a float timer is neither.
- **Combat logic lives in a plain `AdvanceCombatTick()` method**, not inside `Update()`. The
  coroutine in `GameBootstrap` only supplies the passage of time. This is what keeps the whole
  new model covered by the test suite - the spec's `Update()`-driven version could not have been
  tested at all.
- **Taunt still spills over.** The spec's `taunts.Count > 0 ? taunts : group` targets *only*
  taunts, so once they die damage stops instead of reaching the rest of the lane. The existing
  `taunts.Concat(others)` behaviour is kept.
- **Overflow is still leftover damage**, not the full lane attack. The spec sent the entire lane
  attack to the Avatar *on top of* the damage already spent killing defenders - roughly a double
  count.
- **No percentage lane bonuses.** The spec's `+20%` is a no-op on this game's stats:
  `(long)(3 * 1.20f)` is 3. Same Integer Model collision as the previous file's
  `CardStatMultiplier`. The existing flat +1 bonuses are kept.
- **Healing caps at a unit's starting Health and never revives the dead** - resurrection would
  quietly undo the lane-clearing rules that overflow damage depends on.
- `StatBuff`/`Shield` were declared but unimplemented in the spec; the effect list here is
  shorter and every entry actually does something, since a declared-but-unhandled effect
  silently does nothing at runtime and reads as a bug.

**HUD**: the resource bar becomes the Energy bar during Combat (resource is spent entirely in
Formation and is meaningless afterwards), and the bottom row swaps between the START BATTLE
button and the spell bar. Each spell button shows its cost or its remaining cooldown rather than
just going dead.

**Recommended lineup confirmed as highest-stats-only** - picking purely on Attack+Health and
ignoring class skills and synergy is the intended behaviour, per direct confirmation.

6 new tests (20 -> 26, all passing), covering formation dealing, the empty-board guard, per-tick
energy accrual, no-draw-during-combat, spell cost/cooldown rules, AvatarStrike, and the heal
cap/no-revive rules. The 1.6s tick coroutine itself is still Play-Mode-only and unverified by me.

## Zero-damage root cause found, screenshot feedback round, card animations

### The "I deal zero damage to the Avatar after 2 rounds" bug - a real one

`LaneBattleResolver.ApplyDamageToLane` only released overflow damage when `lane.IsFullyCleared`
was true. That property is defined as `Cards.Count > 0 && all dead` - so a lane that was simply
**empty** returned false and swallowed the entire attack. Attacking a completely undefended lane
dealt **zero damage to anything**, which meant leaving a lane empty was a perfect defence and
matches stalemated. Overflow now keys off "no living defenders", covering both an emptied lane
and a never-occupied one.

This **deliberately changes the semantics** an existing regression test asserted, so that test
was rewritten rather than deleted: it still guards the part of the original dead-card bug that
must not return (a lane with no living attacker opposite must leak nothing), but now asserts
that a surviving attacker facing an empty lane *does* reach the Avatar.

### "Recommended" was picking weaker cards than "Reset"

Measured by a new test before fixing: 12.9 average Attack+Health versus Reset's 14.9. The
cost-curve logic forced a third of the deck to be cost-1-2 cards (rarity 1-2, the weakest in the
game) while Reset's rarity-fair split handed out mostly rarity 5-7. The curve was **dropped
entirely**, not reweighted, because resource isn't a binding constraint under the current economy
(20 Resource at level 1 against a max card cost of 7; 60 at mid-level) - board space is. So the
best card for a slot is just the strongest one. `BuildCurveAwareDeck` -> `BuildStrongestDeck`.
Flagged in the code: if card costs or the resource formula are ever retuned so cost genuinely
bites, this needs revisiting.

### Screenshot feedback (attachment 2 - battle scene)

- **Extra bars removed**: each lane row stretched the full panel width while its 3 slots occupied
  only the left portion, so the remaining ~40% rendered as a long empty tinted bar - the marks
  down the right side. Rows are now only as wide as their content (`EnemyLaneX0/X1`,
  `PlayerLaneX0/X1`), with the far side left clear for the Avatar. End Turn and the lineup
  buttons are inset rather than full-bleed.
- **Avatars**: enemy pinned top-left, player bottom-right (diagonal corners, per the mockup), and
  both made much larger - they were a 0.22-height sliver before.
- **Text sizes**: lane tags 10pt over two lines -> a single 15pt line; status row (Resource /
  Turn / Deck) 13-15pt -> 17pt. These are the numbers checked most often mid-turn.
- **Cards no longer covered**: the hand band was too short for the 180px card buttons so they
  overflowed under the End Turn bar. Y-bands rebalanced to give the hand the largest share.
- **Lane taps now register**: lanes were left non-interactable whenever no card was selected, and
  with `Transition.None` that means *no feedback at all* - tapping a slot did nothing and looked
  broken. Lanes are always tappable now and every rejected tap explains itself ("Tap a card in
  your hand first", "Front lane is full", "Not enough Resource"). Valid targets also highlight.

### Screenshot feedback (attachment 3 - card detail popup)

- **Panel narrowed hard**, 0.08-0.92 -> 0.26-0.74. It was 84% of the screen wide while its only
  real content is a portrait-shaped card and two lines of text, so most of it was empty stone
  texture - on a wide desktop window, most of the screen.
- **Border now at the card**: the rarity frame sits tight around the art (0.06 inset) instead of
  floating in a much wider box.
- **Text highlighted**: a dark plate behind the stat/skill text. These previously sat directly on
  the frame's mottled stone texture - the colour was never the problem, the busy backdrop was.
  Sizes up too (16->19 stats, 15->16 skill, 24->26 name).
- **Buttons slimmed**: action and Close were near-full-panel-width bars; now compact centred pills.

### Animations

- **Idle shimmer** on hand cards - a 3% scale breathing pulse over ~2.4s, staggered per card so
  they don't pulse in lockstep. Deliberately subtle: it runs on every card forever, so anything
  more pronounced reads as flicker.
- **Slide into lane** - a played card eases up into its slot from below with a fade, rather than
  teleporting onto the board.

Both check `Application.isPlaying` before starting, because `StartCoroutine` throws outside Play
Mode and `RefreshHand()` is reachable from the EditMode tests. **Same standing caveat as before:
coroutines can't be exercised by an EditMode suite, so these two are compile-verified and
reasoned-through but not runtime-verified by me.**

20/20 tests passing. Synced to Drive.

## Solo AI now scales with the player - the actual "it's unbalanced" fix

Adapted from an external design spec (a second Gemini pass, this one genuinely written against
this game's real systems - lanes, Taunt, Avatar HP, resource cap). New file
`Assets/Scripts/AI/AIOpponentScaling.cs`, plus a rewrite of `SimpleAIOpponent.cs`.

**The root problem it fixes**: the enemy was pinned at Avatar 1 / Castle 1 forever while the
player gains +3 Avatar levels per win, so the game got monotonically *easier* the longer it was
played. (That pinning was itself a fix for an earlier bug where the enemy mirrored the player's
economy exactly - it overcorrected.) The opponent is now derived from the player's own
progression every match via `SoloAIScalingSystem.GenerateAIOpponent()`.

**What was adapted rather than taken as-is:**
- `SoloPlayerProgression` -> `PlayerEmpireData`, `CalculateMaxHealth()`/`CalculateResourceCap()`
  -> the `StartingAvatarHealth`/`ResourceCap` properties this project actually has; `long` ->
  `int` throughout (the whole battle stack is `int`).
- **Tier thresholds rescaled.** The spec used 1-300 bands (Novice 1-30 ... Titan 221-300), but
  this game's player stat growth *caps at Avatar 30* (both `MaxAvatarResourceBonus` and
  `MaxAvatarHealthBonus` stop increasing there), so Veteran/Master/Titan would have been
  functionally identical - the player's own numbers stop moving long before level 150. Rebanded
  to 1-10 / 11-25 / 26-50 / 51-80 / 81+, which matches the curve that actually exists.
- Opponent names made **deterministic per tier** rather than randomly picked from a pool - a
  random name each match looks like a different opponent when nothing has changed.

**Three spec fields are declared on `AIBattleProfile` but deliberately NOT applied**, each
because it depends on a system this game doesn't have. They're left as explicit unapplied hooks
with comments rather than silently dropped:
- `CardStatMultiplier` - there is no card-stat scaling mechanism, and adding one cuts against
  Game Mechanics v2 Part II §3's "Integer Model" (card Attack/Health stay whole numbers 1-12 so
  board math is computable in your head). A 1.45x multiplier turns a 3-Attack card into 4.35.
  **Whether to break that rule for AI difficulty is a design call, not a coding one** - flagging
  it rather than deciding it unilaterally.
- `SkillTriggerBonus` - every class hook here is deterministic (Knight always taunts, Strategist
  always draws on play, Perfect draws in Back). There is no trigger *chance* to give a bonus to.
- `DecisionDelaySeconds` - the AI runs synchronously inside the End Turn handler; simulated
  think-time needs a coroutine, which only runs in Play Mode and can't be covered by the
  EditMode test suite.

HP/resource scaling plus archetype placement is enough to fix the imbalance on its own.

**The AI also actually plays better now** (`SimpleAIOpponent`), which is a bigger real change
than the stat scaling. It used to walk the hand in arbitrary order and drop each card into the
first lane with a free slot. It now:
- spends on the **strongest card it can afford** first, because board slots (3 lanes x 3) run out
  well before resource does under the current economy, and
- places by **archetype** - Aggressive stacks Front (+1 Attack), Defensive banks Middle (+1
  Health) then Back, Balanced fills the emptiest lane, Tactical contests whichever lane the
  player has committed most to (a lane only overflows to Avatar HP once *fully cleared*, so
  meeting their biggest stack is what actually denies damage).

`IBattleOpponentConfig` was kept from the spec as-is - it costs nothing now and lets the
multiplayer/guild phase supply a human opponent to the same battle-setup path later.

Enemy deck size now simply matches the player's, rather than coming from a separate pinned
Empire - deck size is a poor difficulty lever against a board that fills long before a 10-20
card deck runs out, so making it uneven mostly just looked unfair.

**5 new tests** (12 -> 17, all passing). One of them caught a wrong assumption of mine rather
than a code bug: I'd asserted a Defensive AI would leave Front empty, but it correctly fills
Middle -> Back -> Front, and the Strategist/Perfect draw hooks refill its hand while it does so.
The assertion now checks the real invariant (ordering) instead of the accidental one (card count).

## Panels made see-through, buttons/card-holder popups given a slicker treatment

Two direct requests off the reference "CARD BATTLE GAME - CORE ASSET GUIDE" mockup: make the
HUD panels semi-transparent so the new arena backdrop actually shows, and make buttons/the card
"holder" popup as polished as the reference without losing readability.

- **Panel transparency**: `GameBootstrap.HudPanelAlpha` (0.6) now applies to the Enemy/Player/
  Hand band panels via a new `WithAlpha()` helper - the backdrop and its dim overlay show through
  the gaps and the semi-transparent color washes instead of a flat opaque block. Deliberately did
  NOT touch the card-detail/match-result modal panels or the interactive lane-row buttons - those
  need to stay fully opaque/readable as the focused element or the primary tap target.
- **Buttons** (`CreateButton` - End Turn, Reset Lineup, Recommended, and every interactive lane
  row, since they're built on the same helper): now two stacked rounded-rect sprites instead of
  one flat fill - a thin gold rim (`AccentBorderColor`) showing through a 3px inset around a
  more-rounded fill (corner radius bumped from 18 to 24/26 of 56, closer to the pill/stadium
  shape in the reference's PRIMARY/SECONDARY buttons) - all still procedural, no new art needed.
- **Card holder popups**: the card-detail and match-result modals (`CreateRoundedPanel`) now use
  real ornate frame art instead of a flat gradient. You'd uploaded `9_slice_panel_frame.png` and
  `popup_window_frame.png` earlier, but both were held back at the time - like the other spec-
  sheet images, they had a caption ("9-SLICE PANEL FRAME / 1024x1024 PNG") baked across the top.
  Cropped that band off with a Python/PIL script (kept the actual frame art, which was clean) and
  saved the results to `Resources/UI/Frames/NineSlice/Popup_Frame.png` (used) and
  `Ornate_Panel_Frame.png` (cropped and ready, not wired to anything yet). Extended
  `CardArtImportSettings.cs` to set a pixel `spriteBorder` (100px each side) on anything under
  that `NineSlice/` path so Unity's 9-slicing stretches only the straight edges and leaves the
  painted corners alone - **that border value is an approximate eyeball measurement**, not
  pixel-exact (the art is hand-painted and not perfectly symmetric edge to edge), so it's a
  reasonable default rather than a precisely-fitted one.

Verified via the same 12/12 EditMode test pass and synced to Drive (a temporary Drive
disconnect - `G:` unavailable - interrupted this specific sync partway through; re-synced once
it reconnected).

## Battle backdrop replaced with a proper arena illustration

The Lava_Arena.png used as the battle backdrop in the round below was from the original
general-purpose landscape batch - not actually composed as a battle scene. You separately
uploaded a dedicated `battle scene background` folder (10 4K images) purpose-built as arena
scenes: a circular stone dueling platform flanked by torches/pillars/chains, unlike the plain
landscape shots. Checked all 10 directly - all clean, no baked captions.

Copied them into `Resources/UI/Backdrops/Arenas/` (a separate folder from the existing
`Backdrops/`, since a couple of names collide with the old landscape-only batch - e.g. there's
already a non-arena `Celestial_Palace.png` in `Backdrops/` that this doesn't overwrite):
Castle_Valley, Celestial_Palace, Desert_Ruins, Enchanted_Forest, Frozen_Citadel, Haunted_Citadel,
Infernal_Hellscape, Lava_Fortress, Steampunk_Harbor, Storm_Coast.

`GameBootstrap.BuildCanvas()` now loads `UI/Backdrops/Arenas/Lava_Fortress` (was
`UI/Backdrops/Lava_Arena`) - kept the fire/molten theme since it's the closest continuation of
what was already there and fits a dragon-themed game. The other 9 are organized and ready if you
want a different mood (icy Frozen_Citadel, mossy Enchanted_Forest, stormy Storm_Coast, etc.) -
swapping is a one-line change to that Resources.Load path.

Verified via the same 12/12 EditMode test pass and synced to Drive.

## Real art assets integrated - 95 of ~130 uploaded PNGs, the rest flagged as unusable as-is

You uploaded a large AI-generated asset batch (`Resources/UI/Icons/ai generated asset/`, with
a `master icon list.png` reference sheet organizing it into 9 batches). Before wiring anything
in, checked every file's actual pixel content rather than assuming the names/organization meant
they were all game-ready - about a dozen were not:

- **`Attack.png`, `Health.png`, `Mana.png`, `Energy.png`, `Shield.png`** (the main Batch 2 stat
  icons), **`9_slice_panel_frame.png`**, **`popup_window_frame.png`** - all have the icon's name
  and "1024x1024 PNG" (sometimes garbled to "102A") literally painted into the image as a
  caption. Using these as-is would show that caption text during actual gameplay.
- **`Player_Name_Plate.png`, `Enemy_Name_Plate.png`** - have placeholder text ("PLAYER NAME",
  "10") baked in rather than left blank for a real name/value to be overlaid.
- All `*_Button.png` files - inconsistent/ambiguous crops (one showed just a corner glow, not a
  full button face) that couldn't be confirmed clean without risking a broken-looking result.
- The bar PNGs (`Full health bar.png`, `boss health bar.png`, etc.) - same caption problem as
  the stat icons ("FULL HEALTH BAR" baked into the bar art itself).

None of these were used. The other **95 files were confirmed clean** (checked a representative
sample from each batch directly, not just inferred from filenames) and organized into
`Resources/UI/{Frames,Portraits,Backdrops,Slots,VFX,Popups,StatusIcons}/`:

- **Card rarity frames** (Common/Rare/Epic/Legendary) now sit behind each hand card, mapped from
  `Card.Rarity` (1-2/3-4/5-6/7). The frame art's fill is opaque, not a transparent cutout
  (checked directly - center pixel alpha is 255), so card art layers on top within an inset
  margin rather than showing through a hole - visually equivalent since the art itself is opaque.
- **Avatar portraits** (Paladin for the player, Orc King for the enemy) with the circular frame
  ring, top-left corner of each panel - not a true circular mask (the frame is a ring, not a
  filled disc), so portrait corners can peek out slightly past the ring in the gaps; a real mask
  would need a filled-circle asset, which wasn't in the set.
- **Lane slots**: every slot shows Friendly_Slot/Enemy_Slot art when occupied, Empty_Slot art
  when open - all 3 slots per lane are now always visible instead of just empty space until
  something's played there.
- **Battlefield backdrop**: Lava_Arena replaces the flat procedural gradient behind the whole
  scene, with a 35% dim overlay added so panel text stays readable over the busier painted scene.
- **Animation template**: `PlayEffect()`/`PlayFloatingText()` - a real (if simple) reusable
  fade/rise-and-destroy coroutine pattern, not just static sprites. Wired to two real triggers:
  an element-appropriate VFX burst (Holy_Beam/Shadow_Explosion/Magic_Circle by Card.Element) on
  card play, and floating damage numbers over each side's health bar when a turn resolves with
  actual overflow damage. **Important caveat: `StartCoroutine` only runs in Play Mode, and my
  test suite is EditMode-only** - the automated tests confirm this compiles and doesn't break
  anything, but the animation triggers themselves were never actually exercised by a test (the
  smoke test's empty starting board means zero damage was dealt, so the floating-text path
  never ran during the test). This one specifically needs your eyes, not just my test suite.

**Not done yet**: status effect icons (35 of them - Stun/Poison/Taunt/etc.) were copied in but
not wired to anything, since most don't correspond to a real game mechanic yet (only Knight's
Taunt does). Buttons still use the procedural rounded-gradient style, pending clean button art.
Also flagged, not touched: a stray unrelated file
(`Jun 2026 - Professional Management of Corporate Fleet petroleum.xlsx`) was sitting in the
uploaded folder - left alone in case it landed there by accident.

## Resource rescaled to match HP, "Reset Lineup"/"Recommended" now actually exist

Direct feedback: after the HP rescale (below), a mid-level player with 1300 HP still had only
16 Resource, meaning they couldn't afford to play most of a full hand - HP and Resource had
drifted out of proportion. Rescaled Resource the same way: **20 at level 1, ~60 at the mid-test
profile, ~80 at max** - deck size (10-20 cards) and lane space (3x3) are now the real strategic
constraints, not resource, matching how mana usually isn't the bottleneck in Marvel Snap either.

Also: `Assets/Resources/UI/`'s reference-image buttons were asked about three times across this
session and kept getting scoped out with the same explanation - that stopped being useful
feedback to give, so they're now real, working buttons instead of a repeated deferral:
- **"Reset Lineup"** restarts the current match with a freshly-shuffled deck at the player's
  current level (`StartNewMatch()`).
- **"Recommended"** does the same but picks the player's deck with `BuildCurveAwareDeck()` - a
  real (if simple) heuristic that deliberately balances the deck across cheap/mid/expensive
  cost bands, rather than `BuildBalancedDecks`' rarity-only fairness. A genuinely fair deck can
  still be all-expensive and clunky to play; this is what "Recommended" concretely means here.

Neither is a full deck-builder (you still can't pick specific cards) - that still needs the
same prerequisites flagged earlier (deck size is now decided, card-ownership rules aren't).
But both buttons now exist and do something real, rather than not existing at all.

## Rounded corners, round 2: bigger radius, and bars now included via Mask

Still reported as "boxes and monotone" after the first rounding pass - the corner radius (12
canvas units) was likely too subtle to read at the actual window's scaled size. Increased
across the board (buttons 12->18, cards 10->14, modal panels 18->26). Also: health/resource
bars are now rounded too, using Unity's built-in `Mask` component - the bar's rounded-gradient
background doubles as a mask, clipping the square-cornered `Type.Filled` fill to the rounded
silhouette. This is the safe way to combine the two (a naive rounded-bg-plus-square-fill
overlay risks the fill's corners poking past the rounded edge at high fill amounts); Mask
guarantees the fill never renders outside the rounded shape.

## Avatar HP rescaled to hundreds/thousands, card combat stays untouched

Direct request: a new player should start with "hundreds" of HP, a mid-level player should
reach "thousands," not the previous 20-44 range. Card combat stats (Attack/Health 1-12) are
deliberately **unchanged** - still the small, mentally-computable Integer Model from Part II §3.
What connects a big HP pool to small per-card numbers without a match taking 10-50x longer to
resolve is a new `LaneBattleResolver.AvatarDamageMultiplier` (25) - lane-vs-lane combat math is
completely untouched, only the final "overflow hits the Avatar" step is scaled up. Covered by a
new test (`LaneBattleResolver_ResolveTurn_MultipliesOverflowBeforeHittingAvatarHealth`) that
checks the multiplication happens, not just that the numbers changed.

New scale: **100 HP at Avatar level 1, ~1300 at the mid-test profile (Avatar 25/Castle 15),
~1900 at max level.**

## UI beautification, round 2: actual rounded corners, not just gradients

Follow-up feedback that it still read as "boxes and monotone colours" after the gradient pass.
A flat `Image` is a rectangle no matter what color fills it - genuinely not looking like a box
needs either an imported sprite or a generated one, and no art has been supplied yet. Added
`CreateRoundedGradientSprite()` - procedurally generates a rounded-rectangle gradient texture
and 9-slices it, so panels/buttons/cards get real rounded corners at any size with no import
step. Applied to: all buttons, hand card thumbnails, lane mini-cards, and the card
detail/match-result modal panels. **Deliberately not applied** to the full-width Enemy/Player/
Hand bands - those span edge-to-edge, so rounding them would leave odd gaps at the screen
edges rather than reading as intentional; a full-bleed band is supposed to look rectangular.
Also **not applied** to the health/resource bars - their fill uses `Image.Type.Filled`, which
can't safely combine with 9-slicing (the fill's square corners could poke past a rounded
background at full fill) - a real visual risk I can't verify without seeing it rendered, so
left as gradient rects rather than risk shipping something broken-looking.

## UI beautification: gradients and a wider palette, not flat single-tone boxes

Direct feedback that the layout read as "boxes, single color tone." No new art was
commissioned, so this is what's achievable in legacy uGUI: every major panel and button
background is now a **procedurally-generated top-to-bottom gradient** (`CreateGradientSprite()`,
a small runtime-generated Texture2D->Sprite - no import step needed) instead of a flat
`Image.color`, plus a widened, more intentional palette:

- Enemy panel reads warm/hostile (red-brown gradient), Player panel reads cool/allied
  (teal-blue gradient) - matching the red/blue Avatar-HP convention from the reference images.
- Each of those panels gets a thin gold accent stripe along its top edge.
- Player and Enemy health bars are now different colors (teal vs red) instead of both red -
  they were visually identical before despite representing opposite sides.
- Buttons use a two-tone gradient (lighter top, darker bottom) instead of one flat red.

**Not attempted** (unchanged from the last round - still needs commissioned art/a shader
pipeline, not layout code): custom illustrated backgrounds, character portraits, glowing rune
borders, rounded corners, a turn timer, or an animated reveal splash screen.

Also applied: **ENDORALT.ttf**, a thematic display font that's been sitting unused in
`Resources/Fonts/` since an early art-import pass, is now actually used - the title and card
names in the detail overlay. Not used for body/stat text (Attack/Health/Cost numbers), since a
stylized display face is the wrong choice for small text that needs to stay legible at a glance.

### What assets would actually move this further

Asked directly: legacy uGUI without a shader pipeline can fake gradients (done above) but can't
do rounded corners, soft shadows, or real illustrated framing without an actual image asset.
If you want to push past what code alone can do, drop files into `Assets/Resources/UI/` (already
auto-imported as Sprites by `CardArtImportSettings.cs` - no manual Inspector step needed):

- **A 9-sliced panel/button frame** (PNG, e.g. 64x64 or 96x96, with a border thick enough to
  survive 9-slicing without the corners stretching) - this alone would fix "boxes" more than
  anything code-side can, since real rounded/beveled/framed panels need a border image, not a
  flat rect.
- **Icon sprites** for Attack/Health/Cost/Resource (small square PNGs, transparent background) -
  swaps the current text labels ("Attack 9") for icon+number, closer to the reference images.
- **A background/backdrop image** for behind the battle scene (the reference's cave/dungeon
  scene) - currently a flat procedural gradient; a real illustrated backdrop would read
  completely differently.
- Any portrait art for the Avatar corners (reference shows small circular character portraits
  next to the HP bars) - even reusing existing card art assigned as "this is your Avatar" would
  work, no new art strictly required for this one specifically.

None of this is required to keep testing the game - it's specifically for closing the gap
between what's here now and the reference images' illustrated-game level of polish.

## "Recommended lineup" / "preset lineup" buttons - still explicitly out of scope

Re-flagging since this was asked about again: these buttons from the reference images are not
implemented, and weren't missed - they were explicitly scoped out because they need a deck data
model and a deck-building/selection UI that doesn't exist yet (see the "Card detail overlay
rebuilt" section's prerequisites: deck size is now decided, 10-20, but card-ownership rules and
what "preset"/"recommended" concretely mean here still need answering before this is buildable).

## Automated tests now actually run and pass - real verification, not code-tracing

`Assets/Tests/Editor/BattleLogicTests.cs` is a real NUnit suite (12 tests) covering the exact
areas that were the source of every reported bug so far: card data loading, the Turn-1
resource-curve math against the real 85-card pool, `TryPlayCard` cost/lane behavior,
`LaneBattleResolver`'s simultaneous-clash and overflow rules, `GameBootstrap.Initialize()`
actually building a playable match with a rendered hand across two refresh cycles, and (since
2026-08-05) the `DrawsPerTurn`/Perfect-Back-lane behavior that silently regressed once already.
This is run headlessly via Unity's own CLI:

```
Unity.exe -batchmode -projectPath <this folder> -runTests -testPlatform EditMode -testResults test-results.xml -logFile test-run.log
```

(Note: don't add `-quit` - Unity's docs warn against combining it with `-runTests`, and doing
so was confirmed here to make the run silently do nothing instead of erroring.) This requires
Unity to not already be open on the project - batch mode needs exclusive access.

Running this for the first time caught two real bugs before any manual playtest could have:

- The test assembly's `.asmdef` referenced `Assembly-CSharp` by name to reach the gameplay
  code - which silently fails to resolve, because the implicit default `Assembly-CSharp`
  assembly has no GUID for a custom assembly definition to actually reference. Fixed by giving
  the gameplay scripts their own real assembly (`Assets/Scripts/MyriadOfDragons.Runtime.asmdef`)
  and referencing that instead.
- `CardDatabase` only loaded `card_data.json` from its `Awake()` method - but Unity does not
  invoke `Awake()` for a plain `MonoBehaviour` added via `AddComponent` while the Editor isn't
  in Play Mode, which is exactly the situation for any EditMode test (and for any other
  headless/tooling context). Fixed by extracting a public `Initialize()` method that `Awake()`
  now calls, so it's explicitly callable instead of silently depending on a lifecycle callback
  that doesn't fire everywhere. `DontDestroyOnLoad` is now also guarded behind
  `Application.isPlaying`, since it's invalid outside Play Mode too.

All 7 tests pass as of the latest run (`test-results.xml` in this folder has the full output).
This doesn't replace an actual playtest - it can't click buttons or see the screen - but it
does mean the underlying match logic (resource math, card play rules, combat resolution) is
now checked by something other than reading the code and hoping.

## Roster expanded from 44 to 85 cards, mined from unused Drive art

The original 44-card pool was too thin to build a real deck around - both a design concern
(not enough variety) and a mechanical one (only 1 card in the whole pool cost less than 3,
which was the root cause of the earlier "can't play anything on Turn 1" bug). Rather than
generate new art (not something available here), 41 more cards were built from art that was
already sitting unused in `G:\My Drive\card game\Drawing\` - out of 700+ candidate images
across the artist folders, these were screened down to ones that are (a) a single character
suitable for a card portrait, not a multi-character promotional/key-art scene, and (b) not
already used by one of the original 44 under a different crop/filename.

- **21 hero-tier cards** (rarity 3-7) - pulled from Joe, Pablo, YongHui, Dennie, Timi,
  Sswander_Sergey, Ольга Колесникова, and Adam's folders.
- **20 NPC/low-rarity cards** (rarity 1-2, plus a few 3s) - pulled specifically from
  `Reign\Lower rarity\` (`Jan_2017`, `16_march 2017`, `Apr 2017`), a folder the original art
  team had already organized as the lower-tier pool. This is what fixes the resource curve
  properly: the pool now has 8 rarity-1 and 9 rarity-2 cards, versus zero and one before.
- **Excluded, and not part of the 85**: pieces that were multi-character battle/promo scenes
  with the game logo baked in rather than single-portrait card art (e.g. Joe's "Lava Monster"
  and "Hybrid" turned out to be full key-art compositions, not individual monster portraits),
  and two pieces (`dragonlingC`, `orge2`) that were alternate art for characters the roster
  already has under a different name.
- New `id`/`element`/`type`/`rarity` values were assigned per-card by hand, following the same
  convention as the original 44 (`Andras` = human-flavored, `Ktini` = beast, `Pnevmas` = magic;
  `type` picked by combat archetype - Warrior/Knight/Strategist).
- `Assets/Editor/CardArtImportSettings.cs` picked up all 41 new images automatically as Sprites
  on import (verified: `textureType: 8` in their generated `.meta` files) - no manual Inspector
  step was needed.
- Verified for real, not just by reading the JSON: `BattleLogicTests.cs`'s card-count assertion
  was updated to 85 and the full suite re-run headlessly - **7/7 passing** against the actual
  expanded `card_data.json`, including the Turn-1 playable-hand check against the real new pool.

## Opening this in the Editor

Unity Hub and an Editor (6000.5.6f1) are both installed, and both interactive and headless
(batch-mode) compilation have been confirmed working on this machine as of the latest test run
above - there's no outstanding compile blocker.

### Launching

1. Open Unity Hub, click **Open**, browse to this folder (the one with `Assets/` in it), open it.
2. Let the Editor finish importing/compiling (should now succeed).
3. Make sure any scene is open - even a blank default one; `GameBootstrap` doesn't need a
   specific scene, it builds everything itself the moment Play starts.
4. Press the **Play** button (▶, top-center of the Editor).
5. The whole match UI builds itself: tap a card in "Your Hand" to select it, tap a lane
   (Front/Middle/Back) in "Your Lanes" to place it there, press **End Turn** to let the AI
   play and resolve the turn. Repeat until either Avatar HP hits 0.
6. You do **not** need to do the Sprite-import step first to test this - `GameBootstrap`
   currently renders everything as text (names/stats/HP), no art on screen yet, so the core
   loop is testable before any manual importer steps.
