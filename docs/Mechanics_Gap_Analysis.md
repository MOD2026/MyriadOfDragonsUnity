# Mechanics gap analysis — Myriad of Dragons

Written 2026-08-06, to be handed to a second reviewer (human or AI) to audit for blindspots.
It assumes no prior context beyond `START_HERE.md` and `Findings_2026-08-06.md`.

**Suite status at time of writing: 77/77 EditMode tests passing** (was 48 — this session added the
save system, the siege rule, and their tests).

---

## How to read this

Every mechanic in the game is in one of five states. The states matter more than the list:

| State | Meaning |
|---|---|
| **SHIPPED** | Implemented, covered by tests, believed correct. |
| **UNVERIFIED** | Implemented, but the EditMode runner *cannot* execute it (anything in `Update()` or a coroutine). Needs manual play testing. |
| **STUB** | Data or scaffolding exists; nothing reads it. Looks built, is not. |
| **MISSING** | Not built at all. |
| **DECISION** | Built or buildable, but a design question is unanswered and the answer changes the code. |

The dangerous rows are **STUB** and **DECISION**, in that order. A STUB reads as finished in a
file listing and does nothing at runtime; a DECISION is where a second opinion is actually worth
something.

---

## 1. The four things most worth a second opinion

Ranked by how much a wrong answer costs later. Everything else is detail.

### 1.1 DECISION — the exposed-Avatar siege rule is built, measured, and switched OFF

`LaneBattleResolver.ExposedAvatarSiegeEnabled` is `false`. Flipping it is a one-line change and it
is the single highest-impact open item in the project.

**The problem it solves.** The knockout rate sits around 51% against a design target of >70%.
Avatar Health has already been shown not to be the lever — cutting the pool 400 → 260 (35%) moved
the rate about 5 points. The cause is structural, not numeric:

> Overflow damage only reaches an Avatar through a lane with **no living defenders**. Once *both*
> sides' boards are wiped — the normal end state of two evenly matched formations, which never
> replenish — total Attack on both sides is zero and **no further damage of any kind is possible**.
> The match cannot be won from that position by anyone. It can only run out the clock.

That is not a hypothesis. `BattleController.MaxCombatTicks` exists purely to stop that state
hanging the game forever, and its comment records hitting it in real play with both Avatars above
half Health. `ExposedAvatarSiegeTests.WithoutSiege_TwoWipedBoardsCanNeverDamageEachOtherAgain`
pins it down as a test.

**The rule.** From tick 7 (the existing overtime tick), a side with nothing alive on the board
takes **6% of its own maximum Avatar Health per tick**, escalating on the same 1.0 / 1.5 / 2.0
overtime curve as everything else. An Avatar with no army in front of it is under siege.

**Measured in-engine, 400 matches per cell, driving the real combat code:**

| Profile | OFF | 4% | **6%** | 7% | 8% |
|---|---|---|---|---|---|
| early (Avatar 1 / Castle 1) | 88.0% | 93.8% | **98.3%** | 100.0% | 100.0% |
| mid (25 / 15) | 51.3% | 57.8% | **83.3%** | 99.0% | 100.0% |
| max (30 / 30) | 58.3% | 54.8% | **69.0%** | 88.5% | 100.0% |

Average match length is essentially unchanged (mid 7.9 → 7.6 ticks). Matches that were already
decisive are untouched; only the ones that were going to the cap change.

**Two findings from the tuning that the numbers alone don't show, both of which cost a run to
learn:**

1. **Applied from tick 1, the rule is far too strong.** It took every profile to 100% knockouts
   and cut the early-game match from 3.2 ticks to 1.7 — shorter than it takes to bank Energy for
   the cheapest spell. Gating it to the overtime tick is what makes it a stalemate-breaker instead
   of a damage source.
2. **Expressed in flat damage instead of a fraction, it is a cliff rather than a dial.** Flat
   values 2 → 3 → 4 moved the mid profile 61.5% → 97.0% → 100.0% and the max profile 53.5% →
   58.8% → 90.5%. No single flat number was correct for both, and any number chosen would drift on
   the next Avatar Health rescale — of which there have already been three. A fraction of the pool
   is profile-independent by construction.

