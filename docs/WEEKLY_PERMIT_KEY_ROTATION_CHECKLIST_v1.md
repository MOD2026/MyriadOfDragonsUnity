# Weekly Permit Key Rotation Checklist v1

**READY FOR CC — Yes / No:** Approve this as the temporary owner checklist for rotating `ManualTrustedWeekKey`?

**Status:** Option C stopgap only. It remains in use only until server-issued Option A week keys/reconciliation ship. It is not a server-key implementation task.

## Once per approved weekly rotation

1. Confirm the previous week’s player claim window is closed by CC decision; do not rotate early to force a new claim.
2. Change **only** `CollectionAscensionPermits.ManualTrustedWeekKey` in `Assets/Scripts/Save/CollectionAscensionPermits.cs` to one new, unique CC-approved key. Use the existing naming pattern: `cc-permit-week-YYYY-MM-DD`.
3. Record the rotation in `docs/OWNER_REVIEW_LOG.md` (or the current release log): date/time, old key, new key, approving owner, and the build/commit identifier.
4. Have the implementation seat run the existing claim coverage: `CollectionWeeklyPermitClaimTests` and `HomeWeeklyPermitClaimTests`.
5. Confirm the player-facing weekly claim can be made once for the new key and cannot be claimed twice for the same key.

## Do not touch during a rotation

- Do **not** change the weekly amount (**8**) or hoard cap (**16**).
- Do **not** change Gold, Gems, Forge/Dust yields, Evolution costs, pack rules, card ownership, or any Save schema.
- Do **not** reuse an old key, use a device date as an authority, or issue multiple keys in the same intended week.
- Do **not** change the future-design 4/week + 8-hoard target; it activates only with the later server Option A work.

## Rotation log template

`Permit rotation | UTC/SGT date | old key: … | new key: … | approved by: Zihan | validation: CollectionWeeklyPermitClaimTests + HomeWeeklyPermitClaimTests | build/commit: …`

## Stop condition

If the key is unclear, a prior claim state is disputed, or the tests are not green: **do not rotate**. Record the issue and wait for CC. Server-issued ISO-week keys replace this manual process later.
