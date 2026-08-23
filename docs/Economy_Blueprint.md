# Myriad of Dragons — Shop & Currency Blueprint (v0.1, DRAFT FOR ATTACK)

> **MOS v1.2 amendment — 2026-08-11:** Guild Contribution now has an available spendable balance
> and a separate non-spendable lifetime-earned statistic. Phase 1 adds capped donation/help faucets,
> a Guild Store with weekly purchase limits, guild league/milestone rewards and seven-day appointed
> offices. Power ranking is prestige-only and produces no recurring functional payout. Exact
> values remain provisional until economy and bot-farm simulation. See
> `Guild_Competition_Rewards_v1.md` and MOS §11.4–§11.10.

**Status:** Pre-implementation design. Nothing here is built yet.
**Date:** 2026-08-07
**Purpose:** Define the full currency and shop specification so it can be reviewed adversarially before any code is written.

---

## 0. How to review this document

This document exists to be attacked. It is deliberately specific — every number is a
concrete proposal, not a placeholder — because vague blueprints cannot be critiqued.

**Reviewers: please try to break the following, in priority order.**

1. **Faucet/sink arithmetic** (§4.1, §4.5). Does gold inflate? Does any currency have a
   faucet with no matching sink? Do the daily-earn numbers make the shop prices absurd
   (either trivially affordable or insultingly expensive)?
2. **Bot profitability** (§9). For every faucet, compute whether a bot farm clears
   more value per account-hour than the account costs to create and warm up. If any
   faucet is bot-positive, that faucet is broken.
3. **Boundary violations** (§6.3). Find any SKU in the shop that violates the
   "money buys speed, never exclusive power" rule, or any path by which a soulbound
   item reaches the shop.
4. **Conversion loops** (§5). Find any cycle in the conversion graph. Any cycle is a
   money-printing exploit. The graph must be a DAG.
5. **Known weak points** (§12). I have listed the parts I am least confident about.
   Start there.

Do not review this against "is this how other games do it." Review it against whether
the system is internally consistent and abuse-resistant.

---

## 1. Governing principles (reference)

These were settled in design discussion and constrain everything below. They are not
open for re-litigation in this review — only their *application* below is.

| # | Principle | Practical consequence |
|---|---|---|
| 1 | Developer creates the world; players determine value | Studio sets sinks and drop rates, never item prices — **conditional on sinks actually functioning (§4.5)** |
| 2+4 | Every collectible has a birth certificate | One immutable ledger, framed as engineering integrity *and* as narrative provenance. Same system. |
| 3 | Not everything is an item | Levels/research/achievements are *variables*. Variables unlock assets; variables never become assets. |
| 5 | Build the economy like a country | Central Bank (currency issuance), Treasury (sinks), Property Registry (ledger), Court (disputes), Customs (transfer gates) |
| 6 | Build for abuse first | No reward/event/transfer mechanic ships without a documented bot-farm review |
| 7 | Separate Identity / Progression / Property | **Only Property moves.** Identity and Progression never transfer, ever. |

**Two hard engineering rules carried forward:**

- **Legacy extraction is additive, never destructive.** Converting a milestone into a
  collectible must never reduce, reset, or consume the underlying progression. This
  closes the strip-and-abandon exploit.
- **Trading is optional.** The game must be fully enjoyable and fully competitive for a
  player who never trades. If the meta requires marketplace participation, the design
  has failed.

---

## 2. The four currencies at a glance

| | **Gold** | **Gems** | **Event Medals** | **Market Credits** |
|---|---|---|---|---|
| **Type** | Soft | Hard (IAP) | Seasonal | Trade proceeds |
| **Source** | Gameplay | Real money + sparse gameplay | Event participation only | Marketplace sales only |
| **Expires** | No | No | **Yes — 14 days after event ends** | No (see §4.4 for hoarding control) |
| **Tradeable** | No | No | No | No (it *is* the trade medium) |
| **Cashable** | Never | Never | Never | **Not in Phase 1–2.** Phase 3 only, gated on legal review |
| **Wallet cap** | 9,999,999 | None | 50,000 per event | 999,999 |
| **Exists in Phase** | 1 | 1 | 1 | 2 |

