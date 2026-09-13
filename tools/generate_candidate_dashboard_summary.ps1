# Release-candidate dashboard summary - reads the JSON produced by
# tools/generate_candidate_acceptance_report.ps1 and renders one concise, decision-ready summary.
#
# This is a pure CONSUMER of that JSON - it re-derives nothing by re-running any check, re-hashing
# any file, or re-scoring any evidence. Every fact in the dashboard (PASS/FAIL, identity, totals,
# findings) is read directly from the report JSON's own fields. The one thing this tool adds is a
# deterministic mapping from specific finding patterns already in the report to a short list of
# "required owner actions" - a pure function of what's already in the JSON, not a fresh judgment
# call about the candidate.
#
# Usage (summarise a real acceptance report):
#   powershell -File tools/generate_candidate_dashboard_summary.ps1 `
#     -ReportJson "rc19_acceptance.json" -OutSummary "rc19_dashboard.md"
#
# Usage (run the tool's own automated tests, no candidate/build/Unity needed):
#   powershell -File tools/generate_candidate_dashboard_summary.ps1 -SelfTest
#
# Exit codes: 0 = summary written, candidate PASS. 1 = summary written, candidate FAIL (a FAIL is a
# normal, reportable outcome - the summary is still produced). 2 = the tool itself could not run
# (missing/malformed input JSON, bad arguments). -SelfTest exits 0 only if every internal test
# passes.

[CmdletBinding(DefaultParameterSetName = "Summary")]
param(
    [Parameter(ParameterSetName = "Summary", Mandatory = $true)]
    [string]$ReportJson,

    [Parameter(ParameterSetName = "Summary")]
    [string]$OutSummary = "",

    [Parameter(ParameterSetName = "SelfTest", Mandatory = $true)]
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

# Schema versions this tool knows how to read. tools/generate_candidate_acceptance_report.ps1 is
# the only producer of this JSON - bump both together and add a version here rather than silently
# guessing at an unrecognised shape.
$KnownSchemaVersions = @(1, 2)

# ---------------------------------------------------------------------------
# Reads and minimally validates the report JSON. Returns the parsed object; throws with a clear
# message on anything that isn't a real, schema-recognised acceptance report - this tool must never
# silently fabricate a dashboard from a malformed or foreign JSON file.
# ---------------------------------------------------------------------------
function Read-AcceptanceReportJson {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw "Report JSON not found: $Path" }
    try {
        $report = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Report JSON is not valid JSON: $Path ($($_.Exception.Message))"
    }
    $required = @("schemaVersion", "decision", "candidateIdentity", "buildHash", "testTotals", "captureCompleteness")
    foreach ($field in $required) {
        if (-not ($report.PSObject.Properties.Name -contains $field)) {
            throw "Report JSON is missing required field '$field' - not a recognised acceptance report: $Path"
        }
    }
    if ($KnownSchemaVersions -notcontains [int]$report.schemaVersion) {
        throw "Report JSON schemaVersion $($report.schemaVersion) is not one this tool recognises ($($KnownSchemaVersions -join ', ')): $Path"
    }
    if ($report.decision -ne "PASS" -and $report.decision -ne "FAIL") {
        throw "Report JSON has an unrecognised decision value '$($report.decision)' (expected PASS or FAIL): $Path"
    }
    return $report
}

