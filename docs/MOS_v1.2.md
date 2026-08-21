# MYRIAD OF DRAGONS — MYRIAD OPERATING SYSTEM (MOS) v1.2

**Design Governance • Technical Guardrails • AI Development Contract**

**Status:** FOUNDATION / APPROVAL REQUIRED. Reconciles the August 2026 Game Mechanics v2, Economy
Blueprint, Metagame Handoff, Mechanics Gap Analysis and the current test baseline.

**Protected Git Baseline:** 81/81 EditMode tests passed, 0 failed, 0 inconclusive (2026-08-07).

**Latest verified working-tree baseline:** 100/100 EditMode tests passed, 0 failed, 0 skipped,
0 inconclusive (2026-08-11), including the provider-neutral social contracts. This newer result
is not a clean Git baseline until the unrelated dirty worktree is separated and reviewed.

**AI ownership:** Game Director = Zihan · System/Game Design Architect = ChatGPT · Battle
Engineering = Claude · Metagame + bulk content/data = Gemini.

> **Master copy remains the `.docx` on Drive** until a v1.2 DOCX is produced and approved. This
> repository file is the current v1.2 working reference for local AI and coding tools. If it is
> accepted, re-sync the Drive master and archive v1.1 rather than overwriting its history.
> This file is a working mirror inside the repo specifically so every AI seat working
> locally (Claude, Gemini, GitHub Copilot) can actually read it from their own working directory —
> a governance document living somewhere none of the coding tools can see isn't governing them.
> If the two drift, the `.docx` is authoritative; re-sync this file when it's amended.

---

## Wartime build priority (2026-08-22, Command Centre / owner lock)

**Authoritative detail:** `docs/CORE_SYSTEMS_CONSTITUTION.md` §0.

While the solo playable loop is incomplete, every AI seat optimizes for:
1. **Fastest path to the whole playable game** (full loop + campaign depth; big breaks before polish).
2. **Token efficiency** (bulk content; no abstract decision cards; no HUD/animation polish).

Stage difficulty fine-tune is deferred (enemy decks = data). Perfecting/polishing comes after the game exists. This does **not** rewrite guild/Phase 1 design in MOS — it governs **build order and agent spend** until Command Centre lifts wartime.

**Affected:** agent tasking, campaign content fill, UI polish timing. Battle combat knobs remain per `CORE_SYSTEMS_CONSTITUTION` §C / MVP combat constitution.

---

## v1.2 changelog (2026-08-11, ChatGPT, per §22 Amendment Rule)

1. **§3/§11/§19 — Guild priority:** moved the guild foundation from future P3 work to Phase 1.
  Phase 1 now includes persistent identity, R1–R5 membership, guild chat, one-to-one direct
  messaging, donations, Guild Contribution, timer help, guild research and the Guild Store. PvP
  remains deferred.
2. **§8 — Guild Contribution:** defined one spendable account-bound balance plus a separate
   non-spendable lifetime-earned statistic. Spending never erases contribution history.
3. **§11 — Guild operating model:** added standardized building levels, personal research,
   guild research, donation charges, timer help, the Embassy, Guild Citadel, Guild Store and
   server-authoritative abuse controls.
4. **§11 — R1–R5 governance:** R1 is the default joining rank; R4 officers operate routine guild
   systems; R5 is the sole leader. Only R4/R5 may activate guild research. Destructive and
   ownership powers remain R5-only.
5. **§17/§18 — Verification:** retained 81/81 as the protected Git baseline and recorded 100/100
   as the latest verified working-tree result, including 19 social-contract tests.
6. **§11.9/§11.10 — Competition and offices:** added guild leagues, rotating individual
   achievement boards, anti-repeat-winner reward bands, Hall-of-Fame prestige and seven-day
   R4/R5-appointed offices with bounded non-combat benefits.
7. **§11.11 — Direct messaging:** recorded one-to-one private messaging, message requests,
  accepted contacts privacy, block/mute/report, retention and audited moderation access as
  mandatory Phase 1 social requirements. Trusted block/mute server implementation is now
  authored in `CloudCode/SocialSafety/`; report submission remains contract-blocked pending an
  additive idempotency/evidence field decision.

**Reason:** guilds and chat are now launch foundations, and the launch guild needs a complete
cooperation loop rather than chat alone. The rules preserve server authority, solo viability and
non-exclusive power.