**Naming note:** The codebase currently has `gold` and `gems` in `SaveData`. Event Medals
and Market Credits are new. Player-facing names TBD ("Dragon Credits" was floated for
Gems — flagged as a **naming risk**, see §12.6).

---

## 3. Item taxonomy

Every ownable object carries three orthogonal flags. These are independent — a thing can
be Prestige *and* Tradeable, or Prestige *and* Soulbound.

```
                        OWNABLE OBJECT
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
   ACQUISITION           TRANSFERABILITY        POWER CLASS
   ───────────           ───────────────        ───────────
   Shop                  Soulbound              Competitive (affects matches)
   Event-only            Tradeable              Prestige (cosmetic/status only)
   Milestone-minted      Bound-until-unlocked
   Crafted
```

### 3.1 The three hard rules of the taxonomy

1. **`acquisition == EventOnly` implies the object can never appear in any shop SKU,
   bundle, pack, or pity table.** Not "rarely." Never. This is enforced by a query-level
   filter, not by content-authoring discipline (§10.2).
2. **`powerClass == Competitive` objects must be obtainable without spending money.**
   Money may accelerate acquisition (§6.3). Money may never be the only path.
3. **`powerClass == Prestige` objects may be permanently exclusive** — this is where
   scarcity and status live, and it is the only place exclusivity is permitted.

### 3.2 Provenance record (Principle 2+4)

Every object with `transferability != Soulbound` carries an immutable birth certificate.

```
ItemInstance
  instanceId       : ulong      // server-assigned, globally unique, never reused
  definitionId     : string     // "card_ancient_dragon" — the template
  originType       : enum       // EventReward | ShopPurchase | Craft | Milestone | Trade
  originContext    : string     // "Guild War Season 2"
  mintedUtc        : long       // server clock only, never client
  edition          : int?       // 7  (null if unlimited)
  editionOf        : int?       // 100 (null if unlimited)
  currentOwnerId   : string
  ownershipHistory : []         // append-only, never rewritten
  flags            : bitfield   // Tradeable | Prestige | EventOrigin | Legacy
```

**`ownershipHistory` is append-only. There is no update path, only insert.** A "correction"
is a new compensating entry, never a mutation. This is the Property Registry from
Principle 5, and it is what makes the Court (dispute resolution) cheap: ownership questions
are answered by reading the log, not by adjudicating memory.

---

## 4. Currency specifications

### 4.1 GOLD — the soft currency

**Role:** Paces day-to-day progression. Absorbs the bulk of routine player activity.
**Never cashes out. Never trades. Never converts to Gems.**

**Faucets (per day, active F2P player):**

| Source | Rate | Daily ceiling | Est. daily gold |
|---|---|---|---|
| Match victory | 60 | 15 matches | 900 |
| Match loss | 20 | 15 matches | (consolation, included above) |
| Daily quests (3) | 150 each | 3 | 450 |
| Campaign stage first clear | 250 | one-time per stage | ~250 amortised |
| Weekly guild contribution payout | 1,200 | weekly | ~170 |
| **Total est. daily faucet** | | | **~1,600–1,800** |

**Sinks:**

| Sink | Cost | Notes |
|---|---|---|
| Card fusion (duplicate → level) | 500 → 12,000 by rarity | **Primary sink.** Scales steeply. |
| Card evolution (tier-up) | 8,000 – 40,000 | Endgame sink |
| Deck slot unlock | 2,000, ×1.5 each | Capped at 12 slots |
| Crafting reagents | 300 – 3,000 | Feeds Legacy/cosmetic crafting |
| Guild monument contribution | Player-set, min 100 | **Uncapped sink** — critical, see §4.5 |
| Marketplace listing fee (Phase 2) | 2% of ask, min 50 | Burned. Anti-spam-listing. |
| Cosmetic card frames (gold tier) | 5,000 – 25,000 | Prestige track, gold-purchasable subset |

