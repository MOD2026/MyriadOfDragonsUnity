# Battle / Deck Progression Amendment v0.2 — Owner Signature Sheet

**Date: 2026-09-02. Status: PROPOSED, unsigned.** Contains only the rows still unresolved after
`BATTLE-DECK-PROGRESSION-AMENDMENT-0.2-DECISION-SHEET.md`. No production, save, or test file
touched. Nothing here approves a mechanic — every row is Zihan's signature to give.

---

### 1. Migration path

**Zihan signs:** ☐ Preserve &nbsp;&nbsp; ☐ Clamp to 15 + compensation &nbsp;&nbsp; ☐ Defer

**CR7 implementation consequence:**
- **Preserve** → `PlayerEmpireData.DeckSlotsForBarracksLevel` (`Assets/Scripts/Empire/
  PlayerEmpireData.cs:239-252`) needs a per-profile branch: accounts with a stored capacity above
  the new curve's max keep their existing value; new/below-cap accounts use the new milestone
  table. No `PlayerProfile.cs` field changes.
- **Clamp + compensation** → same function reduces existing 16/18/20 profiles to the new cap
  (15 at level 30), plus a one-time grant written through `PlayerProfile.cs` (frozen — needs the
  human coordination that file's freeze requires) and `SaveMigration.cs` (frozen, same
  requirement) to apply once per profile on load.
- **Defer** → no change to `PlayerEmpireData.cs` for existing high-capacity accounts; new curve
  gated to apply only where `barracksLevel`'s current mapped slot count is below the new curve's
  value at the same level, leaving legacy accounts on the old table indefinitely.

**Files affected:** `Assets/Scripts/Empire/PlayerEmpireData.cs`; `Assets/Scripts/Save/
PlayerProfile.cs` and `SaveMigration.cs` only under Clamp+compensation (both FROZEN, human
coordination required before any edit regardless of which option is picked).

---

### 2. Shared Energy behavior (Option C)

**Zihan signs:** ☐ Confirmed future direction, CR7 may implement &nbsp;&nbsp; ☐ Amendment's
"owner-approved" framing was in error — do not implement as described &nbsp;&nbsp; ☐ Hold, revisit later

**CR7 implementation consequence:** live code currently has reinforcement spend Resource
(`BattleController.cs:763`, `TryDeployReinforcement`) and spells spend Energy
(`BattleController.cs:804`) — two separate pools. "Confirmed" means CR7 rewrites
`TryDeployReinforcement` to check/debit `Energy` instead of `Resource`, and removes or repurposes
whatever currently consumes leftover post-formation Resource. "In error" or "Hold" means CR7 makes
no change to this file and the two-pool model ships as-is.

**Files affected:** `Assets/Scripts/Battle/BattleController.cs` (lines 763, 804, and the
`Energy`/`EnergyPerTick`/`MaxEnergy` fields near 117-147).

---

### 3. Beta cap behavior (15-card vs. 20-card live cap)

**Zihan signs:** ☐ Ship 15-cap curve this beta &nbsp;&nbsp; ☐ Keep live 20-cap curve, defer new
curve past this beta

**CR7 implementation consequence:** "Ship 15-cap" means `PlayerEmpireData.DeckSlotsForBarracksLevel`
and its backing `PaidBarracksMilestones`/`DeckSlotsAtPaidMilestone` arrays (same file, near line
239) are replaced with the corrected 8-row table from the MS economy section, and
`MaxDeckSlotCount` (line 129) drops from 20 to 15. "Keep live 20-cap" means no change to this file
for this beta; the amendment's curve work stays PROPOSED with no ship date.

**Files affected:** `Assets/Scripts/Empire/PlayerEmpireData.cs` (constants and
`DeckSlotsForBarracksLevel`) only.

---

### 4. Duplicate handling

**Status: not open.** MS's decision sheet already resolved this (no new reserve/hotkey duplicate
cap, matches existing draw-pool behavior) and it required no new evidence to resolve. **No
signature needed unless Zihan wants to add a restriction beyond current behavior** — that would be
a new decision, not a re-opening of this one.

**CR7 implementation consequence, if Zihan does add a restriction:** a duplicate-count check would
need to be added wherever reserve/hotkey selection is implemented — that code does not exist yet
(this amendment's reserve/hotkey UI is unbuilt), so the consequence is additive scope on
CR7's/whoever's future implementation, not a change to an existing file.

**Files affected:** none today; would apply to the not-yet-written reserve/hotkey selection code
if this is reopened.

---

### 5. Replay/fallback acceptance wording

**Zihan signs:** ☐ Define now &nbsp;&nbsp; ☐ Defer to CR7's own implementation-time proposal

**CR7 implementation consequence:** the owner-matrix assigns CR7 "no-reroll replay semantics" but
neither source doc defines what happens when a replay cannot be reconstructed exactly (a fallback
state, and its player-facing wording) — that gap is real, not previously flagged. Without an
owner-signed answer, CR7 has no acceptance criterion to test a fallback path against, and BS/AD
have no locked copy to write to. "Define now" means Zihan states the fallback behavior (e.g., "an
unreproducible replay shows a plain unavailable state, never a fabricated one") before CR7 starts;
"Defer" means CR7 proposes wording as part of implementation, which then needs a second signature
round before it ships.

**Files affected:** none yet — this is a replay-storage/format decision that precedes any file
existing to hold it. Downstream, likely a new file under `Assets/Scripts/Battle/` for
replay-record storage (CR7's naming, not proposed here) plus whatever presenter surfaces the
fallback state.

---

## Explicitly not done here

No mechanic is approved by this document. No lane-wording work is repeated — items 1-3, 5 are
mechanic/schema/implementation decisions, not copy. No new audit or simulation was requested or
run; every consequence above is derived from existing file citations (`PlayerEmpireData.cs`,
`BattleController.cs`, `PlayerProfile.cs`, `SaveMigration.cs`), not new measurement.

## Next action

Zihan signs the checkboxes above (rows 1, 2, 3, 5 — row 4 needs no signature as filed). CR7
implements only the rows that come back signed; unsigned rows stay PROPOSED and CR7 does not touch
the files listed under them.

## Superseded authority notice (2026-09-02)

This historical sheet is **SUPERSEDED** for approval status and signature state by
`docs/BATTLE-REMAINING-OWNER-DECISIONS-0.9-SIGNED.md`. The newer signed document is authoritative for the 7→15 curve, Option A compatibility behavior, 4 skill + 3 reinforcement hotkeys, and Shared Energy. This notice preserves the original proposal/history; it does not alter its prior content.