**Affected systems:** Identity, Future Server, Guild/Social, Empire, Research, Economy, Save, UI,
telemetry and tests. Battle mechanics are not changed.

**Save/data migration impact:** specification only. Implementation requires new server-owned
guild/research/contribution ledgers and additive Empire fields. `PlayerProfile`, `SaveSystem` and
`SaveManager` remain frozen until a separately approved migration plan exists.

**Test impact:** no code changed by this amendment. Future implementation requires contract,
authority, timer, economy, migration and abuse tests described in §11 and §18.

**Compatibility:** existing player data remains compatible at the specification stage.

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
| Guild | Phase 1 foundation in progress: provider-neutral social contracts exist; identity, server implementation, R1–R5 governance, donations, help, research and store remain unbuilt. | P1 |
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
| Guild Contribution | Bound, spendable guild-activity currency for the Guild Store. Track available balance separately from non-spendable lifetime contribution earned. | No | No |
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

Guild is a Phase 1 social and cooperation foundation. Solo systems must remain complete and must
not require guild membership. Phase 1 includes persistent membership, R1–R5 governance, chat,
one-to-one direct messaging, announcements, donations, Guild Contribution, timer help, personal
research, guild research and the Guild Store. PvP, Guild Wars, shared inventory, lending and
trading remain later work.

### 11.1 Standard building framework

- **All upgradable Empire buildings have a launch maximum of level 30.** Castle, Barracks, Gate,
  Academy, Embassy and every later Empire building use the same level ceiling unless a future MOS
  amendment raises the global ceiling.
- Avatar level is character/account progression, not a building, and is outside this ceiling.
- Guild Citadel is a collective guild structure and also has a launch maximum of level 30.
- No building may silently exceed level 30, use a private exception, or continue charging for an
  upgrade after its useful effects stop scaling.
- Every building must define a useful effect at each level or at explicit milestone levels. Empty
  levels are prohibited.
- The current Barracks formula reaches its deck-slot maximum at level 50 and therefore conflicts
  with this rule. Implementation must rescale the Barracks progression to finish by level 30 and
  rerun battle/balance simulations; this documentation amendment does not change combat code.
- Building costs and durations are data-driven and server-validated. Final curves require economy
  simulation before release.

**Launch building roles:**

| Building | Maximum | Primary role |
|---|---:|---|
| Castle | 30 | Empire progression and economic capacity. |
| Barracks | 30 | Deck-capacity and military progression already defined by the Empire system. |
| Gate | 30 | Defensive/Empire progression; exact non-combat effects require a separate spec. |
| Academy | 30 | Individual research, research queue and personal research-speed progression. |
| Embassy | 30 | Guild access, timer-help capacity, minimum help reduction and simultaneous help requests. |
| Guild Citadel | 30 | Collective guild level, member capacity, research tiers and Guild Store tiers. Server-owned, not part of a player's local Empire save. |

Do not add further buildings merely to create upgrade sinks. A new building needs a distinct
player decision, progression role and data/test specification.

### 11.2 Individual research

- Individual research belongs to the account and is initiated by the player through the Academy.
- One individual research may be active at a time at launch. Additional queues require a later
  economy decision; they are not silently granted by IAP.
- Individual research is eligible for guild timer help.
- Academy level gates research tiers and can improve research speed or unlock queue-management
  conveniences, but cannot bypass the level-30 building ceiling.
- Initial research domains are Economy, Construction, Research, Collection/Crafting and Empire
  Utility. Direct card-stat or battle-rule research is excluded until separately specified and
  simulated with the battle owner.
- Personal research remains account-bound, survives guild departure and is server-authoritative
  once online identity is active.

### 11.3 Guild research

- Guild research is collective, server-owned progression attached to one guild. Benefits apply
  only while the account is an eligible member of that guild.
- Members donate to the selected guild research or Guild Citadel project. Donations never grant
  the client authority to complete, redirect or activate research.
- **Only R4 officers and the R5 leader may select and activate guild research.** R1–R3 may donate
  but cannot choose the active research target.
- One guild research project is active at a time at launch. R4/R5 may queue the next project.
- R4 may activate or reorder eligible projects. R5 may additionally cancel a project. Cancellation
  requires confirmation and an audit entry; donated value is not silently refunded or destroyed.
- Guild research nodes have explicit rank caps of 5 or 10, declared in data. Guild research is
  gated by Guild Citadel level, which is capped at 30.
