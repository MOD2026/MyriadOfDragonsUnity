# Shop V1 Release Contract

**Status:** Design/audit document. No production code, test, or Bible change accompanies this file.
**Grounded in:** `Assets/Scripts/UI/ShopPresenter.cs`, `Assets/Scripts/Save/PlayerProfile.cs`,
`Assets/Scripts/Save/SaveMigration.cs`, `Assets/Scripts/Cards/CardDatabase.cs`,
`Assets/Scripts/Economy/CurrencyManager.cs`, `Assets/Scripts/UI/CampaignMapPresenter.cs`,
`Assets/Tests/Editor/ShopCardPersistenceTests.cs`, `docs/MOS_v1.1.md`.
**Rule followed throughout:** no invented IAP pricing, no invented drop odds. Every number below is
either an existing constant read from `ShopPresenter.cs` or explicitly marked as "to be set."

---

## 1. Capability Matrix

| Feature | Current support | Evidence |
|---|---|---|
| Daily free claim | **None.** | `PlayerProfile` has no date/timestamp field of any kind (no `lastLoginDate`, `lastClaimUtc`, `dailyStreak`). There is nothing to gate a "once per day" claim against. |
| Rotating soft-currency offers | **None.** | `ShopPresenter.SetupShopItems()` builds one fixed, hardcoded `List<ShopItemData>` on every `Initialize()`. No rotation index, no seed, no "current offer set" persisted anywhere. |
| Deterministic card packs | **Implemented.** | `TryGrantNextUnownedCard` grants the first id in `CardDatabase.AllCards` order (excluding the placeholder id `"dragon"`) not already in `profile.cardCollection`. Deterministic, no RNG, covered by 8 tests in `ShopCardPersistenceTests.cs`. |
| Premium-currency purchases | **Implemented (Gems only).** | `pack_dragon` (100 Gems), `res_gold` (50 Gems), `res_energy` (30 Gems) already spend `player.gems` via `AttemptPurchase`. |
| First-clear/progression bundles | **None in Shop.** | Stage-clear rewards exist but live entirely in `CampaignMapPresenter`/`PlayerProfile.claimedStageRewardIds` (Chapter 1 progression contract), a separate system with its own claim gate. `ShopPresenter` has no notion of a stage-gated offer. |
| Limited-time bundles | **None.** | Requires a start/end time; no timestamp field exists (same gap as daily claim). |
| Real-money IAP | **None.** | No receipt field, no store SDK reference, no purchased-product-id tracking anywhere in `PlayerProfile` or `ShopPresenter`. All four existing items spend only `gold`/`gems`. |
| Battle pass | **None.** | No tier/track field, no season id, no XP-toward-track field in `PlayerProfile`. |
| Bank/piggy bank | **None.** | No accrual field, no cap field, no "collect" state. |
| Chain offers (buy A unlocks B) | **None.** | `ShopItemData` has no prerequisite/unlock-chain field; every item is independently purchasable at all times (subject only to currency and, for cards, unowned-card availability). |
| Duplicate compensation | **Partial, incidental.** | `TryGrantNextUnownedCard` already refuses to grant an owned card — it returns `false` and `AttemptPurchase` spends nothing (`ExhaustedRewardSequence_DoesNotSpendCurrency_...` test). This prevents duplicates but does not *compensate* for one (no fallback currency/material payout on exhaustion). |
| Loyalty/reward track | **None.** | No streak counter, no cumulative-spend counter, no milestone list. |

**Cross-cutting gap, evidenced in code:** `CurrencyManager.cs` already exists as a typed
`GetBalance`/`AddCurrency`/`SpendCurrency` service over all five currencies (Gold, Gems,
EventMedal, GuildContribution, DragonRelic), and calls `SaveSystem.Save(profile)` itself. `ShopPresenter.AttemptPurchase` does not use it — it reads/writes `player.gold`/`player.gems` directly and calls `SaveSystem.Save(player)` separately. Two independent spend paths exist for the same fields today. This is a pre-existing inconsistency, not something to silently fix in this document (see §5).

---

## 2. Three-Tier Roadmap

