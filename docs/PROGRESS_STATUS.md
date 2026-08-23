# Progress status

**Last updated:** 2026-08-22

## Golden rules

1. **Fastest route** — Editor Play + EditMode tests (no APK unless you ask).
2. **MVP first** — ship playable loop; agents self-check before human playtest.
3. Theory locked → code immediately.

## MVP loop

| Step | Screen | Status |
|---|---|---|
| Home | HomeV3 art + 5 nav tiles | **Done** |
| Campaign | 2–3 stage window + progression | **Done** |
| Battle | Tutorial + campaign launch | Live |
| Empire | Dedicated screen | **Done** |
| Shop | Gem packs + **pack-open reveal overlay** | **Done** |
| Collection | Burn yields (packet table) + evolve sinks + XP→level | **Done** |
| Deck Builder | Reads `cardProgression` when V1 | **Done** |
| Starter grant | V1 → `cardProgression` via TryGrantFirstCopy | **Done** |
| Bazaar / pack polish animation | — | Phase 2 |

## Self-check (automated — no manual test needed)

**Latest batch: 47/47 passed** (campaign, shop, pack reveal, burn, evolution, home, empire).

Additional suites:
- `DeckBuilderCollectionOwnershipTests` (3)
- `StarterGrantCollectionV1Tests` (3)
- `ShopToDeckIntegrationTests` (1) — gem pack → progression → deck builder

## Play flow (when you choose to look)

`Assets/Scenes/HomePagePresenter.unity` → Play → Campaign / Shop / Cards.
