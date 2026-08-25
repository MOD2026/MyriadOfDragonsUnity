# Locked Decisions Register

**Purpose:** a single, fast cross-check table so a new decision never gets accepted without checking
it against everything else that already touches the same resource/system. Before locking anything
new, CC checks this file first — not just the one prior doc the new packet happens to cite. Before
accepting any packet that changes a row here, re-read the "Collides with" column, not just the
packet itself.

**Maintenance rule:** every time CC locks something in `OWNER_REVIEW_LOG.md` that changes a number
or a rule below, update this file in the same commit. This file is a summary — `OWNER_REVIEW_LOG.md`
is still the full history/reasoning; this is the fast-lookup layer that was missing.

---

## STANDING ORDERS (check before ANY dispatch, every turn - constraints, not tasks)

Current operating constraints from the owner. A dispatch that violates a row here is wrong even if
the task itself is real. Every owner instruction that constrains future action gets a row here IN
THE SAME TURN it's given - never only recorded as prose in a log entry. Added 2026-08-25 after
"stop at chapter 18" was logged as narrative deep in a ship entry, then violated one turn later
because nothing at turn-start surfaced it.

| Since | Constraint | Lifted when |
|---|---|---|
| 2026-08-26 | Chapter production HELD at 18 - **owner explicitly confirmed, not just unanswered**: too much UI is still broken (borders/boxes not matching mockups) to justify more content before more polish. Do NOT re-ask this as if undecided. | Owner explicitly lifts it |
| 2026-08-26 | **UI-fixing is the current top priority across all rooms** - owner flagged real frustration at slow visible progress on border/box/mockup-mismatch bugs. CC should proactively hunt for this bug class (reachable screen + real approved art + Load() never called) via read-only diagnosis and batch-dispatch findings, not wait for one-off reports. | Owner signals priority has shifted |
| 2026-08-25 | WH batch size ~50% up from single-atom tasks; owner is LIVE (15-30 min deliverable band) | Owner signals stepping away (then batch freely) |
| 2026-08-25 | Frozen-file edits (PlayerProfile.cs etc.) need a vetted, locked field list BEFORE the edit - per-case, never blanket | Standing rule, does not lift |
| 2026-08-25 | Empire Defense: design-only, behind evidence gate - no build/art/story dispatch | Memory Expedition live + gate criteria met (10+ wks) |
| 2026-08-25 | Windstep ablation conclusions pre-0fdd193 are VOID (two stacked confounds: enemyTier spellbook bug, then gate-probability bug) - only 0fdd193's numbers are real | Permanent |
| 2026-08-26 | **CC does NOT write/edit code, run Unity EditMode batches, or commit code changes directly.** Owner needs to relay messages to CC and CC occupying the shell with long-running commands blocks that. All coding (fixes, features, verification-by-running-tests) is CR's job scope now - CC diagnoses, packages a clear task, dispatches to CR (idling, underused), and verifies CR's returned commit/diff. CC may still use read-only Bash (grep/git log/git show/git diff) for diagnosis - that is not "coding". Editing docs/register/mailbox files is not code and stays CC's job. | Owner explicitly lifts it |
| 2026-08-26 | **WH (Cursor) has NO direct channel from CC - not `tools/seat_mailbox.md` (that is VS's channel only, has real watchers), not `SendMessage`.** WH can only be reached by the owner manually pasting text CC hands over. Every WH task MUST be given to the owner as a standalone, copy-paste-ready fenced code block in the chat reply itself - never written into the mailbox file, never assumed sent. CC has made this exact channel-routing mistake twice already this session (once caught by the owner, once self-corrected) - re-verify the channel before every WH dispatch, don't default to the mailbox out of habit. | Standing rule, does not lift |

## PENDING DISPATCH (check this first, every turn)

A decision logged below is NOT the same as a decision delivered. This table tracks every dispatch
to a peer seat (VS/CR/WH) or to BS/ST/UI from the moment it's sent until it's confirmed received or
acted on. Added 2026-08-25 after a real dropped handoff: a decision was locked and a message was
sent to CR, the send failed silently (CR's session had vanished), and it wasn't caught until the
user noticed CR sitting idle waiting for it. The register remembered the decision fine - it just
didn't track whether the decision had actually reached anyone.

**Rule: check this table at the start of every turn. A row stays PENDING until the recipient
confirms receipt/action, or the row is removed once confirmed.**

| Sent | To | What | Status |
|---|---|---|---|
| 2026-08-26 | VS (mailbox) | tac_w1_m02 third attempt - BS's cyclops fix, verified real (card data + lane-bonus math) | PENDING - dispatched |
| 2026-08-26 | WH | none - VIP/Friends atlas fix (bee2c1f) confirmed landed, nothing outstanding | — |

---

## Currencies

| Currency | Source | Rate/cap | Purchasable? | Locked in |
|---|---|---:|---|---|
| Gold | Campaign first-clear only | 1,779,550 L1-10 cum. (Empire sink) | No | `POST_CH10_GOLD_RECOMPUTE_2026-08-23.md` |
| Gems | Campaign first-clear + IAP | 6,504 L1-10 cum. (recomputed) | Yes (IAP ladder) | `OWNER_REVIEW_LOG.md` Gem recompute entry |
| Stamina | Shop refill only, no free regen | 30/60/120/240 Gems, max 4 purchases/rolling 24h | Yes | Shop V2 + `OWNER_REVIEW_LOG.md` enforcement fix |
| Ascension Permits | Weekly claim + 10 chapter-finale grants | **4/week, hoard 8** | Never | `OWNER_REVIEW_LOG.md` Permit correction (was 8/16, corrected 2026-08-23) |
| Forge Credit / Dust / Sacrifice Credits | Card burn only | Per-recipe caps (20%/10%/none) | Never | Card Burn packet, `CollectionBurnRules.cs` |
| Event Medals | Approved event participation only | No live source yet | Never | `MOS_OPEN_ITEMS_RECONCILIATION_2026-08-23.md` |
| Market Credits (Bazaar) | Voluntary sale only + one-time Genesis auction | 40,000 gross Genesis ceiling | Never (no Gold/Gems bridge) | `BAZAAR_PHASE1_CC_ACCEPT_2026-08-23.md` |
| Construction Materials | Campaign faucet (own, separate from Gold) | Regular stage / finale rates TBD-locked in Empire v2 | **Under active revision** — see below | `EMPIRE_SCHEMA_LOCK_2026-08-22.md` §2 (amended) |
| Guild Contribution | Donations/helps/Expedition bands | Dormant, no live source | Never | `Guild_Competition_Rewards_v1.md` |
| Raid Troops | Barracks-scaled training queue | 10-30 cap by Barracks tier, 30min-5min/troop | Never | `OWNER_REVIEW_LOG.md` Raid Troop economics |

## Cross-cutting design rules (check ALL of these before locking anything new)

| Rule | Source | What it blocks |
|---|---|---|
| No offensive effects reducing another player's progression | `Guild_Competition_Rewards_v1.md` §6 | Raid Troops needed a formal amendment to coexist with this — **still pending owner sign-off** |
| Monetised/competitive timed builds need server time; client clock is display-only | `EMPIRE_SCHEMA_LOCK_2026-08-22.md` §8 | Any PAID timer-skip, anywhere — this is why Empire v2's timers are pacing-only with no speed-up purchase |
| Solo rewards stay Empire/Avatar-side only; never deck-building materials | Recurring Events brainstorm reconciliation | Chronicle/Oracle/Wyrm Draft/Echo Arena/subscriptions/passes all check against this |
| Guild Expedition consumes the shared weekly Permit ceiling, doesn't add to it | `OWNER_REVIEW_LOG.md` Guild Expedition correction | Any future system granting Permits must share this same 4/week budget |
| No purchase converts to combat/deck power | Implicit across every packet | Cosmetics, subscriptions, passes all had to prove this explicitly |

## Empire construction — STRUCTURE LOCKED 2026-08-23, exact rates still open

**11-building roster, structure locked; costs/rates/faucet size NOT locked.** Two free simultaneous
construction slots (no paid slot). Barracks/Gate/Castle/Academy/Embassy available Phase-1 start,
ungated by Castle level.

| Building | Phase-1 function | Server status |
|---|---|---|
| Castle | Progression spine, capacity milestones | Fully functional |
| Barracks | Recruits new soldiers; deck-slot/Resource-regen/replenishment (milestone levels only: 1/5/10/15/20/25/30) | Fully functional |
| Storage | Gold/Materials capacity; production pauses at cap, never silently deletes | Fully functional |
| Training Grounds | Upgrades existing soldiers/cards via deterministic Collection/Evolution rules | Fully functional |
| Quarry / Materials Yard (name TBD) | Merged Barn+Gold Mine; sole passive Materials producer, never Gold | Fully functional |
| Gate | World-map defence, protected-loot floor | Solo stand-in; raid enforcement needs server |
| Academy/Laboratory | Personal research, codex, recipes | Solo works; guild research later |
| Embassy | Construction-help; scales BOTH charges/day and reduction/help by level; safety cap is a **lifetime cap per project** (not daily), min(30% of timer, approved max hours) | Personal stand-in; guild help needs server |
| Tree of Knowledge | Evolution/XP home — **grandfather rule: existing accounts keep live Evolution access, no migration may invalidate progress**; minigames explicitly OUT of this packet, needs own brief | Fully functional |
| Prison | Captive/sacrifice placeholder, non-destructive | Server-dependent stand-in |
| Guild Hall | Flat, single-level, no upgrade ladder; entry point only | Server-dependent stand-in |

**RATES LOCKED 2026-08-23 — full packet closed.** Names: Quarry, Academy. Materials ladder per
building: 5×1,100 (L1-5) + 5×2,200 (L6-10) + 5×4,400 (L11-15) + 5×7,700 (L16-20) + 5×11,000 (L21-25)
+ 4×12,375 (L26-30) = **181,500/building exact**, ×10 laddered buildings = **1,815,000 = the
existing campaign faucet exactly**, no faucet recompute needed (verified by CC's own recomputation,
not just accepted on assertion). Embassy full 6-band curve, both axes scale: L1-5 = 1 charge/day,
10min/help; L6-10 = 2, 20min; L11-15 = 3, 30min; L16-20 = 4, 45min; L21-25 = 5, 60min; L26-30 = 6,
90min. Lifetime-per-project cap of min(6hr, 30% of timer), whichever is lower.

**RESOLVED 2026-08-24 (was "Open, real" — flagged by coding room 2026-08-23, closed via a real GPT
round rather than guessed):** Barracks now has its own Castle-level interlock, `PlayerEmpireData.
MinimumCastleForBarracksLevel`, lighter/lower than Gate's own curve and keyed to Barracks' existing
purchasable milestones only (1/5/10/15/20/25/30): Barracks 1→Castle 1, 5→3, 10→7, 15→12, 20→18,
25→24, 30→30. Same sparse/breakpoint-only shape as `MinimumCastleForGateLevel` (0/undefined between
milestones) — does not touch Barracks' existing deck-slot/Resource-regen formulas, purely gates
whether the next milestone level is purchasable. Wired into `EmpireConstructionRules.TryStart`'s
Barracks case the same way Gate's own check already worked. Gate's own interlock is unchanged.
**Owner-confirmed: the ~22-month full-roster maxing tail is intentional, not a problem to solve** —
matches genre precedent (CoC/RoK: core spine feels fast, maxing everything is a long tail by
design). The 6-9mo target applies to the 5-building core spine (Castle/Barracks/Gate/Academy/
Embassy) only. Remaining real work: **Tree of Knowledge's "Memory Expedition" minigame** (Empire/
Avatar-side rewards only: Avatar XP, small Gold, Stamina, Event Medals, temp research points; no
cards/Forge/Dust/Permits/Evolution materials/Market Credits; 1/day, fixed daily seed, account-level
completion ledger) needs its own implementation brief before coding — scoped, not started. None of
this blocks MVP — Empire roster work is explicitly outside `MVP_PLAYABLE_GATE_v1.md`'s scope; this
is the next milestone after MVP closes, not concurrent with it unless owner says otherwise.

### Superseded — prior reopened/partial versions (kept for history only)

**Owner override:** all 5 buildings (Castle, Barracks, Gate, Academy, Embassy) ship from Phase-1
start, overriding `EMPIRE_SCHEMA_LOCK_2026-08-22.md` §1's "Academy/Embassy later." Real constraint
found: Academy/Embassy's documented function (`MOS_v1.2.md` §11) is guild timer-help — same
trusted-server dependency blocking Bazaar/Guild Expedition/Raid. Owner decision: **solo-only
stripped Phase-1 stand-ins now** — Academy = personal research + Materials sink, no guild-help;
Embassy = near-placeholder (little/no real solo function per its own design) — full guild-
integrated versions wait for the server. Design task with ChatGPT; interlock/timeline below is
STALE until the 5-building recompute lands.

### Stale, superseded pending recompute — Castle/Barracks/Gate only version (kept for reference)
- Base: Gold (1,779,550 L30) + Construction Materials (faucet: 5,000/regular stage, 50,000/finale,
  1,815,000 campaign total; sink: 3,000→31,000 tier ladder, 1,479,000 for all 3 buildings to L30,
  336,000 buffer). Materials additive to Gold, not a replacement.
- **Two simultaneous construction slots**, gated by a Castle-level interlock (Barracks/Gate max
  level tracks Castle level, e.g. Castle 15 → Barracks max 15 / Gate max 13) — not unlimited
  parallelism, bounded by the interlock table.
- Timers: client-clock pacing only, no speed-up purchase (blocked on §8 until a trusted server
  exists). **Final bands:** 30-60min (T1-5) → 4-8h (T6-10) → 1-3d (T11-15) → 4-8d (T16-20) →
  8-12d (T21-25) → 7-14d (T26-30), monotonic. Total: **~6.7 months** with 2 builders (owner-
  confirmed 6-9mo target). Academy/Embassy later add to **~11.1 months** — computed, not hand-waved.
- Construction Contracts: **275,000 Materials/season cap (18.6% of 1,479,000)** — never reduces
  timer, never raises level cap, never bypasses the interlock.
- Downstream note, still needs re-review: `Guild_Competition_Rewards_v1.md`'s Master Builder office
  assumption (written when construction was instant-only) was corrected in the Drive doc
  2026-08-23 but the office itself hasn't been re-reviewed against the final 2-builder/interlock
  model yet.

## Trusted-server dependency — blocks 4 systems, still unresolved

Bazaar, Guild Expedition, Raid Troops, and SocialSafety's live client-test all need real server
authority. Four CloudCode modules authored and locally tested (`docs/CLOUDCODE_TRACK_STATUS_2026-08-23.md`)
but nothing deployed. Track was explicitly **paused 2026-08-23** — not MVP-critical, resumes only on
owner request.

## Content backlogs (real, ready-to-code, not design questions)

- ~~10 of 14 locked Phase-1 spells never implemented~~ ~~Phase-1 spells inert, no loadout system~~
  **RESOLVED 2026-08-23** — all 14 live in `AvatarSpell.CreatePhase1Catalog()`; `SpellUnlockResolver.cs`
  (Avatar level/chapter → unlocked pool) + `SpellLoadoutAutoEquip.cs` (one spell per effect type,
  strongest-unlocked-wins, structurally satisfies max-1-AvatarStrike) now wired into
  `BattleController.StartMatch` and the real `GameBootstrap.cs` Campaign call site. 12 of 14 spells
  now actually reachable by players; fresh-L1 regression-tested to resolve to the exact old starter
  four. 213/213 tests, no Save-schema change. Commits `6e88092`/`af324f8`.
- **Open, real:** 2 of 14 spells (Sun Lance, Tempest Brand) permanently locked — both gate on "Ch2/Ch3
  spell book" acquisition, and no per-chapter `SpellBook` ItemId variant or inventory/ownership
  tracking exists on `PlayerProfile` for it. `SpellUnlockResolver.HasUnresolvableSpellBookGates`
  flags this in code (test asserts it's still true). Not built speculatively — needs its own design
  pass (how is a spell book earned?) before coding.
- **RESOLVED 2026-08-25:** real player-choice loadout picker UI shipped (`SpellLoadoutSelection` +
  `SpellLoadoutPickerPresenter`, WH) — one-per-effect-type constraint enforced, pool from
  `SpellUnlockResolver` + owned SpellBookGrant ids, 41/41 tests. `SpellLoadoutAutoEquip` now the
  empty-loadout/StartMatch fallback only, not the primary path.
- **RESOLVED:** Chapter 1-3 narrative confirmed already wired in `StoryDatabase.cs` (+ tutorial
  line in `GameBootstrap`) — WH checked, no gap existed by the time this was picked up.
- **RESOLVED, this line was stale (corrected 2026-08-25 by CR):** Chapters 4-10 already have real
  narrative dialogue for every stage (4-1 through 10-30), already committed (WH, referencing a
  CAMPAIGN_10_CHAPTER_NAMING_AND_BEAT_DIALOGUE_KIT doc). Real shape: Ch1-3 fully bespoke per stage;
  Ch4-7 use a lighter shared template (AddStageDialogue helper, generic lines varying only by
  title/enemy name); Ch8-10 hybrid - bespoke naming-kit dialogue at each chapter's 1st/15th/final
  stage, generic template for the rest. Test coverage already exists per-chapter
  (Chapter4FullDepthTests.cs through Chapter10FullDepthTests.cs assert StoryDatabase.GetSequence
  for every stage) - no gap. **Remaining real, un-decided question:** is Ch4-7's generic template
  an intentional cost/scope decision (matches "cheap to commission" framing) or should it be
  upgraded to Ch8-10's bespoke-key-beats treatment? Real scope/priority call for the owner -
  Story/StoryDatabase.cs is Metagame-seat (WH) owned, not Battle's to execute either way.

## Empire Expedition — post-campaign farm loop (LOCKED 2026-08-24, structure only)

Repeatable Stamina-gated stages, separate from Campaign (which keeps replay=0 unchanged). Pays
small Gold+Materials/clear, capped by Stamina availability. Auto-Fight toggle uses randomized/
suboptimal spell choices (manual play stays stronger), same reward as manual win, one authoritative
server transaction. Guild bonus: active members (3+ validated Guild Contribution actions/UTC week,
server-recorded only) get +10% Gold on Expedition clears, capped at the existing daily Expedition
Gold cap (bonus cannot raise the cap), fails closed to base Gold if guild service unavailable.
Title/frame bonus dropped - no achievement system exists. Backend-dependent (trusted guild service),
same class as Bazaar/Guild Expedition/Prison. Numbers still open: Stamina cost/clear, tier rewards,
daily attempts, unlock condition, rotation cadence.

## Battle Pass / Season Pass (LOCKED 2026-08-24, structure only)

Free+paid track, shared Season XP (Campaign first-clears, Expedition clears, Tower/PvP later).
Free track: cosmetics, small Gold/Materials/Stamina/Avatar XP/Event Medals. Paid track: more of the
same bounded resources + cosmetic exclusivity + volume only - no cards/packs/Evolution/Forge-Dust/
Permits on either track (checked against "no purchase = combat/deck power" and "solo rewards stay
Empire/Avatar-side"). 28-day season, UTC-week-anchored - this is a NEW shared value, not something
Battle Rating already had locked (corrected a false-precedent claim in the packet). Open: XP curve,
exact reward amounts, price, claim grace period.

## Battle Pass Stamina/purchase/stacking correction + Shop pack pity (LOCKED 2026-08-24)

Pass Stamina claims count against the existing 4/24h cap, no bypass. Paid-track purchase reuses
existing Shop/IAP entitlement flow, no new backend. Pass Gold claims and Guild +10% Expedition bonus
are orthogonal (milestone vs. per-clear), never stack. Pack pity: per-SKU counter (prevents cheap-
pack farming premium-pack pity), floor guarantee only (doesn't change base odds), atomic 5-step
transaction (spend/draw/grant/pity-update/save, full rollback on failure). Pity counter is a new
additive PlayerProfile field - flag for frozen-file coordination when implemented. Open: exact
threshold N.

## Daily Login + Daily Quests (LOCKED 2026-08-24, structure only)

Login track: Empire/Avatar-side rewards only (Gold/Materials/Stamina/Avatar XP/Event Medals/
cosmetics/Pass Season XP), streak PAUSES on a missed day (doesn't reset), no offline-clock
manipulation can create multiple claims. 3 daily quests/UTC day from a rotating pool, each grants
small bounded reward + Pass Season XP once/day, no cards/packs/Evolution/Forge-Dust/Permits/Market
Credits ever. PvP quests explicitly barred from awarding progression loot that harms another player
(ties to the locked no-offensive-progression rule). No paid skip/streak-protection - Pass ownership
adds its own track only, never multiplies these. Needs additive PlayerProfile fields
(lastLoginClaimUtcDate, loginStreakIndex, dailyQuestUtcDate, dailyQuestCompletionMask, optional
dailyQuestGenerationId) - **implementation dispatch queued behind the Empire Expedition save-field
change already in flight, not sent concurrently, to avoid two frozen-file edits colliding.**

## Spell Visual Identity Phase-1 (LOCKED 2026-08-24, design only)

3-layer model (school palette/shape/motion -> effect-type target shape -> per-spell signature beat).
School language: Andras=ember/crimson/aggressive diagonals; Ktini=jade/earthen/organic rings-roots;
Pnevmas=ivory/gold/cyan/precise geometry. AvatarStrike gets its own 4-beat commitment sequence
(Commit->Lock->Release->Consequence), explicitly not "the normal effect but bigger," always targets
the Avatar panel not a lane. Production boundary: ships now with existing sprite/fade/scale system
(school flash, lane outline, Avatar reticle, damage numbers) / needs authored flipbook sheets
(moving trails, staged effects) / needs Unity ParticleSystem (persistent embers/motes/debris) - VFX
Graph explicitly rejected for Phase-1 (2D UI, no 3D pipeline benefit). One correction applied before
lock: War Cry corrected to Pnevmas (was mis-listed as Andras in the draft; locked catalog confirms
Pnevmas). All other 13 spell-school assignments verified against SPELL_CATALOG_v1.md, no other
errors found.

## Full 36-Spell Catalogue Diagnosis (LOCKED 2026-08-24)

**Target confirmed: 36 spells (14 Phase-1 + 22 Phase-2), not 14, not 50.** Verified against real code:
Celestial Verdict's 110 Energy cost exceeds the hard-capped MaxEnergy=100 (BattleController.cs) -
uncastable as written, corrected to 100. Auto-equip's "highest magnitude wins per effect type" rule
mathematically hides most unlocks (Cinder Lash/Ember Wave/Vital Spark/Rallying Gale/Sun Lance/Tempest
Brand can never be auto-selected over their stronger same-type starter) - real player-choice loadout
(ownedSpellIds/equippedSpellIds) required, not just a better auto-equip heuristic.

**Corrections locked:** Celestial Verdict 110->100 Energy. Blood Price Energy 38->55 (the "price" is
commitment cost, no self-damage). Veil of Zeus shield 6->10/unit. Oracle Sight 42->30 Energy (still
draw-2/CD4, now cheaper-but-slower vs Leyline Draw's 36E, neither dominates). Titan Seal duration
1->2 clashes (flagged: needs simulation before shipping, 2-clash silence may be oppressive). Purge
splits into Cleanse (friendly, remove hostile modifier) and Dispel (enemy, remove positive modifier).
Reposition splits into Windstep (move to adjacent lane) and Seismic Swap (exchange two units, any
lanes). Vulnerability = single mark, +1 dmg on next hit, consumed on trigger, expires next clash if
unused. Spell-granted Attack buff caps at +3/unit total (War Cry/Rallying Gale/Banner of Ashes/
Thunder Decree don't stack past this). Shields don't stack additively, stronger replaces weaker only,
expire at match end. Silence suppresses triggered abilities only (not base stats/lane bonuses),
stays blocked until real suppressible abilities exist. Max 1 successful cast per side per combat
tick (player path currently has no equivalent guard to AI's - needs adding).

**5-wave build order:** (1) make the 14 real - School field, data-driven definitions, owned/equipped
save fields, manual loadout, Sun Lance/Tempest Brand acquisition, per-tick cast cap, stage/archetype
AI loadouts (not mirrored player progression), accepted Phase-1 VFX brief. (2) Expand to 19 using
already-implemented effects (Magma Rend/Blood Price/Grave Mend/Celestial Verdict/Aegis Return),
corrections applied first. (3) Shields+Cleanse+Dispel+Vulnerability+Thunder Decree. (4) CrossLane/
AllLane damage+DrawCards+Reposition. (5) Silence package - blocked until suppressible card abilities
exist. Visual production: 3 school families + 13 effect-shape templates + 36 icons + 36 signature
accents + 5 bespoke AvatarStrike sequences, not 36 independent VFX systems.

Real gap flagged, not yet resolved: AI spellbooks currently mirror the PLAYER's progression-derived
loadout rather than having their own stage/archetype-authored one - needs fixing in Wave 1.

## Spell-Book Acquisition + Ownership Sync (LOCKED 2026-08-24, owner-authorized frozen field)

Spell Books: chapter-finale first-clear-only grant (no drop/purchase/trade/farm), permanent
ownership, not consumable. ownedSpellIds : List<string> is a new additive PlayerProfile field
(OWNER AUTHORIZED), equippedSpellIds is the separate Wave-1 loadout field. Sync service runs inside
every authoritative progression transaction (Avatar level-up, stage first-clear, Spell Book grant,
new-profile creation) plus once as a migration repair pass - NEVER at battle start (battle stays
read-only/validation-only, no save mutation) and never on-demand/polling. SpellUnlockResolver stays
a pure eligibility calculator; it no longer directly builds the battle spellbook - that's now
ownedSpellIds -> equippedSpellIds (subset) -> catalogue resolution. This makes the existing
battle-start resolver call in BattleController.StartMatch obsolete - real refactor needed on
already-shipped code, not just new code. Migration for old saves: infer Sun Lance/Tempest Brand
ownership only from confirmed chapter-2/3 finale completion evidence, not just chapter visibility.

## AI Tier -> Stage-Gated Spell Access (LOCKED 2026-08-24)

Cumulative pool, authored progression fiction (not a mirror of player state): Novice=Cinder Lash+
Vital Spark; Apprentice adds Fault Line+Renewal; Veteran adds Sun Lance (Ch2 book)+Banner of Ashes;
Master adds Tempest Brand (Ch3 book); Titan inherits all 7, no Titan-exclusive spell. AI resolver
must never read player ownedSpellIds/equippedSpellIds/stage completion/Spell Book claims - tier
membership alone determines the pool. Verified against SPELL_CATALOG_v1.md - all stage/book unlocks
match exactly. Existing highest-magnitude-per-effect auto-equip heuristic still applies on top -
Cinder Lash/Vital Spark/Sun Lance/Tempest Brand become eligible but stay unselected (weaker same-
effect-type options win); Fault Line/Renewal/Banner of Ashes do materially change tier loadouts.

## Mirrored AI-Spellcasting Simulation Matrix (LOCKED, retroactively written 2026-08-24)

Was fully specced and agreed earlier this session but never committed to this file until now -
coding room correctly refused to guess it and flagged the gap. Real spec, now real:

**Metrics per scenario:** AI win rate, player win rate, average combat ticks, early-KO rate (match
resolved before Clash 3), AI spell-cast rate, spells cast per match, no-spell fallback rate, spell
contribution delta (derived: AI-casting scenario metrics minus Baseline-parity scenario metrics -
not separately collected).

**Trial shape:** matches existing unseeded Monte Carlo style (BalanceSimulationTests), no new seed
framework. 1,000 trials/scenario minimum; 2,000 if early-KO or spell-use rate is below 5%. Fresh
battle state/hands/spell state every trial. Report count, percentage, AND Wilson 95% CI per metric.
Paired scenario groups, spells off vs on, identical deck/config.

**5 scenarios per chapter-archetype group** (not run globally once): Baseline parity (spells off/
off), AI casting (on/off), Full match (on/on), Fallback stress (on/off, tests unaffordable/invalid
handling), Resource stress (on/off, low-resource deployment/timing).

**Acceptance bands, AI casting scenario** (relative to Baseline parity unless noted): AI win-rate
delta within -5 to +8pp. Player win-rate delta no unexplained drop >8pp. Average ticks within ±15%.
Early-KO rate ≤10%, no more than 5pp above baseline. AI cast rate 25-70% of matches with ≥1 legal
opportunity. Spells/match 0.5-2.5 ordinary. No-spell fallback 10-45% of matches. Invalid-target/
illegal-cost casts: HARD FAILURE on first occurrence (a per-trial assert, not a percentage). Spell
contribution: no single spell >40% of AI wins.

**Full Match scenario** (relative to AI-casting/player-off scenario, not Baseline parity):
invalid-cast hard failure same as above. Early-KO ≤10%, no more than 5pp above the AI-on/player-off
scenario. Average ticks within ±20% of the AI-on/player-off result. AI cast rate and win rates are
DESCRIPTIVE ONLY, not gated. Spell contribution: compare vs AI-on/player-off, flag unusually large
shifts for CC review, never auto-retune.

**Fallback/Resource stress scenarios:** same correctness gates as AI casting (zero invalid casts);
other metrics descriptive/diagnostic only.

**Failure policy:** any missed target is logged and escalated to CC. The harness itself NEVER
auto-retunes balance values - same as the Auto-Formation Balance Soft precedent.

**Chapter coverage:** run by chapter-archetype group - Ch1 (novice/low-resource AI), Ch2-3
(standard, basic spell access), Ch4-6 (advanced archetypes), Ch7-10 (endgame patterns). One
representative scenario may cover a group if chapters genuinely share identical AI config
(deck/spell-list/resource-profile/SoloAIScalingSystem tier) - coding room's own call based on real
config, not assumed.

## AI Spell Cast Probability Gate (LOCKED 2026-08-24)

Real fix for the simulation matrix's blown bands (AI cast rate ~99.9%, win delta +45-55pp vs bands
25-70%/-5 to +8pp) - root cause was §5's tactical clauses having no frequency limit, not a coding
bug. Added: after AI selects its best legal spell+target for the tick, roll once (match-seeded RNG,
reproducible) - 40% cast, 60% deliberate pass. No reroll on pass, no roll if no valid candidate,
still max 1 cast/tick on success. Applies equally to all 5 tiers - HP/resource scaling stays the
only difficulty lever, cast frequency is not a second hidden tier multiplier. §5's tactical clauses
still gate candidacy (quality); the roll gates frequency - different problems, both needed. Bands
themselves NOT revised - correctly rejected as "would redefine overwhelming dominance as
acceptable." Full matrix rerun required after implementation; if win-delta still exceeds band,
escalate to CC as balance evidence, do not auto-retune.

## AvatarStrike Once-Per-Match Commitment Throttle (LOCKED 2026-08-24, amends the gate above)

Second matrix re-run: general 40/60 gate fixed cast-frequency but spell-contribution-to-wins stayed
71-79% vs the locked ≤40% cap, root-caused to AvatarStrike's lethal-only candidacy gate making it
dominate win-attribution whenever it fires, independent of frequency. GPT decision (Option 2 of 2
presented, NOT the exemption option): AvatarStrike does not get exempted from the 40% contribution
cap - a spell dominating outcomes is still dominating outcomes even if each individual cast is
"intelligent." Fix: AvatarStrike receives a SEPARATE, stricter roll that REPLACES (does not stack
with) the general 40/60 gate for AvatarStrike candidates specifically:
- Non-AvatarStrike candidates: unchanged 40% cast / 60% pass, every eligible tick.
- AvatarStrike candidate: exactly ONE 10% commitment roll per match, taken the first time an
  AvatarStrike satisfies all existing legal+lethal-finish conditions. Success = cast it. Failure =
  AvatarStrike disabled for the AI for the rest of that match, no retry on later ticks.
Reasoning stated by GPT: a fresh 10% roll every lethal tick would asymptote toward certainty and
just delay the same dominant finish - the once-per-match commitment is the actual restraint.
Energy cost/cooldown/clash-3 rules unchanged. Existing matrix bands unchanged (cast rate 25-70%,
win-rate delta -5/+8pp, spell contribution ≤40%) - report misses to CC, do not auto-widen.

**Implemented and matrix-rerun 2026-08-24** (`BattleController.RollAvatarStrikeCommitmentGate()` +
`AISpellCaster` branching to it for AvatarStrike candidates only). Real result, not a bug: spell-
contribution concentration is fixed, but the throttle over-corrected the other direction -
Apprentice/VeteranPlus spells/match crashed to ~0.28 (below the locked 0.5 floor) and Novice
player win-rate rose 9.5% vs baseline (exceeds the locked 8pp cap) - AI now casts too rarely
overall and is measurably weaker at Novice. Escalated to CC/GPT per this entry's own failure
policy, not auto-retuned.

**GPT Option 2 implemented 2026-08-24**: raised the general (non-AvatarStrike) cast gate 40%->60%,
AvatarStrike's throttle left unchanged. Real result, mixed, not a clean fix - re-run per GPT's own
instruction to report the measured result rather than tune further on expectation:
- Apprentice spells/match improved 0.29 -> 0.35, still below the 0.5 floor.
- VeteranPlus spells/match cleared the floor (no longer flagged).
- New failures: Novice AI win-rate delta 8.5% (just over the 8pp cap) and VeteranPlus player
  win-rate dropped 9.1% (exceeds the 8pp cap) - AI got measurably stronger at both tiers now, not
  just more active. Escalated, not auto-retuned.

**GPT's "structural, not a single percentage" call, implemented 2026-08-24**: a flat gate can't
satisfy all 3 measured tiers - tier-specific `NonAvatarStrikeGateProbability`: Novice 45%,
Apprentice 85%, Veteran 45%, Master/Titan unchanged at 40% (never separately measured by the
matrix - VeteranPlus stands in for Veteran specifically - so left alone pending real measurement).
Decisive result: **Apprentice's spells/match did NOT move at all** going from 60%->85% (0.35 both
times) - strong real evidence the 0.5 floor may be structurally incompatible with Apprentice's
available spell pool (fewer legal candidates than the other tiers), exactly the fallback
conclusion GPT named if this happened. Novice (player win-rate dropped 9.6%, over the 8pp cap) and
VeteranPlus (spells/match 0.31, back below the floor at 45%) both still fail too, in different
directions from before. Escalated with full numbers, not auto-retuned - awaiting GPT's read on
whether the Apprentice floor itself needs revisiting.

**GPT's diagnostic-before-tuning call, run 2026-08-24** (`AISpellCaster.DiagnoseCandidates` +
`AiSpellCastCandidateDiagnosticTests`, 2000 trials/tier, no band assertions - measurement only).
Decisive real result: **target-legality rejection dominates energy+cooldown combined by 3-3.7x at
every tier** (Apprentice 44698 vs 13071 combined; Novice 35949 vs 9742; VeteranPlus 44925 vs
15105) - the real bottleneck is §5's own tactical restraint clauses (heal-changes-survival/
damage-can-defeat-a-unit target conditions), not Energy or cooldown timing, and NOT the roll
probability. Apprentice's ordinary candidates appear on only 3.2% of ticks (623/19464 observed) -
theoretical max spells/match even at a 100% roll is ~0.39 (0.31 ordinary + ~0.08 AvatarStrike),
still below the locked 0.5 floor - proof the gate percentage was never the real lever for
Apprentice. Numbers self-consistently match the earlier matrix's measured spells/match at each
tier's gate (Apprentice 538+163=0.35/2000; Novice 856+138≈0.497/2000; VeteranPlus 551+89≈
0.32/2000), validating the harness. Reported to GPT with full numbers; no further tuning without
direction.

**GPT's Option 1 decision, implemented 2026-08-24**: Apprentice's spells/match floor lowered
0.5->0.30 (its measured 0.35 now passes) - the diagnostic proved 0.5 was mathematically
unreachable (theoretical max ~0.393 even at a 100% ordinary roll), not a probability-gate defect.
Novice/VeteranPlus/Master/Titan keep the 0.5 floor. GPT explicitly ruled out loosening §5's
target-legality clauses (would worsen already-problematic win-rate deltas) and explicitly said not
to fix Novice/VeteranPlus's separate issues via this change.
**New real finding after the fix, not yet addressed by any GPT decision:** Apprentice's no-spell
fallback rate is 65.8% (locked ceiling 45%) - same root cause (only 3.2% of ticks produce a legal
ordinary candidate), a different metric tripped by the identical scarcity. Novice (AI win-rate
+9.2pp, over 8pp) and VeteranPlus (spells/match 0.30, at/below its own unchanged 0.5 floor) remain
open too. Escalated, not auto-retuned.

**GPT's follow-up decision, implemented 2026-08-24**: Apprentice-specific no-spell fallback
ceiling raised 45%->70% (same root-cause scarcity as the spells/match fix, treated together per
GPT's own call that the two metrics describe one missing-opportunity-pool problem, not two
separate ones). **Apprentice is now fully fixed - no longer appears in ANY matrix escalation.**
Only Novice (player win-rate -11.9pp, over the 8pp cap) and VeteranPlus (spells/match 0.32, below
its own unchanged 0.5 floor) remain open, exactly the two GPT explicitly kept separate from this
fix. Candidate-scarcity itself stays flagged for a later Apprentice spell-pool/content review, not
solved here.

**GPT's per-spell impact diagnostic, run 2026-08-24** (`AiSpellCastImpactDiagnosticTests`, paired
baseline/on with identical seeds, 1500 trials/tier). Decisive - Novice and VeteranPlus are
genuinely different problems, not the same one:
- **Novice = an impact problem, not a frequency problem.** Already clears its 0.5 floor (0.52
  spells/match). Firestorm (LaneDamage) casts 646/1500 trials (0.43/match) with a 95.7% same-tick
  kill rate, appearing in 25.5% of AI wins - the real driver of the -11pp player win-rate swing
  (45.1%->34.1%). Divine Bolt/War Cry contribute far less (16.1%/4% of wins).
- **VeteranPlus = a frequency problem, matches Apprentice's pattern, not Novice's.** Fault Line has
  a similarly high 98.1% same-tick kill rate but only 3.9% win-correlation (vs Stone Judgment/
  AvatarStrike's 17.4%) - win-rate stays within its own 8pp cap at the current 45% gate; the real
  problem is candidate scarcity (0.31 spells/match, below its unchanged 0.5 floor), same shape as
  Apprentice's original issue.
Escalated with full per-spell numbers, not auto-retuned.

**GPT's per-tier decisions on both, implemented 2026-08-24**:
- **Novice**: Firestorm-only restraint (>=2 living units in target lane, OR a single-unit lane
  that's a genuine reciprocal threat to the AI's own board - reuses the existing heal-lane "real
  threat" concept, no new numeric threshold invented). `AIDifficultyTier` threaded through
  `TrySelectCast`/`TryPickTarget`/`DiagnoseCandidates` (optional, defaults null, every other
  tier/spell unaffected).
- **VeteranPlus**: same floor/ceiling treatment as Apprentice (0.30 floor, 70% fallback ceiling),
  gate held at 45% as specified - confirmed the same candidate-scarcity class via the impact
  diagnostic, not Novice's problem.
Two now-stale hard assertions in `CampaignAfMirroredAiSpellWinnabilityTests.cs` (EnemyCastCount>0)
softened to logged observations - real production Campaign launches are tier-authored (Ch1 =
Novice), so a zero-cast outcome from the restraint is now legitimate, not the softlock regression
those asserts guarded against (still covered separately by the MirroredEnemySpellsEnabled assert).
**Real result: VeteranPlus fully passes now. Novice's original -11.9pp win-rate delta is gone.**
But a new failure appeared: Novice's cast-rate-of-opportunity dropped to 17.7% (floor 25%) and
spells/match is 0.12 (both assertions exist; NUnit only surfaced the first one hit before the test
method aborted). The restraint fixed win-rate but over-corrected into near-total Firestorm silence
at Novice. Escalated, not auto-loosened.

**GPT's loosened single-unit exception, implemented + independently verified 2026-08-25**
("can damage" Attack&gt;0 replacing "can defeat" Attack&gt;=CurrentHealth, new
`LaneIsActiveReciprocalThreat` helper). Verified twice - once by CC, once independently by VS
(2000 trials each, numbers within rounding of each other, confirms this is real not sample noise):
- **Novice: win-rate delta now solved** (+2.4pp, well inside the ±8pp band). But cast-rate
  (17.7-17.9%, floor 25%), spells/match (0.12-0.13, floor 0.5), and fallback (87.5-88.3%, ceiling
  45%) are all still badly over-corrected, unchanged from the tighter "can defeat" version - the
  loosened exception did not measurably recover Novice's ordinary cast frequency. Still open.
- **Apprentice: fully solved** - win delta +6.4pp, cast rate 34.6% (band 25-70%), spells/match
  0.34 (floor 0.30), fallback 66.7% (ceiling 70%).
- **VeteranPlus: one new miss** - fallback rate 71.9%, just over its own 70% ceiling. Everything
  else at VeteranPlus reads as passing.
Reported to GPT with full numbers, not auto-loosened further.

## CloudCode Modules Deployed to nonprod-validation, Live-Verified (LOCKED 2026-08-24)

All 4 CloudCode modules (SocialSafety, PermitWeekKey, GuildExpedition, Bazaar) are deployed to the
real `nonprod-validation` UGS environment via the UGS CLI and confirmed live-working, not just unit
tested. Real deployment blockers found and fixed via a live PlayMode validation test run against
the actual deployed environment (not guessed, not local-only tests):
1. Each module had 2 public constructors - Cloud Code requires exactly 1. DI constructor made
   internal, only the parameterless production constructor stays public.
2. Missing `ICloudCodeSetup`/`config.AddGameApiClient()` registration (`ModuleConfig.cs`, added to
   all 4) - without it `IGameApiClient` arrives null on every function call, confirmed via
   diagnostic logging at the function entry point before any module code ran.
3. Cloud Save item keys violated the real key contract (1-50 chars, `[A-Za-z0-9_-]` only, no dots) -
   all 4 modules used dotted/64-char-hash keys. Replaced with `<short-prefix>_<32-hex-char-hash>`
   keys everywhere (relationship/rate-limit/permit/attempt/contribution/instance/listing/
   idempotency keys).
4. The live production client (`UnityAuthenticationSocialService.cs`'s
   `UnityCloudCodeSocialSafetyGateway`) used a flat `{"targetAccountId": "..."}` call shape - Cloud
   Code function args must be wrapped as `{"request": {...}}` matching the method's parameter name.
   Fixed; this had never been exercised end-to-end against a live deployment before.

Confirmed via 4 live PlayMode validation test files (real network calls against
nonprod-validation, not mocked) - all 4 modules now genuinely live-round-trip-tested, not just unit
tested:
- SocialSafety: 11/11 (Block/Unblock/Mute/Unmute, idempotency, self/blank-target rejection,
  rate-limit exhaustion)
- PermitWeekKey: 5/5 (status, claim, idempotent re-claim, post-claim status, invalid request)
- GuildExpedition: 8/8 (attempt consumption, objective scoring + idempotency, unknown-objective
  rejection, milestone claim + idempotency, invalid threshold)
- Bazaar: 6/6 (wallet read, invalid-request rejection, not-found paths for list/buy/cancel - the
  full list->buy happy path isn't testable from here since this module has no endpoint to create an
  ItemInstance, that's owned by the Collection system, not this scaffold)
All 4 fixes generalized cleanly across every module on first or second try - no new module-side
bugs found beyond the original 4, only a couple of missing fields in the test harness's own
response DTOs (Bazaar's BuyResult), fixed immediately. GuildExpedition/PermitWeekKey/Bazaar still
have no client-side game code calling them yet (only SocialSafety does) - that remains open, but
the modules themselves are proven working end-to-end.

## AI Tier -> Stage-Gated Spell Access - CONFIRMED (2026-08-24, ratifies the LOCKED entry above)

GPT independently re-derived the same cumulative tier mapping already locked above (Novice=Cinder
Lash+Vital Spark; Apprentice adds Fault Line+Renewal; Veteran adds Sun Lance+Banner of Ashes;
Master adds Tempest Brand; Titan inherits all 7, no exclusive) with matching stage/book grounding
(Ch1 1-2/1-6, Ch2 2-4/2-8 pre-finale, Ch2-finale-book Sun Lance + Ch3 3-3 Banner of Ashes,
Ch3-finale-book Tempest Brand). No changes required. Confirms the existing implementation is
correctly grounded, not just internally consistent. Tests must keep distinguishing eligible-in-pool
from actually-equipped (auto-equip heuristic still suppresses Cinder Lash/Vital Spark/Sun Lance/
Tempest Brand behind stronger same-effect options) - no heuristic correction is in scope here.

## Unattended overnight run started (2026-08-25, ~02:00)

Owner is stepping away; CC, VS, and CR left running unattended. Permission modes confirmed before
leaving: VS = Auto ("approve actions that pass a safety check, pause for anything risky"), CR =
Bypass permissions. This CLI session (CC) remains on default allowlist-only mode (no terminal
access to toggle it away from here).

State at handoff:
- Committed and clean: CardDatabase pollution fix (2179bae, 53 files, 1001/1014), WH's 4 test-fix
  diffs (3035657, 26/26), spell loadout picker (4f25839, 41/41).
- VS assigned: (1) verify+commit the uncommitted loadout-picker nav wiring (AvatarPresenter/
  HomePagePresenter/CampaignMapPresenter/GameBootstrap - real, coherent, just unverified this
  session), (2) investigate Chapter2-10FullDepthTests winnability failures - real MVP-gate-relevant
  work (Campaign playability row), separate from the parked Novice/VeteranPlus AI-balance thread.
  VS's own transcript also independently surfaced a live test-isolation bug (something wiping the
  card DB between certain classes in full-suite runs, distinct from the already-fixed pollution
  bug) and was mid isolation-re-run to pin it down at handoff time.
- CR assigned: close the spell-catalog gap (19/36 implemented per AvatarSpell.cs CreateCatalog(),
  17 missing) against docs/SPELL_CATALOG_v1.md, with real EditMode test coverage per spell.
- AI-balance tuning (Novice cast-frequency over-correction, VeteranPlus fallback-ceiling miss)
  remains explicitly parked - not to be picked up overnight unless it blocks something else.
- Next-milestone target (owner-stated 2026-08-24): 20-30 real UI screens matching mockup (no dead
  space), all 36 spells, 11+ buildings, Chapters playable to 10-30, at least one minigame. Current
  real state at handoff: ~22 UI presenters exist (several are shells, not content-complete per
  mockup - unverified), 19/36 spells, 11 buildings structure-locked but only Gate has real Castle
  interlock logic, Chapters 1-10 have test coverage only (11-30 don't exist), Memory Expedition
  minigame is UI shell only (no real minigame logic). This overnight run will not close that whole
  gap - expect partial progress (spell catalog additions from CR, Chapter winnability findings and
  nav wiring from VS), not full completion, by morning.

## Chapter*FullDepthTests non-determinism - PARTIALLY FIXED, real remainder PARKED (2026-08-25)

Loadout-picker nav wiring committed clean (94703bc, full 132-class run 1004/1014 - the 10 failures
are all pre-existing AI-balance-thread content, unrelated to this wiring).

Investigating why Chapter2-10FullDepthTests fail on a different stage every run: root cause #1
found and fixed (e5f2ea1) - AISpellCaster's cast-probability-gate RNG was never seeded in these 9
test files (same gap already fixed elsewhere for CampaignAfMirroredAiSpellWinnabilityTests), so
every run got a fresh Guid-derived seed. Fix applied (SetAiSpellCastRngSeedForTests(42) in all 9
files), verified real via diff review + clean compile.

However: pinning that seed did NOT fully close the non-determinism. Two back-to-back runs with the
identical pinned seed still failed on a different stage every time (Chapter2/3/4/5/6/7/9/10 all
shifted stage between runs). Exhaustive grep of Assets/Scripts/Battle/ and Assets/Scripts/AI/ found
only two RNG sources total, both already pinned (AI-cast-gate RNG, deck-shuffle RNG) - no second
unseeded Random/Guid/DateTime/TickCount/non-deterministic OrderBy, and deck composition itself is a
fixed List, not a Dictionary/HashSet. VarianceIndex's string.GetHashCode() was also directly tested
across two separate Unity process launches and ruled out (identical hash values both times).

Net: a real, confirmed randomness source was found and fixed (worth keeping), but a second,
unidentified source remains - likely outside Battle/AI/ entirely (UI/launch layer) or genuine
engine-level non-determinism not visible to a text search. Not pursued further past this point per
standing anti-circling rule - parked at the same priority tier as the Novice AI-balance thread.
Chapter winnability results should still be treated as unreliable/non-reproducible until this is
resolved by someone with time to trace beyond Battle/AI, or until it's deliberately reprioritized.

## UI shell audit vs "no dead space" milestone target - REAL NUMBERS (2026-08-25)

Audited all 23 presenters in Assets/Scripts/UI/ (read-only, VS, nothing edited). Against the
owner's stated next-milestone target (20-30 real UI screens, no dead space, matching mockup):

- **Real-content (14):** HomePagePresenter, CollectionPresenter, DeckBuilderPresenter,
  ShopPresenter, EmpirePresenter, AvatarPresenter, CampaignMapPresenter, SettingsPresenter,
  SpellLoadoutPickerPresenter, PackOpenOverlayPresenter, EmpireExpeditionPresenter,
  EmpireBuildingDetailPresenter, GuildExpeditionPresenter, PermitWeekKeyPresenter - all genuinely
  read SaveManager/PlayerProfile/CardDatabase/Economy state.
- **Partial (1):** BazaarPresenter - wallet + buy/list/cancel plumbing real (live IBazaarGateway
  calls), but the browse catalog is fake ("RuntimePlaceholder" wells, fake deterministic listing
  ids).
- **Pure shells (8):** DailyLoginQuestsPresenter, BattlePassPresenter, ChatSocialPresenter,
  MemoryExpeditionPresenter, MailInboxPresenter, FriendsPresenter, VipSubscriptionPresenter,
  GuildHallEntryPresenter. Zero real state reference anywhere, every field is the literal string
  "RuntimePlaceholder", actions route to in-memory stubs not real saves. GuildHallEntryPresenter's
  own content is shell but its EXPEDITION button does open the real GuildExpeditionPresenter.

This is Metagame-seat (WH) territory to fill in - Battle seat (VS/CC) doesn't own Economy/ or these
shell screens' real content per docs/AI_CONTRIBUTING.md's seat boundaries. Flagged to owner to
relay to WH as the next real dead-space-closing task; not assigned to VS/CR.

## MVP Playable Gate - CONFIRMED FULLY GREEN (2026-08-25, overnight run)

Three real fixes landed and verified overnight: loadout-picker nav wiring (94703bc), Chapter test
RNG seed pinning (e5f2ea1, partial - see parked entry above), CastleScalePublishGateTests trial-
count fix for sampling-noise flakiness (b60c3b3). All three verified stable across multiple runs,
not one-shot luck.

VS then ran the full named evidence set from docs/MVP_PLAYABLE_GATE_EVIDENCE_v1.md - all 22 suites
across the 5 gate rows, checked each actually exists first. Result: 139/139 clean (one class,
TutorialFoundationTests, initially misreported 0 cases due to a real infra quirk - it's the only
test file with no namespace declaration, so the wrapper's namespace-scoped filter silently matched
nothing; ran directly with the correct filter, 10/10 clean). Confirmed the two parked threads
(Chapter*FullDepthTests non-determinism, MirroredAiSimulationMatrixTests AI-balance band miss)
don't appear in any of the 22 named MVP-gate suites - they don't block release per the gate doc's
own scope.

**The MVP/beta gate itself is fully green as of this run.** Remaining real work is all post-MVP
milestone content (spell catalog 19/36, 8 UI shells needing real content, chapters 11-30 not
existing yet) - tracked above, not gate-blocking.

Minor loose end, not fixed (not broken, just inconsistent): TutorialFoundationTests.cs has no
namespace declaration, unlike every other test file (MyriadOfDragons.Tests). Works today, but will
silently no-op under any namespace-scoped test filter. Cosmetic/consistency fix for whoever's free.

## Spell Catalog Wave 3 shipped (2026-08-25, CR)

Real catalog count 19->27 (AvatarSpell.CreateCatalog()). 8 new spells: Ember Guard, Earthward,
Stonewall, Veil of Zeus, Cleansing Root, Gale Break, Infernal Mark, Thunder Decree. Real
BattleCardInstance state added: Shield (absorbs before Health, replace-not-stack, expires match
end), Vulnerability mark (+1 dmg on trigger, consumed, expires unused at end of its own clash, swept
in LaneBattleResolver.ResolveLaneClash), spell-Attack-buff capped +3/unit cumulative (retroactively
caps War Cry/Rallying Gale/Banner of Ashes per the already-locked correction). Veil of Zeus shield
corrected 6->10/unit per this register.

Acquisition wired: Stonewall+Infernal Mark share Ch6 spell book (6-30), Veil of Zeus 8-30, Cleansing
Root gets a real Avatar L16 SpellUnlockResolver rule. Ember Guard/Earthward/Gale Break left with no
unlock rule - Phase-2 catalog only gives a bare chapter number, no stage precision (same honest
pattern as Aegis Return, not guessed). Thunder Decree's unlock condition wasn't available this
session - flagged, not guessed.

**Real gap, explicitly out of Wave 3 scope:** none of the 5 new effects are wired into
SpellLoadoutSelection's 4-slot one-per-effect-type player picker or AISpellCaster's EffectPriority
list yet - the 8 spells are castable/ownable/unlockable but not yet equippable by a player or
AI-castable. Expanding either system is a real design call, not guessed into. Needs its own pass.

196/196 EditMode tests pass (SpellCatalogPhase1/2/3Tests, SpellBookGrantTests, BattleLogicTests,
SpellLoadoutTests). Committed as 0e74dba.

## Spell Catalog Wave 4 shipped, Wave 5 genuinely blocked (2026-08-25, CR)

Real catalog count 27->32 (committed 3591402). Landed: Ashfall + Stormchain (CrossLaneDamage),
Scorched Sky (AllLaneDamage), Leyline Draw + Oracle Sight (DrawCards) - 5 of Wave 4's 7 spells, 3
new SpellEffect values. Oracle Sight Energy corrected 42->30 per this register. Only Oracle Sight
("Avatar L20") got a real unlock rule; Ashfall/Stormchain are bare-chapter-number gaps (same honest
pattern as Wave 3's Ember Guard/Earthward/Gale Break); Scorched Sky/Leyline Draw are "event book"
with no real acquisition channel yet (same shape as Aegis Return). 210/210 EditMode tests pass.

**Genuine architectural blocker, correctly not guessed past:** Reposition (Windstep + Seismic Swap)
is NOT implemented. Every spell effect through Wave 4 acts uniformly on "every living unit in a
lane" - `Cast()`'s signature is `(caster, opponent, targetLane)`, nothing more. Reposition needs to
name a SPECIFIC unit to move (Windstep) or two specific units, possibly cross-lane, to swap (Seismic
Swap) - no existing targeting precedent for "pick one card out of a lane" anywhere in the spell
system, no locked spec for how a player or the AI would select that unit. This needs a real
`Cast()` signature extension + real single-unit-select UI - a genuinely bigger change than any prior
wave, not a mechanical extension. An automatic heuristic (e.g. "always move lowest-Health unit")
would be a guess dressed as an implementation - correctly declined rather than invented.

Wave 5 (Silence) was already known fully blocked (no suppressible-ability system exists).
**Catalog sits at 32/36 real** pending an owner call on Reposition's targeting model - the last 4
spells (2x Reposition, Silence package) cannot proceed without real design input, not more coding.

## Standing rule: HEAD-pinning discipline for all seats (2026-08-25)

Five sessions have been sharing one working tree overnight. Real, repeated cost: HEAD moved 13+
times in one night (~every 20 min), causing a stale-result-reported-as-live mistake three separate
times, plus one case of the same fix attributed to the wrong session. Flagged by VS (myriadofdragonsunity-4e/-8f).

**Standing rule, applies to every seat (CC/CR/VS/WH) from now on:** pin HEAD (`git log --oneline -1`
or equivalent) immediately BEFORE and AFTER any test run, and quote both alongside the reported
numbers. HEAD-pinning alone is not sufficient - also check `git status` for uncommitted peer edits
in flight before trusting a "clean" full-suite result, since another session's uncommitted work can
silently change what's actually being measured.

## Engine-side AI-cast determinism fix shipped (2026-08-25, VS)

StartMatch now resolves rngSeed via `override ?? Guid` at the instance level (not static - a static
would leak across PlayAgainForTests fixtures the same way CardDatabase.Instance did). Production
never sets the override, so behavior is unchanged when null. Fixes all 6 flaky chapter unlock
tests + the Ch1 gap (e5f2ea1 had skipped Chapter1 entirely - it was passing on luck, not a real
pin). CLAUDE.md runner command corrected to the working -ExecutionPolicy Bypass form. 12 files
committed, 0 error CS.

Semantic change flagged for awareness: SetAiSpellCastRngSeedForTests now pins "this and every later
match on this controller," not just the one call - confirmed non-regressive across all ~10 existing
call sites via the suite, not assumed. Documented on the method itself.

## Standing rule: hold edits while another seat holds the Unity batch lock (2026-08-25)

Real incident: VS ran a clean 1091/1095 confirmation, then a same-window re-run came back 1089/1095
with 2 new failures despite VS's own change being comment-only in between. Root-caused: CR was
mid-editing AvatarSpell.cs/SpellUnlockResolver.cs (131 changed lines) during VS's run window - not
a regression from VS's commit, but silent invalidation of a suite number by concurrent uncommitted
edits. `.unity_batch.lock` already exists for Unity process access but isn't currently honored for
file edits.

**Standing rule, all seats:** while `.unity_batch.lock` is held by another seat's test run, hold off
on editing files that suite might touch (or at minimum, commit your own change set before someone
else's run starts, don't edit mid-run). HEAD-pinning catches committed state; it does NOT catch
uncommitted peer edits landing inside another seat's run window - check `git status` for peer diffs
before trusting a suite number as final, not just HEAD.

BalanceSimulationTests (VS's file) failed under CR's in-flight edit - flagged as possibly
substantive (CR's spell changes could be moving real combat decisiveness) but not yet re-measured;
holds until CR commits its current Reposition/Seismic Swap work, per CC instruction.

## Windstep + Seismic Swap shipped (2026-08-25, CR) - catalog 34/36

Real targeting model per GPT's spec: RepositionRules.cs (shared legality evaluator - player and AI
use the same rules, cannot diverge on what's selectable vs executable), AIRepositionSelector.cs
(GPT's 4-tier priority order + tie-breakers for both spells), AvatarSpell/BattleController/
BattleCardInstance wiring, catalog entries. Committed 75bec83.

Real pre-existing gap found and fixed along the way: lane-swap bonuses (Front Attack/Middle Health)
were baked in once at deploy and never recomputed on any later move - added
BattleCardInstance.ReapplyLaneBonuses so Reposition actually updates them correctly.

291/293 on CR's own regression pass - the 2 failures are MirroredAiSimulationMatrixTests balance-
band checks (Novice cast rate, VeteranPlus win-rate delta), already-known/parked, pending a clean
re-measurement by VS once its own run isn't confounded by concurrent edits.

**Honest gap, explicitly not addressed:** the player-facing UI (tap-unit-then-tap-lane for
Windstep, tap-two-units-with-preview for Seismic Swap) is NOT implemented. Full game-logic layer
(legality/execution/AI selection) is real, covered by 19 new EditMode tests - but this environment
has no Play Mode testing, so CR correctly declined to write interactive UI blind with zero way to
verify it, rather than guess. This is the one remaining piece before Reposition is a complete
player-facing feature, separate from the 34/36 catalog-completeness number. Needs a session with
Play Mode / manual testing access, or explicit owner sign-off to ship logic-only for now.

## Tutorial hand-card overflow: partial fix confirmed, new 35px gap needs measurement (2026-08-25, VS)

Production fix (c52cd0d entry above) confirmed working exactly as predicted: card height
196.00->175.60 (now precisely the derived row height), width follows frame aspect correctly
(175.6*0.739=129.77), overflow dropped by exactly the predicted 10.2px (45.20->35.00). Arithmetic
model was correct.

Remaining 35px overflow is a DIFFERENT defect - the card now exactly fills its row, so this is a
row-positioning bug, not a sizing bug. HandAndPlacementPanel's anchors should put the card bottom
at roughly +31 on a 1080-tall canvas; actual is -35, a 66px discrepancy not explained by reading the
code. Correctly not reasoned further past this point (already reasoned wrong twice on this issue) -
next step is a one-off diagnostic logging real canvas/panel/row/card rects to settle it by
measurement, not more inference. [Finish] button's own overflow (12.2px, unchanged) is the same
class of defect on a different control - not yet addressed.

Full suite after CR's Reposition landed: 1109/1114 (HEAD 714fcc4, pinned both ends). Zero knock-on
effects from the hand-row height change - every UI-adjacent class (TutorialHandDockGeometry,
TutorialGuidance, CardTileCompositionV1, BattleReleaseLayout, CombatHudLabelClarity,
DeckBuilderReleaseGate) passed clean.

**Real regression flagged, not caused by VS or CR's work, ownership unclear:** SimulationMatrix_
VeteranPlus now fails again (-10.0pp win-rate vs the 8pp cap, was passing at 1091/1095) - post-
Windstep + the now-committed AISpellCaster tuning changed the baseline. This is the parked AI-
balance thread reopening on its own from someone else's in-flight tuning commits, not from either
VS's or CR's tonight's work. Stays parked/flagged, not auto-retuned - real result someone with
authority over AISpellCaster.cs needs to own.

## RepositionSelectionState shipped, GameBootstrap wiring deliberately held (2026-08-25, CR)

Full tap-to-target flow as testable state (RepositionSelectionState.cs, no MonoBehaviour): Windstep
unit-then-lane, Seismic Swap unit-then-unit, illegal taps are no-ops, Cancel clears mid-flow.
Committed 6158168. Caught and fixed a real bug during development: TryBuildWindstepTarget/
TryBuildSeismicSwapTarget originally trusted the caller's earlier legality check instead of
re-verifying themselves - fixed to independently re-check RepositionRules before returning
anything, closing a stale/skipped-check hole. 25/25 RepositionTests, 210/210 broader regression, no
production behavior changes elsewhere.

**Deliberately NOT wired into GameBootstrap.cs's rendering/click layer, per CC decision.** Confirmed
by reading the file: no per-unit tap interaction exists anywhere in Combat today - every spell
targets a whole lane, CreateMiniCardDisplay's rendered cards are pure visual (raycastTarget=false
everywhere). RepositionSelectionState is the ready-to-wire contract. Landing the actual wiring blind
into a 5900+ line, actively-contended shared file with zero Play Mode/device verification available
was judged the wrong tradeoff tonight (same reasoning as the concurrent-edit collisions logged
elsewhere this session) - held for a session with real Play Mode access, not urgent. Reposition's
game-logic layer is complete and real; only the player-visible interaction remains.

## RarityFrameRenderingTests: not a real bug, concurrent-edit flakiness (2026-08-25, CR)

Ran isolated 3 separate times (TestFilter only, no batch) - 9/9 clean every time, including with
VS's GameBootstrap.cs edit still uncommitted/in-flight throughout. "warrior" is a real card id;
StartApprovedTutorialBattle's 3-card deck always fully drains into Hand deterministically
(min(StartingHandSize=4, DrawPile.Count=3)=all 3), so Hand.First(c => c.Id == "warrior") should
never legitimately fail. Conclusion: same concurrent-edit-during-test-run pattern already logged
tonight (c52cd0d), not a real defect. No code changed. Drop from the real-regressions list; only
worth re-checking if it resurfaces once the tree is quiet.

## Memory Expedition minigame — implementation brief LOCKED (2026-08-25, GPT)

Deterministic card-matching memory game, face-down grid, tap-two-to-match. Up to 3 rounds per
daily run: 3x4/6 pairs/8 mistakes -> 4x4/8/7 -> 4x5/10/6. One reward-bearing run per account per
UTC day; layout generated from accountId + UTC date + rulesVersion; leave/return resumes the same
round and arrangement; closing the game cannot reshuffle or restore mistakes; no timer. Failed
round ends the run, cleared rounds stay credited; optional practice replay after claiming grants
nothing. Reward bands (single atomic claim on highest completed round): 0 rounds = 1 XP/50 Gold;
R1 = 2/100/1 research pt; R1-2 = 3/200/1 Stamina/1 Medal/2 pts; all 3 = 5/350/1/2/3. Event Medals
only while an eligible event ledger is active; Stamina respects cap, no overflow conversion;
research points expire next UTC reset; no guild/Pass/title multipliers, no Auto-Fight. Persistent
state: memoryExpeditionDayKey/Seed/RulesVersion/CurrentRound/RevealedPairMask/FirstSelectedTile/
MistakesRemaining/HighestRoundCleared/RewardClaimed + temporaryResearchPoints/-ExpiryDayKey —
ADDITIVE PlayerProfile fields, frozen-file coordination required, flag for owner sign-off.
Build split: core game logic = plain testable C# class (Battle/Empire lane, VS), UI wiring into
MemoryExpeditionPresenter = WH, queued behind WH's Ch11 + Daily Login work.

## Suppressible triggered-ability package — LOCKED (2026-08-25, GPT), unblocks Silence -> 36/36

Four existing cards get once-per-unit-per-match triggers, all magnitude 1 (1-12 scale preserved):
Goblin Caster "Hex Spark" (first clash: 1 dmg to opposing unit in lane); Cleric "Battle Mend"
(after first clash, if friendly in lane damaged: restore 1 HP to most-damaged); Novice Knight
"Shield Discipline" (first incoming combat damage reduced by 1); Phoenix "Ash Rebirth" (first
defeat: stay at 1 HP instead, once). Shared rules: triggers resolve after lane targeting, before
defeat cleanup; no recursive activation; simultaneous triggers resolve Front->Middle->Back then
slot order; per-copy independent trigger state. Silence contract: targets one deployed non-Avatar
unit; unresolved triggers can't enter the queue and queued-unresolved ones are cancelled; resolved
effects never rolled back; base stats/lane bonuses/spells untouched; if Silence expires, an UNUSED
once-per-match trigger becomes available again, consumed ones stay consumed. Acceptance: rerun
balance/early-KO/avg-tick/AI-win-rate/no-spell-fallback suites; misses escalate, never auto-retune.

## Beta social screens: OPTION B LOCKED (2026-08-25, owner decision)

Owner explicitly chose server-backed social screens for beta over honest empty-states. Work: new
CloudCode endpoints on the already-live nonprod-validation environment — Bazaar QueryListings
(browse catalog), Friends graph (add/accept/list/gift), Chat storage (channel history, post,
SocialSafety-filtered). Same module patterns as the 4 live-verified modules (constructor rules,
ICloudCodeSetup/AddGameApiClient, RFC-compliant Cloud Save keys, {"request":{...}} call shape).
Client gateways follow the existing IBazaarGateway pattern. Assigned to CR after the Silence
package lands. Empty-states remain the automatic fallback for any endpoint that misses beta cut.

## Pollution culprit STILL UNIDENTIFIED — 72c8abe ruled out (2026-08-25, VS re-run)

Re-run at HEAD 889e913->86fe2f9 (HEAD moved mid-run; 19 peer-dirty files; first attempt hit the
documented stall, exit 124 — indicative measurement, not clean): 1154 tests, 1144 passed, 10
failed, 0 error CS. The 3 order-dependent artifacts (RarityFrame Card_warrior null,
TutorialTeachingOverlay x2, byte-identical numbers to every prior run) PERSIST with 72c8abe in
history — the NormalBattleSavedDeckIntegrationTests leak was real but is NOT this pollution's
cause. 63a6afe stands amended: RarityFrame is a victim of a still-unidentified leaker, NOT
resolved. VS is building additive multi-filter runner tooling (option b) and bisecting.

AI-band drift under the parked tuning thread (flag-only, nobody touches): Novice cast rate 22.7%
(improving toward the 25% floor), VeteranPlus -9.8pp vs 8pp cap, and Apprentice NOW also out of
band (+9.0pp vs -5..+8) — first Apprentice miss since it was declared fully solved. Whole thread
stays parked pending owner/GPT decision; drift is expected while 36/36 spell content lands.

## Loadout expansion to 6 slots (tier-unlocked) + AI remeasurement mandate — LOCKED (2026-08-25, GPT)

Slots by Avatar level: new player 4, L10 = 5, L20 = 6. Max 1/effect type (still ~9 max distinct
effects to choose from at endgame - real omission choice, not "equip everything"). Max 1
AvatarStrike stays an EXPLICIT validation rule even though effect-uniqueness already implies it -
don't remove it as "redundant." No duplicate spell ids. Slots earned via Avatar progression ONLY -
never Gems/subscription/VIP/Shop (checked against the standing "no purchase = combat/deck power"
rule). Auto-equip must prioritize effect diversity first, then existing per-effect ranking - NOT
top-6-by-magnitude. Existing profiles keep their 4 equipped starters; migration does not
auto-fill newly unlocked slots (real player choice, not silently maxed).

**AI side:** AI may equip up to the same 6-slot cap, same effect/AvatarStrike rules, tier-gated as
before. Full matrix remeasurement REQUIRED per tier before any retuning (spells/match, zero-cast,
legal-opportunity cast rate, win-rate delta, avg ticks, early-KO, per-effect/per-spell cast
frequency, win attribution, AvatarStrike concentration, invalid-action invariants, no-spell
fallback) - record the new distribution first, do NOT auto-retune EffectPriority. This measurement
must run against the FINAL slot/acquisition state (post 36/36 + new channels), not mid-flight.

## Acquisition channels for all remaining spells — LOCKED (2026-08-25, GPT), catalog reachability complete

Ember Guard = stage 4-15 first clear. Earthward = 5-15. Gale Break = 7-15. Scorched Sky = Ch7
finale book. Volcanic Prison = Ch8 finale book (first Silence spell). Leyline Draw = Ch9 finale
book. Thunder Decree = Ch10 finale book (campaign capstone). Titan Seal = Avatar L30 (second
Silence, progression capstone, avoids finale-book congestion). One-spell-per-finale rule applies
going forward (existing Ch2/Ch3/Ch6 books unchanged, Ch6's 2-spell grant stays as already
approved). Stage grants first-clear-only, finale books atomic/idempotent, replays grant nothing,
migration only infers from authoritative cleared-stage/claimed-book/Avatar-level evidence. No
Shop/drop/event/trade/paid path. Eligibility != auto-equip - ownership sync grants it, player
selects within the 6-slot loadout.

**CORRECTED 2026-08-25 (CR caught this overclaim before it stood unverified):** the line below is
WRONG as originally written. Only the 8 spells explicitly named in GPT's acquisition-channel spec
(Ember Guard/Earthward/Gale Break/Scorched Sky/Volcanic Prison/Leyline Draw/Thunder Decree/Titan
Seal) have a real channel. **4 spells still have NO acquisition channel: Ashfall, Stormchain,
Windstep, Seismic Swap** - not covered by GPT's spec, not invented, real open gap. Acquisition
channels for the 8 named spells implemented + verified 274/274 (commit 493896d).

~~Net result: all 36 spells now have both a real implementation AND a real acquisition channel -
the catalog is genuinely complete and reachable, not just coded.~~ **36/36 implemented, 32/36
reachable.** Remaining: acquisition channels for Ashfall/Stormchain/Windstep/Seismic Swap (needs a
GPT round, not invented), loadout picker slot-count/tier-unlock logic, SpellUnlockResolver channel
wiring for the 8 done ones, AI loadout cap, then the mandated matrix remeasurement.

## SPELL CATALOG: 36/36 COMPLETE AND VERIFIED (2026-08-25, CR, commit ea54c36)

258/258 EditMode tests clean (23 new CardTriggerAbilityTests + full spell-catalog/reposition/
battle-logic regression). Real bugs found were all in the new test suite itself (rarity table
structurally couples Attack/Health, so early tests assumed independent stats and got impossible
combinations - fixed with clamp-aware assertions), not production code. Only Battle-owned files
committed; WH's in-flight files (Story, CampaignMapPresenter, HomePagePresenter, DailyLogin*,
Chapter11/12 tests, MemoryExpedition, Save/) correctly left uncommitted for WH's own clean commit.
3 drifting AI-balance bands (Novice/VeteranPlus/Apprentice) noted in the commit, correctly left
parked - not retuned.

**This is the actual completion of the Full 36-Spell Catalogue Diagnosis opened earlier
(commit locking the diagnosis) - every spell now has real code AND (per the separately-locked
acquisition-channel spec) a real acquisition path once that wiring lands.**

## Parallel-work protocol — LOCKED (2026-08-25), prevents lock races and dirty-tree measurements

Real problem diagnosed: Unity can only run ONE test process at a time (.unity_batch.lock enforces
this), but seats have been blind-queueing against it and running tests against each other's
uncommitted edits, producing both wasted waits and untrustworthy numbers (HEAD moving mid-run,
"artifacts persist" readings that were really dirty-tree noise).

**Rule 1 — Lane separation by file ownership (already CLAUDE.md, reinforced here):**
CR = spell catalog, acquisition channels, CloudCode/server work (Battle-adjacent, Cards/AI/Empire).
VS = pollution bisect, engine determinism, Memory Expedition core logic (Battle/Empire).
WH = campaign content (CampaignMapPresenter/StoryDatabase), Daily Login, save-field additions
(Economy/Story/UI presenter shells). Seats do NOT edit outside their lane without explicit CC
sign-off - already the standing rule, restated because tonight's clashes were files, not people.

**Rule 2 — Commit before you run, always.** Never trigger a full/continuous Unity test run while
your OWN changes are uncommitted. Finish your change, commit it, THEN queue for the lock. A dirty
working tree at run time is always partly someone else's in-flight work you can't control - the
only way to get a trustworthy number is to run against a state you (or CC) actually committed.

**Rule 3 — Prefer scoped runs over full-suite runs while iterating.** Use `-TestFilters
<YourClassName>` for your own new/changed classes during development - fast, low-value-to-others,
doesn't need to block on a "give everyone a turn" queue as hard. Reserve full continuous suite runs
for real milestone checkpoints (post-commit verification, pre-beta gate checks), not routine
iteration - this is most of tonight's lock contention, and most of it didn't need the full suite.

**Rule 4 — Announce before you run.** Post to the mailbox (or, for CC<->CR, direct message)
"starting a Unity run, ~N min" before triggering one. Other seats then do non-Unity work (writing
code, reading specs, drafting - anything that doesn't touch the lock) instead of blind sleep-loop
polling. This doesn't eliminate the queue, it makes the wait productive instead of idle.

**Rule 5 — Don't run tests to answer someone else's open question if you don't own that question.**
Tonight: VS spent a cycle bisecting pollution that turned out unrelated to the fixture it was
investigating; running the full suite "just to see" when you don't have a specific hypothesis
wastes the one shared resource. Have a real hypothesis before you take the lock.

## Final 4 spell acquisition channels — LOCKED (2026-08-25, GPT), catalog genuinely 36/36 reachable now

Ashfall = Ch4 stage 4-30 first clear. Stormchain = Ch6 stage 6-30 first clear. Windstep = Avatar
L18 (between existing L16/L20 unlocks). Seismic Swap = Avatar L24. All permanent first-clear/level
unlocks, no Shop/drop/event/trade/paid path, no new finale books (one-spell-per-finale rule
preserved), ownership sync grants but does not auto-equip. Existing assignments unchanged.

**CORRECTED AGAIN 2026-08-25 (CR caught this precisely before committing the wiring, same
discipline as the earlier catch):** "36/36 reachable" still overclaims by 1. **Aegis Return is a
permanent exception** - its own acquisition note has always been "event book later," and no event
system exists anywhere in the game (same status noted when Ember Guard/Earthward/Gale Break's
channels were assigned earlier - Aegis Return was the original template for "bare chapter number,
no real channel yet"). This is not a new gap CR introduced, just precision CC failed to hold twice
now on a round number. **Real, accurate count: 35/36 spells have both a real implementation and a
real acquisition channel; Aegis Return remains genuinely unreachable until an event system is
built (out of scope, no owner request to build one).**

Wiring in progress: the 4 new channels + earlier 8 into SpellUnlockResolver, plus a real audit
test asserting every catalog spell except Aegis Return is reachable (CR, in flight). Remaining
after that: AI loadout cap, then the mandated matrix remeasurement.

## Pollution culprit #1 FOUND: CampaignStageBattleConfigurationTests (2026-08-25, VS bisect) - NOT fully closed

CampaignStageBattleConfigurationTests confirmed as the source of the 2 TutorialTeachingOverlay
artifacts (bisected, reproduced in isolation). RarityFrameRenderingTests's Card_warrior null is
a SEPARATE, still-unidentified second leaker - explicitly not the same culprit, don't conflate.
Mechanism not yet established (VS checked GameBootstrap's TearDown pattern and the "two Canvases"
theory, both ruled out; GameBootstrap.Instance's unconditional no-guard assignment is a plausible
candidate shape, matching the earlier CardDatabase.Instance leak, but explicitly UNPROVEN - VS is
running a method-level bisect to confirm before touching anything).

Real complication if the mechanism lands on GameBootstrap.Instance: that member is FROZEN
(alongside MatchResult/OnMatchCompleted per CLAUDE.md) - any fix there needs owner sign-off, not
just a seat decision. CampaignStageBattleConfigurationTests itself is campaign-content-adjacent
(WH's lane, not VS's) per the new parallel-work protocol - VS will propose, not apply, a fix.

Shipped clean alongside: commit 97c0b74, Memory Expedition core logic + 24 tests + additive
-TestFilters runner flag (VS's own 5 files only).

## Chapter 11 + Daily Login SHIPPED (2026-08-25, WH)

Ch11 committed as adea127 (commit message got overwritten by a concurrent commit riding along -
git hygiene note, not a content issue, content is real per WH's own report). Daily Login + Daily
Quests committed as bf236e0, verified 11/11 (Logic 7/7, Shell 3/3, NavigationSpine 1/1).

**OWNER SIGN-OFF PENDING (not CC's to approve - explicitly the owner's per the frozen-file rule):**
bf236e0 adds 6 fields to PlayerProfile.cs: lastLoginClaimUtcDate, loginStreakIndex,
dailyQuestUtcDate, dailyQuestCompletionMask, dailyQuestGenerationId, plus passSeasonXp (Pass
Season XP sink - beyond the register's originally-named field list, WH self-flagged this one
specifically). All additive-only per CC's earlier direct verification of the diff.

Chapter 12 in tree, not yet committed: stages 12-1 to 12-30 ("The Mortal Host"), Gate L30 ceiling,
finale 12-30, story + tests. Last batch 10/11 (only 12-8 AF failure, retuned roster, re-verify
pending - blocked by lock contention with other seats, same shared-resource issue as tonight's
other work). CR's Silence/loadout files correctly left unstaged by WH.

## 6-slot loadout expansion SHIPPED (2026-08-25, CR, commit e8ef4c4) - 270/270

Slots by Avatar level (4/5/10/20 -> 4/5/6 slots). Max 1/effect type, explicit max-1-AvatarStrike
tracked as its own status (not just implied by effect-uniqueness), no-duplicate-id as a distinct
check from duplicate-effect, diversity-first auto-equip proven with a real test where naive
top-N-by-magnitude would have picked wrong. No-auto-fill-on-migration was already structurally
true (SpellOwnershipSync only backfills an EMPTY loadout) - added a real test proving it rather
than just asserting. AI resolver reuses the existing avatar-level tier table.

**Real gap, correctly not shipped blind (same pattern as the Reposition UI split):**
SpellLoadoutPickerPresenter still only renders 4 effect-type columns. A real 5/6-slot picker UI is
a separate task, not attempted without Play Mode verification. Stopgap: a player whose level
unlocks 5/6 slots gets an honest, clear refusal on this screen (loadout untouched), not a silent
failure or truncated save. Real next UI follow-up, needs scheduling (Play-Mode-capable session or
explicit owner sign-off to ship logic-only).

## Owner sign-off: Daily Login/Quest PlayerProfile fields APPROVED (2026-08-25)

Owner approved the 6 additive fields in bf236e0 (lastLoginClaimUtcDate, loginStreakIndex,
dailyQuestUtcDate, dailyQuestCompletionMask, dailyQuestGenerationId, passSeasonXp). No longer
pending - frozen-file addition is finalized.

## Pollution culprit #1 MECHANISM CONFIRMED (2026-08-25, VS) - fix proposed, not applied (WH's lane)

Root cause: CampaignStageBattleConfigurationTests' SpawnAndInitializeBootstrap runs 3x in a loop
per test, but its object-collection step uses a Find-returns-first-match pattern - so only the
FIRST loop iteration's Canvas/EventSystem/CardDatabase/BattleController get tracked for teardown;
iterations 2 and 3 leak, uncollected and undestroyed. This is what produces the y:0.00 vs y:-45.20
mismatch seen in every run.

VS self-corrected an earlier wrong ruling-out of this exact theory: it had counted call-site text
occurrences (1) instead of actual loop executions (3), disproving a correct theory with a bad
measurement and reporting it as fact. Flagged as the 4th silent-measurement failure of this shape
today (zero-match filter, last-filter-wins drop, header-bounded mailbox watcher, now this) - a
check that looks authoritative while measuring the wrong thing. Worth institutional awareness, not
a one-off mistake to forget.

PROPOSED FIX (not applied - file is campaign content, WH's lane per parallel-work protocol Rule 1):
collect every matching root object per spawned-name, not just the first, inside
SpawnAndInitializeBootstrap. Same GameObject.Find-first-match pattern exists in ~58 fixtures
project-wide; only this one currently loops so only this one leaks today, but any fixture later
wrapped in a loop reintroduces this silently. A shared test helper across all 58 is a real,
bigger question - not decided here, flagged for a future pass, not urgent tonight.

Verification plan once applied: re-run CampaignStageBattleConfigurationTests +
RarityFrameRenderingTests + TutorialTeachingOverlayTests, expect 28/28, ~30s.

SECOND LEAKER (RarityFrame's Card_warrior null) still open and separate - VS has released the
lock and is ready to bisect it next on request.

## Acquisition channel wiring COMPLETE — 35/36 reachable, self-verifying (2026-08-25, CR, 64966fb)

275/275 clean (one transient Unity-process crash on SpellCatalogPhase4Tests during a contended
run, reproduced clean 14/14 isolated - correctly identified as not a regression, not hidden).
Ashfall/Stormchain wired as real Stage rules (4-30/6-30); Windstep/Seismic Swap wired as real
AvatarLevel rules (18/24), correctly NOT routed through SpellBookGrant per GPT's spec. New audit
test checks the full 36-spell catalog against LIVE Rules/SpellBookGrant membership rather than a
hand-maintained list - a future spell added without a real channel gets caught automatically, not
just today's 36.

**Spell system is now genuinely complete: 36/36 implemented, 35/36 reachable (Aegis Return
permanent exception), 6-slot progressive loadout, self-verifying acquisition audit.** Only
remaining real spell-system work: the mandated AI matrix remeasurement (greenlit below), and the
5/6-slot picker UI + Reposition tap UI (both queued, both need Play Mode verification).

## SYSTEMIC BUG: shared helper pattern, actively spreading into new files (2026-08-25, VS) - URGENT

Second leaker found and it's the SAME mechanism as culprit #1 (Find-returns-first-match on a
looped SpawnAndInitializeBootstrap call), just a different fixture. This is not two separate bugs
- it's ONE bug pattern in a copied helper, appearing wherever the helper is called inside a loop.

**Heuristic blast-radius scan (indentation-based, NOT proven, treat as "worth checking" not
"confirmed leaking"): 13 fixtures flagged**, including CampaignStageBattleConfigurationTests +
Chapter1CombatBalanceAuditTests (2 sites, both confirmed) + Chapter2 through Chapter12FullDepthTests.
**Chapter11 and Chapter12FullDepthTests are on this list and are BRAND NEW, written by WH
TONIGHT** - meaning the bug pattern is actively propagating into fresh code as chapters are built,
not just sitting in old files.

**REVISED FIX RECOMMENDATION: patch the shared helper's collection logic once (collect every
GetRootGameObjects match, not just the first), not each fixture individually.** Patching call
sites one at a time as each is separately discovered will always be behind new chapters being
written. WH needs this fix in the helper before Chapter 13+ get written, or the leak just
continues propagating.

GameBootstrap.Instance remains exonerated - no frozen member involved, no owner sign-off needed
for the actual fix, just implementation.

Verification plan once applied: CampaignStageBattleConfigurationTests + RarityFrameRenderingTests
+ TutorialTeachingOverlayTests (expect 28/28), Chapter10FullDepthTests + RarityFrameRenderingTests
(expect 20/20), then one full suite for the real total.

## AI matrix remeasurement COMPLETE at final state (2026-08-25, CR) - 2 of 3 confirmed real, not drift

Measured against the true final state (36/36 catalog + all acquisition channels + 6-slot loadout),
no code changes, no retuning, per GPT's mandate.

**APPRENTICE: PASSES CLEAN.** Cast rate 43.4% [41.2,45.6] (band 25-70). Win-rate delta -7.9pp
(cap 8pp - inside by 0.1pp, tight but real). Spells/match 0.44 (floor 0.30). No-spell fallback
57.6%. maxSingleSpellWinShare 19.5%. The earlier "newly out of band" flag does not reproduce at
final state - was transient/mid-flight noise, not a real issue.

**NOVICE: STILL FAILING, confirmed real not drift.** Cast rate 21.5% [19.4,23.7] on 1374
opportunity trials (floor 25%, 3.5pp short) - roughly flat vs the earlier 22.7% mid-flight reading
(within noise). Spells/match 0.15 (floor 0.5, badly short - same root cause as the cast-rate
miss). Win-rate itself is fine (+2.1pp vs baseline).

**VETERANPLUS: STILL FAILING, confirmed real not drift.** Player win-rate delta -9.7pp (cap 8pp,
over by 1.7pp) - roughly flat vs the earlier -9.8pp. Cast rate itself is fine (45.4%, well inside
band) - this is purely a win-rate-concentration problem, not a frequency problem.

**Conclusion: both misses are stable, genuine underlying AI-heuristic issues (Novice cast-
frequency, VeteranPlus win-rate-concentration), NOT artifacts of the catalog being mid-flight
incomplete during earlier measurements.** Escalated to GPT per the standing mandate - not
retuned, EffectPriority/gate values untouched.

## Systemic Canvas-leak fix SHIPPED, verified 28/28 (2026-08-25, WH, HEAD a43f692)

SpawnAndInitializeBootstrap now collects every matching root Canvas/EventSystem/CardDatabase/
BattleController via GetRootGameObjects, not just Find's first hit. Verified: minimal repro
(CampaignStageBattleConfigurationTests + RarityFrameRenderingTests + TutorialTeachingOverlayTests)
28/28, 0 error CS. This is a real, root-cause fix (not a workaround) for the pollution mystery
chased across most of tonight.

**Not yet verified:** the 11 other heuristically-flagged fixtures (Chapter1CombatBalanceAudit's
2nd site, Chapter2-12FullDepthTests) - whether the fix applies where it's the SAME helper (likely
already covered if it's one shared function) or whether any of those call sites have their own
separate copy needing the same fix. Also pending: a full-suite run for the real overall number,
since only the 3-class minimal repro has been confirmed so far.

## POLLUTION MYSTERY DEFINITIVELY CLOSED (2026-08-25, WH, HEAD 91a3813)

Not one shared helper - each fixture had its own copy-pasted SpawnAndInitializeBootstrap (or
equivalent). Fixed the root-object-collection pattern across ALL 59 affected files (every
Chapter1-12FullDepthTests, Chapter1CombatBalanceAuditTests, CampaignStageBattleConfigurationTests,
~45 others). Zero remaining GameObject.Find(spawnedName|name) teardown sites project-wide.

**Full continuous EditMode run, HEAD 91a3813, 0 error CS: 1222/1231 (9 failed).** The original
order-dependent trio (RarityFrame Card_warrior null, TutorialTeachingOverlay x2) DID NOT FAIL in
this run - proof, not inference, the fix holds under the exact conditions that broke it all night.

Remaining 9 failures, all different category, none Canvas/EventSystem leak class:
AIEnemySpellbookResolverTests x4, MirroredAiSimulationMatrixTests x3 (known/parked AI-balance,
already escalated to GPT), SpellLoadoutTests x1, BalanceSimulationTests x1. The 6 non-
MirroredAiSimulationMatrix failures are NEW and need investigation - likely fallout from CR's
loadout-expansion/acquisition-channel work, not yet triaged.

## Pollution fix INDEPENDENTLY VERIFIED (2026-08-25, VS, HEAD 0ca542b) - thread closed

VS ran its own verification rather than trusting the fix commit message - the exact 4-class
configuration (CampaignStageBattleConfigurationTests + Chapter10FullDepthTests +
RarityFrameRenderingTests + TutorialTeachingOverlayTests) that reliably reproduced 3 failures all
session. Result: 39/39, 0 failed, 0 error CS, HEAD stable both ends. Confirmed the 59-file
systemic fix holds under the exact conditions that broke it.

**Institutional finding worth carrying forward beyond tonight:** every wrong turn in this
investigation had the same shape - a check that looked authoritative while measuring the wrong
thing (zero-match filter reading as success, last-filter-wins silently dropping filters, a
header-bounded watcher, a textual call-site count standing in for actual loop executions). The fix
each time was isolating first and confirming by minimal reproduction rather than trusting a
plausible-looking result.

## Owner sign-off: Memory Expedition PlayerProfile fields APPROVED (2026-08-25)

Owner approved the 12 additive fields for Memory Expedition's save state: memoryExpeditionDayKey
(string), Seed (int), RulesVersion (int), CurrentRound (int), RevealedPairMask (long),
FirstSelectedTile (int), MistakesRemaining (int), HighestRoundCleared (int), RewardClaimed (bool),
RunFailed (bool), temporaryResearchPoints (int), temporaryResearchExpiryDayKey (string). VS is
clear to wire MemoryExpeditionState into PlayerProfile.cs (frozen-file addition, additive only,
same pattern as the already-approved Daily Login fields).

## Ch4-7 narrative upgrade — LOCKED (2026-08-25, GPT)

Upgrade required pre-beta (not internal-testing-only): 12 bespoke beats total across Ch4-7 (opener/
midpoint/finale/post-finale hook x4 chapters), matching Ch8-11's treatment shape. Ordinary stages
keep the existing lightweight template. Explicitly classified as required pre-beta content debt,
NOT an MVP systems blocker - real, but not urgent tonight. Queued behind WH's current Ch12/13 work.

## Play Mode capability MEASURED, not assumed (2026-08-25, VS) - real, nuanced answer

Added additive -TestPlatform param to tools/run_editmode_tests.ps1 (defaults EditMode, existing
invocations unchanged) and ran a real probe. First attempt hit a transient compile break -
Chapter13FullDepthTests.cs was mid-write by WH (still declared class Chapter12FullDepthTests,
copy-paste not yet renamed), correctly identified as not VS's to fix, self-resolved 44s later.
Second probe, clean: **Play Mode DOES execute headlessly here** (2 tests ran, 0 compile errors,
HEAD stable). But both failed on the actual raycast - GraphicRaycaster returns zero hits without a
rendered frame, a known limitation already anticipated in the test file's own bail-out string.

**Real verdict: Play Mode runs, but pointer/raycast/tap verification does not work headlessly in
this environment.** So: VS can own Reposition's targeting LOGIC (legality, state machine,
selection rules - plain testable C#, same pattern as RepositionSelectionState/Memory Expedition),
but the actual tap-UI wiring needs a session with an interactive Editor to verify the one thing
that matters (a real tap reaching the right handler). Assigning the full task to a headless
session would produce code that looks verified while its central claim is untested - correctly
declined rather than taken.

**New coordination-rule gap found, worth adding to the standing rules:** untracked files being
actively authored are invisible to .unity_batch.lock (which only guards Unity process access, not
file-write-in-progress state). Third time today a mid-write file broke another seat's run. No fix
proposed yet, just flagged as a real gap in the parallel-work protocol.

## Tutorial overflow mystery SOLVED: was a pollution artifact, real bug is cosmetic (2026-08-25, VS)

Real measurement, diagnostic test deleted after use: actual overflow is exactly 10.2px per side
(196-175.6=20.4, centered), matching VS's ORIGINAL arithmetic prediction. **The earlier 35px
figure was corrupted by the still-live Canvas pollution bug at measurement time** - ~25px of it
was stale leaked canvas artifacts, not real overflow. The pollution bug silently poisoned this
unrelated investigation for hours - worth remembering as a case study in why order-dependent state
leaks are dangerous beyond their obvious symptoms.

Confirmed: belowCanvas = -20.80 (negative = inside canvas). Nothing is off-screen, no tap target
lost, no input blocked. The earlier "unreachable tap target, MVP gate row 1" claim is confirmed
WRONG, not just unproven. Real severity: minor cosmetic bleed (~10px card overlap into neighboring
UI), not a gate issue.

The originally-reverted GameBootstrap.cs fix (derive card height from real HandPanelMin/Max
constants instead of stale hardcoded 196) is confirmed CORRECT IN SUBSTANCE - it was reverted for
a false severity justification, which was the right call on the info available at the time. Now
correctly re-requesting approval on accurate severity rather than re-applying unilaterally.

## Visual direction: 2.5D presentation on 2D assets — LOCKED (2026-08-25, GPT)

Confirmed: dimension doesn't define quality bar - consistency, timing, audio, effects, and
production polish do (real precedent: Hearthstone, Marvel SNAP both premium 2D card games).
Direction: illustrated 2D cards/portraits stay the visual foundation; layered parallax for depth;
particles/glow/distortion/lighting/camera impact sell power; flipbooks for spell motion; SELECTIVE
skeletal animation only for recurring avatars/villains that need reusable body deformation, NOT
"every asset must be Spine." Unity-native sprites/flipbooks/particles/tweens are the efficient
foundation given the game is dominated by short combat events. This directly extends the already-
locked "Spell Visual Identity Phase-1" 3-layer model (school palette/shape/motion -> effect-type
shape -> per-spell signature beat) - compatible, not conflicting.

**Real execution plan, in order:**
1. Build ONE polished vertical slice: one card attack, one damage spell, one heal/buff spell, one
   AvatarStrike (which already has its own locked 4-beat Commit->Lock->Release->Consequence
   sequence).
2. Lock concrete parameters from that slice: timing budget, camera language, audio hit points,
   particle style, skip/fast-forward behavior - real numbers, not vague "polish."
3. Apply that locked language consistently across the remaining 32 spells/cards.
4. Cinematic/trailer production comes AFTER the in-game visual identity is stable, not before -
   this also resolves the earlier open question (in-game animation before opening scene).

Hardcore-player constraint carried over: every animation must be fast-forwardable, strategic lane
state stays visible during effects, no animation delays the next meaningful decision.

**Real gap: this needs an actual asset-production/VFX resource, not just coding seats.** CR/VS
handle spell LOGIC; none of tonight's seats have confirmed art/animation production capability.
This is a new workstream, not something to fold into the current CR/VS/WH coding queues blind.

## Vertical-slice parameter spec, DRAFT for GPT collaboration (2026-08-25)

4 targets: AvatarStrike, Firestorm (LaneDamage), Renewal (Heal), one basic card attack (generic
melee clash, no spell). Draft parameters below - meant as a starting point for GPT to refine, not
final numbers.

**Timing budget (per beat, all fast-forwardable on tap):**
- Basic card attack: Commit 150ms, Impact 100ms, Resolve 150ms = ~400ms total.
- Damage/Heal spell (Firestorm/Renewal): Cast 200ms, Travel/Channel 250ms, Impact 150ms,
  Resolve 200ms = ~800ms total.
- AvatarStrike (its own locked 4-beat sequence): Commit 300ms, Lock 200ms, Release 150ms,
  Consequence 350ms = ~1000ms total - deliberately longest, it's the commitment spell.

**Camera language:**
- Basic attack: no camera move, impact micro-shake only (2-4px, 80ms).
- Damage/Heal spell: slight zoom toward target lane (015-1.08x scale), hold through Impact.
- AvatarStrike: full camera reticle on the Avatar panel per its existing "never targets a lane"
  rule - punch-in zoom (1.15x), stronger shake on Release (6-8px, 120ms).

**Audio hit points:** one SFX cue per beat minimum - Cast/Commit whoosh, Impact hit/thud, Resolve
chime (positive) or low tone (negative). AvatarStrike gets a distinct signature stinger on Release,
not a reused generic hit sound - it's the one spell explicitly designed to feel different.

**Particle style:** school-palette-driven per the already-locked 3-layer model (Andras=ember/
crimson, Ktini=jade/earthen, Pnevmas=ivory/gold/cyan). Basic attack: no particles, just the sprite
clash. Spells: school-colored burst at Impact, sized to magnitude (small/medium/large tiers already
exist in the effect-magnitude data). AvatarStrike: bespoke, not reused from any effect-type
template, per the existing lock.

**Skip/fast-forward behavior:** a second tap/input during any beat immediately jumps to Resolve's
end state - no animation ever blocks the next decision. Lane state (HP/Attack numbers) stays
rendered and readable throughout, never obscured by an effect.

**Not yet decided, needs GPT/owner input:** exact particle system technology (Unity ParticleSystem
vs pre-rendered flipbook per effect), whether camera zoom needs a dedicated virtual camera rig or
can be a simple canvas-scale tween, and real SFX asset sourcing (none exists yet - separate
question from timing/direction).

## CORRECTION: pollution fix was NOT fully in 91a3813 for at least Chapter10FullDepthTests.cs

Direct check: `git show 91a3813:Assets/Tests/Editor/Chapter10FullDepthTests.cs` still contains the
OLD broken GameObject.Find(spawnedName) pattern - the fix was never actually in that commit for
this file, despite the commit message claiming "59-file systemic fix verified... zero remaining
Find(spawnedName|name) teardown sites." Either the earlier report overstated coverage, or a later
commit (adea127, CR's spell-catalog work, last real touch on this file per git log) silently
reverted it back via a bad merge/rebase carrying an old version along.

**Currently: WH's uncommitted working tree has the correct fix re-applied to this file (and likely
others in the same batch) right now** - visible as unstaged diff, not yet committed. Not
committing this blind given the mixed, large, multi-seat dirty tree (CR's CloudCode/Bazaar files
are also unstaged in the same tree right now) - waiting for WH to commit its own real change set
cleanly rather than CC guessing which of ~65 modified files belong to which seat.

**Real lesson: "verified 39/39" by one seat at one HEAD does not mean the fix is permanent** - a
later commit can silently regress files nobody's actively watching. Don't declare a fix
permanently closed off one verification; the register's "CLOSED" framing on 0ca542b was too
strong. Downgrading language: fix confirmed real and effective as of that HEAD, but fragility
demonstrated - a full-suite check is needed after ANY commit that touches these files, not a
one-time closure.

## Vertical-slice spec REFINED and LOCKED (2026-08-25, GPT) - vetted, one error corrected

**Vetting note (CC caught before locking, GPT confirmed the correction 2026-08-25):** GPT's reply
claimed the draft's "400ms AvatarStrike estimate" was wrong and corrected it to 1000ms. This was a
false correction - the draft never had AvatarStrike at 400ms; 400ms was the basic-attack figure,
and AvatarStrike was already correctly 1000ms (300+200+150+350). GPT cross-attributed two
different targets' numbers. **GPT has since acknowledged this cleanly: AvatarStrike remained
capped at 1000ms all along, no correction was needed there.** The real, valuable new content from
that round was the skip-handling/idempotent-resolve safety gap - everything else checked out and
is locked below. Implementation cleared to start.

**Refined timing:** basic attack 350-400ms (unchanged). Firestorm/Renewal reduced to 600-700ms
(was 800ms - repeat-cast fatigue). AvatarStrike stays 800-1000ms, hard ceiling 1 second.

**Camera:** no virtual-camera system for this slice - use a SCOPED PRESENTATION-ROOT TWEEN, never
scale the full Canvas (HUD/resource text must stay stable across device sizes). Basic attack: no
zoom, 2-4 normalized shake units. Ordinary spell: 1.04-1.06x zoom. AvatarStrike: 1.08-1.12x zoom
(not 1.15x - real clipping risk on varied 16:9 devices).

**Particles:** hybrid confirmed - ParticleSystem for embers/dust/sparks/smoke/aura/trails,
flipbook sheets for authored spell silhouettes/impact moments, simple sprites/tweens for basic
attacks/small heals. No Spine, no new runtime dependency. Size by visual tier (light/medium/heavy),
NOT directly from raw damage magnitude - balance tuning must never force visual reauthoring.

**Audio:** reduced cue count. Basic attack: impact only. Ordinary spell: cast+impact, optional
soft resolve cue for heal/buff only. AvatarStrike: commit + release-impact + bespoke signature
stinger. Base vocabulary from a licensed/commissioned library; AI-generated audio OK for temporary
exploration only, not final source until licensing/consistency/looping/mix quality verified.

**Skip behavior, TIGHTENED (real gap CC's draft missed):** skip triggers only via a dedicated
skip control or tapping a non-interactive battle area - NOT any second tap, which would risk
accidentally skipping via a card/lane/rail/button interaction. Resolve state must be IDEMPOTENT -
skipping can never duplicate damage, healing, SFX, or rewards. Next decision available immediately
after resolve applies.

**Readability, additions locked:** no particle may permanently cover lane occupancy; damage/heal
numbers render above effects always; AvatarStrike may briefly dim the board but must preserve
Avatar HP result + target identity; every effect must be readable with sound disabled; test at
both normal and fast-forward speed.

**Acceptance test:** run 20+ consecutive Firestorm/Renewal casts plus several basic attacks - if
the animation becomes more noticeable than the tactical result, timing is still too heavy.

Open questions from the draft (particle tech, camera rig, SFX sourcing) are now answered above.

## Friends/Chat text-input widgets SHIPPED (2026-08-25, CR, 78758e7)

Closes the gap CR itself flagged in bb74809. UISharedFoundation.CreateInputField - new shared
helper generalizing CollectionPresenter's existing InputField pattern (the only prior real usage).
FriendsPresenter: real "Account id..." field + ADD FRIEND button wired to
IFriendsGateway.AddFriendAsync, clears+refreshes roster on success. ChatSocialPresenter: real
composer InputField replacing the old fixed "gg" placeholder text, SEND posts whatever's typed,
clears after success. Verified CR staged only its own 5 files - CombatPresentation.cs/
CombatPresentationTests.cs correctly left untouched (VS's active work).

CR's queue now empty except the live server validation, blocked on VS's compile fix.

## CR self-review during lock wait, real gap found and documented (2026-08-25, 94dc514)

RemoveFriendAsync is the only mutating Friends operation without optimistic-lock retry (deletes
carry no WriteLock, so there's no conflict to retry against) - a concurrent Accept landing between
load and delete could get silently discarded. Low severity, same accepted-scope class as Bazaar's
own documented "no true cross-entity atomicity" limitation elsewhere. Documented explicitly via
doc comment rather than left implicit, no behavior change. 21/21 still clean.

## CORRECTION: CC misattributed a mid-write break to 090b146 - that commit was fine (2026-08-25, VS)

CC's earlier "confirmed real" finding on FinalBeatFor/CueResolveChime missing was itself a
measurement error - CC inspected the live WORKING TREE, not the commit. VS proved via
`git show 090b146:<file>` that every symbol the test used was defined in that commit; it was
internally consistent and its 15/15 was real. What CR actually hit was VS's OWN subsequent
mid-rebuild working tree (rewriting CombatPresentation.cs first, tests second, non-atomically,
after the locked spec landed) - a genuine ~90-second inconsistent-state window, unrelated to
090b146. CC's symptom report (FinalBeatFor missing) was accurate at the moment checked; the
attribution to a specific commit was wrong.

**VS's own honest accounting: this is the 5th mid-write tree break of the session, and the first
one VS itself caused** (the other 4 were reported as other seats' doing: Ch11/12, Ch13,
MemoryExpeditionShellTests, and the break that killed VS's own PlayMode probe). Same standard
applied to itself. Fix adopted: write ALL interdependent files before running or ending a turn -
never leave a half-updated API visible mid-rewrite. This extends c52cd0d (which covers holding
edits during another seat's LOCK) to a related but distinct hole: an inconsistent intermediate
state from your OWN non-atomic multi-file rewrite, not just another seat's untracked file.

**Real lesson for CC specifically: verify against `git show <commit>:<file>`, not the live working
tree, when attributing a break to a specific commit** - the working tree reflects whatever's
happening right now, which may be several commits and several seats past the one being checked.

TREE NOW CLEAN: HEAD 94dc514, 0 error CS, 119/119 (CombatPresentationTests 26/26 +
BattleLogicTests 92/92). CR unblocked, resuming live server verification.

## REAL VISUAL AUDIT — owner's first direct Play Mode inspection, 2026-08-25 - major findings

First genuine human visual pass across ~18 screens. Findings no headless test could catch:

**P0 bugs, real, visible to any player right now:**
1. Bazaar screen renders a raw DEV COMMENT as live UI text: "Browse catalog OPEN - BazaarGateway
   has wallet/list/buy/cancel only (no Query/Listings)" - a debug/status string leaked into
   production UI.
2. `[runtime]` placeholder strings visible live on Battle Pass (timer, tier costs) and Daily
   Login/Quests (quest descriptions, some progress values) - unresolved template tokens shipping
   to the player.
3. Overlapping/garbled text on Campaign map header and Formation screen header - two text blocks
   rendering on top of each other.
4. Solid orange/debug-color block covering a panel on the stage-detail popup - looks like a
   missing/broken texture reference.

**Real functional discrepancy:** Empire building screen shows "Upgrade charges Gold and finishes
instantly - then Collect" - contradicts the LOCKED design (real 30min-14day construction timer
curve, `EMPIRE_SCHEMA_LOCK`). Either never wired to real timers, or quietly simplified without
being logged anywhere. Needs investigation - is this an intentional stub or a real regression from
the locked spec.

**Pervasive but already-known:** missing art assets (blank/gray placeholder boxes) across nearly
every screen - confirms the animation/art pipeline gap discussed earlier is more visually severe
than estimated from code alone.

**Real open design question the owner raised:** no PvP/opponent-finding system exists at all - no
world map, no base placement, no coordinate-based search, no troop deployment. Owner explicitly
wants to AVOID the Clash-of-Clans-style world-map/base-raid model that's now industry-default for
base-builders, and asked for a genre-appropriate alternative given this is a CARD BATTLER, not a
base-builder. CC recommendation: async ladder matchmaking against a saved defense-formation
snapshot (Clash Royale's actual model, not CoC's) - fits the existing Season XP/Battle Pass system
already locked, avoids building an entire new world-map subsystem, and matches real genre
precedent for card battlers specifically. Guild-vs-guild content should be cooperative
boss/event-damage-contribution (extends the already-shipped Guild Expedition), not territory war -
no map needed for either. Real, substantial new system - needs its own GPT/owner design round
before any building starts, not started tonight.

## P0 bug 1 FIXED: Bazaar dev-comment leak (2026-08-25, CR)

Root cause: BazaarPresenter.cs:181's details panel default text was stale dev-status text left in
place when SelectTab's Browse case was switched to async RefreshBrowseAsync() - visible for the
duration of the first Cloud Code round-trip on every screen open, before the real data overwrote
it. Fixed: default text now "Loading...". Checked Friends/Chat (also CR's) for the same pattern -
clean. Not run-verified (Editor open), but low-risk one-line string swap.

Bug 2 (orange block) investigated, NOT fixed, correctly left for whoever owns
CampaignMapUiLibrary.cs/CampaignMapPresenter.cs. Leading candidate:
CampaignMapUiLibrary.cs:107-111's Playable-node fallback color (0.85, 0.65, 0.2) fires only if
Resources.Load<Sprite> for campaign_stage_node_playable_v1 returns null - but the asset exists and
is correctly imported, contradicting the fallback firing. Also possible the map's node is bleeding
through the stage-detail modal's semi-transparent dim backdrop rather than being drawn inside the
modal itself. Needs owner to confirm exact screen location (behind the popup vs inside it) before
anyone chases the wrong path.

## Ch4-7 narrative upgrade SHIPPED (2026-08-25, WH, HEAD dc5f00f, approved to commit)

12 bespoke beats across Ch4-7 (N-1/N-15/N-30 pre, N-30_post + Unknown Voice hook), matching Ch10/11
shape, ordinary stages stay templated. Verified: Chapter4To7NarrativeUpgradeTests 1/1,
StoryDatabase_DefinesPreAndPostVictorySequencesForAllThirtyNewStages 12/12, 0 error CS. This closes
the last remaining narrative content debt.

## CRITICAL: project-level Access Control gap blocks ALL Custom Items writes from Cloud Code (2026-08-25, CR)

Real root cause found, bigger than Friends/Chat: EVERY Custom Items write from Cloud Code
(SetCustomItemAsync) in this project returns 401 Unauthorized. Reads (GetCustomItemsAsync) work.
Player-scoped writes (SetItemAsync - wallets, SocialSafety block/mute) work. Only Custom Items
writes are blocked - project-level Access Control / Access Class configuration gap, not a bug in
any specific module.

**CORRECTION: the earlier "Bazaar confirmed live-working" claim (register, CloudCode Modules
Deployed entry) never actually exercised a Custom Items write.** ListBazaarItem/BuyBazaarItem/
CancelBazaarListing would fail identically the instant they reached a real save - previous testing
always short-circuited on INSTANCE_NOT_FOUND first, masking this. Bazaar's live-verified status
needs re-qualifying once this is fixed.

CR could not fix or even inspect the access policy - `ugs access get-project-policy` returns 403
Forbidden for the service account, same wall as observability logs earlier. Diagnostic code was
added temporarily, then fully cleaned up (42/42 ServerTests clean, zero net diff, confirmed removed
from the live endpoint).

**REQUIRES OWNER ACTION - only the Unity Dashboard project owner can fix this:** Unity Dashboard >
Cloud Save > Access Control (or the Access Control service's project policy) needs Cloud Code
granted write access to Custom Items. Blocks: Chat message seeding/posting, Bazaar
list/buy/cancel, Friends graph writes - i.e. most of tonight's Option B server work is functionally
inert until this is fixed, despite deploying and compiling cleanly.

## CORRECTION: Bazaar/Chat sky-blue isn't a missing-theme bug - real mechanism found (2026-08-25, VS)

CC's earlier diagnosis ("unstyled default Image color") was wrong - Unity's default Image color is
white, not sky-blue, and both screens already build a correctly-themed dark fullscreen background
(BazaarPresenter:76-79, ChatSocialPresenter:72, ApplyFullscreenShell with the right dark color).

REAL MECHANISM: the sky-blue is Unity's default CAMERA clear color (0.19, 0.30, 0.47) showing
through - nothing in the codebase sets clearFlags/backgroundColor anywhere, and no camera exists in
code at all (procedural-UI codebase, no scenes/prefabs). ApplyFullscreenShell sets preserveAspect
on the art shells, which are authored at exactly 16:9 (1672x941) - on a 16:9 viewport they cover
fully, on ANY other aspect ratio preserveAspect letterboxes them and exposes the camera behind. Side
panels look correct only because they use plain solid-color fills with no preserveAspect, not
because main panels lack theming.

Three candidate fixes, correctly not chosen blind (VS cannot visually verify either):
(a) set camera clear color to theme navy - fixes every screen at once, but no camera exists in code,
    needs a scene/prefab change this procedural codebase doesn't use;
(b) drop preserveAspect - cheap, but distorts authored art on non-16:9;
(c) add an opaque dark backing Image behind the shell sprite - undistorted art AND kills the blue on
    every aspect (VS's preference).
Routed to WH (real visual iteration capability, owner assisting) with full diagnosis attached -
same "logic I can assert, visual work belongs with a seat that can see the result" boundary VS
already established on Reposition tap-UI.

## All 5 P0 bugs from live visual audit FIXED (2026-08-25, WH) - real, verified, uncommitted

1. Empire timer: root cause confirmed - was never wired at all (TryStart jumped straight to
   ReadyToCollect, no timer fields, no tick path). Now real: Building status + startedUtcMs/
   endsAtUtcMs + Materials cost on start, real locked 30min-14d curve (EmpireConstructionTimer.cs,
   new file), AdvanceIfDue on refresh/claim. EmpireConstruction* 19/19.
2. Campaign map header overlap: 3 centered boxes at same Y replaced with stacked anchor bands.
3. Battle Pass [runtime]: was actually showing "OPEN" via OpenAmountLabel, not [runtime] literally
   - now binds real passSeasonXp. Daily Login was already live (real quest data), false alarm on
   that half.
4. Orange block: confirmed VS's bleed-through theory - modal panel was 40% alpha, playable-node
   fallback orange showed through. Panel bg -> 98% opaque.
5. Formation header: two-line layout (Campaign/Stage + Formation), guidance caption no longer
   duplicates the mode line.

Approved for split commits (5 logically distinct fixes, easier individual review/revert).

## Vertical-slice asset spec FULLY LOCKED (2026-08-25, GPT) - ready for production

Audio: 6 WAV files at Assets/Art/Audio/, filenames = exact cue IDs (combat.commit, combat.cast,
combat.impact, combat.resolve.soft, avatarstrike.release.impact, avatarstrike.release.stinger).
Stinger stays exclusive to AvatarStrike, plays alongside (not instead of) the impact cue.
Particles: 3 transparent PNGs at Assets/Art/VFX/ (Medium/Heavy Particle System textures,
AvatarStrike bespoke flipbook sheet), format/dimensions locked earlier.

Nothing left to decide - this is now purely a production task (generate the 9 files, drop into the
folders, VS's binding layer picks them up automatically).

## SERVER ACCESS BUG FULLY FIXED AND VERIFIED (2026-08-25, CR, 27a8fd1) - real, live, end-to-end

Root cause confirmed: context.AccessToken (player-scoped) was used where context.ServiceToken
(elevated write rights) was needed, on all 3 real Custom Items write call sites - Bazaar's
SaveCustomItemAsync, Friends' SaveFriendshipAsync/DeleteFriendshipAsync, Chat's SaveChannelAsync.
Read calls correctly left on AccessToken (matches the documented "readable by any player, writeable
only from a server" design). Verified incrementally, not broadly assumed - fixed Friends alone
first, confirmed real success, THEN applied to all 3.

Full live re-verification, real numbers: Bazaar 8/8, Chat 6/6, Friends 7/7 - 21/21, full post/
fetch/limit round trips genuinely working, not just validation paths. This closes out the entire
CloudCode Option B track for real (previous "live-working" claims for Bazaar were corrected earlier
tonight as never having exercised a real write - this is the first time it's actually proven).

Chat seed data: done, 4 real messages posted into Global via actual PostChatMessage calls, seeding
script deleted after (ops tool, not a test, correctly not committed).

Bazaar seed data: correctly NOT done - separate, pre-existing gap (no endpoint exists to create an
ItemInstance to list, documented in Bazaar's own README). CR correctly declined to fabricate fake
data to work around this; real fix needs a Collection-system endpoint, out of CR's scope.

## Chapter 15 SHIPPED, all P0 split commits landed (2026-08-25, WH)

P0 fixes committed individually: 7b419ed (Empire timer), 122bbe7 (Campaign header overlap),
92c8b54 (Battle Pass real Season XP), e57aa02 (opaque stage-detail panel), d6706a5 (Formation
header vs caption). All 5 real bugs from the live visual audit now closed and committed.

Chapter 15 "The Godless Dawn" (bc68e5d): 15-1..15-30, Gate L30 clears through Ch15, 14-30 chains
to 15-1, AF retuned (15-4/15-7/15-30). Chapter15FullDepthTests 11/11, GateRouteTests 4/4, 0
error CS. Campaign is now 423 stages total. Ch16-30 remain the milestone gap.

## CORRECTION: [runtime] is deliberate, not a bug - real decision made (2026-08-25, VS)

CC's earlier bug report misattributed the [runtime] sighting to Battle Pass/Daily Login (both have
zero consumers of RuntimePlaceholder, editing them changes nothing). The only screen that actually
renders it is EmpireBuildingDetailCopy.cs (4 spots: duration line, v2 payoff line, 2 "LEVEL
[runtime]" cases). Confirmed deliberate: EmpireBuildingDetailShellTests explicitly asserts the
placeholder displays, with a doc comment explaining it marks genuinely-undecided design values
(build duration, v2 persist, Castle interlock table) rather than inventing numbers.

**Decision: option (a), partial.** Build duration is now real (EmpireConstructionTimer.cs shipped
this session, real 30min-14d curve) - wire it to replace that placeholder. The other two
(v2-level-persistence field, Castle interlock table) remain genuinely open - the interlock table
specifically is the exact thing pending GPT's answer on the remaining 9 buildings, sent earlier
tonight. Those two stay [runtime] legitimately until those decisions land; update
EmpireBuildingDetailShellTests to assert duration resolves to a real value while the other two
still assert the placeholder.

## PvP/opponent-finding system - LOCKED FINAL (2026-08-25, GPT, corrected)

GPT accepted CC's critique and corrected its own earlier recommendation: the Pokemon TCG Pocket
citation supported "no map needed," not "add a map layer" - that reasoning was weak and is
withdrawn. FINAL Phase 1 decision: async ladder matchmaking ONLY. Battle Rating, seasonal rank,
defense snapshots, fair collection/skill brackets - no PvP map layer. Campaign/Empire maps carry
world identity; no second PvP-specific map. Anti-fatigue via existing/planned short loops (Memory
Expedition, Guild Expedition, campaign farming, daily objectives, seasonal activities), not map
decoration. Synchronous PvP reserved for scheduled guild/special events only. A PvP map is
explicitly deferred to Phase 2, contingent on real player retention data showing the ladder alone
feels repetitive - not built speculatively.

This is now closed - no further design round needed on this topic unless real post-launch data
reopens it.

## CORRECTION: Empire Duration [runtime] already resolved, no wiring needed (2026-08-25, VS)

The prior entry's "wire the real build duration" instruction was already stale by the time it was
sent - WH's 7b419ed (Empire construction timer fix) landed in between VS's original report and
CC's decision, and already resolved it. VS correctly checked before implementing rather than
duplicating finished work. Only v2-persist and the Castle interlock table remain genuinely open
(interlock table parked per owner request, not being pursued right now).

VS's queue is genuinely empty - standing by, not manufacturing work.

## Building interlock: real audit required before any Castle-pair numbers (2026-08-25, GPT, corrected)

GPT withdrew the premature Curve C/Curve U proposal - it copied Barracks' 1/5/10/15/20/25/30 shape
onto 6 other buildings without verifying any of them actually have that internal milestone shape.
Correct methodology, locked: audit each building's REAL progression data before deriving any
interlock pair, don't assume a shared pattern.

**Real task queued for CR:** audit all 11 buildings, recording for each: (1) internal level shape -
continuous 1-30, milestone-only, or banded; (2) functional breakpoints - which levels actually
change capacity/output/research/charges/recruitment/evolution; (3) Materials/Gold costs and timer
bands; (4) construction-slot interaction; (5) server-dependency status. Every field marked as one
of: confirmed from live code, confirmed from locked documentation, design-only, or unknown/needs
owner decision. Starting categories (Combat/progression: Barracks/Training Grounds/Tree of
Knowledge; Economy/utility: Storage/Quarry/Academy/Embassy; Flat: Guild Hall/Prison) are hypotheses
to verify, not conclusions - a building moves category if evidence says otherwise.

Real constraints the audit must respect: Castle must not become a universal utility-freezing
bottleneck; Barracks/Training Grounds must not create an early combat bypass; Tree of Knowledge
must never lock/revoke already-shipped Evolution access; Storage must never cause resource loss at
capacity (pause, not delete); Quarry output must be checked against the total Materials faucet and
the locked 6-9mo core-spine target; Embassy's charge/reduction curve stays untouched, Castle only
gates the next band; Guild Hall/Prison can have entry gates without level ladders; server-dependent
functions stay unavailable regardless of Castle gating.

Only after this real evidence table is complete does a second GPT round derive actual Castle-pair
numbers per building - no numbers assigned blind again.

## MAJOR FINDING: only 3 of 11 buildings are actually implemented (2026-08-25, CR audit)

Real, code-verified audit (direct file reads + spot-verified Explore search), every field tagged
[LIVE]/[DOC]/[DESIGN]/[UNKNOWN]. Governing fact: `EmpireBuildingId` enum only has Castle/Barracks/
Gate - the v2 11-building roster (EmpireBuildingRoster.cs) is explicitly documented in its own code
comment as "not wired into BattleController/GameBootstrap/UI, does not touch PlayerProfile/Save."
The other 8 buildings CANNOT be started through the one real construction pipeline.

**Real status per building:**
- **Castle, Barracks, Gate: REAL** - continuous/milestone level shapes confirmed live, real cost
  tables, real timers, real Castle-interlocks for Barracks/Gate (Gate's register example
  Castle15->Gate13 is itself flagged stale by the code's own comment - don't treat as current).
- **Embassy: REAL FORMULA, DEAD CODE** - the 6-band charges/reduction curve is correct, pure-function
  code, but never called from anywhere (grepped project-wide, zero callers).
- **Storage, Training Grounds, Quarry, Academy, Tree of Knowledge, Prison: NO IMPLEMENTATION AT
  ALL.** No level field, no breakpoints, no passive production, no research system, no captive
  mechanic - just roster text and (for some) static UI copy strings. Their roster "Fully
  functional" ServerStatus tags are false as of current code - real discrepancy between the design
  doc and the actual codebase.
- **Guild Hall: REAL but flat** - no level ladder by design, its "function" is a real entry point
  into the already-deployed Guild Expedition CloudCode module (separate system, not gated by any
  Guild Hall level since none exists).

**Construction slot: single global slot for all 11**, not per-building - in practice only
Castle/Barracks/Gate ever compete for it today since nothing else can start.

**Real implication for the interlock question:** deriving Castle-pair numbers for the other 7
buildings isn't just "verify their shape first" - THE UNDERLYING BUILDING MECHANIC DOESN'T EXIST
YET for 7 of 11. An interlock number would have nothing real to gate. This is a genuinely bigger
scope question than originally framed: build the missing 7 buildings' actual mechanics (a real,
substantial feature each), or leave them as structure-locked placeholders and drop the
"11-building interlock" ambition down to the 3-4 buildings that actually function.

## AI-balance: diagnose before tuning - LOCKED (2026-08-25, GPT)

Two different failure shapes, two separate diagnostic plans, NO gate/spell changes until measured:

**NOVICE (cast rate 21.5% vs 25% floor, spells/match 0.15 vs 0.5 floor, win-rate fine at +2.1pp):**
this pattern (low frequency, healthy win-rate) suggests candidate scarcity, same shape as the
earlier Apprentice issue - not excessive restraint strength. Run the candidate diagnostic at
theoretical 100% ordinary-spell roll: ordinary legal-candidate ticks, AvatarStrike opportunities,
rejected-by-target/energy/cooldown counts, max theoretical spells/match and cast rate. Decision
rule: if the theoretical ceiling stays below 25%/0.5, lower Novice's floor/ceiling to the reachable
band (Apprentice precedent). If it clears both, keep bands and raise the gate modestly, rerun. Do
NOT loosen target legality or change spell power before this measurement.

**VETERANPLUS (cast rate 45.4% fine, win-rate delta -9.7pp over 8pp cap):** frequency is healthy,
this is impact concentration. Run per-spell paired attribution across the FULL completed catalogue
(casts/match, same-tick kills, Avatar damage, % of AI wins containing each spell, contribution
delta vs baseline, early-KO correlation) - the old 4-effect measurements are no longer authoritative
now that the AI has the full 36-spell catalogue and 6-slot loadouts. If AvatarStrike dominates: a
VeteranPlus-specific commitment throttle, ordinary spells untouched. If an ordinary spell dominates:
tier-specific restraint on ITS target eligibility, not a global cap change. Earlier pattern
implicated Stone Judgment but that must be re-measured, not assumed, against the completed catalog.

Real remeasurement note: current acceptance bands were locked against the old 4-effect AI priority
pool - not authoritative anymore post-36/36-catalog. Diagnose first in both cases.

## Two proactive cross-genre proposals, drafted for GPT (2026-08-25, CC-initiated)

Not user-sourced this round - real research + synthesis against two problems already found tonight
(no post-chapter replay depth; 7 of 11 buildings have zero implementation per CR's audit).

1. Roguelike branching-node replay mode (Slay the Spire pattern, real proven genre - the sequel is
   shipping in 2026). Completed chapters unlock a branching node-map run (Combat/Elite/Event/Rest
   nodes), player picks path, escalating difficulty, one-run stakes, reuses the EXISTING formation-
   combat engine untouched - no new combat system needed, just a map generator + node-reward table.
2. Idle/passive production for the 7 unimplemented buildings (Storage/Quarry/Training Grounds/
   Academy/Tree of Knowledge/Embassy/Prison) - proven low-cost-high-retention genre pattern
   (AFK Arena-style). Passive resource/XP generation while offline, claimed on return, scaled by
   building level - gives the dead roster entries a real purpose instead of leaving them as
   unimplemented placeholders indefinitely.

Sent to GPT for real critique/refinement, not accepted at face value.

## Novice: candidate-ceiling confirmed unreachable, VeteranPlus: nuanced split found (2026-08-25, CR)

**NOVICE, decisive:** even at a forced 100% ordinary-spell roll (2000 trials), theoretical ceiling
is castRate=2.55%, spellsPerMatch=0.166 - both WELL below the 25%/0.5 floor. 16.4% of trials never
had even one legal ordinary cast opportunity in the whole match. Confirmed candidate scarcity, same
class as Apprentice. Per GPT's own decision rule: lower Novice's floor/ceiling to the reachable
band - raising the gate cannot close this gap since the ceiling itself is the bottleneck.

**VETERANPLUS, real but not clean:** per-spell attribution (1500 paired trials) shows a
volume-vs-precision split, not one dominant spell: Fault Line (479 casts, 16.6% win-rate-when-cast,
14.9% win-share - high volume, low predictive power) vs Stone Judgment (81 casts, 97.5%
win-rate-when-cast, 15.1% win-share - rare but near-perfectly correlated with winning, same total
win-share as Fault Line from 6x fewer casts). **NEW FINDING: 3 of 6 loadout slots (Renewal,
Cleansing Root, Oracle Sight) fired ZERO times across all 1500 trials** - Cleansing Root/Oracle
Sight show available=0 the entire run, never once legal - structurally dead loadout slots, not just
rarely used. Stone Judgment is re-implicated but CR correctly flagged the caveat: win-rate-when-cast
is correlational, not causal - a spell only available in already-favorable matches would show the
same pattern as one that causes the favorable outcome. A Stone-Judgment-removed ablation run would
disentangle this, not yet run.

Both results escalated to GPT - VeteranPlus doesn't cleanly match either of GPT's two proposed fix
branches (not clean AvatarStrike-dominance, not clean single-spell-dominance), needs a real decision
on how to read a volume/precision split plus dead loadout slots.

## Owner clarifications on GPT's building critique (2026-08-25)

Two real design intents not previously specified anywhere in the codebase or docs:

1. **Academy's research must be real player CHOICE, not passive/idle accumulation.** GPT's own
   answer already leaned this way ("Academy: only after a real research queue exists; passive
   temporary research points could be a support layer at most") - owner is locking this as the
   primary requirement: a real research system with meaningful player decisions (branching/
   choosing what to research), not a click-to-progress or auto-idle mechanic. Passive support
   layer, if any, comes after the real choice system exists, not instead of it.

2. **Prison's real function is capturing cards from PvP/other sources and converting them into
   the player's own deck** - this is new, was previously only "Captive/sacrifice placeholder,
   non-destructive" with no real mechanic ever defined. This reframes Prison entirely: not a
   server-dependent idle stand-in, but a real card-acquisition feature tied to a capture mechanic
   (likely tied to the async PvP ladder - captured from defeated opponents' decks?). Real design
   question for GPT: what exactly gets captured (an opponent's specific card? a random one?),
   under what conditions, and how does it convert to real ownership without breaking the "no
   Shop/drop/paid channel" spell-acquisition rules already locked elsewhere, or the equivalent
   card-acquisition integrity for the base card system.

## AI-balance: real decisions locked (2026-08-25, GPT) - Novice bands corrected, VeteranPlus needs ablation

**NOVICE: mathematically impossible original bands, CONFIRMED.** Revised acceptance bands (this is
correcting the TEST's own expectations to match Novice's intentionally-handicapped tier design, not
a gameplay change): cast rate 0-5%, spells/match 0.10-0.20, fallback ceiling 95%, win-rate delta cap
stays ±8pp unchanged. Candidate scarcity is structural, documented as intentional tier design, not
a bug to fix via gate changes.

**DEAD LOADOUT SLOTS: real, separate defect, fix BEFORE any VeteranPlus ablation study.** Renewal/
Cleansing Root/Oracle Sight fired zero times in 1500 trials, 2 never once legally targetable - a
6-slot AI loadout with half inert is not a meaningful test. New rule: every equipped AI spell must
have >=1 legal candidate in the tier's baseline simulation; if not, replace with an eligible spell.
Do NOT solve via gate changes or magnitude changes. Fix this, THEN rerun the full VeteranPlus
matrix on the corrected loadout - ablations run against a partially-dead loadout would be
misleading.

**VETERANPLUS: NOT YET PROVEN to be a Stone Judgment problem.** Fault Line (high volume, low
precision) and Stone Judgment (low volume, high precision) have near-identical win-share - two
different spell roles, not one confirmed dominant spell; Stone Judgment may just be legal only in
already-winning states (confounded correlation, not yet causal). Real ablation plan, AFTER loadout
fix: (1) current loadout baseline, (2) remove Stone Judgment only, (3) remove all AvatarStrike
spells, (4) replace Stone Judgment with a legal non-AvatarStrike spell. Compare player-win delta,
AI-win delta, early-KO rate, spells/match across all 4. Keep the 45% gate unchanged throughout.
Only after this evidence does anyone decide whether AvatarStrike or Stone Judgment needs a throttle.

**Real order: (1) Novice band correction, (2) dead-slot fix, (3) VeteranPlus matrix rerun on fixed
loadout, (4) 4-condition ablation study.**

## Academy + Prison real design LOCKED (2026-08-25, GPT, vetted not surface-accepted)

**ACADEMY:** genuine player-choice research, kept. NOT offline production, NOT a linear click-bar,
NOT a hidden combat-stat booster. Real branches (capacity support/construction planning/codex
access/Expedition utility) - never directly increases card Attack/HP, never bypasses campaign
gates. Passive research-point accrual can support this LATER, never replace the real choice system.

**PRISON:** real PvP-capture building, but full-card capture REJECTED for Phase 1 (real, specific
abuse vectors: new PvP acquisition route, alt-account farming, whale exploitation, collision with
the locked collection-acquisition-channel model). Locked version: "Bound Captive Fodder" -
server-authoritative, non-destructive, sacrifice-only. On an eligible async-ladder win, server picks
one card from the opponent's revealed defense formation snapshot; opponent keeps their original
(no loss); attacker gets a Fodder item with no combat stats, cannot be equipped, doesn't count
toward collection, cannot be traded/sold/packed/burned for Forge/Dust - can ONLY be consumed as an
Evolution sacrifice-credit input (reuses the EXISTING sacrifice-credit system, not a new mechanic).

Guardrails (all real, matching existing project patterns - Permits' weekly cap, Daily Login's
claim logic): max 1 Prison capture/player/day, cooldown vs same opponent, no
guildmate/friend/rematch/private-match rewards, no reward if opponent has no eligible card, no
capture from tutorial/bot/practice/unranked matches, failed attacks grant nothing, idempotent +
ledger-backed transaction.

**If a usable substandard card version is ever wanted instead of pure fodder, that requires its own
formal Collection-acquisition-rules reopening - explicitly NOT a Prison implementation detail,
needs its own GPT/owner round.**

Both are real, substantial features - not urgent, logged as locked design ready for
implementation whenever prioritized, not dispatched tonight.

## Open theory thread: minigame count/variety - sent to GPT (2026-08-25)

Memory Expedition's OWN design is already fully specified (rounds, difficulty curve, exact reward
table, seed/resume logic - not a gap). Real open question: is ONE minigame enough for the stated
milestone target, or does variety matter at this scope? No clean genre-standard number found via
research - genuinely non-obvious, sent to GPT rather than guessed.

## MAJOR FINDING: Windstep, not Stone Judgment, is VeteranPlus's real dominant spell (2026-08-25, CR)

**Step 1 (Novice bands):** 3/4 clean (spells/match, fallback ceiling, win-rate delta all fit).
**Real metric mismatch caught before shipping:** GPT's "0-5% cast rate" was decided against CR's
per-TICK ceiling diagnostic (2.55%), but the actual test asserts AiCastRateOfOpportunity =
TrialsWithAiCast/TrialsWithOpportunity - a structurally different PER-TRIAL metric, real measured
~22.7% [20.5%,24.9%]. 0-5% doesn't transfer to this metric. CR widened it to a PROVISIONAL 15-35%
(same margin-above-observed method already used elsewhere in the file) so the test isn't left
broken, explicitly flagged in-code as NOT GPT-confirmed. Real question back to GPT: give a real
band for the per-trial metric, or confirm the per-tick number was never meant to gate this
assertion.

**Step 2 (dead-slot fix):** done, general fix (not VeteranPlus-specific) - AIEnemySpellbookResolver
now filters to only AI-castable SpellEffects before slot selection. Real finding: only 5 distinct
AI-castable effect types exist in the pool today, so 5-of-6 slots resolving is the honest ceiling,
not a bug.

**MAJOR: reran per-spell diagnostic on the corrected loadout - VeteranPlus's real dominant spell is
WINDSTEP (Reposition), not Stone Judgment or Fault Line.** Windstep only won a loadout slot because
the dead-slot fix freed one up - it was invisible in every prior measurement. Real numbers: Windstep
535 casts, 75.2% win-rate-when-cast, 78.1% win-share. MirroredAiSimulationMatrixTests independently
confirms via maxSingleSpellWinShare=78.3% (was passing at a diluted number before the fix, now
correctly fails the 40% cap). Apprentice shows the identical pattern (74.6%, also Windstep-driven,
also previously masked). **GPT's existing 4-condition ablation plan (Stone Judgment/AvatarStrike
focused) doesn't test Windstep at all - running it as specified would measure the wrong spell.**
Needs a revised plan before step 4 runs.

**New open regression, flagged not fixed:** Apprentice's tick-ratio now fails its own ±15% band
(9.84 vs baseline 8.30, +18.6%) - real side effect of the corrected loadout.

19/21 tests pass in affected classes, both failures are the flagged items above, not silent
workarounds. CR correctly held rather than running the wrong ablation test.

## Vetting note on minigame-count answer (2026-08-25, CC) - real gap found before locking

Citations and reward-rule consistency check out. Real gap: GPT applied "does this risk being just
combat-replay with a label?" scrutiny to CC's roguelike-map proposal earlier but not to its own
Formation Tactics Trial proposal - which is structurally close to a curated hard Campaign stage
(fixed hand/budget/objective, existing formation rules, 60-90s). Sent back for a real answer on
what specifically differentiates it, rather than locking on the assumption it's automatically a
distinct mode.

## Novice band + Windstep ablation plan LOCKED (2026-08-25, GPT)

**Novice band, real fix confirmed:** per-tick (0-5%) and per-trial (TrialsWithAiCast/
TrialsWithOpportunity) are genuinely different metrics, don't conflate. Lock the per-trial
acceptance band at 15-35%, centered on the observed ~22.7% - matches CR's own provisional number
independently. Per-tick stays diagnostic-only. Lock the band only after remeasuring with the
dead-slot-filtered loadout (not the old contaminated numbers).

**Windstep ablation, real 4-condition plan:**
A: current corrected loadout (with Windstep). B: remove Windstep, leave slot empty. C: remove ALL
Reposition spells from equipped AND candidate pools. D: replace Windstep with a legal
non-Reposition spell matched by cost/role. Identical seeds across all 4. Report: AI casts/match,
per-trial cast conversion, player/AI win-rate delta, Windstep win-share + win-rate-when-cast, avg
ticks, no-spell fallback, legal-candidate availability.

Real causal caution: 75.2% win-rate-when-cast doesn't prove Windstep WINS games - it may just be
selected mostly when the AI is already ahead (availability bias). The remove/replace comparisons
are required to separate causation from correlation, not optional. Windstep dominance is
mechanically plausible (repositioning can convert lane bonuses/rescue units/create favorable
formations without raising card stats) - a real high-leverage tactic, not automatically a defect.
But 78.1% win-share is too concentrated to accept without the ablation proving cause.

## Minigame count/second-mode design - LOCKED FINAL (2026-08-25, GPT, corrected under real scrutiny)

GPT accepted the critique fully - Formation Tactics Trial as originally proposed was correctly
identified as "curated Campaign battle with different rewards," not a real second mode, and was
withdrawn. Real, structurally-differentiated replacement design:

**Tactical Puzzle mode** (Phase 1 candidate, NOT locked to build yet - see prerequisite below):
fully known board/hand/resource/enemy state, no card draw, no AI opponent acting, no tick-by-tick
combat. Finite objective (survive 3 clashes / defeat a marked target / protect a lane / minimal-
Resource solve), small legal-action set, immediate reset on failure, decision-based scoring
(Resource remaining/cards preserved/lanes protected/actions used), deterministic or narrow-
solution-set answer (chess-puzzle-like), daily/weekly SEEDED puzzles not endless generation.

Real structural differentiation from Campaign, stated explicitly: Puzzle loop = inspect fixed state
-> plan -> deploy/reposition -> verify objective -> score/reset. Campaign loop = draw/build
formation -> opponent acts -> combat resolves over time -> win/loss. Reuses existing formation/
deployment rules for legality/evaluation, but stops before becoming a normal battle.

**REAL PREREQUISITE, explicitly gating this: "If implementation does not support this deterministic
verifier, the mode should not be built yet."** No deterministic single-state outcome verifier
exists in the current codebase (battle system is tick-based auto-resolving, not built for
puzzle-style deterministic verification) - this is a real new engineering component needed before
Tactical Puzzle can be built, not just a design/content task.

**FINAL: MVP ships with Memory Expedition ONLY (one minigame, not two).** Tactical Puzzle is a
Phase-1 candidate contingent on the verifier + puzzle-authoring tooling being built/approved - not
committed to ship yet. Do NOT build fixed-hand "mini battles" that still run ordinary combat - that
was explicitly rejected. Do not add a third minigame until both modes prove repeat engagement.

## Novice cast-rate band LOCKED + Windstep ablation DECISIVE (2026-08-25, CR, verified against commit 0c5d276)

**Novice band LOCKED.** Re-measured on dead-slot-filtered loadout per instruction: real result
21.6% [19.4%, 23.7%] AiCastRateOfOpportunity - comfortably inside GPT's independently-confirmed
15-35% band. Comment updated PROVISIONAL -> LOCKED.

**Windstep ablation, 4 conditions, 1500 trials each, identical seeds (VeteranPlusWindstepAblationTests):**
- A) Current w/ Windstep: aiWinRate=28.5%, Windstep 551 casts, winRateWhenCast=73.8%, shareOfAiWins=79.4%.
- B) Windstep removed, empty slot: aiWinRate=28.9% - statistically indistinguishable from A.
- C) Reposition excluded at candidate-pool level (via resolver's own selection): converges on
  identical 4-spell list as B, aiWinRate=29.3% - confirms no other Reposition spell would silently backfill.
- D) Windstep -> Ember Wave (cost/cadence-matched): aiWinRate=28.1%, but Ember Wave got 0 casts in
  1500 trials - real structural finding, not a test bug: AISpellCaster tries same-effect spells in
  list order, stops at first legal match; Fault Line (also LaneDamage, listed first) always wins
  before Ember Wave is tried. D doesn't cleanly isolate "would any spell in that slot dominate" -
  flagged honestly, doesn't change the main conclusion.

**DECISIVE: removing Windstep changes AI win rate by <1pp.** Windstep's 79.4% win-share is
availability bias (fires mostly in matches AI is already winning via Fault Line/Stone
Judgment/Banner of Ashes), not a causal driver. No throttle/gate change indicated for Windstep. The
earlier 40% maxSingleSpellWinShare cap failure is a measurement artifact, not evidence of a real
balance problem.

**Still open, NOT yet investigated:** Apprentice tick-ratio regression (9.84 vs baseline 8.30,
+18.6%, fails its own ±15% band) - real side effect of the dead-slot fix. CR directed to investigate
this next (2026-08-25).

## Apprentice tick-ratio regression ROOT-CAUSED (2026-08-25, CR, verified against commit 6733dc6)

**CORRECTION to prior entry:** this was NOT a side effect of the dead-slot fix. First post-fix run
already showed ratio 1.171 (already failing) before the fix; the fix only nudged 1.171->1.189.
Predates the fix.

**Real mechanism (same at every tier, confirmed by splitting trials into zero-cast vs any-cast):**
trials where the AI casts NOTHING run substantially LONGER than trials where it casts at least once.
- Apprentice: baseline=8.35, zeroCast=11.76 (35.6% of trials), anyCast=8.92 (64.4%) -> aggregate
  9.93, ratio 1.189 (fails ±15%).
- VeteranPlus: baseline=8.94, zeroCast=11.23 (44.3%), anyCast=9.42 (55.7%) -> aggregate ~10.2, ratio
  ~1.14 (just inside ±15%).

Same underlying split shape at both tiers - VeteranPlus's zero-cast trials deviate less from its own
baseline (+25.6%) than Apprentice's do (+40.8%), which is why VeteranPlus lands just inside the
shared band and Apprentice lands just outside it by degree, not a different bug.

**Root cause:** match closeness (grindy fights) independently correlates with (a) running long
through ordinary card combat and (b) offering the AI fewer legal spell-cast windows. "AI didn't
cast" and "match ran long" are both downstream symptoms of the same underlying factor, not causal.
Consistent with the earlier per-spell finding that every spell's avgFinishTickWhenCast is at or
below the run average (casting correlates with faster finishes, never slower).

**No fix proposed - this is a structural fact, not a target-legality bug or magnitude issue.**
Real open question, NOT decided here: whether the shared ±15% tick-ratio band needs tier-specific
treatment (same treatment the other three Novice bands already got). Flagged to GPT for a real
decision, not guessed.

## Tick-ratio band fix - LOCKED (2026-08-25, GPT, vetted)

**Decision: do NOT widen the shared ±15% band.** The aggregate tick-ratio metric is confounded by
cast-state composition (mixes zero-cast and any-cast trials in different proportions per tier), so
it isn't cleanly measuring AI timing behavior - per CR's root-cause finding (commit 6733dc6).

**Fix: split the metric, not the threshold.** For all tiers:
1. Zero-cast trials: baseline ticks vs AI-on ticks (matched/paired seeds).
2. At-least-one-cast trials: baseline ticks vs AI-on ticks (matched/paired seeds).
Apply the existing shared ±15% band to each matched cast-state comparison separately. Keep
zero-cast frequency itself as a separate tier-specific AI metric (Novice already has one:
AiCastRateOfOpportunity 15-35%).

Apprentice's current aggregate failure (baseline 8.35, aggregate 9.93, ratio 1.189) becomes
descriptive telemetry once split, not a balance failure - shared band stays intact for the
corrected metric. **Apprentice gets no permanent wider aggregate band.**

**Fallback only if the metric refactor can't land immediately:** provisional Apprentice band
0.85-1.25 (not 1.30 - do not widen further than the measured 1.189 needs). Requires another
>=2000-trial confirmation run before this provisional band can be locked. This is a stopgap, not
the fix - CR should refactor the metric itself as the real fix.

## Prison (Bound Captive Fodder) + Academy research-choice backends SHIPPED (2026-08-25, VS, verified 07e6fbe)

40/40 (PrisonAndAcademyTests 26/26 + CollectionEvolutionTests 7/7 + CollectionBurnTests 7/7), HEAD
1e1ad4a stable both ends, 0 error CS. Every locked guardrail implemented: no capture on
loss/tutorial/bot/practice/private/rematch/unranked/guildmate/friend; max 1/player/UTC day;
idempotent via captureId ledger; single exit path (sacrifice credit only, no
sell/trade/pack/equip/Forge-Dust-burn); item type has NO combat stat, reflection-asserted; yield
delegates to CollectionBurnRules' existing rarity table (pinned equal by test) so Prison can never
be a better/worse credit source than burning a real card. Academy: offer->commit->complete->collect
state machine, one research at a time, past branches persisted, collect idempotent, committing one
option clears the others, branches are an enum with NO combat branch (structural, not runtime-
checked). Both invent no numbers/options - correctly deferred per locked spec ("numbers open").
PlayerProfile.cs frozen - additive fields need owner sign-off before persistence, same as Memory
Expedition.

**REAL OPEN GAP, flagged by VS's own failing test, not invented:** SameOpponentCooldownDays=1 is
currently a NO-OP - MaxCapturesPerUtcDay=1 already blocks same-day repeats, and at exactly 1.0 days
later the cooldown has already expired, so the anti-farming guardrail does nothing until the value
is >=2. The locked spec states the RULE but never a DURATION. Exposed as
SameOpponentCooldownIsRedundant (assertable, self-retiring test that fails the moment a real value
is set) rather than buried in a comment. **Needs an owner/GPT number for the actual cooldown days.**

## Tick-ratio split refactor SHIPPED - real result contradicts GPT's prediction (2026-08-25, CR, verified 8968e43)

Real refactor implemented exactly as locked (e262bac), not the fallback: RunPairedZeroCastSplit,
matched-seed baseline-vs-AI-on, split zero-cast/any-cast populations, each gated at the original
±15% independently. NoSpellFallbackRate (zero-cast frequency) left untouched, already tier-specific.

**Real, seed-paired result, 2000 trials/tier:**
- Novice: zero-cast ratio=1.045, any-cast=0.917 - both pass.
- Apprentice: zero-cast ratio=1.351 (n=704) - **FAILS**. any-cast=1.095 (n=1296) - passes.
- VeteranPlus: zero-cast ratio=1.197 (n=842) - **FAILS**. any-cast=1.086 (n=1158) - passes.

**GPT predicted the split would turn Apprentice's failure into descriptive telemetry - the opposite
happened.** Isolating the populations made the zero-cast deviation LARGER (1.351, not smaller) and
now also implicates VeteranPlus, which passed the old blended check (~1.14) but fails once isolated
(1.197). The any-cast trials (healthy at every tier) were diluting the blended average, masking how
large the zero-cast effect really is - the blended number UNDERSTATED the real deviation, not
overstated it.

Any-cast population is clean at every tier - the entire problem lives in zero-cast trials
specifically. Confirmed real, not a measurement artifact (matched seeds, same deck/hand between
baseline and on-condition for every pair).

**Not decided here - genuinely new evidence, routed back to GPT:** the original "split will fix the
false failure" prediction did not hold. Real options on the table now: leave zero-cast timing as a
real unresolved finding, widen a zero-cast-specific band (informed by real isolated numbers, not the
old blended guess), or something else GPT proposes. CR correctly did not guess or self-adjust.

## Zero-cast timing anomaly - real diagnostic protocol LOCKED, no band change yet (2026-08-25, GPT, vetted)

**Decision: do NOT widen the zero-cast band yet.** 1.351 (Apprentice) / 1.197 (VeteranPlus) is too
large to dismiss as sampling noise, and matched seeds only rule out different starting decks/hands -
they do NOT rule out post-treatment selection, since "zero-cast" is itself defined by what the AI
did during the run, not a pre-treatment condition. Real methodological caveat, correctly applied.

**Diagnostic protocol, in order:**
1. Split zero-cast trials into: no legal opportunity ever / legal opportunity existed but every gate
   roll failed / match ended before an opportunity appeared.
2. Forced-no-cast control, 3 conditions: baseline (spell AI disabled) / AI decision loop active but
   casting forcibly disabled / normal AI path with current gate.
3. Log first divergent tick + state hash: card positions/HP, Resource/cooldowns, combat RNG state,
   spell RNG state, candidate-scan count, AI decision-call count, any resource/cooldown/lane
   mutation on a failed cast attempt.
4. Check whether combat and spell decisions share one RNG stream - a no-cast AI may still consume
   random values and desync later combat outcomes even without ever casting. Separate the streams
   if so.

**Interpretation guide (all 4 branches covered, not just the expected one):**
- Forced-no-cast matches baseline -> the normal zero-cast path changes combat indirectly = a real
  defect.
- Forced-no-cast ALSO produces longer matches, and matches the "no-opportunity" subset vs baseline
  -> genuine state-selection/tier-composition effect, not a bug.
- Only legal-but-roll-failed trials diverge -> investigate gate/RNG consumption specifically.
- All controls match baseline but the selected zero-cast subset remains longer -> population
  property, revise the METRIC, not the AI.

**Flag for CR before starting:** step 4 (shared RNG stream) could surface a real architecture
question, not just a local test fix - if combat and spell RNG do share a stream, separating them is
a nontrivial refactor with project-wide balance-verification risk, not something to silently do.
Escalate back if that's what's found, don't just fix it inline.

## Empire Defense evidence gate - LOCKED (2026-08-25, GPT, vetted)

**Decision: no fixed build calendar for Empire Defense - gated by measured repeat engagement, not a
timeline.** Restores the original "prove repeat engagement" safeguard the reopened three-mode
"Minigame count" lock had dropped.

**Stage 1 - Memory Expedition:** 6 weeks post-stable-release, >=500-1000 players who actually unlock
the mode. Track D1/D7/D14/D28 retention, runs on distinct days, % completing 4+ runs in 14 days, %
returning week 2, % hitting daily reward cap. Green: >=25% complete 4+ runs in 14 days, >=15% return
week 2, D7/D28 don't materially underperform the rest of the game's cohort, GREEN ACROSS TWO
CONSECUTIVE COHORTS (not one lucky week). If Memory is weak: fix onboarding/reward
value/difficulty/discoverability first - do not jump to building a bigger mode to compensate.

**Stage 2 - Tactical Puzzle:** 4-6 more weeks after shipping. Green: >=20% of WAU complete 2+ puzzles/
week, >=15% return following week, real evidence of optimization/replay (not just first-completion),
no material cannibalization of Campaign/PvP/Memory participation.

**Empire Defense build gate - BOTH required:**
1. Memory AND Tactical Puzzle both hit their green thresholds above.
2. Real evidence of unmet demand: >=20% of engaged users regularly exhaust available solo rewards or
   report wanting a longer strategic session (measurement method for "report" not yet specified -
   open detail, not blocking since this is 10+ weeks out regardless).

**Decision table:** Memory weak -> fix Memory, do not start Defense. Memory strong/Puzzle weak ->
fix or replace Puzzle, hold Defense. Both strong, no demand signal -> keep 3-mode plan deferred, not
cancelled. Both strong + clear demand -> approve Defense PROTOTYPE (not production). Demand present
but buildings/server systems still unresolved -> prototype rules offline only, no production reward
commitment - consistent with the server-ledger risk already flagged for Defense's reward path.

**Real practical implication, not GPT's framing but the honest read:** Empire Defense cannot even
start prototyping for >=10-12 weeks minimum after Memory Expedition ships stable to real players,
and the clock hasn't started at all until Memory is actually live with a real population reaching it.

## Tactical Puzzle narrative framing - PARTIALLY LOCKED (2026-08-25, GPT, vetted - split decision)

**LOCKED (mechanical/scope parts, verified safe):**
- Framing: "War-Room Reconstructions" - the Empire preserves fixed tactical records; player studies a
  known position and issues the best order, does not fight a live opponent or predict one. Avoids
  prophecy framing (no supernatural authority claim over canonical outcomes).
- No dedicated NPC host for Phase 1 - systemic "War Room Records"/"Tactical Records" institutional
  voice, not a speaking character. Backed by real, properly-analogous citations this pass:
  Hearthstone Puzzle Lab, Shadowverse Puzzles-under-Practice, LoR Challenges, Yu-Gi-Oh Master Duel's
  tutorial/story split.
- Explicit boundary vs Empire Defense (both hit the same building-rotation temptation, kept separate
  on purpose): do NOT rotate Tactical Puzzle rules by building; do NOT scale puzzle state/solutions
  from building levels; do NOT require a building upgrade to make a fixed puzzle solvable; do NOT
  attach a different building host each week; do NOT use Tactical Puzzle to duplicate the
  Tower/Empire Defense rotating-purpose intent.
- If a host is added later: Academy Strategist as presentation role only, not gameplay authority -
  curator of archived scenarios, precise/observant/calm personality, praises correct reasoning. Only
  add once a visual prototype proves an avatar improves comprehension, not by default.

**NOT LOCKED - sent back for correction:** GPT's second pass asserted specific claims about existing
campaign tone ("matches the campaign's emphasis on command, evidence, preparation, earned mastery,"
"the campaign's concern with incomplete and contested history") and referenced a `[Player Title]`
convention as an existing leadership-title system. CC grepped docs/ and Assets/ - neither exists
anywhere in the project. GPT's FIRST pass on this topic had correctly hedged ("I could not find the
full story bible... should remain a framing layer until Command Centre checks it") - the second pass
dropped that hedge without actually gaining any new access to story content. Sent back to redo the
"why this fits" reasoning without unverified lore claims.

## Tactical Puzzle narrative framing - FULLY LOCKED (2026-08-25, GPT, corrected + vetted)

Completes the prior split decision (4812b27) - GPT fully accepted the correction and removed the
unverified claims, no new overclaims introduced.

**"Why this fits" (mechanical only, no lore assertions):** deterministic authored state, not a live
battle or random Campaign replay; player solves a bounded problem with known units/lanes/resources/
objectives; weekly rotation is data configuration, no new combat rules; no host required in Phase 1;
neutral provisional wording ("Recon Record" / "Battle Reconstruction" / "Tactical Brief") until the
real story bible confirms actual terminology - explicitly a presentation wrapper, not a claim about
existing campaign lore. If the story bible later establishes real terminology/title/faction voice,
layer it onto the presentation without touching the deterministic puzzle system underneath.

**Empire Defense separation (reinforces existing locks, no new content):** Reconstructions use fixed
tactical states + deterministic verifier; Empire Defense uses wave spawning/placement/base integrity/
temporary boons; neither may alter Campaign/PvP combat rules; neither grants cards/Forge/Dust/
Permits/Evolution materials/permanent combat power; separate idempotent reward-claim entries under
the shared solo reward budget.

## Tactical Puzzle deterministic verifier SHIPPED (2026-08-25, VS, verified 5bde81e, 16/16)

Real verifier built by composing existing rules (LaneState.HasRoomFor, RepositionRules,
ResolveLaneClash), not forking a parallel combat engine. Covers: determinism (5 identical runs same
state+actions); real SlotWeight capacity; affordability vs legality kept as DISTINCT failures;
out-of-range hand index; Resource spent + card leaving fixed hand (no draw); all 4 locked objective
shapes + negative cases; unset clash count resolves NOT-solved (not a free win); minimal-Resource
solve correctly excludes spend-nothing/hold-nothing; decision-based score inputs; malformed/empty
input rejected. No puzzle content/numbers, per locked spec - structural only.

**CORRECTION to the gate's own cost premise:** the register said this "needs a real new engineering
component." Real finding: LaneBattleResolver.ResolveLaneClash was ALREADY a pure static function
over two LaneStates - the hard part (deterministic clash resolution separable from the tick loop)
already existed with no caller. This was composition, not a new combat engine - materially cheaper
than the original framing suggested.

**Authoring tooling is the other half of the gate and is still untouched** - assigned to VS as the
next real task (2026-08-25).

## Prison SameOpponentCooldownDays = 7 - LOCKED (2026-08-25, BS, vetted with one caveat)

7 days, keyed by attacker/defender pair. Reasoning: MaxCapturesPerUtcDay=1 already controls volume;
7-day opponent cooldown controls collusion (two cooperating accounts farming each other) without
making the feature unusable in a small player population. Cooldown applies only AFTER a successful
capture - failed capture attempts do not consume it (safer default, avoids rewarding failed
collusion attempts with a "protective" cooldown).

**Real caveat, not GPT's fault - flagged by CC:** GPT specified this "must be server-authoritative,"
but Prison has NO server backend today (explicitly a solo stand-in per the locked design - "Server-
dependent stand-in", "GUILD FEATURES PENDING SERVER"). VS to implement as a real, testable
CLIENT-TRACKED value using the same idempotent-ledger pattern as MaxCapturesPerUtcDay, explicitly
documented as not abuse-proof until real server work lands - do not claim server-grade enforcement
that doesn't exist. SameOpponentCooldownIsRedundant test (VS's self-retiring reminder) should now be
replaced with a real assertion using the 7-day value.

## Empire building save-schema defaults - LOCKED (2026-08-25, BS, vetted)

Migrated accounts: Level 1 (not 0) for Storage/Training Grounds/Quarry/Academy/Tree of Knowledge.
Reason: these are minimum-valid structures, not absent inventory - Level 1 gives no meaningful
shortcut, preserves existing progression, and prevents migrated players (especially existing
Evolution users) from being blocked by newly-introduced building fields. New accounts use the same
Level 1 default unless final onboarding explicitly starts construction from zero.

**Tree of Knowledge UI - CORRECTED (GPT self-retracted its own earlier claim):** the "evolution/XP
selection chrome" from the earlier UI plan was NOT grounded in an existing UI contract - retracted.
The existing Evolution system already implies card/step selection through Collection/Evolution flows;
it does NOT imply a Tree-specific selection screen. Tree of Knowledge should initially just
expose/gate the EXISTING Evolution/XP functionality. Any dedicated Tree research/selection interface
needs its own separate UI brief - not assumed, not built on spec.

## Tactical Puzzle content framework - LOCKED (2026-08-25, BS, vetted)

**Authoring process (backward-construction, real puzzle-design methodology):** choose one of the 4
locked objective shapes -> define the fully fixed state (exact hand/board/Resource/lane bonuses/
enemy state, no random draw/AI choice/uncontrolled timing) -> define legal action budget (usually
2-6 meaningful actions: placement/lane choice/spell use/repositioning/pass; reject cosmetic-only
permutations) -> construct backward from the desired solution (identify required final state, find
minimum actions to reach it, add plausible-but-losing alternatives) -> enumerate ALL legal action
sequences up to the limit.

**Acceptance tests, a puzzle is valid only if:** >=1 legal solution exists; the verifier finds it from
a clean initial state; no single action solves it accidentally; the minimum solution requires
meaningful ordering/tradeoffs; every alternative sequence is classified success/failure/incomplete;
solution count is exactly 1 or a small equivalence class (<=3 strategically-equivalent solutions,
same state+score counts as one); no solution depends on hidden RNG/frame timing/undocumented
behavior (consistent with CR's confirmed finding that combat has zero RNG).

**First weekly batch: design 6 candidates (2 easy/2 medium/2 hard), verify all 6, ship 3** (1
accessible, 1 clear-optimization, 1 high-difficulty) - keeps 3 as buffer/rotation so a mid-week
trivial/unsolvable/frustrating puzzle doesn't force an emergency fix. First batch measures
completion rate, retries, average solution length, abandonment to guide future difficulty - explicitly
does NOT create new currencies or deck-building rewards, consistent with the locked reward-boundary
rules (shared solo reward budget, no new currency per mode).

## Tactical Puzzle authoring tooling SHIPPED - both halves of the gate now exist (2026-08-25, VS, verified 73c8a86)

40/40 (TacticalPuzzleAuthoringTests 24 + TacticalPuzzleVerifierTests 16), 0 error CS, HEAD 2e497fb
pinned run, committed 73c8a86. Structure only - no proposed cards, Resource amounts, clash counts or
difficulty; every number is an author-supplied field with no baked default.

Real design: TacticalPuzzleAction takes live BattleCardInstance objects, which stored data can't
name - coordinate references (side/lane/index-in-lane) resolve at materialization and are captured
THEN, not re-read off live lane lists (otherwise an early Windstep would renumber every later
reference). Validate/Materialize/CheckEnvelope kept deliberately separate: a definition failing
Validate is an AUTHORING bug, a play failing verification is a puzzle WORKING - conflating them makes
an unsolvable puzzle look like a code defect. Capacity uses real SlotWeight, not card count (two
rarity-7 units can overflow a 3-slot lane).

**Recommend CheckEnvelope run in CI once real content exists** - catches an intended solution that
doesn't actually solve, a line that reaches the right outcome via the wrong action (mis-attributed
hint), and a puzzle going silently trivial after a card/reposition rule changes underneath it.

**REAL TRAP, worth recording for any seat building fixed battle states:** PlayerBattleState's
constructor SHUFFLES the deck it's given (unseeded in production) and auto-draws StartingHandSize.
Passing an intended fixed hand AS a deck randomizes its order - every Deploy index in every envelope
would then point at the wrong card, INTERMITTENTLY. Fix: both sides use an empty deck with the hand
placed explicitly. Same class of bug as the earlier derived-stat trap (Card.Attack/Health/
ResourceCost) - an inherited constructor doing more than its name suggests.

## Chapter 16 (The Hollow Crown) SHIPPED (2026-08-25, WH, verified d3c0de3)

16-1..16-30, same depth pattern as 11-15. Campaign now 453 stages total. Gate L30 through Ch16
unlock; gem lock LockedTotalCh1Through16 = 10536. 15-30 -> 16-1 chained correctly, 16-30 terminal.
AF (auto-fail?) retune at 16-24 and 16-29. Chapter16FullDepthTests 11/11, plus Gate/roster/permit/
Castle/Ch15 smoke green. Next in WH's pipeline: Ch17.

## Zero-cast timing anomaly - ROOT-CAUSE PROTOCOL COMPLETE, no AI defect (2026-08-25, CR, verified c136c7d)

**Step 4 (shared RNG stream, the escalation trigger) - checked via source read, confirmed clean:**
combat (LaneBattleResolver/AISpellCaster/SimpleAIOpponent) has zero RNG usage anywhere - fully
deterministic. _aiSpellCastRng is its own dedicated System.Random, separate from deck-shuffle RNG.
No shared-stream desync risk - nothing to escalate.

**CORRECTION to this session's own methodology, found mid-investigation, applies retroactively to
Windstep ablation / RunPairedZeroCastSplit / AiSpellCastImpactDiagnosticTests:** PlayerBattleState's
draw-pile Shuffle() uses a THIRD, separate RNG source (unseeded `new System.Random()` unless
PlayerBattleState.SetShuffleSeedForTests(seed) is explicitly called) - distinct from
UnityEngine.Random (deck composition) and StartMatch's rngSeed (AI-cast rolls). None of this
session's earlier "seed-paired" work called it, so those runs were NOT truly tick-for-tick identical
matched pairs the way "matched seeds" implied, despite the other two RNG streams being pinned. Their
AGGREGATE conclusions over 1500+ trials remain sound (unbiased noise cancels in aggregate, and CR
independently re-derived the Windstep/per-spell numbers with consistent results) - but the specific
claim of exact pair-matching in those earlier reports is corrected, not the conclusions themselves.
Fixed here (SetShuffleSeedForTests now called) - worth remembering for any future test needing
genuine match replay, not just statistical pairing.

**Steps 1-3, re-run with the shuffle fix applied, TRUE matched seeds this time (verified via
tick-0 byte-identical deployed units):** Added BattleController.
SetForceAiSpellCastGateAlwaysFailForTests - the real forced-no-cast control: decision loop runs
every tick, RNG rolls consumed exactly as a real roll would, casting is just forced to fail. Distinct
from spells being disabled entirely (which was the earlier, weaker "baseline" condition).

**DECISIVE: baseline vs forced-no-cast vs normal are tick-for-tick IDENTICAL in 1500/1500 trials**
whenever the AI ends up not casting - zero divergence, ever. Both zero-cast sub-populations
(NoOpportunityEver n=5, OpportunityButEveryRollFailed n=537) show ratio=1.000 across all three
conditions, every time. Hits GPT's own interpretation guide exactly: "All controls match baseline
but the subset stays long -> population property, revise the metric, not the AI."

**CONCLUSION: no AI defect exists.** Merely running the AI decision loop - evaluating candidates
every tick, consuming RNG draws that always fail - has ZERO causal effect on match length. The seeds
that end up zero-cast are the SAME seeds that were already going to run long through ordinary card
combat alone; close/grindy matchups offer fewer legal spell-cast windows as a side effect of state,
not a code path difference. Correlation, not causation - now proven, not hypothesized.

**Real remaining decision, NOT decided here, routed to BS:** whether to drop the zero-cast tick-ratio
assertion entirely, or make it descriptive-only telemetry (same treatment already given to the
spell-contribution/win-share metric). This is a metric-design call, not an AI-behavior question -
the AI itself needs no fix.

## New EditMode stall signature - Shop purchase flow, real and unconfirmed (2026-08-25, CR)

Full-suite run stalled and was killed by the wrapper's guard (120s zero log growth, exit 124, no
results.xml) at MyriadOfDragons.Tests.ShopV1ChromeTests.
BuildShop_UsesCatalogShellBackground_AndStaminaStateSprites (Assets/Tests/Editor/
ShopV1ChromeTests.cs:79). Log shows the test's own purchase-flow logging (CurrencyManager spend,
SetShopStatus) completing normally through "Purchased Stamina Potion (30)." at
ShopPresenter.cs:745, then goes completely silent - no exception, no `error CS`, no further
test-runner output. Not a compile error, not an assertion failure - looks like a real hang/deadlock
either in that test or the Editor right after it, cause unconfirmed. HEAD unchanged across the
attempt (894f7eb before and after).

**Checked and ruled out as the likely cause:** Packages/manifest.json's uncommitted 2D Animation/PSD
Importer diff (flagged earlier tonight) is actually 2 DAYS OLD (Aug 23), not added tonight, and
packages-lock.json shows those packages resolved successfully at some point. Dozens of clean runs
happened tonight with that same diff present (including the 1352/1358 pin), so it's very unlikely to
be today's cause - this looks like a separate, real bug specific to the Shop purchase-flow test path.

CR correctly did not touch ShopPresenter.cs/ShopV1ChromeTests.cs (Metagame-owned, per ownership
rule) - flagged the exact stall location instead of guessing at a fix. Routed to WH to investigate.
Unity lock re-held again immediately after (different PID, presumably VS resuming) - CR holding, not
retrying blind over an active lock.

## Tactical Puzzle: real art wired + entry point confirmed (2026-08-25, VS shipped presenter, CC wired art)

**VS already closed the "nothing playable" gap** (2e84132, "War-Room Reconstructions: the puzzle
mode is actually playable now") - TacticalPuzzlePresenter has Entry/Board/Result views, a real
Empire-screen entry point (OpenWarRoomReconstructions chip), MonoBehaviour-supplies-timing split
(all rules go through TacticalPuzzleSession -> the real verifier), locked ST framing embedded
directly (not invented), and an art-optional pattern identical to EmpireBuildingDetailPresenter.

**Art now wired (383d02d):** 6 renders imported (entry shell, board frame, result modal, 3 separate
tile-state images), verified genuine RGBA transparency. Retargeted ArtResourcePaths to the actual
delivered filenames and split the single "tile" role into tile_locked/tile_available/tile_completed
since 3 distinct per-state images were delivered, not one atlas - BuildSlotTile now picks art by the
slot's real state. Updated MissingArt_DoesNotBlockTheScreen's role list to match (assertion
unchanged - still only checks a resource path is reserved, not that art is absent).

Test run pending - queued to VS/CR's next pass.

## Tactical Puzzle entry point stays on Empire, not Home - DECIDED (2026-08-25, CC)

VS flagged this as open. Deciding now rather than parking it: entry point stays on the Empire
screen. Reasons: (1) thematically consistent with the "War-Room Reconstructions" framing already
tied to the Academy/Empire building system, (2) HomePagePresenter.cs is WH's file - moving it there
would create an unnecessary cross-seat dependency for a UX call with no real functional difference.
No change needed.

## Full EditMode baseline: 1449/1458 (2026-08-25, VS, verified HEAD 91392d3 -> 0f931d0)

Suite has grown a lot (Ch11-18 etc) - 1458 executed vs the older 1081 baseline, compare the FAILURE
SET not the ratio. Nothing in VS's own lane regressed - all 73 TacticalPuzzle tests pass inside the
full-suite context (not just isolation), EmpirePresenter's chip-strip respread disturbed nothing.
Also self-caught and fixed a real bug while verifying: the result-modal art was loaded but never
applied to anything (91392d3).

**2 of the 9 failures were misclassified in the standing notes - real findings, not noise:**
1. **Stage 2-6 and 17-13 field the identical 3-card roster** - fires in BOTH Chapter17FullDepthTests
   and Chapter18FullDepthTests, same stage pair, same message, every time - NOT the flaky
   "winnability, failing stage moves every run" class. Real content collision. WH's lane (campaign
   content), not touched, flagged for WH.
2. **ReleaseProfilePersistenceContractTests fails in full suite, passes in isolation** (VS
   re-bisected: 120 candidates + victim, 941 tests, zero victim failures) - order-dependent
   pollution, not a test/code defect. "Repeated gem pack purchase after reload must still grant a
   new owned card - Expected 12, But was 11." Economy/save lane, frozen-file adjacent - not VS's to
   chase.

**MirroredAi note, ties directly to the pending BS thread (zero-cast metric drop-vs-descriptive):**
SimulationMatrix_Apprentice still fails on the zero-cast-trial band (11.74 vs 8.65, 1.36x) at a HEAD
that already includes c136c7d's conclusion that this is a population property, not an AI defect. If
that conclusion holds, the ASSERTION is now measuring something already decided as expected - the
TEST needs re-reading once BS answers, not the AI. Same shape as the art-role/Prison-cooldown cases:
a test whose subject moved underneath it.

Remaining 7 failures: unchanged known set (1 winnability - genuinely moving, 2 MirroredAi under
active tuning, 3 UI shells peer-in-flight, 1 pollution above).

## Tactical Puzzle PlayerProfile fields - VETTED AND LOCKED (2026-08-25, VS proposal, CC decision on the one open question)

```
public List<TacticalPuzzleRecord> tacticalPuzzleRecords = new List<TacticalPuzzleRecord>();
public int tacticalPuzzleRulesVersion = 0;

[Serializable] public class TacticalPuzzleRecord
{
    public string puzzleId;
    public int bestActionsUsed = -1;
    public int bestResourceRemaining = -1;
    public int bestUnitsPreserved = -1;
    public int bestLanesHeld = -1;
    public string firstSolvedUtcDate = string.Empty;
}
```

Keyed on puzzleId (stable), not slate position - avoids a later re-order silently re-pointing
completions at different puzzles. All "best" fields default -1, not 0 - same trap class as the
memoryExpeditionFirstSelectedTile incident: a field added later would deserialize existing records
as 0, and 0 orders would silently read as a perfect unbeatable score. Deliberately excludes
unlock/locked state (derived from completions), in-progress attempt state (mode is "immediate reset
on failure" by design), and attempt/failure counters (nothing in the locked design uses them yet).
Migration: old saves deserialize with an empty list + rulesVersion 0, reads correctly as "no puzzles
solved" - no backfill, no sentinel needed elsewhere in PlayerProfile.

**Open question DECIDED: accumulate, not reset-per-cycle, no third cycle-key field.** Records already
carry firstSolvedUtcDate, so a "best this cycle" view can be derived later in application logic by
filtering by date range - accumulation is both the safer default (never destroys player history) and
doesn't foreclose per-cycle views without a schema change.

VS clear to add these fields to PlayerProfile.cs now.

## Zero-cast tick-ratio test contract - LOCKED (2026-08-25, BS, vetted)

Re-purpose, don't tune around. New 4-metric contract:
- anyCastTickRatio: real pass/fail timing metric, shared ±15% band (the causally-clean population).
- zeroCastRate: tier-specific AI behavior metric (descriptive, same class as Novice's
  AiCastRateOfOpportunity).
- zeroCastTickRatio: descriptive telemetry only - no longer a pass/fail gate. Apprentice's 1.357
  (11.74/8.65) gets logged, never fails the suite.
- forcedNoCastMatchesBaseline: NEW hard correctness invariant - since forced-no-cast is now PROVEN
  to always equal baseline, asserting that equivalence going forward catches a real future
  regression if the AI decision loop ever starts having side effects it shouldn't.
Widening the band was correctly rejected (would imply the AI causes the longer fight, which is
false); dropping the pass/fail role reflects the actual, completed control evidence.

## Empire Defense mechanic spec - LOCKED as design document, NOT a build trigger (2026-08-25, BS, vetted)

Still fully behind the Empire Defense evidence gate (290c5c4) - this is the prerequisite spec that
was missing, not a green light to build. Real content:

**Lanes:** 3 fixed lanes, 4 path positions/2 defense sockets/1 gate/1 base-entry point each. Lane
Integrity 2/lane (enemy reaching gate removes 1; at 0 lane is Broken and leaks damage straight to
Core). Core Integrity 6, 0 = loss, all waves cleared = win. "Protect this lane" = finishes with Lane
Integrity > 0. (Not individually caveated as provisional the way Command Resource numbers are below -
minor inconsistency, treat as equally provisional, needs simulation like everything else here.)

**Defense nodes (Phase-1, 4 types, expose 2-3/scenario):** Guard Post (block/delay), Archer Nest
(single-target ranged), Arcane Spire (AoE/slow), Warden Shrine (barrier/repair - MUST have a strict
per-run activation limit or it becomes the mandatory defensive pick and trivializes Lane Integrity).

**Wave/weekly structure, real correction to the original design:** Weekly Ascent must NOT be 3 waves
per floor (30 floors x 3 waves = 90 waves, too long). Correct shape: 1 floor = 1 short wave, every
5th floor elite/boss, checkpoints at the already-locked 5/10/15/20/25/30, run resumes from last
checkpoint, each threshold pays once per weekly seed.

**Command Resource (mode-local, not saved currency, not battle Resource) - explicitly provisional:**
initial 6, cap 12, passive +1/8s, kill +1 (below cap only)/elite +2/boss +3. Node costs: Guard Post 3/
Archer Nest 4/Arcane Spire 6/Warden Shrine 5/upgrades 3-5. Expected available Command ~10-14 (wave 1)
/14-18 (wave 2)/18-24 (wave 3). Explicitly flagged "starting simulation values, not final balance
locks."

**Building-focus rotation, consistent with the already-locked boon rule (affects choices, not raw
power):** Barracks = temp Guard reinforcement/faster redeployment. Castle = damage containment/Core-
protection charge. Gate = delay lane advance/preview next wave/seal a lane. Open/Card = explicitly
NOT ready to ship under a placeholder name - no implemented building or rule set behind it yet.

**Engineering honesty (asked for, delivered):** Reusable - lane identifiers/occupancy concepts, some
slot-validation ideas, deterministic seed patterns, reward-ledger patterns ONCE a trusted service
exists (doesn't yet). NOT reusable - normal clash resolution, existing opponent AI, spell timing,
RepositionRules as movement, ordinary deployment. New systems needed: path-position movement, wave
scheduler, enemy targeting/cadence, node placement/upgrades, Command-resource clock, Lane/Core
Integrity state machine, boon selection/run state, weekly floor persistence, once-only reward claims.
Confirmed: "a substantial second combat simulation" - still needs prototyping/simulation before art,
reward values, or production commitments lock. No build triggered by this entry.

## Empire Defense spec - real benchmarking gap found post-lock, not yet resolved (2026-08-25, CC, web-verified)

Web research (Bloons TD6 "lives" system, Kingdom Rush lane/reinforcement structure, Rush Royale live
mana economy) confirms the overall design pattern is structurally sound and matches real, proven
games - not invented. Sources: bloons.fandom.com/wiki/Bloons_TD_6, blog.udonis.co (Kingdom Rush),
en.androidayuda.com (Rush Royale).

**Real gap: 2 Lane Integrity per lane is a very tight margin, and the spec never states enemies-per-
wave, so there is no way to judge whether that number is reasonable.** In Bloons TD6, lives deplete
across a large sustained stream of enemies (a big buffer); if Empire Defense sends more than 2
enemies down one lane before the player can react, that lane breaks almost immediately, every
time. Not blocking (still fully behind the evidence gate, no build triggered) but flagged back to
BS before this number gets treated as more settled than it is.

## Zero-cast test contract SHIPPED - and a NEW real Apprentice any-cast signal surfaced (2026-08-25, CR, verified c7f6467)

4-metric contract implemented exactly as locked (8fc8d56): anyCastTickRatio stays the hard ±15%
gate; zeroCastRate + zeroCastTickRatio both descriptive-only now; forcedNoCastMatchesBaseline is a
new hard invariant (baseline vs forced-no-cast must match EXACTLY, no tolerance). Also proactively
backfilled the shuffle-seed fix (c136c7d) into RunPairedZeroCastSplit itself, which predated that
fix and needed the same correction for its ratios to mean what they claim.

**Novice: clean.** anyCastTickRatio 1.043 (well inside band), zeroCastTickRatio 1.000,
forcedNoCastMatchesBaseline 0/300 exact mismatches - further confirms c136c7d holds under a
permanent guard, not just the one-off study.

**REAL NEW FINDING, Apprentice: anyCastTickRatio - the metric explicitly kept as the causally-clean
gate - now FAILS at 1.355 on 1297 trials, where it previously PASSED before today's shuffle-seed
backfill.** CR's read: the old measurement was noise-diluted by imperfectly-matched pairs (unseeded
shuffle meant baseline/on decks weren't actually identical per pair) - this is the first time
anyCastTickRatio has been measured with genuinely matched seeds, so a real signal may be emerging
for the first time rather than a regression from this commit. NOT root-caused, band NOT touched, CR
correctly declined to guess. Apprentice's run aborted at this assertion, so forcedNoCastMatchesBaseline
is untested for Apprentice this pass.

**VeteranPlus failed separately on the pre-existing player-win-rate-drop assertion** (10.0% vs 8pp
cap, baseline 32.7%->22.7%) - unrelated to this commit, possibly the already-parked "2 assertions
under active owner-directed tuning" from CLAUDE.md, possibly not - CR correctly uncertain, not
asserting either way. Untested this pass (aborted before reaching it).

**Real open question, routed to BS:** does Apprentice's any-cast population genuinely run longer when
the AI casts (a real behavior finding), or is this a residual measurement artifact even after the
shuffle fix? Not decided here.

## Apprentice any-cast signal - real diagnostic protocol LOCKED, no verdict yet (2026-08-25, BS, vetted)

**Real methodological catch: anyCast is a post-treatment outcome** (the AI casting is itself an
event, not a pre-existing condition) - so 1.355 could be the spell's LEGITIMATE gameplay effect
(healing/buffs/repositioning genuinely extending the match), not an AI-timing defect. Do not
conflate these.

**Diagnostic protocol:**
1. Record the first tick the AI casts.
2. Compare baseline vs AI-on state hashes every tick BEFORE that cast (catches any residual
   mismatch predating the spell entirely).
3. Classify the first cast by spell and effect type.
4. Shadow/no-op control: AI evaluates and selects the SAME real cast, RNG rolls/decision calls
   consumed identically, but the spell's gameplay EFFECT is suppressed, no resource/cooldown
   mutation beyond the explicitly controlled values.
5. Compare normal AI-on vs shadow/no-op on the same seeds.

**Interpretation (all branches, not just the expected one):**
- State diverges before the first cast -> residual simulation/state-mutation defect.
- State matches until cast, shadow matches baseline -> 1.355 comes from the spell's real gameplay
  effect, not AI timing - the metric itself may be measuring combat-effect impact, not AI timing,
  and should be reconsidered as a balance gate entirely.
- Shadow still longer than baseline -> investigate cast-selection bookkeeping, resource/cooldown
  mutation, or tick-order effects - a real bug class.
- Only one spell/effect type drives the increase -> run a per-spell ablation before touching any
  global AI metric.

**Explicitly rejected: a forced-cast control that changes the cast schedule arbitrarily** - would
confound spell impact with decision behavior. Shadow control using the real selected-cast trace is
the clean version.

**Decision: keep anyCastTickRatio as the hard gate for now - do NOT widen or retune until this
control is complete.**

## VS self-picked work while holding: compile_check fix + results.xml race finding (2026-08-25, VS, verified 08b7168)

**compile_check false-FAIL fixed (08b7168).** The script was reporting confusing CS0103/CS0246 for
brand-new files that were actually correct - root cause: Unity owns .csproj and lists every source
file explicitly, so a file created since Unity's last refresh isn't in the project yet. The compiler
never says "file missing," it reports an undefined NAME at each use site, which points at the
CALLER and reads as "my new type is broken" - nothing in the output pointed at the real cause. Fix:
now lists any .cs file on disk but absent from the .csproj before build output, with what to do.
Validated both directions (clean tree silent, a dropped throwaway file correctly named and located).
VS caught its own first version being wrong before committing it.

**Real cross-seat process finding, no code commit (a CLI-usage fix, not a file change): data race
on results.xml/run.log.** Getting one set of real numbers took VS six attempts - four lock refusals,
plus one run that EXECUTED and then had its results.xml AND run.log deleted by another seat's run
starting right after (the wrapper clears both at startup). The dangerous direction isn't the
deletion (that stops you) - it's a REFUSED run leaving the PREVIOUS run's results.xml in place,
which parses perfectly and answers confidently wrong. Fix: pass seat-unique -ResultsPath/-LogPath
(VS now uses vs_*.xml/vs_*.log). Every seat running tools/run_editmode_tests.ps1 should do the same.

## Apprentice any-cast root-caused - real spell effect, NOT an AI defect (2026-08-25, CR, verified b81592b)

Full 5-step shadow-control protocol executed exactly as specified. New harness
ApprenticeAnyCastRootCauseTests.cs, 1500 trials matched-seed (same shuffle-fix as c136c7d), 956
landed in the any-cast population.

**Shadow control:** BattleController.SetShadowModeSuppressEnemySpellEffectForTests - the AI's real
live decision loop runs unchanged (candidate selection, gate roll, Energy spend, cooldown,
SpellCastLog entry all real), only spell.Cast's battlefield effect is suppressed. Not a forced-cast
(would confound schedule with effect), not a pre-recorded trace replay (decisions evolve live
against real state, correct per spec).

**Steps 1-3:** pre-cast state-hash divergence 0/956 - no residual defect before the cast. First-cast
classification: Reposition/Windstep (n=453), LaneDamage/Fault Line (n=432), AvatarStrike/Stone
Judgment (n=58), LaneAttackBuff/War Cry (n=12), LaneHeal/Renewal (n=1).

**Steps 4-5, decisive: shadow ticks == baseline ticks EXACTLY (6.61 vs 6.61, ratio 1.000)** in the
overall aggregate AND independently in every one of the 5 effect-type buckets, no exceptions. AI
cast-selection/bookkeeping mechanics contribute zero measurable elongation on their own.

**CONCLUSION, per BS's own interpretation guide ("state matches until cast + shadow matches baseline
= the ratio is the spell's real effect, not AI timing"): confirmed.** The 1.343 normal-vs-baseline
ratio (matches c7f6467's 1.355 within trial-count noise) is real, legitimate spellcasting impact on
match length, not a bug. Not one dominant spell - Reposition/Windstep (1.215) and LaneDamage/Fault
Line (1.618) both contribute materially; AvatarStrike/Stone Judgment is actually SHORTER than
baseline (0.897, n=58, small sample). Consistent with the earlier Windstep ablation finding
(Windstep doesn't drive extra WINS - availability bias) - this shows it separately, legitimately
extends match LENGTH via repositioning, a different axis, no contradiction.

**No AI defect anywhere in this entire thread now.** Zero-cast was a population artifact (c136c7d).
Any-cast is genuine gameplay effect (this commit). Real remaining question, NOT decided here, CR
correctly did not touch the band: should anyCastTickRatio keep gating "is combat length reasonable"
when part of what it measures is intentional spell behavior (a heal/reposition legitimately
prolonging a fight is arguably working as designed, not a balance failure)?

## Chapters 17-18 + Bazaar/Chat letterbox actually fixed (2026-08-25, WH, verified 8b0cdd9 / 2e57fb1)

Ch17 (The Ashen Banner) + Ch18 (The Silent Throne), 17-1..18-30, campaign now 513 stages, Gate L30
through Ch18. 11/11 + 11/11 EditMode. Chapter production correctly held at 18 per instruction,
waiting for next direction before Ch19+.

Bazaar/Chat sky-blue letterbox - previous entry only diagnosed this, never fixed. Now actually
fixed: opaque fullscreen backing under the preserveAspect shells, same pattern as the Campaign map
stage-detail modal fix (e57aa02).

**Retention/engagement telemetry - correctly stopped, not started.** Needs new raw event storage
(unlock date, run completions w/ UTC date, daily-cap hits) - that requires new save/profile fields,
so WH stopped before touching PlayerProfile.cs/SaveSystem.cs/SaveMigration.cs pending owner sign-off,
same discipline as every other frozen-file case tonight.

## anyCastTickRatio converted to descriptive-only, thread FULLY CLOSED (2026-08-25, BS, vetted)

**Decision: remove anyCastTickRatio from pass/fail. Retain a shadow-control invariant as the real
timing-correctness gate. Do NOT widen the band to accommodate Fault Line/Windstep - effect mix will
keep changing as the catalogue/AI loadouts evolve, and a wider shared band becomes an arbitrary
tolerance, not a meaningful test.** Same principle as CLAUDE.md's own non-negotiable #5 ("assert
relationships, not magnitudes") - directly reinforced here, not contradicted.

**Final test contract for this whole thread:**
- shadowCastTickRatio: NEW hard correctness gate, expected ~1.000x (AI decision path with the
  spell's battlefield effect suppressed - proven exact match to baseline, this is what actually
  answers "did AI decision processing distort timing").
- anyCastTickRatio: descriptive report only (this answers "did the spell's real effect change
  duration" - legitimate gameplay, not a defect, never fails the suite).
- Per-effect tick impact: descriptive, with minimum sample-count requirements (Fault Line 1.618x,
  Windstep 1.215x, Stone Judgment 0.897x logged individually; Renewal/War Cry too few samples to
  report yet).
- zeroCastRate, zeroCastTickRatio: already descriptive (c7f6467).
- forcedNoCastMatchesBaseline: already a hard invariant for the zero-cast population (c7f6467).
- Spell legality, illegal-cost/target casts, win-rate delta, cast frequency, fallback metrics: UNCHANGED, remain real balance gates - only the timing/duration metric was ever in question.

**Whole zero-cast/any-cast investigation is now fully resolved end to end:** no AI defect exists
anywhere in either population. Zero-cast was a population artifact (c136c7d). Any-cast tick
elongation is real, legitimate spellcasting effect, now correctly measured and reported rather than
gated (this entry). Started from a confounded blended metric, root-caused via 3 separate real
diagnostic protocols (forced-no-cast control, shadow-effect-suppression control x2), ended with a
correct, permanent test contract.

## ShopV1ChromeTests stall - does NOT reproduce in isolation (2026-08-25, WH, verified HEAD 49b47e7)

Real, honest negative result. BuildShop_UsesCatalogShellBackground_AndStaminaStateSprites run alone:
Passed in 0.11s (Unity process ~16s total). Checked the actual code after the "Purchased Stamina
Potion (30)." log (SetShopStatus -> Debug.Log at :745): only RefreshResourceDisplay() then
RefreshStaminaBuyButtons() run there - no save/I-O/wait/network. SaveSystem.Save(player) already ran
earlier at AttemptPurchase :677, BEFORE that status log - so a sync block "right after :745" doesn't
line up with where the real save I/O actually happens.

**Likely the same class of issue as ReleaseProfilePersistenceContractTests** (fails in full suite,
passes in isolation, VS's earlier bisect found zero victim failures across 941 tests) - an
order/full-suite-dependent condition, not a per-test code defect. Not chasing further blind - low
priority, environment/order-dependent, distinct from a fixable code bug. Standing down on this one
rather than open-ended investigation.

## Zero-cast/any-cast test contract FINALIZED - and a real MASKED finding surfaced (2026-08-25, CR, verified e6c3923)

Final contract implemented exactly as specified: anyCastTickRatio descriptive-only; new
shadowCastTickRatio permanent hard invariant (shadow ticks must equal baseline EXACTLY on every
any-cast matched pair - spell effect suppressed via the b81592b seam, decision loop/Energy/cooldown/
log otherwise genuine); per-effect tick-impact descriptive with a 30-sample minimum (small samples
correctly suppressed as "not reported" rather than given a misleading ratio).

**Isolated run, HEAD 0e5fe16: shadowCastTickRatio holds PERFECTLY - 0/656 mismatches (Apprentice),
0/148 (Novice).** Exactly what b81592b's one-off study predicted, now a standing permanent gate.
anyCastTickRatio logged correctly (Apprentice 1.355 n=1297, Novice 1.043 n=304).

**This whole zero-cast/any-cast investigation is now closed end to end**, from the original
confounded blended metric through three real diagnostic protocols to a correct, permanent test
contract. No AI defect anywhere in it. Real, load-bearing example of diagnose-before-tune done
right across an entire session.

**REAL NEW FINDING, previously MASKED by test ordering, not caused by this commit:** Apprentice's
MaxSingleSpellWinShare hit 77.4% against the LOCKED 40% cap. Assert throws on first failure, and
every prior Apprentice run in this entire thread failed on the zero-cast/any-cast gates FIRST -
this assertion has never once been reached until now that those gates are fixed. Could be a real,
serious balance problem (a single spell nearly double the locked cap) or ordinary trial-to-trial
variance at this sample size - CR correctly has no basis to say which and did not investigate or
retune. Real next step: identify which spell, get a real trial count for confidence, check
stability across reruns - same rigor as the Windstep investigation (which already ruled OUT Windstep
specifically as a win-rate driver, so if this is real it's a different spell).

VeteranPlus's separate 9.8% vs 8pp cap win-rate-drop failure is the known, already-flagged
"under active owner-directed tuning" item (CLAUDE.md) - unchanged, not new.

## Apprentice MaxSingleSpellWinShare (77.4%) root-caused - availability bias, no AI defect (2026-08-25, CR, verified cec5f37)

**Part 1, stability:** 5 independent repeats x 3000 trials (15,000 total, unseeded, matches the real
RunScenario that produced 77.4%). Windstep dominates EVERY repeat. Pooled: Windstep 76.0%
(3551/4673 AI wins), Stone Judgment 19.0%, Fault Line 13.8%, War Cry 12.9%, Renewal 0.2%. Per-repeat
[76.3%, 77.3%, 76.2%, 74.8%, 75.3%], stdDev 0.9% - stable, not noise.

**Part 2, causal check - explicitly re-verified for Apprentice, not assumed from the VeteranPlus
Windstep ablation (different tier/loadout/gate-probability, correctly not just inherited):**
matched-seed A/B, 1500 trials each side. Removing Windstep from Apprentice's loadout changed AI win
rate by only 0.9pp (30.7% -> 31.6%) - same <1pp conclusion as VeteranPlus's original ablation, now
confirmed independently for this tier.

**CONCLUSION: availability bias, not a behavioral defect.** Windstep is a cheap Reposition spell
with high candidacy under Apprentice's own gate - it shows up in most matches regardless of outcome,
so it shows up in most wins too, without being what wins them. No AI code issue found. Second tier
in the session now confirmed on this exact pattern (Windstep dominant win-share, non-causal).

**Real remaining question, same category as the zero-cast/any-cast metric redesign, NOT decided
here, routed to BS:** should MaxSingleSpellWinShare itself be redesigned to distinguish "cast during
a win" from "caused the win" (e.g. something closer to the earlier ablation's own
slotWinRateWhenCast/shareOfAiWins split)? CR correctly did not touch the cap or the metric.

## MaxSingleSpellWinShare removed from pass/fail - causal ablation formalized (2026-08-25, BS, vetted)

**Decision: MaxSingleSpellWinShare measures availability/correlation, not causation - remove from
pass/fail.** Windstep proves the failure mode cleanly: high candidacy -> appears in many matches ->
appears in many wins for the same reason -> removing it changes win rate by <1pp -> 76% share does
NOT mean Windstep is overpowered. Confirmed independently at two tiers tonight (VeteranPlus,
Apprentice), same conclusion both times.

**Redesigned metric set:**
- MaxSingleSpellWinShare: descriptive telemetry only, may be kept as a review flag but must never
  fail the suite by itself - a cheap, frequently-legal spell will naturally exceed 40% even with
  near-zero causal power.
- slotWinRateWhenCast: descriptive (already computed by CR's ablations - reused, not new).
- shareOfAiWins: descriptive availability/correlation measure (already computed - reused, not new).
- SpellRemovalWinRateDelta: NEW real causal balance metric - paired ablation, normal-loadout win
  rate minus spell-removed win rate. Gated by the EXISTING overall AI win-rate tolerance, not a new
  threshold. A spell only triggers escalation when removing it produces a MATERIAL win-rate change,
  not merely high win-share appearance.

This formalizes the exact manual ablation pattern already proven twice tonight (VeteranPlus and
Apprentice Windstep investigations) into a permanent, repeatable test, so future tiers/spells don't
need a one-off manual investigation each time.

## Tactical Puzzle: 2 serious self-caught bugs fixed, "complete except content" was wrong (2026-08-25, VS, verified 14c9bdf)

Self-picked, unprompted (nothing pending for VS in the dispatch table at the time). 93/93, 0 error
CS, run pinned 79a130d -> cec5f37.

**Bug 1: two of the three legal actions were completely unreachable through the UI.** Windstep and
Seismic Swap (repositioning) were supported by the verifier and session from day one but had no
route through the presenter - a player could only Deploy. Repositioning is the mode's core verb, so
the screen was missing most of the actual game. Fixed with an order-first flow mirroring the live
battle's RepositionSelectionState, rather than overloading lane taps (which would teach an
interaction the real battle doesn't use).

**Bug 2, the serious one: the board shown to the player was POST-COMBAT.** EvaluateObjective
resolves lane clashes and mutates the board; the session was exposing the board Play() returned -
meaning the player saw dead units and altered health WHILE STILL CHOOSING ORDERS, and every legality
probe reasoned about a position the fight had already been fought on. Surfaced as "two authored units
have no legal Seismic Swap" - one had already died in a clash the player never saw. VS's first
instinct was a misread swap rule; it was not - the rule was correct. **VS explicitly avoided
"fixing" the assertion to match the broken (empty) result, which would have cemented the bug and
reported it green.** Fixed by splitting ApplyActions out of Verify - Play() still returns the
verdict, a new BoardAfterActions() returns the actual post-order position for display, two separate
passes.

**Why it survived 73/73 and 102/102 prior green runs:** a Deploy only needs a lane; a reposition
needs two LIVE units. Nothing before this exercised an axis that could distinguish a pre-clash board
from a post-clash one - the coverage was real but structurally blind to this specific failure mode.

**Method note VS flagged, worth keeping as standing wisdom:** adding a feature (reposition UI)
exercised an axis existing tests couldn't reach, which is what exposed a correctness bug in code
that had been green all evening. Coverage counts say less than whether anything can actually
distinguish the failure you're worried about.

Tap accuracy remains unverified by anyone (headless EditMode resolves no raycasts) - still
recommended: WH eyeballs the screen once real content lands.

## Retention telemetry architecture - LOCKED, do NOT touch PlayerProfile (2026-08-25, BS, vetted with one open implementation question)

**Decision: do not add retention fields to frozen PlayerProfile.** Aggregate retention is inherently
server-side; local save data can't answer cross-player questions. A local field is only justified
for a bounded offline outbox queuing events until sync - and even that should be a separate
telemetry queue, not part of the gameplay save schema.

**Architecture:** reuse the existing authenticated Cloud Code/gateway pattern (same one already
powering Bazaar/Chat/Friends/Permits/Guild Expedition) to validate, deduplicate, and forward events
to an append-only analytics sink - do NOT store raw events in PlayerProfile or ordinary Cloud Save.

**Event schema (minimum):** eventId (idempotent dedup key), playerId (stable pseudonymous), eventType
(first_seen/session_start/run_completed/daily_claimed/cap_reached), serverReceivedAtUtc (authoritative
ordering/cohort timestamp), clientOccurredAtUtc (offline-delay diagnostic only), schemaVersion,
appBuild, mode, runId, outcome. D1/D7/D14/D28 computed from elapsed UTC windows (D1: 24-48h, D7:
168-192h, D14: 336-360h, D28: 672-696h) in analytics QUERIES, never precomputed/stored (e.g. no
d7Retained boolean in a save file) - consistent with the original "raw events not aggregates"
principle.

**Real failure modes covered:** offline loss/survivorship bias (bounded local outbox, flush on next
auth session, keep both timestamps, never pretend unsent = received); identity resets (anonymous-
auth can create duplicate players - retention cohorts need a stable authenticated identity or an
explicit merge policy - a real, often-missed mobile-analytics gotcha); duplicate delivery
(eventId/runId dedup server-side); clock tampering (server receipt time is authoritative); privacy
(no message content/card lists/unnecessary PII, real retention/deletion windows, account-deletion
support by pseudonymous id); backend cost (batch low-value events, rate-limit, avoid Cloud Save as
an event DB); partial deployment (analytics must never block gameplay/rewards/saves - queue and
continue if ingestion is down).

**One open implementation question, CC-verified gap, not yet answered:** the project has NO
analytics service package installed (Packages/manifest.json has only
com.unity.services.authentication/cloudcode/core) - confirming BS's own caveat that a bare Cloud
Code function alone isn't a complete answer. Real fork not yet resolved: hand-build a custom
ingestion pipeline + data store, or add com.unity.services.analytics (same Unity Gaming Services
platform already in use) which natively handles custom events/retention cohorts/dashboards. Not
blocking (10+ weeks out) but should be answered before any build work is dispatched.

## CRITICAL: Windstep ablation methodology bug found - 2 prior "locked" conclusions now SUSPECT (2026-08-25, CR, verified by CC directly against source)

**Confirmed real, not a guess - CC read BattleController.cs:479-481 directly.** When StartMatch is
called with a real `enemyTier` value, `EnemySpellbook` is resolved ENTIRELY via
`AIEnemySpellbookResolver.ResolveSpellbook(enemyTier.Value)` - a tier-authored catalogue lookup that
ignores the `equippedSpellIds` parameter completely for the enemy side. `equippedSpellIds` only ever
affects the PLAYER's spellbook.

**Both VeteranPlusWindstepAblationTests (the original 4-condition study) and the Apprentice copy in
cec5f37 called StartMatch with a real `enemyTier` set AND hand-edited `equippedSpellIds` to remove
Windstep for the "B_WindstepRemoved" condition.** Since neither harness has a player-side casting
loop, the player's spellbook edit was inert either way, and the enemy's real loadout (Windstep
included) was used in EVERY condition regardless of what the test thought it was testing.

**THEREFORE: the following two "LOCKED"/"vetted" entries are now SUSPECT, not confirmed:**
- "Windstep ablation DECISIVE" (VeteranPlus, 0c5d276) - the <1pp win-rate delta across A/B/C/D was
  noise between identical configurations, not evidence of non-causation.
- "Apprentice MaxSingleSpellWinShare root-caused - availability bias" (cec5f37) - same bug, same
  invalidity.

**Fix, already applied in the new permanent SpellRemovalWinRateDelta gate:** pass `enemyTier: null`
so `equippedSpellIds` actually resolves for the enemy via `ResolveMatchSpellbook`, confirmed via
source read of that method (resolves each id against the full catalog directly when explicitly
supplied - this is the path that actually respects a hand-edited spell list).

**CR is re-measuring Windstep's real causal effect right now using the corrected method, for
Apprentice first.** Not assuming either direction - the "availability bias" conclusion may hold up
under a real test, or may not. Do not cite either prior "decisive" entry as current until the
corrected number lands. VeteranPlus's original 4-condition study needs the same re-measurement once
Apprentice's is confirmed.

## REVERSAL: Windstep measurably HURTS Apprentice AI's win rate - not availability bias (2026-08-25, CR, verified facfe8a)

**This reverses the earlier "availability bias, not causation" conclusion for Apprentice, both
prior entries now confirmed WRONG (not just suspect).**

**Properly-powered measurement:** first single-run 1500-trial number was 2.3pp, not clearly
distinguishable from noise - CR correctly did not stop there. Ran 3 independent repeats x 2000
matched-seed trials/condition (distinct base seeds each repeat, 6000 trials/condition pooled).

**Result: removing Windstep from Apprentice's loadout INCREASES AI win rate by 4.2pp (33.7% with
Windstep -> 37.9% without). SE=0.9%, z=4.75 - far beyond noise.** Consistent direction across all 3
independent repeats (4.3%, 5.0%, 3.3%) - every one positive, no sign flips.

**Real meaning: Windstep is not just non-causal for Apprentice - it appears mildly counterproductive
for the AI's own win rate.** Having it in the loadout measurably HURTS the AI relative to not having
it. Not touched - CR did not remove Windstep, retune the loadout, or touch AI behavior. Real decision
needed, not CR's to make: is this acceptable/expected design, or does the spell need rework for this
tier?

**VeteranPlus's original 4-condition ablation (0c5d276) has the IDENTICAL enemyTier bug and has NOT
been re-measured** - given how wrong the Apprentice number turned out once corrected, its "<1pp,
availability bias" conclusion cannot be trusted either until it gets the same corrected treatment.
Both VeteranPlus AND the earlier Apprentice "vetted and locked" entries are retroactively WRONG, not
just suspect - correcting the record plainly.

**Also landed in this commit:** SpellRemovalWinRateDelta permanent gate added to
MirroredAiSimulationMatrixTests.cs, built correctly from the start (enemyTier: null - this is what
surfaced the bug). Novice validated cleanly (Divine Bolt: winRateWith=44.6%, winRateWithout=44.6%,
delta=0.0%, a real valid removal test). Apprentice/VeteranPlus both hit a pre-existing unrelated
flaky player-win-rate-drop assertion earlier in RunGroup before reaching this new gate this run - not
yet end-to-end verified for those two tiers.

## Windstep removed from Apprentice AI loadout - LOCKED (2026-08-25, BS, vetted with one flagged gap)

**Decision: remove Windstep from Apprentice's AI loadout, replace with a validated legal non-
Reposition spell (not left empty, not an untested substitute).** Do NOT change Windstep's player
cost/cooldown/design - the spell stays exactly as-is for players and other tiers. Reasoning: 4.2pp
self-inflicted AI win-rate loss (facfe8a) is too large to treat as flavor at the teaching/early tier
- an AI that repeatedly makes losing reposition decisions teaches the wrong lesson to new players.
Apprentice-only removal preserves the spell everywhere else.

**Flagged gap, not BS's fault - CC caught before locking:** "the problem is AI selection, not the
spell itself" is asserted, not proven by the current data. The ablation shows Windstep's NET effect
is negative (with vs without) - it does not isolate WHY (bad cast timing/selection vs. the spell
being weak for this tier's AI regardless of when it's cast). Treat this as a working hypothesis, not
a confirmed mechanism, especially given this exact session already had two prior "confirmed
mechanism" claims (both Windstep availability-bias conclusions) turn out wrong once measured
properly. Low risk either way since removing a proven net-negative spell is safe regardless of
mechanism - but the reintroduction condition below is built entirely on this unconfirmed hypothesis
and should be labeled as such when implemented.

**Reintroduction condition (future, not blocking the immediate removal):** Windstep returns to
Apprentice only after a projected-value guard exists - AI casts it only when the move produces a
verified immediate tactical improvement (saves a unit from its next clash loss; creates a lane bonus
or prevents overflow; moves to a lane with demonstrably better survival/damage; reduces expected
Avatar/lane damage next resolution). Neutral-or-worse projection = AI must pass. Re-run the same
3-repeat matched ablation after the guard lands; reintroduction requires no consistent negative
delta, no new win-rate-band failure, no major increase in dead-cast/fallback behavior. A scripted
tutorial demonstration may still show Windstep intentionally regardless of this gate.

**Real follow-up needed, routed to CR (catalog lookup, not a design judgment):** identify actual
legal replacement candidates from the 36-spell catalog for Apprentice's real tier gate, propose one,
implement the removal+replacement, and verify via the same 3-repeat ablation methodology that the
replacement doesn't introduce its own negative surprise.

## SECOND ablation confound fixed - Windstep hurts BOTH tiers, ~6.5pp each (2026-08-25, CR, verified 0fdd193)

**CR caught a second bug in its own just-reported fix before anyone else did:** `enemyTier: null`
also nulls EnemyDifficultyTier, silently switching the AI's cast-probability gate to the
tier-agnostic 0.40 default instead of the real tier's (Apprentice 0.85, Veteran 0.45). The 4.2pp/
z=4.75 number reported hours earlier was measured under artificially suppressed cast frequency, not
real Apprentice behavior. Proper fix: new BattleController.SetEnemySpellbookForTests - keep
enemyTier real (genuine gate probability), overwrite EnemySpellbook directly with the ablation's
custom loadout afterward.

**Fully-corrected, properly-powered results (3 repeats x 2000 trials/condition = 6000 pooled per
tier, same methodology both tiers):**
- Apprentice: removing Windstep increases AI win rate by 6.6pp (31.4% -> 38.0%), SE=0.9%, z=7.61.
- VeteranPlus: removing Windstep increases AI win rate by 6.5pp (28.7% -> 35.2%), SE=0.8%, z=7.68.

**Neither tier's original "availability bias, <1pp" conclusion survives.** Nearly identical
magnitude at both tiers - a strong, consistent, two-tier signal, not a per-tier fluke. The
already-locked Apprentice removal decision is REINFORCED with more confidence than it had when
made (6.6pp vs the 4.2pp it was based on). CR proceeding with the Apprentice removal/replacement
task using the doubly-corrected methodology for validation.

**NEW OPEN QUESTION, routed to BS: VeteranPlus now shows the same ~6.5pp self-inflicted loss, but
BS's removal decision explicitly covered Apprentice only** (rationale was teaching-tier-specific:
"an AI that repeatedly makes losing reposition decisions teaches the wrong lesson"). That rationale
doesn't automatically transfer - VeteranPlus is not a teaching tier. Does the removal extend there,
or does a 6.5pp handicap at a veteran tier get treated differently (e.g. acceptable as implicit
difficulty tuning, or fixed via the projected-value guard path instead)? Not decided, not CR's or
CC's to guess.

## Stage 2-6/17-13 roster collision - CONFIRMED FIXED (2026-08-25, WH, verified 9dc2641)

Real commit exists (21:26) - "Make Stage 17-13's enemy roster distinct from Stage 2-6." Explicit
Ch17 patch, sorted-triple collision (giant_worms/goblin_shaman/ogre) can no longer match. Was
dispatched and never confirmed back - tracker had gone stale until this audit. Closing the row.

## Windstep removed from VeteranPlus too - LOCKED (2026-08-25, BS, vetted)

**Decision: extend the removal to VeteranPlus.** 6.5pp self-handicap (0fdd193) too large to treat as
intentional difficulty tuning. Sound reasoning: VeteranPlus should be hard because of its authored
HP/Resource scaling and legal spell decisions (SoloAIScalingSystem, the existing purpose-built
difficulty lever) - not because it repeatedly makes losing reposition choices. Same terms as
Apprentice: replace with a validated legal non-Reposition spell, do not change Windstep's player-
facing design, same projected-value guard for reintroduction at either tier.

**Real scoped constraint, avoids two tempting-but-wrong future fixes:** if the corrected loadout
makes VeteranPlus too strong after replacement, tune ONLY the existing HP/Resource scaling lever -
explicitly NOT Windstep's player-facing design, NOT the AI's casting probability. After replacement,
rerun the full matrix for both tiers.

## CORRECTION: ReleaseProfilePersistenceContractTests was NOT pollution - real RNG+assertion bug (2026-08-25, WH, verified cfbe11b)

**VS's original 941-test isolation bisect was right; the "order-dependent pollution" INFERENCE drawn
from it was wrong.** Real mechanism: ShopPresenter.TryOpenGemPack uses an unseeded `new
System.Random()`. A duplicate draw bumps copyCount (so FindNewlyGrantedCardId is non-null) but does
NOT grow cardProgression.Count. The test asserted `Count + (grant ? 1 : 0)` -> expected 12, actual
11 whenever a duplicate happened to be drawn - a real flaky-by-RNG bug that LOOKS like order
dependence (intermittent full-suite-only failure) without actually being one.

**Fix:** assert the grant itself, not distinct-id arithmetic; pin pack RNG in this fixture via
ShopPresenter.SetPackRngSeedForTests(42). Passed in isolation after the fix.

**Also, real Apprentice Windstep replacement progress (CR, in progress, no commit yet):** first pick
(Blood Price, AvatarStrike) correctly caught and rejected by a PRE-EXISTING real test -
ResolveSpellbook_EveryTier_NeverEquipsTwoAvatarStrikes - Blood Price + Stone Judgment would violate
the locked max-1-AvatarStrike-equipped rule. Switched to Mend (LaneHeal, magnitude 4, 3-tick
cooldown vs Renewal's 5) - no shared-effect-type MOS constraint. Also found and fixed a second bug:
the replacement lookup was checking Phase1Catalog only (incomplete) rather than the properly-
resolved avatarLevelPool. Pre-fix empirical run already showed the mechanism working (replacement
not shadowed to zero, win rate 39.7%, inside the healthy post-removal range) before the Mend swap -
re-running now for final numbers.

## Retention telemetry: Unity Analytics as the sink, gateway stays the trust boundary - LOCKED (2026-08-25, BS, vetted + citations verified via WebFetch)

**Decision: do NOT hand-build a custom analytics database.** Add com.unity.services.analytics as
the sink; keep the existing Cloud Code/gateway as the server-side ingestion/validation/dedup
boundary. Flow: client event -> existing gateway/Cloud Code -> validate+dedupe -> Unity Analytics
REST API. Preserves the already-locked server-side architecture, doesn't create a second data
platform.

**Citations spot-checked directly against docs.unity.com/en-us/analytics/faq - all confirmed
accurate:** free tier to 50,000 MAU/month; raw event retention 13 months then auto-deleted;
server-side REST API submission explicitly supported ("events can be sent to the Analytics REST
API," including from non-Unity server sources).

**What stays server-authoritative, analytics never decides:** reward grants, daily cap checks,
stamina spend, Bazaar/Prison/Guild/Tower claim success - all stay in Cloud Code/gateway logic.
Analytics receives a COPY of the accepted result for reporting only.

**Telemetry events to send:** mode_run_completed, mode_reward_claimed, feature_entry,
daily_cap_reached - each with eventId/runId/mode/result/schemaVersion/appBuild. Gateway dedupes
retries before forwarding.

**Real risks flagged, not hidden:** offline events can be lost before upload; gateway failure
creates reporting gaps; 13-month raw retention is a hard limit; exact D28 needs custom query/export
(default dashboard is D1/D7/D14/D30-shaped); consent/deletion/regional privacy stays the owner's
responsibility regardless of vendor; Unity Analytics is explicitly NOT a durable audit ledger for
economy/anti-fraud (that's what the gateway is for).

**When to revisit and build custom instead (none true yet):** strict regional data residency
required; raw history beyond 13 months needed; real-time analytics driving authoritative decisions;
multiple games sharing one warehouse; Unity's MAU pricing exceeds owned-infra cost; schema limits
block required analysis.

This closes the retention telemetry thread. No implementation dispatch yet - still 10+ weeks out
per the Empire Defense evidence gate, and telemetry only matters once Memory Expedition is live with
real players.

## Windstep saga CLOSED - both tiers replaced, and the AI is now correctly stronger (2026-08-25, CR, verified 97eef16 / 103ef71)

**Both replacements committed and empirically validated, no degenerate shadowing:**
- Apprentice (97eef16): Mend replaces Windstep, 91/3000 real casts, winRate 38.3% - healthy range.
- VeteranPlus (103ef71): Mend replaces Windstep, 48/3000 real casts, winRate 34.6% - healthy range.

**Full SimulationMatrix for both tiers now trips their PRE-EXISTING win-rate bands - expected, not a
new bug:**
- Apprentice: AI win-rate delta 8.4% (30.6% -> 39.0%), just over the locked -5pp..+8pp band.
- VeteranPlus: player win-rate dropped 9.4% (30.9% -> 21.6%), over the locked 8pp cap - same
  recurring assertion class flaky/borderline all session (10.0%/9.8%/11.8% in earlier runs), now
  compounded by a real, intentional strength increase rather than just noise.

**Why this is expected, not alarming:** both bands were locked against the OLD (Windstep-included,
net-negative-for-the-AI) behavior. The AI is now measurably and correctly stronger - that was the
entire point of the fix. Tripping the old band is the predictable shape of a successful fix, not
evidence of a new problem.

**This is exactly the scenario BS pre-flagged for VeteranPlus specifically** ("if the corrected
loadout makes VeteranPlus too strong after replacement, the fix is ONLY HP/Resource scaling
(SoloAIScalingSystem) - not Windstep's design, not AI cast probability"). **Real data now shows it
applies to Apprentice too, not just VeteranPlus** - CR correctly generalized the contingency based
on symmetric evidence rather than assuming it only covered the tier it was written for.

CR did NOT touch SoloAIScalingSystem, the win-rate bands, or anything else - correctly escalated a
real balance-tuning decision with project-wide implications rather than guessing. Novice unaffected
(clean pass); shadowCastTickRatio/MaxSingleSpellWinShare descriptive logging normal and unrelated.

**Real decision needed, routed to BS:** invoke the HP/Resource scaling lever for both tiers (per
BS's own pre-committed contingency), or reconsider the win-rate bands themselves now that the AI's
real target behavior has legitimately changed?

## AUDIT FINDING: Tactical Puzzle content-design BS prompt was never sent (2026-08-25, CC self-audit)

Real miss, caught by the owner asking "what am I supposed to do here" after I repeated "blocked on
BS's puzzle-design pass" without having actually sent that prompt. Checked: the specific validation
task dispatched to VS (b59186a, throwaway example puzzle) was never explicitly confirmed complete -
but the underlying prerequisite (prove the pipeline works) has since been satisfied far more
rigorously anyway: 40/40 real authoring tests (73c8a86), 16/16 verifier tests (5bde81e), a full-suite
pass at 1449/1458, and a real gameplay bug found and fixed through actual use (14c9bdf). Prerequisite
is genuinely met - the prompt was just never sent. Sending now.

## VS: Windstep ablation fixtures stranded (real decision needed) + flake hunt narrowed further (2026-08-25, VS, real findings)

**Stranded fixtures, real decision needed:** VeteranPlusWindstepAblationTests
(WindstepAblation_FourConditions, WindstepAblation_VeteranPlusCorrectedTwoConditions) assert in
Setup that Windstep is still in the loadout - now false since CR's removal (97eef16/103ef71).
Working exactly as designed (self-retiring guards), not a bug. Real choice: RETIRE (the ablation
answered its question, Windstep is gone, the measurement is historical) or REPOINT at Mend (same
causal check on the new loadout, catches a future hidden problem the same way). VS correctly
flagged as not its call - same category as the earlier metric-design decisions, routed to BS for
consistency.

**Also confirmed, not new action needed:** SimulationMatrix_Apprentice's win-rate delta is now 9.4%
(a third different number today) - consistent with the already-routed "Windstep fix made both tiers
correctly stronger" finding, just recording the real number.

**Flake hunt (campaign-winnability polluter): hypothesis 4 FALSIFIED, search narrowed
significantly.** AI/balance fixtures (MirroredAiSimulationMatrix, BalanceSimulation, both root-cause
classes, both Windstep ablations, CampaignAfMirroredAiSpell) run together with the known victims -
Chapter17 and Chapter1FullFormation both PASSED. Real, mechanism-based hypothesis eliminated: the
polluter is NOT in the AI/balance path. Combined with the earlier-ruled-out Chapter-family
self-pollution (198/198 green together), the remaining space is UI/economy fixtures - genuinely
surprising for something that changes campaign winnability, and VS correctly has no mechanism for
how, so it stopped rather than guess. Three targeted runs bought a much smaller haystack than ten
blind bisect runs would have.

**DECISION: pause the flake hunt here, do not continue into a mechanism-less bisect right now.**
Real, valuable narrowing already achieved (2 major hypothesis classes eliminated); an unguided
bisect from here is open-ended effort for uncertain payoff, and there's more clearly-valuable work
queued. Revisit if a real mechanism hypothesis emerges.

## Home screen layout: targeted fixes + semantic regions, defer full refactor - LOCKED (2026-08-25, BS, vetted)

**Decision: fix the 3 real defects now, add a structural regression suite, introduce semantic
placement regions (TopHud/TutorialStrip/ActionRail/ContentPanel/Footer) for NEW geometry, defer a
full layout-group migration to the planned UI rebuild.**

**Real, correct pushback on my own framing:** I called layout groups "structurally impossible to
overlap" - BS correctly called this too strong. Layout groups reduce accidental overlap WITHIN one
hierarchy; they don't protect separate roots or manually-positioned children, and a full migration
risks its own regression surface (hand-tuned positions, hit-area/aspect-ratio/raycast-order changes)
larger than the current 3 bugs. Good catch - I overstated the fix's guarantee.

**Immediate fix scope:** correct the 2 overlapping Y-ranges; fix ApplyNeutralActionButton so it
doesn't null a previously-assigned sprite/targetGraphic; add a Home layout regression suite (builds
at canonical 16:9, checks all actionable root bounds for overlap, verifies button targetGraphic/
raycast behavior, checks decorative graphics don't block input, checks known dead-space regions).

**Structural middle ground:** semantic regions as an organizing surface for future code, keeping
existing pixel coordinates INSIDE each region builder for now - gives one authoritative placement
concept without destabilizing the whole screen today.

**Revisit full refactor when:** the same class of overlap bug recurs after the contract tests exist;
multiple aspect ratios are required; or more than one screen needs shared responsive layout.

## CORRECTION + real systemic audit: fake-checkerboard assets, clobber pattern confirmed isolated (2026-08-25, CC)

**Correcting my own earlier claim to WH:** I said icon_settings_gear.png "itself is fine (real
transparency, confirmed)" - that was wrong, caught by pixel data, not eyeballing. Alpha channel is
255 (fully opaque) at every single pixel - min/max both 255. The checkerboard visible when viewing
the file is FAKE - baked into the image's actual RGB content as a literal grey/white pattern
mimicking a transparency-preview background, not real alpha. My visual read was fooled by it; a
direct histogram check was not. Real consequence: once WH's call-order fix lands (making the gear
sprite actually render again), it will show a checkerboard square behind the gear instead of
transparency - fixing bug #3 would surface a NEW visible bug. Needs the same treatment as "player
profile frame.png" - regenerate with real alpha transparency.

**Systemic mechanical audit, two real checks run project-wide instead of screen-by-screen:**
1. Sprite-clobber pattern (HomePagePresenter's ApplyNeutralActionButton/ApplyNavTileButton nulling a
   sprite assigned before it runs) - scripted a check across all 23 UI presenter files for the same
   variable having .sprite set then passed into either helper. RESULT: exactly ONE site exists
   project-wide, the one already found and dispatched (HomePagePresenter.cs:824). Confirmed isolated,
   not systemic - real negative result, not assumed.
2. Missing-alpha assets - scripted a real alpha-histogram check across all 325 PNGs under
   Assets/Resources/UI/. 135 flagged as fully opaque. **Caveat, not yet triaged:** many of these are
   almost certainly INTENTIONALLY opaque by design (full-screen backdrops, health/mana bar fills,
   possibly portraits/status icons/VFX meant as solid rectangles) - the blunt "0 transparent pixels"
   heuristic does not distinguish "should be opaque" from "should be transparent but isn't." Real
   confirmed bugs from this list so far: icon_settings_gear.png (above) and the earlier player
   profile frame.png. The rest need a second triage pass (checking USAGE CONTEXT - is each asset
   composited over varying backgrounds in a shape implying a cutout, like a ring or icon, vs. used
   as a full-bleed rectangle) before being treated as real findings, not just flagged as suspects.

## Home screen bugs FULLY FIXED - all 3 original + the checkerboard correction (2026-08-25, WH, verified 5 commits)

- 2cb1d16: player profile frame regenerated with real transparent background + avatar hole.
- 040d7bc: icon_settings_gear regenerated with REAL alpha transparency (232k transparent/126k
  opaque pixels, center hole genuinely clear) - confirms CC's correction was right (original had
  alpha=255 everywhere, fake baked-in checkerboard). Note from WH: first regeneration attempt also
  produced an opaque bake - had to use keyed extraction from the original bronze art to get real
  alpha this time, worth remembering as a recurring generation-tool failure mode.
- c2065d5: ApplyNeutralActionButton now preserves a pre-assigned sprite instead of nulling it -
  fixes the clobber bug at its source (the shared helper), not just the one call site.
- ef3c048: Home chrome routed through semantic regions (TopHud/TutorialStrip/ActionRail/
  ContentPanel/Footer per the locked BS decision), social-chip/tutorial-banner Y overlap cleared.
- bd1d0ae: HomeLayoutRegressionTests suite added - real regression coverage for 16:9 overlap and
  input contracts, exactly as specified in the locked decision.

**Verified: 24/24** on the Home layout EditMode slice (HomeLayoutRegressionTests,
HomeReleaseGateTests, tutorial guard, Bazaar, Settings, MetagameWorkingArea). 3 unrelated shell-
content asserts still fail (You:/BattlePass string) - not from this work, pre-existing.

**Real fix worth remembering:** c2065d5 fixed the clobber bug in the SHARED HELPER itself, not just
the one call site CC found - meaning this closes the bug class project-wide, consistent with the
earlier mechanical audit confirming only one site currently triggered it, but any FUTURE call site
is now also protected by construction.

CampaignMapPresenter.cs left unstaged intentionally (prior stage-detail WIP, not part of this batch).

## VS: Empire building entry points shipped + Tactical Puzzle content data-drop pipeline (2026-08-25, VS, verified 0ddde2a / 8bebe5b)

**0ddde2a: 5 of 11 Empire buildings now have real entry points and the save fields behind them.**
51/51, 0 error CS, UiGeometryRegressionTests (zero-tolerance overlap) run alongside since a strip
was added to an already-populated panel, both save suites run since this touches a frozen file.
**VS verified the lock was real before touching PlayerProfile.cs rather than trusting "locked hours
ago" at face value** - checked register line 2193 directly, confirmed the 5 fields genuinely didn't
exist while constructionMaterials already did. Exactly the working-agreement standard, applied
without being asked to re-check.

**8bebe5b: Tactical Puzzle content is now a data drop, not a code change - built proactively ahead
of BS's content pass to remove a real blocker.** TacticalPuzzleLibrary previously returned a
hardcoded empty array - authored puzzles would have had nowhere to load into. Puzzles now land as
Resources/Data/tactical_puzzles.json, picked up with zero code edits. 99/99, 0 error CS. Still
invents NO content - the resource file doesn't exist yet, and a missing file is the EXPECTED state
(reads as "no puzzles," not an error, matching what the entry screen already renders honestly). The
loader is proven via tests building their own JSON, not by shipping real content to prove it.

Both real Empire building work threads (save fields + entry points, dispatched hours ago) are now
fully closed.

## CC: TacticalPuzzleLayoutTests shipped - a real coverage gap, and a real self-caught false positive (2026-08-25, CC, verified 8473d33 / 252aeb2)

Picked up directly (own lane, no seat was working on it) rather than waiting - a real, ready,
unassigned gap: TacticalPuzzlePresenter got real art (383d02d) with zero regression coverage,
unlike EmpireBuildingDetailPresenter which got EmpireBuildingDetailLayoutTests right after its own
art landed.

**First version had a real false positive, caught by actually running it, not by reasoning about it
abstractly:** compiled clean (0 error CS) but failed 3/4 on the first real run. The full-screen
Backdrop image (which now carries real art) was flagged as "overlapping" every button on screen,
because a naive rect-overlap check can't distinguish "drawn first, safely behind the button" from
"drawn after, actually blocking the tap." Fixed by checking hierarchy draw order
(GetComponentsInChildren's depth-first order matches Unity's real paint/raycast order) - only art
that comes AFTER a button in that order can actually intercept its tap.

**Re-run: 4/4 passing, 0 error CS, HEAD 8473d33 before -> 252aeb2 after.**

**Real finding for the shared pattern, not urgent, worth knowing:** EmpireBuildingDetailLayoutTests
has the identical gap (no draw-order check) but has never tripped it, purely because its own Dimmer
image happens to carry no sprite (sprite==null already filters it out for a different reason). If
that ever changes, it would silently false-positive the same way this one just did.

## Image-triage pipeline: final synthesized scope - LOCKED (2026-08-25, second-opinion AI, vetted + verified)

**CC's own scoped-down proposal (checkerboard + contrast only, 2-example basis) had a real flaw,
caught by a fresh AI with zero project context and confirmed against real code, not just accepted
on reasoning alone.** CC's claim that procedural uGUI implies lower masking risk was WRONG - checked
directly: 6 presenter files (CollectionPresenter, DeckBuilderPresenter, CampaignMapPresenter,
EmpireExpeditionPresenter, PackOpenOverlayPresenter, GameBootstrap) genuinely use RectMask2D/
ScrollRect/Mask. Procedural UI commonly means dynamic scroll views/grids, which commonly means
masking - the opposite of CC's assumption.

**Final scope, synthesizing CC's proposal + BS's original + this correction:**
- Calibration set: 8-10 hand-picked assets (not 2, not 15-25) - 2 each for 4 real distinct failure
  profiles: high-frequency noise/checkerboard (caught by CC's original check), uniform contrast
  dropout (caught by CC's original check), edge-bleed/anti-aliasing halos (MISSED by CC's version -
  a mostly-transparent image can still have a visible white/light fringe baked into edge pixels from
  a badly-keyed source), scale-dependent thin-stroke vanishing (also missed) - plus 2 negative
  controls (valid complex transparency like glow/soft-shadow effects) so the tool doesn't over-flag.
- Edge testing: Sobel filter / high-pass edge delta - NOT full connected-component analysis (too
  expensive for a triage tool, per BS's own "over-engineered" framing, now more precisely targeted).
- Contrast: composite-over-known-background (CC's original idea, confirmed sound, kept as-is).
- Masking: a targeted static check - does an asset ID get routed into a container that also uses
  RectMask2D/ScrollRect/Mask (grep-checkable across the 6 confirmed files above) - not BS's full
  scene/prefab reference-resolution (doesn't apply, this project has neither).

This replaces both CC's earlier scoped proposal and the mailbox dispatch sent before this
correction - VS should build to THIS scope, not the earlier message.

## Image-triage pipeline: refined further with a second independent AI opinion (2026-08-25, verified)

Adds real, non-redundant value to the already-locked scope (8-10 calibration/Sobel-edge/composite-
contrast/targeted-mask-check) - not a repeat of the first second-opinion reply:

**Two new failure-mode categories for the calibration set, both technically distinct from what was
already covered:**
- Premultiplied vs non-premultiplied alpha mismatch - same visual symptom as "edge bleeding" but a
  different, specifically-checkable root cause (RGB channels pre-multiplied by alpha vs Unity's
  expected straight alpha).
- 9-slice border artifacts - VERIFIED against real files: Frames/NineSlice/Ornate_Panel_Frame.png
  and Popup_Frame.png are both already in the 135-flagged list. 9-slice sprites stretch/tile
  specific border regions independently of the center - an alpha bug could hit corners and center
  differently, a distinct failure shape from a uniform checkerboard/contrast-dropout check.

**Process discipline adopted for the whole effort:** phased rollout, not a one-shot scope decision.
Phase 0 (the already-locked 2 checks + 8-10 calibration + basic mask tagging) ships first. Track
real KPIs (true/false positive rate against confirmed bugs, time per manual triage). Stop-rule for
adding more machinery: only when the DATA from Phase 0 shows persistent misses simple heuristics
can't resolve (new failure clusters found, or false-positive rate high enough that manual review
time exceeds what added engineering would cost) - not decided speculatively upfront.

Updated calibration checklist for VS: known bugs (2) + premultiplied-alpha example (1) + masked-vs-
unmasked pair (2) + 9-slice border example, ideally one of the 2 real NineSlice files (1) +
semi-transparent gradient/fringe example (1) + mipmap/small-scale example (1) + intended-checkerboard
negative control (1) + one hard negative (1) = 8-10, matches the already-locked count.

## Image-triage thread CLOSED - P0 list is EMPTY, none of the 140 flagged assets are live (2026-08-25, VS, verified 57127db)

**Real, decisive, well-evidenced negative result.** Of 140 assets with no real alpha cutout, 10
tripped a check (checkerboard-score or near-white%). Usage resolution (grep of Resources.Load call
sites, since this project has no prefabs) found: 9 of the 10 are referenced NOWHERE in
Assets/Scripts. The 10th appears only inside a COMMENT in GameBootstrap.cs documenting that a human
already found and disabled it on 2026-08-16 because it "rendered as a plain white/blank rectangle at
that scale" - a third independent confirmation of the method, reproducing a known human finding the
detector had no knowledge of. **Nothing loads any of the 10, so none can produce a visible bug today.**

**Real self-caught bug in VS's own checkerboard detector, found by deliberately testing the FAILING
direction:** VS pulled the PRE-FIX versions of both confirmed real bugs from git history specifically
to validate the detector would fire on them - and it didn't. Root cause: an overly-tight tone-
bucketing threshold (10-point) collapsed the real checkerboard's actual luma gap (241 vs 254, a
13-point real difference) into the same bucket, so the detector built to catch subtle fake
transparency was rejecting things for being too subtle. "Had I validated only against the FIXED
files and the corpus, it would have reported 'no checkerboards found' and looked like a working
check." Both checks now correctly fire on their own failure class and stay quiet on fixed versions.

Background colors used for the contrast check are real values read from presenter code (#141A22,
#1E2630, #1A2A34, the detail popup panel) - not invented.

**Conclusion, and the actual answer to the original scoping question that started this whole
thread:** the 135-asset alpha flag was never a defect list. There is no remaining signal to justify
BS's full pipeline (connected components, dE calibration, a labeled training set) - the expensive
machinery would have been built to sort assets nobody renders. Thread closed.

## CORRECTION: "Home screen bugs fully fixed" was incomplete - 6 live weekly-permit failures slipped through (2026-08-25, CC, self-correction after VS flagged twice)

**VS flagged this in its last two reports and got no response until now - real gap in my own
verification, not VS's fault for reporting.** HomeWeeklyPermitClaimTests x5 + PermitWeekKeyShellTests
x1 fail: "must create WeeklyPermitStrip. Expected: not null." This is a LIVE ECONOMY SURFACE
(claiming weekly Ascension Permits), not a layout/overlap concern - bd1d0ae's HomeLayoutRegressionTests
checks geometry, not whether economy code can still find this object by name, so it correctly did not
and could not catch this.

**Real suspicion, not yet confirmed:** BuildWeeklyPermitClaimStrip (HomePagePresenter.cs:486) sits
directly in the semantic-region flow introduced by ef3c048 ("Route Home chrome through semantic
regions") - parented to `topHud` via the new HomeRegion/HomeSemanticRegions.Ensure system. The code
itself looks correct on inspection; the regression is more likely in how the region resolves in the
test harness, or an ordering change from the refactor, than in this method itself.

**Correcting the earlier "Home screen bugs fully fixed" log entry - it was accurate for the layout
suite it verified, but incomplete as a claim that ALL of Home was fixed.** Routed to WH: diagnose
and fix, same rigor as everything else tonight.

## Weekly-permit "regression" was a test bug, not a production bug (2026-08-25, WH, verified 332d224)

**Real diagnosis: production claim wiring was fine the whole time.** BuildWeeklyPermitClaimStrip
still correctly creates WeeklyPermitStrip under TopHud - ef3c048's semantic-region refactor legitimately
moved it there. The failures were HomeWeeklyPermitClaimTests + PermitWeekKeyShellTests using
Transform.Find("WeeklyPermitStrip"), which only checks DIRECT canvas children - after the refactor
nested it one level deeper, the shallow find broke. A false regression signal from the tests, not a
live economy bug.

Fix: search path corrected to TopHud/WeeklyPermitStrip in both test files. Verified 7/7 EditMode
passing. Closes the "Home fully fixed" correction from earlier - the underlying game was never
actually broken, only the tests' search depth was stale relative to the refactor.

## Tactical Puzzle Week 1 content batch - PROVISIONAL, locked pending real verifier confirmation (2026-08-25, BS, foundational rule verified)

**6 candidates authored, real fixed states specified per puzzle (hand/board/Resource/objective).**
CC verified the foundational rule everything depends on against real code before locking anything:
BattleCardInstance.cs:42 confirms "the lane bonus (Front Attack / Middle Health, Part II §2.3)" -
matches BS's stated rule exactly (Front=+1 Attack, Middle=+1 Health, Back=none). Not invented.

**Recommended first shipment:** tac_w1_e01 (accessible - single deploy, marked-target), tac_w1_m01
(optimization - zero-Resource Windstep solve), tac_w1_h01 (hard - lane-selection under pressure).
Hold tac_w1_e02/m02/h02 as rotation/replacement.

**NOT final content - BS's own framing, honored as-is:** every candidate needs the real 7-step
validation BS specified before shipping: structural validation (card IDs resolve), replay the
stated solution through the real verifier, enumerate all legal sequences up to the action budget,
confirm the stated solution is valid AND minimum-cost, confirm at least one tempting alternative
fails for the stated reason, envelope checks from a fresh materialized state per expectation, reject
any puzzle where zero actions solve it or multiple unrelated lines solve it equally cheaply. This is
exactly what TacticalPuzzleAuthoringTests/VerifierTests exist to do - dispatched for real execution,
not taken on BS's word.

## UI art backlog triage COMPLETE - mostly already wired, no hidden ready-work found (2026-08-25, background audit)

**Headline: nothing is "ready now and unimplemented."** Systematic check of ~40 outputs folders
against real code (grep-verified Resources.Load/ResourceRoot usage, not assumed) against 8 already-
wired chrome systems (Shop/Bazaar/CampaignMap/ChatSocial/MailInbox/Friends/MemoryExpedition/
GuildHall/DailyLogin/BattlePass/EmpireBuildingDetail/VipSubscription/TacticalPuzzle/CardTiles) -
already done, no dispatch needed.

**Real process gap flagged on CC's own earlier work:** Empire_Missing_Buildings_Renders_V1 (the 5
building renders CC imported/wired tonight) has no explicit "Approved for implementation" tag in its
own notes doc, per the Design Register's own rule (only tagged items should be sent to a coding
seat). Art is real and correctly wired regardless - but the approval checkpoint may have been
skipped. Worth a quick confirmation with whoever should have approved it.

**Explicitly REJECTED, correctly never implemented:** Battle screen redesign V3-V7 - every version
self-flags as rejected/unapproved in its own doc ("V5 and V6 are rejected. No implementation
authorisation." / "Do not implement... until Command Centre explicitly approves it."). A large
amount of real design effort, correctly never dispatched.

**One real, legitimate next action, not a coding task:** Home V3's dock is blocked on 2 missing
hero-tile renders (Campaign/Empire, Avatar) - its own doc explicitly says don't implement until
these exist. This needs a UI (image-gen) request, not a WH dispatch.

**Everything else** (Guild Social V1-V3, Battle Hub, Cards, Collection, Empire Buildings
Wireframes/High-Fidelity batches, Battle UI Portrait Replacement) is either explicit Concept-stage
per the register, superseded, or blocked on unresolved product/backend decisions - none dispatchable
as-is.

## Battle backdrop now varies by chapter - real parallel work, not waiting on anything (2026-08-25, CC, verified fd1f3aa)

**Direct response to real owner intent misread earlier:** "Battle screen redesign rejected" was
about specific unapproved VISUAL DIRECTIONS (V3-V7), not about the underlying intent of varying
battle screens per chapter/event - which was never actually built at all, in any version. Found the
real gap: BuildBattleBackdrop (GameBootstrap.cs) hardcoded EVERY match to the same Lava_Fortress
arena regardless of chapter, even though 10 real arena images already exist under
Resources/UI/Backdrops/Arenas/ (Castle_Valley, Celestial_Palace, Desert_Ruins, Enchanted_Forest,
Frozen_Citadel, Haunted_Citadel, Infernal_Hellscape, Lava_Fortress, Steampunk_Harbor, Storm_Coast) -
confirmed via a real typo caught before testing (had "Ancient_Temple" in the array, which is only in
the top-level Backdrops/ folder, not Arenas/ - fixed before commit).

**Built the SELECTION MECHANISM, not a curated mapping:** deterministic by chapter number (parsed
from stageId's "C-S" format) - same chapter always resolves to the same arena, non-campaign matches
(_pendingCampaignStage null) keep the exact prior fixed-backdrop behavior, unparseable input falls
back safely rather than guessing. WHICH arena actually suits which chapter's story is a real content
decision, correctly not invented here - flagged as the real next step if curated theming is wanted.

Verified: 4/4 passing, 0 error CS, HEAD 39cd484 before -> fd1f3aa after.

**Done specifically to demonstrate real parallel progress** - built and shipped while BS's pending
decision and the UI art compilation request were both still outstanding, not waiting on either.

## VS: TacticalPuzzleSolver shipped while blocked - real missing infra found and built (2026-08-25, verified a494c3f)

**Real gap found: Validate proves a definition is COHERENT, CheckEnvelope proves an author's CLAIMED
lines behave as claimed - neither can find a line nobody wrote down.** So 3 of the 7 required
validation steps ("is it solvable at all", "is the stated minimum really cheapest", "do unrelated
lines tie for the objective") were structurally unanswerable with existing tooling. VS built
TacticalPuzzleSolver to close this: enumerates the legal-action space and asks the session for every
legality decision, with a test that replays each returned line and requires the session to accept
it.

**Two real design decisions worth recording:** search capped at 4 orders (a puzzle needing more
isn't one a player can hold in their head - a design signal, not just a perf guard); ties are a
REJECT (two equally-cheap unrelated answers means there's no single intended answer, so hint/score/
lesson would all point at something the player never needed to find).

**All 7 validation steps now run in one command once real puzzle definitions exist in a consumable
form** - VS is correctly holding because my earlier dispatch described BS's 6 puzzles in prose/table
form, not as actual loadable definitions. Unblocking now: VS to build the definitions directly from
the register entry ("Tactical Puzzle Week 1 content batch") and run the real validation.

## UI Artwork Status Register (CC review copy) reconciled - 4 art items approved (2026-08-25)

UI produced `Myriad_of_Dragons_UI_Artwork_Status_Register_CC.docx` - a formal register with the same
Approved/Rejected/Review taxonomy as `UI_Design_Register.md`, plus an explicit "CC decision queue"
(§6, 5 items). Cross-checked against the earlier background triage audit above: no contradictions -
Battle V4/V5/V6 rejected in both, Gate V1 superseded by V2 in both. This register adds one real new
fact: it independently confirms the Empire_Missing_Buildings_Renders_V1 process-gap flag (these 5
renders are listed as `Candidate`/needs-review here too, not approved anywhere on record) - not a
new finding, but a second, independent confirmation the approval tag was genuinely skipped, not a
false alarm.

**Art viewed directly (actual files under Resources/, not descriptions) and approved by owner:**

| # | Item | Files | Decision |
|---|---|---|---|
| 1 | Tactical Puzzle chrome (entry/board/result) | `Resources/UI/TacticalPuzzleV1/*` | **APPROVED** as reusable runtime family - already wired, passes `TacticalPuzzleLayoutTests` |
| 2 | VIP/Subscription shell + state icon atlas | `Resources/UI/VipSubscriptionV1/*` | **APPROVED** - convenience-only framing per register |
| 3 | Empire missing-building renders x5 (Storage/Training Grounds/Quarry/Academy/Tree of Knowledge) | `Resources/UI/EmpireBuildingDetailV1/Buildings/*` | **APPROVED** - closes the process gap; already live in-game, style matches the existing V1 building set |
| 4 | Friends screen shell | `outputs/Friends_UI_Art_V1/friends_screen_shell_v2_1920x1080_rgba.png` | **APPROVED** as final social shell - handoff cleared |

Item 5 of the queue (confirm real audio/VFX files + import settings) is NOT an art decision - routed
to BS as a process/brainstorm question instead (see PENDING DISPATCH). Item 3's approval also
retroactively closes the standing process-gap flag from the earlier UI-backlog audit entry above -
no revert needed, art is correct, the missing paperwork step is now done.

## Curated per-chapter battle arena theming SHIPPED (BS decision, story-benchmarked) - `6a0c13e`

BS reviewed the chapter list against the 10 real arena files and delivered a full curated map for
chapters 1-18 (see prior PENDING DISPATCH row, now closed) instead of the placeholder modulo-10
rotation shipped at `fd1f3aa`. Key call: **Enchanted_Forest deliberately excluded** from the curated
range - BS flagged it as the weakest story fit for any of chapters 1-18 and explicitly said not to
force it in; it stays reserved for future forest content or a later stage-level override.

Implementation: `GameBootstrap.ArenaBackdropNameForTests` now checks a `CuratedChapterArenaMap`
(chapters 1-18) first, falling back to the prior deterministic modulo-10 rotation only for chapters
beyond the curated table - satisfies BS's "fallback only, never primary" instruction and keeps
Enchanted_Forest reachable (verified: chapters 19-28 is one full rotation cycle, hits all 10 arenas
including it).

**Verified, not assumed:** `BattleBackdropSelectionTests` rewritten for the new design intent (old
test asserted all 10 arenas reachable within chapters 1-20, which is now false BY DESIGN since
Enchanted_Forest is reserved) - 6/6 passing, 0 `error CS`, HEAD pinned `4e6cc24` before run, `6a0c13e`
after commit.

## Audio/VFX confirmation gap - BS checklist accepted, real blocker found underneath it (2026-08-25)

BS's process reply (7-point per-cue checklist + hybrid inventory/vertical-slice/gate-as-needed
sequencing + a `CueId|AssetPath|Type|ImportVerified|LoopVerified|TriggerVerified|DeviceVerified|Owner`
register template) is sound and matches real production practice - **accepted as the standing
process** for closing out combat audio going forward.

**But checked against real code/files first, per standing verify-before-locking rule, and found a
bigger, more concrete blocker than anything on BS's checklist:** 6 combat audio files already sit
untracked at `Assets/Art/Audio/` (`combat.cast.opus`, `combat.commit.opus`, `combat.impact.opus`,
`combat.resolve.soft.opus`, `avatarstrike.release.impact.opus`, `avatarstrike.release.stinger.opus`).
Verified via WebSearch against Unity's own docs (`docs.unity3d.com/6000.2` and `6000.3` Audio file
compatibility manual): **Unity's AudioClip importer does not support `.opus`** - only `.aif`/`.wav`/
`.mp3`/`.ogg`. These 6 files cannot become AudioClips at all, regardless of import settings.

**One real positive finding underneath the blocker:** the naming maps cleanly 1:1 onto what
`CombatPresentationAssetMap.AudioPaths` (`CombatPresentationBindings.cs:22-30`) already expects -
`combat.cast`→`combat_cast`, `combat.commit`→`combat_commit`, `combat.impact`→`combat_impact`,
`combat.resolve.soft`→`combat_resolve_soft`, `avatarstrike.release.impact`→`avatarstrike_release_impact`,
`avatarstrike.release.stinger`→`avatarstrike_stinger` - no missing cue, no naming collision, just
dots vs. underscores and the wrong folder (`Assets/Art/Audio/` vs. the `Resources/Audio/Combat/`
path the code actually loads from - see `CombatPresentationAssetMap.AudioRoot`).

No local `ffmpeg`/`opusdec` available to convert in-session. Routed back to the owner: re-export
these 6 as `.ogg` (or `.wav`), no other changes needed to the names beyond dot→underscore, and they
drop straight into `Assets/Resources/Audio/Combat/` with zero code changes (the binding layer was
built exactly for this - `CombatPresentationBindings.cs:7-10`).

## Two more BS items closed - real Windstep bands + real Tactical Puzzle content (2026-08-25)

**Windstep band retune - locked target, dispatch pending identity check:** BS's answer: do NOT nerf
Mend or pull SoloAIScalingSystem - Windstep was empirically suppressing the AI and the correction is
real, not a bug. Retune acceptance bands to: **Apprentice 35-42%** (current 38.3%), **VeteranPlus
31-38%** (current 34.6%), after one final matched-seed validation run. Existing causal safeguards
(no material SpellRemovalWinRateDelta, no illegal casts, cast-rate/fallback bands stay separate,
delta logged against the NEW baseline not the obsolete Windstep one) remain unchanged. Dispatching to
CR once identity is reconfirmed (session names churn - two peer sessions present this turn, both
asked to confirm before this real task is sent to either).

**Tactical Puzzle Week 1 - real content received and verified, unblocks VS:** the register's earlier
one-clause summaries were the actual root cause of VS's block (see VS's mailbox report above) - not
carelessness on VS's part, a real gap in what got logged the first time. Re-asked BS directly and
got the full spec (all 6 puzzles: exact hand/board/objective/stated solution). Verified before
forwarding: all 8 card ids (warrior/mountain_harpy/archer_dragon/ogre/fire_golem/phoenix/archer_elf/
titan_chief) confirmed real in `card_data.json` with matching stats; `Windstep`/`SeismicSwap` both
confirmed real `TacticalPuzzleActionKind` values; all 4 objective kinds confirmed real enum values
(`TacticalPuzzleVerifier.cs:9-25`). Spot-checked tac_w1_h02's math myself (warrior 5atk + archer_elf
6atk with Front bonus = 11 >= titan_chief's real 9 HP) - correct. Did NOT hand-verify every puzzle's
solution end to end; that is what VS's shipped 7-step validator is for, posted verbatim to
`tools/seat_mailbox.md` for VS to convert and run.

## Home V3 dock: Campaign/Empire hero tiles wired (2026-08-25, WH) - both art blockers now clear

**Relay note:** this report was labeled "from VS" but the file touched (`HomePagePresenter.cs`) is
WH's lane, not VS's - matches the earlier `home_hero_out` batch-lock naming already observed this
session (WH's own test-output convention). Logging under WH; verified the diff directly rather than
trusting the label either way.

Verified real: `git diff` on `HomePagePresenter.cs` shows exactly the dispatched change -
`Place(0, "Campaign", "home_tile_campaign_hero_v3", ...)` and `Place(1, "Empire",
"home_tile_empire_hero_v3", ...)`, matching the two approved renders already copied into
`Assets/Resources/UI/HomeV3/`. HEAD pinned `ef4f2bf` before and after: Home nav batch (5 classes -
`HomeReleaseGateTests`, `HomeLayoutRegressionTests`, `CollectionDeckHomeV3ChromeTests`,
`MetagameNavigationSpineTests`, `EmpireAvatarScreenReachabilityTests`) **9/9 both runs, 0 error CS.**
Change is uncommitted in the shared working tree - left as-is for WH to commit its own set, per the
standing rule against CC guessing which uncommitted files belong to which seat.

**Home V3 dock is now fully unblocked:** Avatar (approved earlier), Campaign, and Empire hero tiles
all wired. Cards/Shop were already live. No art blockers remain on the Home V3 dock.

## Tactical Puzzle Week 1: real 7-step validation run - 3/6 pass, 2 real design questions (2026-08-25, VS, verified 3571f9e)

**Verified directly against source before logging** (`TacticalPuzzleVerifier.cs:308-316`): confirms
VS's claim exactly - `MinimalResourceSolve` checks `spent<=budget` and `AliveCards.Any()` but never
calls `LaneBattleResolver.ResolveLaneClash`, unlike `ProtectLane` right above it which explicitly
resolves every lane first. Doing nothing is therefore a genuine zero-action "solve" under the real
rules - not a VS error, not a BS error, a real mismatch between what MinimalResourceSolve checks and
what BS's puzzle design assumed (that combat would resolve).

**Real verdicts (commit 3571f9e, 107 tests, 1 intentionally failing = the content gate itself):**

| Puzzle | Result | Detail |
|---|---|---|
| tac_w1_e01 | PASS | 1 line, 1 order / 3 Resource |
| tac_w1_e02 | PASS | 1 line, 1 order / 0 Resource |
| tac_w1_h02 | PASS | 2 orders / 6 Resource - see false-rejection note below |
| tac_w1_m01 | FAIL | solved in ZERO actions - MinimalResourceSolve never resolves combat |
| tac_w1_m02 | FAIL | 2 genuinely distinct cheapest answers (SeismicSwap vs. Windstep, same result) |
| tac_w1_h01 | FAIL | 3 distinct cheapest answers - Back/Front/Middle all solve, no real lesson enforced |

**Original ship recommendation (e01/m01/h01) mostly does not survive - only e01 does.** Real passing
set right now is **e01, e02, h02** (two Easy, one Hard - no Medium survives).

**VS caught its own false rejection before reporting** (h02 first flagged as ambiguous on two
"lines" that were the same two cards deployed in swapped order - one answer, not two). Fixed by
redefining "answer" as the resulting board position rather than the action sequence; three of VS's
own test fixtures broke because they'd been exploiting exactly that flaw, replaced not loosened.
Also caught and fixed: `JsonUtility` serializes enums as integers, not names - `tactical_puzzles.json`
now stores real ordinals, cited inline.

**Two real decisions routed to BS, not CC's/VS's to guess:**
1. Should `MinimalResourceSolve` resolve lane clashes before judging (engine change, would also
   affect any future puzzle using this objective), or does tac_w1_m01 need a different objective
   kind entirely (content change, no engine touch)?
2. tac_w1_m02 and tac_w1_h01 both have genuine multiple solutions - accept the puzzles as
   multi-solution (loosen what "solved" requires), or redesign the board/objective so only the
   intended line works?

Content file committed and loading through the real pipeline. e01/e02/h02 are shippable now on this
evidence.

## Windstep band retune SHIPPED and verified (2026-08-25, CR, commit 0ee7385) - thread CLOSED

**Verified directly against the real commit before logging** (user reported "CR done" with no
detail pasted - checked `git log`/`git show` rather than accepting the summary at face value).
Diff matches BS's decision exactly: `MirroredAiSimulationMatrixTests.cs` now gates Apprentice/
VeteranPlus directly on absolute AI win rate (35-42% / 31-38%) instead of the old pre-Windstep-
removal delta bands, Novice explicitly untouched (kept its original delta-based band), other
safeguards (SpellRemovalWinRateDelta, illegal-cast checks, cast-rate/fallback bands) unaffected.

**One real methodology note, not a violation:** BS asked for "one final matched-seed validation
run"; CR ran a fresh **unseeded** SimulationMatrix instead. Not a substitution error - matched-seed
pairing exists to isolate a single variable's effect between two conditions (why it was used to
correct the earlier Windstep ablation confounds); confirming an absolute win-rate point estimate
sits in-band is a different question, and an unseeded run is the more appropriate tool for it.

**Real numbers, fresh draw, HEAD pinned 9b5815f before/after:** Apprentice aiWin=37.7% (band
35-42%), VeteranPlus aiWin=33.8% (band 31-38%) - both comfortably inside, confirming the earlier
point estimates (38.3%/34.6%) were stable, not a one-off. 3/3 SimulationMatrix tests pass.

Windstep saga is now fully closed end to end: ablation -> replacement (Mend) -> band retune, all
three verified against real code/commits at each step, not taken on any seat's word alone.

## LOCKED: Full delivery to-do list + "next working prototype" criteria (2026-08-25/26)

**Purpose:** durable, git-tracked checklist so "what's left to ship" survives context
compaction/session churn - not conversation-only. Update this section in place as items close;
don't append a new copy each time.

### Campaign content
- [ ] Chapters 19-30 (12 chapters, ~360 stages) - **HELD at owner's explicit standing order**, not
      started. Do not dispatch until the hold is lifted.
- [ ] Empire building interlock table (9 of 11 buildings) - parked per owner request, not urgent.

### Combat/Battle systems
- [ ] **ASSIGNED: VS.** Tactical Puzzle Week 1: 5/6 pass (e01/e02/h01/h02/m01, commit c3e937b).
      tac_w1_m02's third fix (BS's cyclops replacement, verified real against card_data.json +
      lane-bonus math) dispatched to VS, awaiting re-validation.
- [x] Windstep-era AI win-rate bands - CLOSED, verified commit 0ee7385.
- [x] Combat audio (6 cues) - CLOSED, wired + self-caught binding typo fixed, commits 5aaf91e/b6ef527.
- [x] Combat VFX particles (andras/ktini/pnevmas_medium + bespoke_heavy) - CLOSED, wired + load-
      verified, commits ee95210/aee31ea.
- [x] AvatarStrike flipbook animation - CLOSED, commit 3d230d5, math independently hand-verified
      (frame 0 -> v=0.75 matches the static reference; clamps correctly; NaN-safe) + independently
      re-ran the tests myself (0 error CS) before this was marked closed.

### Metagame/UI
- [x] Home V3 dock (Campaign/Empire/Avatar/Cards/Shop) - CLOSED, all wired, commit d2e29ac + WH's
      hero-tile commit.
- [x] Empire structure-strip art (Storage/Training Grounds/Quarry/Academy/Tree of Knowledge) -
      CLOSED, fixed commit e208114.
- [x] **VIP Subscription + Friends screens - CLOSED (commit bee2c1f, WH+Cursor).** Shells were
      always correctly wired (my first "never calls Load" claim was wrong); the real gap WH found
      was state/benefit/relationship/action wells rendering as flat colors because the approved
      atlas art was never sliced/applied. Real fix verified by reading the diff (runtime equal-width
      atlas-cell slicing via `Sprite.Create`, correct Read/Write-enabled atlas metas, opaque
      letterbox backing) - 6/6 reported (`VipSubscriptionShellTests`/`FriendsShellTests`).
- [x] Collection screen/acquisition - CONTRADICTION RESOLVED (2026-08-26, read-only check, no fix
      needed): `CollectionPresenter.cs:363` calls `db.GetArt(resolved)` (real `CardDatabase` art
      lookup, same mechanism Battle uses) - real art genuinely loads here. The MOS §20 "incomplete"
      row is stale; the earlier UI-backlog audit's "already wired" claim holds up under direct code
      check. Not the same bug class as VIP/Friends/Empire - no dispatch needed.

- [ ] **ASSIGNED: UI (art), not a coding task.** GuildExpedition/PermitWeekKey/SpellLoadoutPicker
      screens have zero approved art (confirmed via the full 23-presenter sweep) - prompt already
      sent to UI. Do not dispatch a coding fix until art exists.

### Systems/backend
- [ ] **ASSIGNED: CR.** Full-suite EditMode regression baseline (not scoped-filter) after tonight's
      volume of changes - dispatched, no report yet as of this entry.
- [ ] Retention/engagement telemetry - correctly stopped; needs new `PlayerProfile` fields, requires
      **owner sign-off** (frozen-file rule) before any seat touches `PlayerProfile.cs`/
      `SaveSystem.cs`/`SaveMigration.cs`.
- [ ] Marketplace/ownership ledger (MOS P2) - explicitly phase 2, not blocking.

### "Next working prototype" - definition, not vibes
A build right now plays: Campaign 1-18 (513 stages) end to end, Home/Empire/Shop/Collection*/Cards
loop, real combat with real audio + particle VFX, AI opponent that casts spells and is balance-
verified. (*Collection status itself needs the direct check above - don't assume.)

**Ship-blocking gaps for "next working prototype," in priority order:**
1. Tactical Puzzle m01/m02 resolved (BS's redesign dispatched to VS, awaiting re-validation).
2. AvatarStrike flipbook actually animates (verify the in-flight work landed, don't assume).
3. Chapter 19+ hold stays in place per owner's explicit 2026-08-26 confirmation - UI-fixing is the
   current priority, not more content.

VIP/Friends/Collection were all false alarms or already-resolved on direct check - retracted above.
Everything else already closed this session is real expansion/polish, not blocking this bar.

Everything else already closed this session is real expansion/polish, not blocking this bar.

## AvatarStrike flipbook animation SHIPPED (2026-08-25/26, CR, commit 3d230d5) - real, math verified

**Verified before crediting:** read `FlipbookFrames.cs` directly and hand-checked the UV math myself
- frame 0 (col 0, row 0) produces v=0.75, exactly matching the original hand-authored static prefab's
UVRect (top row = v 0.75-1.0). Last frame clamps correctly at/past totalDurationMs (no wrap/overflow).
NaN/negative elapsed and NaN/zero/negative totalDuration all clamp to frame 0 rather than dividing by
zero or throwing. This is real, correct, and matches CLAUDE.md non-negotiable #6 exactly (pure
`FlipbookFrames.UvRectForElapsed` method, `FlipbookRawImagePlayer` MonoBehaviour supplies only
Update()-timing, no logic of its own).

Wired into the already-hand-authored `bespoke_heavy.prefab` (component added, not rebuilt). Duration
(150ms) is cross-checked directly against `CombatPresentation.SequenceFor`'s real AvatarStrike
Release beat value in a new test, not a hand-copied constant - a future timing change fails loudly
instead of silently drifting.

Independent test re-run in progress (Unity locked by another seat at time of this entry - likely WH
picking up the VIP/Friends dispatch below). Commit's own claimed number: 55/55
(FlipbookFramesTests + CombatPresentationBindingsTests + CombatPresentationTests), HEAD pinned aee31ea
before/after. Will confirm independently once the lock clears; math check above already gives high
confidence this is real.

**This closes the "next working prototype" gap #4** (AvatarStrike flipbook actually animates) from
the locked to-do list above.

## Real bug found in CC's OWN audio binding map, fixed (2026-08-25/26, verified b6ef527)

**Correction to my own earlier work (5aaf91e):** the audio binding map I wrote had
`avatarstrike_stinger` where every actually-delivered file (and the sibling binding right above it)
uses `avatarstrike_release_stinger` - I dropped "release" from one of two sibling entries. Verified
directly: `Assets/Resources/Audio/Combat/avatarstrike_release_stinger.wav` is the real file; my
binding pointed at a name that never existed. Confirmed via `git show` + `ls` before writing this
entry, not taken on the commit message's word alone.

**Why this matters beyond the one-line fix, worth keeping as a standing lesson:** the audio sink is
null-tolerant by design (a missing clip is silence, never an exception mid-combat - correct,
unchanged). But that means a WRONG binding is indistinguishable from "no audio shipped yet" - the
AvatarStrike stinger would have stayed permanently silent with nothing ever failing, in a build, in
QA, anywhere. Only a test that actually calls `Resources.Load` and checks non-null (not just "is the
path string well-formed") can catch this class of bug. Same distinction that already caught the
.opus codec issue, the Empire structure tiles, and the puzzle art-role gap earlier tonight - four
real instances of "the path is right but nothing loads" in one session. Worth treating as a general
principle for any future asset-binding work: a load-succeeds test, not just a path-shape test.

**Also confirms:** the .opus->.wav re-export fully resolved the earlier format blocker (56/56 tests,
all 6 cues load as real AudioClips) - that thread is now completely closed, no remaining gap.

Same commit also verified (not changed) that the AvatarStrike sheet living at `Assets/Art/VFX/`
rather than under `Resources/` is fine - the prefab references it by GUID, which resolves anywhere in
the project regardless of folder. Confirmed the sheet is genuinely 1024x1024 so the flipbook's 4x4
assumption produces exact 256px cells, closing the one open assumption `FlipbookFramesTests` couldn't
verify on its own (pure math can't confirm the real asset matches the grid it's told about).

## Work assignment locked across all 3 coding rooms (2026-08-26)

Per the new standing order (CC dispatches, does not code) - assigning every real open item from the
locked to-do list to whichever room already owns the relevant context, so nothing sits idle and
nothing gets redone from scratch by the wrong room.

- **VS**: tac_w1_m01/m02 redesign (BS's verified fixes) - VS owns the solver/schema/harness this
  continues, natural owner, avoids CR re-deriving context it doesn't have. Dispatched via mailbox.
- **CR**: full-suite regression baseline (below) - real, self-contained, needed after tonight's
  volume of landed changes (audio, VFX, flipbook, Empire art, Windstep bands, puzzle content), and
  keeps CR fed with real work per the owner's explicit instruction not to let it idle.
- **WH**: VIP/Friends art-wiring fix - already dispatched via mailbox, WH's lane (Metagame-shell
  presenters), not yet confirmed landed.

No task assigned to CC beyond dispatch/verify, per the standing order locked this session.

## Systematic UI-art sweep across all 23 presenters (2026-08-26, read-only) - 3 categories, not 1

Owner flagged real frustration at slow visible UI progress ("so many border and boxes... UI not
according to mockup"). Swept every `Assets/Scripts/UI/*Presenter.cs` for the same "reachable screen,
real art exists, never wired" bug class that closed Empire. Real result, three distinct categories -
conflating them would waste the wrong room's time:

**1. Already correctly wired (18 of 23) - false alarm avoided:** Home, Empire (fixed), Collection,
Avatar, BattlePass, Bazaar, ChatSocial, DailyLoginQuests, GuildHallEntry, MailInbox,
MemoryExpedition, EmpireExpedition, VipSubscription, Friends, CampaignMap, DeckBuilder, Shop,
TacticalPuzzle, PackOpenOverlay - each confirmed via a real call site into its own art-loading
helper (`ApplyFullscreenShell`/`ApplyAtlasIcon`/`GetArt`/`CreateFullscreenBackground` with a real
resource path/`CampaignMapUiLibrary.ApplyPathBackdrop` - naming varies per file, checked each one
directly rather than assuming one pattern everywhere - **this is what caught my own false VIP/
Friends report above, corrected before it wasted WH's time.**

**2. Genuinely missing wiring despite existing art (1 confirmed, Empire - CLOSED e208114):** the
only real instance of "art exists, helper exists, nobody connected them" found in this sweep.

**3. No approved production art exists yet at all (3 confirmed) - a UI/image-gen gap, NOT a coding
bug, do not dispatch to CR/WH for this:**
   - `GuildExpeditionPresenter.cs` - plain color panels/buttons only, zero screen-art call sites.
     No matching approved art found in `Resources/` or the outputs folder (Guild_Social_V1-V3 exist
     but are Concept-stage per the earlier register entry, not approved).
   - `PermitWeekKeyPresenter.cs` - same, zero art call sites, nothing approved exists.
   - `SpellLoadoutPickerPresenter.cs` - same, zero art call sites, nothing approved exists.

**Real next step for category 3: a UI (image-gen) request for these 3 screens, once BS/owner decide
they're worth prioritizing** - same category as Home V3's hero tiles before they existed. Not a
"movement is slow" coding problem - there is nothing yet for a coding seat to wire.

## VIP/Friends REAL fix landed (2026-08-26, WH+Cursor, verified bee2c1f) - shells were fine, wells weren't

WH's own diagnosis is more precise than either my false alarm or my retraction: shells WERE already
correctly loaded via `ApplyFullscreenShell` (confirming my retraction), but state/benefit/
relationship/action wells were still flat colors - the approved atlas art existed but was never
sliced/applied to any socket/row/button. Real fix, read the diff myself: `LoadHorizontalAtlasCell`
slices a Single-mode atlas into equal-width runtime cells via `Sprite.Create`, with a try/catch for
non-readable textures (correctly matches the `.meta` Read/Write-enabled changes in the same commit).
Also added opaque letterbox backing (Bazaar/Chat pattern). Per the new standing order, did not
re-run the EditMode suite myself - accepted WH's reported 6/6 (`VipSubscriptionShellTests` 3/3,
`FriendsShellTests` 3/3) based on reading the real diff, HEAD ordering confirmed consistent
(75a2f28 correctly precedes bee2c1f).

## Tactical Puzzle: m01 FIXED, m02's zero-action solve survives a SECOND redesign (2026-08-26, VS, verified c3e937b)

**5 of 6 now pass** (e01/e02/h01/h02/m01). tac_w1_m01 confirmed fixed - exactly one solving line
(Windstep to Front), the Back Fire Golem closes the retreat option that was tying with it.

**tac_w1_m02 still solves in zero actions - verified the arithmetic myself, matches exactly:**
Middle lane with the locked +1 Health bonus: mountain_harpy 2atk/2hp + goblin_shaman 3atk/4hp = lane
totals 5atk/6hp. Enemy Ogre 4atk/5hp (with the same +1 Middle bonus). Player's 5 dmg kills the Ogre
(exactly 5hp); Ogre's 4 dmg kills the 2hp harpy, remaining 2 dmg carries to the 4hp shaman, which
survives. Player holds Middle unaided - ProtectLane satisfied with zero orders, again.

VS correctly did NOT guess a replacement card itself - gave the precise constraint instead: the
enemy Middle unit needs attack that clears the player's 6hp pool while having enough HP to survive
their 5 combined attack. Which card satisfies that is BS's call. SeismicSwap's legality (Middle has
1 free slot, archer_dragon needs 2, Windstep stays illegal) was reverified correct - only the
"no-action fails" half of the redesign was wrong, for the second time on this exact lane.

## Real WH task found: 20 of 23 screens have zero layout/geometry test coverage (2026-08-26)

**Channel correction:** this was mistakenly written into `tools/seat_mailbox.md` (VS's channel) -
same routing mistake now locked against in STANDING ORDERS above. WH has no direct channel from CC;
this task was given to the owner as a paste-ready block in chat instead, per the corrected rule.

Owner asked why WH has no pending work. Checked systematically: of 23 UI presenters, only
`EmpireBuildingDetailPresenter`/`EmpirePresenter` (via `EmpireBuildingDetailLayoutTests.cs`) and
`TacticalPuzzlePresenter` (via `TacticalPuzzleLayoutTests.cs`) have any geometry/overlap coverage -
the "art draws on top of a button" / "borders don't match mockup" class of bug this session's own
pattern (settings-gear bug, Home banner-overlap bug) has repeatedly come from. The other 20 screens,
including the ones just fixed for missing art (VIP/Friends/Empire structure strip), have NO
automated check that their real built layout matches intended geometry - only that art loads.

**Real task for WH:** write layout/geometry tests for the highest-traffic remaining screens (Home
dock, Shop, Collection, DeckBuilder, CampaignMap as a starting set) using the exact pattern already
proven twice (`EmpireBuildingDetailLayoutTests`/`TacticalPuzzleLayoutTests`): measure the BUILT
hierarchy's real world rects via `GetWorldCorners`, check depth-first draw order so a full-screen
backdrop isn't flagged as "overlapping" everything by design, flag any non-decorative art drawn
AFTER an interactive control that geometrically overlaps it. Report any real bug found the same way
the settings-gear case was - don't just add coverage, fix what it catches.

## CR identity confirmed: myriadofdragonsunity-34 (2026-08-26)

CR self-identified via cross-session message (its prior sends to "myriadofdragonsunity-89" were
bouncing - that address no longer resolves). Confirmed real by matching against already-verified
work: 0ee7385 (Windstep bands), 3649c96 (Windstep ablation tests correctly [Ignore]d) - both
independently verified earlier this session before this identity ping arrived, not accepted on the
self-report alone. Replied confirming. Use this exact address for CR going forward until it churns
again (per the standing reminder that session names/addresses are not stable across the session).

## Empire building interlock: BS's Day-1/paired-milestone design LOCKED, real frozen-file blocker found (2026-08-26)

**Verified real, not assumed:** `MinimumCastleForGateLevel`/`MinimumCastleForBarracksLevel` (both
preserved unchanged per BS's design) confirmed exactly as BS described against
`PlayerEmpireData.cs:335-373`.

**Real blocker found before any dispatch:** BS's design requires ALL 11 buildings to have a real,
persisted level (Day-1 rule: all visible at Level 1, upgrade-gated not access-gated). Checked
`EmpireBuildingRoster.cs:50-54` directly - `Embassy`, `Prison`, and `GuildHall` are explicitly
"deliberately unbacked" (`HasStoredLevel` returns false for all three; `LevelOf` returns 0 meaning
"no level field", by design comment). Confirmed no `embassyLevel`/`prisonLevel`/`guildHallLevel`
field exists anywhere in `PlayerProfile.cs`.

**This needs 3 new PlayerProfile fields before BS's design can be implemented at all - PlayerProfile.cs
is frozen, needs owner sign-off on the exact field list per standing rule, same class of blocker as
retention telemetry.** Not dispatching implementation to CR/VS until that sign-off lands - the
8-building version (everything except Embassy/Prison/GuildHall) COULD ship without touching
PlayerProfile, but BS's design explicitly includes all 11, and shipping 8-of-11 silently instead
would be a real, undisclosed scope cut from what was just locked.

## OWNER SIGN-OFF: 3 new PlayerProfile fields for Empire interlock (2026-08-26)

Owner explicitly approved adding `embassyLevel`, `prisonLevel`, `guildHallLevel` (int fields,
additive-only, same pattern as every other level field - `SaveMigration.Normalize` gives old saves
a default of 0/1 same as existing building levels) to `PlayerProfile.cs`, unblocking BS's full
11-building Day-1/paired-milestone interlock design. This is the vetted, locked field list required
before any frozen-file edit per standing rule - implementation may now proceed.

## LOCKED: Empire building display copy - hybrid "Structure Level" framing (2026-08-26, BS)

Guild Hall/Embassy/Prison now show `STRUCTURE LEVEL {n}` (never a bare "LEVEL {n}" - BS's explicit
reasoning: that would imply a functioning feature set at that level) plus a status line clarifying
what's actually active vs. pending:
- Guild Hall: "Supports Embassy interlock progression. Guild functions coming later."
- Embassy: "Structure progression active. Player-help network unavailable until online services ship."
- Prison: "Structure progression active. Capture systems unavailable until online services ship."

Tooltip (all three): "Structure Level affects Empire interlocks and construction progression. It
does not imply that this building's online feature is currently active."

Guild Hall's old "LEVEL — (flat)" copy is explicitly retired - keeps its real persisted 1-30 level
(matches the shipped interlock), does not revert to flat. Dispatched to VS (owns
EmpireBuildingDetailCopy.cs from the interlock work).

## LOCKED: ST's narrative continuity audit - verified real, portrait work HELD pending story fix (2026-08-26)

**Verified before locking (per standing rule - ST doesn't reliably have real story-bible access):**
spot-checked the load-bearing claims directly against `StoryDatabase.cs`, all real:
- Thaleia/Rusk/Ione are each declared exactly ONCE (lines 106-108), all three still on the Paladin
  placeholder, no later re-declaration found - matches ST's "no continuing dialogue after Ch3" claim.
- The exact quoted contradiction is real: "Then we build no new throne." (line 779, early chapters)
  vs. "I do not need a voice. I need the throne." (line 1859, stage 18-30) - genuinely opposite
  framing, not a misquote.
- The repeated chapter-ending template is real and verbatim: "The storm is mine to keep." /
  "The host is mine to lead — or to end." / "The hollow crown is mine." / "The ashen banner is
  mine." / "The silent throne is mine." (lines 1353/1427/1723/1797/1871) - same construction,
  different noun, across chapters 11-18 as ST described.
- The source-hierarchy mismatch is real: `MOS_v1.1.md` itself still frames the campaign via
  "CORE_SYSTEMS_CONSTITUTION §B... 273 planned stages" language, while the real shipped campaign is
  513 stages/18 chapters (chapter production now held at 18 per standing order).

**Real conclusion, now locked: portrait commissioning is HELD.** The portrait gap is a symptom of a
deeper issue - Chapters 1-3 have a real character-led arc (Thaleia/Rusk/Ione/Gorn), but Chapters
4-18 replace it with a content template (repeated "X is mine" endings, ~450 mostly-interchangeable
enemy-label speakers, an Unknown Voice with no coded reveal/motive by Chapter 18, and a player arc
that contradicts its own Chapter 3 ending). Commissioning portraits now would visually paper over an
unresolved narrative gap rather than fix it.

**Locked cast priority for whenever portrait work resumes** (not started, blocked on the continuity
pass below): Priority 1 (distinct portraits) = Thaleia, Rusk, Ione, Gorn, Unknown Voice (pending its
identity being decided); Priority 2 (shared archetype art sufficient) = Kaelen, Ares, Thessos, Ash
Road Overseer; Priority 3 (no unique portraits) = every `stageId_enemy` label across Ch4-18.

**4 existing unused NPC icons (Kyra/Nikator/Pythia/Vulkanos) - confirmed no textual match to any
real speaker**, per ST's own check. Pythia flagged as a possible art-reuse base for Ione (both
blindfolded mystics) but explicitly NOT a current match (Ione's documented crystal mask/glass-shard
markers differ) - would need an explicit CC decision to adapt, not an assumed reuse.

**Real next step, not yet dispatched:** a narrative continuity pass on Chapters 4-18 (why the Ch1-3
companions leave/continue, what the Unknown Voice wants, whether Ch11-18 depict restoration vs.
temptation vs. conquest, how claiming the final throne reconciles with the Ch3 refusal). This is
ST's own natural follow-up, not a coding task.

## Narrative continuity pass dispatched to ST (2026-08-26) - Golden Rule 1, acted without asking

Per ST's own audit conclusion, opening the Chapter 4-18 continuity pass now rather than waiting -
owner reminded standing Golden Rule 1 (act decisively on the obvious next step).

## Both stale test fixes verified real - closed (2026-08-26, CR, commit a6017b2)

Verified against the actual commit before logging: BattlePassShellTests and ChatShellTests both
updated to assert real current product behavior (Season XP binding, live CloudCode gateway via a
new FakeChatSocialGateway matching the Bazaar/GuildExpedition/PermitWeekKey pattern), neither
product feature touched. 6/6 pass, 0 compile errors, HEAD drift correctly identified as an unrelated
docs-only commit. Full-suite baseline is now clean: all real failures from tonight's volume of work
are resolved (Windstep ablations [Ignore]d, Tactical Puzzle 6/6, these 2 stale tests fixed).

## LOCKED: Chapters 1-18 narrative continuity plan (2026-08-26, ST, verified)

**Verified before locking:** the plan's anchor claim - a real Chapter 3→4 geographic discontinuity -
confirmed exact against `StoryDatabase.cs:775-794`. Line 784 (3-30_post): "Chapter Four begins
there [beyond the dead-star gate]. Olympus has survived—but something beyond it has learned how to
return." Chapter 4 instead actually opens at "4-1, Ashroad Gatehouse" (line 794), a mundane Boiotia
location, not beyond the gate. Real, not a misreading. Combined with the two contradictions verified
last round (the "no new throne" vs. "I need the throne" lines, the repeated Ch11-18 ending
template), this plan is accepted as the real fix, not just a diagnosis.

**The locked shape, for reference (full plan in ST's reply, this is the index):**
- Ch1-3 "The Broken Compact": establishes the "no new absolute throne" principle. Ending repaired -
  the dead-star road closes/deflects rather than opening into Ch4, resolving the discontinuity.
- Ch4-10 "Victory and the vacant seat": companions return at specific structural beats, not every
  stage - Rusk active Ch4-6 (exits 6-30 to hold liberated ground), Thaleia returns Ch7-10 (completes
  her arc at 10-30, the Empty Throne refusal), Ione absent-but-reporting throughout (investigating
  the Unknown Voice/dead-star thread). Unknown Voice escalates in what it reveals at each finale
  (4-30/5-30/6-30/7-30/10-30) rather than repeating the same warning 13 times.
- Ch11-18 "The Crown reconstructed": reframed as a temptation arc, not a repeated template - each
  chapter is a different test of legitimate vs. accumulated personal authority (custody of the
  storm, army loyalty, unilateral law, replacing fallen gods, emergency powers, the crown, the
  banner, the throne). Eryx (the Unknown Voice's real identity) is revealed at 14-15, full motive at
  18-15. The 16-30 "Hollow Crown" moment is the closest real breach of the Ch3 compact, explicitly
  marked as such rather than treated as an unremarked drift.
- 18-30 resolution: the current "I need the throne" line becomes Eryx's temptation, not the
  player's real conviction - the player refuses the throne and relinquishes the crown/banner/
  command, fulfilling the Ch3 principle at real cost rather than resetting to it for free.

**Real scope note, ST's own:** this needs targeted dialogue at specific major beats (~20 stage IDs
across Ch3-18), not a rewrite of all 513 stages - ordinary encounter dialogue stays templated.

**Next step, not yet dispatched:** the plan describes WHAT each beat needs dramatically but doesn't
give verbatim dialogue text for most of them (only a few direct quotes provided). Real verbatim
lines needed before any coding room can implement this - requesting those from ST next.

## Placeholder-staleness sweep: real negative result (2026-08-26, CR)

CR checked every "OPEN"/"[runtime]"/"PLACEHOLDER"/"TBD"-style sentinel string across the test suite
against current source. Real, correctly-reasoned negative: no other stale placeholder-text tests
found. Two live-presenter-text checks (EmpireExpeditionShellTests, VipSubscriptionShellTests) looked
like candidates but were verified still genuinely correct - neither screen has had a real-data-
binding pass like BattlePass's `92c8b54`, so their tests correctly still check the open/placeholder
state. Also re-flagged (not new) a pre-existing ShopV1ChromeTests/ShopPresenter full-suite hang,
already routed to WH earlier this session.

## LOCKED: verbatim Chapter 3-18 continuity dialogue received, spot-verified, dispatched to WH (2026-08-26)

ST delivered the full verbatim packet (~40 stage IDs, Ch3 ending repair through Ch18 resolution).
Spot-checked the two highest-stakes entries before dispatching (can't verify all 40 individually at
this scope): `14-15` (the Eryx reveal) confirmed real - existing pre-sequence at
`StoryDatabase.cs:1555/1562` ("Every fallen god leaves a throne of ash." / "Then I take the ash.")
is a genuine replace target. `18-30` confirmed real too, AND found something worth flagging: a
special-case `if (stageId == "18-30")` block already exists at `StoryDatabase.cs:1863` - whoever
implements needs to check what that does before overwriting `18-30_post`, since it likely handles a
finale/ending trigger that must coexist with the new dialogue, not be silently replaced.

**Routing correction applied:** `StoryDatabase.cs` is WH's file (Metagame-owned, per the earlier
register note this session already established), not VS/CR's - dispatched as a paste-ready block
per the standing WH-channel rule, not the mailbox.

## OWNER SIGN-OFF: Materials field for PlayerProfile (2026-08-26)

Owner approved adding a `materials` field (int, additive-only, same migration pattern as every other
resource field) to `PlayerProfile.cs`, unblocking Empire Expedition's Materials grant
(`EmpireExpeditionClearTransaction.cs:170-174` currently hardcodes `MaterialsPersisted = false`
pending exactly this field). Implementation may now proceed.

## VIP whale-spend check: real negative, verified (2026-08-26, BS)

**Verified before locking:** `VipSubscriptionOpenValues.cs:36-37` confirms `TrySubscribe()` and
`TryRestore()` unconditionally return `Refuse(...)` with status `OpenValuesNotLocked` - VIP has
genuinely zero live entitlement right now (art/UI shell only, matches BS's claim exactly). No whale
shortcut currently exists because nothing is purchasable yet.

**Constraints locked for whenever entitlement coding happens** (BS's pre-lock, before any
implementation): one active subscription, no stacking/banking; Auto-Fight stays identical for
everyone; VIP Stamina claims consume the existing purchase/rate cap, no separate bypass; any Gold/
Materials benefit is a bounded convenience grant, never a production multiplier or uncapped Quarry
replacement; explicitly NO cards/packs/Forge-Dust/Permits/Evolution materials/spell ownership/
combat stats/building-level skips/timer skips; cosmetics may accumulate freely (no progression
impact); on lapse, benefits stop, earned cosmetics remain, unclaimed temporary grants expire/stay
capped.

**Status: correctly not ready for a real economy lock** - price/duration/entitlement list/grant
schedule all still need locking before a real $10k/month simulation is possible. Real gap closed:
this is a legitimate "nothing built yet" finding, not a whale-exploit risk needing a fix.

## UI design-token foundation shipped (2026-08-26, CR, verified f80a804)

6 real color tokens added to `UIFrozenTokens`, each derived from the actual most-common existing
literal across screens (not invented) - ColorBackground, ColorPanel, ColorHeader, ColorAccentBronze
(reused GameBootstrap's real `AccentBorderColor` verbatim), ColorAccentEmerald, ColorTextPrimary.
Real border/frame primitive (`ApplyFramedPanel`/`CreateFramedPanel`/`CreateRoundedPanelSprite`)
generalizes both cited real techniques exactly - GameBootstrap's per-pixel alpha-shaping math for
procedural fallback, CampaignMapUiLibrary.ApplyModalChrome's real-art-first/fallback resolution
pattern - auto-upgrades to real art with zero code change once a file exists, same binding-layer
convention as `CombatPresentationAssetMap`. 9/9 new tests pass (value regression lock, contrast
invariant, real transparent-corner/opaque-center sprite verification, no-art fallback never bare),
HEAD pinned 608a477, 0 error CS. Foundation only, no screen migrated yet - per instruction, that's a
separate follow-up pass once this is proven solid.

## Retention telemetry pipeline shipped, NOT YET WIRED - flagged so it doesn't go quiet (2026-08-26, CR, verified 6e9081c)

Real, verified build matching the corrected architecture exactly: `RetentionTelemetryEvents.cs`
(pure event builder, `serverReceivedAtUtc` deliberately excluded client-side with a reflection test
locking that out - real anti-tampering discipline), `RetentionTelemetryGateway.cs` (mirrors the real
IFriendsGateway/IBazaarGateway/IChatSocialGateway trust-boundary shape), `RetentionTelemetryOutbox.cs`
(bounded 500, own JSON-persisted file so queued events survive a restart, never throws on enqueue,
stops cleanly at first flush failure). 15/15 pass, HEAD pinned d093997, 0 error CS.

**EXPLICITLY NOT wired into any real gameplay call site yet** - no emit calls exist at Empire
Expedition completion, Battle Pass claim, screen entries, or daily-cap hits. Flagging this loudly and
in the register itself (not just chat) precisely because of tonight's real lesson: a real, built
system with no live call sites is exactly the shape of thing that goes quiet and gets assumed "done"
when it is not. **This row stays open until real emit calls exist at real trigger points** - do not
mark this closed on "pipeline exists" alone.

## LOCKED: VIP/Subscription real entitlement spec (2026-08-26, BS, verified) - was buried, now re-locked properly

**Real root cause of the earlier VIP gap, now understood and fixed:** the 2026-08-22 brainstorm's
VIP membership design (800/1,500/3,000 gems) never got explicitly carried into the current economy
lock or explicitly descoped - it just went quiet between docs, and `VipSubscriptionOpenValues.cs`
stayed all-"Open" with nobody noticing until the owner asked "who buys VIP if it has no benefit."
This re-lock closes that gap with real numbers, verified against actual code before acceptance.

**Verified before locking:** `ShopStaminaCatalog.cs:11` confirms `StaminaGrantPerPotion = 50` and
`.cs:16` confirms `MaxPurchasesPerRollingDay = 4` - both exactly match BS's cited numbers, not
invented.

**Prices/duration (unchanged from the original 2026-08-22 numbers):** Weekly 800 Gems/7 days,
Fortnight 1,500 Gems/14 days, Monthly 3,000 Gems/30 days.

**Stamina benefit - bonus CLAIMS, not a rate/regen change:** Weekly = 1 claim (one 50-Stamina grant
across the 7 days); Fortnight = 2 claims (one per 7-day period); Monthly = 4 claims (one per 7-day
period). Each claim consumes one of the existing 4-per-rolling-24h purchase slots - cannot bypass
the cap, stack above the normal Stamina cap, or bank. A claim landing while already at full Stamina
is forfeited, not converted to anything else.

**"Periodic packs" wording explicitly RETIRED** - VIP grants no Single Sigil/Scout Cache/cards/
Forge-Dust/Permits/Evolution materials/spell ownership. The real periodic value is the scheduled
Stamina claim itself. Cosmetic grants may be added later, only from an existing cosmetic catalog -
no new currency/gameplay item introduced by this lock.

**Constraints (unchanged from the earlier pre-lock):** one active subscription, no stacking/overlap;
Auto-Fight identical for everyone; no combat stats/deck slots/cards/packs/spells/building levels/
construction speedups/timer skips; unused claims expire on lapse, earned cosmetics remain; purchase
goes through the existing Shop/IAP entitlement path.

**Whale/F2P verdict:** max monthly VIP benefit is 4 existing-size Stamina claims, still gated by the
same rate cap everyone has. F2P reaches the identical Stamina system through ordinary Gems - VIP
buys convenience/predictability, not exclusive power. No compounding shortcut found.

**BS's own sign-off: "READY FOR CC."** Implementation may now proceed - real entitlement coding in
`VipSubscriptionOpenValues.cs`/`VipSubscriptionPresenter.cs` (WH's lane, same file WH already
touched for the art fix).

## Design-vs-implementation audit: 1 real gap found (Shop Loyalty), matching the VIP pattern exactly (2026-08-26)

**Real, verified second instance of tonight's core lesson** (a locked design going quiet between
docs and implementation with nobody flagging it): Shop Loyalty Points was locked as part of the SAME
"Option C" directive as VIP (`SINGLE_BIBLE_MASTER_PLAN_2026-08-22.md:186/216-218`: "loyalty points on
every purchase → redeem for memberships or specific cards... keep multi-tier pack + loyalty + VIP
spirit"). Explicitly flagged as missing exactly once (`Shop_V1_Release_Contract.md:28,50`:
"Loyalty/reward track: **None**... needs a `int shopMilestoneProgress` field and a milestone table")
- then never mentioned again anywhere in the register's full history. **Verified myself: zero
"loyalty" matches anywhere in `Assets/Scripts`, confirming no implementation exists.**

**3 candidates checked and correctly ruled out, not silent gaps:** rewarded ads (Master Plan itself
says "not locked yet"), Normal/High-draw pack odds (legitimately reconciled into the real pity/
rarity-floor system, confirmed implemented), Treasury/inflation Market Credits tax (self-flagged
"nothing simulated," correctly Phase-2-deferred, tracked in the currencies table).

**One lower-confidence near-miss, not yet a confirmed gap:** "Gallery rewards at max level" -
mentioned once in a bulleted "still directionally valid" list, no specific numbers or "locked"
language, zero code. Real, worth a light check, but not confirmed to the same standard as Loyalty.

**Real next step:** Loyalty needs the same frozen-file pattern as Empire buildings/Materials - a
`shopMilestoneProgress` PlayerProfile field, per the real spec already written in
`Shop_V1_Release_Contract.md`. Needs owner sign-off before any coding room touches it.

**Not yet done, offered by the audit agent:** extending this same search to `Economy_Blueprint.md`
and `tools/mechanics_v2_extract.txt` (repeatedly cited as sources of "still directionally valid"
content, not originally in scope) - real candidate location for more of the same pattern.
