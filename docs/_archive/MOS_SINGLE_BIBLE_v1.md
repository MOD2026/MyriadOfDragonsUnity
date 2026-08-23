# Myriad of Dragons — Single Bible v1 (DRAFT INDEX)

**Status:** Draft skeleton — Cursor, Aug 22 2026. **Canonical merge may live on Drive WIP** if another workflow updated it later — do not overwrite Drive blindly; diff before merge.

**Numbers:** `SHOP_V2_NUMBERS_PACKET.md` and `CARD_BURN_AND_INFLATION_PACKET.md` delivered (Drive WIP). Shop READY FOR CC gates resolved in `OWNER_REVIEW_LOG.md` (2026-08-22).

**Authority order:** Running code + tests → MOS v1.2 → Mechanics v2 docx → Drive `WIP/MOS_SINGLE_BIBLE_v1.md` (if newer) → this doc → Economy_Blueprint (theory).

**Working copies:** Unity `docs/MOS_SINGLE_BIBLE_v1.md` · Drive `WIP/MOS_SINGLE_BIBLE_v1.md`

---

## 0. Supersedes (do not cite for new design)


| Source                                                                          | Action                    |
| ------------------------------------------------------------------------------- | ------------------------- |
| `docs/_archive/*`                                                               | Archived                  |
| Codex `outputs/document_updates/*`, `HANDOVER_*`, `COMMAND_CENTRE_*` duplicates | Ignore                    |
| Drive `WIP/Archive/Legacy_Reference/` (UI v0.5, Brand v0.5)                     | Archived                  |
| `game mech 13082017.docx`, `24012015*.docx`                                     | Historical only           |
| `Metagame_Handoff.md`                                                           | Implementation notes only |


**Canonical gameplay docx:** `Myriad_of_Dragons_Game_Mechanics_v2.docx` (Drive Phase 1/Game mech/)

---

## 1. Combat

- Simultaneous 3-lane combat; integer stat model (Part II Mechanics v2).
- Rarity slot weighting: 1★–4★ = 1 slot; 5★–7★ = 2 slots per lane.
- Starting hand 4; 2 draws/turn. Resource constraint is binding.
- **Avatar spells (player):** 4 spells in code today (see §1.1). Player casts during combat via Energy.
- **AI opponent:** **AI spells are shipped in current code** under mirrored PvE Option B. AI uses the mirrored combat spell policy; this is live behavior, not a pending implementation item.
- Tie-breaker = loss for progression (anti-turtle).
- **Supersedes:** V4 Blueprint docs, Combat_Design_V4_Counter_Proposal.

### 1.1 Avatar spells shipped today (code)


| #   | Name        | Cost      | Cooldown (ticks) | Effect                                                        |
| --- | ----------- | --------- | ---------------- | ------------------------------------------------------------- |
| 0   | Firestorm   | 30 Energy | 3                | 4 damage to all enemies in lane (Avatar damage if lane empty) |
| 1   | Mend        | 25        | 3                | Heal 4 per friendly in lane                                   |
| 2   | War Cry     | 40        | 4                | +2 Attack permanently to friendly lane                        |
| 3   | Divine Bolt | 60        | 5                | **Live code: 150** direct Avatar damage (stub). **Catalog lock: 100 flat** before spell expansion ships. |


Source: `AvatarSpell.CreateDefaultSpellbook()` — **MVP stub only (4 spells).**

**Owner target (Aug 22):** **30–50** Avatar spells in full catalog; spell books / unlocks per Mechanics v2.  
**In flight:** `SPELL_CATALOG_v1.md` (locked) · `SPELL_SYSTEM_ARCHITECTURE_PACKET.md` (implementation prep).