**Design intent:** an active player should end each day roughly *break-even* — able to
afford one meaningful fusion, not able to bank indefinitely. Gold banking is the leading
indicator of sink failure.

### 4.2 GEMS — the hard currency

**Role:** The studio's revenue instrument. Bought with real money via IAP.
**Never cashes out. Never trades. Never converts to Market Credits.**

**IAP ladder (proposed, USD):**

| SKU | Price | Gems | Rate (gems/$) | Bonus |
|---|---|---|---|---|
| Pouch | $0.99 | 60 | 60.6 | — |
| Satchel | $4.99 | 330 | 66.1 | +9% |
| Chest | $9.99 | 700 | 70.1 | +16% |
| Vault | $19.99 | 1,500 | 75.0 | +24% |
| Hoard | $49.99 | 4,000 | 80.0 | +32% |
| Dragon's Trove | $99.99 | 8,500 | 85.0 | +40% |

Baseline: **1 gem ≈ $0.0165**. All gem prices below can be read as USD by dividing by 60.

**Sparse non-IAP faucet:** ~250–400 gems/month total from campaign first-clears, season
pass free track, and event placement. Deliberately *not* enough to compete with spending —
enough to make gems feel real to an F2P player.

**Sinks:**

| Sink | Cost | USD equiv | Notes |
|---|---|---|---|
| Standard card pack (5 cards) | 100 | $1.67 | Core sink |
| 10-pack (guaranteed 1 Epic+) | 900 | $15.00 | 10% discount |
| Stamina refill (+50) | 30 | $0.50 | Escalates: 30/60/120/240 per day, resets daily |
| Season pass (premium track) | 900 | $15.00 | Per season |
| Cosmetic frames/skins (gem tier) | 300 – 2,000 | $5–33 | Prestige track |
| Deck slot instant-unlock | 150 | $2.50 | **Speed, not exclusivity** — gold path exists |
| Gold conversion | 50 gems → 5,000 gold | $0.83 | **One-way. See §5.** |

### 4.3 EVENT TOKENS — the seasonal sink

**Role:** Drive event participation. Create urgency. **Absorb value and destroy it.**

- **Earned only** by event participation. No IAP path. No gold/gem conversion in.
- **Expire 14 days after the event's shop closes.** Hard delete, with 7-day and 1-day warnings.
- **Non-tradeable, permanently.** Event Medals must never touch the marketplace, or events
  become a farmable income stream and the entire anti-bot posture collapses.
- **Spent only** in that event's own shop, which sells: event-exclusive `Soulbound` cards
  and trophies (Prestige), crafting reagents, and gold.

**Why expiry is non-negotiable:** a non-expiring event currency is an accumulating claim
on future content — players bank it, then a later event's shop gets drained on day one by
stockpiled tokens, and the event's pacing is destroyed. Expiry makes each event a closed
economy.

**Rollover:** unspent tokens at expiry convert to gold at a punitive rate (10 tokens → 100
gold) so the loss stings but isn't total. **Reviewer: is this too generous? It creates a
weak token→gold faucet — see §12.2.**

### 4.4 MARKET CREDITS — the trade medium (Phase 2)

**Role:** The proceeds of a marketplace sale. **Closed-loop in Phase 1 and 2.**

- **Earned only** by selling a `Tradeable` item to another player on the marketplace.
- **Spent only** on: buying other players' listings, and a restricted **Credit Store**
  (cosmetics, crafting reagents, gold — never gems, never packs, see §5).
- **Cannot be bought.** No IAP path to Market Credits, ever, in any phase. The moment gems
  buy credits, the studio becomes the counterparty to a real-money market and every legal
  concern from the design discussion lands at once.
- **No withdrawal path exists in the client in Phase 1 or 2 — not disabled, not greyed out,
  not present.** A visible-but-inactive "Withdraw" button tells App Review and players that
  real-world value is intended. Phase 3 adds the surface, if legal review clears it.

