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

## Empire construction — CURRENTLY BEING REVISED, do not treat as settled

- Base: Gold + Construction Materials (Materials amendment 2026-08-23, see `EMPIRE_SCHEMA_LOCK_2026-08-22.md` §2)
- Timers: client-clock pacing only, no speed-up purchase (blocked on §8 above until a trusted server exists)
- **Open right now:** whether Construction Materials become partially Gem-purchasable via capped
  "Construction Contracts" — under active review 2026-08-23, do not treat as locked. If this locks,
  update this row.
- **Known stale downstream reference:** `Guild_Competition_Rewards_v1.md` reconciliation's Master
  Builder office assumption was written when construction was instant-only — corrected in the Drive
  doc 2026-08-23, needs re-review before that office ships.

## Trusted-server dependency — blocks 4 systems, still unresolved

Bazaar, Guild Expedition, Raid Troops, and SocialSafety's live client-test all need real server
authority. Four CloudCode modules authored and locally tested (`docs/CLOUDCODE_TRACK_STATUS_2026-08-23.md`)
but nothing deployed. Track was explicitly **paused 2026-08-23** — not MVP-critical, resumes only on
owner request.

## Content backlogs (real, ready-to-code, not design questions)

- **10 of 14 locked Phase-1 spells never implemented** — `SPELL_CATALOG_v1.md` vs live `AvatarSpell.cs` (4 shipped: Firestorm/Mend/War Cry/Divine Bolt).
- **Chapter 1-3 narrative locked, not yet in `StoryDatabase.cs`** — queued for WH.
- **Chapters 4-10 narrative not yet drafted** — same reusable template as Ch1-3, cheap to commission once WH clears the Ch1-3 backlog.
