# MYRIAD OF DRAGONS — MYRIAD OPERATING SYSTEM (MOS) v1.1

**Design Governance • Technical Guardrails • AI Development Contract**

**Status:** FOUNDATION / APPROVAL REQUIRED. Reconciles the August 2026 Game Mechanics v2, Economy
Blueprint, Metagame Handoff, Mechanics Gap Analysis and the current test baseline.

**Golden Baseline:** 81/81 EditMode tests passed, 0 failed, 0 inconclusive (2026-08-07). Preserve
this state in Git before major changes.

**AI ownership:** Game Director = Zihan · System/Game Design Architect = ChatGPT · Battle
Engineering = Claude · Metagame + bulk content/data = Gemini.

> **Master copy is the `.docx` on Drive** (`Myriad_of_Dragons_MOS_v1.1.docx`, same WIP folder as
> v1.0). This file is a working mirror inside the repo specifically so every AI seat working
> locally (Claude, Gemini, GitHub Copilot) can actually read it from their own working directory —
> a governance document living somewhere none of the coding tools can see isn't governing them.
> If the two drift, the `.docx` is authoritative; re-sync this file when it's amended.

---

## v1.1 changelog (2026-08-07, Claude, per §22 Amendment Rule)

1. **§6** — added the exposed-Avatar siege rule and the onboarding Health taper as adopted
   mechanics; both were missing from v1.0 despite being tested and part of the Golden Baseline.
   Affected: Battle only. No save/data migration impact. No test impact (already covered).
2. **§7** — "Card level: Must persist" reworded from an asserted requirement to an explicit **NOT
   YET IMPLEMENTED** gap: `PlayerProfile.cardCollection` has no level field today. Affected: Save
   (flagged, not changed), Collection/Evolution work (now blocked on an explicit decision rather
   than free to add silently).
3. **§15/§18** — Gemini's role corrected to match actual shipped scope (metagame systems
   implementation, not just bulk content); Economy/Collection/Ledger test rows marked **(NOT YET
   BUILT)**.
4. **§20** — "Resolve the Back-lane benefit for Warrior/Knight" corrected: current implementation
   is already uniform across classes. Reframed any class-differentiated version as a future
   proposal, not an open item.
5. **§19** — campaign-unlock item marked **DONE** (commit `b64cbce`), not open P1.

All existing player data remains compatible — every v1.1 change is documentation-only or additive.

---

## 1. Purpose

MOS is the project's design constitution. It prevents different AI collaborators from silently
redefining shared systems, breaking save compatibility, duplicating classes, or drifting away from
approved economy and progression rules.

- Design specifications precede implementation when a rule is not settled.
- Code must not silently become the source of new game rules.
- Architecture conflicts must be reported before coding.
- MOS amendments require an explicit decision and impact note.

## 2. Source-of-Truth Hierarchy

| Priority | Source | Use |
|---|---|---|
| 1 | Running code + tests | What currently works. |
| 2 | Approved MOS + technical specs | What the project is allowed to become. |
| 3 | Current handoffs/gap analyses | Current implementation facts and known gaps. |
| 4 | Game Mechanics v2 | Primary gameplay reference, except where its implementation-status section records later changes. |
| 5 | 2017/legacy documents | Historical reference only. |

**Conflict rule:** never silently reconcile contradictions. Record the gap, decide which rule is
authoritative, then update the appropriate document and tests.

## 3. Current Audit

