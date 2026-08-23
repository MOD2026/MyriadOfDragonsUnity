# Your status — Aug 22 (no playtest required)

**Cursor ran the diagnostic and fixed the flow bug.** You do not need to playtest to unblock this.

---

## What was broken

Ghost **CampaignMapCanvas** (and sometimes Shop/Deck/Collection canvases) stayed alive after launching battle. They blocked **Continue**, **Launch**, and other taps.

Your screenshot (tutorial Firestorm + Continue stuck) matches this exactly.

---

## What Cursor fixed

- Destroy campaign map canvas when launching battle or leaving Story
- Cleanup stale overlay canvases when opening Story, entering battle, returning Home
- Same teardown pattern on Shop / Deck Builder / Collection
- Battle canvas always draws on top (`sortingOrder`)
- Tutorial **Finish**: one Continue tap finishes the fight (not 9 hidden taps)

**Automated proof:** flow-critical EditMode tests **68/68 passed** (`docs/FLOW_DIAGNOSTIC_2026-08-22.md`)

---

## What you do

**Nothing for this bug.** Optional later: skim `docs/FLOW_DIAGNOSTIC_2026-08-22.md` if you want the technical write-up.

---

## Still not built (not this bug)

- Evolution / duplicate cards (needs Save schema — see `DESIGN_BIBLE_RECONCILIATION_2026-08-22.md`)
- Empire upgrade UI on Home
- Gate lock on campaign without Empire UI

Cursor continues Ch10 + Empire UI.
