# Castle Phase-1 Implementer Brief — Block AA v1

**CC note (2026-08-23):** Drive file was labeled `BLOCK_Z`; **Block Z** in the live board is Empire Gate row UX (already shipped). This Castle packet is **Block AA**. Start only after Gate W/W.1 EditMode is green.

**READY FOR CC — Yes / No:** Approve this as the Castle implementation brief after Gate EditMode is green?

**Authority:** This Block AA packet supersedes the sequencing label in `CASTLE_PHASE1_IMPLEMENTER_BRIEF_v1.md`. It does not reopen any parked Soft.

## 1. Required live-combat relationships

Sources: `docs/EMPIRE_SCHEMA_LOCK_2026-08-22.md` locked decision 7; `Assets/Scripts/Empire/PlayerEmpireData.cs` methods `InitializeTCGModifiers()`, `CastleResourceBonusForLevel(int)`, and `CastleHealthBonusForLevel(int)`.

| Castle output | Required relationship | Cosmetic-only? |
|---|---|---|
| Player `ResourceCap` | With Avatar/Barracks/Gate fixed, crossing a live Castle bonus breakpoint increases the Castle Resource contribution and total cap. | No |
| Player `Turn1Resource` | Remains derived from the resulting Resource cap; no second Castle economy formula. | No |
| Player `StartingAvatarHealth` | With Avatar/Barracks/Gate fixed, crossing a live Castle bonus breakpoint increases Castle HP contribution and total starting HP. | No |
| Normal/Campaign AI Resource | Continues mirroring the player’s Castle-scaled cap and Formation Resource. | No |
| Normal/Campaign AI HP | Continues deriving through the existing AI profile from the player’s Castle-influenced HP; no second Castle multiplier. | No |
| UI/presentation | May display only the level and benefits backed by these live readers. | Cosmetic support only |

## 2. EditMode assertions

- [ ] Later live Castle breakpoint produces greater Castle Resource bonus and total `ResourceCap` than an earlier breakpoint, with all other levels fixed.
- [ ] Later live Castle breakpoint produces greater Castle HP bonus and total `StartingAvatarHealth`, with all other levels fixed.
- [ ] Change in each total equals the corresponding Castle-only helper delta; no double application.
- [ ] `Turn1Resource` stays positive, never exceeds the cap, and is non-decreasing when Castle raises the cap.
- [ ] Castle bonuses stop growing at `MaxCastleLevel`; above-cap input grants no extra power.
- [ ] Castle-only changes do not alter deck slots, Avatar/Barracks/Gate levels, or Gate eligibility.
- [ ] Normal and Campaign player Battle state receives the initialized Empire Resource cap, Turn-1 Resource, and starting HP.
- [ ] Normal and Campaign AI Resource remains equal to the player’s corresponding Castle-scaled values.
- [ ] With Avatar tier/archetype fixed, AI HP moves consistently with the Castle-influenced player HP through the existing scaling path.
- [ ] Tutorial remains on its fixed tutorial economy and ignores the real player Castle level.
- [ ] Run the locked Castle matrix at L1/5/10/15/20/25/30 using live formulas; report results and match length as observations, not new target numbers.

## 3. Non-goals

- No Gate retune or Gate-reader modification.
- No Normal Battle/Tutorial reward-faucet change.
- No PlayerProfile, construction, migration, or Save-shape change.
- No Chapter 1-2 Soft work or Stage 1-2 retune; its cause-frame is CC-received and parked.
- No Castle cost/breakpoint invention, card/spell/AI tuning, economy change, or UI redesign.

## 4. WH/Claude implementation checklist — Block AA

1. [ ] Confirm Gate EditMode is green and stable; record its final suite result before opening Castle paths.
2. [ ] Read the three cited `PlayerEmpireData` methods and current normal/Campaign economy handoff.
3. [ ] Add relationship tests first; vary only Castle while fixing Avatar, Barracks, Gate, deck, AI archetype, and seed policy.
4. [ ] Prove player economy handoff, AI shared scaling, and Tutorial isolation.
5. [ ] Run the L1/5/10/15/20/25/30 matrix through production combat constraints.
6. [ ] If every relationship passes, do not change production balance; report Block AA green with exact test counts and matrix output.
7. [ ] If a relationship fails, repair only that reader/handoff and rerun the focused Castle plus Battle/AI regression suites.
8. [ ] If relationships pass but outcomes appear unhealthy, stop with a CC finding. Do not retune any system inside Block AA.

## Completion report

- Files changed and ownership.
- Exact suites and pass/fail totals.
- Castle matrix output.
- Confirm: Gate unchanged; reward faucets unchanged; Save shape unchanged; Chapter 1-2 Soft untouched.
- No production edit after final green validation.