**What to challenge.** The three alternatives were rejected on reasoning, not measurement, and
that reasoning is the part worth a second opinion:

- *Partial overflow* (damage leaks through a lane that survives) directly removes the point of
  Taunt and of holding a lane — the trade the whole formation layer rests on.
- *More reinforcement windows* let a player chump-block indefinitely, which is the stalling
  behaviour the tick cap was added to prevent.
- *Raising card Attack* shortens every match, including early-game ones that are already too short
  (3.1 ticks) for a spell to ever be cast.

**Recommendation:** adopt at 6%. Set `ExposedAvatarSiegeEnabled = true` and delete
`ExposedAvatarSiegeTests.SiegeIsOffByDefault`. 7% also clears the target everywhere but saturates
the mid profile at 99%, which means the tie-breaker never fires and the board stops deciding
anything.

### 1.2 DECISION — the AI opponent plays a materially different game to the player

This is the largest correctness gap in the project and it is not written down anywhere else. Four
systems apply to the player's side only:

| System | Player | AI | Where |
|---|---|---|---|
| Avatar spells | Yes — the entire active layer | **Never casts** | `BattleController.TryCastSpell` reads `PlayerState` as caster |
| Energy | Accrues per tick | **Has none at all** | `BattleController.Energy` is a single value |
| Back-lane Energy bonus | +2/card/tick | **No effect** | `BackLaneEnergy(PlayerState)` — hardcoded side |
| Reinforcement windows (ticks 4, 8) | Yes | **Never reinforces** | `TryDeployReinforcement` reads `PlayerState` |

The AI does one thing: it deploys its formation once, competently, by archetype
(`SimpleAIOpponent`). After `ConfirmFormation()` it is a static board.

**Why this matters more than it looks.** Every balance number in this document and in
`Findings_2026-08-06.md` was measured with *both* sides played by `SimpleAIOpponent` and *neither*
side casting spells. That makes the measurements internally consistent and fair — which is why
they are trustworthy for comparing rules — but it means **no measurement in this project has ever
included the spell layer.** A human player casting spells is strictly stronger than everything
simulated so far, by an unmeasured amount. The knockout-rate numbers are a floor, not a forecast.

**Open question for the reviewer:** should the AI cast spells, or is the spell layer intended as
the player's compensation for an opponent that gets to scale its HP and Resource
(`SoloAIScalingSystem`)? Both are defensible designs. Nothing in the code states which one is
intended, and the difference decides whether `Energy` needs to become per-side state.

### 1.3 DECISION — what level a new player starts at, now answerable for the first time

Before this session there was no save system, so `GameBootstrap` hardcoded a "mid-range test
profile" (Avatar 25 / Castle 15 / Barracks 25) and every launch began there. Progression now
persists, which turns that placeholder into a real question.

The seed values were **kept exactly as they were** (`GameBootstrap.NewProfileAvatarLevel` and
friends) so that adding persistence did not silently also become a balance change. But a genuine
new player should almost certainly start at 1/1/1 — and cannot yet, because:

- the level-1 profile resolves matches in **~3.1 ticks**, and
- the cheapest spell costs 25 Energy at 18 Energy/tick, so **a level-1 player can never cast
  anything.** The entire active layer is invisible for the whole early game.

Fixing the level-1 match length is a prerequisite for a real onboarding curve. Note this is the
*opposite* problem to §1.1: the mid and max profiles run too long to resolve, the early profile
resolves too fast to play.

### 1.4 DECISION — stat scale (x1 vs x10), unchanged from `Findings_2026-08-06.md` §4.1

Carried forward because it is still open, still proposed repeatedly, and the usual justification
for it is still wrong. Card stats are 1–12. The "buildings need granular bonuses" argument does not
hold, because buildings are forbidden from granting flat card stats. The genuine argument is
percentage effects.

