# Combat Design V4 - counter-proposal to the "Avatar Conduit" model

Written 2026-08-06, against the codebase as it actually stands (37 EditMode tests green).

## 0. Starting position: most of V3's "fixes" are already shipped

Before proposing anything, three of the six V3 remedies describe the code that is already
running. Worth stating plainly so nobody spends a sprint re-implementing them:

| V3 remedy | Actual status |
|---|---|
| "Revert to Lethal Combat, bring back `LaneBattleResolver`" | Never left. Cards fight, die, and are pruned. |
| "Mid-combat deployments into empty slots" | Shipped - reinforcement windows at Clash 4 and 8. |
| "Un-merge the economy: split Resource & Energy" | Never merged. Resource deploys, Energy casts. |
| "Scrap Overwriting" | Never implemented. Nothing to remove. |

So the live delta from V3 is two items: **slot weighting** and **overtime escalation**. Both are
adopted below, with numbers.

## 1. Correction: lethal combat does not prevent stalling - it caused it

V3 §4 argues that reverting to lethal combat "naturally prevents infinite stalling", because
cards absorb damage, die, and expose the lane.

That is backwards, and it is worth being precise about because it was observed in play, not
theorised. Under lethal combat with no replenishment, **both squads wipe each other out**. Every
lane is then empty, total Attack on both sides is zero, no further damage is possible, and the
match hangs forever. That exact state was hit at Clash 7 with both boards empty and both Avatars
above half Health.

Lethal combat is the *cause* of that stall, not the cure. What actually fixed it was two
unrelated changes:
1. an undefended lane passes damage through to the Avatar (previously it absorbed everything and
   dealt nothing, which is why an emptied board produced zero damage), and
2. a hard tick cap with a Health-fraction tie-breaker.

Both are shipped. V3's reasoning would have removed the cap on the grounds that lethality made it
unnecessary, which would reintroduce the hang.

## 2. Rejected: "max 1-2 cards per turn" (V3 §3 Option A)

This directly contradicts the deployment model. The formation phase exists specifically because
deploying two cards per turn over a dozen turns was the main source of play fatigue - that was
the reason for the whole Formation -> Combat -> Resolved rework. Capping deployment per turn
re-introduces exactly the loop that was removed.

Option B (slot weighting) achieves the same anti-whale goal without touching pacing. Take that
one, drop this one.

## 3. Adopted with numbers: slot weighting

**Rule:** a card occupies slots by rarity. Rarity 1-4 costs **1 slot**; rarity 5-7 costs
**2 slots**. Lanes still hold 3 slots each, 9 across the board.

Why those thresholds - attack per *slot*, using the real stat table:

| Rarity | Avg Attack | Slots | Attack / slot |
|---|---|---|---|
| 1 | 1.5 | 1 | 1.50 |
| 2 | 2.5 | 1 | 2.50 |
| 3 | 3.5 | 1 | 3.50 |
| 4 | 5.0 | 1 | **5.00** |
| 5 | 6.0 | 2 | 3.00 |
| 6 | 7.5 | 2 | 3.75 |
| 7 | 9.5 | 2 | 4.75 |

Rarity 4 becomes the efficiency peak; rarity 7 becomes a concentrated-power choice rather than a
strict upgrade. A full board of 4-stars is 9 bodies for ~45 Attack; a full board of 7-stars is
4 bodies for ~38 Attack but each body has roughly double the Health, so it survives longer and
soaks more. That is a genuine trade rather than a dominant strategy, which is what the original
anti-whale claim wanted and did not achieve.

**Implementation cost - honest:** `LaneState.HasOpenSlot` is currently `Cards.Count < MaxSlots`.
This needs to become a sum of occupied weights, and `TryPlayCard` needs to reject a 2-slot card
into a lane with only 1 free. Small, contained, and testable. The lane picker already renders
slots explicitly, so it can show a 7-star filling two of them.

## 4. Adopted with numbers: overtime escalation

