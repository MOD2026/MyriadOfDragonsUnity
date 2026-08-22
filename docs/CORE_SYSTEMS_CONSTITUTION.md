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

### Deep review — not surface (LOCKED — owner 2026-08-22)

Applies to **every** workstream (campaign accept, Empire/Shop/gates packets, art triage, economy, guild, APK):

1. **Surface checks are not enough** — “compiles / tests green / file exists” is the start, not the accept bar.
2. **Near-future lens required** — before lock or code, ask what breaks when we have 10 chapters, gates, gold sinks, Save migration, AI parity, Normal Battle farm, and server time.
3. **Doc loopholes before Unity** — system packets get Command Centre deep review (economy collisions, dual-write, test/contract breakage, softlocks) **before** implementation. Half-locked schema = wasted credit.
4. **Accept = think one release ahead** — Claude DONE reports still need CC check for chain terminal, roster uniqueness vs prior chapters, seed determinism, and whether the next chapter/gate will fight this design.

Owner instruction: *do not work only at surface level; look deeper into the near future.*

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

## B. Campaign depth (LOCKED — owner 2026-08-22)

### Live content (in code now)

| Ch | Stages | Count | Status |
|---|---|---:|---|
| 1 | `1-1` … `1-12` | 12 | **DONE** |
| 2 | `2-1` … `2-21` | 21 | **DONE** |
| 3 | `3-1` … `3-30` | 30 | **DONE** |
| 4 | `4-1` … `4-30` | 30 | **DONE** |
| 5 | `5-1` … `5-30` | 30 | **DONE** |
| 6 | `6-1` … `6-30` | 30 | **DONE** |
| 7 | `7-1` … `7-30` | 30 | **DONE** |

**Live total:** **183** sequential stages. Fresh = `{1-1}`. Today unlock = win prior stage only (too soft for hardcore long-run — see gates below).

### Planned spine — minimum **10 chapters** (LOCKED intent)

Player must have a long grill path. Chapters **8–10** remain required.

| Ch | Stages | Count | Status |
|---|---|---:|---|
| 8 | `8-1` … `8-30` | 30 | NOT FILLED |
| 9 | `9-1` … `9-30` | 30 | NOT FILLED |
| 10 | `10-1` … `10-30` | 30 | NOT FILLED |

**Planned full spine:** 183 + 90 = **273** stage fights. Retune **enemy decks**, not global combat knobs (§C).

### Chapter gates (LOCKED intent — not just “beat last stage”)

Opening chapter `N` (N≥2) requires **all** of:
1. Clear prior chapter final stage (`(N-1)-last`)
2. Meet a **hard requirement** (at least one): Avatar level floor, collection/deck power floor, or currency sink — exact numbers after owner plays Ch1–3
3. Optional later: clear that chapter’s **between-chapter mini-game**

Within a chapter, stages stay sequential (`N-k` → `N-(k+1)`).  
Gates make chapter openers **earned**, not autopilot.

**Save note:** gate fields must stay additive; frozen save shape changes need Command Centre + both seats.

### Between-chapter mini-games (LOCKED intent — dual prestige)

End of a chapter must not dump the player into empty waiting. After clearing chapter `N` (or as part of unlocking `N+1`), player gets a **mini-game / side activity** for variety.

**KEEP (yes):** Empire-powered activities with **1 daily** + **1 weekly** refresh; rewards feed deck/mats; creates hunger to upgrade Empire. Main climb can be Tower-style.

**Weekly building focus (LOCKED):** Barracks → Castle → Gate → Open/Card (repeat). Focus opens **routes/rules** that week — not raw combat-stat cheats. Accept record: `docs/DUAL_PRESTIGE_TOWER_CC_ACCEPT_2026-08-22.md`.

**REPLACE the “no” (pure Empire climb as the only ranking):**

| Track | What players strive for | Power source |
|---|---|---|
| **Card prestige** | Campaign mastery (clears, optional later PvP ladder) | Deck + formation + spells + Avatar |
| **Empire prestige** | Daily clear + weekly highest Tower floor (and small satellite mini-games) | Empire buildings, **rotated weekly focus** |

Do **not** make weekly Tower floor the sole definition of “best player.” Hardcore brand lives on the card track; midcore retention lives on the Empire track. Reward caps + server required for weekly ranks. TD code after Campaign spine + Empire construction.

### Debt / build order (wartime)

1. **DONE:** fill Ch1–7 content (**183** stages)  
2. **Next content:** fill Ch8→Ch10 using ChatGPT naming kit  
3. **Next systems:** Barracks paid milestones → construction (instant A) → Gate reader → Castle sim  
4. Dual-prestige mini-games / TD after Empire construction exists  
5. Per-stage feel polish = **last**

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
| Normal win | 250g/25g every time (**live faucet**) | **LOCKED to remove:** Normal+Tutorial → 0g/0gems before Empire ships (`EMPIRE_SCHEMA_LOCK_2026-08-22.md`) |
| Deck | Confirmed 10 slots | Barracks never raises |

### OPEN

Currency naming (EventMedal vs Event Token). Single wallet path (`CurrencyManager`) for all grants. Farm policy (§D).

---

## I. Empire building — DESIGN LOCKED (2026-08-22), code not started

**Lock record:** `docs/EMPIRE_SCHEMA_LOCK_2026-08-22.md`  
**Full packet:** ChatGPT `EMPIRE_SAVE_AND_PROGRESSION_SCHEMA_PACKET_2026-08-22.md` (Codex referenced conversation folder).

