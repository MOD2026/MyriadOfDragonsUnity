# Candidate acceptance report generator - built on tools/CandidateAcceptanceLib.ps1's
# Invoke-CandidateAcceptanceCheck (the same checks tools/validate_release_candidate.ps1 uses).
#
# Produces ONE concise acceptance report in two forms from a single check run:
#   - JSON  (machine-readable) - candidate identity, build hash, test totals, compiler status,
#            capture completeness, resolution, sidecar validity, duplicates, forbidden runtime
#            text, and the PASS/FAIL decision.
#   - Markdown (human-readable) - the same information, rendered for a person to read in seconds.
#
# This does NOT re-implement any check - it calls Invoke-CandidateAcceptanceCheck once and
# reshapes the one result into both output formats, so the report can never disagree with what
# validate_release_candidate.ps1 itself would say about the same candidate.
#
# Usage (generate a report for a real candidate):
#   powershell -File tools/generate_candidate_acceptance_report.ps1 `
#     -CandidateRoot "C:\Users\zihan\Downloads\MoD-lk-line-019" `
#     -ExpectedHead "d4469a9ce70f5e920753f52e528ffccbadde8fef" `
#     -ExpectedRuntimeHash "aeca62d094b2cc4263396f3fe2ae2605fbdfc8752e956ddb647176235d5b483a" `
#     -ExpectedTag "lk/FROZEN-capture-candidate-rc19" `
#     -CaptureDir "ve_rc19_native_capture" `
#     -RequiredScreens "PackOpen","LoadingSigil","Bazaar","BattlePass","BattleUI","Friends" `
#     -BuildLog "MoD-lk-line-019\lk_rc19_build.log" `
#     -TestResultsXml "MoD-lk-line-019\lk_rc19_results.xml" `
#     -OutJson "rc19_acceptance.json" -OutMarkdown "rc19_acceptance.md"
#
# Usage (gate mode - before capture exists, omit -CaptureDir; -BuildLog/-TestResultsXml required):
#   powershell -File tools/generate_candidate_acceptance_report.ps1 -CandidateRoot ... -ExpectedHead ... `
#     -ExpectedRuntimeHash ... -ExpectedTag ... -BuildLog ... -TestResultsXml ... -OutJson ... -OutMarkdown ...
#
# Usage (run the tool's own automated tests, no candidate/build/Unity needed):
#   powershell -File tools/generate_candidate_acceptance_report.ps1 -SelfTest
#
# Exit codes: 0 = report generated, candidate PASS. 1 = report generated, candidate FAIL (the
# report itself was still written - a FAIL is a normal, reportable outcome, not a tool error).
# 2 = the tool itself could not run (bad arguments, missing required path). -SelfTest exits 0 only
# if every internal test passes.

