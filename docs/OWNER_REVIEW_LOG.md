# Owner review log (Cursor auto-lock from sim — revert in chat if wrong)

## Spell catalog — **LOCKED**

| Gate | Answer | Date | Source |
|---:|---|---|---|
| 1 36 / 4 loadout / 14 Phase-1 | Y | 2026-08-22 | Owner |
| 2 flat strike magnitudes + clash-3 gate | Y — **Bolt magnitude revised to 100** | 2026-08-22 | Cursor sim (`Balance_OnboardingHealthTaper_*`, L5 HP=120) |
| 3 mirrored PvE AI spells | Y | 2026-08-22 | Owner |
| 4 Phase-2 effects only | Y | 2026-08-22 | Owner |

**Spell catalog Drive file:** divine_bolt **100** — live Unity aligned 2026-08-22.

---

## Collection schema — **LOCKED (Owner 2026-08-22)**

Source: `COLLECTION_SCHEMA_PROPOSAL_v1.md`

| Gate | CC | Owner | Notes |
|---:|---|---|---|
| 1 additive fields + legacy preserve | **Yes** | **Y** | |
| 2 Permit cap 16, 8/wk, server week key | **Yes** | **Y** | |
| 3 atomic migration + rollback | **Yes** | **Y** | Save opened 2026-08-22 |
| 4 tests before pack UI | **Yes** | **Y** | |

**Implementation:** Save V1 fields + migration landed 2026-08-22. `CollectionPackReceiptService` + §10 EditMode **11/11 green** (2026-08-22); pack UI still gated.

---

## Shop — **LOCKED (Owner 2026-08-22)**

Source: `SHOP_V2_NUMBERS_PACKET.md`

| Gate | CC | Owner | Rationale |
|---:|---|---|---|
| 1 pack prices + inverse gem/card | **Yes** | **Y** | 150/800/1650/4000; bulk = High-draw quality only |
| 2 Ascension Permit 8/wk, non-purchasable | **Yes** | **Y** | Fusion throughput gate |
| 3 no gem→gold while Empire sink active | **Yes** | **Y** | Protects 1,779,550 Gold sink |
| 4 stamina-only resources; no shop slots/build speed | **Yes** | **Y** | Barracks owns deck slots |

**Code:** receipt service + §10 tests green; gem pack-open UI still blocked until Shop wiring review.

---

## Burn — **LOCKED (Owner 2026-08-22)**

Source: `CARD_BURN_AND_INFLATION_PACKET.md`

| Gate | CC | Owner | Rationale |
|---:|---|---|---|
| 1 burn-yield table | **Y** | **Y** | Literal table in `CollectionBurnRules.cs` (packet anchors 1/4/7) |
| 2 dupes never gold | **Y** | **Y** | |
| 3 Ascension Permit for evolution | **Y** | **Y** | Pairs with shop whale guard |
| 4 bazaar Phase 2 | **Y** | **Y** | Server ledger required |

**Code:** `CollectionBurnService` + `CollectionEvolutionService` + sinks (forge/dust on evolve, training XP→level, **sacrifice credit on step 0→1**) wired 2026-08-23.

**Claude re-audit 2026-08-23 (second pass):** All 5 collection economy gates **Yes** on live tree.

**CC MVP locks (revert in chat if wrong — 2026-08-23):**

| Constant | Value | Rationale |
|---|---|---|
| `GenericSacrificeCreditsFirstStep` | **1** | **Owner+ChatGPT locked 2026-08-23** — `SACRIFICE_CREDIT_SINK_CHECK_2026-08-23.md` READY 3/3 Yes; flat 1, no rarity scale |
| `XpCostForNextLevel` | **100 × current level** | Phase-1 training sink stub until Mechanics v2 skill gauge packet ships |

**Gold pacing (ChatGPT 2026-08-23):** Ch1–Ch10 first-clear **3,862,910** Gold; Empire sink **1,779,550**; remainder **2,083,360**. Empire fundable Ch8.

**Evolution Gold scalar (ChatGPT + WH B.5 — shipped 2026-08-23):** `1000 × rarity × (step+1)` in `CollectionEvolutionRules`. EditMode **12/12** (`CollectionEvolutionTests` + `CollectionBurnRulesTests`).

**Claude batch #2 (2026-08-23):** Block B audit all Yes · GoldCost 1000 confirmed · Forge/Dust relative weakness flagged (ChatGPT next) · future Permit **4/wk + hoard 8** locked together · §7 three P1 tests → WH Block C · AI spells live in prod (BalanceSim AI-ON gap → WH) · Shop month-15 claim patched on Drive by CC.

**Forge/Dust vs Gold 1000 (`FORGE_DUST_YIELD_VS_GOLD1000_2026-08-23.md` — CC locked):** **Option A** — keep burn Forge/Dust yield table unchanged. Do not ×5 rescale (would weaken Gold sink; Dust is not a Gold offset). Claude’s “5× harder relative forge relief” is intentional scarcity.

**Claude batch #4 (2026-08-23):** Empire Soft-block → **CC locks Offline A (Gold-only, no timer) as Phase-1**; Gold+time = Phase 2. §7 P1 confirmed closed. Block E test matrix accepted.

**Claude batch #5 (2026-08-23):** Block E API split verified 6/6 · bible AI/whale synced · Offline A doc drift listed · weekly faucet = progression blocker.

**Claude batch #6 (2026-08-23):** Finale milestone pre-review — replay-farm risk; hoard-full silent lose; MaxCastleLevel still dual at audit time.

**CC:** Hoard-full milestone = **silent 0** (no queue). Replay safety = shared `claimedStageRewardIds` gate.

**Claude batch #8 (2026-08-23):** MaxCastleLevel confirmed · silent-hoard tests match lock · weekly earn = only substantial Soft-block.

