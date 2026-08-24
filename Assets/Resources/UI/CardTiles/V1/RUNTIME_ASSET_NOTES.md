# Card Tile Composition V1 — runtime notes (POC)

Native tile: **400×600 RGBA** (2:3 portrait).

## Layer order (back → front, Unity sibling order)

1. **CardPortrait** — per-card art tile PNG (card_tile_art_{cardId}_v1)
2. **CardFrame** — shared frame (card_tile_frame_v1) with empty stat/name wells
3. **Name** — runtime Text from card_data.json 
ame
4. **ClassSchool** — runtime Text: {element} · {type} from card_data
5. **Cost**, **AtkStat**, **HpStat** — separate runtime Text (values only; wells empty in art)

## POC card IDs (verified vs card_data.json)

- warrior
- archer_dragon
- cyclops
- dragonqueen

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

Non-POC cards fall back to legacy HomeV3 rarity-frame composition until full 85-card crop rollout.
