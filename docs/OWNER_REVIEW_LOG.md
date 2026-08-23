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
