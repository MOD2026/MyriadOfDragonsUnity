# Battle / Deck Progression Amendment v0.2 — MS Economy & Progression Section

**Status: PROPOSED.** Not signed. No production, save, or test file was edited to produce this —
read-only analysis of `CC7-BATTLE-DECK-PROGRESSION-AMENDMENT-0.2.md` and
`docs/BATTLE-DECK-PROGRESSION-AMENDMENT-0.2-OWNER-REVIEW.md`, plus this MS section. This is the
economy/progression half of the joint amendment that document's own decision-ownership table
assigns to MS (deck-depth economy, reserve diversity, catch-up costs, anti-dominance evidence).
CR7's seed/replay/RNG-stream section is not addressed here — that's explicitly CR's decision area,
not mine to draft.

## 1. Defect found in the candidate curve — real, not a nitpick

The hotkey column in the owner-review table is **not monotonic**:

| Level | 1 | 3 | 5 | 8 | 10 | 15 | 20 | 30 |
|---|---|---|---|---|---|---|---|---|
| Hotkeys | 1 | 2 | 3 | **1** | 2 | 3 | 3 | 3 |

A player leveling Barracks from 5 to 8 would lose two of their three hotkey slots, then have to
re-earn them back up to level 15. This reads as a real regression, not an intentional "soften
early cliffs" design (the doc's own stated goal for the early rows) — a mid-curve dip like this
would be the opposite of softening a cliff. I'm treating this as a transcription/design defect in
the candidate table, not a hidden intentional mechanic, since nothing in either source doc explains
why level 8 would be worse than level 5 on this axis.

**Proposed correction:** hotkeys monotonically non-decreasing, capped at 3 once reached and never
reduced: `1, 2, 3, 3, 3, 3, 3, 3` for the same eight levels. This is the smallest change that fixes
the regression without touching any other column. Still proposal-only — Zihan signs the actual
curve per the decision-ownership table, this is MS flagging the defect and offering the minimal fix,
not overriding the owner's row.

## 2. Reserve derivation — exact formula proposed

The table's reserve numbers are consistent with a simple rule once the formation cap is read as
"capped at 9 starting Barracks level 5" (matches the owner-review doc's own note that the 15-card
total cap "coincides with the existing Barracks cap," i.e., formation depth stops growing once
reserve becomes the growth lever):

```
FormationCap(level) = min(TotalCards(level), 9)
Reserve(level)      = max(0, TotalCards(level) - FormationCap(level))
```

This reproduces every row from level 5 onward exactly (9→0, 10→1, 11→2, 12→3, 13→4, 15→6). The two
early rows (level 1: total 7, "reserve 0–1"; level 3: total 8, "reserve 0–2") don't fit this formula
cleanly if `FormationCap` is fixed at `TotalCards` for those rows — a fixed formula would give 0 for
both. I read the table's "0–1" / "0–2" ranges there as **not a formula output but a player-choice
range**: at low levels a player isn't required to field every owned card, so anywhere from 0 to
(TotalCards − minimum viable formation) can sit unfielded. Proposing this be made explicit rather
than left as an ambiguous range: **reserve at any level is simply "owned minus currently fielded,"**
with the table's numbers from level 5 on being the *forced minimum* reserve once `TotalCards`
exceeds the 9-card formation cap, and 0 the forced minimum below that. This removes the ambiguous
range notation entirely and gives one formula for every level.

## 3. Duplicate handling

The owner-review doc already settles draw-pool duplicates: *"Duplicate instances are allowed
exactly as supplied by the deck source"* (unchanged from current behavior, not reopened here). The
genuinely open question is whether **reserve/hotkey selection** should independently cap duplicates
of the same card.

**Proposed:** no dedup at the reserve level either — a player who owns three copies of one card can
hold all three in reserve and hotkey all three, consistent with "duplicate instances are allowed
exactly as supplied." Restricting reserve duplicates but not formation duplicates would be an
inconsistent rule with no stated justification in either source doc, and inventing a new
duplicate-restriction policy here would be exactly the kind of unrequested product decision this
task didn't ask MS to make. If the owner wants a reserve-specific duplicate cap, that's a distinct,
explicit decision for Zihan to add to the curve sign-off, not something MS should default into.