**CC 7-day:** Weekly Permit **Option C** (manual `ManualTrustedWeekKey` stopgap). Option A server = later target. Keep 8/wk+16 until C proven.

**WH Block G (2026-08-23):** **30/30** — finale at-full-hoard + cap-collision + Barracks/Gate at-cap tests. Barracks/Gate Soft-block **closed**.

**WH Block H (2026-08-23):** **30/30** — `ManualTrustedWeekKey = "cc-permit-week-2026-08-23"`; Home auto-claim + button; `CollectionWeeklyPermitClaimTests` 7/7.

**Claude batch #9 (2026-08-23):** Option C mechanics **ship** as time-boxed stopgap. Live risk = **forgotten week-key rotation** (CC owns bump + log). Dev-placeholder reuse + milestone collision **closed**. Block G locks confirmed; Barracks/Gate Soft-block confirmed closed. **Must-have gap:** Home UI trigger path → **WH Block I**.

**WH Block I (2026-08-23):** **19/19** — `HomeWeeklyPermitClaimTests` 4/4 via `BuildHomePageUIForTests` (strip/button, auto-claim, rebuild idempotent, button no double-grant). **Weekly Soft-block fully closed** (Option C stopgap). Claude #10 = optional verify.

**Claude Soft-block parallel (2026-08-23):** Dust balance never shown; Evolve label uses pre-discount Gold only; scarcity copy missing. Ranked Dust display + due-Gold preview as progression info gaps (not yield bugs).

**ChatGPT packets (2026-08-23):** `FORGE_DUST_PLAYER_COPY_v1` — **CC approve** copy strings (no number rescale). `PERMIT_WEEK_KEY_SERVER_STUB_v1` — **CC lock later Option A** (ISO `YYYY-Www` server key); **not this week**; keep 8/wk + 16 hoard stopgap.

**CC → WH Block J:** Collection UI honesty (show matching Dust, `ComputeGoldDue` on Evolve label, wire approved copy). Yields/caps/Save untouched.

**WH Block J (2026-08-23):** **22/22** — Dust on detail wallet, Evolve label uses `ComputeGoldDue`, copy wired via `CollectionPlayerCopy`. Yields/caps/Save unchanged. **Collection UI honesty Soft-block closed.**

**Claude Soft-feel + #10 (2026-08-23):** Weekly Soft-block fully closed (Home UI tests). AI-spell + pack-open feel scripts locked for owner.

**ChatGPT `HUMAN_PLAYTEST_SCRIPTS_v1` (2026-08-23):** **CC approved** as human-feel gate (cold start / AI spells / shop→burn→evolve).

**Owner preference (2026-08-23):** Zihan will **not** run human playtests until MVP is playable **with tutorial**. Human scripts **deferred**; agents must self-check Tutorial→Home→Campaign spine first.

**Claude J spot-check (2026-08-23):** **Y** — matching-rarity Dust, `ComputeGoldDue` Evolve label, copy wired; no yield/cap/Save drift. UI honesty Soft-block **closed**.

**CC next:** WH Block K (MVP+tutorial playable spine EditMode) · Claude MVP+tutorial gap audit · ChatGPT `MVP_PLAYABLE_GATE_v1` checklist.

**ChatGPT `MVP_PLAYABLE_GATE_v1` (2026-08-23):** **CC approved** as minimum greenlight before Zihan’s first human session. Non-goals locked (polish/Bazaar/server week key/economy rescale).

**Claude MVP+tutorial audit (2026-08-23):** Path is **Tutorial → Home → Deck Builder detour → Campaign 1-1** (intentional; no Hard-block). Segments tested in isolation; **missing one chained EditMode spine**. Soft confidence gap only.

**CC → WH Block K (refined):** Implement Claude must-have #1 — chained fresh-profile Tutorial → Home → confirm no deck → save deck → launch/win 1-1 → unlock 1-2. Do not remove Deck Builder gate.

**Claude Soft-gap parallel (2026-08-23):** Empire Offline A start→claim OK. **Castle Soft-block:** row shows level only, no Resource/HP payoff after Gold spend → **WH Block L after K**. Upgrade buttons reactive-only during busy project → include in L if cheap. Home/Shop/Collection nav OK. AI-spell feel parked (cosmetic).

**ChatGPT `MVP_FIRST_SESSION_SCRIPT_v1` (2026-08-23):** **CC approved** — 10–12 min post-green human checklist (Tutorial→Deck→1-1→optional Shop→Collection). Skip list locked. Run only after Block K spine green + Claude verify.

**WH Block K (2026-08-23):** **1/1** `MvpOnboardingSpineTests` green. **Production fix:** Return to City clears `IsTutorialMatch` + `_tutorialStep` (was blocking post-tutorial Campaign/To Battle). Spine = Tutorial→Home→Deck confirm→1-1 win→unlock 1-2.

**Claude Soft-gap #2 (2026-08-23):** Shop reveal shows cards but no Collection next-step → Soft → Block L. Campaign/stamina blocked messages **OK**. No new Hard-blocks.

**CC → WH Block L:** Castle Resource/HP payoff UI + Shop overlay Collection nudge (+ optional Empire busy-button disable).

**Claude Block K verify (2026-08-23):** Spine **Y** agent-proven (Return-to-City clear asserted). Related **54/58** — 4 fails flagged **likely NEW from K** (1-2/1-3 AF wins, Winning1_1 unlock, TutorialRoot null). **CC HOLD** human playtest / gate until fixed. Soft-gap #2 Shop nudge still open for L.

**CC → WH Block K.1 (before L):** Restore the 4 related greens without removing Return-to-City tutorial-session clear. Keep `MvpOnboardingSpineTests` green.

**Claude Soft (2026-08-23):** Fail #4 = **HomeV3 rename** (`TutorialRoot` → `HomeFeatureRoot`; `StartTutorialButtonRoot` still wired). Soft: restore tutorial-invite copy on that panel.