[CmdletBinding(DefaultParameterSetName = "Report")]
param(
    [Parameter(ParameterSetName = "Report", Mandatory = $true)]
    [string]$CandidateRoot,

    [Parameter(ParameterSetName = "Report", Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$ExpectedHead,

    [Parameter(ParameterSetName = "Report", Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$ExpectedRuntimeHash,

    [Parameter(ParameterSetName = "Report")]
    [string]$RuntimeDllRelativePath = "Builds\Windows64\MyriadOfDragons_Data\Managed\MyriadOfDragons.Runtime.dll",

    [Parameter(ParameterSetName = "Report")]
    [string]$ExpectedTag = "",

    [Parameter(ParameterSetName = "Report")]
    [string]$CaptureDir = "",

    [Parameter(ParameterSetName = "Report")]
    [string[]]$RequiredScreens = @(),

    [Parameter(ParameterSetName = "Report")]
    [string]$BuildLog = "",

    [Parameter(ParameterSetName = "Report")]
    [string]$TestResultsXml = "",

    [Parameter(ParameterSetName = "Report")]
    [string]$KnownStaleHashesFile = "",

    [Parameter(ParameterSetName = "Report")]
    [string]$OutJson = "",

    [Parameter(ParameterSetName = "Report")]
    [string]$OutMarkdown = "",

    [Parameter(ParameterSetName = "SelfTest", Mandatory = $true)]
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "CandidateAcceptanceLib.ps1")

# ---------------------------------------------------------------------------
# Reshapes one Invoke-CandidateAcceptanceCheck result into the concise report schema. Pure
# function - no I/O - so it's directly unit-testable with a synthetic result object.
# ---------------------------------------------------------------------------
function ConvertTo-AcceptanceReportObject {
    param([Parameter(Mandatory)]$CheckResult)

    $capturesReport = @($CheckResult.Captures | ForEach-Object {
        [pscustomobject]@{
            name             = $_.Name
            label            = $_.Label
            width            = $_.Width
            height           = $_.Height
            matches1920x1080 = $_.Matches1920x1080
            pngSha256        = $_.ActualPngHash
            sidecarValid     = $_.Passed
            findings         = @($_.Findings)
        }
    })

    # Sidecar-note forbidden-text findings land only in AllFindings (Invoke-CandidateAcceptanceCheck
    # adds them there directly, not into any one capture's own .Findings list), so scan AllFindings
    # rather than the per-capture/build-log lists alone - that missed real forbidden-text findings
    # until this file's own self-test caught it.
    $forbiddenText = New-Object System.Collections.Generic.List[string]
    foreach ($f in $CheckResult.AllFindings) {
        if ($f -like "FORBIDDEN MARKER*") { $forbiddenText.Add($f) }
    }

    return [pscustomobject]@{
        schemaVersion = 1
        generatedUtc  = $CheckResult.GeneratedUtc
        decision      = $CheckResult.Decision
        gateOnly      = $CheckResult.GateOnly
        candidateIdentity = [pscustomobject]@{
            candidateRoot = $CheckResult.CandidateRoot
            expectedHead  = $CheckResult.Identity.ExpectedHead
            actualHead    = $CheckResult.Identity.ActualHead
            headMatches   = $CheckResult.Identity.HeadMatches
            trackedDirt   = $CheckResult.Identity.DirtCount
            tag           = [pscustomobject]@{
                name               = $CheckResult.Identity.Tag.Tag
                supplied           = $CheckResult.Identity.Tag.Supplied
                exists             = $CheckResult.Identity.Tag.Exists
                resolvesToExpected = $CheckResult.Identity.Tag.ResolvesToExpected
                resolvedTo         = $CheckResult.Identity.Tag.Resolved
            }
        }
        buildHash = [pscustomobject]@{
            relativePath = $CheckResult.BuildHash.RelativePath
            expected     = $CheckResult.BuildHash.Expected
            actual       = $CheckResult.BuildHash.Actual
            exists       = $CheckResult.BuildHash.Exists
            matches      = $CheckResult.BuildHash.Matches
        }
        compilerStatus = [pscustomobject]@{
            buildLogSupplied = $CheckResult.BuildLog.Supplied
            buildLogPath     = $CheckResult.BuildLog.Path
            errorCsCount     = $CheckResult.BuildLog.ErrorCsCount
            clean            = ($CheckResult.BuildLog.Supplied -and $CheckResult.BuildLog.Findings.Count -eq 0)
        }
        testTotals = [pscustomobject]@{
            resultsSupplied = $CheckResult.TestResults.Supplied
            resultsPath     = $CheckResult.TestResults.Path
            total           = $CheckResult.TestResults.Total
            passed          = $CheckResult.TestResults.Passed
            failed          = $CheckResult.TestResults.Failed
        }
        captureCompleteness = [pscustomobject]@{
            requiredScreens = $CheckResult.RequiredScreens.Required
            missingScreens  = $CheckResult.RequiredScreens.Missing
            captureCount    = $CheckResult.Captures.Count
        }
        captures      = $capturesReport
        duplicates    = @($CheckResult.Duplicates)
        forbiddenRuntimeText = @($forbiddenText)
        allFindings   = $CheckResult.AllFindings
        passNotes     = $CheckResult.PassNotes
    }
}

# ---------------------------------------------------------------------------
# Renders the same report object as human-readable Markdown.
# ---------------------------------------------------------------------------
function Format-AcceptanceReportMarkdown {
    param([Parameter(Mandatory)]$Report)

    $lines = New-Object System.Collections.Generic.List[string]
    $badge = if ($Report.decision -eq "PASS") { "PASS" } else { "FAIL" }
    $lines.Add("# Candidate Acceptance Report - $badge")
    $lines.Add("")
    $lines.Add("Generated (UTC): $($Report.generatedUtc)")
    if ($Report.gateOnly) { $lines.Add("**Mode: GATE (pre-capture) - capture-set checks skipped, nothing captured yet.**") }
    $lines.Add("")

    $lines.Add("## Candidate identity")
    $ci = $Report.candidateIdentity
    $lines.Add("- Candidate root: ``$($ci.candidateRoot)``")
    $lines.Add("- Expected HEAD: ``$($ci.expectedHead)``")
    $lines.Add("- Actual HEAD:   ``$($ci.actualHead)``")
    $lines.Add("- HEAD matches: $($ci.headMatches)")
    $lines.Add("- Tracked dirt: $($ci.trackedDirt)")
    if ($ci.tag.supplied) {
        $tagLine = "- Frozen tag ``$($ci.tag.name)``: exists=$($ci.tag.exists), resolves to expected=$($ci.tag.resolvesToExpected)"
        if ($ci.tag.exists) { $tagLine += " (resolved: $($ci.tag.resolvedTo))" }
        $lines.Add($tagLine)
    }
    else {
        $lines.Add("- **No frozen tag supplied.**")
    }
    $lines.Add("")

    $lines.Add("## Build hash")
    $bh = $Report.buildHash
    $lines.Add("- Relative path: ``$($bh.relativePath)``")
    $lines.Add("- Expected: ``$($bh.expected)``")
    $lines.Add("- Actual:   ``$($bh.actual)``")
    $lines.Add("- Exists: $($bh.exists) | Matches: $($bh.matches)")
    $lines.Add("")

    $lines.Add("## Compiler status")
    $cs = $Report.compilerStatus
    if ($cs.buildLogSupplied) {
        $lines.Add("- Build log: ``$($cs.buildLogPath)``")
        $lines.Add("- error CS count: $($cs.errorCsCount)")
        $lines.Add("- Clean: $($cs.clean)")
    }
    else { $lines.Add("- No build log supplied.") }
    $lines.Add("")

    $lines.Add("## Test totals")
    $tt = $Report.testTotals
    if ($tt.resultsSupplied) {
        $lines.Add("- Results XML: ``$($tt.resultsPath)``")
        $lines.Add("- Total: $($tt.total) | Passed: $($tt.passed) | Failed: $($tt.failed)")
    }
    else { $lines.Add("- No test results XML supplied.") }
    $lines.Add("")

    $lines.Add("## Capture completeness / resolution / sidecar validity")
    $cc = $Report.captureCompleteness
    if ($Report.gateOnly) {
        $lines.Add("- Skipped (gate mode).")
    }
    else {
        $lines.Add("- Required screens: $($cc.requiredScreens -join ', ')")
        $lines.Add("- Missing screens: $(if ($cc.missingScreens.Count -eq 0) { 'none' } else { $cc.missingScreens -join ', ' })")
        $lines.Add("- Captures found: $($cc.captureCount)")
        $lines.Add("")
        $lines.Add("| Capture | Width x Height | 1920x1080 | Sidecar valid |")
        $lines.Add("|---|---|---|---|")
        foreach ($cap in $Report.captures) {
            $lines.Add("| $($cap.name) | $($cap.width)x$($cap.height) | $($cap.matches1920x1080) | $($cap.sidecarValid) |")
        }
    }
    $lines.Add("")

    $lines.Add("## Duplicate pixels")
    if ($Report.duplicates.Count -eq 0) { $lines.Add("- None found.") }
    else { foreach ($d in $Report.duplicates) { $lines.Add("- $d") } }
    $lines.Add("")

    $lines.Add("## Forbidden runtime text")
    if ($Report.forbiddenRuntimeText.Count -eq 0) { $lines.Add("- None found.") }
    else { foreach ($f in $Report.forbiddenRuntimeText) { $lines.Add("- $f") } }
    $lines.Add("")

    $lines.Add("## Decision: $badge")
    if ($Report.decision -eq "FAIL") {
        $lines.Add("")
        $lines.Add("### Findings ($($Report.allFindings.Count))")
        foreach ($f in $Report.allFindings) { $lines.Add("- $f") }
    }

    return ($lines -join "`n")
}

# ---------------------------------------------------------------------------
# -SelfTest: exercise the report-shaping and Markdown-rendering functions against real fixtures
# (a throwaway git repo, real PNGs, real sidecars) via Invoke-CandidateAcceptanceCheck - the exact
# same entry point the real CLI path below uses. No Unity, no network.
# ---------------------------------------------------------------------------
if ($SelfTest) {
    $fails = 0
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("gen_report_selftest_" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null

    function Check {
        param([string]$Name, [scriptblock]$Body)
        try { & $Body; Write-Host "PASS  $Name" }
        catch { Write-Host "FAIL  $Name :: $($_.Exception.Message)"; $script:fails++ }
    }

    function New-TestPng {
        param([string]$Path, [int]$Width, [int]$Height)
        Add-Type -AssemblyName System.Drawing -ErrorAction SilentlyContinue
        $bmp = New-Object System.Drawing.Bitmap($Width, $Height)
        try { $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png) }
        finally { $bmp.Dispose() }
    }

    function New-FixtureCandidate {
        param([string]$Root, [string]$DllContent = "fake-dll-content")
        New-Item -ItemType Directory -Force -Path $Root | Out-Null
        & git -C $Root init -q 2>$null
        & git -C $Root config user.email "selftest@example.com" 2>$null
        & git -C $Root config user.name "selftest" 2>$null
        Set-Content -Path (Join-Path $Root "a.txt") -Value "a" -Encoding utf8
        & git -C $Root add a.txt 2>$null
        & git -C $Root commit -q -m "init" 2>$null
        $head = (& git -C $Root rev-parse HEAD).Trim()
        & git -C $Root tag -a "rc-fixture" -m "freeze" 2>$null
        $dllDir = Join-Path $Root "Builds\Windows64\MyriadOfDragons_Data\Managed"
        New-Item -ItemType Directory -Force -Path $dllDir | Out-Null
        $dllPath = Join-Path $dllDir "MyriadOfDragons.Runtime.dll"
        Set-Content -Path $dllPath -Value $DllContent -Encoding utf8
        $dllHash = Get-Sha256Hex -Path $dllPath
        return [pscustomobject]@{ Root = $Root; Head = $head; DllHash = $dllHash }
    }

    Write-Host "=== generate_candidate_acceptance_report.ps1 -SelfTest ==="

    # 1. End-to-end PASS: real fixture candidate + one real matching capture -> report says PASS,
    #    every required schema section is present, Markdown renders without error.
    Check "PASS candidate: report decision is PASS and every schema section is populated" {
        $cand = New-FixtureCandidate -Root (Join-Path $tmp "cand_pass")
        $capDir = Join-Path $tmp "captures_pass"
        New-Item -ItemType Directory -Force -Path $capDir | Out-Null
        $png = Join-Path $capDir "rc_Home_1920x1080.png"
        New-TestPng -Path $png -Width 1920 -Height 1080
        @{
            label = "rc_Home"; sourceHead = $cand.Head; runtimeHash = $cand.DllHash
            pngSha256 = (Get-Sha256Hex -Path $png)
        } | ConvertTo-Json | Set-Content -Path (Join-Path $capDir "rc_Home_1920x1080.json") -Encoding utf8
        $cleanLog = Join-Path $tmp "clean_pass.log"
        Set-Content -Path $cleanLog -Value "Build succeeded." -Encoding utf8
        $goodXml = Join-Path $tmp "good_pass.xml"
        Set-Content -Path $goodXml -Value '<?xml version="1.0"?><test-run testcasecount="3" passed="3" failed="0"></test-run>' -Encoding utf8

        $checkResult = Invoke-CandidateAcceptanceCheck -CandidateRoot $cand.Root -ExpectedHead $cand.Head `
            -ExpectedRuntimeHash $cand.DllHash -ExpectedTag "rc-fixture" -CaptureDir $capDir `
            -RequiredScreens @("Home") -BuildLog $cleanLog -TestResultsXml $goodXml
        $report = ConvertTo-AcceptanceReportObject -CheckResult $checkResult

        if ($report.decision -ne "PASS") { throw "expected PASS, got $($report.decision): $($report.allFindings -join '; ')" }
        if (-not $report.candidateIdentity.headMatches) { throw "headMatches should be true" }
        if (-not $report.buildHash.matches) { throw "buildHash.matches should be true" }
        if (-not $report.compilerStatus.clean) { throw "compilerStatus.clean should be true" }
        if ($report.testTotals.total -ne 3 -or $report.testTotals.failed -ne 0) { throw "test totals not carried through" }
        if ($report.captureCompleteness.missingScreens.Count -ne 0) { throw "expected no missing screens" }
        if ($report.captures.Count -ne 1 -or -not $report.captures[0].sidecarValid -or -not $report.captures[0].matches1920x1080) {
            throw "capture entry not populated correctly"
        }
        if ($report.duplicates.Count -ne 0) { throw "expected no duplicates" }
        if ($report.forbiddenRuntimeText.Count -ne 0) { throw "expected no forbidden text" }

        $md = Format-AcceptanceReportMarkdown -Report $report
        if ($md -notmatch "PASS") { throw "Markdown does not mention PASS" }
        if ($md -notmatch [regex]::Escape($cand.Head)) { throw "Markdown does not include the expected HEAD" }
        $json = $report | ConvertTo-Json -Depth 8
        $roundTrip = $json | ConvertFrom-Json
        if ($roundTrip.decision -ne "PASS") { throw "JSON did not round-trip decision correctly" }
    }

    # 2. End-to-end FAIL: wrong HEAD/hash/dimensions/missing screen/forbidden text -> report says
    #    FAIL, every specific finding surfaces in the right report section.
    Check "FAIL candidate: report decision is FAIL and each specific finding is in the right section" {
        $cand = New-FixtureCandidate -Root (Join-Path $tmp "cand_fail")
        $capDir = Join-Path $tmp "captures_fail"
        New-Item -ItemType Directory -Force -Path $capDir | Out-Null
        $png = Join-Path $capDir "rc_Home_1707x1067.png"
        New-TestPng -Path $png -Width 1707 -Height 1067
        @{
            label = "rc_Home"; sourceHead = "0000000000000000000000000000000000000000"
            runtimeHash = "1111111111111111111111111111111111111111111111111111111111111111"
            pngSha256 = (Get-Sha256Hex -Path $png)
            note = "This label still shows placeholder text pending art"
        } | ConvertTo-Json | Set-Content -Path (Join-Path $capDir "rc_Home_1707x1067.json") -Encoding utf8
        $dirtyLog = Join-Path $tmp "dirty_fail.log"
        Set-Content -Path $dirtyLog -Value "Assets\Foo.cs(1,1): error CS0103: bad" -Encoding utf8
        $badXml = Join-Path $tmp "bad_fail.xml"
        Set-Content -Path $badXml -Value '<?xml version="1.0"?><test-run testcasecount="3" passed="2" failed="1"></test-run>' -Encoding utf8

        $wrongHead = "ffffffffffffffffffffffffffffffffffffff"
        $wrongHash = "2222222222222222222222222222222222222222222222222222222222222222"
        $checkResult = Invoke-CandidateAcceptanceCheck -CandidateRoot $cand.Root -ExpectedHead $wrongHead `
            -ExpectedRuntimeHash $wrongHash -CaptureDir $capDir -RequiredScreens @("Empire") `
            -BuildLog $dirtyLog -TestResultsXml $badXml
        $report = ConvertTo-AcceptanceReportObject -CheckResult $checkResult

        if ($report.decision -ne "FAIL") { throw "expected FAIL, got PASS" }
        if ($report.candidateIdentity.headMatches) { throw "headMatches should be false" }
        if (-not $report.buildHash.exists) { throw "buildHash.exists should be true - the fixture DLL is really on disk, only its hash is wrong" }
        if ($report.buildHash.matches) { throw "buildHash.matches should be false" }
        if ($report.compilerStatus.clean) { throw "compilerStatus.clean should be false" }
        if ($report.compilerStatus.errorCsCount -ne 1) { throw "expected errorCsCount 1, got $($report.compilerStatus.errorCsCount)" }
        if ($report.testTotals.failed -ne 1) { throw "expected 1 failed test" }
        if ($report.captureCompleteness.missingScreens -notcontains "Empire") { throw "Empire not in missingScreens" }
        if ($report.captures[0].matches1920x1080) { throw "capture should not match 1920x1080" }
        if ($report.forbiddenRuntimeText.Count -eq 0) { throw "expected forbidden text (placeholder note) to surface" }

        $md = Format-AcceptanceReportMarkdown -Report $report
        if ($md -notmatch "FAIL") { throw "Markdown does not mention FAIL" }
        if ($md -notmatch "Findings") { throw "Markdown does not include a findings section on FAIL" }
    }

    # 3. Gate mode: report reflects gate-only state (no capture section, no missing-screen churn).
    Check "Gate mode: report marks gateOnly and skips capture completeness" {
        $cand = New-FixtureCandidate -Root (Join-Path $tmp "cand_gate")
        $cleanLog = Join-Path $tmp "clean_gate.log"
        Set-Content -Path $cleanLog -Value "Build succeeded." -Encoding utf8
        $goodXml = Join-Path $tmp "good_gate.xml"
        Set-Content -Path $goodXml -Value '<?xml version="1.0"?><test-run testcasecount="1" passed="1" failed="0"></test-run>' -Encoding utf8

        $checkResult = Invoke-CandidateAcceptanceCheck -CandidateRoot $cand.Root -ExpectedHead $cand.Head `
            -ExpectedRuntimeHash $cand.DllHash -ExpectedTag "rc-fixture" -BuildLog $cleanLog -TestResultsXml $goodXml
        $report = ConvertTo-AcceptanceReportObject -CheckResult $checkResult

        if (-not $report.gateOnly) { throw "expected gateOnly true" }
        if ($report.decision -ne "PASS") { throw "expected PASS in gate mode with clean evidence, got FAIL: $($report.allFindings -join '; ')" }
        if ($report.captures.Count -ne 0) { throw "expected 0 captures in gate mode" }
        $md = Format-AcceptanceReportMarkdown -Report $report
        if ($md -notmatch "GATE") { throw "Markdown does not flag gate mode" }
    }

    # 4. JSON output actually written to disk by the real CLI path (not just the in-memory object).
    Check "JSON/Markdown files are actually written to disk with the right content" {
        $cand = New-FixtureCandidate -Root (Join-Path $tmp "cand_files")
        $capDir = Join-Path $tmp "captures_files"
        New-Item -ItemType Directory -Force -Path $capDir | Out-Null
        $png = Join-Path $capDir "rc_Home_1920x1080.png"
        New-TestPng -Path $png -Width 1920 -Height 1080
        @{
            label = "rc_Home"; sourceHead = $cand.Head; runtimeHash = $cand.DllHash
            pngSha256 = (Get-Sha256Hex -Path $png)
        } | ConvertTo-Json | Set-Content -Path (Join-Path $capDir "rc_Home_1920x1080.json") -Encoding utf8

        $outJson = Join-Path $tmp "written_report.json"
        $outMd = Join-Path $tmp "written_report.md"
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "SilentlyContinue"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
            -CandidateRoot $cand.Root -ExpectedHead $cand.Head -ExpectedRuntimeHash $cand.DllHash `
            -ExpectedTag "rc-fixture" -CaptureDir $capDir -RequiredScreens "Home" `
            -OutJson $outJson -OutMarkdown $outMd 1>$null 2>$null
        $code = $LASTEXITCODE
        $ErrorActionPreference = $prevEap

        if ($code -ne 0) { throw "expected exit 0, got $code" }
        if (-not (Test-Path -LiteralPath $outJson)) { throw "JSON report was not written" }
        if (-not (Test-Path -LiteralPath $outMd)) { throw "Markdown report was not written" }
        $parsed = Get-Content -LiteralPath $outJson -Raw | ConvertFrom-Json
        if ($parsed.decision -ne "PASS") { throw "written JSON does not say PASS" }
        $mdText = Get-Content -LiteralPath $outMd -Raw
        if ($mdText -notmatch "Candidate Acceptance Report") { throw "written Markdown missing its own title" }
    }
    Check "CLI exits 1 (not an error) when the candidate is a real FAIL, and still writes the report" {
        $cand = New-FixtureCandidate -Root (Join-Path $tmp "cand_cli_fail")
        $capDir = Join-Path $tmp "captures_cli_fail"
        New-Item -ItemType Directory -Force -Path $capDir | Out-Null
        # No captures at all + a required screen -> guaranteed FAIL via MISSING REQUIRED SCREEN.
        $outJson = Join-Path $tmp "written_fail_report.json"
        $outMd = Join-Path $tmp "written_fail_report.md"
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "SilentlyContinue"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
            -CandidateRoot $cand.Root -ExpectedHead $cand.Head -ExpectedRuntimeHash $cand.DllHash `
            -ExpectedTag "rc-fixture" -CaptureDir $capDir -RequiredScreens "Home" `
            -OutJson $outJson -OutMarkdown $outMd 1>$null 2>$null
        $code = $LASTEXITCODE
        $ErrorActionPreference = $prevEap
        if ($code -ne 1) { throw "expected exit 1, got $code" }
        if (-not (Test-Path -LiteralPath $outJson)) { throw "JSON report was not written on a FAIL" }
        $parsed = Get-Content -LiteralPath $outJson -Raw | ConvertFrom-Json
        if ($parsed.decision -ne "FAIL") { throw "written JSON does not say FAIL" }
    }

    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host ""
    if ($fails -eq 0) { Write-Host "SELFTEST: all guard checks passed."; exit 0 }
    else { Write-Host "SELFTEST: $fails guard check(s) FAILED."; exit 1 }
}

# ---------------------------------------------------------------------------
# Report mode: run the check, build the report, write JSON + Markdown, print a short summary.
# ---------------------------------------------------------------------------

if (-not (Test-Path -LiteralPath $CandidateRoot)) {
    Write-Error "CandidateRoot does not exist: $CandidateRoot" -ErrorAction Continue
    exit 2
}

try {
    $checkResult = Invoke-CandidateAcceptanceCheck -CandidateRoot $CandidateRoot -ExpectedHead $ExpectedHead `
        -ExpectedRuntimeHash $ExpectedRuntimeHash -RuntimeDllRelativePath $RuntimeDllRelativePath `
        -ExpectedTag $ExpectedTag -CaptureDir $CaptureDir -RequiredScreens $RequiredScreens `
        -BuildLog $BuildLog -TestResultsXml $TestResultsXml -KnownStaleHashesFile $KnownStaleHashesFile
}
catch {
    Write-Error $_.Exception.Message -ErrorAction Continue
    exit 2
}

$report = ConvertTo-AcceptanceReportObject -CheckResult $checkResult
$markdown = Format-AcceptanceReportMarkdown -Report $report

if ($OutJson -eq "") {
    $tagSlug = if ($ExpectedTag -ne "") { ($ExpectedTag -replace '[\\/]', '_') } else { $ExpectedHead.Substring(0, 8) }
    $OutJson = "acceptance_report_$tagSlug.json"
}
if ($OutMarkdown -eq "") {
    $tagSlug = if ($ExpectedTag -ne "") { ($ExpectedTag -replace '[\\/]', '_') } else { $ExpectedHead.Substring(0, 8) }
    $OutMarkdown = "acceptance_report_$tagSlug.md"
}

$report | ConvertTo-Json -Depth 8 | Set-Content -Path $OutJson -Encoding utf8
Set-Content -Path $OutMarkdown -Value $markdown -Encoding utf8

Write-Output "candidate acceptance report generator"
Write-Output "Decision: $($report.decision)"
Write-Output "JSON:     $OutJson"
Write-Output "Markdown: $OutMarkdown"
Write-Output ""
if ($report.decision -eq "FAIL") {
    Write-Output "Findings ($($report.allFindings.Count)):"
    foreach ($f in $report.allFindings) { Write-Output "  FAIL $f" }
    exit 1
}
exit 0