`AvatarDamageMultiplier` is currently a flat 6 across all 12 ticks. Make it escalate:

- Ticks 1-6: x6 (unchanged)
- Ticks 7-9: x9 (1.5x)
- Ticks 10-12: x12 (2.0x)

This forces decisive endings rather than letting the tie-breaker decide most matches, and it
converts the tick cap from a *result* into a *deadline*. Trivial to implement - one method
replacing one constant - and directly testable.

## 5. Modified: elemental advantage must not be a percentage on a small integer

V3 §5 proposes +25% damage when Ktini attacks Andras. The intent is right - flat +1 on a board
outputting 80 is statistically invisible. But a percentage applied per card collides with the
Integer Model (Part II §3): 25% of a 3-Attack card is 0.75, which rounds to +1 - exactly the
value being replaced. On a 9-Attack card it rounds to +2. So the "fix" mostly reproduces the
problem it was meant to solve, unevenly.

This is the same collision that sank `CardStatMultiplier` and the `+20%` lane bonus in two
earlier proposals. Percentages do not survive contact with 1-12 stats.

**Instead, apply the multiplier at lane-sum scale, where the number is big enough to survive one
rounding step.** `LaneBattleResolver.ResolveLaneClash` already computes each lane's total Attack.

1. Determine each lane's dominant element (most cards; ties = no dominance).
2. If lane A's element counters lane B's, multiply **A's lane total** by 1.25 and round once.

A lane total is typically 15-30, so the bonus is +4 to +7 - meaningful, and rounded exactly once
rather than nine times. It also makes elemental composition a *lane-level* decision, which is
precisely the granularity the new lane picker asks the player to think at.

## 6. Not addressed by V3, still open: the Back lane does nothing

Front grants +1 Attack, Middle grants +1 Health, Back grants **nothing** - only a niche draw hook
for Strategist and Perfect. There is currently little reason to use it beyond overflow space.

**Proposal:** each living card in the Back lane generates **+2 Energy per clash**. Three Back
cards take base regen from 18 to 24.

This gives Back the "engine" role V3 wanted, but with a defined number feeding the Energy system
that already exists, rather than an undefined "Ichor generation" that would snowball unopposed.
The trade is clean and already half-expressed by the existing lane bonuses: Back cards fight like
any others but forgo a stat bonus, buying spell tempo instead.

## 7. Not addressed by V3: the True Draw reward exploit

V3 removes the *stalling incentive* but not the *reward*. If a draw awards "partial rating/event
currency to both" (V3 §4), two accounts running maximum-defence decks can farm draw currency
indefinitely at zero risk, since a mutual 100%-HP draw is the most likely outcome of mutual
turtling.

**Proposal:** a draw awards strictly less than a loss, and never currency. The current
implementation already reports a draw as a loss for progression - keep that, and never add a
draw-specific reward. This costs nothing now and closes the exploit before a live-ops economy
exists to be farmed.

## 8. Deliberately not adopted

- **Ultimate Meter / Training Grounds / Laboratory** - no such systems exist. Adding a second
  charge-up economy alongside Energy needs its own justification, not a mention in a table.
- **Auto-Play** - premature. V3's own argument for it (humans beat the AI via spell timing and
  overwriting) collapses once overwriting is scrapped, and the case should be re-made against the
  real combat model before any of it is built.
- **Cards-not-dying** - the one genuinely attractive idea in V3, and still rejected: it removes
  Taunt's purpose, the lane-clearing rule, overflow, and most of `LaneBattleResolver`, in exchange
  for a board that stops changing by round 3.

## 9. Suggested order of work

1. Overtime escalation (smallest change, immediate effect on decisiveness)
2. Back-lane Energy generation (makes a third of the board matter)
3. Lane-scale elemental advantage (makes composition matter)
4. Slot weighting (largest change; touches `LaneState`, `TryPlayCard`, and the picker UI)

Each is independently shippable and independently testable. None requires touching the phase
flow, the economy split, or the reinforcement windows.