| Domain | Finding | Priority |
|---|---|---|
| Battle | Strong, tested core. Formation → Combat → Resolved, reinforcement windows, 12-clash cap. | P0 |
| Testing | 81/81 green. | P0 |
| Save | Atomic/quarantine behavior is a high-value foundation. | P0 |
| Metagame | Home/campaign/shop/deck builder exist; campaign unlock now reads saved state (fixed), rest still hardcoded/incomplete. | P1 |
| Economy | Gold/Gems are wired; other currency fields are partly inert. | P1 |
| Collection | Storage exists; full collection/acquisition UI is incomplete. | P1 |
| Card progression | Duplicate fusion exists conceptually; Evolution/Limit Break curves require simulation. | P1 |
| Trading | Primitive exists; marketplace UI/minting pipeline incomplete. | P2 |
| Guild | Designed but not implemented; future solo-to-guild expansion. | P3 |
| Story/events | Large legacy content library; should be retained as a content layer. | P1/P3 |

## 4. Non-Negotiable Design Pillars

- **P1 — Fun before monetization:** F2P must be able to enjoy the complete core game and compete meaningfully.
- **P2 — Money buys speed, not exclusive competitive power:** paying players may accelerate acquisition; Competitive objects must have a non-IAP path.
- **P3 — Prestige is earned:** event-exclusive/status rewards cannot be bought in the normal shop.
- **P4 — Only Property can move:** Identity and progression do not transfer between players.
- **P5 — Device migration is normal:** changing phones must preserve game progress; migration is not account selling.
- **P6 — Abuse review before launch:** every faucet, reward and trade mechanic receives a bot-farm review.
- **P7 — Rules are enforced in code:** use validators, query filters and tests, not author discipline alone.
- **P8 — Player time has value:** returning after a break should preserve meaningful property/progression.

## 5. Identity, Save & Device Migration

- One active session/device at a time may be used as an anti-abuse control, but device migration must be supported.
- Device migration moves game state to a new device; it is not a sale or ownership-transfer product.
- No official priced account-transfer/account-selling feature is approved.
- Identity, progression, reputation, rank history, guild history, achievements, friends, chat history, Empire level and research remain bound to the account.
- IAP purchase history must never be represented as transferable property.
- Save writes remain atomic; corrupted saves are quarantined rather than overwritten.
- Newer-build saves must not be accidentally overwritten by older builds.
- **PlayerProfile/SaveSystem changes require a whole-project usage audit and full test run.**
- **Core rule: do not casually delete, rename or change public PlayerProfile/SaveSystem APIs.** Prefer additive compatibility changes and planned migrations.

## 6. Battle Engine Contract

| Area | Current MOS rule |
|---|---|
| Flow | Formation → Combat → Resolved; formation locks, timed clash resolution, reinforcement windows on clashes 4 and 8. |
| Board | Front/Middle/Back; 3 slots per lane. |
| Win | Avatar reaches 0 HP or clash cap resolves the match. |
| Cap | 12 clashes; tie-break counts as a loss for progression. |
| Overflow | Undefended/open lanes can damage Avatar; multipliers x6/x9/x12. |
| Back lane | Living Back-lane cards generate +2 Energy per clash, uniformly across all classes. |
| Elements | Advantage applied at lane-total scale. |
| Rarity slots | Rarity 1–4 uses one slot; rarity 5–7 uses two. |
| Result boundary | Battle emits `MatchResult`; reward amounts belong to metagame/economy. |

**v1.1 addition — adopted mechanics:**
1. **Exposed-Avatar siege:** from clash 7, a side with no living cards bleeds 6% of its own max
   Avatar HP per clash, escalating on the same overtime curve as overflow damage. Resolves the
   mutual-wipe stalemate the 12-clash cap alone could not — measured via a 400-match sweep across
   profiles before adoption.
2. **Onboarding Health taper:** a new player (Avatar level 1) gets +100 starting Health, linearly
   gone by level 5 (the level the first per-level Health tier begins), so early matches last long
   enough for a spell to actually be cast.

Both are tested (`ExposedAvatarSiegeTests.cs`, `BalanceSimulationTests.cs`) and part of the 81/81
Golden Baseline.

**Battle owner:** Claude owns battle implementation, combat tests and simulations. Other systems
consume `MatchResult`; they do not duplicate combat resolution.

## 7. Card Collection, Duplicates, Evolution & Limit Break