- Initial guild research branches: Cooperation (help capacity), Development (construction/research
  convenience), Community (member/announcement capacity), Treasury (store inventory/limits) and
  Logistics (donation efficiency). Direct combat-stat bonuses are excluded from Phase 1.
- Every activation, donation, completion, cancellation and benefit change uses server time and an
  append-only audit record.

### 11.4 Guild donations and contribution

- Each account has 12 donation charges. One charge replenishes every two hours on a rolling server
  clock, so the full allowance recovers in 24 hours. No fixed regional reset is used.
- Initial tuning target: each charge costs 50 Gold and awards 10 available Guild Contribution,
  10 lifetime contribution earned and 10 guild-development points. These are provisional values
  and must pass faucet/sink and bot-farm simulation before release.
- Available Guild Contribution is spent in the Guild Store. Lifetime contribution earned is a
  non-spendable statistic; store purchases never reduce visible contribution history.
- Chat volume, online time, opening guild screens and passive membership award no contribution.
- Contribution, donation charges and guild-development totals are server-authoritative.
- No Gem purchase of donation charges is approved for Phase 1.

### 11.5 Embassy and timer help

- Eligible timers are building upgrades and individual research. Each eligible timer can publish
  one guild-help request. Each other member may help it once; self-help is prohibited.
- Maximum helps received per timer: `min(25, 5 + EmbassyLevel)`.
- Fixed reduction per valid help: `30 seconds + 10 seconds × (EmbassyLevel - 1)`.
- Actual reduction is the larger of the fixed reduction or 0.5% of remaining time, capped at
  10 minutes per help and never below zero.
- At launch a player may expose one building request and one research request simultaneously.
  Embassy level 10 adds one active-request slot; level 20 adds one more.
- The first 20 valid helps given per rolling 24 hours award 1 Guild Contribution each. Further
  helps remain allowed but award no additional currency.
- Finished, cancelled, expired, duplicate, cross-guild and self-help requests award nothing.
- A `Help All` client action is permitted, but the trusted service still validates each help.

### 11.6 Guild Store

- Guild Contribution may buy bounded convenience and recognition: small construction/research
  speed-ups, Gold, ordinary crafting materials, guild cosmetics and later non-exclusive card or
  skill materials with a genuinely comparable solo route.
- The store must not sell Gems, Event Tokens, Market Credits/Dragon Relics, tradeable property,
  exclusive combat power, unlimited resources or effects that reduce another player's progress.
- Every useful stock item has a per-account weekly purchase limit. Prices and limits require
  economy simulation before they become final.
- Store access, balance, stock, purchase limits and receipts are server-authoritative.

### 11.7 R1–R5 ranks and command authority

R1 is assigned automatically when a player joins. Rank is not personal power and grants no combat
bonus. Authority is hierarchical: no officer may alter an equal/higher rank unless explicitly
allowed below.

| Rank | Meaning | Command authority |
|---|---|---|
| R1 | Recruit; default joining rank. | Chat, donate, help, request help, view roster/research and use the Guild Store after the new-member wait. No management authority. |
| R2 | Member in good standing. | Same system access as R1; recognition rank for established participation. May respond to guild activities and polls when implemented. |
| R3 | Veteran/core member. | Same baseline access plus trusted-member recognition and future event coordination privileges. Cannot manage membership, research or moderation by default. |
| R4 | Officer. | Invite players; accept/reject applications; promote/demote R1–R3; remove R1–R3; edit announcements; moderate guild chat; select/activate/queue guild research; select donation targets; schedule guild activities; manage ordinary Guild Store rotation within approved data; view guild activity/audit summaries. Cannot appoint/remove R4, affect R5, transfer ownership, disband the guild, change protected identity fields or erase audit history. |
| R5 | Leader; exactly one per guild. | All R4 powers plus appoint/demote/remove R4, edit guild identity/settings, transfer ownership, cancel guild research, control officer permissions within MOS limits and disband the guild. |

R5-only destructive actions require explicit confirmation, recent authentication and audit logs.
Guild disbanding enters a 24-hour pending state visible to all members and may be cancelled by R5
during that window. Ownership transfer immediately removes the old owner's R5 authority and is
rate-limited to prevent rapid transfer abuse.

R4 operational roles may later include Recruiter, Community/Moderation Officer, Research Officer,
Treasurer/Logistics Officer and War Officer. These are permission bundles, not extra ranks. War
Officer has no Phase 1 combat authority while PvP/Guild Wars are deferred.