# ---------------------------------------------------------------------------
# Deterministic finding -> owner-action mapping. Every rule here reads an already-present field on
# the report; none of it re-evaluates or re-scores the underlying evidence.
# ---------------------------------------------------------------------------
function Get-RequiredOwnerActions {
    param([Parameter(Mandatory)]$Report)
    $actions = New-Object System.Collections.Generic.List[string]

    $ci = $Report.candidateIdentity
    if (-not $ci.headMatches) {
        $actions.Add("LK: candidate HEAD does not match ($($ci.actualHead) vs expected $($ci.expectedHead)) - re-pin to the correct commit or re-verify the worktree before anything else is trusted.")
    }
    if ($ci.trackedDirt -gt 0) {
        $actions.Add("LK: $($ci.trackedDirt) tracked file(s) are dirty in the candidate worktree - clean the tree before re-validating.")
    }
    if (-not $ci.tag.supplied) {
        $actions.Add("LK: no frozen/immutable tag was supplied for this candidate - publish one before this line can be treated as a release candidate.")
    }
    elseif (-not $ci.tag.exists) {
        $actions.Add("LK: frozen tag '$($ci.tag.name)' does not exist - publish it against the exact expected HEAD.")
    }
    elseif (-not $ci.tag.resolvesToExpected) {
        $actions.Add("LK: frozen tag '$($ci.tag.name)' resolves to $($ci.tag.resolvedTo), not the expected HEAD - fix or replace the tag.")
    }

    $bh = $Report.buildHash
    if (-not $bh.exists) {
        $actions.Add("LK/VE: Runtime.dll is missing at the expected build path - produce a real build before any capture or hash check can pass.")
    }
    elseif (-not $bh.matches) {
        $actions.Add("LK/VE: Runtime.dll hash does not match the expected candidate build - rebuild and re-hash; do not reuse a stale binary.")
    }

    $cs = $Report.compilerStatus
    if ($cs.buildLogSupplied -and $cs.errorCsCount -gt 0) {
        $actions.Add("LK: build log shows $($cs.errorCsCount) 'error CS' occurrence(s) - fix compile errors before any other evidence from this build can be trusted.")
    }

    $tt = $Report.testTotals
    if ($tt.resultsSupplied -and $tt.failed -gt 0) {
        $actions.Add("Owner/dev: $($tt.failed) of $($tt.total) tests failed - investigate and fix before acceptance.")
    }
    if ($tt.resultsSupplied -and $tt.total -eq 0) {
        $actions.Add("Owner/dev: test results report zero tests executed - this is not a passing run, check the test filter/config.")
    }

    if (-not $Report.gateOnly) {
        $cc = $Report.captureCompleteness
        if ($cc.missingScreens.Count -gt 0) {
            $actions.Add("VE: capture the following missing required screen(s): $($cc.missingScreens -join ', ').")
        }
        $wrongRes = @($Report.captures | Where-Object { -not $_.matches1920x1080 })
        if ($wrongRes.Count -gt 0) {
            $names = ($wrongRes | ForEach-Object { "$($_.name) ($($_.width)x$($_.height))" }) -join ', '
            $actions.Add("VE: recapture at native 1920x1080 - found non-matching resolution on: $names.")
        }
        $invalidSidecars = @($Report.captures | Where-Object { -not $_.sidecarValid })
        if ($invalidSidecars.Count -gt 0) {
            $names = ($invalidSidecars | ForEach-Object { $_.name }) -join ', '
            $actions.Add("VE: fix sidecar provenance for: $names (see per-capture findings in the full report).")
        }
    }

    if ($Report.duplicates.Count -gt 0) {
        $actions.Add("VE: $($Report.duplicates.Count) duplicate/stale pixel finding(s) - recapture the affected screen(s) fresh rather than reusing an old frame.")
    }

    if ($Report.forbiddenRuntimeText.Count -gt 0) {
        $actions.Add("Dev/ST: forbidden runtime text found ($($Report.forbiddenRuntimeText.Count) occurrence(s)) - resolve real errors/placeholders before this candidate can pass.")
    }

    if ($actions.Count -eq 0) {
        $actions.Add("None - candidate is clean per this report; ready for owner/CC sign-off.")
    }

    return ,$actions
}