Duplicate cards are an intentional long-term progression resource, not simply unwanted duplicates.

| Mechanic | Rule | Status |
|---|---|---|
| Card ownership | Persistent collection object; tradeability is independent. | Foundation |
| Duplicate cards | Used as progression fuel for Evolution/Limit Break. | Approved direction |
| Evolution | Uses an eligible duplicate/sacrifice and raises progression tier/max-level ceiling. | Curve TBD |
| Limit Break | Uses duplicate copies and/or approved materials to raise a card's permitted power/level ceiling. | Curve TBD |
| Card level | Must persist in save data. **NOT YET IMPLEMENTED as of v1.1**: `PlayerProfile.cardCollection` is currently a bare `List<string>` of card IDs with no level/copies field. Extending it is a PlayerProfile schema change (§5, FROZEN) and needs an explicit decision + migration plan before Collection/Evolution work begins — not a change either AI should make unilaterally. | Required, blocked |
| Skills | Individual + Ultimate; deterministic unless a future spec changes this. | Current |
| Variants | Normal/Warrior/Knight/Strategist/Perfect/Perfect+ remain collection/build identities. | Current |

**Anti-pay-to-win rule:** spending may accelerate obtaining duplicates, but Competitive power
cannot become permanently exclusive to spenders.

## 8. Currency Constitution

| Currency | Role | Tradeable | Cash-out now |
|---|---|---|---|
| Gold | Soft progression; fusion/evolution/crafting/deck/cosmetic sinks. | No | No |
| Gems | Premium/IAP currency; speed/convenience and approved packs. | No | No |
| Event Tokens / Event Medals | Event participation currency; non-tradeable and time-limited. | No | No |
| Guild Contribution | Bound guild reputation/contribution currency. | No | No |
| Dragon Relics / Market Credits | Future closed-loop marketplace medium. | No direct currency transfer | No in Phase 1–2 |

**Naming conflict (still open):** current code uses `eventMedals` and `dragonRelics`; the Economy
Blueprint uses "Event Tokens" and "Market Credits". Do not create both — choose one canonical
code/player-facing name before wiring the systems further.

## 9. Economy Rules

- Gold never converts to Gems or Market Credits/Dragon Relics.
- Gems never convert to Market Credits/Dragon Relics.
- Event Tokens cannot be bought with Gems or Gold.
- Event-exclusive rewards never appear in the normal shop.
- Market Credits/Dragon Relics cannot be purchased with real money.
- Every conversion graph must be acyclic.
- Every faucet needs a legitimate sink and must be simulated before final numbers are locked.
- Trading is optional; the game must remain playable and competitive without it.

## 10. Story & Events

- Core setting: post-Titan rebellion; humanity has driven off the Titans and the player builds an empire.
- Story and events should be developed now as a content framework, even if some implementation is later.
- Events should advance the world, not function only as disconnected reward calendars.
- Retain the legacy roster as a content library: Chest, Raid, Guild Wars, Battle Royale, Puzzle, Conqueror, Floor Clearance/Prison Break, Castle Defend, Dragon Raising and Special Dungeon.
- Event numbers are not automatically final; each event needs economy, bot and balance review.
- Solo-first event design must expose clean hooks for future guild participation.

## 11. Guild Expansion

- Guild is a future social layer; solo systems must not depend on it.
- Guild Wars may call the battle engine but must not duplicate combat logic.
- Guild contribution remains bound reputation/progression currency unless MOS is amended.
- Shared Armory/Vault and Guild War roles require explicit ownership/security rules before implementation.

## 12. Property Registry & Trading

- Only Property may move; Identity and Progression do not.
- Every tradeable instance needs a server-assigned unique instance ID and immutable provenance.
- Ownership history is append-only; corrections are compensating ledger entries.
- Client-supplied ownership, edition numbers and transaction authority are never trusted.
- Build the ownership ledger and item-tier flags early, even while marketplace UI remains disabled.
- Marketplace is Phase 2 after economic stability and anti-bot gates.
- No real-money cash-out, withdrawal or external payment surface is part of the current mobile scope.