**Loadout rule (locked):** **Max 1 spell with `AvatarStrike` tag equipped** when sun_lance / stone_judgment ship — enforce on save/equip (see architecture packet task #6).

**AI spells:** **SHIPPED** — mirrored PvE Option B is live in current code.

---

## 2. Empire — full bible building list vs Phase 1 code

### Mechanics v2 Part I §6 (full empire — not all built)


| Building              | Bible role                                             | Phase 1 code             |
| --------------------- | ------------------------------------------------------ | ------------------------ |
| **Castle**            | Deck size, rarity cap, card capacity                   | **Yes** — L30, Gold sink |
| **Barracks**          | Soldiers + TCG meta (deck slots, regen, replenishment) | **Yes** — L30 milestones |
| **Storage**           | Food/Gold capacity                                     | **No**                   |
| **Training Grounds**  | Level/fuse speed bonus                                 | **No**                   |
| **Barn & Gold Mines** | Food/Gold production faucets                           | **No**                   |
| **Gate**              | Wall defense / raid layer; TCG = chapter route         | **Yes** — Ch1–10 route   |
| **Laboratory**        | Research (deferred in bible)                           | **No**                   |
| **Tree of Knowledge** | Manual XP pool to cards                                | **No**                   |
| **Prison**            | Raid captives → sacrifice fodder                       | **No**                   |
| **Academy**           | Individual research (MOS §10)                          | **No** — Phase 1b        |
| **Embassy**           | Ally help / trade hooks (MOS §10)                      | **No** — Phase 1b        |
| **Guild Citadel**     | Collective guild building                              | **No** — server/guild    |


**Phase 1 ships 3 of 12+.** Your instinct is correct — the full game economy assumes Storage, Mines, Barn, Training Grounds, Prison, etc. Master plan §G covers sustain beyond Castle/Barracks/Gate.

**Construction UI on Home:** not built yet — backend rules exist (`EmpireConstructionService`); no Empire panel in `HomePagePresenter` today.

---

## 3. Shop & economy (Phase 1 — **numbers locked** 2026-08-22)

**Locked direction (Option C hybrid):**

- Tabs: Packs | Resources | Cosmetics | Events (later).
- **Inverse gem/card:** smallest pack = best gem/card; larger = worse **quantity** rate.
- **Bulk quality axis:** larger packs = more **High-draw** mix + disclosed rarity floors — **not** a gem/card discount.
- Loyalty + VIP memberships (Mechanics v2 spirit).
- Cosmetics = Prestige only; no competitive gem exclusives.
- **Ascension Permit:** 8/week earn, **non-purchasable** — prerequisite for Shop V2 + Evolution code.
- Rewarded ads: optional, capped, soft rewards only (§D.2 master plan) — confirm after sim.

**Locked pack SKUs (CC gates Yes):**

| SKU | Gems | Cards | Gem/card | Draw mix |
|---|---:|---:|---:|---|
| Single Sigil | 150 | 1 | **150** | 1 Normal |
| Scout Cache | 800 | 5 | 160 | 4 Normal + 1 High |
| Warband Cache | 1,650 | 9 | ~183 | 7 Normal + 2 High |
| Legion Cache | 4,000 | 20 | 200 | 16 Normal + 4 High |

Full detail: Drive `WIP/SHOP_V2_NUMBERS_PACKET.md`.

**Whale sim floors (monotonic — more spend = faster, but not weeks):**


| Tier    | $/mo   | Fail if game end before |
| ------- | ------ | ----------------------- |
| F2P     | 0      | 18 mo                   |
| Dolphin | 20     | 12 mo                   |
| Whale   | 500    | 9 mo                    |
| Mega    | 2,000  | 6 mo                    |
| Ultra   | 10,000 | **3 mo**                |

**Phase 1 shop rules (locked):** no gem→gold; stamina-only resource lane; no shop deck slots or build speed (Barracks owns slots).

**Code today (V1 stub — do not treat as design):** 4 shop items, unowned-only grants — conflicts with evolution.

---

## 4. Collection, evolution & burn

- Max card level **100**. Duplicates → evolution/burn — **never** gold when collection full.
- **1★ Common:** low but essential fodder (training XP, early sacrifice, bazaar bulk, tiny shards).
- 3★+ skill fodder; Prison/captives for sacrifice (Mechanics v2).
- Bazaar + market tax Phase 2; Treasury/inflation per Economy_Blueprint §4.5.

**Locked burn rules (2026-08-22):** Forge Credit capped at **20%** of evolution Gold fee; Dust shards at **10%** of eligible recipe; named duplicate + **Ascension Permit** required for competitive evolution. Full yield table: Drive `WIP/CARD_BURN_AND_INFLATION_PACKET.md`.

**Blocked:** Save schema for copies + levels (frozen — coordinate both seats) — **not shipped**.

---

## 5. Avatar (Phase 1)

- **Cap: 100** (locked). Mechanics v2 300-band + equipment slots 100/150/200 = Phase 2+ doc only.
- Code today: +1 level per win (`PlayerEmpireData`).
- **Curve packet:** → `AVATAR_PROGRESSION_PACKET.md` (Cursor draft).

---

## 6. UI

- Procedural uGUI; no scenes/prefabs for screens.
- Latest handoff: `Myriad_of_Dragons_UI_UX_Consolidated_Handoff_v3.docx` (Drive WIP).
- Home V3 asset kit in `Assets/Resources/UI/HomeV3/`.

---

## 7. Social, guild & trading

- MOS §11 + `Guild_Competition_Rewards_v1.md`.
- Phase 1: donations, help, store, leagues; power ranking prestige-only.

### Bazaar / market trading (Mechanics v2 §2.2)

**Bible:** Player Bazaar — 3 listing slots, cards/potions/gold/food/gear/runes, 48h listings, level gates (15+), gems never tradable.

**Code today:**


| Piece                                               | Status                                            |
| --------------------------------------------------- | ------------------------------------------------- |
| Bazaar / Marketplace **UI**                         | **Not built** — no screen in Home/Shop/Collection |
| `CurrencyManager.ExecutePlayerTrade`                | Primitive only (dragon relics)                    |
| `TradeableAssetInstance`, `inventoryAssets` on save | Schema stub                                       |
| Economy_Blueprint **Market Credits**                | Phase 2 design                                    |


**You will not see market trading in the app yet.** Phase 2 per MOS + master plan; needs server + provenance ledger.

---

## 8. Theory acceptance checklist (not Empire Gate — doc task list)

These markdown `- [ ]` boxes are **PM task tracking**, not an in-game Gate building or UI toggle.

- [x] SHOP_V2_NUMBERS_PACKET — whale sim passes; CC gates Yes
- [x] CARD_BURN_AND_INFLATION_PACKET — 1★ yields; CC gates Yes
- [x] AVATAR_PROGRESSION_PACKET — XP curve 1→100
- [ ] Save schema migration (copies, pity, permit ledger) — **ChatGPT proposal pending; human Save OK required**
- [x] Claude bible diff — `MOS_SINGLE_BIBLE_CLAUDE_DIFF_2026-08-22.md`; targeted bible patch applied 2026-08-22

---

## 9. Seat ownership

See **`docs/AI_SEAT_CHART_LOCKED.md`** (Command Centre vs Working Hands vs ChatGPT vs Claude).