# ---------------------------------------------------------------------------
# Builds the concise dashboard object from a validated report. Pure function - no I/O.
# ---------------------------------------------------------------------------
function ConvertTo-DashboardSummary {
    param([Parameter(Mandatory)]$Report)

    $ci = $Report.candidateIdentity
    $tt = $Report.testTotals
    $cc = $Report.captureCompleteness

    $resolutionIssues = @()
    if (-not $Report.gateOnly) {
        $resolutionIssues = @($Report.captures | Where-Object { -not $_.matches1920x1080 } | ForEach-Object {
            [pscustomobject]@{ name = $_.name; width = $_.width; height = $_.height }
        })
    }

    return [pscustomobject]@{
        schemaVersion   = 1
        sourceReport    = $ReportJson
        generatedUtc    = (Get-Date).ToUniversalTime().ToString("o")
        reportGeneratedUtc = $Report.generatedUtc
        decision        = $Report.decision
        gateOnly        = $Report.gateOnly
        candidateIdentity = [pscustomobject]@{
            candidateRoot = $ci.candidateRoot
            expectedHead  = $ci.expectedHead
            actualHead    = $ci.actualHead
            headMatches   = $ci.headMatches
            trackedDirt   = $ci.trackedDirt
            tag           = $ci.tag.name
            tagResolvesToExpected = $ci.tag.resolvesToExpected
        }
        testTotals      = [pscustomobject]@{
            total   = $tt.total
            passed  = $tt.passed
            failed  = $tt.failed
            skipped = if ($tt.PSObject.Properties.Name -contains 'skipped') { $tt.skipped } else { $null }
        }
        captureCompleteness = [pscustomobject]@{
            requiredScreens = $cc.requiredScreens
            missingScreens  = $cc.missingScreens
            captureCount    = $cc.captureCount
        }
        resolutionIssues     = $resolutionIssues
        forbiddenTextFindings = @($Report.forbiddenRuntimeText)
        requiredOwnerActions = Get-RequiredOwnerActions -Report $Report
    }
}

# ---------------------------------------------------------------------------
# Renders the dashboard as one concise Markdown page.
# ---------------------------------------------------------------------------
function Format-DashboardSummaryText {
    param([Parameter(Mandatory)]$Dashboard)

    $lines = New-Object System.Collections.Generic.List[string]
    $badge = $Dashboard.decision
    $lines.Add("# Release Candidate Dashboard - $badge")
    $lines.Add("")
    $lines.Add("Source report: ``$($Dashboard.sourceReport)``")
    $lines.Add("Report generated (UTC): $($Dashboard.reportGeneratedUtc)")
    if ($Dashboard.gateOnly) { $lines.Add("**Mode: GATE (pre-capture)**") }
    $lines.Add("")

    $ci = $Dashboard.candidateIdentity
    $lines.Add("## Candidate identity")
    $lines.Add("- Root: ``$($ci.candidateRoot)``")
    $lines.Add("- HEAD: ``$($ci.actualHead)`` (expected ``$($ci.expectedHead)``) - matches: $($ci.headMatches)")
    $lines.Add("- Tracked dirt: $($ci.trackedDirt)")
    if ($ci.tag) { $lines.Add("- Frozen tag: $($ci.tag) (resolves to expected: $($ci.tagResolvesToExpected))") }
    else { $lines.Add("- Frozen tag: none supplied") }
    $lines.Add("")

    $tt = $Dashboard.testTotals
    $lines.Add("## Test totals")
    $lines.Add("- Total: $($tt.total) | Passed: $($tt.passed) | Failed: $($tt.failed) | Skipped: $($tt.skipped)")
    $lines.Add("")

    $cc = $Dashboard.captureCompleteness
    $lines.Add("## Capture completeness")
    if ($Dashboard.gateOnly) {
        $lines.Add("- Skipped (gate mode - no capture evidence yet).")
    }
    else {
        $lines.Add("- Required: $($cc.requiredScreens -join ', ')")
        $lines.Add("- Missing: $(if ($cc.missingScreens.Count -eq 0) { 'none' } else { $cc.missingScreens -join ', ' })")
        $lines.Add("- Captures found: $($cc.captureCount)")
    }
    $lines.Add("")

    $lines.Add("## Resolution")
    if ($Dashboard.resolutionIssues.Count -eq 0) { $lines.Add("- All captures at native 1920x1080.") }
    else { foreach ($r in $Dashboard.resolutionIssues) { $lines.Add("- $($r.name): $($r.width)x$($r.height) (expected 1920x1080)") } }
    $lines.Add("")

    $lines.Add("## Forbidden-text findings")
    if ($Dashboard.forbiddenTextFindings.Count -eq 0) { $lines.Add("- None found.") }
    else { foreach ($f in $Dashboard.forbiddenTextFindings) { $lines.Add("- $f") } }
    $lines.Add("")

    $lines.Add("## Required owner actions")
    foreach ($a in $Dashboard.requiredOwnerActions) { $lines.Add("- $a") }
    $lines.Add("")
    $lines.Add("## Decision: $badge")

    return ($lines -join "`n")
}

