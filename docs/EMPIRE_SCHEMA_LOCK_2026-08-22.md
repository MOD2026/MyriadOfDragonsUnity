# Empire Schema — Command Centre LOCK (2026-08-22)

**Status:** **DESIGN LOCKED** (owner direction accepted after second deep review).  
**Authority packet:** ChatGPT revised  
`C:\Users\zihan\Documents\Codex\2026-08-11\referenced-chatgpt-conversation-this-is-an\EMPIRE_SAVE_AND_PROGRESSION_SCHEMA_PACKET_2026-08-22.md`  
**Prior CC holes:** `docs/EMPIRE_SCHEMA_CC_REVIEW_2026-08-22.md` (superseded for the holes that packet now fixes).

**No Unity Empire implementation yet.** Code is gated by section “Implementation gates” below + feasibility audit.

---

## Locked decisions (do not re-litigate in chat)

1. Ship **Castle / Barracks / Gate** only; Academy / Embassy later.  
2. ~~Phase-1 construction = **Gold only → instant `ReadyToCollect`** (Offline A), **one** account-wide slot, **no Gems**. Gold+time construction is Phase 2 only.~~ **AMENDED 2026-08-23 (owner decision, CC-drafted):** Phase-1 construction adds a second resource (**Construction Materials**, own Campaign faucet, non-purchasable, additive alongside the existing Gold sink) and **client-clock pacing timers** — see "Empire construction v2" in `docs/OWNER_REVIEW_LOG.md`. This is a real amendment, not an oversight: it does **not** reopen §8/§9's server requirement for *monetised* timed builds — no speed-up purchase exists, timers are pacing-only, single-player, non-competitive, with a rollback-detection rule instead of server-verified time. Gems remain forbidden for construction; Gold, Gems, and account slot rules from this decision are otherwise unchanged. §9's Offline A claim UX (`Idle → Building → ReadyToCollect → CompleteClaimed`) stays exactly as locked — the timer only gates when `ReadyToCollect` becomes valid.
3. Gate = **meta-only** chapter route clearance for **Ch1–10**; **necessary, never sufficient** (stage unlock + Avatar/collection/mini-game gates still required).  
4. Additive `empireConstruction` on `PlayerProfile` with `projectId`, `status`, `costCharged`, `costGold`, idempotent complete; dual-write forbidden — one completion API → save → `ApplyDataToEmpire()` once.  
5. Barracks **option A:** **20 deck slots at L30** via **paid milestone tiers only** (L1→L5→L10→L15→L20→L25→L30); lookup table `{1:10,5:11,10:12,15:14,20:16,25:18,30:20}`; **no** empty +1 levels; **no** regen/replenish UI. `targetLevel` = next paid tier, not `current+1`.  
6. **Paired economy:** Normal Battle + Tutorial grant **0 Gold / 0 Gems**; Campaign **first clear** is the finite Gold/Gem source; replays 0 until a separate replay economy is locked. Empire construction **must not ship** until Normal Battle faucet is removed.  
7. Castle is **shared match scale** (AI derives from player HP/Resource) — must be balance-simulated before publishing costs/breakpoints.  
8. Client clock = **display only**; monetised/competitive timed builds need **server time**.  
9. **Offline construction bridge (feasibility ACCEPTED):** option **A** — Gold spend + **instant** complete locally; timed builds wait for server. Claim UX: `Idle → Building → ReadyToCollect → CompleteClaimed`.  
10. Feasibility cost/prereq bands in ChatGPT `EMPIRE_FEASIBILITY_AUDIT_2026-08-22.md` are the working draft for sinks (recompute after Ch7–10 land).

---

## Second-round CC analysis (adds up?)

| Prior hole | Packet fix | CC verdict |
|---|---|---|
| Gate stops at Ch7 | Milestones through Ch10 | **Closed** |
| Gate replaces stage unlocks | Necessary ≠ sufficient | **Closed** |
| Normal Battle gold farm | Zero Normal/Tutorial rewards as ship gate | **Closed as policy** (live code still pays 250g — must change before Empire code) |
| Barracks L30 vs L50 | Explicit 20@L30 milestones | **Closed** (formula rewrite required; not “tweak constant only”) |
| Dual-write / crash spend | Atomic start + projectId + costCharged + idempotent complete | **Closed** |
| Client clock cheat | Display-only; server for secure | **Closed for competitive**; see residual |
| Castle asymmetric tank | Named as shared scale + sim gate | **Closed** |

### Residual — resolved or deferred

1. **Offline timer:** **ACCEPTED A** (instant Gold complete until server time).  
2. **Castle/Barracks/Gate cost bands + Castle prereqs:** drafted in feasibility audit — recompute after Ch7–10.  
3. **Claim UX:** **ReadyToCollect** required (accepted).  
4. Cumulative Campaign gold through Ch1–5 is large — final L21–30 costs must stay above that budget when prior spends included.

---

## Implementation gates (code acceptance checklist)

From packet §6 — all required before Empire feature accept:

- Gate map EditMode tests Ch1–10; unreleased chapters invisible  
- Entry tests: low Gate / missing stage / missing other gates / all pass  
- Economy tests: Normal + Campaign replay do not mint Gold/Gems  
- Atomic start/complete + duplicate completion failure injection  
- One completion API updates profile + derived Empire once  
- Barracks milestone formula tests through L30=20  
- Castle L1/5/10/15/20/25/30 balance matrix vs current AI policy  

**Save:** additive fields only; `SaveMigration` + Battle + Metagame review; frozen shape coordination via Command Centre.

---

## Next seat tasks

| Seat | Next |
|---|---|
| **VS Code / Metagame** | Idle until Copilot; next game task: construction UI after Save opens |
| **Claude** | Ch9 then Ch10 from naming kit |
| **ChatGPT** | Idle |
| **Cursor / Battle** | **DONE Barracks slots + Gold cost table**; next: Gate chapter reader OR open Save for construction |

## Barracks Gold cost table (CC locked 2026-08-22)

Paid upgrades only. Total L1→L30 = **411,200 Gold**.

| Upgrade | Gold | Intent |
|---|---:|---|
| → L5 | 1,200 | After Ch1 |
| → L10 | 5,000 | Ch2 band |
| → L15 | 15,000 | Ch3 band |
| → L20 | 40,000 | Mid campaign |
| → L25 | 100,000 | Late mid |
| → L30 | 250,000 | Capstone — competes with Castle/Gate; Barracks-only dump still needs deep Ch5+ |

Recompute if Ch9–10 gold curve changes the CumCh5/CumCh8 bands materially.
