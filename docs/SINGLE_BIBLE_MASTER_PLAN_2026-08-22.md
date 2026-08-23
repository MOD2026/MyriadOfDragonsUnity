# Single Bible Master Plan — theory first (Aug 22, 2026)

**Owner input:** Zihan spot-check (6 items). **Rule:** drill theory until numbers make sense, **then** code.  
**This doc:** cleanup map + workstreams. Not implementation.

**Working copies:**
- Unity repo: `docs/SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md`
- Drive WIP: `G:\My Drive\card game\Game mech\MOD\Phase 1\Game mech\WIP\SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md`

**Decisions locked (Aug 22, updated):**
- **Avatar cap:** **100** for Phase 1. Mechanics v2’s 300-band and equipment slots 4–6 (100/150/200) stay documented as Phase 2+ — not coded until theory packet accepted.
- **Shop ladder:** **Option C (Hybrid)** — loyalty/VIP spirit + modern shop tabs + pity/guarantees + cosmetics. **Pricing rule (owner mandate): inverse bulk discount** — smallest pack has the **best** gem-per-card value; larger packs pay **more** per card (no whale bulk discount). Must pass whale sim (§C.2).
- **Card burn:** **1★ (Common) = low but essential fodder** — training XP, generic sacrifice, bazaar bulk, tiny shard yield. Never competitive power. Full table in §E.1.

**Decisions locked (Aug 22, Cursor sim — no owner packet read required):**

| Topic | Lock | Evidence |
|---|---|---|
| **Divine Bolt magnitude** | **100 flat** (not 150) | `PlayerEmpireData` + `Balance_OnboardingHealthTaper_AppliesAtLevel1AndIsGoneByTier1`: L5 HP = **120**. Bolt 150 = guaranteed one-shot at first legal cast (clash 3+). Bolt 100 leaves **20 HP** — lane counterplay still matters. **Rejected:** keep 150 + shared AvatarStrike CD — CD does not stop the first cast one-shot. |
| **AvatarStrike loadout (catalog)** | **Max 1 `AvatarStrike` equipped** when Ch2+ strike spells ship | Prevents sun_lance + stone_judgment stacks; simpler than shared CD for Phase 1. Clash-3 cast gate stays. |
| **Shop V2 numbers** | **All four READY FOR CC gates: Yes** | Single Sigil 150 / Scout 800 / Warband 1650 / Legion 4000 (150 < 160 < 183.3 < 200 gem/card). **Ascension Permit 8/week non-purchasable** is mandatory — the shipped 3-step model requires **168 Permits**, which is **21 weeks at 8/week**. No gem→gold Phase 1. Stamina-only resource lane; no shop deck slots or build speed. |
| **Card burn packet** | **All four READY FOR CC gates: Yes** | Yield table + 20% Forge / 10% Dust caps; dupes never → Gold; named duplicate + Permit for evolution; Bazaar/trade/prison receipts server-gated Phase 2. |

**Seat split (until 2026-09-01):** Cursor = VS Code / metagame implementation (`HomePagePresenter`, `CampaignMapPresenter`, `ShopPresenter`, `DeckBuilderPresenter`, Ch9–10, Empire UI). Claude = doc review only — **do not edit** `CampaignMapPresenter` or campaign tests. See `docs/SEAT_ASSIGNMENT.md`.

**Decision gate (all agents — mandatory before any new lock):**

1. **Top-grosser relationship check** — compare to 2–3 live references (TCG/gacha/midcore: e.g. Pokémon TCG Pocket, Clash Royale, Hearthstone, Dislyte, HoYoverse pull economics). Copy **relationships**, not cloned prices. Cite what we match vs deliberately invert.
2. **Loophole / adversarial pass** — stress ultra whale, F2P, combo exploits, schema gaps, “fun to break” paths. If a lock fails, revise or add Phase 2 hook — do not follow owner or packet blindly.
3. **Phase 1 scope** — ship the minimum closed loop; document what expands in Phase 2 (server ledger, bazaar, extra spell effects, bulk cosmetic bundles, permit hoard cap, etc.).

See **§N** for Aug 22 benchmark + loophole audit on current locks.

---

## A. One bible — hierarchy (use this, archive the rest)

| Priority | Document | Role |
|---:|---|---|
| **1** | Running code + EditMode tests | What actually ships |
| **2** | `docs/MOS_v1.1.md` / `MOS_v1.2.md` | Constitution (priority, anti-P2W, seat rules) |
| **3** | **Drive:** `G:\My Drive\card game\Game mech\MOD\Phase 1\Game mech\Myriad_of_Dragons_Game_Mechanics_v2.docx` | **Primary gameplay bible** (2017 base + 2026 combat/empire revisions) |
| **4** | `docs/Economy_Blueprint.md` | Shop/currency/inflation **theory** (2026 draft — attack this, don’t code it yet) |
| **5** | `docs/EMPIRE_SCHEMA_LOCK_2026-08-22.md` + Codex `EMPIRE_FEASIBILITY_AUDIT_2026-08-22.md` | Empire sinks, Gate route, construction rules |
| **6** | `docs/Metagame_Handoff.md` | **Historical** implementation notes only — header says read MOS first |

**Target deliverable:** `MOS_SINGLE_BIBLE_v1.md` — one merged index + extracted canon (ChatGPT draft → Drive WIP). Until merged, **this master plan + Mechanics v2 docx** are the working bible.

