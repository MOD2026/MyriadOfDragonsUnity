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

**Open, CC must still lock:** (1) Quarry vs Materials Yard naming, (2) expand campaign Materials
faucet (currently 1,815,000, sized for 3 buildings, now insufficient for 11) vs. reduce the per-
building cost ladder, (3) Embassy's exact level-band curve, (4) Academy/Laboratory naming,
(5) Tree of Knowledge minigame brief, once written. None of these block MVP — Empire roster work is
explicitly outside `MVP_PLAYABLE_GATE_v1.md`'s scope.

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
