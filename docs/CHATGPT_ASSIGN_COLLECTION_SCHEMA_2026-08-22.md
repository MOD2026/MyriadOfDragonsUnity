# PASTE TO CHATGPT — Collection schema proposal (docs only)

**No Unity. No Save file edits.** Output is a **proposal** for human + both seats to approve before code.

## Read first

| Doc | Path |
|---|---|
| Master plan locks | Drive `WIP/SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md` |
| Shop gates (resolved) | Drive `WIP/SHOP_V2_NUMBERS_PACKET.md` — **Ascension Permit 8/wk non-purchasable is mandatory** |
| Burn packet | Drive `WIP/CARD_BURN_AND_INFLATION_PACKET.md` |
| Empire lock | Unity `docs/EMPIRE_SCHEMA_LOCK_2026-08-22.md` |
| Live save (read-only) | Unity `Assets/Scripts/Save/PlayerProfile.cs` — **FROZEN — propose additive fields only** |

## Deliver — save to Drive WIP

**File:** `COLLECTION_SCHEMA_PROPOSAL_v1.md`

Must include:

1. **Additive fields** on profile/collection: copy counts, card level, evolution step, pity counters (Normal/High), Ascension Permit balance + weekly earn ledger.
2. **Permit rules:** 8/week earn, non-purchasable, **max hoard cap** (propose number + rationale — loophole from CC §N).
3. **Migration story:** v1 flat `cardCollection` string[] → copies model; idempotent; rollback.
4. **Shop V2 prerequisites:** what must exist before pack open code ships.
5. **Burn path hooks:** duplicate overflow → XP / sacrifice / forge / dust per burn packet.
6. **Test matrix** (EditMode): pack inverse pricing audit, pity reset, permit cap, no-gold-on-full-collection.
7. **READY FOR CC:** 4 yes/no gates at bottom.

## Locked — do not reopen

- Inverse gem/card; bulk = High-draw quality only, not quantity discount.
- No gem→gold Phase 1.
- Dupes never → Gold.

## Do NOT

- Edit Unity repo.
- Reopen shop pack prices or burn yield table.