## 13. Archive / Dormant / Exit Economy

- Players may leave without deleting property or history.
- Archive/Dormant are capability states, not one boolean.
- Archived/Dormant accounts cannot generate ordinary gameplay rewards; existing property remains preserved.
- Dormant property movement requires step-up authentication.
- Legacy extraction is additive: minting a Legacy object never consumes the underlying milestone.
- No commercial account succession feature is approved.

## 14. Technical Architecture Guardrails

| Layer | Owns | Must not own |
|---|---|---|
| Battle | Match state, combat resolution, combat tests. | Currency rewards, shop logic, persistence policy. |
| Metagame | Campaign, collection, rewards, shop presentation. | Combat math. |
| Economy | Currency balances, faucets/sinks, trade economics. | Combat resolution. |
| Save | Persistence, migration, corruption handling. | Gameplay decisions. |
| UI | Presentation/input. | Authoritative economy/combat rules. |
| Content/Data | Definitions/configuration. | Security-critical ownership authority. |
| Future Server | Authority, anti-cheat, multiplayer, ownership ledger. | Trusting client-supplied state. |

- Unity/C# is the production target.
- Current UI convention is procedural Legacy uGUI; do not introduce a new UI architecture casually.
- Unity compiles all C# under `Assets`: duplicate core class definitions are a hard stop.
- Do not delete legacy systems until whole-project references are proven and Git rollback exists.
- Future multiplayer must be server-authoritative.

## 15. AI Development Governance

| Role | Primary responsibility |
|---|---|
| Game Director | Vision, priorities, final acceptance. |
| System Architect / Design | MOS, architecture boundaries, economy/progression/story audits. |
| Claude | Battle code, combat tests, simulations and approved battle refactors. |
| Gemini | Metagame systems implementation (home/campaign/shop/deck-builder UI, Economy and Save-adjacent glue code, Story content system) plus bulk card/event/dialogue/data generation, within System Architect-approved specs. |
| Any AI | Must document changes and run tests; must stop on MOS conflict. |

**Golden rule:** no AI changes a core system simply because it believes the architecture can be
cleaner. A change must be scoped, authorized and regression-tested.

## 16. Safe Change Protocol

1. Describe the desired player outcome.
2. System Architect identifies affected systems and dependencies.
3. Create/update the specification if the rule is unsettled.
4. Declare allowed and forbidden files.
5. Create a Git branch from the known-good main.
6. Implement the smallest additive change.
7. Compile the full Unity project.
8. Run the full EditMode suite.
9. Run targeted simulations/manual play where automated tests cannot validate balance or `Update()`/coroutine behavior.
10. Review the Git diff.
11. Update MOS/spec/handoff documents if the approved design changed.
12. Merge only after acceptance criteria pass.

## 17. Golden Baseline

- Current result: 81 total / 81 passed / 0 failed / 0 inconclusive on 2026-08-07.
- Tag this state in Git before major changes.
- Clean compile is necessary but not sufficient.
- Balance can regress while structural tests remain green; simulation and manual playtesting remain mandatory.
- Save integrity, battle resolution, card persistence and currency integrity are release-blocking domains.

## 18. Testing Standards

| Test area | Purpose |
|---|---|
| BattleLogicTests | Combat relationships and edge cases. |
| BalanceSimulationTests | Outcome distributions and balance/economy simulations. |
| ExposedAvatarSiegeTests | Exposed-avatar transition and damage rules. |
| SaveSystemTests | Persistence, corruption, versioning and isolation. |
| Economy tests **(NOT YET BUILT)** | Faucet/sink integrity, conversion DAG and shop restrictions. |
| Collection tests **(NOT YET BUILT)** | Duplicate consumption, Evolution/Limit Break persistence and illegal sacrifice prevention. |
| Ledger tests **(NOT YET BUILT)** | Unique IDs, append-only ownership and transfer authorization. |

