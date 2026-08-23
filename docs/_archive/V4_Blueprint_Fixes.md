# V4 Blueprint - proposed fixes for the nine issues

Companion to `docs/V4_Blueprint_Review.md`, which identified the problems. This document
proposes a specific resolution for each, with the numbers worked out.

Project now lives at:
`G:\My Drive\card game\Game mech\MOD\Phase 1\Game mech\MyriadOfDragonsUnity`

---

## Suggestion 1: Adopt a x10 rescale, not x100

The blueprint's core move - escaping the small-integer trap - is right. The proposed magnitude is
not. Compare the three options against what actually breaks:

| | Current (x1) | Proposed x10 | Blueprint x100 |
|---|---|---|---|
| Card ATK/HP | 1-12 | 10-120 | 100-1,500 |
| Avatar HP | 1,300 | 13,000 | 150,000 |
| Smallest useful building step | +1 = 8-100% | +1 = 0.8-10% | +10 = 0.7-10% |
| Digits on screen | 1-2 | 2-3 | 4-6 (needs "1.2K") |
| Can a player eyeball a lane clash? | yes | yes | no |
| Existing balance constants invalidated | - | all, uniformly | all, uniformly |

x10 delivers the same granularity per building level as x100 (both land at roughly 1-10% per
step) while keeping numbers readable without abbreviation. The abbreviation itself is a
**regression in precision**: "1.2K" cannot distinguish 1,150 from 1,249, so the player sees less
than they do today.

The only thing x100 buys over x10 is headroom for very large future numbers (guild raid bosses,
million-HP world bosses). That is a real consideration for the live-ops roadmap - but it can be
handled by giving *bosses* big numbers, not by inflating every common card.

**Recommendation: x10.** Card ATK/HP 10-120, Avatar HP ~13,000, lane bonuses +10 ATK / +10 HP.

## Suggestion 2: Derive the rarity curve from the anti-whale constraint

The blueprint asserted the anti-whale conclusion and then wrote numbers that contradict it. Invert
the process: state the constraint, then solve for the curve.

**Constraint:** a wide board must out-damage a tall board of equal slot cost.

With 3 slots per lane, 1 slot for rarity 1-4 and 2 slots for rarity 5-7:

```
3 x ATK(r4) + 3 x FrontBonus  >  ATK(r7) + ATK(r1) + 2 x FrontBonus
```

At x10 scale with FrontBonus = 10:

```
3 x ATK(r4) + 30  >  ATK(r7) + ATK(r1) + 20
```

A curve that satisfies this with a comfortable margin:

| Rarity | ATK | HP | Slots | ATK/slot |
|---|---|---|---|---|
| 1 | 12 | 12 | 1 | 12.0 |
| 2 | 22 | 22 | 1 | 22.0 |
| 3 | 34 | 34 | 1 | 34.0 |
| 4 | 48 | 48 | 1 | **48.0** |
| 5 | 62 | 68 | 2 | 31.0 |
| 6 | 78 | 88 | 2 | 39.0 |
| 7 | 96 | 112 | 2 | 48.0 |

Check: wide = 3(48) + 30 = **174**. Tall = 96 + 12 + 20 = **128**. Wide wins by 46 (36%).

Note rarity 7 matches rarity 4 on ATK/slot but carries **2.3x the HP per body**, so it trades
raw output for durability rather than being strictly worse. That is the trade the blueprint
wanted and did not achieve.

## Suggestion 3: Scale card costs with everything else, or the board cannot fill

This is the blueprint's most damaging number. Cost must be solved against board capacity, not
set independently.

**Constraint:** a player should be able to fill 9 slots with a reasonable mix.

If cost stays equal to rarity (1-7), a 9-slot board of mixed rarity costs roughly:
`3x r4 (12) + 2x r6 (12) + 4x r2 (8) = 32 Resource`

So the **Resource cap must be at least ~35** for a full board, and higher to allow choice. The
blueprint's cap of 6-15 permits 1-4 cards. The currently-shipped 20-80 is approximately right and
should be kept.

**Recommendation:** keep cost = rarity (1-7), keep Resource cap 20 (level 1) to 80 (max). Do
*not* adopt the blueprint's Barracks caps. Restate Barracks as governing where in the 20-80 band
a player sits, which is what the shipped `PlayerEmpireData` already does.