### A.1 Full cleanup manifest (Aug 22)

**Principle:** Extract decisions into this doc + `MOS_SINGLE_BIBLE_v1.md`, then delete or archive duplicates. **Do not delete without checking the manifest.**

#### Unity repo `docs/` — archived (moved to `docs/_archive/`)

| File | Why archived |
|---|---|
| `V4_Blueprint_Review.md`, `V4_Blueprint_Fixes.md` | Superseded by integer combat in Mechanics v2 |
| `Combat_Design_V4_Counter_Proposal.md` | Superseded |
| `SESSION_HANDOFF_2026-08-07.md`, `Findings_2026-08-06.md` | Session snapshots |
| `PM_PARALLEL_WORK_2026-08-22.md`, `FLOW_DIAGNOSTIC_2026-08-22.md` | Session work — root cause captured in flow tests |
| `DESIGN_BIBLE_RECONCILIATION_2026-08-22.md` | Merged into this master plan §C–§G |
| `CHATGPT_ASSIGN_CASTLE_*_2026-08-22.md` | Empire costs locked in code |

#### Unity repo `docs/` — keep active

| File | Role |
|---|---|
| `SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md` | **This doc** — theory hub |
| `MOS_v1.1.md`, `MOS_v1.2.md` | Constitution |
| `Economy_Blueprint.md` | Shop/currency theory (attack in sim) |
| `EMPIRE_SCHEMA_LOCK_2026-08-22.md`, `CASTLE_GATE_GOLD_CC_ACCEPT_2026-08-22.md` | Locked empire numbers |
| `Metagame_Handoff.md` | Historical implementation notes only |
| `Mechanics_Gap_Analysis.md`, `Guild_Competition_Rewards_v1.md` | Reference until merged |
| `CHATGPT_ASSIGN_SHOP_BURN_BIBLE_2026-08-22.md` | **Paste to ChatGPT now** |
| `AI_CONTRIBUTING.md`, `START_HERE.md` | Contributor entry |

#### Downloads folder — MOD-related (scanned Aug 22)

| Path | Verdict |
|---|---|
| `C:\Users\zihan\Downloads\MyriadOfDragonsUnity` | **Canonical working Unity project** |
| `C:\Users\zihan\Downloads\MOD_BattleV5` | **Deleted Aug 22** |
| `C:\Users\zihan\Downloads\MOD_BattleV6` | **Deleted Aug 22** |
| `C:\Users\zihan\Downloads\MOD_BattleV7` | **Deleted Aug 22** |
| `C:\Users\zihan\Downloads\MOD_TutorialRecon` | **Deleted Aug 22** |
| `C:\Users\zihan\Downloads\MyriadOfDragons_AI_Handover.txt` | **Deleted Aug 22** |
| `MyriadOfDragonsUnity\handover\Myriad_of_Dragons_Command_Centre_Handover_2026-08-13.docx` | Merge into bible → archive to Drive `WIP/Archive/` |

