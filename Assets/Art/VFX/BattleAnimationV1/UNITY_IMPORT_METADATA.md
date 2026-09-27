# Battle Animation V1 import metadata

- Import all PNGs as Sprite (2D and UI), sRGB enabled, straight alpha preserved, no mipmaps.
- Atlas files use Multiple sprites, 4 horizontal 256×256 rects at x=0/256/512/768; no extrusion across frame boundaries.
- `firestorm_impact_overlay.png` uses Single sprite, 512×512 full rect.
- Reduced files are Single sprites for static reduced-motion fallback.
- Use Bilinear filtering and lossless/none compression; disable Read/Write after import.
- No baked text, labels, gameplay values, or UI chrome are present.

PSD masters are not included: a valid layered PSD cannot be produced safely in this environment without a PSD authoring tool. The PNGs remain the only generated assets.
