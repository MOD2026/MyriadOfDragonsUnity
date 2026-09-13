# CR-RELEASE-051 — rc31 dashboard decision: **FAIL** (gate mode)

**Room:** CR (tooling). **Source:** real acceptance report, not manually scored.

Ran `tools/generate_candidate_dashboard_summary.ps1` directly against LK's real rc31 gate-mode
acceptance report — `C:\Users\zihan\Downloads\MoD-lk-line-019\lk_rc31_acceptance.json`, produced by
LK's own `generate_candidate_acceptance_report.ps1` run (`lk_rc31_acceptance.json` / `.md`, see
[LK-RELEASE-050](LK-RELEASE-050-rc31-NEXT-LINE.md)). This tool only reads that JSON; it does not
re-run any check, re-hash any file, or re-score any evidence itself.

```
powershell -ExecutionPolicy Bypass -File tools\generate_candidate_dashboard_summary.ps1 `
  -ReportJson "C:\Users\zihan\Downloads\MoD-lk-line-019\lk_rc31_acceptance.json" `
  -OutSummary "C:\Users\zihan\Downloads\MoD-lk-line-019\lk_rc31_dashboard.md"
```

Exit code: **1** (FAIL — a normal, reportable outcome; the summary is still written).

## Decision: FAIL

| | |
|---|---|
| **Mode** | GATE (pre-capture — no captures exist yet, none expected) |
| **HEAD** | `ee81680b3e0da7b448c05940db5c593054dc91e2` — matches expected |
| **Tracked dirt** | 0 |
| **Frozen tag** | none supplied |
| **Test totals** | 2335 total, 2327 passed, **5 failed**, skipped: *(not reported — source report predates the `skipped` schema field, schemaVersion 1)* |
| **Capture completeness** | skipped (gate mode) |
| **Resolution** | n/a (no captures) |
| **Forbidden-text findings** | 1 — `FORBIDDEN MARKER (HTTP 404) found in lk_rc31_build.log: (matched '404')` |

## Required owner actions (from the dashboard, verbatim)

- **LK:** no frozen/immutable tag was supplied for this candidate — publish one before this line can be treated as a release candidate.
- **Owner/dev:** 5 of 2335 tests failed — investigate and fix before acceptance.
- **Dev/ST:** forbidden runtime text found (1 occurrence(s)) — resolve real errors/placeholders before this candidate can pass.

## Known caveat on the forbidden-text finding (not altered by this tool)

LK's own report ([LK-RELEASE-050](LK-RELEASE-050-rc31-NEXT-LINE.md) §4) already identified this
exact "404" hit as a **false positive**: it is Unity's build-step counter
(`[404/660    0s] CopyFiles …/UnityEngine.SubsystemsModule.dll`), not an HTTP error, and the same
counter pattern appears in the rc29/rc26 build logs depending on step count. The dashboard reports
findings straight from the acceptance report's own `forbiddenRuntimeText` field — it does not
second-guess or filter them, so this known false positive still surfaces here. The underlying
pattern (`(?<![0-9])404(?![0-9])` in `validate_release_candidate.ps1` / `CandidateAcceptanceLib.ps1`)
still needs tightening by its owner; not done as part of this task (out of scope — this task was
"run the dashboard tool and publish the decision," not "fix the gate's false positive").

## What this confirms / does not confirm

- Confirms LK's own conclusion: **rc31 is not acceptance-clean** — 5 real test failures (none
  tablet-excluded) and no frozen tag. The release lane correctly stays held at rc30.
- Does not add or remove any finding beyond what the source acceptance report already contains.
- No candidate/game code was touched. No manual scoring was performed — every fact above is a
  direct field from the generated dashboard.

Full rendered dashboard: `C:\Users\zihan\Downloads\MoD-lk-line-019\lk_rc31_dashboard.md` (LK's
worktree, not this repo).