**WH Block K.1 (2026-08-23):** Related filter **58/58**. Root causes: AF fails = Option B AI spells vs spells-off taught-path contract (tests spells-off; AI-on smoke separate); TutorialRoot → HomeFeatureRoot test update. Return-to-City clear kept. Home invite Soft **not** restored yet → Block L.

**CC → WH Block L:** Castle Resource/HP payoff + Shop Collection nudge + Home tutorial-invite FeatureCopy.

**Claude K.1 verify (2026-08-23):** **Y** first-session gate OPEN (Tutorial→Deck→win 1-1 under real AI spells). AF spells-off taught-path pattern accepted. Gap: 1-2/1-3 AI-spells-ON winnability/smoke unverified → **WH Block M** (non-blocking for first session). HomeFeatureRoot match confirmed; FeatureCopy Soft still for L.

**WH Block L (2026-08-23):** **8/8** — Castle `+Resource · +HP` row; pack overlay Collection next-step; Home FeatureCopy tutorial invite in tree.

**Claude Block L verify (2026-08-23):** All three Softs **Y closed**. First-session script **not blocked**. 1-2/1-3 AI-on gap remains non-blocking → Block M.

**CC → WH Block M:** Campaign 1-2/1-3 AI-spells-ON resolve smoke (extend 1-1 pattern). Non-blocking for first session.

**WH Block M (2026-08-23):** **8/8** — `CampaignAfMirroredAiSpellSmokeTests` 3/3 (1-1/1-2/1-3 resolve + spells ON); Chapter1 AF taught-path 4/4 untouched; spine 1/1. Victory not asserted (unstable AF+AI-on).

**Claude Block M verify (2026-08-23):** **Y** gap closed · taught-path not weakened · **MVP Soft queue for first session is clear.**

**CC next:** WH Block N (Deck Builder first-open Soft) · Claude Soft stretch past 1-1 · ChatGPT gate evidence + week-key rotation ops checklist. Owner may run first-session script anytime.

**ChatGPT (2026-08-23):** `MVP_PLAYABLE_GATE_EVIDENCE_v1` + `WEEKLY_PERMIT_KEY_ROTATION_CHECKLIST_v1` — **CC approved**. Rotation: change only `ManualTrustedWeekKey`; keep 8/wk + 16 hoard; log in OWNER_REVIEW_LOG.

**Claude Soft stretch (2026-08-23):** Post-1-1 unlock CTA **OK**. Deck Soft mostly already present → Block N locked it. Empire busy-button Soft **stale** (L already disables). Remaining Soft: AI-on IsVictory for 1-2/1-3 → Block O; AI feel parked.

**WH Block N (2026-08-23):** **4/4** — `DeckBuilderFirstOpenSoftTests` 2/2 + `MvpPlayableGateEvidenceTests` 1/1 + spine 1/1. `ConfirmedDeckRequiredGuidance` wired.

**CC → WH Block O:** 1-2/1-3 AI-spells-ON IsVictory confidence (stable seed or documented resolve-only). Non-blocking for first session.

**Claude Block N verify (2026-08-23):** Deck Soft **closed** · Empire busy-button Soft confirmed fixed · Block O **approved** non-blocking · first-session blockers **none**.

**WH Block O (2026-08-23):** **12/12**. 1-3 AF+AI-on Victory at seed **11** (4/40). **1-2 AF+AI-on = 0/40 wins** (resolve-only + rate log). Taught-path spells-off untouched. First-session still clear (1-1).

**Claude Soft stretch #2 (2026-08-23):** Battle screen has **no Normal vs Campaign mode label** → Block P. Stamina cues OK. Top Softs: mode label · 1-2 AI-on win confidence · AI feel.

**ChatGPT `MVP_POST_FIRST_SESSION_SOFTS_v1` (2026-08-23):** **CC approved** — with amendment: after O evidence, treat **1-2 0/40 AF+AI-on** as next Soft triage before feel work.

**CC → WH Block P:** Battle HUD mode label (Campaign · Stage X vs Normal Battle). No combat retune until Claude severity on 1-2 0/40.

**Claude Block O verify (2026-08-23):** Evidence **Y**. Severity: Soft but **investigate 1-2 0/40 now** (ahead of cosmetic P); not first-session Hard. Mode label Soft still open → P after Q. First-session still clear.

**CC → WH Block Q (before P):** Root-cause 1-2 AF+AI-on 0/40 — bug fix if real; if balance-only, document cause, no retune this block.

**Claude Soft parallel (2026-08-23):** 1-2 unique facts = all-warrior + identical 3/2×3. Locked post-Q evidence bar (bug-fixed vs balance Soft). If pattern confirmed → **Block R** same-class/uniform roster scan across 273 stages **before** cosmetic Block P.

**Claude Block Q verify (2026-08-23):** **Q never landed** at WH — nothing to accept. First-session still clear.

**ChatGPT `STAGE_1_2_AF_AI_ON_CAUSE_FRAME_v1` (2026-08-23):** **CC approved** as evidence-first bug-vs-balance Soft tree. First-session / economy holds locked.

**ChatGPT Drive re-drop (2026-08-23 later):** Same cause-frame packet confirmed on Drive WIP. **CC: PARKED — do not assign.** Golden rule: no Ch1-2 Soft coding until Zihan explicitly reopens. Seats stay on Gate EditMode / HomeV3 / Castle sim.

**CC pivot (2026-08-23):** Owner call — Ch1-2 Soft spiral is **not** wartime game-dev priority. **Park Q/R/P.** Next WH = **Block S** Campaign Ch2+ same 3-node MVP window as Ch1. First-session remains clear for human when ready.

**Owner correction (2026-08-23):** Golden rule #1 = **get the game up ASAP.** CC must not assign Soft-confidence / Ch1-2 AF rabbit holes. WH = Block S. Claude = ship Soft only (map/continue-game blockers).

