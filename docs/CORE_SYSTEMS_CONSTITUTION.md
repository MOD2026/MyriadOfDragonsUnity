# CORE SYSTEMS CONSTITUTION — Hardcore Solo Card Battler

**Authority:** Command Centre + live code. Beats agent chat and stale MOS overflow tables.  
**Audience:** Every AI seat. Owner reads tables only.  
**Design intent:** Hardcore / theorycraft-friendly. Systems must be coherent so analysis is rewarded — not fake depth, not dead UI pretending to be systems.

**Out of scope here:** HomeV3 cosmetics, combat VFX/animation (parked until systems + campaign depth land).

---

## 0. Wartime doctrine (LOCKED — owner 2026-08-22)

**Goal:** get the **whole playable game loop** up first. Perfecting and polishing come **after** that exists.

Every seat must optimize for these **two** things only:

| # | Priority | Meaning |
|---:|---|---|
| 1 | **Fastest path to a playable game** | Full loop + campaign depth end-to-end. Big breaks first. Stage fine-tune is cheap later (enemy decks = data). |
| 2 | **Token efficiency** | Bulk content in one pass. No decision cards, no HUD polish, no bit-by-bit balance essays, no re-litigating locked knobs. |

**Do:** fill campaign stages, keep unlock/reward chain working, fix softlocks/exploits that block play.  
**Do not:** UI/animation polish, HomeV3, Empire redesign, spell/XP curve debates, per-stage “feel” tuning before the whole loop exists.  
**Owner process:** play the assembled loop → name big issues → then retune. Do not ask the owner to lock abstract tables from a doc.

---

## A. Product spine (what must stay coherent)

| Pillar | Player skill used |
|---|---|
| Formation + lane math | Board geometry, slot weight, element RPS |
| Combat spells + Energy | Tempo, cooldown planning |
| Deck composition | Rarity/slot efficiency, class hooks |
| Campaign length | Persistence, stamina, progression |
| Avatar economy | ResourceCap / HP growth vs stage walls |
| Empire (future) | Long-term investment vs match power |
| Metagame economy | Farm vs sink integrity |

If a pillar is **STUB**, do not balance content as if it were live.

---

## B. Campaign depth (LOCKED)

| Ch | Stages | Count |
|---|---|---:|
| 1 | `1-1` … `1-12` | 12 |
| 2 | `2-1` … `2-21` | 21 |
| 3 | `3-1` … `3-30` | 30 |

Sequential unlock. Fresh = `{1-1}`. Retune **enemy decks**, not global combat knobs, when a stage fails AF.

**Debt:** Ch1 **1-12 done**. Ch2 **2-1..2-21 done**. Next fill `3-1..3-30`. Do not per-stage polish until all three chapters exist.

---

## C. Combat pacing (LOCKED)

| Knob | Live |
|---|---|
| Turn1ResourceFraction | **0.6** |
| Overflow → Avatar | **×4 / ×6 / ×8** (ticks 1–6 / 7–9 / 10–12) |
| Siege | **6%** max HP / tick from tick 7 if board empty |
| Max clashes | **12**; % HP decide; draw = loss for XP |
| Reinforce | ticks **4, 8** |
| Energy | max 100; **+18**/tick + **+2**/living Back (player) |

MOS tables saying ×6/×9/×12 are **STALE**.

---

## D. Experience / leveling (LOCK before more chapters)

### Live

| Track | From matches? | Effect today |
|---|---|---|
| **Avatar** | **+1 win / +0 loss** | ResourceCap, Starting HP, AI HP tier |
| Castle | **No** | Formula exists; always L1 → 0 bonus |
| Barracks | **No** | Deck slots stuck at **10**; regen/replenish **computed never used** |
| Gate | **No** | **No combat reader** |

### Avatar economy (Castle=1)

| Avatar L | Cap | Turn1 | HP (approx) |
|---:|---:|---:|---:|
| 1 | 20 | 12 | 200 (onboarding) |
| 5 | 25 | 15 | ~120 |
| 10 | 30 | 18 | ~140 |
| 25 | 45 | 27 | ~200 |
| 30+ | 50 | 30 | ~220 (Avatar HP bonus capped) |

### Hardcore risks

- Player Avatar keeps growing; campaign decks are **fixed** → late Ch3 can become free if walls don’t escalate in **card power**, not just count.
- Normal Battle: **free stamina** + every win **+1 Avatar +250g/25gems** → infinite soft farm (theorycraft XP dump).

### OPEN (owner must decide before Empire / long climb)

1. Keep Avatar-only XP until buildings ship? **Y/N**  
2. Normal Battle: keep free faucet / add stamina / remove Avatar XP / cap daily?  
3. When Barracks opens: deck 10→20 curve vs chapter pacing?  
4. Gate: delete from player-facing UX until it has a combat rule?

---

## E. Spells (Avatar) — LOCK numbers; OPEN progression

### Live spellbook (fixed 4 — no unlocks)

| Spell | Cost | CD | Effect | Mag |
|---|---:|---:|---|---:|
| Firestorm | 30 | 3 | Lane damage | 4 (→Avatar ×4 if empty lane = 16) |
| Mend | 25 | 3 | Lane heal | 4 |
| War Cry | 40 | 4 | Lane ATK buff | +2 permanent |
| Divine Bolt | 60 | 5 | Direct Avatar | **flat 150** |