### 11.8 Membership, security and anti-abuse

- New members wait 24 hours before earning contribution, donating to collective development or
  purchasing from the Guild Store. They may chat and receive essential onboarding access subject
  to moderation policy.
- Available Guild Contribution remains account-bound when leaving; lifetime history remains.
  Donations already credited to the guild remain with the guild.
- Leaving closes outstanding help requests and removes active guild-research benefits.
- Every promotion, demotion, removal, research action, donation, help, store purchase, moderation
  action, ownership transfer and disband action is server-authoritative and auditable.
- R4/R5 permissions are necessary but never sufficient by themselves: the service must also verify
  that every target membership, role, message, research and guild resource belongs to that guild.
- Guild Wars may call the battle engine later but must never duplicate combat logic.
- Shared Armory/Vault, card lending and trading require separate ownership/security rules and are
  not approved by this section.

### 11.9 Guild and individual competition

- Guild competition is part of the Phase 1 multiplayer foundation even while synchronous PvP is
  deferred. Initial competition uses cooperative event objectives, donations, help and verified
  PvE/progression achievements.
- Guild competition uses 14-day seasons and five leagues: Iron, Bronze, Silver, Gold and Dragon.
  Guilds are grouped by prior league, active-member count and recent activity. Top and bottom 10%
  are promoted/relegated after an eligible season.
- Guild Season Score uses capped event objectives, the top 30 capped eligible contributors and a
  participation-breadth bonus of up to 20%. Power, purchases, chat and passive presence score zero.
- Individual boards are separated into Event Contribution, Guild Contribution, Combat Achievement,
  Builder, Scholar, Power Showcase and Lifetime Achievements.
- Combat Achievement uses eligible weighted results with repeat-target and daily/best-result caps;
  it is never a raw farmable kill counter.
- Power Showcase is prestige-only. It may award an emblem, frame, title and Hall-of-Fame record but
  no recurring functional payout.
- Every event provides personal milestones. Rank 1–10 receive the same functional rank package at
  launch; higher placements receive stronger visual distinction rather than compounding resources.
- Repeated winners may continue competing and accumulate visual Hall-of-Fame marks, but do not gain
  multiplying functional rewards or permanent combat power.
- Guild placement rewards require a minimum personal-contribution threshold.
- Detailed scoring, rewards, UI and test rules live in `docs/Guild_Competition_Rewards_v1.md`.

### 11.10 Seven-day appointed offices

- After an eligible guild event, R4/R5 may appoint qualifying members to one seven-day office.
  Offices are temporary duties and benefits, not replacements for R1–R5 ranks.
- Master Builder: +5% personal construction speed.
- Royal Scholar: +5% personal research speed.
- Quartermaster: +5% ordinary resource production/gathering.
- First Envoy: +1 simultaneous Embassy help request.
- Guild Champion: prestige emblem, animated frame and title; no combat-stat bonus.
- R4 may appoint eligible R1–R3. R5 may appoint eligible R1–R4. Nobody may appoint themselves;
  R4 cannot appoint another R4.
- Eligibility requires the personal event milestone and at least three active event days. One
  account may hold one office; each office has one holder per guild.
- Reassignment has a 24-hour cooldown and cannot extend the original office window. Leaving,
  removal, suspension or archival ends the benefit immediately.
- Appointment, replacement, revocation and expiry are server-authoritative and auditable.
- Offices grant no direct card/combat stats, premium currency, exclusive cards/skills or PvP
  advantage. Combined social speed bonuses require a configured cap; launch target 20%.

### 11.11 Direct messaging

- Phase 1 includes one-to-one direct messages between eligible players. No group private
  messaging is approved for Phase 1.
- A message-request system is required before unrestricted conversation begins. The later social
  contract slice must support accepting and declining requests, closing conversations and read
  state.
- Privacy choices are limited to nobody, guild members only and accepted contacts.
- Block, mute and report controls are mandatory and integrate with direct messages. Blocking
  immediately prevents further messages in both directions.
- R4 and R5 authority cannot bypass another player’s privacy, block or moderation settings.
- Text and approved game-generated links only. No image, video, audio, file, location or
  unrestricted external-link sharing in Phase 1.