## 4. Diversity / anti-dominance protection

No new hard quota is proposed. Following this project's own established rule (assert relationships,
not magnitudes — a hardcoded balance constant breaks on legitimate tuning), a magnitude-based quota
(e.g. "max 2 duplicates as hotkeys") would be exactly the kind of hardcoded number this project's
own standing practice warns against inventing without simulation evidence.

**Proposed evidence gate instead of a rule:** before this curve ships, run
`BalanceSimulationTests.cs` (the project's existing real-engine simulation harness, not an external
model — this project has a documented incident where an external Python replica of this combat math
predicted ~89% knockouts against a real ~50%) with the new reserve/hotkey depths enabled, and check
whether any single-card-heavy reserve composition produces a win-rate outlier against a diverse
reserve of the same total power budget. If the simulation shows real dominance, a duplicate cap or
role-quota becomes a MS/owner decision backed by a number, not a guess made now. This keeps
"anti-dominance evidence" honest to what the phrase in the decision-ownership table actually asks
for — evidence, not an invented cap.

## 5. Catch-up behavior

For players progressing forward under the new curve: no catch-up mechanism is needed. Every level
in the corrected table grants strictly more capacity than the level before it (once the hotkey
defect above is fixed), so there's no point where a player is worse off than at a prior level -
nothing to "catch up" from.

Catch-up only becomes a real question for the **migration case** below, where an existing account
might end up below where the new curve would have placed them at the same Barracks level. That's
folded into the migration section, not treated as a separate mechanic, since inventing a standalone
catch-up system for a case migration policy might not even produce would be scope creep.

## 6. Migration alternatives for existing 16/18/20-capacity profiles

Three real options, with concrete tradeoffs — this section does not choose one; Zihan does, per the
decision-ownership table.

| Option | What happens | Pro | Con |
|---|---|---|---|
| **A. Preserve (grandfather)** | Existing accounts keep their current 16/18/20 capacity untouched; the new curve applies only to accounts below that capacity or created after cutover. | Zero player-facing loss, zero compensation math needed, fully reversible (no schema change forces a value down). | Two permanent capacity tracks exist side by side (old grandfathered accounts vs. new-curve accounts) - a small, permanent bit of complexity, not a one-time cost. |
| **B. Clamp to 15 + compensation** | Existing accounts above 15 are reduced to the new cap; a one-time grant (materials/currency sized to the lost capacity) compensates. | Single unified curve going forward, simpler long-term. | Requires defining a compensation formula (a new value someone has to sign), and clamping something a player already owns is the kind of change that reads as a take-away regardless of compensation size - reputational/trust risk, not just a balance one. |
| **C. Defer** | Ship nothing for existing 16/18/20 accounts until a separate follow-up decision; new curve only affects new/low-capacity accounts in the meantime. | No decision forced now; unblocks the rest of the amendment (seed/replay/curve) without resolving the hardest sub-problem under time pressure. | Leaves the actual problem unresolved indefinitely if there's no forcing function to revisit it; two-tier system persists same as Option A but without even the benefit of being a final answer. |

**MS recommendation, not a decision:** Option A (preserve) is the lowest-risk path — it's the only
one of the three that requires no new value invention (no compensation formula) and is trivially
reversible (grandfathering can be lifted later; a clamp-and-compensate, once paid out, cannot be
cleanly undone). This is a recommendation for Zihan to weigh, not a sign-off — migration choice is
explicitly the owner's decision per both source docs.

## What remains open, explicitly

- Curve rows themselves (beyond the hotkey defect fix) — Zihan.
- 15-cap timing, whether this ships beta or post-beta — Zihan.
- Migration option A/B/C — Zihan.
- Seed derivation, RNG stream, replay format — CR7, not addressed in this section.
- Reserve-duplicate cap, if the owner wants one beyond what's proposed in §3 — Zihan.

No production code, save schema, or test file was touched to produce this document.