**Claude broader Soft (2026-08-23):** Ch2+ MVP 3-node window **already live** (`GetMvpWindowStages` + tests) — **cancel Block S**. Next ship slice: wire Ch3–9 real titles into `GetChapterTitle` (**Block T**).

**Claude ship Soft (2026-08-23):** Reconfirms **cancel S** (do not touch window). Titles Ch3–9 = Block T. Next after T: EditMode for Ch2→Ch3+ display-chapter advance (**Block U**). Stamina regen = parked.

**Claude ship Soft titles (2026-08-23):** Locked Ch3–9 `GetChapterTitle` strings from naming kit. Ch1 keep `THE ORC INVASION`. **Cancel Block U** — Ch3 MVP window tests already exist. Next after T: Collection browse Soft audit (then WH only if gaps).

**Block T (2026-08-23):** **Accepted.** `GetChapterTitle` Ch3–9 wired + `CampaignChapterTitleTests`. Ch1/2/10 unchanged. Next: Claude Collection browse Soft → WH Block V only if ship-blocker gaps.

**Claude Collection Soft (2026-08-23):** **CLEAR** — search/detail/empty OK; Filter/Sort honest Coming Soon. Recommended Block P mode label — **rejected as Soft assign**: Block P helpers already live in `GameBootstrap` (`NormalBattleModeLabel`, `FormatCampaignModeLabel`, `WithBattleModePrefix`). Next WH = **Block V** Collection class Filter (polish, not Soft).

**Claude Soft (2026-08-23 later):** **CLOSE P** confirmed. Broader Soft **CLEAR** (StoryOverlay OK). Recommended Gate reader per constitution build order.

**Owner process call (2026-08-23):** Stop Claude Soft-review treadmill — Soft queue clear; Claude **codes**. Next = **Block W Gate chapter reader** (wire existing `IsCampaignChapterAllowedByGate` into Campaign launch; no Save shape / no Gate table retune without escalate).

**Owner enforcement (2026-08-23):** Golden rule alwaysApply rule — Soft waste / Ch1-2 Soft banned; reviews OK for done work. **Block V accepted.** Parallel coding: Claude **W** Gate launch (plan accepted), WH **X** Collection Sort, ChatGPT Castle sim design packet. No idle WH.

**Block X (2026-08-23):** **Accepted** — Collection Sort Default/Name/Rarity + `CollectionSortTests`. **Block W** code landed (Gate launch); EditMode awaits Unity-closed confirm. Next WH = **Block Y** HomeV3 chrome on Collection + Deck Builder.

**Block Y (2026-08-23):** **Accepted** — HomeV3 header/nav on Collection + Deck + `CollectionDeckHomeV3ChromeTests`.

**Block W (2026-08-23):** Focused Gate suites **28/28**, but **not accepted** — FullDepth/Ch2+ launch profiles still default `gateLevel=1` and call `LaunchCampaignStageForTests` → likely mass `BlockedByGate`. Next Claude = **W.1** harness fix. WH = **Z** Empire Gate row clarity (parallel).

**WH Empire Gate row / Block Z (2026-08-23):** **Accepted** — `FormatGateRowSummary` + EmpireConstructionHomeTests.

**ChatGPT Castle brief (2026-08-23):** Drive `CASTLE_PHASE1_IMPLEMENTER_BRIEF_BLOCK_Z_v1` — **CC accepts content as Block AA** (Z name clash with Empire Gate row). Repo: `docs/CASTLE_PHASE1_IMPLEMENTER_BRIEF_BLOCK_AA_v1.md`. Coding starts only after W.1 EditMode green. Ch1-2 cause-frame stays parked.

**W.1 code (2026-08-23):** Landed in tree (`MinimumGateLevelForChapter`, `GateTestSupport`, Ch2–10 FullDepth). **Await EditMode counts** before accepting W / opening AA.

**Block W + W.1 (2026-08-23):** **Accepted** — Gate suite **9/9**. FullDepth ~20 AF+AI-on combat failures flagged as **pre-existing / out of Gate scope** — parked, no retune in W.1. Castle AA brief locked but **not opened**; seats idle until next CC assign.

**Owner seat change (2026-08-23):** Cursor unfit as CC (soft assigns, idle seats, golden-rule breaks). **Claude = Command Centre. Cursor = Working Hands only.** Handover: `docs/CC_HANDOVER_TO_CLAUDE_2026-08-23.md`. Castle AA assign = Claude’s first CC act.

---

| Gate | CC |
|---|---|
| Replace Shop 6-fusion / 504 Permits / 63-week claim with shipped **3 steps / 168 Permits / 21 weeks @ 8/wk** | **Yes** |
| Keep shipped 3-step evolution; do not add steps to rescue old sim | **Yes** |
| Future weekly Permit issuance **4/week** (not 8) when weekly earn ships — restores ~9.7 mo full-collection floor | **Yes** — design lock; **do not** change `AscensionPermitsPerTrustedWeek` in code until weekly earn + hoard-cap review |
| If 8/week kept: amend Shop “month 15” claim | **N/A** — superseded by 4/week lock |

Shop inverse pack prices remain valid. Shop “cannot finish before month 15” whale proof is **obsolete** — **CC patched Drive `SHOP_V2_NUMBERS_PACKET.md` §4 on 2026-08-23** (ChatGPT Drive-lock bypass).

---

## Pack receipt — **LOCKED (Owner + Claude 2026-08-22)**

Source: `COLLECTION_PACK_RECEIPT_v1.md` — Claude review patched same day.

| Gate | Owner | Claude | Patch |
|---:|---|---|---|
| 1 atomic TryOpenPack + rollback | **Y** | **Y** | Full `cardProgression` snapshot Phase A |
| 2 floor re-roll rule | **Y** | **Y** | Normal-slot only; §4 explicit |
| 3 pity precedence + per-draw | **Y** | **Y** | Sequential + Warband/Legion tests |
| 4 §10 tests before pack UI | **Y** | **Y** | |

