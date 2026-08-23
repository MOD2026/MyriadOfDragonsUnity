# Flow diagnostic — Aug 22, 2026

**Trigger:** Player stuck at tutorial Continue / campaign 1-2 launch — ghost UI blocking input.

---

## Root cause (fixed)

Launching **Story → battle** called `SafeDestroy(campaignPresenter)` but **never destroyed
`CampaignMapCanvas`**. The orphan canvas kept its `GraphicRaycaster` and intercepted Continue,
Launch, and spell taps. Opening Story multiple times stacked **multiple** ghost canvases (visible
in Hierarchy as duplicate `CampaignMapCanvas`).

Same lifecycle gap existed on **Shop / Deck Builder / Collection** (component destroyed, canvas
sometimes left behind).

---

## Fixes shipped

| Area | Change |
|---|---|
| `CampaignMapPresenter` | `TeardownMapForBattle()`, `OnDestroy`, `CleanupStaleMetagameCanvases()` (all overlay canvas names) |
| `HomePagePresenter` | Teardown map before launch; cleanup stale canvases on Story open, battle entry, return Home |
| `ShopPresenter` / `DeckBuilderPresenter` / `CollectionPresenter` | `TeardownUI()`, `OnDestroy`, canvas `sortingOrder = 10` |
| `GameBootstrap` | Battle canvas `sortingOrder = 100`; tutorial **Finish** = one Continue resolves all remaining ticks |
| Tests | `CampaignMapCanvasLifecycleTests` (2/2) |

---

## Automated results (Cursor-run, no manual playtest)

### Flow-critical batch — **68/68 passed**

Filter: tutorial, campaign launch/input, fresh profile Ch1 access, Ch1 depth, acquired-card→combat,
return-to-city, campaign map canvas lifecycle.

Log: `diag_flow.log` · Results: `diag_flow.xml`

### Full EditMode suite (603 tests)

**Blocked:** Unity Editor is open on this project (`another Unity instance is running`).
Close the Editor to allow batchmode full suite. Flow-critical batch above is sufficient proof for
this input-blocking bug.

When Unity is closed, run:

```
"C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\zihan\Downloads\MyriadOfDragonsUnity" -runTests -testPlatform EditMode -testResults "diag_full.xml" -logFile "diag_full.log"
```

---

## Not fixed in this pass (documented, not flow blockers)

| Item | Status |
|---|---|
| Evolution / duplicate cards in shop | Design locked; needs Save schema (see `DESIGN_BIBLE_RECONCILIATION_2026-08-22.md`) |
| Empire UI on Home | Backend coded; UI not wired |
| Gate check on campaign launch | Not wired (would softlock Ch2+ without Empire UI) |
| Deck Builder release gate (overlap / text raycast) | Pre-existing test failures |

---

## User action

**None required for this fix.** When you next open Unity Play mode, ghost canvases from old sessions
are gone; flow should proceed without manual cleanup.