Two things to add since that document was written:

- The **siege rule is scale-invariant** (it is a fraction of a pool), so a x10 rescale does not
  touch it. That is one fewer system in the "must be atomic across six systems" list.
- The **save file is not scale-invariant.** `SaveData` stores levels, not stats, so it survives a
  rescale — but if card levels are ever adopted (§3.3) and a rescale happens afterwards, every
  stored card level has to be migrated. Adopting one before the other has a cost; adopting the
  rescale first is cheaper.

---

## 2. Built this session

| Mechanic | State | Notes |
|---|---|---|
| Save / persistence | **SHIPPED** | `Assets/Scripts/Save/`. Versioned JSON at `Application.persistentDataPath`, atomic write-then-swap, corruption quarantine, migration path, cross-device version-skew guard. 20 tests. |
| Progression persistence | **SHIPPED** | Avatar/Castle/Barracks/Gate levels survive a restart. `PlayerProfile.RecordMatchResult` is the single point at which a match becomes permanent. |
| Match record | **SHIPPED** | Matches played/won, current and best win streak. Nothing consumes them yet — recorded because a streak cannot be reconstructed after the fact. |
| Story/tutorial gating | **SHIPPED** | Moved off `PlayerPrefs` (per-machine registry state that no reset path could clear) onto the profile. Per-chapter ids recorded, not just one boolean. |
| Card collection storage | **SHIPPED (storage only)** | `SaveData.collection` holds card id, copies, level. **Nothing reads it.** See §3.3. |
| Exposed-Avatar siege | **SHIPPED, OFF** | See §1.1. |

### Save system — what a reviewer should check

The failure paths are where save systems lose people's progress, so they are where the tests are:

- **Atomic write.** Write to `.tmp`, then swap. An interrupted in-place write leaves a truncated
  file that parses as garbage and loses the whole profile. `File.Replace` is not supported on all
  sync-backed volumes, so there is a delete-then-move fallback — this project is explicitly synced
  through Google Drive.
- **Corruption is quarantined, never deleted.** An unreadable save is still the only copy of that
  player's progress.
- **Foreign JSON is detected by looking for the `saveVersion` key in the raw text**, not by
  checking the parsed value. This was learned by a failing test: `JsonUtility` does not throw on
  well-formed JSON of the wrong shape, and it *does* run C# field initialisers for absent keys — so
  `{"someOtherGame":true}` parses into an object claiming to be a valid current-version save.
- **A save from a newer build loads but refuses to be autosaved over.** With two machines syncing
  through Drive, the older machine reading the newer machine's file is the normal case.
- **An unreadable file is never overwritten.** Drive hydration failures (a file that exists in the
  listing but has not been materialised locally) return a fresh profile *without* touching disk.

---

## 3. Not built

| Mechanic | State | What "done" means |
|---|---|---|
| **Home screen / main menu** | MISSING | Fully specified in `BRIEF_HomePage_MainMenu.md`. Zero combat dependency — still the best parallel task. `GameBootstrap.BuildTitlePanel` was kept for it. |
| **Deck builder** | MISSING | `SaveData.activeDeckCardIds` exists and is written by nothing. Today decks are generated per match; "Recommended Lineup" is a cost-curve heuristic standing in for a real one. |
| **Card levels / fusion** | STUB | Storage exists (`OwnedCard.level`); no rule reads it. Deliberate: what a level *does* to a card's stats is a balance decision that must be simulated before it ships. Was blocked on persistence; now blocked only on design. |
| **Collection screen** | MISSING | Storage exists. No UI, and no way to acquire a card — `GrantCard` is called by nothing outside tests. |
| **Economy / items** | STUB | `ItemDatabase` defines 10 currencies with real names and icons. There is **no inventory, no balance, nothing grants or spends any of them.** This is the most convincing-looking stub in the project. |
| **Empire / base building** | STUB | `PlayerEmpireData` derives match economy from four building levels. Nothing can raise Castle, Barracks or Gate — only Avatar level moves, and only by winning. Three of the four tracks are permanently at 1. |
| **Cloud save** | MISSING | Profiles are per-device. "My level reset when I switched machines" is expected behaviour today — see `OPEN_ME_FIRST.md` on the Drive. |
| **Multiplayer / guild** | MISSING | Stated long-term direction. One file exists: `Server/matchmakingValidator.js` on the Drive copy. |
| **Audio** | MISSING | No `AudioSource` anywhere in the project. |

