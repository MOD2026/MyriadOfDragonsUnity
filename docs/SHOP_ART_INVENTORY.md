# Shop Art Inventory

Scope: read-and-measure inventory of `ShopPresenter` at a 1920×1080 `CanvasScaler` reference resolution. No Shop code, art, import settings, contrast values, gameplay, or economy values were changed.

## Runtime verification basis

- `wh_shop_out/run/ShopLayoutContentTests.results.xml` records a passing real `ShopPresenter.Initialize` run for `GemPackTiles_FillProductArtWell_AndBuyIsHitTargetOnly`. The test asserts that every live pack's `ProductArt.sprite` is non-null after the complete runtime load/fallback chain.
- `ShopV1ChromeTests` exercises the real stamina cards and asserts that tier 1–4 state sprites load and swap between locked/unlocked through `ShopV1UiLibrary.LoadStaminaTierSprite`.
- A new short Unity probe was attempted to emit the exact resolved pack fallback names and world rects. Unity's licensing client stalled before executing the test, so the probe was stopped and removed. Exact fallback identity is therefore marked **ambiguous** where the existing passed tests prove only “non-null”.
- Rects below are derived from the exact 1920×1080 pixel bounds and normalized child wells used by the running presenter. No dimensions are inferred from placeholder artwork.

## Live Shop entries

| Real identifier | Entry type | Art expected by code | Runtime load chain and verified result | 1920×1080 render slot | Shared or unique | Unity Image type / 9-slice |
|---|---|---|---|---|---|---|
| `pack_single_sigil` | Gem pack | Unique `UI/ShopV1/product_art_pack_single_sigil`; shared `UI/ShopV1/shop_gem_pack_tile_shared_v1` frame | Unique product path is absent from the resource pack. Runtime `ProductArt.sprite` is verified non-null by the passed Shop test after trying `UI/Icons/dragon_eggs`, then `UI/HomeV3/home_icon_shop_v3`. Existing evidence does not expose which fallback won: **ambiguous**. No flat-colour fallback occurred in that test. | Card: **235×580** (0.405:1). Product-art well: **148.52×279.56** (0.531:1). | Product art must be unique; frame is shared by all four packs. | Product art and frame are both `Simple` with `preserveAspect=true`; **not 9-sliced**. |
| `pack_scout_cache` | Gem pack | Unique `UI/ShopV1/product_art_pack_scout_cache`; same shared frame | Same verified non-null fallback chain and same exact-name ambiguity as above; no flat-colour fallback in the passed test. | Card: **236×580** (0.407:1). Product-art well: **149.15×279.56** (0.534:1). | Unique product art; shared frame. | `Simple`, preserve aspect; **not 9-sliced**. |
| `pack_warband_cache` | Gem pack | Unique `UI/ShopV1/product_art_pack_warband_cache`; same shared frame | Same verified non-null fallback chain and same exact-name ambiguity as above; no flat-colour fallback in the passed test. | Card: **237×580** (0.409:1). Product-art well: **149.78×279.56** (0.536:1). | Unique product art; shared frame. | `Simple`, preserve aspect; **not 9-sliced**. |
| `pack_legion_cache` | Gem pack | Unique `UI/ShopV1/product_art_pack_legion_cache`; same shared frame | Same verified non-null fallback chain and same exact-name ambiguity as above; no flat-colour fallback in the passed test. | Card: **218×610** (0.357:1). Product-art well: **137.78×294.02** (0.469:1). | Unique product art; shared frame. | `Simple`, preserve aspect; **not 9-sliced**. |
| `res_energy` | Stamina ladder tier 1 | Unique locked and unlocked tier sprites: `shop_stamina_tier1_locked_v1`, `shop_stamina_tier1_unlocked_v1`; shared `UI/Icons/icon_stamina` | Both tier states are exercised through real `Resources.Load<Sprite>` calls and verified non-null by Shop tests. `icon_stamina` is also verified non-null on the live card. Flat colour is only the failure branch and was not reached. | Row: **640×150** (4.267:1). Icon well: **128.64×54.30** (2.369:1). | Tier background/state art unique to tier 1; stamina icon shared across all four tiers. | Both `Simple`, preserve aspect; **not 9-sliced**. |
| `res_stamina_60` | Stamina ladder tier 2 | Unique `shop_stamina_tier2_locked_v1` and `shop_stamina_tier2_unlocked_v1`; shared stamina icon | Locked and unlocked tier 2 sprites are directly exercised and verified non-null by the state-swap test; icon verified non-null. | Row: **640×150**; icon well **128.64×54.30**. | Unique tier 2 states; shared icon. | `Simple`, preserve aspect; **not 9-sliced**. |
| `res_stamina_120` | Stamina ladder tier 3 | Unique `shop_stamina_tier3_locked_v1` and `shop_stamina_tier3_unlocked_v1`; shared stamina icon | Shop V1 resource-pack test verifies the stamina sprite family loads; live card test verifies icon assignment. Per-state resolved names are not emitted by the existing test: **ambiguous at individual-name level**, but the runtime card does not use flat colour when the family loads. | Row: **640×150**; icon well **128.64×54.30**. | Unique tier 3 states; shared icon. | `Simple`, preserve aspect; **not 9-sliced**. |
| `res_stamina_240` | Stamina ladder tier 4 | Unique `shop_stamina_tier4_locked_v1` and `shop_stamina_tier4_unlocked_v1`; shared stamina icon | Shop V1 resource-pack test directly verifies tier 4 locked loads; live card test verifies icon assignment. Tier 4 unlocked exact runtime identity is not separately emitted: **ambiguous at individual-name level**. | Row: **640×150**; icon well **128.64×54.30**. | Unique tier 4 states; shared icon. | `Simple`, preserve aspect; **not 9-sliced**. |

