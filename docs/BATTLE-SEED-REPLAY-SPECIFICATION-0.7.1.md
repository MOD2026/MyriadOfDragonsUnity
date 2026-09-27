# Battle Seed / Replay Technical Specification v0.7.1

Status: CR-AUTHORED, attached to `docs/BATTLE-MECHANICS-IMPLEMENTATION-MANIFEST-0.7.md` row 7
("Replay + lane fallback — OWNER-SIGNED behavior; seed schema OPEN"). Closes the "seed schema OPEN"
half of that row only — the behavior half (one uniform seeded draw among legal lanes, no reroll,
explicit-lane fallback when unavailable) was already owner-signed and is unchanged here.

Per `BS-001-BATTLE-CONTRACT-CLOSURE.md`'s own line — *"CR7 must define local authoritative match
seed, separate reinforcement stream, draw ordinal, persistence, and replay equality"* — this is
CR's decision to finalize, not the owner's; Zihan's sign-off on row 7 is for accepting this
specification as the implementation basis, not for choosing between alternatives CR hasn't
resolved. No production code, Battle UI, Save file, or art was touched to write this. The completed
98/98 rehearsal (`CR-BATTLE-005-TARGETING-RECEPTION-PACKET.md`) is not reopened — this section adds
a design for reinforcement's seed schema, a row 7 subfield that chain never touched.

---

## 1. MatchRngSeed input and lifecycle

**No new seed concept.** `BattleController.MatchRngSeed` (`public int { get; private set; }`,
existing, unchanged) remains the single authoritative source for every RNG-resolved outcome in a
match — AI spell-cast probability, spell one-tap targeting, and (this section) reinforcement lane
selection.

**Input**: `StartMatch(..., int? rngSeed = null)`'s existing optional parameter. Resolution order,
unchanged from today: explicit caller-supplied `rngSeed` wins; then an EditMode-only sticky test
seed (`_aiSpellCastSeedOverrideForTests`, existing); otherwise `System.Guid.NewGuid().GetHashCode()`
— a fresh, unpredictable seed for every production match, since no caller supplies one there today.

**Lifecycle**: set once, in `StartMatch`, for the life of the `BattleController` instance. Never
reassigned mid-match (no code path does this today, and none should be introduced — a mid-match
reseed would desynchronize every stream from what already happened). Exposed publicly so a caller
that wants reproducibility (a balance-simulation harness, or a future replay feature) can record it
at match start.

**Nothing changes here.** This subsection restates the existing, already-correct lifecycle so the
rest of this document has a fixed point to build on — reinforcement introduces no new seed input.

---

## 2. RNG stream ownership

Three independent `System.Random` instances, all seeded from the one `MatchRngSeed`, in `StartMatch`:

| Stream | Field | Owns | Status |
|---|---|---|---|
| AI spell-cast probability | `_aiSpellCastRng` | Whether the mirrored PvE AI attempts a cast this tick | Existing (LOCKED 2026-08-24) |
| Spell one-tap targeting | `_spellTargetRng` | Which lane/card a one-tap spell resolves to | Existing (`cd626ff3`, BS-020) |
| **Reinforcement lane selection** | **`_reinforcementRng`** | **Which legal lane a one-tap reinforcement resolves to** | **New, specified here** |