AI: **no spells** (asymmetric by design).

### Hardcore risks

- Divine Bolt **150** vs ~200 early HP = huge; vs late HP = weak → skill ceiling shifts by progression without a designed curve.
- Back-lane Energy battery + spells; AI cannot contest Energy.
- Firestorm empty-lane chip ignores overtime curve.

### OPEN

1. Spell unlocks by Avatar / chapter? or stay global 4 forever?  
2. Divine Bolt: stay flat / scale with Avatar / scale with stage?  
3. More spell types later (silence, purge, draw) — only after Energy economy sim.

---

## F. Cards — stats, classes, elements (SHIPPED spine)

### Integer model (LOCKED this era)

Rarity 1–7 → cost 1–7; Atk/HP bands up to **12**; SlotWeight 1 (≤4) / 2 (≥5). Prefer rarity-4 Attack/slot (intentional trade).

### Class hooks (SHIPPED)

| Class | Live |
|---|---|
| Warrior | +1 Atk (authored or fallback) |
| Knight | Taunt |
| Strategist | Draw on play |
| Perfect | Draw only if played Back |

### Element RPS (SHIPPED)

Lane majority: Ktini > Andras > Pnevmas > Ktini → **+25%** lane Attack.

### Card “spells” / Skills / Ultimates

**MISSING.** `SkillTag` names only feed **formation synergy** (+1 ATK pair / +2 ATK+1 HP trio). No DoT, no proc %, no card ultimates. MOS “Individual + Ultimate skills” = **not coded**.

### OPEN (FROZEN save until decided)

- Collection = `List<string>` IDs only — no copies, levels, Evolution, Limit Break.  
- Do not ship card-skill combat until schema + sim exist.

### Hardcore risks

- Strategist **recall does not undo draw** → Formation draw farm.  
- Mono-element lanes +25% — strong composition lever (keep; document).  
- Authored stats outside band silently fall back to hash — corrupts audits.

---

## G. Formation boosts (SHIPPED)

| Lane | Boost |
|---|---|
| Front | +1 Atk |
| Middle | +1 HP |
| Back | +2 Energy/tick if alive |

Reinforce {4,8}: spend leftover Resource. Synergy at ConfirmFormation; reinforce only buffs the new unit.

---

## H. Metagame boosts (PARTIAL — integrity before depth)

| Surface | Live | Issue |
|---|---|---|
| Shop | 4 SKUs; packs deterministic | EventMedal / Relic unused |
| Stamina | Campaign cost 1 | Normal Battle **free** |
| Campaign rewards | First-clear only | Often `gold +=` not CurrencyManager |
| Normal win | 250g/25g every time | Farm faucet |
| Deck | Confirmed 10 slots | Barracks never raises |

### OPEN

Currency naming (EventMedal vs Event Token). Single wallet path (`CurrencyManager`) for all grants. Farm policy (§D).

---

## I. Empire building (STUB — do not fake it)

| Building | Formula? | Raises in play? | Player-facing today |
|---|---|---|---|
| Avatar | Yes | Yes | Real |
| Castle | Yes | No | Dead bonus |
| Barracks | Yes | No | Dead deck/regen |
| Gate | No effect | No | Pure stub |

**Rule:** No Empire UI / “build to power” marketing until advancement rules + Gate effect are decided and simulated in-engine (`BalanceSimulationTests` pattern).

---

## J. Hardcore analysis checklist (theorycrafter surfaces)

Treat as **features to balance**, not bugs to hide — unless marked EXPLOIT.

| Surface | Class |
|---|---|
| Rarity-4 board density | Intentional efficiency |
| Element mono-lanes | Intentional |
| Back Energy → spell tempo | Intentional asymmetry vs AI |
| Spell timing vs AF | Intentional skill |
| Strategist recall-draw | **EXPLOIT** — should patch or design-approve |
| Normal Battle XP/gold faucet | **EXPLOIT / economy** — must decide |
| Divine Bolt early spike | Curve issue — OPEN |
| Fixed stage walls vs rising Avatar | Content scaling — OPEN |
| Dead Castle/Barracks/Gate UI | **FAKE DEPTH** — hide or ship |

---

## K. Work order (coding time savers)

1. **Owner decisions** on OPEN items in §D, §E, §H (short answers).  
2. Fill campaign **1-12 → 2-21 → 3-30** with AF-measured decks (enemy composition, not combat retunes).  
3. Patch agreed EXPLOITs (recall-draw, farm faucet).  
4. Empire + card Evolution only after FROZEN schema + sim.  
5. UI/UX animation last.

---

## L. AI seat rules

| Seat | May touch |
|---|---|
| Claude | Campaign depth (bulk), Battle EXPLOIT fixes, EditMode proof — follow §0 |
| Cursor | This doc, accepts, commits, owner Qs, APK |
| VS Code | UI only after systems + full campaign depth |
| ChatGPT | Curves/packets when asked — no Unity |

No HUD busywork. No 3-stage “chapters.” Unity open → stop. Obey §0 wartime doctrine.

---

## M. Abstract system decisions — PARKED

XP/spell unlock/Normal Battle farm/Empire timing: **not locked from a doc.**  
After full campaign loop is playable, owner names big issues from play; then Command Centre locks 1–2 changes. Until then: **campaign fill only.**