### Release V1 — ships with existing local profile/save/card systems, no new fields
- Deterministic card packs (Gold and Gems) — already implemented, already tested.
- Direct currency-for-currency/resource conversions (Gold Vault, Energy Potion) — already implemented, already tested.
- A **finite, non-rotating** catalogue (fixed at build time, same for every player) — matches what `ShopPresenter` already is. No offer-rotation logic needs to be written for V1.

### Post-release V2 — requires additional local schema/state, still fully offline
Each item below needs a new `PlayerProfile` field (added the same way `claimedStageRewardIds` was
added: additive, nullable-safe, with a matching `SaveMigration.Normalize` null-coalesce/default line),
but needs **no server, no payment, no clock trust boundary**:
- Daily free claim — needs a `lastClaimUtcTicks` (or date-string) field + one `stamp-and-compare` check. Client-clock-based; acceptable for a non-monetary free reward, not acceptable for anything with real-money weight.
- First-clear/progression bundles sold through the Shop UI (as opposed to auto-granted by Campaign) — needs a `List<string> claimedShopBundleIds`, same pattern as `claimedStageRewardIds`.
- Duplicate compensation — needs a defined fallback payout (e.g., an existing currency amount) wired into `TryGrantNextUnownedCard`'s failure branch, plus a `PlayerProfile` counter if the payout should scale.
- Loyalty/reward track — needs a `int shopMilestoneProgress` (or similar) field and a milestone table.
- Chain offers — needs a small prerequisite graph on `ShopItemData` plus a `List<string> unlockedShopOfferIds` on the profile.
- Route all of the above's currency movement through `CurrencyManager` instead of direct field writes, closing the two-path inconsistency noted in §1.

### Live-ops/V3 — requires infrastructure this repo does not have
- Rotating offers with a real refresh cadence — needs a trusted (server or verified) clock, since a local device clock can be rolled back to re-farm a "daily" reset.
- Real-money IAP — needs store SDK integration, server-side receipt verification, and refund/chargeback handling. `PlayerProfile` has no receipt field today; adding one without verification would let a save-file edit fabricate paid currency.
- Limited-time bundles — same trusted-clock dependency as rotating offers.
- Battle pass — needs season/backend definition of the track content, plus (if it has any real-money tier) the IAP dependency above.
- Bank/piggy bank — if it involves a "buy out early" IAP hook, inherits the IAP dependency; the accrual mechanic itself could technically be V2, but bundling it with V3 avoids building an accrual system twice.
- Analytics-driven personalization of any offer — needs an analytics pipeline this project does not have.
- Anything requiring a legal disclosure (odds disclosure, purchase terms) — out of scope until real-money IAP is in scope, since deterministic-only V1/V2 offers have no odds to disclose.

---

## 3. V1 Shop Catalogue

This is the **existing, already-implemented** catalogue in `ShopPresenter.SetupShopItems()` — no
new item is proposed for V1, per the instruction not to invent pricing. All four are already covered
by `ShopCardPersistenceTests.cs`.

| Offer | Currency | Price | Reward | Eligibility | Repeat/exhaustion behavior | Persistence owner |
|---|---|---|---|---|---|---|
| `pack_novice` — Novice Card Pack | Gold | 500 | One card via `TryGrantNextUnownedCard` (next unowned id in `CardDatabase.AllCards` order, excluding `"dragon"`) | Any profile with ≥500 Gold | Repeatable while unowned real cards remain. Once the profile owns every real card, purchase attempt spends **nothing** and grants **nothing** (`ExhaustedRewardSequence_...` test) — this is the existing failure behavior, not a proposal. | `PlayerProfile.cardCollection` (+`gold`), written via `SaveSystem.Save` inside `ShopPresenter.AttemptPurchase` |
| `pack_dragon` — Dragon Booster | Gems | 100 | Same `TryGrantNextUnownedCard` reward rule as above | Any profile with ≥100 Gems | Same exhaustion behavior as `pack_novice` (shares the same unowned-card pool — a card granted by either pack is no longer eligible for the other) | `PlayerProfile.cardCollection` (+`gems`), same save path |
| `res_gold` — Gold Vault | Gems | 50 | +1,500 Gold | Any profile with ≥50 Gems | Always fulfillable (currency conversion, no exhaustion condition) — freely repeatable | `PlayerProfile.gold`, same save path |
| `res_energy` — Energy Potion | Gems | 30 | +50 Stamina, clamped to `player.maxStamina` | Any profile with ≥30 Gems | Always fulfillable; reward is clamped so overbuying past the Stamina cap wastes the excess but is not blocked outright (matches current `Mathf.Min` behavior — not a proposed change) | `PlayerProfile.stamina`, same save path |