**Ownership rule, extended to the third stream without modification**: each stream is a fully
independent `System.Random(MatchRngSeed)` instance. A draw on one stream must never advance or read
from another. This is not a new rule — it is the existing rule `_spellTargetRng` already proved out
(`CC6-BATTLE-ONE-TAP-RANDOM-TARGET-IMPLEMENTATION-020.md`'s own "never mixed" invariant, itself
directly tested), applied to a third stream by the same reasoning: three independently-seeded
`System.Random` instances from the same integer seed produce three independent sequences by
construction (each instance's internal state is seeded once at construction and never touches
another instance's state) — no new proof obligation, only a new test case exercising it (§6).

**Why not reuse `_spellTargetRng` for reinforcement**: reinforcement and spell targeting are
different player actions that can interleave arbitrarily during a match (a spell cast between two
reinforcement placements, or vice versa). Sharing one stream would make a reinforcement's resolved
lane depend on how many spells were cast before it — replaying a match with even one different
spell-cast *attempt* (not just outcome) would then desync every subsequent reinforcement, which
defeats the entire purpose of separable, per-mechanic determinism. Independent streams make each
mechanic's outcomes replayable on their own, and jointly replayable when the full action log is
replayed in order (§4).

---

## 3. Reinforcement candidate selection (the pure logic `_reinforcementRng` feeds)

**New file**: `Assets/Scripts/Battle/ReinforcementRandomTargeting.cs` — pure, plain, static, no
`BattleController`/MonoBehaviour state (CLAUDE.md non-negotiable #6), same shape as
`SpellRandomTargeting.SelectLane`:

```
public static Lane? SelectLane(IReadOnlyDictionary<Lane, LaneState> lanes, Card card, System.Random rng)
```

**Candidate filter**: a lane is legal iff `lanes[lane].HasRoomFor(card)` (existing method,
`LaneState.cs` — slot-weight capacity check, the same predicate `TryPlayCard`'s own Formation-
deployment path already uses). This is deliberately a different filter than spell targeting's
"has a living card" — reinforcement is placing a new unit, not targeting an existing one.

**Selection**: uniform among legal candidates, canonical `Front, Middle, Back` order before the
draw (same canonical-ordering rule as `SpellRandomTargeting`, so identical board states always
enumerate candidates identically regardless of draw order — required for §5's determinism
invariant).

**Empty-candidate rule**: zero legal lanes → return `null`, **no RNG draw at all** — same rule
`SpellRandomTargeting.SelectLane` already proved (`SelectLane_NoLivingCandidates_ReturnsNullAndDoesNotDrawFromTheRng`),
extended here without modification. A no-op reinforcement attempt must never perturb the stream, or
two matches that differ only in one player attempting an illegal reinforcement (which changes
nothing) would silently diverge on every later reinforcement draw.

---

## 4. Input-log record format

**No raw-RNG-draw log.** The replay contract is **input-log replay**: record the ordered sequence
of player-initiated actions that can draw from a seeded stream, and replay them through a *fresh*
`BattleController` constructed with the same `MatchRngSeed`. Every RNG-resolved outcome re-derives
identically because the actions that trigger each draw happen in the same order against the same
resulting board state (each derived deterministically from the actions before it).

**Record shape** (conceptual — no serialization format is being built yet, per §7; this is the
minimum information a log entry needs to carry, whenever one is built):

| Action | Fields needed |
|---|---|
| Card played (Formation or explicit-lane reinforcement) | tick, card id, lane |
| Spell cast (one-tap or explicit Reposition/Silence target) | tick, spell index, [repositionTarget \| silenceTarget if applicable] |
| Reinforcement deploy, auto-lane path | tick, card id — **no lane recorded**, because replaying the same action against the same seed re-derives the same lane via `_reinforcementRng` deterministically; recording the resolved lane would be redundant data that could drift from the true source of truth if ever hand-edited |
| Reinforcement deploy, explicit-lane fallback path | tick, card id, lane — **lane IS recorded**, since this path bypasses the RNG entirely (§5) |

**Why an action log and not a snapshot-per-tick log**: an action log is orders of magnitude smaller
(one entry per player decision, not one per tick), and — more importantly — an outcome log would
need its own drift-detection against the live simulation forever, while an action log's only
correctness requirement is "the same actions produce the same outcome," which is exactly what
determinism (§5) already guarantees for free.

---

## 5. Replay initialization sequence

1. Construct a fresh `BattleController` (new `GameObject`/component, exactly today's `CreateController`
   test pattern already establishes — no new construction path needed).
2. Call `StartMatch(playerDeck, enemyDeck, playerEconomy, enemyEconomy, ..., rngSeed:
   <recordedMatchRngSeed>)` — the existing `rngSeed` parameter, already wired to seed all three
   streams identically (§2).
3. Replay the recorded action log (§4) in tick order, calling the same production entry points a
   live match would (`TryPlayCard`, `TryCastSpell`, `TryDeployReinforcement` — auto-lane or
   explicit-lane per what each entry recorded), advancing `AdvanceCombatTick()` between ticks
   exactly as the original match did.
4. No step in this sequence is new production API — replay is a *caller* of existing methods, not a
   new code path inside `BattleController` itself. This keeps replay correctness inseparable from
   live-match correctness: there is no second simulation to drift out of sync with the first.

---

## 6. Deterministic replay invariant

**The exact, testable claim**: two independent `BattleController` instances, constructed and seeded
identically (§5 steps 1-2), fed the identical ordered action log (§5 step 3), produce byte-identical
`CombatLedger` (count and contents), `SpellCastLog` (count and contents), and final `AvatarHealth`
on both sides.

This is directly testable today, using the exact technique already established and trusted in this
codebase (`SpellRandomTargetingTests.TryResolveSpellTarget_SameSeed_ProducesTheSameLaneAcrossTwoIndependentMatches`,
`cd626ff3`) — no new test infrastructure, only new test cases once `_reinforcementRng`/
`ReinforcementRandomTargeting` exist to exercise (§9's own test row list carries this forward as
row 13, already specified in `CR7-001-BATTLE-AMENDMENT-IMPLEMENTATION-CLOSURE.md` §7).

**Explicit non-goal**: this invariant does NOT claim network-authoritative replay, cross-session
replay storage, or tamper-evidence — per BS-001's own "no network authority is assumed" and §7
below's "no migration/storage required," this is a same-process, same-build determinism proof, the
same scope every other RNG-stream test in this codebase already operates at.

---

## 7. Explicit-lane fallback trigger

**Finalized**: `_reinforcementRng == null`.

In production, `StartMatch` unconditionally seeds all three streams (§2) — a live match's
`_reinforcementRng` is never null once `StartMatch` has run, so this trigger fires in exactly one
real circumstance: a `BattleController` whose `TryDeployReinforcement` is called **without**
`StartMatch` having seeded it first. That should be unreachable in production by construction (the
one production caller, `GameBootstrap.cs`, only ever calls `TryDeployReinforcement` on a
`BattleController` that already went through `StartMatch` to reach `Phase == BattlePhase.Combat` in
the first place — `IsReinforcementWindowOpen`'s own existing precondition). The fallback exists as
**defensive determinism**, not a scenario the design expects to occur in normal play: a future
refactor that ever constructs a reinforcement-capable `BattleController` through a path that skips
seeding fails safe into the explicit-lane path rather than drawing from an unseeded/default
`System.Random()` (which would be non-deterministic and unreplayable — the one outcome this whole
specification exists to prevent).

**Why this satisfies row 7's "if valid local seed/replay state is unavailable"**: "seed/replay
state unavailable" is precisely and only true when the stream that would service the draw doesn't
exist — there is no other real state (a valid seed with a null stream, or vice versa) this codebase
can construct, since the two are set together in one line (§2). A simpler, single-condition trigger
is deliberately preferred over inventing additional unavailability conditions with no corresponding
real code path to trigger them.

**Player-visible behavior when this fires** (per manifest row 7's own UI-dependency column, "explicit
fallback picker must remain available" — restated here only to confirm this section's trigger is
compatible with it, not to specify new UI, which is out of this task's boundary): the reinforcement
UI falls back to the existing explicit-lane tap flow, unchanged from `TryDeployReinforcement`'s
current (pre-this-chain) shape — no new UI state needed, since that flow already exists and never
stopped working; the one-tap path is additive.

---

## 8. Deployment result copy (row 7's UI-dependency column, restated for completeness only)

Row 7: *"Show only 'Deployed to {lane}'; explicit fallback picker must remain available."* No new
technical parameter needed here — `{lane}` is `Lane.ToString()` on whichever lane
`ReinforcementRandomTargeting.SelectLane` (auto path) or the player's own tap (fallback path)
resolved to. Included only so this section's boundary with the UI row is explicit, not because this
task authorizes any UI copy/implementation work.

---

## 9. Migration / storage impact

**Explicit: no migration required.** This specification:

- Adds no new field to `PlayerProfile` or any other save-schema type.
- Reads no new save data (the only production input is `StartMatch`'s existing `rngSeed` parameter,
  already present, already optional, already defaulting to a fresh unpredictable value).
- Persists nothing new. `MatchRngSeed` and the action log (§4) are match-scoped, in-memory only,
  discarded at match end exactly like `CombatLedger`/`SpellCastLog` already are today — no
  file, no save slot, no server call. If a future product decision wants matches replayable *after*
  the fact (across sessions, not just within one), that is a separate, not-yet-requested feature
  requiring its own storage-format decision — explicitly out of scope here, consistent with BS-001's
  "no network authority is assumed" and this task's own forbidden list ("do not modify production
  code... Save files").
- Existing accounts, existing saves, and the still-open Amendment 0.2 migration question
  (`CR7-001-BATTLE-AMENDMENT-IMPLEMENTATION-CLOSURE.md` §6-7) are entirely unaffected — that
  question is about `DeckSlotCount`/Barracks curve persistence, unrelated to this specification.

---

## Versioning

v0.7.1, attached to `docs/BATTLE-MECHANICS-IMPLEMENTATION-MANIFEST-0.7.md` row 7. Supersedes
`CR7-001-BATTLE-AMENDMENT-IMPLEMENTATION-CLOSURE.md` §4's own seed/replay sketch (same design,
finalized here rather than proposed) — that document's other sections (files/classes,
Energy-settlement diff shape, migration table) are unaffected and still current.

```yaml
STATUS: FINALIZED, ready for owner sign-off on row 7's seed subfield
ROOM: CR
ATTACHED_TO: BS-BATTLE-007 (tools/seat_reports/BS-BATTLE-007-IMPLEMENTATION-MANIFEST-0.7.md), docs/BATTLE-MECHANICS-IMPLEMENTATION-MANIFEST-0.7.md row 7
FILES_TOUCHED: none (docs/ only - no production code, Battle UI, Save file, or art)
DOES_NOT_REOPEN: the completed 98/98 rehearsal (CR-BATTLE-005) - unrelated code paths
CR7_MAY_IMPLEMENT: only once Zihan signs row 7 (seed subfield) in addition to its already-signed behavior half
NEXT: CC7 consolidates this with the completed BS/AD/CR packets into the owner decision gate; CR7 implements only signed rows.
```
