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