# ---------------------------------------------------------------------------
# -SelfTest: exercise the JSON reader, action-mapping, and rendering against synthetic fixture
# report JSON files - no real acceptance report, Unity, or network required.
# ---------------------------------------------------------------------------
if ($SelfTest) {
    $fails = 0
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("dashboard_selftest_" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null

    function Check {
        param([string]$Name, [scriptblock]$Body)
        try { & $Body; Write-Host "PASS  $Name" }
        catch { Write-Host "FAIL  $Name :: $($_.Exception.Message)"; $script:fails++ }
    }
    function Expect-Throw {
        param([string]$Name, [scriptblock]$Body)
        $threw = $false
        try { & $Body } catch { $threw = $true }
        if ($threw) { Write-Host "PASS  $Name" } else { Write-Host "FAIL  $Name :: expected an error, none thrown"; $script:fails++ }
    }

    function New-FixtureReportJson {
        param(
            [string]$Path,
            [bool]$HeadMatches = $true,
            [int]$TrackedDirt = 0,
            [bool]$TagSupplied = $true,
            [bool]$TagExists = $true,
            [bool]$TagResolves = $true,
            [bool]$DllExists = $true,
            [bool]$DllMatches = $true,
            [int]$ErrorCsCount = 0,
            [int]$TestTotal = 10,
            [int]$TestPassed = 10,
            [int]$TestFailed = 0,
            [int]$TestSkipped = 0,
            [string[]]$RequiredScreens = @("Home"),
            [string[]]$MissingScreens = @(),
            [object[]]$Captures = @(),
            [string[]]$Duplicates = @(),
            [string[]]$ForbiddenText = @(),
            [bool]$GateOnly = $false,
            [int]$SchemaVersion = 2
        )
        $obj = [pscustomobject]@{
            schemaVersion = $SchemaVersion
            generatedUtc  = "2026-01-01T00:00:00Z"
            decision      = if ($HeadMatches -and $TrackedDirt -eq 0 -and $TagSupplied -and $TagExists -and $TagResolves -and $DllExists -and $DllMatches -and $ErrorCsCount -eq 0 -and $TestFailed -eq 0 -and $MissingScreens.Count -eq 0 -and $Duplicates.Count -eq 0 -and $ForbiddenText.Count -eq 0) { "PASS" } else { "FAIL" }
            gateOnly      = $GateOnly
            candidateIdentity = [pscustomobject]@{
                candidateRoot = "C:\fixture"
                expectedHead  = "1111111111111111111111111111111111111111"
                actualHead    = if ($HeadMatches) { "1111111111111111111111111111111111111111" } else { "2222222222222222222222222222222222222222" }
                headMatches   = $HeadMatches
                trackedDirt   = $TrackedDirt
                tag           = [pscustomobject]@{
                    name = "fixture-tag"; supplied = $TagSupplied; exists = $TagExists
                    resolvesToExpected = $TagResolves; resolvedTo = "1111111111111111111111111111111111111111"
                }
            }
            buildHash = [pscustomobject]@{
                relativePath = "fake.dll"; expected = "aaaa"; actual = if ($DllExists) { if ($DllMatches) { "aaaa" } else { "bbbb" } } else { $null }
                exists = $DllExists; matches = $DllMatches
            }
            compilerStatus = [pscustomobject]@{ buildLogSupplied = $true; buildLogPath = "build.log"; errorCsCount = $ErrorCsCount; clean = ($ErrorCsCount -eq 0) }
            testTotals = [pscustomobject]@{ resultsSupplied = $true; resultsPath = "results.xml"; total = $TestTotal; passed = $TestPassed; failed = $TestFailed; skipped = $TestSkipped }
            captureCompleteness = [pscustomobject]@{ requiredScreens = $RequiredScreens; missingScreens = $MissingScreens; captureCount = $Captures.Count }
            captures = $Captures
            duplicates = $Duplicates
            forbiddenRuntimeText = $ForbiddenText
            allFindings = @()
            passNotes = @()
        }
        $obj | ConvertTo-Json -Depth 8 | Set-Content -Path $Path -Encoding utf8
        return $Path
    }

    Write-Host "=== generate_candidate_dashboard_summary.ps1 -SelfTest ==="

    # 1. Reader: rejects missing file, malformed JSON, missing required field, unknown schema
    #    version, and a bogus decision value - accepts a genuinely valid fixture.
    Expect-Throw "Reader: rejects a missing file" {
        Read-AcceptanceReportJson -Path (Join-Path $tmp "does_not_exist.json") | Out-Null
    }
    Expect-Throw "Reader: rejects malformed JSON" {
        $bad = Join-Path $tmp "malformed.json"
        Set-Content -Path $bad -Value "{ not: valid json" -Encoding utf8
        Read-AcceptanceReportJson -Path $bad | Out-Null
    }
    Expect-Throw "Reader: rejects JSON missing a required field" {
        $bad = Join-Path $tmp "missing_field.json"
        @{ schemaVersion = 2; decision = "PASS" } | ConvertTo-Json | Set-Content -Path $bad -Encoding utf8
        Read-AcceptanceReportJson -Path $bad | Out-Null
    }
    Expect-Throw "Reader: rejects an unrecognised schema version" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "bad_schema.json") -SchemaVersion 999
        Read-AcceptanceReportJson -Path $bad | Out-Null
    }
    Expect-Throw "Reader: rejects a bogus decision value" {
        $bad = Join-Path $tmp "bad_decision.json"
        $obj = Get-Content -LiteralPath (New-FixtureReportJson -Path (Join-Path $tmp "base_for_bad_decision.json")) -Raw | ConvertFrom-Json
        $obj.decision = "MAYBE"
        $obj | ConvertTo-Json -Depth 8 | Set-Content -Path $bad -Encoding utf8
        Read-AcceptanceReportJson -Path $bad | Out-Null
    }
    Check "Reader: accepts a genuinely valid fixture report" {
        $good = New-FixtureReportJson -Path (Join-Path $tmp "good.json")
        $r = Read-AcceptanceReportJson -Path $good
        if ($r.decision -ne "PASS") { throw "expected PASS fixture to read as PASS" }
    }

    # 2. Dashboard construction + owner-action mapping: PASS fixture -> clean dashboard, one
    #    "ready for sign-off" action, no false findings.
    Check "Dashboard: PASS fixture produces a clean summary with the sign-off action only" {
        $good = New-FixtureReportJson -Path (Join-Path $tmp "clean.json")
        $report = Read-AcceptanceReportJson -Path $good
        $dash = ConvertTo-DashboardSummary -Report $report
        if ($dash.decision -ne "PASS") { throw "expected PASS" }
        if ($dash.requiredOwnerActions.Count -ne 1 -or $dash.requiredOwnerActions[0] -notlike "None*") {
            throw "expected exactly one 'None - ready' action, got: $($dash.requiredOwnerActions -join ' | ')"
        }
        if ($dash.resolutionIssues.Count -ne 0) { throw "expected no resolution issues" }
        $text = Format-DashboardSummaryText -Dashboard $dash
        if ($text -notmatch "PASS") { throw "rendered text does not mention PASS" }
    }

    # 3. Each individual failure signal maps to its own specific, correctly-worded action - tested
    #    one signal at a time so a broken mapping for one signal can't hide behind another.
    Check "Owner actions: wrong HEAD produces the HEAD-mismatch action" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "head.json") -HeadMatches $false
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*HEAD does not match*" }).Count -eq 0) { throw "HEAD action missing" }
    }
    Check "Owner actions: tracked dirt produces the dirty-tree action" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "dirt.json") -TrackedDirt 3
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*tracked file(s) are dirty*" }).Count -eq 0) { throw "dirt action missing" }
    }
    Check "Owner actions: missing frozen tag produces the tag action" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "notag.json") -TagSupplied $false
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*no frozen*tag*was supplied*" }).Count -eq 0) { throw "no-tag action missing" }
    }
    Check "Owner actions: tag exists but resolves wrong produces the tag-mismatch action" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "tagmismatch.json") -TagResolves $false
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*resolves to*not the expected HEAD*" }).Count -eq 0) { throw "tag-mismatch action missing" }
    }
    Check "Owner actions: missing Runtime.dll produces the missing-build action" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "nodll.json") -DllExists $false
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*Runtime.dll is missing*" }).Count -eq 0) { throw "missing-dll action missing" }
    }
    Check "Owner actions: Runtime.dll hash mismatch produces the rebuild action" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "dllmismatch.json") -DllMatches $false
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*hash does not match*" }).Count -eq 0) { throw "dll-hash-mismatch action missing" }
    }
    Check "Owner actions: compiler errors produce the fix-compile-errors action" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "cserr.json") -ErrorCsCount 4
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*4 'error CS' occurrence*" }).Count -eq 0) { throw "compiler-error action missing" }
    }
    Check "Owner actions: failing tests produce the investigate-failures action with real counts" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "testfail.json") -TestTotal 20 -TestPassed 18 -TestFailed 2
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*2 of 20 tests failed*" }).Count -eq 0) { throw "test-failure action missing or counts wrong" }
    }
    Check "Owner actions: zero tests executed produces its own distinct action" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "zerotests.json") -TestTotal 0 -TestPassed 0 -TestFailed 0
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*zero tests executed*" }).Count -eq 0) { throw "zero-tests action missing" }
    }
    Check "Owner actions: missing required screens name the exact screens" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "missingscreens.json") -RequiredScreens @("Home", "Empire") -MissingScreens @("Empire")
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*missing required screen(s): Empire*" }).Count -eq 0) { throw "missing-screen action missing or doesn't name Empire" }
    }
    Check "Owner actions: wrong-resolution capture names the file and actual dimensions" {
        $badCaptures = @([pscustomobject]@{ name = "rc_Home_1707x1067.png"; width = 1707; height = 1067; matches1920x1080 = $false; sidecarValid = $true })
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "wrongres.json") -Captures $badCaptures
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*rc_Home_1707x1067.png (1707x1067)*" }).Count -eq 0) { throw "wrong-resolution action missing or missing exact dims" }
        if ($dash.resolutionIssues.Count -ne 1 -or $dash.resolutionIssues[0].width -ne 1707) { throw "resolutionIssues not populated correctly" }
    }
    Check "Owner actions: invalid sidecar names the file" {
        $badCaptures = @([pscustomobject]@{ name = "rc_Bad_1920x1080.png"; width = 1920; height = 1080; matches1920x1080 = $true; sidecarValid = $false })
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "badsidecar.json") -Captures $badCaptures
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*sidecar provenance for: rc_Bad_1920x1080.png*" }).Count -eq 0) { throw "bad-sidecar action missing" }
    }
    Check "Owner actions: duplicates produce the recapture-fresh action" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "dupes.json") -Duplicates @("DUPLICATE PIXELS: A is byte-identical to B")
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*1 duplicate/stale pixel finding(s)*" }).Count -eq 0) { throw "duplicate action missing" }
    }
    Check "Owner actions: forbidden text produces its own action with the real count" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "forbidden.json") -ForbiddenText @("FORBIDDEN MARKER (placeholder text) found in rc_Home", "FORBIDDEN MARKER (404) found in build log")
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*forbidden runtime text found (2 occurrence(s))*" }).Count -eq 0) { throw "forbidden-text action missing or wrong count" }
    }

    # 4. Gate mode: capture-completeness/resolution actions must not fire when there's nothing
    #    captured yet - only pre-capture signals (HEAD/dirt/tag/dll/compiler/tests) are relevant.
    Check "Gate mode: does not produce capture-completeness or resolution actions" {
        $gate = New-FixtureReportJson -Path (Join-Path $tmp "gate.json") -GateOnly $true
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $gate)
        if (($dash.requiredOwnerActions | Where-Object { $_ -like "*missing required screen*" -or $_ -like "*recapture at native*" }).Count -gt 0) {
            throw "gate mode should not produce capture-related actions"
        }
        $text = Format-DashboardSummaryText -Dashboard $dash
        if ($text -notmatch "GATE") { throw "rendered text does not flag gate mode" }
    }

    # 4b. Multiple simultaneous findings: each owner action must render as its OWN bullet line,
    #     not get collapsed into one line. This is a regression test for a real bug found while
    #     running this tool against the real rc31 acceptance report (2026-09-13): a redundant
    #     @() wrapper around Get-RequiredOwnerActions' already-safe ",$list" return re-nested the
    #     list as a single array element, so every action printed on one joined bullet line.
    Check "Owner actions: multiple simultaneous findings render as separate bullet lines" {
        $bad = New-FixtureReportJson -Path (Join-Path $tmp "multi.json") -TagSupplied $false -TestTotal 20 -TestPassed 18 -TestFailed 2 -ForbiddenText @("FORBIDDEN MARKER (placeholder text) found in rc_Home")
        $dash = ConvertTo-DashboardSummary -Report (Read-AcceptanceReportJson -Path $bad)
        if ($dash.requiredOwnerActions.Count -ne 3) {
            throw "expected exactly 3 owner actions, got $($dash.requiredOwnerActions.Count): $($dash.requiredOwnerActions -join ' <> ')"
        }
        $text = Format-DashboardSummaryText -Dashboard $dash
        $bulletLines = @(($text -split "`n") | Where-Object { $_ -like "- LK: no frozen*" -or $_ -like "- Owner/dev: 2 of 20*" -or $_ -like "- Dev/ST: forbidden*" })
        if ($bulletLines.Count -ne 3) {
            throw "expected 3 distinct owner-action bullet lines in the rendered text, got $($bulletLines.Count):`n$text"
        }
    }

    # 5. Full CLI path: real end-to-end chain from tools/generate_candidate_acceptance_report.ps1's
    #    own output into this tool, writing a real summary file to disk. Proves the two tools
    #    actually interoperate, not just that this tool's internals work in isolation.
    Check "Full chain: acceptance report JSON -> dashboard summary file, real interop" {
        Add-Type -AssemblyName System.Drawing -ErrorAction SilentlyContinue
        function New-TestPng {
            param([string]$Path, [int]$Width, [int]$Height)
            $bmp = New-Object System.Drawing.Bitmap($Width, $Height)
            try { $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png) } finally { $bmp.Dispose() }
        }
        . (Join-Path $PSScriptRoot "CandidateAcceptanceLib.ps1")

        $candRoot = Join-Path $tmp "chain_cand"
        New-Item -ItemType Directory -Force -Path $candRoot | Out-Null
        & git -C $candRoot init -q 2>$null
        & git -C $candRoot config user.email "selftest@example.com" 2>$null
        & git -C $candRoot config user.name "selftest" 2>$null
        Set-Content -Path (Join-Path $candRoot "a.txt") -Value "a" -Encoding utf8
        & git -C $candRoot add a.txt 2>$null
        & git -C $candRoot commit -q -m "init" 2>$null
        $candHead = (& git -C $candRoot rev-parse HEAD).Trim()
        & git -C $candRoot tag -a "chain-fixture" -m "freeze" 2>$null
        $dllDir = Join-Path $candRoot "Builds\Windows64\MyriadOfDragons_Data\Managed"
        New-Item -ItemType Directory -Force -Path $dllDir | Out-Null
        $dllPath = Join-Path $dllDir "MyriadOfDragons.Runtime.dll"
        Set-Content -Path $dllPath -Value "fake-dll" -Encoding utf8
        $dllHash = Get-Sha256Hex -Path $dllPath

        $capDir = Join-Path $tmp "chain_captures"
        New-Item -ItemType Directory -Force -Path $capDir | Out-Null
        $png = Join-Path $capDir "rc_Home_1920x1080.png"
        New-TestPng -Path $png -Width 1920 -Height 1080
        @{ label = "rc_Home"; sourceHead = $candHead; runtimeHash = $dllHash; pngSha256 = (Get-Sha256Hex -Path $png) } |
            ConvertTo-Json | Set-Content -Path (Join-Path $capDir "rc_Home_1920x1080.json") -Encoding utf8

        $acceptanceJson = Join-Path $tmp "chain_acceptance.json"
        $acceptanceMd = Join-Path $tmp "chain_acceptance.md"
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "SilentlyContinue"
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "generate_candidate_acceptance_report.ps1") `
            -CandidateRoot $candRoot -ExpectedHead $candHead -ExpectedRuntimeHash $dllHash `
            -ExpectedTag "chain-fixture" -CaptureDir $capDir -RequiredScreens "Home" `
            -OutJson $acceptanceJson -OutMarkdown $acceptanceMd 1>$null 2>$null
        $acceptCode = $LASTEXITCODE
        if ($acceptCode -ne 0) { $ErrorActionPreference = $prevEap; throw "acceptance report generator did not exit 0 (got $acceptCode) - cannot test the chain" }

        $dashJson = Join-Path $tmp "chain_dashboard.md"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -ReportJson $acceptanceJson -OutSummary $dashJson 1>$null 2>$null
        $dashCode = $LASTEXITCODE
        $ErrorActionPreference = $prevEap

        if ($dashCode -ne 0) { throw "expected dashboard exit 0 for a PASS candidate, got $dashCode" }
        if (-not (Test-Path -LiteralPath $dashJson)) { throw "dashboard summary file was not written" }
        $text = Get-Content -LiteralPath $dashJson -Raw
        if ($text -notmatch "PASS") { throw "dashboard does not report PASS for a genuinely passing chained candidate" }
        if ($text -notmatch [regex]::Escape($candHead)) { throw "dashboard does not include the real candidate HEAD" }
    }

    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host ""
    if ($fails -eq 0) { Write-Host "SELFTEST: all guard checks passed."; exit 0 }
    else { Write-Host "SELFTEST: $fails guard check(s) FAILED."; exit 1 }
}

# ---------------------------------------------------------------------------
# Summary mode: read a real report JSON, build and write the dashboard.
# ---------------------------------------------------------------------------

try {
    $report = Read-AcceptanceReportJson -Path $ReportJson
}
catch {
    Write-Error $_.Exception.Message -ErrorAction Continue
    exit 2
}

$dashboard = ConvertTo-DashboardSummary -Report $report
$text = Format-DashboardSummaryText -Dashboard $dashboard

if ($OutSummary -eq "") {
    $base = [System.IO.Path]::GetFileNameWithoutExtension($ReportJson)
    $OutSummary = "dashboard_$base.md"
}
Set-Content -Path $OutSummary -Value $text -Encoding utf8

Write-Output "release-candidate dashboard summary"
Write-Output "Decision: $($dashboard.decision)"
Write-Output "Summary:  $OutSummary"
Write-Output ""
Write-Output $text

if ($dashboard.decision -eq "FAIL") { exit 1 }
exit 0
