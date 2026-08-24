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

**Open, real (not yet locked — flagged by coding room 2026-08-23):** the Castle-level interlock
table (which Castle level unlocks which Barracks/Gate level) has no real 1-30 curve anywhere. The
only numeric example ("Castle 15 → Barracks max 15 / Gate max 13") lives in the section below marked
Superseded/stale — one qualitative rule plus one stale data point isn't enough to derive a real
curve, and it doesn't match the already-shipped `PlayerEmpireData.MinimumCastleForGateLevel` v1
formula either. Needs a real GPT round before implementation.
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
- **Open, real:** real player-choice loadout picker UI (choosing 4 of the unlocked pool, vs. today's
  auto-equip stopgap) is unbuilt — WH/future work, not guessed at now.
- **Chapter 1-3 narrative locked, not yet in `StoryDatabase.cs`** — queued for WH.
- **Chapters 4-10 narrative not yet drafted** — same reusable template as Ch1-3, cheap to commission once WH clears the Ch1-3 backlog.

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