- Mandatory rate limiting, spam protection, retention and trusted-service moderation apply.
- Moderators may access retained messages only through audited moderation processes.
- Direct messages must never expose raw account IDs, credentials, tokens, age data or device
  information.
- Age and regional safeguards apply. Accounts ineligible for unrestricted text communication may
  use approved preset messages only.
- The same age, regional, privacy, blocking, reporting and moderation safeguards apply equally to
  both guild chat and direct messaging.
- The later social-contract slice must add provider-neutral contracts for direct-conversation
  identity; message requests; accepting and declining requests; sending and retrieving messages;
  pagination and message ordering; delivery/read state; messaging privacy preferences;
  block/mute/report integration; conversation closure; retention and audited moderation access.

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
| Social/Guild | Provider-neutral client contracts and presentation of trusted state. | Authentication authority, contribution minting, permissions, timer decisions or store settlement. |
| Future Server | Identity, authority, anti-cheat, guild membership/roles, chat moderation, donations, help timers, research, store settlement and ownership ledger. | Trusting client-supplied state, rank, timestamps, balances or resource relationships. |

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
| VS Code / Copilot | Narrowly scoped implementation with an explicit file allowlist and acceptance tests. No exploratory redesign, broad cleanup or commits without approval. |
| Backend/Server seat (owner TBD) | Trusted identity, guild/social authority, moderation, contribution/research/help/store ledgers and service tests. The Phase 1 social-safety module scaffold and block/mute slice are authored under `CloudCode/SocialSafety/`; broader backend ownership remains to be assigned. |
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

- Protected Git result: 81 total / 81 passed / 0 failed / 0 inconclusive on 2026-08-07.
- Latest verified working-tree result: 100 total / 100 passed / 0 failed / 0 skipped / 0
  inconclusive on 2026-08-11. It includes 19 social-contract tests but is not a clean baseline tag
  while unrelated UI/recovery changes remain in the worktree.
- Create a reviewed clean baseline/tag before major implementation or committing social work.
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
| SocialFoundationTests | Provider-neutral social contract shape, trusted-session boundary, guild lifecycle and authority requirements. |
| Guild authority tests **(NOT YET BUILT)** | R1–R5 permission matrix, same-guild resource checks, ownership transfer/disband safety and audit records. |
| Guild economy tests **(NOT YET BUILT)** | Donation recharge, contribution faucet/sink, store limits, guild-hopping and bot-farm resistance. |
| Timer-help tests **(NOT YET BUILT)** | Embassy caps/formulas, one-help-per-member, request lifecycle and server-time enforcement. |
| Research tests **(NOT YET BUILT)** | Personal/guild ownership, R4/R5 activation, cancellation, tier gates and benefit removal on departure. |
| Ranking/reward tests **(NOT YET BUILT)** | League placement, capped scoring, participation eligibility, flat top functional bands, Power Showcase restrictions and repeat-winner concentration. |
| Appointed-office tests **(NOT YET BUILT)** | R4/R5 authority, eligibility, no-self-appointment, one-office limit, seven-day expiry, guild-departure removal and buff caps. |

**Telemetry:** bot-detection signals should exist before marketplace launch so normal player
behavior has a baseline.

## 19. Priority Work Queue

| Priority | Gap | Next action |
|---|---|---|
| P0 | Protect battle + verified baselines. | Preserve 81/81 Git baseline; isolate and review the 100/100 social working-tree state. |
| P0 | Approve governance. | Approve MOS v1.2 and sync the Drive master. |
| P1 | Persistent player identity absent. | Select trusted identity approach; implement recovery/device migration before live guild state. |
| P1 | Guild/social contracts exist but no trusted implementation. | Build identity, membership and authority in narrow reviewed slices. |
| P1 | Guild cooperation loop unimplemented. | Implement donations/contribution, Embassy help, research and Guild Store after identity/membership. |
| P1 | Guild competition/reward layer unimplemented. | Implement league/event score contracts, milestone rewards and appointed-office authority after the core guild ledger exists. |
| P1 | ~~Campaign unlocks hardcoded.~~ **DONE (commit b64cbce).** | — |
| P1 | Deck builder persistence incomplete. | Bind to PlayerProfile. |
| P1 | Shop spends currency without reliably granting goods. | Implement acquisition pipeline + tests. |
| P1 | Collection screen/acquisition incomplete. | Build authoritative collection view. |
| P1 | Evolution/Limit Break curve unresolved. | Simulate before final coding. |
| P1 | Currency names conflict. | Choose canonical names and migrate additively. |
| P2 | Ownership ledger/property flags. | Build foundation before marketplace UI. |
| P2 | Marketplace UI. | Phase 2 only. |
| Later | PvP/Guild Wars. | Defer until population and server foundations justify it; reuse the battle engine. |