**Code:** `CollectionPackReceiptService` + pack-open Shop overlay + §10 EditMode green (2026-08-22).

**CC accept — ChatGPT `MOS_OPEN_ITEMS_RECONCILIATION_2026-08-23.md` (2026-08-23):** All four MOS
§20 open items resolved — Event Medals/Market Credits naming, Evolution sole mechanic (Limit Break
retired as label), 1–12 stat scale retained, no initiative. Docs-only: `docs/Economy_Blueprint.md`
"Event Tokens" → "Event Medals", `docs/MOS_v1.1.md` §20 struck through, `CLAUDE.md` open-items note
updated. No code/economy/Save change. ChatGPT idle pending next theory ask.

**CC accept — Bazaar Phase-1 fully locked (2026-08-23):** ChatGPT delivered all four packets
(catalogue/ledger/non-goals, genesis mechanism, genesis numbers). All reviewed and locked — see
`docs/BAZAAR_PHASE1_CC_ACCEPT_2026-08-23.md`. Design is done; implementation stays blocked on the
trusted-server/backend dependency, which has no assigned owner. Cursor's Block AC continues to
exclude Bazaar code.

**CC accept — Recurring Events/Mini-Game brainstorm revision (2026-08-23):** Solo-vs-guild reward
identity split locked as design direction (solo = Empire/Avatar-side only, never Forge/Dust/Permits/
Evolution material; Guild Expedition = sole new deck-material source, inside existing rate caps).
Chronicle/Oracle/Wyrm Draft ship as rewardless practice only until an authoritative claim ledger
exists - no client-authoritative reward exception for any mode. Multi-resource Empire construction:
no existing resource is safe as a second cost; Gold-only instant construction stays. A dedicated
Castle/Barracks material remains a future option requiring separate new-resource approval. Still
discussion-only overall - no implementation assigned.

**CC accept — Guild Expedition + Competition reconciliation, corrected (2026-08-23):** Permit rate
fixed to 4/week (was incorrectly assumed 8/week) - matches the already-locked ~9.7-month full-
collection timeline exactly (168 / 4 = 42 weeks). Two 7-day Expeditions per 14-day season each grant
up to 4 Permits, together consuming the full weekly 4-Permit budget (replace, not stack, per the
existing rule). Gates 1-5 all confirmed. Still READY FOR IMPLEMENTATION: No - blocked on trusted
guild identity/time/result/score/claim service, same as before.