No fifth item is added, and no price on the four above is changed — this section describes what
ships, it does not propose new content.

---

## 4. Requirements Before Any V2/V3 Package

**Schema additions (all follow the `claimedStageRewardIds` precedent exactly — additive field +
matching `SaveMigration.Normalize` line, never a breaking rename or removal):**
- A time-tracking field (`lastClaimUtcTicks` or equivalent) before any daily/time-gated feature.
- A claimed-shop-bundle id set (mirroring `claimedStageRewardIds`) before any Shop-native
  progression/first-clear bundle.
- A purchased-offer/receipt id set before any real-money IAP — required specifically so a later V3
  can distinguish "already fulfilled this receipt" from "fulfill again," which nothing in the current
  schema can do.
- A milestone/track progress counter before any loyalty track or battle pass.

**Service boundary requirement:** any new spend/grant path must go through `CurrencyManager`
(`GetBalance`/`AddCurrency`/`SpendCurrency`), not direct `profile.gold`/`profile.gems` field writes.
`ShopPresenter`'s current direct-field pattern is pre-existing and out of scope to change in this
document, but any *new* V2 offer type should not replicate it — see §1's cross-cutting gap.

**Trust boundary requirement (V3 only):** rotating/limited-time/IAP features must not rely solely on
the local device clock or local save file as the source of truth for "has this reset happened" or
"was this payment real" — `SaveMigration.cs` confirms there is currently no version-stamped,
tamper-evident state at all, only a null-coalescing normalizer. A trusted clock/verification layer is
an infrastructure prerequisite, not a schema change.

---

## 5. Player-Safe Ruleset

- **No fake rewards.** Every granted item must resolve through a real system of record —
  `CardDatabase.GetCard(id)` for cards, an actual `PlayerProfile` field for currency/resources. No
  offer may claim a reward that has no backing field or database entry (already true for all four V1
  items; must remain true for every V2/V3 addition).
- **No silent deck changes.** A Shop purchase must never write `activeDeckCardIds` — already enforced
  and tested (`Purchase_DoesNotModifyActiveDeckCardIds`). Any V2/V3 feature that grants a card must
  preserve this: granting ownership is not the same as equipping it.
- **No duplicate ownership.** `TryGrantNextUnownedCard` already refuses to grant an id already in
  `cardCollection`. Any future reward path (bundles, loyalty track, IAP packs) must route through the
  same check or an equivalent one — never a reward path that can independently re-grant an owned id.
- **No spend on an exhausted offer.** Currency must only be deducted *after* `onPurchase` reports a
  successful fulfillment (`AttemptPurchase`'s existing fulfil-then-spend order). This must remain the
  pattern for every future offer type, including time-limited and IAP ones: verify fulfillability,
  then charge, never the reverse.
- **Clear failure behavior.** On an unfulfillable purchase, the existing behavior is: no currency
  spent, no partial state change, and a logged reason (`"{title}: purchase could not be fulfilled -
  no currency spent."`). Any new offer type must fail the same way — silently, safely, and
  atomically — never leaving a profile with currency deducted but no reward, or a reward granted but
  currency untouched.

---

## 6. Recommended Next Implementation Slice

**Route `ShopPresenter.AttemptPurchase` through `CurrencyManager` instead of direct `player.gold`/
`player.gems` field writes**, before any V2 feature is built on top of it.

This is the smallest change that removes the one concrete inconsistency this audit found in
already-shipped code (§1's cross-cutting gap): two independent spend paths exist for the same
currency fields today (`ShopPresenter`'s direct writes vs. `CurrencyManager`'s
`GetBalance`/`SpendCurrency`/`AddCurrency`). Left alone, every V2 feature in §2 (daily claim,
bundles, loyalty track) will have to choose which path to imitate, likely deepening the split. Fixing
it now, while the Shop is still a 4-item, fully-tested, fully-local system, is materially cheaper than
fixing it after V2 schema fields and their tests exist on top of the current direct-write pattern.