## 20. Decisions Required Before Major Metagame Coding

- Approve/amend MOS v1.2 and sync the Drive master copy.
- Assign a Backend/Server owner and select a trusted identity/service approach using player
  recovery, moderation, cost, data portability and operational risk as the decision criteria.
- Simulate Guild Contribution donation/store numbers before implementation values are locked.
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
| 78/78 in handoff vs later verified results | 81/81 is the protected Git baseline; 100/100 is the latest verified social-contract working-tree result pending clean isolation. |
| Legacy many-item currencies vs current five PlayerProfile currency fields | Use one canonical currency registry; do not extend the dead `ItemDatabase` currency definitions. |
| Legacy document says real-money trading is allowed/discouraged | Superseded: current MOS has no real-money cash-out/withdrawal path. |
| Device transfer vs account transfer | Device migration is allowed; account sale/ownership transfer is not an official feature. |
| Duplicate fusion deferred vs current design intent | Duplicates remain intentional progression fuel; final Evolution/Limit Break curves require simulation. |
| 2D current art vs old 3D presentation aspiration | 3D is future presentation work, not a reason to block the core game. |
| Guild listed as P3 vs multiplayer/social required in Phase 1 | Superseded by the 2026-08-11 Game Director decision: identity, guild and chat are Phase 1; PvP remains later. |
| Building progression used inconsistent implicit ceilings | All upgradable Empire buildings and the Guild Citadel now share a launch maximum of level 30; existing formulas that assume higher levels require rescaling and simulation before code changes. |

## 22. Amendment Rule

Any MOS change must state: old rule, new rule, reason, affected systems, save/data migration
impact, test impact, and whether existing player data remains compatible. AI collaborators must
never silently alter MOS through code.

---

## Appendix A — Guild Industry Reference Points

These references inform the guild pattern but do not override MOS or substitute for simulation:

- Last War uses five alliance ranks with R1 as newcomer, R4 as officers and R5 as leader. R4/R5
  can promote/demote R1–R3, while only R5 appoints or removes R4. Its alliance benefits include
  technology and help for construction/research:
  https://firstfungroup.zendesk.com/hc/pt-br/articles/45560184910355-Oficiais-da-Alian%C3%A7a
- Jurassic World Alive separates Member/Veteran/Officer/Co-Leader/Leader powers, prevents leaders
  leaving before ownership transfer and reserves disbanding for the leader:
  https://ludia.helpshift.com/hc/en/21-jurassic-world-alive/faq/2116-what-are-alliance-roles/
- Star Trek Fleet Command allows one help per member per timer, scales maximum helps through
  alliance progression and ties contributions to alliance growth:
  https://scopely.helpshift.com/hc/en/19-star-trek-fleet-command/faq/2937-alliance-help-and-contributions/
- Nova Empire reduces construction/research timers by the larger of a percentage or a fixed floor:
  https://gamebear.helpshift.com/hc/en/3-nova-empire/faq/32-what-is-alliance-help/
- Nations of Darkness caps daily alliance donations and awards contribution points redeemable in
  an alliance store:
  https://byaliens.helpshift.com/hc/en/6-nations-of-darkness/faq/157-alliance-donations/
- Clash Royale documents request/donation caps as protection against infinite resources and
  guild-hopping reward abuse:
  https://support.supercell.com/clash-royale/en/articles/requesting-and-donating-cards-2.html

---

## Appendix B — Short AI Contract

Paste this at the start of future Claude/Gemini/Copilot development sessions:

> You are working on Myriad of Dragons under MOS v1.2. Do not invent or silently change game
> rules. Preserve public APIs unless migration is explicitly approved. Do not duplicate core
> classes. Do not modify PlayerProfile, SaveSystem or economy architecture outside the files
> explicitly authorized. Run the full test suite after changes. If the request conflicts with
> MOS, STOP and report the conflict before coding. Treat 81/81 as the protected Git baseline and
> 100/100 as the latest verified social-contract working-tree result; do not call the latter a
> clean baseline until the unrelated dirty worktree is separated and reviewed.