**Recommended Zihan cleanup (manual, when ready):** Drive `_DELETE_MyriadOfDragonsUnity_20260806-155959\` when Drive releases locks.

#### Google Drive `Game mech\MOD\Phase 1\Game mech\`

| Path | Verdict |
|---|---|
| `Myriad_of_Dragons_Game_Mechanics_v2.docx` | **KEEP — primary bible** |
| `WIP\SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md` | **KEEP — working theory hub** |
| `WIP\Myriad_of_Dragons_UI_UX_Consolidated_Handoff_v3.docx` | **KEEP** until UI chapter merged |
| `WIP\Archive\Legacy_Reference\` | Already archived (v0.5 bibles) |
| `BACKUP_Myriad_of_Dragons_Game_Mechanics_v2_pre-2026-08-06.docx` | Snapshot — keep one backup |
| `game mech 13082017.docx`, `24012015*.docx`, `Adds on 11082015.docx` | Historical only — do not cite |
| `_DELETE_MyriadOfDragonsUnity_20260806-155959\` | **Safe to delete** (old Unity export + duplicate docs) |

#### Codex `Documents\Codex\`

79 markdown/doc outputs from ChatGPT sessions. **Do not treat as canonical.** Useful one-offs already copied to Unity `docs/` or referenced here:

| Codex file | Status |
|---|---|
| `EMPIRE_FEASIBILITY_AUDIT_2026-08-22.md` | Referenced — empire math |
| `CASTLE_GATE_GOLD_COST_DRAFT_2026-08-22.md` | Superseded by coded accept doc |
| `COMMAND_CENTRE_HANDOVER_2026-08-21.md`, `HANDOVER_*` | Merge → archive |
| `outputs/Battle_Screen_*`, `Home_Screen_*` V3–V7 packets | UI implementation history — extract to bible UI chapter only |

**Codex folder:** leave in place; mark “do not cite” in ChatGPT system prompt. No mass delete unless Zihan wants disk space back.

### Archive / ignore (ChatGPT mess — do not cite in design)

| Location | Treat as |
|---|---|
| `G:\My Drive\...\WIP\Archive\Legacy_Reference\` (UI_UX v0.5, Brand v0.5, etc.) | **ARCHIVE** |
| `G:\My Drive\...\BACKUP_Myriad_of_Dragons_Game_Mechanics_v2_pre-2026-08-06.docx` | Snapshot only |
| `G:\My Drive\...\game mech 13082017.docx` | Historical reference only |
| Codex `outputs/document_updates/*` | ChatGPT copies — diff against `docs/` in Unity repo, don’t fork a third truth |
| Multiple handoffs: `HANDOVER_*`, `COMMAND_CENTRE_*`, `Metagame_Handoff` duplicates in Codex/work/ | **Merge into MOS_SINGLE_BIBLE** or archive |
| `docs/V4_Blueprint_Review.md` / `V4_Blueprint_Fixes.md` | Superseded by integer combat model in Mechanics v2 |

**Keep (working):**

| File | Why |
|---|---|
| Mechanics v2 docx | Your 10-year design + 2026 combat |
| `WIP\Myriad_of_Dragons_UI_UX_Consolidated_Handoff_v3.docx` | Latest UI handoff — merge into single bible UI chapter |
| Unity `docs/Economy_Blueprint.md` | Modern shop/packages/cosmetics theory |
| Unity `docs/EMPIRE_*` + Codex Empire packets | Empire cost / feasibility (you asked about this — **found**, see §F) |

---

## B. Your point 1 — compile handoffs → one bible

**Cursor task (docs only, next):**

1. Inventory every `*Handoff*`, `*Bible*`, `*Blueprint*` under `docs/`, Drive `card game`, Codex folder.
2. Produce `docs/MOS_SINGLE_BIBLE_v1.md` with sections: Combat, Empire, Shop/Economy, Collection/Evolution, Avatar, UI, Social/Guild.
3. Each section: **canonical paragraph** + “supersedes: [file list]”.
4. Move superseded files to `docs/_archive/` (or Drive `WIP/Archive/`) — **no code change**.

**Your task:** When draft exists, one pass: “wrong / missing / keep” per section (30 min).

---

## C. Your point 2 — Shop numbers (2010s doc vs today)

Three layers exist — **they conflict**. Theory must reconcile before Shop V2 code.

| Layer | Card packs | Stamina | Gems IAP |
|---|---|---|---|
| **Mechanics v2 (2017 carry-forward)** | 1×300, 5×1400, 9×2500, 20×5000 gems; normal/high draw odds | Regen 60s base | VIP weekly 800 / fortnight 1500 / monthly 3000 gems |
| **Economy_Blueprint (2026 draft)** | 100 gems / 5-card; 900 gems / 10-pack guaranteed Epic+ | 30 gems +50 (escalating daily) | $0.99→60 gems … $99.99→8500 |
| **Code V1 today** | 500 gold OR 100 gems → next unowned card only | 30 gems +50 stamina | None |

**Theory work (ChatGPT packet + CC sim, no Unity):**

1. Recompute **campaign first-clear Gold** through Ch10 vs all sinks (Empire 1.78M + evolution + shop gold SKUs).
2. **Option C (Hybrid) locked** — sim must produce exact gem prices with inverse bulk discount.
3. Validate **F2P gem earn** (~250–400/mo in Blueprint) vs prices.
4. Output: `WIP/SHOP_V2_NUMBERS_PACKET.md` with **READY FOR CC: Yes/No** gates.

**Your calls locked:** Option C + inverse pricing + whale guardrails (§C.2). Exact numbers = sim output.

### C.1 Historical options A & B (reference — superseded by C)

**What “shop ladder” means:** pack SKUs, gem prices, odds/guarantees. Resources/cosmetics/bundles sit on top.

#### Option A — Mechanics v2 (2017 carry-forward)

| SKU | Gems | Cards | Effective gem/card |
|---|---:|---:|---:|
| Single draw | 300 | 1 | 300 |
| 5-pack | 1,400 | 5 | 280 |
| 9-pack | 2,500 | 9 | ~278 |
| 20-pack | 5,000 | 20 | 250 |

**How it works:** Two draw modes — **Normal** (70/20/10 for 3★/4★/5★) and **High** (better odds on bigger packs only; e.g. 20-pack High ≈ 20/45/35). **Loyalty points** on every purchase → redeem for memberships or specific cards. **VIP memberships** (800 / 1,500 / 3,000 gems) bundle stamina perks + periodic packs.

**Pros:** Matches your original bible; rewards whales with bulk High draws; loyalty + VIP are proven retention hooks in your doc.

**Cons:** Prices feel “2017 mobile” (300 gems for one card is steep vs modern $0.99 entry packs); no explicit Epic/Legendary pity language; weak standalone **cosmetic** lane; one gem currency still buys most things.

**When it fits:** You want to preserve the **feel** of the original economy and already plan loyalty + VIP as core loops.

---

#### Option B — Economy_Blueprint (2026 draft)

| SKU | Gems | Cards | Notes |
|---|---:|---:|---|
| Standard pack | 100 | 5 | 1 Rare+ guaranteed |
| Dragon Booster | 300 | 5 | 1 Epic+ guaranteed |
| 10-pack | 900 | 50 | 1 Legendary guaranteed; pity at 10 |

Plus **separate tabs**: Resources (stamina, gold conversion, reagents), Cosmetics (300–2,000 gems), Bundles (season pass 900, weekly value 500), Events/Market later.

**IAP anchor:** $0.99 → 60 gems … $99.99 → 8,500 gems (~$0.0165/gem). F2P gem earn ~250–400/month.

**Pros:** Modern SKU split (packs vs stamina vs skins); explicit guarantees/pity; anti-P2W rules written down; gold sinks tied to fusion/evolution (500→12,000 by rarity).

**Cons:** **Conflicts with Mechanics v2 pack table** (100-gem pack vs 300-gem single); numbers are proposals — §4.1 faucet/sink math still needs campaign-through-Ch10 sim; not yet validated against Empire 1.78M gold sink.

**When it fits:** You want **today’s mobile shop shape** (builder/deck/stamina/cosmetics as separate products) and are willing to **amend** Mechanics v2 pack prices rather than copy them verbatim.

---

#### Option C — Hybrid (**LOCKED — Aug 22**)

Keep **Mechanics v2’s multi-tier pack + loyalty + VIP spirit**, adopt **Blueprint’s** tab structure, guarantees/pity, and cosmetics lane. **Gem prices come from sim**, not copied verbatim from A or B.

**Owner pricing rule — inverse bulk discount:**

| Pack size | Gem/card trend | Player psychology |
|---|---|---|
| **Smallest (1 card)** | **Best value** — the only place with a “discount” | Casual-friendly; encourages frequent small purchases |
| Medium | Worse gem/card | Convenience, not efficiency |
| Largest | **Worst gem/card** | Whales pay premium for volume — **cannot buy their way to game-end cheaply** |

This **replaces** Mechanics v2’s old pattern where 20-packs had *better* odds *and* better gem/card (280→250). High-draw odds may remain on large packs for **card quality**, but **must not** combine bulk gem discount + bulk power — that’s what lets whales finish in months.

**Example shape (SUPERSEDED — see §N and `SHOP_V2_NUMBERS_PACKET.md` for locked SKUs 150/800/1650/4000):**

| SKU | Gems | Cards | Gem/card | Notes |
|---|---:|---:|---:|---|
| ~~Single~~ | ~~120~~ | ~~1~~ | ~~**120**~~ | **Use Single Sigil 150** |
| ~~Small pack~~ | ~~650~~ | ~~5~~ | ~~130~~ | **Use Scout 800** |
| ~~Medium pack~~ | ~~1,400~~ | ~~9~~ | ~~~156~~ | **Use Warband 1650** |
| ~~Large pack~~ | ~~3,200~~ | ~~20~~ | ~~160~~ | **Use Legion 4000** |

### C.2 Whale / completion guardrails (**mandatory sim**)

**“Game end”** = all of: Ch10 clear, Empire L30 (Castle+Gate+Barracks), Avatar 100, full collection max-level 100, all competitive deck slots filled.

**Owner context:** real whales may spend **~$10,000/month**. The economy must survive that spend without going hollow in weeks.

**How to read this table:** More money = **faster** progress (that is normal and expected). Each tier has a **sim floor** — “game end before month X = economy is too shallow.” The **binding constraint** is ultra whale: if $10k/month burns through everything in 2–3 months, the sim fails even if lower tiers look fine.

| Spend tier | Monthly IAP | Design target (months to game end) | **Sim FAIL if game end before** |
|---|---:|---:|---:|
| F2P | $0 | 24–36 | **18 months** |
| Dolphin | ~$20 | 18–24 | **12 months** |
| Whale | ~$500 | 12–18 | **9 months** |
| Mega whale | ~$2,000 | 9–12 | **6 months** |
| **Ultra whale** | **~$10,000** | **6–12** | **3 months** |

**Monotonic rule:** A $10k spender **must** reach game end **sooner** than a $500 spender — but **no** payer should blast through in a few weeks. Ultra whale’s floor (3 months) is the **hardest stress test**, not a requirement to play 12 months.

**Hard rule (binding):** At **$10k/month**, sim projects game end in **under 3 months** → **FAIL**. Tighten large-pack gem/card, evolution gold, prestige/cosmetic sinks, empire Phase 1b — **never** gut small-pack value.

Lower tiers failing their floor → same levers, but ultra whale is always re-checked last.

**Deliverable:** `WIP/SHOP_V2_NUMBERS_PACKET.md` (ChatGPT — see §L).

---

## D. Your point 3 — Modern packages + aesthetics (skins)

**Economy_Blueprint already points this direction** (§4.2, §3 taxonomy):

- Separate SKUs: **packs**, **stamina**, **builder/deck convenience**, **season pass**, **cosmetics (Prestige)**.
- Rule: **Competitive power never gem-only**; money = speed; **Prestige/skins can be exclusive**.

**Mechanics v2 gap:** one gem currency buys almost everything; weak cosmetic lane.

**Theory work:**

1. Define **Shop tabs**: Packs | Resources (gold/stamina/builder) | Cosmetics | Events (later).
2. Map **aesthetic gold mine**: avatar frame, board skin, castle decoration, card back — all `powerClass: Prestige`.
3. Compare 2–3 grossing TCG/midcore references (deck builder packs, stamina, battle pass, skin shop) — **relationships only**, not cloned prices.

**No code** until Shop V2 schema + SKU table accepted.

---

## D.2 Rewarded ads — theory (**open decision, lean yes with caps**)

**Question:** Should players watch ads for rewards?

**Recommendation for Phase 1:** **Yes — optional rewarded video only**, with strict caps. **No** forced interstitials during battle or story. Ads are a **F2P faucet**, not a whale replacement.

| Principle | Rule |
|---|---|
| Optional | Player taps “Watch ad” — never blocks core loop |
| Cap | Hard daily cap (e.g. 3–5 views/day total across all ad placements) |
| Rewards | **Soft currency only** — small gold, +20 stamina, or 1 daily-login bonus step. **Never** gems at a rate that competes with IAP. **Never** competitive cards or exclusive power |
| Whale-neutral | $10k/month players never need ads; ad rewards ≪ smallest IAP pack value |
| Bot risk | Ad views tied to account + device; no tradable reward; daily cap per Economy_Blueprint bot review (§9) |
| Placement | Home idle, shop “free daily”, stamina empty state — **not** mid-match |

**Why include:** Extends F2P retention without undercutting IAP if capped low (~$0.50–1.00 equivalent/day max). Common in mobile TCG/midcore.

**Why careful:** Too-generous ad gems collapse IAP ladder and inverse-pricing model. Interstitials hurt session quality for paying players.

**Phase 1 proposal (theory — sim in SHOP packet):**

| Ad placement | Reward | Daily cap |
|---|---|---|
| +Stamina | +20 stamina | 2 views |
| +Gold | +500 gold | 1 view |
| Shop “daily bonus” | +1 loyalty point or cosmetic shard (Prestige) | 1 view |

**Decision gate:** Include in `SHOP_V2_NUMBERS_PACKET.md` ad-faucet row; mark **READY FOR CC: Yes/No** after sim proves ads don’t let F2P bypass IAP anchor.

**Not locked yet** — owner confirm after seeing sim numbers. Default direction: **rewarded-only, capped, soft rewards**.

---

## E. Your point 4 — Evolution + card burn (TCG inflation / trading)

**Mechanics v2 (still directionally valid):**

- Duplicates → **Evolution/Fusion** (raise max level toward 100).
- **Bazaar** trading (cards, potions, gold, food, gear — not gems).
- **Prison/captives** as sacrifice fodder.
- Gallery rewards at max level.

**Economy_Blueprint adds:**

- **Treasury / inflation control** (§4.5).
- **Market Credits** + 12% tax (6% burn) — Phase 2.
- Event token **expiry** — closed economies.

**Gap vs hardcore TCG today:** no **bulk burn** paths (dust, shard, limit break fodder, season retire, craft sink).

### E.1 Rarity burn roles (**locked direction — Aug 22**)

**Owner rule:** **1★ (Common) must have low but essential value** — keeps the economy healthy without making commons competitive.

| Rarity | Phase 1 role | Power |
|---|---|---|
| **1★ Common** | Training Ground XP feed (small), generic evolution sacrifice for **early** max-level steps, bazaar bulk listings, **tiny** shard/dust yield on overflow dupes | **Never** deck-viable at endgame |
| 2★ Rare | Same lanes, ~2× 1★ yield | Fodder only |
| 3★+ | Skill fodder (Mechanics v2), duplicate evolution, Prison/captive parity | Competitive path |

**Design intent:** A new player who pulls mostly 1★/2★ still feels packs matter — fodder feeds evolution and gold sinks — but chasing 5★/6★ remains the competitive goal. Exact XP/shard numbers → `CARD_BURN_AND_INFLATION_PACKET.md`.

**Reject:** “Collection full → sell duplicate for gold” (Shop V1 audit idea). Duplicates always enter a **burn path**.

**Theory work — Phase 1 burn menu:**

| Burn path | Role |
|---|---|
| Evolution (duplicate → max level) | Core — already in bible |
| Limit break / stat ceiling | Mechanics v2 mentioned, curve TBD |
| **Dust/shards** from duplicate overflow | Modern standard (Hearthstone, etc.) |
| Bazaar listing tax + expiry | Already in 2017 doc |
| Event-only craft reagents | Economy_Blueprint |
| Prestige conversion (duplicate → cosmetic token) | Ties to your skin revenue point |

**Deliverable:** `WIP/CARD_BURN_AND_INFLATION_PACKET.md` — faucet/sink table + 1★ numeric yields.

**Blocked on:** Save schema (`cardCollection` must become copies + level) — **theory first**, then frozen Save coordination.

---

## F. Your point 5 — Avatar cap (**locked: 100 for Phase 1**)

| Source | Avatar cap | Notes |
|---|---|---|
| **Mechanics v2** | **300** (phased; higher bands later) | Equipment slot 4th at 100, 5th at 150, 6th at 200 |
| **Mechanics v2** | Character/card max **100** | Different from Avatar |
| **Phase 1 decision (Zihan, Aug 22)** | **100** | Matches card max; simpler progression; equipment slots 4–6 deferred |
| **Code today** | Wins → `avatarLevel`; empire-derived HP/Resource | No equipment slots built |

**Phase 2+ (document only until accepted):** Avatar 101–300 curve, flag milestones, equipment slots at 100/150/200 — stays in Mechanics v2 as future band, not MVP scope.

**Theory deliverable:** `docs/AVATAR_PROGRESSION_PACKET.md` — XP curve 1→100, win/loss XP, empire tie-in. **No Save schema change until packet accepted.**

---

## G. Your point 6 — Empire costs / “not enough to sustain”

**Found — multiple files:**

| File | What it says |
|---|---|
| `docs/EMPIRE_SCHEMA_LOCK_2026-08-22.md` | Castle/Barracks/Gate only; Phase-1 Gold → instant `ReadyToCollect` (Offline A); Gold+time Phase 2; Gate Ch1–10 |
| Codex `EMPIRE_FEASIBILITY_AUDIT_2026-08-22.md` | Campaign Gold totals; **cost bands** L2–30 per building |
| `docs/CASTLE_GATE_GOLD_CC_ACCEPT_2026-08-22.md` | Castle cum L30 = **727,450** (coded) |
| Coded Barracks | L1→L30 total = **411,200** |
| Coded Gate | Gold table + chapter route (coded) |
| **Combined sink** | Castle + Gate + Barracks ≈ **1,779,550** vs ~**2.2M** Ch8 campaign headroom |
| `docs/MINIGAME_EMPIRE_RANKING_ANALYSIS_2026-08-22.md` | TD/mini-games as **second prestige ladder** so Empire isn’t only campaign sinks |

**Your instinct is right:** **Castle/Barracks/Gate alone** don’t carry a live game — bible always had Food, Barn, Mines, Training Grounds, Embassy, events.

**Theory path (already partially locked):**

1. **Phase 1 sinks:** Empire construction (coded numbers) + Evolution materials (when schema exists).
2. **Phase 1b:** Academy / Embassy / scarcity resources (raid/trade — deferred in constitution).
3. **Phase 2:** Tower/mini-game ranking (`MINIGAME_EMPIRE_RANKING`) — drives building upgrades without P2W combat stats in shop.

**Recompute needed after Ch10 rewards land** (feasibility audit says this explicitly).

---

## H. What we already have vs what we’re still drafting

**You should not worry about “missing answers.”** The earlier questions were **theory gates**, not blockers for current work. Here is the inventory:

| Topic | Already in hand | Still drafting (theory docs only) |
|---|---|---|
| Gameplay bible | Mechanics v2 docx + extract | `MOS_SINGLE_BIBLE_v1.md` merge index |
| Constitution | MOS v1.1/v1.2 | — |
| Shop design intent | Mechanics v2 Part VI + Economy_Blueprint | `SHOP_V2_NUMBERS_PACKET.md` (Option C + inverse pricing sim) |
| Evolution / burn | Mechanics v2 Part III + §E.1 | `CARD_BURN_AND_INFLATION_PACKET.md` |
| Empire costs | EMPIRE_SCHEMA_LOCK, feasibility audit, coded totals | Recompute after Ch10 rewards |
| Avatar | **Locked: cap 100** | `AVATAR_PROGRESSION_PACKET.md` (curve only) |
| Code reality | DESIGN_BIBLE_RECONCILIATION | — |

**Your review gates:** (1) `SHOP_V2_NUMBERS_PACKET` when ChatGPT delivers — check whale sim passes. (2) One pass on `MOS_SINGLE_BIBLE_v1`. (3) Optional: delete Downloads MOD forks per §A.1.

| Who | Now |
|---|---|
| **Zihan** | Paste §L assignment to ChatGPT. Review whale sim includes **$10k/mo tier**. Confirm ads direction after sim (§D.2). |
| **ChatGPT** | Three Drive WIP deliverables (§L) — **primary parallel work**. |
| **Cursor** | Ch10 + Empire UI (locked scope). No Shop V2 / Evolution code until packets accepted. |

---

## L. ChatGPT assignment (paste now)

Full prompt: Unity repo `docs/CHATGPT_ASSIGN_SHOP_BURN_BIBLE_2026-08-22.md`  
Copy to Drive WIP: same filename recommended.

| # | Deliverable | Output path |
|---:|---|---|
| 1 | Shop sim — Option C, inverse pricing, whale guardrails | `WIP/SHOP_V2_NUMBERS_PACKET.md` |
| 2 | Card burn — 1★ essential fodder + inflation table | `WIP/CARD_BURN_AND_INFLATION_PACKET.md` |
| 3 | Merged bible index + extracted canon | `WIP/MOS_SINGLE_BIBLE_v1.md` |
| 4 | **Spell catalog 30–50 + AI policy** | `WIP/SPELL_CATALOG_v1.md` — see `CHATGPT_ASSIGN_SPELL_CATALOG_2026-08-22.md` |

**ChatGPT does NOT:** Unity code, Save schema, reopen empire Gold tables.

---

## M. Spell expansion (owner Aug 22)

**4 spells = wartime stub.** Full game needs **30–50** (ChatGPT catalog) + data-driven `SpellDatabase` (Cursor architecture packet). Claude continues Ch9–10 / Empire UI on track A; spell work is **track B** after catalog accept.

---

## M. Extracted canon (living in this doc until MOS_SINGLE_BIBLE_v1 ships)

| Domain | One-paragraph canon |
|---|---|
| **Combat** | Simultaneous lane combat, integer stats, 3 lanes, rarity slot weighting (1–4 = 1 slot, 5–7 = 2 slots). Mirrored PvE AI spells (Option B) **shipped**; SoloAIScaling still applies for HP/difficulty. |
| **Empire Phase 1** | Castle + Barracks + Gate only. Gold → instant `ReadyToCollect` construction (Offline A). Gold+time construction is Phase 2. Gate chapters Ch1–10. Combined sink ~1.78M gold. |
| **Shop Phase 1** | Hybrid tabs; inverse bulk discount; loyalty/VIP; cosmetics Prestige-only; no competitive gem exclusives. |
| **Collection** | Duplicates → evolution/burn, never gold exhaustion. 1★ = essential fodder. Max card level 100. |
| **Avatar** | Cap 100 Phase 1. Wins grant XP; empire-derived HP/Resource. |
| **Social** | Guild Phase 1 per MOS §11; power ranking prestige-only. |

---

## I. Coding freeze until these accept docs exist

- Shop V2 SKUs and schema
- Card collection schema (copies, levels, burn paths)
- Avatar XP curve details (cap **100** is locked; curve is not)
- Any new Empire building beyond Castle/Barracks/Gate

**Already coded and locked (may ship after UI):** Castle/Gate/Barracks Gold, construction service, Gate chapter table.

---

## J. Link to prior work

Reconciliation content from `docs/_archive/DESIGN_BIBLE_RECONCILIATION_2026-08-22.md` is merged into §C–§G.  
This master plan **supersedes** blank shop questionnaires.

---

## N. Top-grosser benchmark + loophole audit (Aug 22 locks)

**References (relationships only):** Pokémon TCG Pocket (pack stamina + battle pass), Clash Royale (gem SKUs + duplicate progression), Hearthstone (dust burn ~12.5% craft value), Dislyte (simple 3-tab store), gacha ascension (named dupes + weekly material gates).

| Lock | Industry norm | Our Phase 1 | Verdict | Phase 2 expansion hook |
|---|---|---|---|---|
| **Inverse bulk pricing** | Larger currency packs = **better** $/gem (Clash Royale, HoK) | Smallest pack = **best** gem/card; bulk = **High-draw + rarity floors only** | **Keep — owner golden rule.** Two-axis: **quantity** (gem/card) inverse; **quality** (rarity %) may rise on bulk — disclosed, not a hidden discount. Phase 2: cosmetic bulk OK. | Prestige bulk SKUs only |
| **Single Sigil 150 gems** | ~$1.5–2.5 per gacha pull (HoYoverse band) | 150 gems ≈ 2.5× entry $0.99 pack (60 gems) | **Aligned** — not undercutting F2P anchor; 1–2 pulls/month at 250–400 F2P gems matches Pocket-style pacing | Event bonus pulls |
| **Ascension Permit 8/wk** | Dupes + weekly domain materials (Genshin ascension, Star Savior limit break) | Non-purchasable permit gates **fusion throughput** not ownership | **Keep — required for $10k/mo guard.** Industry uses similar time gates. | **Loophole found:** permit **hoarding** undefined — Phase 2 schema should cap stack (e.g. 16) or weekly spend cap |
| **No gem→gold** | Many games sell gold (Clash Royale) | Blocked while Empire sink live | **Keep for Phase 1** — protects 1.78M sink. Revisit Phase 2 with audited faucet if post-empire gold drought reported | Capped gold SKU after Empire L30 |
| **Burn table + 20%/10% caps** | Hearthstone dust: disenchant ≈ **12.5%** of craft cost | Forge 20%, Dust 10% of recipe | **Conservative vs HS** — commons still matter (1★ fodder rule) without craft-from-dust alone | Bazaar tax ledger |
| **Dupes never → Gold** | HS never pays gold for dupes | Same | **Industry standard** | — |
| **Bolt 100 + clash 3** | Direct burst spells exist (Clash elixir trades) but rarely flat one-shot at onboarding HP | 100 on L5 (120 HP) leaves 20; clash 3 gate | **Keep.** 150 failed loophole test (first cast kill). | Max 1 AvatarStrike in loadout → Phase 2 shared strike CD if multi-strike meta |
| **Max 1 AvatarStrike loadout** | Some battlers allow multiple finishers | Phase 1 catalog rule | **Conservative — good for Phase 1.** Prevents sun_lance + stone stack without new code | Shared CD when >1 strike spell common |

**Loopholes still open (document, don’t code yet):**

1. **Combo burst:** War Cry + lanes + Bolt 100 can still delete L5 — intentional skill expression, not a shop/spell economy bug.
2. **Permit hoard:** whale completes collection then instant-fuses if campaign dumps permits — add stack cap in Collection schema proposal.
3. **High-draw on bulk packs:** inverse **gem/card** but bulk still offers High-draw floors — watch that quality + volume doesn’t feel like hidden bulk discount; telemetry row required before live.
4. **Live code drift:** Unity stub still Divine Bolt **150** — catalog lock only until spell expansion; mark in implementation checklist.
5. **Late-collection bulk variance (Claude diff Aug 22):** near-full collection, **effective gems-per-new-card** may favor bulk packs (many draws per purchase) even when nominal gem/card is inverse — monitor duplicate rate by pack size at >80% collection completion; add shop telemetry row.
6. **AvatarStrike loadout cap unenforced:** “Max 1 AvatarStrike equipped” is doc-only until spell architecture task #6 ships validation on save/equip.

**Not blindly following owner:** inverse bulk **contradicts** top-grosser default; kept because §C.2 ultra-whale floor is binding constitution. If live data shows whale churn from “punished” bulk buyers, adjust **cosmetic** bundles first, never small-pack card efficiency.

---

## O. Bible consolidation status + near-future risk forecast (Aug 22)

### O.1 Have we consolidated? **Partially — not done.**

| Layer | Status | Problem if ignored |
|---|---|---|
| **Mechanics v2 docx (2017→2026)** | Primary for **unbuilt** systems (12 buildings, Bazaar, Prison, VIP packs 300-gem singles, Avatar 300-band) | Agents cite 2017 numbers and build wrong Phase 1 scope |
| **`SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md`** | **Theory hub + locks** — most current for Aug 22 decisions | Best CC reference |
| **Drive `MOS_SINGLE_BIBLE_v1.md`** | ChatGPT merge — **may differ** from Unity copy; §3 still says “numbers pending”; §8 wrongly `[x]` save schema | Blind overwrite causes regression |
| **Unity `docs/MOS_SINGLE_BIBLE_v1.md`** | Stale skeleton pointer | Do not cite over Drive without diff |
| **`Economy_Blueprint.md`** | 2026 draft — **conflicts** locked shop (100-gem 5-packs, gem→gold, 8500 top IAP) | Coding from Blueprint violates CC gates |
| **`MOS_v1.1/v1.2`** | Constitution + §19 progress table | Living; must update when chapters ship |
| **Codex folder (~79 docs)** | **Do not cite** | Third truth fork |
| **Running code + tests** | Priority 1 truth | Shop V1 stub (500g unowned-only) **≠** any bible |

**Consolidation verdict:** Decisions are **locked in master plan + OWNER_REVIEW_LOG**. Full single bible is **not** trustworthy until Claude diff assign completes and Drive merge is patched once (not blind overwrite).

**Canonical read order for any agent:**

1. Code + tests  
2. `MOS_v1.2.md`  
3. `SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md` + `OWNER_REVIEW_LOG.md`  
4. Mechanics v2 docx — **Phase 2+ only** unless master plan explicitly imports a section  
5. Drive packets (`SHOP_V2`, `CARD_BURN`, `SPELL_CATALOG`)  
6. `Economy_Blueprint.md` — attack surface only, not SKU source  

### O.2 2017 bible vs your direction — structural gaps

| 2017 / Mechanics v2 carries | Your Phase 1 lock | Risk |
|---|---|---|
| 12+ empire buildings, Food/Barn/Mines | Castle + Barracks + Gate only | **Expectation gap** — players see bible features that don’t exist; need UI “Phase 2” labels |
| Avatar cap **300**, equipment slots @100/150/200 | Avatar cap **100**, no equipment | Old doc references confuse tuning |
| VIP 800/1500/3000 + **300-gem single draw** | Option C + **150-gem Single Sigil** + inverse bulk | Price table drift if someone copies docx verbatim |
| Bazaar, Prison, captives live economy | Server-gated Phase 2 | Premature UI promises inflation |
| AI casts spells / full spell books | Mirrored PvE AI spells (Option B) are **shipped** in current code. | Treat as live combat behavior; future catalog expansion still requires normal balance validation. |
| Normal battle gold faucets | Phase 1 intent **0** ordinary Normal/Tutorial gold | If code still prints gold, Empire sink is fake (see EMPIRE_SCHEMA_CC_REVIEW E1) |
| Card evolution via duplicates + gold | + **Ascension Permit** throughput gate | Schema not built — biggest implementation cliff |
| “Collection full → sell dupes” old ideas | Dupes → burn materials **never gold** | Shop exhaustion exploits if reintroduced |

### O.3 Near-future issues (predicted, with your direction)

| When | Issue | Likely cause | Mitigation (Phase 1) |
|---|---|---|---|
| **Ch10 lands** | Campaign Gold total exceeds feasibility audit (~2.2M was ~Ch8) | Linear +10g/+2 gems × 30 × 10 chapters | Recompute empire+evolution headroom **before** tuning evolution gold costs |
| **Ch10 done → APK play** | Gate/Avatar/collection gates not wired — only stage-win unlock | Constitution wants multi-gate; code is soft | Ship with stage-only unlock; document “gates Phase 1b” — don’t fake UI |
| **Empire UI on Home** | Barracks shows regen/replenish that don’t affect combat | Fake depth (EMPIRE_SCHEMA_CC_REVIEW) | UI shows **only** deck slots + construction; hide dead rates |
| **Shop V2 code starts** | V1 `ShopPresenter` sells gold + unowned-only cards | Code ≠ locked design | Replace presenter behind feature flag; **never** ship gem→gold |
| **Save schema merge** | Dual-write Profile vs EmpireData bugs | Historical deck/class failures | Single completion API; human coordinates frozen Save |
| **Whale spends on Legion Cache** | Feels like “paying more for worse deal” without clear UX | Inverse gem/card is unusual | **Disclose:** bulk = High-draw odds, not card discount |
| **Permit + campaign rewards** | Campaign grants permit burst → fusion spike | Hoarding loophole | Stack cap in schema proposal (ChatGPT assign) |
| **Spell expansion** | Catalog Bolt 100 vs code 150; 14 spells need effect types | Three truths | One migration PR: catalog + `AvatarSpell` + tests together |
| **HomeV3 art** | Assets in `Resources/UI/HomeV3/` not fully wired | Art ahead of presenters | Working Hands wires after Ch10 or parallel track — **one seat** on `HomePagePresenter` |
| **Sep 2026 VS Code opens** | Two agents edit metagame again | Seat chart expiry | Re-read `AI_SEAT_CHART_LOCKED.md` before split |

### O.4 What still needs one consolidation pass (assign Claude now)

Paste `CLAUDE_ASSIGN_BIBLE_DIFF_2026-08-22.md` — output should drive **one surgical patch** to Drive `MOS_SINGLE_BIBLE_v1.md` (shop numbers filled, save schema unchecked, Bolt 100, permit rule), not a full rewrite.

**Owner spot-check (15 min):** §O.2 table — mark any row “defer Phase 2” vs “must ship Phase 1.”