## Suggestion 4: Replace Amphitheater with something that exists

There is no probabilistic trigger system, and building one means adding RNG to a game whose
class hooks are currently all deterministic - a significant design change with its own
consequences (variance, feel-bad losses, harder balance).

Two options that need no new systems:

- **Amphitheater -> Reinforcement Windows.** Level 1-50 raises the number of reinforcement
  windows from 2 (ticks 4, 8) up to 4 (ticks 3, 6, 9, 11). This is action economy, not stat
  bloat, which matches the blueprint's own stated principle, and the machinery already exists
  (`BattleController.ReinforcementTicks`).
- **Amphitheater -> Formation Hand Size.** Raises how many cards are dealt for formation,
  improving consistency without improving raw power.

Both are meaningful, both are one-line changes to shipped code, and neither requires inventing
a trigger layer.

## Suggestion 5: Defer Tree of Knowledge until persistence exists

Card levels need XP, a per-level stat curve, and per-card save data. There is **no save system
at all** - card levels would reset every launch, which is worse than not having them.

Sequence it: (1) build a save layer, (2) then card levels. Until then, repoint Tree of Knowledge
at something session-scoped, e.g. **starting Energy** (0 -> 30 at Lv50), which uses the existing
Energy economy and needs no persistence.

## Suggestion 6: Define the shield/tie-breaker interaction explicitly

Great Gate's shield is currently ambiguous - either an automatic tie-break win or a no-op.

**Recommendation:** shield absorbs damage but is excluded from both sides of the tie-break
fraction. So `HP% = AvatarHealth / MaxAvatarHealth`, ignoring shield entirely. The shield buys
survival time; it does not buy the decision. Anything else makes the Gate either mandatory or
pointless.

## Suggestion 7: Keep draw == loss, not worse than loss

The blueprint's zero-currency rule is correct and already shipped. But making a draw *worse* than
a loss means a losing player is better off conceding than fighting on - an incentive to give up,
which is the opposite of the stated goal. Equal-to-a-loss removes all reward without creating
that incentive.

## Suggestion 8: Reconcile the overtime bands deliberately

Blueprint: 1-4 x6, 5-8 x9, 9-12 x12. Shipped: 1-6 x6, 7-9 x9, 10-12 x12.

The blueprint escalates from tick 5 of 12, which compresses the mid-game hard. With formation
locked and only two reinforcement windows, the mid-game is where the few remaining decisions
live. Escalating at tick 7 preserves it.

**Recommendation:** keep the shipped bands; revisit only if playtesting shows matches routinely
reaching tick 12.

## Suggestion 9: Do the rescale as one atomic change, with a balance pass

Whatever multiplier is chosen, these six must move together or the game silently breaks:

1. `Card.RarityTable` - the stat table
2. `AvatarSpell` magnitudes - 4 damage is meaningless against a 120 HP card
3. `FormationSynergy` bonuses - +1/+2 ATK is invisible at 48+ ATK
4. `PlayerEmpireData` health formulas - base and per-tier must reach the new Avatar HP
5. `BattleController` lane bonuses - +1/+1 -> +10/+10
6. `LaneBattleResolver.AvatarDamageMultiplier` - its entire justification is the ratio between
   small card stats and a large HP pool; that ratio changes

The existing tests assert *relationships* rather than magnitudes, which was a deliberate choice
after one test hardcoded a constant and broke on a legitimate balance change. That pays off here:
they will survive a rescale and still catch structural breakage. They will **not** catch "the
numbers are now nonsense", so a separate balance pass is required regardless.

---

## Suggested order of work

1. **Decide the scale** (x10 recommended) - everything else depends on it
2. Rescale the six systems atomically, tests green
3. Balance pass: verify match length still lands at 8-12 ticks
4. Repoint Amphitheater and Tree of Knowledge onto systems that exist
5. Define shield/tie-break; keep draw == loss
6. Home page (separate track - specced in the tutorial doc, no blueprint dependency)

Item 6 is independent of all of the above and could run first if the home page is more urgent
than the rescale.
