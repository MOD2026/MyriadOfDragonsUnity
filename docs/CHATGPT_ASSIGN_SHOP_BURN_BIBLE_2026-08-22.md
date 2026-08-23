# PASTE TO CHATGPT — Shop sim + card burn + bible merge (design/docs only)

You are **design/docs only**. No Unity, no code. Read the locked decisions below first.

## Canonical inputs (read these)

| File | Path |
|---|---|
| Master plan (locked decisions) | `G:\My Drive\card game\Game mech\MOD\Phase 1\Game mech\WIP\SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md` |
| Mechanics v2 extract | Unity repo `tools/mechanics_v2_extract.txt` |
| Economy draft | Unity repo `docs/Economy_Blueprint.md` |
| Empire lock | Unity repo `docs/EMPIRE_SCHEMA_LOCK_2026-08-22.md` |
| Empire feasibility | Codex `EMPIRE_FEASIBILITY_AUDIT_2026-08-22.md` |
| MOS constitution | Unity repo `docs/MOS_v1.2.md` |

---

## LOCKED — do not reopen

1. **Avatar cap Phase 1:** 100 (300-band deferred).
2. **Shop ladder:** **Option C (Hybrid)** — Mechanics v2 loyalty/VIP spirit + Blueprint shop tabs + guarantees/pity + cosmetics lane.
3. **Pricing philosophy (owner mandate):** **Inverse bulk discount.**
   - **Smallest pack = best gem-per-card value** (the “discount” lives here — friendly to casual spenders).
   - **Larger packs = worse gem-per-card** (no whale bulk discount; paying more does NOT buy efficiency).
   - Goal: long-lasting revenue + enjoyable pacing; **a heavy spender must NOT reach “game end” within a few months.**
   - **Ultra whale benchmark:** **$10,000/month** is the **binding stress test** — sim FAIL if game end projected **before 3 months**. More spend = faster progress (monotonic); see master plan §C.2 table.
4. **Card burn:** **1★ (Common) must have low but essential economic value** — not trash, not power creep. Keeps sacrifice/training/bazaar loops healthy.
5. **Rewarded ads (theory):** Optional rewarded video only; capped daily; soft rewards (gold/stamina/prestige shard); never competitive power; sim must prove ad faucet doesn’t undercut IAP. See master plan §D.2.
6. **Never:** gold payout when collection is full (duplicates → evolution/burn, not shop exhaustion).
7. **Empire Gold (coded):** Castle+Gate+Barracks ≈ 1,779,550; campaign CumCh8 ~2.2M first-clear Gold.

---

## Deliverable 1 — `SHOP_V2_NUMBERS_PACKET.md`

Save to Drive WIP: `G:\My Drive\card game\Game mech\MOD\Phase 1\Game mech\WIP\SHOP_V2_NUMBERS_PACKET.md`

Must include:

### A. SKU table (Hybrid Option C)

- Tabs: **Packs | Resources | Cosmetics | Events (later)**
- Pack tiers (propose exact gem prices): single / small / medium / large — with **inverse pricing** (show gem/card column proving small > large efficiency).
- Keep **Normal vs High draw** concept from Mechanics v2 OR justify merging into guarantee floors only.
- Loyalty points + VIP memberships (800/1500/3000) — restated for new prices.
- IAP ladder ($0.99→60 … $99.99→8500) from Blueprint unless sim forces tweak.
- Stamina, builder/deck convenience, cosmetic Prestige SKUs (relationships only).

### B. Whale / completion sim (mandatory)

Define **“game end”** as ALL of:
- Campaign Ch10 first-clear done
- Empire Castle+Gate+Barracks at L30
- Avatar level 100
- Collection: every competitive card at max level 100 (assume ~N unique ids — state assumption)
- Reasonable deck slots filled

Run scenarios — **monotonic:** higher spend = sooner game end, but each tier has a floor. **Binding constraint = ultra whale.**