**Transaction tax: 12% of sale price**, split **6% burned / 6% studio revenue**. The burn
half is the marketplace's own inflation control; the studio half is the recurring revenue
this whole model exists to create.

**Hoarding control:** Market Credits do **not** decay. (Considered and rejected: decay
punishes exactly the cautious mid-tier trader we want participating, and pushes liquidity
into panic-selling. The Credit Store's gold/reagent SKUs are the intended pressure valve.)
**Reviewer: this is a judgment call and a legitimate target — see §12.3.**

### 4.5 Inflation control — the Treasury (Principle 5)

The single biggest failure mode is faucet > sink. Instrumentation is a launch requirement,
not a later addition.

**Required telemetry from day one:**

- Total gold minted vs. burned, daily, per source and per sink
- Median and p90 player gold balance, by account age cohort
- Sink participation rate (what % of active players use each sink weekly)

**Trigger conditions for intervention:**

| Signal | Threshold | Response |
|---|---|---|
| Gold burn / gold mint | < 0.85 for 7 consecutive days | Increase fusion costs or add a sink |
| Median balance growth | > 15% week-over-week for 3 weeks | Same |
| Any single sink | < 5% weekly participation | That sink is decorative — redesign it |

**Principle 1 is conditional on this section actually working.** "Players determine value"
is an outcome of well-engineered scarcity, not a substitute for it. If the Treasury is
cosmetic, player-set pricing just means prices crash to zero.

---

## 5. Conversion matrix — the DAG

**Rule: the conversion graph must be acyclic.** Any cycle is a money printer.

```
   Real Money ──► GEMS ──┬──► Gold ──────► [SINKS: fusion, crafting, monuments]
                         │                    ▲
                         └──► Packs/Cosmetics │
                                              │
   Event Play ──► EVENT TOKENS ──┬──► Event Shop (soulbound goods)
                                 └──► Gold (punitive, at expiry only)
                                              ▲
                                              │
   Marketplace Sale ──► MARKET CREDITS ──┬────┘  (Credit Store: gold, reagents, cosmetics)
                                         └──► Other players' listings
```

**Explicitly forbidden edges (each would create a cycle or a legal problem):**

| Forbidden | Why |
|---|---|
| Gold → Gems | Cycle. Would let players farm revenue currency. |
| Market Credits → Gems | Makes gems earnable by trading; corrupts the IAP price anchor. |
| Gems → Market Credits | Studio becomes counterparty to a real-money market. **Hard no, all phases.** |
| Anything → Event Medals | Would break event pacing and make events buyable. |
| Market Credits → Card packs | Turns the marketplace into a gacha faucet — bot-farm target. |
| Gold → Market Credits | Bots farm gold → credits → real value. **The single most dangerous edge.** |

---

## 6. The Shop

### 6.1 Structure

```
SHOP
├── FEATURED      Rotating bundles, first-purchase offers, season pass
├── PACKS         Card packs (gem + gold tiers)
├── RESOURCES     Stamina, gold conversion, crafting reagents
├── COSMETICS     Frames, avatars, board skins, castle decoration (Prestige track)
├── EVENT         [visible only during an active event] Event Token SKUs
└── MARKET        [Phase 2] Player listings + Credit Store
```

**`EVENT` and `MARKET` are separate top-level tabs, not sections inside the main shop.**
This is deliberate: it makes the "gems cannot buy event goods" boundary visible to the
player and structural in the code, rather than a filter that a future content author can
accidentally bypass.

### 6.2 SKU catalogue (launch)

**PACKS**

| SKU | Cost | Contents | Notes |
|---|---|---|---|
| Novice Pack | 2,500 gold | 3 cards, Common–Rare | Gold path exists for every pack tier |
| Standard Pack | 100 gems | 5 cards, 1 Rare+ guaranteed | |
| Dragon Booster | 300 gems | 5 cards, 1 Epic+ guaranteed | |
| 10-Pack | 900 gems | 50 cards, 1 Legendary guaranteed | Pity: hard-guaranteed at 10 |

**RESOURCES**

| SKU | Cost | Notes |
|---|---|---|
| Stamina +50 | 30 gems (escalating 30/60/120/240/day) | Escalation resets 00:00 UTC |
| Gold Cache | 50 gems → 5,000 gold | One-way (§5) |
| Reagent Bundle | 200 gems or 8,000 gold | Feeds crafting sink |

**COSMETICS** — Prestige track. Some gem-purchasable, some gold-purchasable, **some
neither** (event/milestone only, and those never appear here at all).

**FEATURED**

| SKU | Cost | Notes |
|---|---|---|
| First Purchase Bonus | $4.99 → 330 + 330 gems | Once per account |
| Season Pass (premium) | 900 gems | Competitive-track rewards only, all obtainable free-track eventually |
| Weekly Value Bundle | 500 gems | Rotates Monday |

### 6.3 What the shop may NEVER sell

This is the boundary that makes the whole design coherent. **Enforced by query filter and
by unit test (§10.2), not by author discipline.**

| Forbidden | Reason |
|---|---|
| Any `acquisition == EventOnly` object | Principle: event rewards are earned, never bought |
| Any object where money is the *only* path to a `Competitive` power level | Money buys speed, not power |
| Market Credits, in any quantity, for any currency | §5 forbidden edges |
| Event Medals, for gems or gold | Events must not be buyable |
| Anything that reduces another player's progression | No offensive purchases |
| Randomised boxes without published odds | Legal exposure in multiple jurisdictions, independent of everything else |

### 6.4 Money → Speed, Time → Power (worked example)

| | Whale | F2P |
|---|---|---|
| Ancient Dragon (Legendary, Competitive) | Week 1, via packs | Week 8, via fusion + campaign |
| Same card? | Yes — identical stats, identical art | Yes |
| Season Champion Frame (Prestige) | **Cannot buy at any price** | Earned by placement |
| Guild War Banner (Prestige, Soulbound) | **Cannot buy at any price** | Earned by participation |

The whale bought eight weeks. They did not buy a stronger card, and they cannot buy status.

---

## 7. The Exit Economy

Players leave. The economy should handle that gracefully instead of zeroing them out.

### 7.1 Legacy extraction (additive, never destructive)

Progression milestones **mint** collectibles. The milestone variable is never consumed.

| Milestone | Mints | Tradeable? |
|---|---|---|
| Empire Level 80 | Founder Monument (decoration) | Yes |
| Arena rank ceiling reached | Champion Banner, stamped with season | Yes |
| Guild contribution threshold | Founder Statue | Yes |
| First-season participation | Founder Status (identity marker) | **No — this is Identity, §7.4** |

**Engineering rule: minting a Legacy item must not decrement, reset, or consume the
milestone.** A player who mints a Founder Monument is still Empire Level 80. Anything else
creates a strip-and-abandon exploit.

### 7.2 Archive

A leaving player may **Archive** rather than delete. An archived account:

- **Cannot:** play matches, earn any currency, join guilds, enter ranked, generate event rewards
- **Can:** hold property, manage existing marketplace listings, migrate devices, update credentials

This is a **two-tier session permission model**, not a boolean `isActive` flag. Specify
per-capability, because "archived but can still list" and "archived and frozen" are
different features with different abuse surfaces.

### 7.3 Dormant (automatic, 180 days inactive)

Same permission profile as Archive, entered automatically.

**Critical anti-fraud rule: any property transfer initiated on a Dormant account requires
step-up re-authentication before execution.** A dormant account that suddenly moves assets
is the highest-signal fraud indicator in the entire system — a compromised dormant account
is the ideal laundering pass-through precisely because its owner isn't watching. This is a
gate, not a log line.

### 7.4 Estate (Phase 3, exceptional process only)

- Requires **365 days** of inactivity
- **Successor-initiated**, human-verified request
- **No UI. No button. No "1 succession remaining" indicator anywhere**, or the slot itself
  becomes a sellable product and we have accidentally built the thing we refused to build
- **Eligibility is behaviour/time-gated, never value-gated.** No spend thresholds, no level
  thresholds. A value-gated succession feature visibly functions as a resale product.

**Hard limitation to document in any UI that ever touches this: IAP purchase history never
transfers.** Apple/Google receipts are bound to the purchasing platform account at the
platform level. Estate migrates *game state* to a new auth identity; it cannot and must not
claim to migrate purchases.

### 7.5 What never moves (Principle 7)

Identity and Progression are permanently non-transferable: account, reputation, rank
history, guild history, achievements, Founder Status, friends, chat history, Empire level,
research, quest completion.

**This is also the technical reason the Court works.** The ledger can adjudicate property
disputes cheaply because ownership is logged. It can *never* adjudicate "did I really agree
to hand over my login" — that dispute has no evidence trail and no resolution. Identity
must therefore never be transferable at all, or the dispute becomes unanswerable and the
studio is the one forced to answer it.

---

## 8. Data model changes

### 8.1 `SaveData` deltas (Phase 1)

```csharp
// existing: gold, gems, stamina, maxStamina, unlockedStageIds, activeDeckCardIds
public int eventTokens;              // per-event; see 8.2 — likely moves to its own record
public long marketCredits;           // Phase 2, present-but-unused in Phase 1 schema
public string archiveState;          // "Active" | "Archived" | "Dormant"
public string lastActiveUtcTicks;    // string, not long — JsonUtility 2^53 issue (see SaveData.cs)
```

### 8.2 New records

```csharp
[Serializable] public class EventWallet {
    public string eventId;
    public int tokens;
    public string expiresUtcTicks;   // string, per above
}

[Serializable] public class ItemInstance { /* §3.2 */ }

[Serializable] public class LedgerEntry {
    public ulong instanceId;
    public string fromOwnerId;       // null for mint
    public string toOwnerId;         // null for burn
    public string reason;            // "Mint:GuildWarS2" | "Trade" | "Craft"
    public string utcTicks;
}
```

### 8.3 Build order (this is the one thing I would not compromise on)

**Build the ledger and item-tier flags at launch, even though trading ships in Phase 2.**
Retrofitting immutable ownership history onto items that have been silently mutated in
place for a year of live play is a far worse migration than writing the ledger correctly
now and simply not exposing a marketplace UI on top of it. This changes nothing the player
sees; it changes whether Phase 2 is "ship a UI" or "re-architect the save system again."

---

## 9. Abuse review (Principle 6 — mandatory gate)

Every faucet must be bot-negative: value extractable per account-hour must be lower than
the cost of creating and warming an account.

| Faucet | Bot risk | Mitigation |
|---|---|---|
| Match victory gold | **High** — matches are automatable | Daily cap (15); gold is non-tradeable, so farmed gold cannot exit |
| Daily quests | Medium | Cap; require varied activity, not repeat of one action |
| Event Medals | Low | Non-tradeable + expiring = no exit path for farmed value |
| Marketplace proceeds | **Critical** | §9.1 |
| Legacy minting | Medium | Requires deep progression (Empire 80) — expensive to bot |

### 9.1 Marketplace gates (Phase 2)

Trading eligibility requires **all** of:

- Account age ≥ 30 days **and** ≥ 40 hours of varied match activity
  (age alone is trivially faked by idle accounts; *varied activity* is not)
- Tutorial + first campaign chapter complete
- Step-up authentication enrolled
- Daily throughput cap on tradeable-item volume **per account**, so a 1,000-account farm
  does not scale linearly

### 9.2 Behavioural telemetry — needed in the battle layer from day one

Bot detection for a turn-based card game leans on decision-timing distributions, mulligan
patterns, and click cadence. **This telemetry must be collected before trading exists**, or
there is no historical baseline to detect anomalies against when it does. This is a Phase 1
task in `BattleController`, not a Phase 2 task.

### 9.3 Known abuse case: edition-number race

`edition: 7/100` requires **atomic, server-assigned** sequence numbers. A bot racing the
mint endpoint under load can win duplicate or out-of-order editions — which directly
destroys the provenance value Principle 4 depends on. Minting must be serialised
server-side; client-supplied edition numbers are never trusted.

---

## 10. Enforcement — how the rules survive contact with future content authors

### 10.1 Query-level filtering

Shop inventory is assembled by a query that **excludes `acquisition == EventOnly` at the
data layer**. A content author cannot add an event card to the shop by editing a config,
because the shop cannot see it.

### 10.2 Required unit tests (EditMode)

- No shop SKU resolves to an object with `acquisition == EventOnly`
- No shop SKU grants Market Credits or Event Medals
- The conversion graph (§5) contains no cycles — asserted programmatically
- Legacy minting leaves the source milestone variable unchanged (additive rule)
- Every faucet has at least one registered sink for the same currency
- Event Token expiry actually deletes, and rollover-to-gold applies the punitive rate

These tests are the actual enforcement mechanism. The prose above is documentation; the
tests are the rule.

---

## 11. Phased rollout

| Phase | Ships | Explicitly does not ship |
|---|---|---|
| **1 — Launch** | Gold, Gems, Event Medals. Full shop. Ownership ledger + item flags (no UI). Device migration. Legacy minting. Archive/Dormant. Bot telemetry. | Trading. Market Credits UI. Any transfer. |
| **2 — Stability proven** | Marketplace (closed-loop). Market Credits. Credit Store. Provenance display. Trading gates §9.1. | Any cash-out. Any account transfer. |
| **3 — Mature, gated on legal review** | Possible external web marketplace + withdrawal. Estate succession (exceptional process). | — |

**Phase 2 requires, as entry criteria:** ≥ 6 months live, gold burn/mint ratio stable in
0.9–1.1 for 8 consecutive weeks, bot-detection false-positive rate < 2%, and a functioning
dispute process.

---

## 12. Known weak points — attack these first

I am least confident about the following. They are judgment calls, not derivations.

1. **Gold faucet ~1,600/day vs. fusion costs 500–12,000.** This is a guess. The correct
   numbers come from simulation against real match pacing, which has not been run. The
   ratio is the whole game's pacing, and it is currently vibes.
2. **Event Token → gold rollover at expiry (10:1).** This is a small faucet with no
   participation cost beyond playing the event. It may be exploitable at scale, and the
   "make loss sting but not total" reasoning is emotional, not economic.
3. **Market Credits do not decay.** Defensible, but the counter-argument (hoarded credits
   are latent liquidity that can dump on the market unpredictably) is real and I may be
   wrong.
4. **12% transaction tax, 6/6 split.** Lifted from comparable platforms, not derived from
   this game's velocity. If trade volume is low, 12% may strangle liquidity; if very high,
   6% burn may be insufficient deflation.
5. **Stamina escalation 30/60/120/240.** Aggressive. Risks reading as predatory to the
   exact mid-spend cohort that sustains the game, and stamina-gating a card battler at all
   is arguably in tension with "trading is optional / game is fully enjoyable free."
6. **Naming: "Dragon Credits" for the IAP currency.** Any name containing "Credits"
   adjacent to a system that has a Market Credits currency invites confusion, and
   money-adjacent naming on a currency that explicitly never cashes out is a
   self-inflicted perception risk. Recommend a non-financial name for the hard currency.
7. **Two currencies named similarly (Event Medals / Market Credits)** may confuse players
   in UI where both appear. No mitigation proposed yet.
8. **Nothing in §4.5 has been simulated.** Every threshold in the Treasury section is a
   plausible-sounding number with no model behind it.

---

## 13. What this design will never build

Stated explicitly so it is unambiguous to every collaborator, in every phase:

- Any in-app cash-out, withdrawal, or external payment link inside the mobile binary
- Any priced or facilitated account-transfer feature
- Any purchase of Market Credits with real money or gems
- Any shop path to an event-exclusive reward
- Any commerce-adjacent step, price display, or marketplace surface inside Estate succession
- Any claim, in UI or marketing, that IAP purchase history transfers between accounts