---

## 4. Built but unverifiable by the test suite

Unity's EditMode runner cannot execute `Update()` or coroutines. Everything here is written,
plausibly correct, and **has never been proven to work**. This list is the manual play-test script.

| Mechanic | Risk if broken |
|---|---|
| The 2.2s combat timer | The match does not advance at all. |
| All card/combat animations | Cosmetic, but this is where frame-rate problems would appear. |
| Story and tutorial overlays | A first-run player sees nothing, or gets stuck behind a modal. |
| Typewriter text, background fades, arrow bounce | Coroutines that loop forever by design — leak-prone. |
| Spell casting through the UI | The rules are tested; the button wiring is not. |

The pattern that makes logic testable is `BattleController.AdvanceCombatTick()` — real logic in a
plain method, the MonoBehaviour supplies only timing. Anything added to this list that could have
followed that pattern and didn't is a regression.

---

## 5. Things a reviewer is likely to propose that have already been rejected

Reproduced from `Findings_2026-08-06.md` §5 because several have been proposed more than once by
different sources, and re-rejecting them without the reasoning wastes a session:

| Proposal | Why rejected |
|---|---|
| Immortal cards / "Avatar Conduit" | Board locks by round 3; removes Taunt, lane clearing and overflow. Match becomes arithmetic with no decisions. |
| Single unified resource for deploy + spells | Every spell then costs a deployment. |
| Spell catalysts (discard a card of class X) | The cards worth discarding are the cards worth playing; a hand can hold no valid class, leaving the spell bar dead. |
| % bonuses to skill trigger chance | **There is no RNG trigger system.** Every class hook is deterministic. Proposed four separate times. |
| Per-card percentage modifiers | 25% of a 3-Attack card rounds to +1 — exactly the flat bonus being replaced. Apply at lane-sum scale instead. |
| Avatar HP in the 100,000s | Forces "1.2K" abbreviation, which loses precision. |

Add to that list from this session:

| Proposal | Why rejected |
|---|---|
| Flat siege damage instead of a fraction | Behaves as a cliff, not a dial, and needs retuning on every Avatar Health change. Measured — see §1.1. |
| Siege from tick 1 | 100% knockouts at every profile; early match drops to 1.7 ticks. Measured. |
| `PlayerPrefs` for progression | Per-machine registry state that no export, cloud sync or reset-progress path can see. |

---

## 6. Method note — why the numbers here can be trusted

Every measurement in this document was produced by `Assets/Tests/Editor/BalanceSimulationTests.cs`
driving the **real** `LaneBattleResolver`, `BattleController` and `CardDatabase`. Nothing is
modelled.

This matters because the last balance argument was made from an external Python model, and the
model was wrong in a way that looked completely plausible: it predicted ~89% knockouts at 400
Avatar HP where the real game produced ~50%, reproducibly. The cause was that it filled all nine
board slots while ignoring Resource cost, so its boards were bigger than any real board.

**Simulate in-engine. A replica of the combat maths drifts from the game the moment it omits a
constraint, and it will omit one silently.**

Two caveats on the numbers, stated so they are not over-read:

1. **n=400 per cell gives roughly ±5 points of run-to-run noise.** The max-profile OFF baseline
   measured 49.8%, 50.3% and 58.3% across three runs of identical code. Treat differences under
   about 10 points as unresolved, and re-run before acting on one.
2. **No measurement includes the spell layer** — see §1.2. Both sides are played by
   `SimpleAIOpponent` and neither casts. The figures are a floor.