| Building | Formula today? | Raises in play today? | Locked first-slice role |
|---|---|---|---|
| Avatar | Yes | Yes (+1 win / +0 loss) | Unchanged — not a building |
| Castle | Yes | No | Gold+time build; shared HP/Resource scale (AI mirrors); sim before publish |
| Barracks | Yes (20 slots @ L50 live) | No | **Rescale to 20 slots @ L30**; no regen UI |
| Gate | None | No | Meta route clearance Ch1–10; necessary ≠ sufficient |

**Paired lock:** Normal Battle + Tutorial → **0 Gold/0 Gems** before Empire construction ships. Campaign first-clear remains finite source.

**Rule:** No Empire UI marketing until implementation gates in the lock record pass (economy faucet, formula tests, Gate entry tests, Castle sim). Doc loopholes already reviewed twice — do not re-open locked decisions in chat; residual offline-timer A/B/C is for feasibility only.

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

## L. AI seat rules + ownership matrix (LOCKED)

| Seat | Tool | Owns | Must NOT touch |
|---|---|---|---|
| **Command Centre** | Cursor (this chat) | Queue, bible locks, accepts, commits, APK, owner Qs | Random feature coding that belongs to another seat |
| **Battle / campaign content** | Claude | Campaign stage fill, Battle EXPLOITs, EditMode combat proof, apply story packets into `StoryDatabase` flavor in bulk | Metagame presenters, Economy/, frozen Save shape, UI polish |
| **Metagame UI** | VS Code agent (Gemini historically) | `HomePagePresenter`, `CampaignMapPresenter` UI flows, `ShopPresenter`, `DeckBuilderPresenter`, Story UI chrome | Battle combat math, frozen Save shape without CC |
| **Design / story** | ChatGPT | Story bible, gates fiction, mini-game fantasy, economy/guild **design packets**, curves — **docs only** | Unity / C# |
| **Owner** | Zihan | Priorities, playtest verdicts, lock numbers after feeling the game | — |

### Who picks up work that has not commenced

| Workstream | Design packet | Code seat | When (wartime) |
|---|---|---|---|
| **Campaign Ch4–10** | ChatGPT naming/arc | **Claude** | **NOW** (active) |
| **Story dialogue (beats)** | **ChatGPT** | Claude apply bulk | Parallel **now** (docs) → apply after packet |
| **Chapter gates** | ChatGPT fiction | Metagame (VS Code) + CC if Save touched | After Ch1–3 on device / with Ch4+ |
| **Mini-games** | **ChatGPT** | Metagame (+ Battle if combat-like) | After first playable spine APK |
| **Shop** (real grants, SKUs, integrity) | ChatGPT / Economy blueprint | **Metagame (VS Code)** | After playable spine; Shop shell exists |
| **Empire** (Castle/Barracks/Gate live) | **LOCKED design** — feasibility next | Battle formulas + Metagame UI; **Save = CC coord** | After feasibility + Normal Battle 0-reward + sim — not before |
| **Animation / VFX / HUD polish** | Art brief (optional ChatGPT) | **Metagame / VS Code** | **LAST** (§0) |
| **Music / audio** | Asset list | Metagame hookup + Claude only if Battle cue wiring | After spine; assets can be prepared in parallel |
| **Guild / chat / social** | MOS + ChatGPT | Metagame + `CloudCode/Social*` (server) | Parallel **docs + contracts OK**; full live guild **after** identity/server — do not block campaign |
| **Deck builder / collection depth** | ChatGPT | Metagame | Parallel once Shop/economy integrity clear |
| **Android / APK** | — | **Cursor CC** | On demand after content lands |

### Co-running (speed) — YES, with hard lanes

**Do parallel now**
1. Claude → campaign Ch4→10  
2. ChatGPT → story bible + gate/mini-game fiction + naming kits  
3. ChatGPT → Shop/Empire/Guild **design packets** (no code)  
4. Asset prep (music files, art) outside Unity seats  

**Do not parallel-collide**
- Two seats editing the same `.cs` file  
- Anyone editing **frozen Save** (`PlayerProfile` / `SaveSystem` / `SaveMigration`) without CC  
- Metagame presenters while Claude is mid-edit on `CampaignMapPresenter` content (serialize: Claude content commit → then UI)  
- **Unity open** → no batchmode tests/builds (one machine lock)  
- Empire/animation “busywork” while campaign spine incomplete (§0)

**Rule:** co-run **different workstreams**, not three agents on one system. Design packets ahead of code = free speed.

---

## M. Abstract system decisions — PARKED

XP/spell unlock/Normal Battle farm/Empire timing: **not locked from a doc.**  
After full campaign loop is playable, owner names big issues from play; then Command Centre locks 1–2 changes. Until then: **campaign fill only.**

### Doc-level loophole review BEFORE code (LOCKED — owner 2026-08-22)

Any new system packet (Empire, Shop grants, gates, mini-games, guild) must get a **Command Centre deep review at doc level** (holes, economy collisions, save dual-write, test breakage, near-future Ch6–10 / monetization / server time) **before any Unity implementation**. Coding a half-locked schema is wasted credit.

Same bar for accepting content: not only EditMode green — check next-chapter terminal, catalog exhaustion, gold inflation vs sinks, Gate/chapter collisions.

Empire: **DESIGN LOCKED** — `docs/EMPIRE_SCHEMA_LOCK_2026-08-22.md`. Implementation blocked until feasibility audit + gates (Normal Battle faucet removal, Barracks L30 formula, offline timer A/B/C, Castle sim).