**Telemetry:** bot-detection signals should exist before marketplace launch so normal player
behavior has a baseline.

## 19. Priority Work Queue

| Priority | Gap | Next action |
|---|---|---|
| P0 | Protect battle + 81/81 baseline. | Git baseline/tag; Claude-only battle changes. |
| P0 | Approve governance. | Approve MOS v1.1. |
| P1 | ~~Campaign unlocks hardcoded.~~ **DONE (commit b64cbce).** | — |
| P1 | Deck builder persistence incomplete. | Bind to PlayerProfile. |
| P1 | Shop spends currency without reliably granting goods. | Implement acquisition pipeline + tests. |
| P1 | Collection screen/acquisition incomplete. | Build authoritative collection view. |
| P1 | Evolution/Limit Break curve unresolved. | Simulate before final coding. |
| P1 | Currency names conflict. | Choose canonical names and migrate additively. |
| P2 | Ownership ledger/property flags. | Build foundation before marketplace UI. |
| P2 | Marketplace UI. | Phase 2 only. |
| P3 | Guild implementation. | After solo core is stable. |

## 20. Decisions Required Before Major Metagame Coding

- Approve/amend MOS v1.1.
- Choose canonical names for Event Tokens/Medals and Market Credits/Dragon Relics.
- Define exactly what Evolution changes and exactly what Limit Break changes; define duplicate requirements and stat/level curves.
- Decide whether the 1–12 combat scale remains; any x10 migration must be atomic and simulated.
- ~~Resolve the Back-lane benefit for Warrior/Knight.~~ **RESOLVED as implemented:** uniform +2 Energy/clash for any living Back-lane card, regardless of class — no branching exists or is planned. (Strategist/Perfect get a separate, additional draw-on-play bonus specifically in Back — a different mechanic.) A class-differentiated version would be a new feature proposal.
- Resolve where initiative affects the current simultaneous combat model.
- Define the first solo campaign vertical slice and reward loop.

## 21. Reconciled Contradictions

| Conflict | MOS resolution |
|---|---|
| Mechanics v2 turn-by-turn combat vs current three-phase build | Current verified implementation is the starting truth; formally update the design spec after approval. |
| 78/78 in handoff vs 81/81 current test result | 81/81 is current Golden Baseline. |
| Legacy many-item currencies vs current five PlayerProfile currency fields | Use one canonical currency registry; do not extend the dead `ItemDatabase` currency definitions. |
| Legacy document says real-money trading is allowed/discouraged | Superseded: current MOS has no real-money cash-out/withdrawal path. |
| Device transfer vs account transfer | Device migration is allowed; account sale/ownership transfer is not an official feature. |
| Duplicate fusion deferred vs current design intent | Duplicates remain intentional progression fuel; final Evolution/Limit Break curves require simulation. |
| 2D current art vs old 3D presentation aspiration | 3D is future presentation work, not a reason to block the core game. |

## 22. Amendment Rule

Any MOS change must state: old rule, new rule, reason, affected systems, save/data migration
impact, test impact, and whether existing player data remains compatible. AI collaborators must
never silently alter MOS through code.

---

## Appendix — Short AI Contract

Paste this at the start of future Claude/Gemini/Copilot development sessions:

> You are working on Myriad of Dragons under MOS v1.1. Do not invent or silently change game
> rules. Preserve public APIs unless migration is explicitly approved. Do not duplicate core
> classes. Do not modify PlayerProfile, SaveSystem or economy architecture outside the files
> explicitly authorized. Run the full test suite after changes. If the request conflicts with
> MOS, STOP and report the conflict before coding. Treat the current 81/81 test result as the
> Golden Baseline until a newer verified baseline is approved.