**CC accept — Shop V2 vs Campaign Gem reconciliation (2026-08-23):** Confirmed real conflict
(Ch1-10 grants ~772,551 Gems vs Shop's 250-400/month F2P planning assumption). Decision: do NOT
reprice packs. Campaign Ch1-10 Gems are a one-time progression grant, not the ongoing economy - the
per-stage Gem reward formulas need recompute against the intended post-campaign monthly rate, same
treatment as POST_CH10_GOLD_RECOMPUTE_2026-08-23.md did for Gold. Inverse bulk-discount pricing
structure stays valid as-is. Next: ChatGPT recompute task issued.

**CC accept — Campaign Gem recompute, milestone-weighted (2026-08-23):** Ch1-10 total corrected
from 772,551 to 6,504 (263 regular stages x 8 Gems + 10 chapter finales x 440 Gems), ~21.7 months
at the 300 Gems/month F2P target. Rejected the first pass (2,914, flat trickle, felt thin for a
273-stage campaign) in favor of milestone weighting. Shop V2 pack pricing/inverse-discount stays
unchanged - this fixes the source, not the prices. Not yet implemented - needs assignment to a
coding seat (CampaignMapPresenter.cs stage reward formulas).

**CC accept — Raid Troop training-time economics locked (2026-08-23):** Barracks-scaled storage
(10->30) and training rate (30min->5min), 5-troop attack cost, asymmetric win/lose (net -1 vs -5),
no purchase/refill path. Design is sound. Still blocked on two real prerequisites, unchanged: (1) a
formal Guild_Competition_Rewards_v1.md §6 amendment - that's an MOS v1.2 owner-approved doc, needs
Zihan's explicit sign-off, not just CC's; (2) trusted-server authority, now the 4th system waiting
on it alongside Bazaar/Echo Arena/Guild Expedition. No raid implementation assigned to any seat.

**WH audit (2026-08-23, findings only, no code):** Empire/HomeV3 UI structurally ready for a second
resource pill + timer status copy - add-a-pill, not a redesign. Reference when Empire construction
v2 gets built.

**Block AB accepted; Block AA opens (2026-08-23):** Real baseline via batched per-class runs (zero
stalls across 98 classes, confirms the stall only surfaces in one continuous long process, not a
code bug) - 703/761, 28-cluster CardDatabase.cs issue confirmed fully closed. Castle Phase-1 (Block
AA) accepted - its own tests aren't among the 58 failures and its blocker (CardDatabase reliability)
is resolved. Remaining 58: 32 parked AF/AI-on combat-balance (known category), 12 Permit tests
asserting stale pre-4/8-correction numbers (assigned to coding room to update), 12 confirmed
pre-existing (cross-referenced against this morning's baseline, predate today's work), 2 genuinely
new/unknown (EmpireConstructionHomeTests routed to WH, CampaignMatchContextLifecycleTests parked).

**Block AB fully closed (2026-08-23): real final total 715/761.** Coding room updated the 12 stale
Permit-test assertions to the locked 4/8 numbers (careful recompute through real clamp logic, not
blind arithmetic), plus fixed 4 brittle exact-string-match tests unrelated to the number. Remaining
46 = 32 parked AF/AI-on (known) + 12 pre-existing (predate today) + 2 new/unknown (routed).
Correction to the entry above: Campaign Gem recompute implementation belongs to WH
(CampaignMapPresenter.cs is metagame-owned per CLAUDE.md), not the coding seat.

**MVP spine re-verified against real state (2026-08-23): 9/11.** No new regressions - the 2
DeckBuilderReleaseGateTests failures are the same pre-existing geometry gaps already logged in
Block AB's triage. Onboarding spine (Tutorial->Home->Deck Builder->Campaign 1-1) holds end-to-end
after today's Avatar/Empire/Shop/Permit/Stamina work. Updated MVP_PLAYABLE_GATE_v1.md for real doc
drift: added open checklist lines for the Avatar/Empire screen and Permit/Stamina UI (both real,
shipped, post-date the doc), and flagged that the Shop evidence line exercises the now-retired
pack_novice SKU, not a live Shop V2 pack. Lead for the (c)-bucket unknowns: AcquiredCardToCombat
ContractTests and ReleaseProfilePersistenceContractTests both reference pack_novice directly - the
retirement is a plausible common cause, routed to WH to check.

**Balance health check (2026-08-23): 9/9 BalanceSimulationTests clean.** No regression from today's
Permit/Shop/Stamina work, as expected - confirms those changes stayed isolated from combat math.

**CloudCode track milestone (2026-08-23): 4 modules authored, locally validated, none deployed.**
SocialSafety (43/43, pre-existing), PermitWeekKey (33/33), GuildExpedition (38/38, scoped to core
attempt/score/claim), Bazaar (33/33, scoped to core list/buy/sell). Real gaps flagged not guessed
around: Bazaar's cross-account listing-visibility storage shape (resolved below), Gold listing-fee
enforcement (Gold lives in frozen PlayerProfile only, unreachable from any server ledger - real
launch blocker, separate from storage), genesis-liquidity auction deferred as its own module.
CC researched and answered the storage gap: Unity Cloud Save's "Game Data"/Custom Items scope
(distinct from Player Data) supports unlimited independent records with an Access Class of Default
(any-player-readable, server-only-writeable) - exactly matches "browsable board, Cloud-Code-only
mutation." Source: docs.unity.com/en-us/cloud-save/concepts/game-data. Routed back to coding room
to implement the real store.

All 4 modules still blocked on the same thing: live deployment needs the
SocialSafety_NonProduction_Validation_Checklist.md walkthrough (Dashboard + 2 disposable test
accounts) - in progress with Zihan, paused mid-checklist.

**CC accept — Avatar victory progression, no farming cap (2026-08-23):** Confirmed by design, not
an oversight - Avatar level is monotonic personal progression touching only Resource/HP tiers, never
Gold/Gems/Permits/cards/Materials. No tradable/scarce-economy shortcut exists to cap.

**CC partial accept — Cosmetics/skins portfolio (2026-08-23):** Individually-priced items locked
(card frame 150 / Avatar skin 500 / arena theme 1,000 / victory VFX 300 / deployment VFX 450 Gems),
all zero gameplay benefit, no forbidden-edge touches. Held: the two bundle SKUs (1,650 / 4,000) -
"preserves inverse bulk-discount philosophy" was asserted without the computed math every other
pricing table in this project shows, and the 4,000 bundle has no stated contents. Not implementation-
blocking (no seat is building Shop cosmetics yet) - just not fully locked until the bundle math lands.

**Consolidated re-check (2026-08-23): 717/761**, up from 715/761 - net improvement, no regressions.
Test-by-test diff (not just count) confirms all 44 remaining failures trace to the known parked
AF/AI-on category (30) or the already-triaged unknown bucket (13) or one test
(ChapterOnePostVictoryStoryTests) that depends on the same Stage 1-2 win-variance already tracked
in the parked category, not an independent new issue.

**CC accept — Purchase package portfolio v2, all sections (2026-08-23):** Rotating/time-limited
cosmetic offers, $4.99/30-day "Empire Blessing" subscription (750 Gems total), $4.99/30-day Chronicle
Pass (10-tier free/paid ladder, Empire/Avatar-side free track + Gems/cosmetics paid track),
cosmetic bundle math redone to 31-33% discount with a BEST VALUE tag. Construction Materials stay
non-purchasable in Phase 1 - reasoned decision (contradicts the resource's earn-through-play design
identity), not a reflexive DAG rejection. This resolves the real genre-pattern gaps found in v1
(missing urgency mechanics, missing subscription, weak bundle discounts). Design-locked, not
implemented - all Phase-2/post-MVP, no coding seat assigned.

**CC accept — Chapter 1 narrative continuation, 1-4 through 1-12 (2026-08-23):** Real escalating
arc (Thaleia/Rusk/Ione, the Olympus-conflict hook from 1-3's unused "Olympus will notice this
wound" line), ends on a real Chapter 2 hook. Tutorial confirmed correctly mechanical-only, one
framing line added. Not yet implemented - WH's job (StoryDatabase.cs is Story-owned/metagame).

**Locked production rule: animation scoping.** Every stage keeps cheap text-only StorySequence
dialogue (no art cost). Animated Cinematics reserved for ~3 milestones per chapter (open/mid-turn/
clear), not per-stage - avoids overkill across a 273-stage campaign. Same gap (thin depth-fill
narrative, 2-4..N) confirmed present in every chapter through Ch10, same pattern as Ch1.

**Stage 1-2/1-3 combat-balance finding (2026-08-23), classified per cause-frame doc:** Confirmed
with fresh 25-seed evidence - Auto Formation's deliberate one-tap onboarding policy (3 cards, one
per lane) loses Stage 1-2 25/25 and Stage 1-3 23/25, always via tick-cap not fast KO. Same stages
fall in 4/11 ticks against a full manual-style formation - live route faithful, deterministic,
rule-correct, not a bug. Per the doc's own prescribed remedy for this exact shape: hold combat
numbers unchanged, defer to the owner's own first-session playtest
(MVP_FIRST_SESSION_SCRIPT_v1.md) to determine if this is a real confusion point before any retune.
Not assigned as a code fix.

**CC accept — Chapter 2 narrative continuation + reusable chapter template (2026-08-23):** Full
2-4..2-21 arc (invasion/resistance/ash-road/Thaleia's-fracture/archive/climax beats), same cast
carried forward, ends on a real Ch3 hook ("the gates of Olympus"). Reusable template locked: 2-3
recurring cast (carry >=2 forward, add <=1 new major per chapter), 6 story beats grouping stages
(3 each for 18-stage chapters, 5 each for 30-stage), one choice-prompt per beat not per stage,
animation milestones at open/mid/clear (N-1, N-10 or N-15, N-last). Not yet implemented - WH's job.

**CC accept — Chapter 3 narrative, "The Gates of Olympus" (2026-08-23):** Full 3-1..3-30 arc, same
cast, template correctly applied (choice prompts at 3-1/6/11/16/21/26, animation milestones
3-1/15/30). Real climax (Eryx defeated, palace saved) with a genuine Chapter 4 hook. Not yet
implemented - queued behind WH's current work (Gem recompute, Ch1/Ch2 narrative implementation).

**Combat-balance investigation, full 10-chapter evidence (2026-08-23) - MAJOR finding, escalated
scope beyond the original Stage 1-2/1-3 ask:** Every DEFEAT across all 10 chapters (1-4, 1-7..1-12,
2-2/2-3/2-4, 3-2, and the first new stage of Ch4-10) hit exactly the 12-tick safety cap - never a
fast KO, uniform shape across 10 independent chapters. Root mechanism confirmed by source read
(GameBootstrap.cs:4722, PerformAutoFormation): the one-tap onboarding policy is a deliberate,
documented 3-card (one per lane) "beginner basic squad path," not a bug. Classification: Balance
Soft, same as the original Stage 1-2 finding, now confirmed structural across the whole campaign -
the weak auto-formation shortcut loses reliably at nearly every chapter transition from Ch1 onward.
Not retuned - holds per the cause-frame doc's own rule, this is bigger than "wait for one first-
session data point" and needs real owner attention on return, not a quiet fix.

**CC read for owner, not yet decided:** this may not be a raw "stages are too hard" balance problem
so much as "the Auto Formation feature doesn't scale with player progression" - it stays a flat
3-card policy for the whole 273-stage campaign even as the player's own deck/Barracks grow. That's a
genuine design option worth considering (scale AF's card count with progression) alongside "just
observe first-session and see," but it changes battle outcomes so it counts as a real retune
decision, not something to greenlight without the owner.

**Evidence gap, flagged not filled:** Chapters 4-10's stage-loop tests abort at the first losing
stage (hard Assert inside the loop), so there's no data on stages X-2 onward in those 7 chapters.
Authorizing the coding room to restructure those loops to collect full-sweep diagnostic data
(soft-assert/collect-all pattern) rather than aborting early - this is data collection, not a
retune, and directly improves what the owner will have to decide from.

**Combat-balance investigation, COMPLETE (2026-08-23) - full-depth data, all 273 stages:**
Chapters 4-10 restructured to collect-all (committed 74331a3) - 210/210 stages now have real data,
not just 7 first-stage samples. Win rates: Ch4 5/30, Ch5 5/30, Ch6 8/30, Ch7 6/30, Ch8 7/30,
Ch9 6/30, Ch10 5/30 - sustained ~17-27%, no monotonic decay toward zero. One tick-count outlier
noted honestly (7-15 won in 6 ticks vs the otherwise near-metronomic 11/12 pattern) - a fast win,
not an anomaly needing investigation. Same Balance Soft classification holds at full depth: this is
a stable, uniform ~1-in-5 pattern against the deliberately-weak onboarding Auto Formation policy
the whole way through the campaign, not an escalating or localized problem. Investigation complete,
not retuned - real decision for the owner on return (see CC's earlier framing: this may be "AF
doesn't scale with player progression" rather than "stages are too hard").

**CC accept — Empire construction v2, Materials + timers (2026-08-23):** Materials 5,000/stage +
50,000/finale = 1,815,000 total, 3,000-31,000 tier ladder = 1,479,000 for 3 buildings to L30, Gold
sink stays additive/unreduced. Timer bands 5-15min (T1-5) through 18-30hr (T26-30), client-clock
only, rollback-detection holds eligibility without punishing, no speed-up purchase. Offline A state
flow (Idle->Building->ReadyToCollect->CompleteClaimed) unchanged. Design-locked, not implemented.

**Real gap found (2026-08-23): spell catalog implementation backlog.** SPELL_CATALOG_v1.md locks 36
spells (14 Phase-1 ship slice, reviewed and accepted weeks ago); AvatarSpell.cs ships only 4
(Firestorm/Mend/War Cry/Divine Bolt - the catalog's "Starter" tier). 10 already-locked, already-
reviewed Phase-1 spells (cinder_lash, ember_wave, fault_line, vital_spark, renewal, rallying_gale,
banner_of_ashes, sun_lance, stone_judgment, tempest_brand) were never implemented. No design work
needed - this is a ready-to-code backlog, queued for the coding seat.

**Cross-document alignment sweep, real misalignment found and fixed (2026-08-23):** Empire
construction v2's client-clock timers directly contradicted EMPIRE_SCHEMA_LOCK_2026-08-22.md §2/§9
("Gold+time construction is Phase 2 only", "timed builds wait for server") - accepted without
flagging it as the amendment it actually was. Owner decided: formally amend, not revert. §2 amended
in place with explicit scope (pacing-only, no speed-up purchase, doesn't reopen §8/§9's server
requirement for monetised builds). Downstream consequence found and corrected: Guild Expedition
reconciliation's "Master Builder has no effect while construction is instant" line is now stale -
corrected in the Drive doc directly, flagged for re-review before that office ships.

Also checked and confirmed NOT a bug: SPELL_CATALOG_v1.md's own flagged "Divine Bolt 100 vs live
code stub 150" discrepancy - checked AvatarSpell.cs directly, live magnitude is genuinely 100,
matches the lock. The catalog doc's warning note is just stale text, not a real bug.

**CC accept, partial — Empire L30 timer bands, 6-9 month target confirmed (2026-08-23):** Owner
confirmed the 6-9 month total completion target independently (was not previously locked anywhere -
GPT's citation of it as an existing intention was actually a coincidental match, now formally
locked here). Arithmetic roughly verified (~6.4-7.2 months depending on band-boundary assumptions,
consistent). Timer bands 5-15min through 7-10 days, single builder slot, 275,000 Materials seasonal
contract cap (18.6% of total - matches the requested 15-20% catch-up scale) all accepted.

**Held, not yet locked:** structural pacing risk - MOD's 3-building serial system compared against
Clash of Clans' dozens-of-simultaneous-structures base is an hours-match, not a feel-match; the
top tier band (7-10 days x up to 3 buildings back-to-back) risks a 21-30+ consecutive-day endgame
wait with nothing else happening, right before the completion reward. Sent back to ChatGPT to
address before final lock.

**Consolidated re-check (2026-08-23): 718/762** (was 718/761 - one new test, net pass-count neutral,
+1 fail). Test-by-test diff confirms no regression in the previously-passing set. The 30 AF/AI-on
items are unchanged/expected variance (unseeded formation policy). **New structural insight: the
Balance Soft pattern's blast radius is wider than the 32 originally catalogued tests** - 3 more
failures today (ChapterOneProgressionTests, HomePageTutorialRewardGuardTests,
CampaignMatchContextLifecycleTests) are reward/persistence tests that happen to assume an AF win
without controlling variance, not independent bugs - same root cause surfacing in a different place.
The true population of vulnerable tests may keep shifting run-to-run until the underlying balance
question is resolved - worth knowing for whenever that decision happens.

One separate item: BalanceSimulationTests.Balance_ExposedAvatarSiege_MeasuredAgainstDisabled missed
its threshold by a hair (-0.055 vs required >=-0.05) on an unseeded Monte-Carlo test, no code
changes to that system today - likely sampling noise, not confirmed without a repeat run.

**Siege near-miss confirmed noise (2026-08-23):** BalanceSimulationTests re-run alone, 9/9 clean.
Closed - no real regression, unseeded Monte-Carlo variance as suspected.

**Spell backlog independently verified (2026-08-23):** grepped catalog + live code directly, all
14 Phase-1 spells confirmed matching exactly (cost/cooldown/magnitude), not just trusting the report.

**CC accept, FINAL — Empire construction v2, consolidated (2026-08-23):** Two construction slots
(reversal from single-builder, now properly justified via a Castle-gates-Barracks/Gate interlock
table, not just restated). Materials 1,815,000 campaign supply / 1,479,000 L30 sink / 336,000
buffer. Timer curve corrected to genuinely monotonic, top band 7-14 days (genre-consistent).
Contracts 275,000/18.6% seasonal cap, math verified correct. 6.7-month current target, 11.1-month
projected once Academy/Embassy add their own scope (now a real computed number, not a vague risk).
Offline A flow, rollback handling, raid protection all unchanged from earlier locks. This
supersedes every prior Empire v2 timer/builder proposal - no further iteration on this specific
packet unless something genuinely new surfaces.

**Empire construction REOPENED (2026-08-23): all 5 buildings from Phase-1 start, not 3+2-later.**
Owner explicitly overrides EMPIRE_SCHEMA_LOCK_2026-08-22.md §1 ("Ship Castle/Barracks/Gate only;
Academy/Embassy later"). Real finding before dispatching design work: Academy/Embassy's documented
function (MOS_v1.2.md §11) is tied to guild timer-help - the same trusted-server dependency
blocking Bazaar/Guild Expedition/Raid. Owner decision: ship solo-only stripped Phase-1 stand-ins
now (Academy = personal research + Materials sink, no guild-help; Embassy = near-placeholder given
it has no real solo function per its own design), full guild-integrated versions later once the
server exists. Design task issued to ChatGPT. Empire v2's "FINAL" status from the prior entry is
superseded - 5-building interlock/timeline needs recompute.

**Real source found for Empire building roster (2026-08-23):** tools/mechanics_v2_extract.txt (the
original Mechanics v2 docx, already extracted) has a full 10-building original design: Castle,
Barrack, Storage, Training Grounds, Barn, Gold Mines, Gate, Laboratory (unspecified research -
became "Academy"), Tree of Knowledge, and Prison (captive/sacrifice system tied to raiding). Castle/
Barracks/Gate already have "v2 revision" notes mapping them to the current TCG model. Prison
requires the same trusted-server/raid infrastructure as Bazaar/Raid Troops - not standalone.
Superseding the earlier "propose from scratch" ChatGPT task with this real source material.

**Real gap found (2026-08-23): no spell equip/loadout system exists in production.** BattleController
hardcodes CreateDefaultSpellbook() (starter 4) unconditionally for both player and AI, every match.
CreatePhase1Catalog() (today's 14-spell catalog) is called nowhere in production, only from its own
test file. No branch/config/save-field for spell selection exists anywhere (checked
DeckBuilderPresenter.cs, PlayerProfile.cs - zero matches). "Max 1 AvatarStrike" correctly not
implemented - there's no equip action anywhere for it to intercept yet. This means today's spell
work is real and tested but currently inert to players - a loadout/unlock system is the real next
piece needed to make it count. Flagged as "when this system exists, remember this rule," not built
speculatively.
