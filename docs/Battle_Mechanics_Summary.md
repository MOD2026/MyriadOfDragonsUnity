# Myriad of Dragons — Battle Mechanics Summary (for external review)

**Date:** 2026-08-07
**Scope:** Everything under `Assets/Scripts/Battle/`, `Assets/Scripts/Cards/`, `Assets/Scripts/AI/`,
`Assets/Scripts/Empire/`. This is the "battle" half of the project (metagame — home screen, shop,
economy, save — is a separate collaborator's domain and is out of scope here).

**Test status:** 85/85 EditMode tests passing. Every number in this document is either a source
constant or a measured result from `Assets/Tests/Editor/BalanceSimulationTests.cs`, which drives
the real combat code (`LaneBattleResolver`, `BattleController`, `CardDatabase`) headlessly — not a
spreadsheet or external model. See §9 for why that distinction mattered once already.

---

## 0. How to review this

Please attack the following, in priority order:

1. **§8 — the AI/player asymmetry.** This is the single largest known correctness gap and it is
   currently an open, undecided question (not a bug I'm asking you to find — a design call I want
   a second opinion on).
2. **§4 — the siege rule.** Just adopted, one measured sweep, n=400 per cell. Is 6% actually the
   right number, or does the reasoning for rejecting the alternatives (§4.3) have a hole in it?
3. **§3.4 — the Avatar damage multiplier and overtime escalation.** This is the piece that
   translates small integer card combat into large Avatar HP pools, and it has been retuned twice
   already (25 → 6, then the HP pool itself was rescaled 3 times). Is the current shape (flat 6x,
   then 1.5x at tick 7, 2x at tick 10) actually well-founded, or does it just happen to pass the
   current tests?
4. **§10 — the two other open decisions** (starting level, stat scale). These need a decision, not
   more research; if you have a strong opinion, that's exactly what's useful here.
5. **Anything in §11 (rejected proposals)** — if you were about to propose one of these, the
   reasoning for rejecting it is there; tell me if that reasoning is actually wrong rather than
   re-proposing it blind.

Don't review this against "is this how other card games do it" — review it against whether the
system is internally consistent, whether the measured numbers actually support the conclusions
drawn from them, and whether there's a failure mode the tests don't cover (see §9.2, EditMode
cannot execute `Update()` or coroutines — the entire animation/input layer is unverified by the
suite and is manual-play-test-only).

---

## 1. Core loop

Three phases, `BattleController.BattlePhase`:

```
Formation  →  Combat  →  Resolved
```

- **Formation.** Both sides draw a full hand up to board capacity (9 slots) and deploy freely.
  Cards can be placed and recalled (`TryRecallCard`, full Resource refund) any number of times.
  Locking in (`ConfirmFormation`) computes formation synergy once (§6) and applies it permanently —
  a unit dying mid-fight does not retroactively weaken its surviving squadmates.
- **Combat.** The formation is locked. Both sides' lanes clash automatically once per tick
  (`AdvanceCombatTick`, called on a ~2.2s timer from the scene layer — untested by EditMode, see
  §9.2). The player's only input is casting Avatar spells (§7) and, at ticks 4 and 8, deploying
  reinforcements from hand (§2.4).
- **Resolved.** One Avatar hit 0 HP, or the match reached `MaxCombatTicks = 12` and was decided on
  remaining HP *fraction* (not raw HP — the two sides' maximums can differ by progression, so a
  fraction is the only fair comparison). A tie at the cap is a DRAW, reported as a loss for
  progression but named honestly on the result screen.

There is no per-frame or coroutine-driven combat logic — `AdvanceCombatTick()` is a plain method
that can be called tick-by-tick from a test with no Play Mode running. The scene layer's only job
is calling it on a timer. This is deliberate and is what makes §3–§8 fully unit-testable.

---

## 2. Board, cards, and deployment

### 2.1 Board shape

3 lanes (`Front`, `Middle`, `Back`), 3 slots each, 9 total. Deployment is simultaneous across both
sides — cards are placed during Formation, not alternated turn-by-turn.

### 2.2 Card model (`Card.cs`)

- **Rarity 1–7**, each with a `(cost, attackRange, healthRange)` band from a fixed table (e.g.
  rarity 4: cost 4, Attack 4–6, Health 4–6; rarity 7: cost 7, Attack 7–12, Health 7–12).
- **Stats are small integers, deliberately** ("Integer Model") — Attack/Health stay 1–12 so combat
  math is mentally computable mid-match. This is a deliberate constraint the whole combat system is
  built around; see §10.3 for the open x1-vs-x10 rescale question.
- **Per-card variance is deterministic**, not random: hashed from the card's `Id` into its rarity's
  stat range, so a given card has stable stats across sessions with no save data needed for it.
- **`SlotWeight`**: rarity ≤4 takes 1 board slot, rarity ≥5 takes 2. This exists specifically so
  higher rarity is a *trade* (more Attack per card, less Attack per slot) rather than a strict
  upgrade — board space, not Resource, is the binding constraint over a full match, so without slot
  weighting the correct play is always "fill every slot with your highest rarity," which makes
  collection depth strictly dominant. Measured: rarity 4 peaks at 5.0 Attack/slot; rarity 7 sits at
  4.75 Attack/slot for ~2x the Health.
- **Class** (`Warrior`/`Knight`/`Strategist`/`Perfect`) and **Element** (`Andras`/`Ktini`/
  `Pnevmas`, rock-paper-scissors) are behavioral/placement hooks, resolved at play time — Card
  itself has no notion of which lane it lands in.

### 2.3 Lane placement bonuses

Applied once, at the moment a card is played (`BattleController.TryPlayCard`), baked into the
`BattleCardInstance`:
- **Front**: +1 Attack
- **Middle**: +1 Health
- **Back**: no combat bonus, but generates +2 Energy/tick per living card (§5.2) — this is new as
  of this session; Back was previously dead space.

### 2.4 Class hooks

- **Warrior**: +1 Attack baked into its base stats (not a hook — see `Card.ComputeStats`).
- **Knight**: `HasTaunt = true`. Taunt units absorb incoming lane damage before any other unit in
  the lane (§3.2).
- **Strategist**: draws a card immediately on play, any lane.
- **Perfect**: adopts Strategist's draw-on-play *only* when played into Back — Front/Middle stat
  bonuses already apply to any card equally, so this is the one lane-conditional hook Perfect needs
  (a hardcore-CCG rework that replaced an earlier flat resource-cost tax).

### 2.5 Reinforcement windows

Ticks 4 and 8 only (`BattleController.ReinforcementTicks`), not continuously. A card deployed as
reinforcement goes through the same `TryPlayCard` path (lane bonuses, class hooks) and re-triggers
a synergy recalculation (only the *new* unit gets buffed — the standing squad's bonus was already
applied at lock-in). Gated to two windows specifically to prevent indefinite chump-blocking, which
is the stalling behavior `MaxCombatTicks` exists to cut off.

---

## 3. Combat resolution (`LaneBattleResolver.cs`)

### 3.1 Simultaneity

Both sides' three lanes clash **simultaneously**, not sequentially and not alternating turns —
each lane's total Attack is snapshotted *before* either side takes damage, so lane processing
order in code never affects the result (Front/Middle/Back is just an iteration order, not a
priority order).

### 3.2 Damage application within a lane

Within a lane, incoming damage is spent **Taunt units first, then the rest in placement order**.
A lane with any living defender blocks **all** overflow to the Avatar, even if incoming damage
technically exceeds the lane's total remaining Health — overflow only happens once a lane has
**zero living defenders** (a bug was fixed here 2026-08-05: dead cards used to stay in the list
forever, which both permanently blocked new plays into a "cleared" lane and caused it to re-leak
full overflow damage every subsequent tick even with zero new activity).

An **undefended lane** (never occupied, or fully cleared) passes **all** incoming attack straight
through as overflow. This was also a real bug: it used to gate on `IsFullyCleared` specifically,
which only reads true for a lane that *was* occupied and then wiped — a lane that was simply empty
from the start read `false` and swallowed the entire attack, meaning "attack an undefended lane"
dealt zero damage, the opposite of the intended rule.

### 3.3 Elemental advantage

Rock-paper-scissors: Ktini > Andras > Pnevmas > Ktini. A lane's element is whichever its *living*
cards are mostly of; an even split has no element and grants no advantage either way. The winning
side's lane gets **+25% to its lane's total Attack**, applied to the sum, not per-card (a 25% bonus
on a single 3-Attack card rounds to +1 regardless of the actual percentage; lane totals of 15–30
survive one rounding step as a meaningful +4 to +7).

### 3.4 Avatar damage: the scale bridge

Card combat stays 1–12 (Integer Model). Avatar HP pools are 100–540+ (`PlayerEmpireData`, scaled by
Avatar/Castle level). `AvatarDamageMultiplier = 6` is the single constant that reconciles the two:
only the final "overflow reaches an Avatar" step is scaled, lane-vs-lane math is untouched.

This constant has already been retuned once in-session (25 → 6) after two other changes compounded
underneath it and made matches resolve in ~2 ticks — faster than Energy could accrue for even the
cheapest spell. The Avatar HP pool itself has been rescaled **three times** (see
`PlayerEmpireData.cs` inline history) — most recently down 100/1300/1900 → 100/260/340 at the
early/mid/max progression profiles, after an in-engine simulation (not a spreadsheet) showed the
old pool produced knockouts only 23% of the time, degrading to 5% at max progression — the further
a player advanced, the *less* decisive their matches became, which is the opposite of what
progression should do.

**Overtime escalation:** the multiplier is not flat across a whole match — it steps up specifically
to force a conclusion rather than let a leading side turtle to the tick cap:

| Ticks | Multiplier |
|---|---|
| 1–6 | 1.0x (base, = `AvatarDamageMultiplier` = 6) |
| 7–9 | 1.5x |
| 10–12 | 2.0x |

---

## 4. The exposed-Avatar siege rule — ADOPTED 2026-08-07

### 4.1 The problem it solves

Once **both** sides' boards are fully wiped — the normal end state of two evenly matched
formations, which never replenish outside the two reinforcement windows — total Attack on both
sides is 0 and **no further damage of any kind is possible.** The match cannot be won from that
position; it can only run out the clock. This is not a hypothesis — `MaxCombatTicks` exists purely
because this state was hit in real play, both Avatars still above half HP.

### 4.2 The rule

From tick 7 (the same tick overtime already starts on), a side with **nothing alive on its board**
takes **6% of its own maximum Avatar HP per tick**, escalating on the same 1.0/1.5/2.0 overtime
curve as everything else — derived from `AvatarDamageMultiplierForTick`, not restated, so retuning
overtime can't leave siege escalating on a curve that no longer exists. A side with even one living
card anywhere is not under siege, regardless of which lane it's in.

### 4.3 Why 6%, and why a fraction rather than a flat number

Measured in-engine, 400 matches per cell:

| Profile | OFF | 4% | **6% (adopted)** | 7% | 8% |
|---|---|---|---|---|---|
| early (Avatar 1/Castle 1) | 88.0% | 93.8% | **98.3%** | 100.0% | 100.0% |
| mid (25/15) | 51.3% | 57.8% | **83.3%** | 99.0% | 100.0% |
| max (30/30) | 58.3% | 54.8% | **69.0%** | 88.5% | 100.0% |

6% is the only tested value that clears the >70% design target without saturating the mid profile
to 99%+ — at 8%, *every* match at *every* profile ends in knockout, meaning the tick-cap
tie-breaker never fires and the board stops deciding anything.

Two findings that a static read of the code would not show:

1. **Applied from tick 1 instead of tick 7, siege is far too strong** — 100% knockouts everywhere,
   and it cut the early-game match from 3.2 to 1.7 ticks (faster than Energy can accrue for even
   the cheapest spell).
2. **A flat damage number (not a fraction) is a cliff, not a dial.** Flat 2→3→4 moved the mid
   profile 61.5%→97.0%→100.0% and the max profile 53.5%→58.8%→90.5% — no single flat number worked
   for both, and any chosen number would drift on the next Avatar HP rescale (of which there have
   already been three). A fraction of the pool is profile-independent by construction.

**Alternatives considered and rejected** (reasoning, not measurement — this is the part most worth
a second opinion):
- *Partial overflow* (damage leaks through a lane that still has a living defender) removes the
  entire point of Taunt and of holding a lane.
- *More reinforcement windows* let a player chump-block indefinitely — the exact stalling behavior
  `MaxCombatTicks` was added to prevent.
- *Raising card Attack* shortens every match, including early-game ones that are already too short
  for a spell to ever be cast.

---

## 5. Resources: two deliberately separate economies

### 5.1 Resource — deployment currency

Per-side, ramps `+1/turn` from a `Turn1Resource` baseline (currently full `ResourceCap` from Turn 1
by direct design request) up to `ResourceCap`. Both values are sourced from `PlayerEmpireData`
(§9), so they scale with Avatar/Castle progression rather than being a shared constant. Spent only
during Formation and at reinforcement windows.

### 5.2 Energy — the active-spell currency

Separate pool, `MaxEnergy = 100`, accrues `18/tick` **plus 2 per living Back-lane card**, only
during Combat. Deliberately not unified with Resource — a single shared pool would mean every spell
cast is also a foregone deployment, which was explicitly rejected (§11).

Energy accrues per **tick**, not per frame — `energy += rate * Time.deltaTime` rounded to `int`
rounds a 5/sec-scale rate to 0 on every single 60fps frame and never accrues anything; a tick-based
accrual is also the only version testable without Play Mode.

---

## 6. Formation synergy (`CardSkills.cs`)

A card's `SkillTag` is **derived**, not authored per-card: Knight → AegisGuard, Strategist →
TacticalCommand, Perfect → TitanSlayer, Warriors split by Element (Andras→DivineHeal,
Ktini→VenomStrike, Pnevmas→ElementalSurge). 2 matching tags in a locked formation grants **+1
Attack** to the whole squad; 3+ grants **+2 Attack, +1 Health**. Different tags stack independently
— two separate pairs both count.

Applied once at `ConfirmFormation`, not continuously, so a synergy-providing unit dying mid-combat
does not retroactively weaken its squadmates (which would read as the bonus being taken away).

---

## 7. Avatar spells — the active layer

Four spells, cooldowns counted in **combat ticks** (not seconds — deterministic, testable, survives
tick-rate retuning):

| Spell | Cost | Cooldown | Effect |
|---|---|---|---|
| Firestorm | 30 Energy | 3 ticks | 4 damage to every living enemy unit in a target lane |
| Mend | 25 Energy | 3 ticks | Restore 4 HP to every living friendly unit in a target lane |
| War Cry | 40 Energy | 4 ticks | Permanent +2 Attack to every living friendly unit in a lane |
| Divine Bolt | 60 Energy | 5 ticks | 150 direct damage to the enemy Avatar, bypasses lanes entirely |

**Lane-targeted damage into an undefended lane passes through to the Avatar**, scaled by the same
`AvatarDamageMultiplier` lane overflow uses — mirroring the lane-combat rule exactly. Without this,
casting a damage spell into an already-cleared lane (the normal late-game board state) silently did
nothing, which meant every damage spell became a dead button in exactly the position it should
matter most.

**UI targeting model** (`GameBootstrap.cs`, untested by EditMode — see §9.2): press-and-hold on a
spell icon shows an inspect tooltip; a quick tap arms the spell for targeting. Avatar-targeted
spells (Divine Bolt) cast immediately on arm. Lane-targeted spells require a follow-up tap on a
lane — the player's own board for friendly-targeted spells (Mend, War Cry), the enemy's for
Firestorm — and tapping anywhere else cancels the cast rather than misfiring.

---

## 8. The AI opponent — the largest open gap

`SimpleAIOpponent` deploys competently once, by archetype (Aggressive/Defensive/Tactical/Balanced —
each a different lane-fill order, e.g. Aggressive stacks Front for the +1 Attack lane and accepts
the fragility; Tactical contests whichever lane the player has committed most to). It spends
Resource on the strongest affordable card first, because board slots — not Resource — are the
scarce constraint over a full match.

**After `ConfirmFormation`, the AI does nothing further.** Four systems apply to the player's side
only:

| System | Player | AI |
|---|---|---|
| Avatar spells | Full active layer | **Never casts — has no spellbook interaction at all** |
| Energy | Accrues per tick | **Does not exist for the AI side** |
| Back-lane Energy bonus | +2/card/tick | **No effect** |
| Reinforcement (ticks 4, 8) | Yes | **Never reinforces** |

**This matters more than it looks like it should:** every measured number in §3 and §4 — the
Avatar HP rescale, the damage multiplier retune, the siege sweep — was measured with **both** sides
played by `SimpleAIOpponent` casting nothing. That makes the measurements internally consistent
(fair to compare against each other), but it means **no balance number in this project has ever
included the spell layer.** A human player casting spells is strictly stronger than everything
simulated so far, by an unmeasured amount — every knockout-rate figure above is a floor, not a
forecast of real play.

**Open question, genuinely undecided:** should the AI cast spells (symmetric rules), or is the
spell layer meant as the player's compensation for an opponent that instead scales its HP/Resource
directly via `PlayerEmpireData`-style progression (asymmetric by design)? Both are defensible.
Nothing in the code states which is intended, and the answer decides whether `Energy` needs to
become per-side state rather than the single value it is today.

---

## 9. Method note: why these numbers should be trusted (and how they could still be wrong)

Every measured figure above came from `BalanceSimulationTests.cs` driving the **real** combat code,
not a model of it. This distinction cost a session already: an earlier external Python replica of
the combat math predicted ~89% knockouts at 400 Avatar HP where the real game produced ~50%,
reproducibly — the replica filled all nine board slots while quietly ignoring Resource cost, so its
boards were larger than any board the real game can produce. A replica drifts from the game the
moment it omits a constraint, and it will omit one silently.

**Two caveats on trusting the numbers in this document as stated:**

1. **n=400 per cell gives roughly ±5 points of run-to-run noise.** The max-profile OFF baseline in
   §4.3 measured 49.8%, 50.3%, and 58.3% across three separate runs of *identical* code. Treat any
   difference under ~10 points as unresolved and re-run before acting on it.
2. **No measurement anywhere in this document includes the spell layer** (§8). Every simulated
   match is two non-casting `SimpleAIOpponent`s.

### 9.2 What the test suite cannot see at all

Unity's EditMode runner cannot execute `Update()` or a coroutine. Everything below is written and
plausibly correct but **has never been proven to run**:

- The ~2.2s combat tick timer that actually drives `AdvanceCombatTick()` in a live match
- All card/combat animations
- The spell-targeting UI interaction described in §7 (the underlying rules are tested; the button
  wiring is not)
- Any coroutine-driven UI (typewriter text, fades) — these loop by design and are leak-prone if
  broken

---

## 10. Other open decisions (need a decision, not more research)

### 10.1 What level does a new player start at?

Before this session there was no save system, so `GameBootstrap` hardcoded a mid-range test profile
(Avatar 25/Castle 15). Progression now persists, which turns that placeholder into a real question,
and it currently cannot just become 1/1/1: a level-1 profile resolves matches in **~3.1 ticks**, and
the cheapest spell (Mend, 25 Energy) can never be cast at 18 Energy/tick before the match is already
over. Fixing level-1 match length is a prerequisite for a real onboarding curve.

### 10.2 Stat scale: x1 vs x10

Open since before this session, proposed repeatedly. Card stats are 1–12. The usual argument for
scaling up ("buildings need granular bonuses") doesn't hold — buildings are forbidden from granting
flat card stats by design. The genuine argument is percentage effects rounding better at a larger
scale. Two things worth knowing if this comes up again: the siege rule is scale-invariant (it's a
fraction of a pool, untouched by a stat rescale) — one fewer system in the "must move atomically"
list — but the save file is *not* scale-invariant if card levels (§10.3) are ever adopted before a
rescale happens, since every stored level would need migrating.

### 10.3 Card levels / fusion — not yet built

`OwnedCard.level` exists in the save layer and is written by nothing. Deliberately deferred: what a
level actually *does* to a card's Attack/Health is a balance decision that must be simulated before
it ships, the same way the siege rule was.

---

## 11. Proposals already rejected — please don't re-propose these blind

Several of these have been suggested more than once across sessions/reviewers. Reproduced so the
reasoning travels with the rejection instead of being re-litigated from scratch:

| Proposal | Why rejected |
|---|---|
| Immortal cards ("Avatar Conduit") | Board locks by round 3; removes Taunt, lane-clearing, and overflow entirely — the match becomes arithmetic with no decisions left in it. |
| Single unified resource for deploy + spells | Every spell cast becomes a foregone deployment — collapses two economies that are supposed to be independent (§5). |
| Spell catalysts (discard a card of class X) | The cards worth discarding are the cards worth playing; a hand can hold zero of a required class, leaving the spell bar dead. |
| % chance to trigger a class skill | There is no RNG trigger system anywhere in this game — every class hook (Taunt, draw-on-play) is deterministic. Proposed four separate times already. |
| Per-card percentage stat modifiers | 25% of a 3-Attack card rounds to +1 — identical to the flat bonus it would replace. Elemental advantage (§3.3) applies at lane-sum scale specifically to avoid this. |
| Avatar HP in the 100,000s | Forces "1.2K"-style abbreviation, which loses the precision the Integer Model depends on for the player being able to reason about a fight. |
| Flat siege damage instead of a fraction | Behaves as a cliff, not a dial — measured, §4.3. |
| Siege active from tick 1 | 100% knockouts at every profile; early match drops to 1.7 ticks. Measured, §4.3. |

---

## 12. File map, for orientation

| File | Owns |
|---|---|
| `BattleController.cs` | Match orchestration: phases, resource/energy flow, spell casting rules, reinforcement, formation lock-in. No UI, no AI. |
| `LaneBattleResolver.cs` | Pure combat resolution: lane clash, Taunt/overflow, elemental advantage, Avatar damage scaling, siege. |
| `Card.cs` | Static card definition + stat generation from rarity. |
| `BattleCardInstance.cs` | A card's live, mutable state during one match (current HP, buffs). |
| `LaneState.cs` / `PlayerBattleState.cs` | Board and per-side match state (deck, hand, resource, lanes, Avatar HP). |
| `AvatarSpell.cs` | Spell definitions and effect resolution. |
| `CardSkills.cs` | Formation synergy (`SkillTag`, `FormationSynergy`). |
| `SimpleAIOpponent.cs` | The AI's Formation-phase deployment decisions (§8 — its *only* decisions). |
| `PlayerEmpireData.cs` | Empire/base-building progression → derived match economy (ResourceCap, StartingAvatarHealth, deck slots). |
| `Assets/Tests/Editor/BalanceSimulationTests.cs` | The in-engine simulation harness every number in this document came from. |