| Scenario | Monthly IAP | Design target (months) | **Sim FAIL if game end before** |
|---|---:|---:|---:|
| F2P | $0 | 24–36 | 18 months |
| Dolphin | $20/mo | 18–24 | 12 months |
| Whale | $500/mo | 12–18 | 9 months |
| Mega whale | $2,000/mo | 9–12 | 6 months |
| **Ultra whale** | **$10,000/mo** | **6–12** | **3 months** |

Show math: gems purchased → packs opened → expected unique cards + dupes → evolution gold cost → empire gold. If **$10k/mo** projects game end in **under 3 months**, sim **FAILS** (economy too shallow). Verify $10k finishes **before** $500 (monotonic).

### C. Rewarded ads (theory row)

Propose capped rewarded-video faucet (master plan §D.2): placements, rewards, daily caps. Prove total ad value ≪ smallest IAP pack and doesn’t let F2P bypass gem anchor.

### C. F2P sanity

- ~250–400 gems/month earn vs time-to-first-meaningful-pack
- Campaign gold vs Empire 1.78M + fusion sinks — break-even daily gold intent from Blueprint

End with **READY FOR CC: Yes/No** — max 4 Yes/No gates.

---

## Deliverable 2 — `CARD_BURN_AND_INFLATION_PACKET.md`

Save to Drive WIP.

Must include:

### A. Rarity ladder burn roles (Phase 1)

| Rarity | Phase 1 burn role |
|---|---|
| **1★ Common** | **Essential fodder** — low XP feed, generic sacrifice for low-tier evolution steps, bazaar bulk listings, tiny dust/shard yield. Never competitive power. |
| 2★ Rare | Same lane, slightly better yield |
| 3★+ | Skill fodder (Mechanics v2), duplicate evolution, prison/captive parity |

Propose **numeric yields** (XP units, gold cost offset, dust shards) — low enough that 1★ never competes with 5★, essential enough that players don’t feel 1★ pulls are worthless.

### B. Faucets vs sinks table

- Pack opens, campaign, events → dupes → evolution gold, fusion, bazaar tax (Phase 2)
- Treasury / inflation control concept from Economy_Blueprint §4.5
- Which burns ship Phase 1 vs Phase 2 (market/server)

### C. Explicit reject list

- No “sell duplicate for gold” when collection full
- No gem purchasable competitive exclusives

End with **READY FOR CC: Yes/No**.

---

## Deliverable 3 — `MOS_SINGLE_BIBLE_v1.md` (index + extracted canon)

Save to Drive WIP. **One document** — not another handoff fork.

Structure:
1. **Authority order** (code > MOS > Mechanics v2 > this doc deltas)
2. **Combat** — 1-page canonical (integer model, lanes, simultaneous combat)
3. **Empire** — Castle/Barracks/Gate only Phase 1; Gold totals; Gate chapter route
4. **Shop/Economy** — pointer to SHOP_V2 packet when done; principles (inverse pricing, anti-whale)
5. **Collection/Evolution/Burn** — pointer to CARD_BURN packet; 1★ fodder rule
6. **Avatar** — cap 100 Phase 1
7. **UI** — extract only from `Myriad_of_Dragons_UI_UX_Consolidated_Handoff_v3.docx` (summarize; don’t duplicate art specs)
8. **Social/Guild** — MOS §11 + Guild_Competition_Rewards_v1 one-paragraph each
9. **Supersedes list** — every archived file name and “ignore reason”

Do **not** paste entire old handoffs — extract decisions only.

---

## Do NOT do

- Unity code or Save schema changes
- New competing handoff files in Codex outputs/
- Re-open Castle/Gate/Barracks Gold tables (already coded)
- Ask Zihan blank questionnaires — use assumptions and mark them

---

## Output format

Three markdown files in Drive WIP. Short executive summary at top of each. Tables over prose walls.
