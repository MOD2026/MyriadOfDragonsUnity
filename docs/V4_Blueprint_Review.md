# Review: "Master Combat & City Architecture (V4.0 FINAL)"

Reviewed 2026-08-06 against the running codebase (45 EditMode tests green) and against the
original design sources in `G:\My Drive\card game\Game mech\MOD\Phase 1\`.

## Verdict in one line

Three of its proposals are already shipped and correct. Two are genuinely good and new. But the
document contains **one arithmetic error that inverts its own central claim**, **one number that
makes the board physically impossible to fill**, and **three systems specified as table rows that
do not exist in the game at all**.

---

## What is already built and matches

| Blueprint section | Status |
|---|---|
| §2.1 Lethal combat, pruned from `LaneState` | Shipped |
| §2.1 Back lane = +2 Energy per clash | Shipped (identical number) |
| §2.2 Un-merged Resource / Energy | Shipped |
| §2.3 12-tick cap with escalating multiplier | Shipped (band boundaries differ - see F) |
| §3.1 Slot weighting, rarity 5-7 = 2 slots | Shipped |
| §3.2 Lane-sum elemental advantage x1.25 | Shipped |
| §5.1 Zero-currency draws | Shipped as draw == loss (see G) |

That convergence is a good sign - it means the design and the code are describing the same game.
The problems are all in the parts that are new.

---

## A. The anti-whale proof is arithmetically wrong, and inverts when corrected

§3.1 is the mathematical justification for slot weighting. Its worked example:

- Wide board, 3x rarity 4: `1,500 base + 75 front bonus = 1,575`
- Tall board, 1x rarity 7 + 1x rarity 1: `1,000 base + 50 front bonus = 1,050`
- Conclusion: wide wins by 525, whales are contained.

The wide figure implies **rarity 4 = 500 ATK**. The tall figure implies **rarity 7 + rarity 1 =
1,000**, so with rarity 1 at the stated floor of 100, **rarity 7 = 900**.

But §1 states the range is **"100 to 1,500 ATK"**. Rarity 7 is the top rarity. It cannot be both
900 and 1,500.

Recomputing with the document's own stated maximum:

| Board | Base | Front bonus | Total |
|---|---|---|---|
| Wide: 3x rarity 4 (500 each) | 1,500 | +75 | **1,575** |
| Tall: rarity 7 (1,500) + rarity 1 (100) | 1,600 | +50 | **1,650** |

**The whale board wins.** For §3.1's conclusion to survive, rarity 7 must be <= ~1,425, which
contradicts §1's headline range.

This matters because slot weighting is the *entire* anti-whale mechanism. If the stat curve is
set from §1's range rather than §3.1's example, weighting does not contain high rarity - it
mildly inconveniences it.

**Fix:** pick the rarity curve first, then verify the inequality holds, rather than asserting the
conclusion. Concretely: with 3 slots per lane and 2-slot big cards, containment requires
`3 x ATK(r4) > ATK(r7) + ATK(r1)`. That is a constraint on the stat table, and it should be
written down as one.

## B. Barracks Resource caps make the 3x3 board impossible to fill

§4.1.2 sets **Starting Resource 3-5** and the matrix sets **Max Resource Cap 6 -> 15**.

Card cost equals rarity (1-7). At the *maximum* Level-50 cap of 15 Resource:

| Deploying | Cards affordable | Slots filled (of 9) |
|---|---|---|
| all rarity 7 (cost 7) | 2 | 4 (2 slots each) |
| all rarity 4 (cost 4) | 3 | 3 |
| all rarity 1 (cost 1) | 9 | 9 |

So a Level-50 player fielding good cards fills **3-4 of 9 slots**. A new player at cap 6 fills
**1-2**. Everything else stays empty - and an undefended lane passes full damage to the Avatar
(that rule is deliberate and shipped). Every match becomes a race of half-empty boards decided by
overflow.

This also contradicts §2's own model: the formation phase exists to deploy the *whole squad* in
one sitting. You cannot deploy a squad on 15 Resource.

For reference, the shipped economy runs a cap of 20 (level 1) to 80 (max), which is what lets a
board actually fill. The blueprint's caps are roughly **5x too low**, and the mismatch is
invisible in the document because card costs were never restated after the rescale.

**Fix:** either scale Resource with everything else (cap ~60-150), or decouple cost from rarity
so a 9-card board is affordable at cap 15. The document changes stats by 100x and leaves costs at
1-7; that inconsistency is the root of it.

## C. Amphitheater governs a system that does not exist

The matrix gives Amphitheater "Skill Trigger Bonus +0% -> +50% relative", and §4.1.1 designs a
careful anti-exploit rule around it (relative, not absolute, so cheap cards cannot reach 100%
stun-lock).

**There are no probabilistic skill triggers in this game.** Every class hook is deterministic:
Knight always taunts, Strategist always draws on play, Perfect always draws in the Back lane.
There is no stun, and no trigger roll anywhere in `BattleController` or `LaneBattleResolver`.

This is the fourth separate proposal to assume an RNG trigger layer. The rule in §4.1.1 is good
design - it is simply protecting a mechanic that would first have to be built.

**Fix:** either specify the trigger system (which skills, base rates, what stun does, how it
interacts with Taunt), or repurpose Amphitheater onto something real.

## D. Tree of Knowledge introduces card levels as a one-line table row

"Card Level Cap: Lv 10 -> Lv 100" implies a full per-card progression system: XP sources, a stat
curve per level, and **per-card persistence**. There is no save system in this project at all -
card levels would reset on every launch.

It also quietly reintroduces the problem slot weighting exists to solve. If a Lv100 rarity-4 card
outperforms a Lv10 rarity-7, then depth of investment wins again - just measured in levels
instead of rarity. Whether that is acceptable is a real design decision; it should not arrive as
a table cell.

**Fix:** treat card levelling as its own project with its own document, sequenced *after* a save
system exists.

## E. Great Gate's shield has an undefined interaction with the tie-breaker

The Gate grants a starting shield of up to 15,000 HP, described as 10% of Avatar HP.

The tick-cap tie-breaker compares remaining Avatar Health **as a fraction of maximum**. The
document never says whether shield counts toward the maximum:

- If shield is *not* counted in max HP, a shielded player can sit above 100% and wins every
  tie-break automatically.
- If shield *is* counted in max HP, it contributes nothing to the tie-break at all.

One of those is a guaranteed win condition and the other is a no-op. Both are reachable from the
text as written.

**Fix:** state it. The cleaner option is shield absorbs damage but is excluded from both
numerator and denominator, so it buys time without buying the decision.

## F. Overtime bands differ from what is implemented

Blueprint: ticks 1-4 x6, 5-8 x9, 9-12 x12. Shipped: ticks 1-6 x6, 7-9 x9, 10-12 x12.

The blueprint escalates from tick 5 of 12 - much earlier. Neither is obviously right, but the
document presents its version as descriptive rather than as a change. Worth deciding deliberately
rather than discovering the divergence later.

## G. "A draw is penalised harder than a loss" creates a small perverse incentive

§5.1's goal is right and the zero-currency rule is exactly correct. But making a draw strictly
*worse* than a loss means a player who is behind is better off conceding than fighting to a draw.
That is a mild incentive to give up, which is the opposite of forcing offensive play.

**Fix:** draw == loss (which is what is shipped). Zero reward, no incentive to throw.

## H. Matchmaking brackets (§5.2) assume PvP

There is no multiplayer. This is not wrong, just not actionable yet - and framing it as exploit
*prevention* implies a live exploit that cannot currently occur.

---

## The bigger question the document does not ask

§1 frames small integers as a **bug** ("the Small Integer Bottleneck"). They were a deliberate
design rule - Game Mechanics v2 Part II §3, the Integer Model - chosen so board maths stays
computable in the player's head mid-match. At 100-1,500 stats and 150,000 Avatar HP you cannot
eyeball whether your lane clears theirs; you read abbreviated numbers ("1.2K") that are *less*
precise than what is displayed today.

That may well be the right trade for a mobile game with 50-level building tracks. But it is a
trade, and the document presents it as a defect being repaired.

**A middle option worth considering: rescale x10, not x100.** Card stats 10-120, Avatar HP
~13,000. A +1 building upgrade becomes a 1-10% increment - granular enough for 50 levels - while
numbers stay 2-3 digits, need no abbreviation, and remain mentally comparable. That captures most
of the granularity benefit at a fraction of the disruption.

## Whatever scale is chosen, the rescale must be atomic

The document updates lane bonuses (+1 -> +25) and nothing else. A 100x rescale silently breaks at
least six systems, none of which are mentioned:

1. `Card.RarityTable` - the whole stat table needs rewriting
2. `AvatarSpell` magnitudes - 4 damage against a 1,500 HP card is 0.27%
3. `FormationSynergy` bonuses - +1/+2 ATK is invisible at 500+ ATK
4. `PlayerEmpireData` health formulas - base 100, +150/tier, must reach 150,000
5. Card costs - left at 1-7 while stats move 100x (this is the root of flaw B)
6. `AvatarDamageMultiplier` - exists specifically to bridge small card stats to a large HP pool;
   its justification changes entirely

The existing tests assert *relationships* rather than magnitudes (deliberately - a test that
hardcoded "25" once broke on a legitimate balance change). That helps here: they will survive a
rescale and still catch structural breakage. They will **not** catch "the numbers are now
nonsense", so the rescale needs its own balance pass regardless.