## Withheld entries present in `shopItems` but not rendered

| Real identifier | Status | Art requirement |
|---|---|---|
| `pack_novice` | `hideFromShopGrid: true` | No runtime art slot. Do not commission Shop-grid art unless this entry is separately approved for the live catalog. |
| `pack_dragon` | `hideFromShopGrid: true` | No runtime art slot. |
| `res_gold` | `hideFromShopGrid: true` | No runtime art slot. |

## Shared Shop surfaces

| Surface | Runtime resource | Source dimensions / format | Runtime rect and behavior | Sharing / slicing |
|---|---|---|---|---|
| Catalog shell | `UI/ShopV1/shop_catalog_grid_landscape_v1` | 1920×1080 RGBA PNG | Full 1920×1080 screen; load is asserted by `ShopV1ChromeTests`. | One shared shell; `Simple`, stretched fullscreen; not sliced. |
| Gem-pack frame | `UI/ShopV1/shop_gem_pack_tile_shared_v1` | 1114×1411 RGBA PNG | Full card rect for each pack, preserve aspect. | Shared by all four packs; `Simple`; not sliced. |
| Pack-open frame | `UI/ShopV1/pack_open_reveal_landscape_v1` | 1920×1080 RGBA PNG | Pack-open overlay shell, not a per-SKU product image. | Shared; `Simple`; not sliced. |
| Stamina state art | Eight `shop_stamina_tier{1..4}_{locked|unlocked}_v1` files | Tier 1/4: 467×257; tier 2/3: 468×257; RGBA PNG | Each is placed in a 640×150 row with preserve aspect. Because source aspect ≈1.82 and slot aspect ≈4.27, the visible image fits to about **273×150**, leaving horizontal space; it is not stretched to fill. | Unique by tier and state; not sliced. |
| Stamina icon | `UI/Icons/icon_stamina` | 340×699 RGBA PNG | Shared icon well 128.64×54.30 with preserve aspect; visible image fits to about **26.4×54.3**. | Shared; `Simple`; not sliced. |

## Confirmed JPEG placeholder finding

- File: `Assets/Resources/UI/Icons/dragon_eggs.jpg`
- Encoded format: **JPEG**
- Pixel mode: **RGB** (no alpha)
- Source size: **287×303**, aspect **0.947:1**
- Use: first fallback attempted for every missing gem-pack product-art resource.
- Runtime scaling: all product wells are substantially narrower than the JPEG. With `preserveAspect=true`, it is **downscaled**, not upscaled:
  - pack 1: approximately 148.52×156.80 visible inside a 148.52×279.56 well;
  - pack 2: approximately 149.15×157.47;
  - pack 3: approximately 149.78×158.12;
  - pack 4: approximately 137.78×145.46 inside a 137.78×294.02 well.
- Consequence: it leaves large unused vertical space and cannot provide transparent per-product art. It is not an appropriate production master for the four unique pack illustrations.

## Commissioning inventory

The confirmed missing production set is four unique transparent product illustrations:

1. `product_art_pack_single_sigil`
2. `product_art_pack_scout_cache`
3. `product_art_pack_warband_cache`
4. `product_art_pack_legion_cache`

Author each for its measured product-art well, with the first three targeting approximately **0.53:1** and Legion approximately **0.47:1**. None requires 9-slice borders. The shared frame, shell, pack-open frame, stamina tier states, and stamina icon already exist and must not be duplicated as product artwork.

## Remaining ambiguity

The existing passed runtime test proves every live pack receives a non-null sprite, but does not log whether `dragon_eggs` or `home_icon_shop_v3` is the successful fallback. The dedicated probe that would have captured the exact sprite name was blocked by Unity licensing initialization. This distinction does not change the missing-art count: all four SKU-specific `product_art_*` resources remain absent and require unique art.
