# Art / UI Asset Triage Policy (LOCKED process — 2026-08-22)

## Principles

1. **Doc / inventory first** — no mass delete until owner confirms a quarantine batch.
2. **Live paths stay** — anything `Resources.Load`ed in production code is **KEEP_LIVE** until code is retargeted.
3. **Out of `Resources/` = out of APK** — quarantine lives under `Assets/_ArtTriage/` so dumps stop shipping.
4. **Reuse before regenerate** — cut usable pieces from dumps into `ReuseBank/` with clean names.

## Can Command Centre judge good vs bad?

| I can do reliably | I cannot replace you on |
|---|---|
| Used vs unused (code refs) | Final brand taste between two good options |
| Resolution too low for role (VFX vs icon) | “Does this feel Myriad?” as sole decider |
| Style clash (cartoon vs dark epic) | Legal/IP of sourced packs |
| Technical mess (`.png.png`, spaces, sheets vs cutouts) | Paying for new art commissions |
| Obvious duplicate of a live better file | — |
| Readability at UI size (busy portrait) | — |

**Rubric (score each asset 1–5):** silhouette clarity · style match to city/HomeV3 dark-epic · resolution for intended role · transparency/clean edge · not a sheet/mockup · no baked-in English label unless we want it.

| Score | Bucket |
|---:|---|
| 5 / live-wired | **KEEP_LIVE** |
| 4–5 unused | **REUSE_BANK** (rename + hold for Shop/Empire/Guild) |
| 2–3 or dump/sheet | **QUARANTINE** |
| 1 broken/unusable | **REJECT** (delete only after owner OK) |

## Folder layout

```
Assets/_ArtTriage/
  README.md                 (this policy pointer)
  Quarantine/               (not under Resources — not in builds)
  ReuseBank/                (good unused — clean names, still not Resources until wired)
  RejectCandidates/         (await delete OK)
```

## First cleanup batch (this pass)

1. Move `Resources/UI/Icons/ai generated asset/` → `_ArtTriage/Quarantine/ai_generated_asset_dump/`
2. Move backdrop `*_CANDIDATE*` / `*_LEGACY_BACKUP*` → `_ArtTriage/Quarantine/backdrops/`
3. Leave HomeV3 unused tiles in place but listed as **REUSE_BANK / wire-later** (already correct folder, just unwired)
4. Icon bank with clean Empire/Shop/NPC candidates stay in `Resources/UI/Icons` for now — listed in inventory for Metagame reuse
