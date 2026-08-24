# Card Tile Composition V1 — runtime catalog

Native tile: **400×600 RGBA** (2:3 portrait).

## Layer order (back → front, Unity sibling order)

1. **CardPortrait** — per-card art tile PNG (card_tile_art_{cardId}_v1)
2. **CardFrame** — shared frame (card_tile_frame_v1) with empty stat/name wells
3. **Name** — runtime Text from card_data.json 
ame
4. **ClassSchool** — runtime Text: {element} · {type} from card_data
5. **Cost**, **AtkStat**, **HpStat** — separate runtime Text (values only; wells empty in art)

## Catalog rollout

- **82 cards** use the V1 composition through authored `card_tile_art_{cardId}_v1` assets.
- **14 of those 82** intentionally use approved logo-bearing source art and remain tracked for later replacement.
- `dragon_tamer`, `ancient_dragon`, and `forest_fairy` intentionally remain on the legacy rarity-frame tile because no reliable source match was approved.
- Runtime selection is asset-driven; the complete ID list is not duplicated manually in presenter code.

## Normalised safe boxes (400×600 space)

| Layer | left | bottom | right | top |
|---|---:|---:|---:|---:|
| Portrait | 0.06 | 0.26 | 0.94 | 0.92 |
| Frame | 0 | 0 | 1 | 1 |
| Name | 0.08 | 0.18 | 0.92 | 0.26 |
| ClassSchool | 0.08 | 0.12 | 0.92 | 0.18 |
| Cost | 0.06 | 0.84 | 0.24 | 0.96 |
| AtkStat | 0.06 | 0.04 | 0.48 | 0.12 |
| HpStat | 0.52 | 0.04 | 0.94 | 0.12 |

Cards without an approved V1 portrait asset fall back to the legacy rarity-frame composition.
