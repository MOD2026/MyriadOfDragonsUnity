# One release-capture command: gate -> DPI-aware native capture -> JSON+Markdown acceptance
# report, from a single invocation. Pure orchestration - it does not reimplement any check or any
# capture mechanics; it shells out to the three existing tools in order and stops the pipeline the
# instant an earlier stage fails:
#
#   1. tools/capture_native_window.ps1 - this ALREADY refuses (exit 2) before ever launching the
#      player if tools/validate_release_candidate.ps1's pre-capture gate fails (see that script's
#      own header). This script inherits that guarantee unchanged - it does not gate a second time,
#      it just does not proceed to stage 2/3 if stage 1's exit code is non-zero.
#   2. (implicit) the true 1920x1080 client-area capture itself, entirely inside stage 1 - not
#      touched here.
#   3. tools/generate_candidate_acceptance_report.ps1, pointed at the same -OutDir stage 1 wrote
#      into, to produce the JSON + Markdown acceptance report - only runs if stage 1 succeeded.
#
# If stage 1 refuses or fails, this script stops immediately: no report is generated (there is
# nothing valid to report on), and the exit code is stage 1's own (2 = gate refusal / tool error,
# 1 = capture attempted but failed, e.g. no window). If stage 1 succeeds but stage 3 finds the
# candidate does not actually accept (duplicate pixels, missing required screens, etc.), this
# script exits with stage 3's own code (1 = FAIL, still a normal reportable outcome - the report
# was written).
#
# Usage:
#   powershell -File tools/run_release_capture.ps1 `
#     -ExePath "C:\Users\zihan\Downloads\MoD-lk-line-019\Builds\Windows64\MyriadOfDragons.exe" `
#     -Label "BattlePass_1920x1080" `
#     -CandidateRoot "C:\Users\zihan\Downloads\MoD-lk-line-019" `
#     -SourceHead "d4469a9ce70f5e920753f52e528ffccbadde8fef" `
#     -RuntimeHash "aeca62d094b2cc4263396f3fe2ae2605fbdfc8752e956ddb647176235d5b483a" `
#     -ExpectedTag "lk/FROZEN-capture-candidate-rc19" `
#     -BuildLog "MoD-lk-line-019\lk_rc19_build.log" `
#     -TestResultsXml "MoD-lk-line-019\lk_rc19_results.xml" `
#     -CaptureOutDir "handover\native_capture_out" `
#     -ReportOutJson "rc19_acceptance.json" -ReportOutMarkdown "rc19_acceptance.md"
#
# -RequiredScreens defaults to just -Label if not supplied (the one screen this invocation
# captured). Pass it explicitly when -CaptureOutDir already holds other required screens from
# earlier invocations and the report should check for all of them together.
#
# Usage (run the tool's own automated tests, no candidate/build/Unity/player needed):
#   powershell -File tools/run_release_capture.ps1 -SelfTest
#
# Exit codes: 0 = gate passed, capture written, report says PASS. 1 = either the capture step
# itself failed post-gate (no window found, etc.) or the report step says FAIL (a real, reportable
# acceptance failure). 2 = the pre-capture gate refused, or a tool in the pipeline could not run.

[CmdletBinding(DefaultParameterSetName = "Run")]
param(
    [Parameter(ParameterSetName = "Run", Mandatory)][string]$ExePath,
    [Parameter(ParameterSetName = "Run", Mandatory)][string]$Label,

    [Parameter(ParameterSetName = "Run", Mandatory)][string]$CandidateRoot,
    [Parameter(ParameterSetName = "Run", Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceHead,
    [Parameter(ParameterSetName = "Run", Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$RuntimeHash,
    [Parameter(ParameterSetName = "Run", Mandatory)][string]$ExpectedTag,
    [Parameter(ParameterSetName = "Run", Mandatory)][string]$BuildLog,
    [Parameter(ParameterSetName = "Run", Mandatory)][string]$TestResultsXml,
    [Parameter(ParameterSetName = "Run")]
    [string]$RuntimeDllRelativePath = "Builds\Windows64\MyriadOfDragons_Data\Managed\MyriadOfDragons.Runtime.dll",

    [Parameter(ParameterSetName = "Run")][string]$CaptureOutDir = "handover/native_capture_out",
    [Parameter(ParameterSetName = "Run")][int]$WaitSeconds = 8,
    [Parameter(ParameterSetName = "Run")][switch]$LeaveRunning,
    [Parameter(ParameterSetName = "Run")][string]$AttachProcessId = "",

    [Parameter(ParameterSetName = "Run")][string[]]$RequiredScreens = @(),
    [Parameter(ParameterSetName = "Run")][string]$KnownStaleHashesFile = "",
    [Parameter(ParameterSetName = "Run")][string]$ReportOutJson = "",
    [Parameter(ParameterSetName = "Run")][string]$ReportOutMarkdown = "",

    # Path overrides - default to the three sibling tools. Only ever overridden by this script's
    # own -SelfTest, to substitute a fixture "capture" script and prove the stage-1-fails-stops-
    # the-pipeline / stage-1-succeeds-runs-stage-3 wiring without needing a real Unity window.
    [Parameter(ParameterSetName = "Run")][string]$CaptureScript = "",
    [Parameter(ParameterSetName = "Run")][string]$ReportScript = "",
    # PRODUCTIVE CODING TASK - script-version pinning. Resolved next to THIS script by default -
    # deliberately NOT derived from wherever -CaptureScript happens to point, so pointing
    # -CaptureScript at a stale/frozen checkout's copy of capture_native_window.ps1 cannot also
    # silently drag in that checkout's own stale sibling validator. Forwarded to the capture stage
    # as its own -ValidatorScript, and independently verified here too (defense in depth - this
    # orchestrator does not just trust that the capture stage will catch a stale validator).
    [Parameter(ParameterSetName = "Run")][string]$ValidatorScript = "",

    [Parameter(ParameterSetName = "SelfTest", Mandatory)][switch]$SelfTest
)

if ($CaptureScript -eq "") {
    $CaptureScript = Join-Path (Split-Path -Parent $PSCommandPath) "capture_native_window.ps1"
}
if ($ReportScript -eq "") {
    $ReportScript = Join-Path (Split-Path -Parent $PSCommandPath) "generate_candidate_acceptance_report.ps1"
}
if ($ValidatorScript -eq "") {
    $ValidatorScript = Join-Path (Split-Path -Parent $PSCommandPath) "validate_release_candidate.ps1"
}

# Same version-pin content check as tools/capture_native_window.ps1's own Test-ValidatorImplementation
# (duplicated deliberately, not dot-sourced - each standalone tool in this set verifies its own
# inputs rather than trusting a sibling not to have been swapped out from under it). Both markers
# together confirm the ACTUAL fix is present, not just a function with a similar name.
function Test-ValidatorImplementation {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return [pscustomobject]@{ Ok = $false; ResolvedPath = $Path; Sha256 = ""; Reason = "Validator script not found: $Path" }
    }
    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    $sha256 = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $content = Get-Content -LiteralPath $resolvedPath -Raw
    $hasHelperFunction = $content -match 'function\s+Invoke-GitOnCandidate'
    $hasSafeDirectoryArg = $content -match '\bsafe\.directory=\$RepoPath\b'
    if (-not $hasHelperFunction -or -not $hasSafeDirectoryArg) {
        return [pscustomobject]@{
            Ok = $false
            ResolvedPath = $resolvedPath
            Sha256 = $sha256
            Reason = "Validator at '$resolvedPath' (sha256 $sha256) does not contain the process-local git safe.directory fix (commit 77538a6d, Invoke-GitOnCandidate) - this looks like a stale validator copy (e.g. from a frozen checkout predating that fix). Refusing to run the pipeline against it."
        }
    }
    return [pscustomobject]@{ Ok = $true; ResolvedPath = $resolvedPath; Sha256 = $sha256; Reason = "" }
}

# Runs one pipeline stage as a real subprocess and returns its exit code + combined output, never
# throwing on a non-zero exit (every REFUSES/FAIL case here is an expected, normal outcome, not a
# script bug) - same PS 5.1 stderr-under-Stop-preference guard used by the other tools in this set.
function Invoke-PipelineStage {
    param([Parameter(Mandatory)][string[]]$ArgumentList)
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    $output = & powershell -NoProfile -ExecutionPolicy Bypass @ArgumentList 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $prevEap
    return [pscustomobject]@{ ExitCode = $code; Output = @($output | ForEach-Object { "$_" }) }
}

function Invoke-ReleaseCapture {
    param(
        [Parameter(Mandatory)][string]$ExePath,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$CandidateRoot,
        [Parameter(Mandatory)][string]$SourceHead,
        [Parameter(Mandatory)][string]$RuntimeHash,
        [Parameter(Mandatory)][string]$ExpectedTag,
        [Parameter(Mandatory)][string]$BuildLog,
        [Parameter(Mandatory)][string]$TestResultsXml,
        [Parameter(Mandatory)][string]$CaptureScript,
        [Parameter(Mandatory)][string]$ReportScript,
        [Parameter(Mandatory)][string]$ValidatorScript,
        [string]$RuntimeDllRelativePath = "Builds\Windows64\MyriadOfDragons_Data\Managed\MyriadOfDragons.Runtime.dll",
        [string]$CaptureOutDir = "handover/native_capture_out",
        [int]$WaitSeconds = 8,
        [switch]$LeaveRunning,
        [string]$AttachProcessId = "",
        [string[]]$RequiredScreens = @(),
        [string]$KnownStaleHashesFile = "",
        [string]$ReportOutJson = "",
        [string]$ReportOutMarkdown = ""
    )

    # --- Stage 0: independent version-pin check, before the pipeline touches anything. Stops
    # here (never reaching stage 1, so the player is never even considered) if the validator this
    # orchestrator was told to use is missing or stale. ---
    $validatorImpl = Test-ValidatorImplementation -Path $ValidatorScript
    if (-not $validatorImpl.Ok) {
        return [pscustomobject]@{
            ExitCode      = 2
            Stage         = "validator-pin"
            CaptureOutput = @(
                "REFUSED before the pipeline started: the validator this run would have used failed the version-pin check.",
                "  Validator script:  $($validatorImpl.ResolvedPath)"
                "  Validator SHA-256: $($validatorImpl.Sha256)"
                "  Reason: $($validatorImpl.Reason)"
            )
            ReportRan     = $false
            ReportOutput  = @()
        }
    }

    # --- Stage 1: gate (inside capture_native_window.ps1) + DPI-aware native capture. -ValidatorScript
    # is forwarded explicitly so the capture stage uses the SAME validator this orchestrator just
    # verified, never whatever happens to be sitting next to -CaptureScript. ---
    $captureArgs = @(
        "-File", $CaptureScript,
        "-ExePath", $ExePath, "-Label", $Label,
        "-CandidateRoot", $CandidateRoot, "-SourceHead", $SourceHead, "-RuntimeHash", $RuntimeHash,
        "-ExpectedTag", $ExpectedTag, "-BuildLog", $BuildLog, "-TestResultsXml", $TestResultsXml,
        "-RuntimeDllRelativePath", $RuntimeDllRelativePath, "-ValidatorScript", $validatorImpl.ResolvedPath,
        "-OutDir", $CaptureOutDir, "-WaitSeconds", $WaitSeconds
    )
    if ($LeaveRunning) { $captureArgs += "-LeaveRunning" }
    if ($AttachProcessId -ne "") { $captureArgs += @("-AttachProcessId", $AttachProcessId) }

    $captureStage = Invoke-PipelineStage -ArgumentList $captureArgs
    if ($captureStage.ExitCode -ne 0) {
        # PRODUCTIVE CODING TASK - observability. Always relay the capture stage's FULL output
        # (which, as of capture_native_window.ps1's own fix, already includes the resolved
        # validator path/SHA-256/exit code/every finding) on every non-zero exit - never a
        # summarized or truncated version.
        return [pscustomobject]@{
            ExitCode      = $captureStage.ExitCode
            Stage         = "capture"
            CaptureOutput = $captureStage.Output
            ReportRan     = $false
            ReportOutput  = @()
        }
    }

    # --- Stage 2: acceptance report against the capture this stage just wrote (full validation
    # mode - CaptureDir supplied - now that a real capture exists to check). ---
    $effectiveRequiredScreens = if ($RequiredScreens.Count -gt 0) { $RequiredScreens } else { @($Label) }
    $reportArgs = @(
        "-File", $ReportScript,
        "-CandidateRoot", $CandidateRoot, "-ExpectedHead", $SourceHead, "-ExpectedRuntimeHash", $RuntimeHash,
        "-RuntimeDllRelativePath", $RuntimeDllRelativePath, "-ExpectedTag", $ExpectedTag,
        "-CaptureDir", $CaptureOutDir, "-RequiredScreens", ($effectiveRequiredScreens -join ','),
        "-BuildLog", $BuildLog, "-TestResultsXml", $TestResultsXml
    )
    if ($KnownStaleHashesFile -ne "") { $reportArgs += @("-KnownStaleHashesFile", $KnownStaleHashesFile) }
    if ($ReportOutJson -ne "") { $reportArgs += @("-OutJson", $ReportOutJson) }
    if ($ReportOutMarkdown -ne "") { $reportArgs += @("-OutMarkdown", $ReportOutMarkdown) }

    $reportStage = Invoke-PipelineStage -ArgumentList $reportArgs
    return [pscustomobject]@{
        ExitCode      = $reportStage.ExitCode
        Stage         = "report"
        CaptureOutput = $captureStage.Output
        ReportRan     = $true
        ReportOutput  = $reportStage.Output
    }
}

# ---------------------------------------------------------------------------
# -SelfTest: proves the ORCHESTRATION (stop-before-report-on-gate-failure; runs the report only
# after a successful capture; propagates the right exit code at each stage) without needing a real
# Unity window. Stage 1 refusal cases go through the REAL capture_native_window.ps1 (already
# self-tested for its own DPI/window mechanics) against a real invalid fixture candidate - proves
# an invalid candidate genuinely stops this pipeline before any player launches. The one "stage 1
# succeeds" case substitutes a tiny fixture "capture" script (-CaptureScript override) that always
# exits 0 and writes a real 1920x1080 PNG + sidecar - proving stage 3 (the real report generator)
# is reached and produces a real PASS report, without requiring an actual Unity player.
# ---------------------------------------------------------------------------
if ($SelfTest) {
    $fails = 0
    function Check {
        param([string]$Name, [scriptblock]$Body)
        try { & $Body; Write-Host "PASS  $Name" }
        catch { Write-Host "FAIL  $Name :: $($_.Exception.Message)"; $script:fails++ }
    }

    Write-Host "=== run_release_capture.ps1 -SelfTest ==="

    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("run_release_capture_selftest_" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    $realCaptureScript = Join-Path (Split-Path -Parent $PSCommandPath) "capture_native_window.ps1"
    $realReportScript = Join-Path (Split-Path -Parent $PSCommandPath) "generate_candidate_acceptance_report.ps1"
    $realValidatorScript = Join-Path (Split-Path -Parent $PSCommandPath) "validate_release_candidate.ps1"

    $repoDir = Join-Path $tmp "candidate_repo"
    New-Item -ItemType Directory -Force -Path $repoDir | Out-Null
    & git -C $repoDir init -q 2>$null
    & git -C $repoDir config user.email "selftest@example.com" 2>$null
    & git -C $repoDir config user.name "selftest" 2>$null
    $dllRelPath = "Builds\Windows64\MyriadOfDragons_Data\Managed\MyriadOfDragons.Runtime.dll"
    $dllFullPath = Join-Path $repoDir $dllRelPath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dllFullPath) | Out-Null
    Set-Content -Path $dllFullPath -Value "fake runtime dll contents" -Encoding utf8
    Set-Content -Path (Join-Path $repoDir "a.txt") -Value "a" -Encoding utf8
    & git -C $repoDir add -A 2>$null
    & git -C $repoDir commit -q -m "candidate commit" 2>$null
    $realHead = (& git -C $repoDir rev-parse HEAD).Trim()
    & git -C $repoDir tag -a "run-release-fixture-frozen" -m "freeze" 2>$null
    $realDllHash = (Get-FileHash -LiteralPath $dllFullPath -Algorithm SHA256).Hash.ToLowerInvariant()

    $cleanLog = Join-Path $tmp "clean_build.log"
    Set-Content -Path $cleanLog -Value @("Compiling...", "Build succeeded.") -Encoding utf8
    $goodXml = Join-Path $tmp "good_results.xml"
    Set-Content -Path $goodXml -Value '<?xml version="1.0"?><test-run testcasecount="5" passed="5" failed="0"></test-run>' -Encoding utf8

    # 1. Invalid candidate (wrong Runtime.dll hash) -> stage 1 (real capture_native_window.ps1)
    # refuses, exit 2, no report step reached, no player ever launched (proven the same way
    # capture_native_window.ps1's own self-test proves it: OutDir/report paths stay empty).
    Check "REFUSES an invalid candidate before any capture, and never runs the report step" {
        $captureOut = Join-Path $tmp "out_refuse"
        $reportJson = Join-Path $tmp "should_not_exist.json"
        $reportMd = Join-Path $tmp "should_not_exist.md"
        $r = Invoke-ReleaseCapture -ExePath (Join-Path $tmp "does_not_exist.exe") -Label "ShouldNeverCapture" `
            -CandidateRoot $repoDir -SourceHead $realHead -RuntimeHash ("0" * 64) `
            -ExpectedTag "run-release-fixture-frozen" -BuildLog $cleanLog -TestResultsXml $goodXml `
            -CaptureScript $realCaptureScript -ReportScript $realReportScript -ValidatorScript $realValidatorScript `
            -CaptureOutDir $captureOut -ReportOutJson $reportJson -ReportOutMarkdown $reportMd
        if ($r.ExitCode -eq 0) { throw "expected non-zero exit, got 0" }
        if ($r.Stage -ne "capture") { throw "expected the pipeline to stop at the capture stage, stopped at $($r.Stage)" }
        if ($r.ReportRan) { throw "report stage must not run when the gate refuses" }
        if (Test-Path -LiteralPath $reportJson) { throw "acceptance report JSON must not exist after a gate refusal" }
        if (Test-Path -LiteralPath $captureOut) {
            $written = @(Get-ChildItem -LiteralPath $captureOut -File -ErrorAction SilentlyContinue)
            if ($written.Count -gt 0) { throw "CaptureOutDir has $($written.Count) file(s) - a capture was attempted despite the invalid candidate" }
        }
    }

    Check "REFUSES with a wrong/missing frozen tag before any capture" {
        $captureOut = Join-Path $tmp "out_refuse_tag"
        $r = Invoke-ReleaseCapture -ExePath (Join-Path $tmp "does_not_exist.exe") -Label "ShouldNeverCapture" `
            -CandidateRoot $repoDir -SourceHead $realHead -RuntimeHash $realDllHash `
            -ExpectedTag "no-such-tag" -BuildLog $cleanLog -TestResultsXml $goodXml `
            -CaptureScript $realCaptureScript -ReportScript $realReportScript -ValidatorScript $realValidatorScript -CaptureOutDir $captureOut
        if ($r.ExitCode -eq 0) { throw "expected non-zero exit, got 0" }
        if ($r.ReportRan) { throw "report stage must not run when the gate refuses" }
    }

    # 1b. PRODUCTIVE CODING TASK - script-version pinning. A "stale validator" fixture: a real
    # .ps1 that would run (correct param shape) but contains none of commit 77538a6d's fix
    # markers - simulating a frozen-rc31-checkout's stale sibling validator. Exits 99 if it were
    # ever actually invoked, so a bypassed version-pin check would fail on the WRONG exit code
    # (99, not 2) rather than passing by accident.
    $staleValidator = Join-Path $tmp "stale_validate_release_candidate.ps1"
    @'
param(
    [string]$CandidateRoot, [string]$ExpectedHead, [string]$ExpectedRuntimeHash,
    [string]$RuntimeDllRelativePath, [string]$ExpectedTag, [string]$CaptureDir, [string]$BuildLog, [string]$TestResultsXml
)
Write-Host "STALE VALIDATOR RAN - THIS SHOULD NEVER HAPPEN IN A SELF-TEST"
exit 99
'@ | Set-Content -Path $staleValidator -Encoding utf8
    $staleValidatorHash = (Get-FileHash -LiteralPath $staleValidator -Algorithm SHA256).Hash.ToLowerInvariant()

    Check "REFUSES a stale validator before stage 1 (capture_native_window.ps1 is never even invoked), exit 2" {
        $captureOut = Join-Path $tmp "out_stale_validator"
        $r = Invoke-ReleaseCapture -ExePath (Join-Path $tmp "does_not_exist.exe") -Label "ShouldNeverCapture" `
            -CandidateRoot $repoDir -SourceHead $realHead -RuntimeHash $realDllHash `
            -ExpectedTag "run-release-fixture-frozen" -BuildLog $cleanLog -TestResultsXml $goodXml `
            -CaptureScript $realCaptureScript -ReportScript $realReportScript -ValidatorScript $staleValidator `
            -CaptureOutDir $captureOut
        if ($r.ExitCode -ne 2) { throw "expected exit 2, got $($r.ExitCode)" }
        if ($r.Stage -ne "validator-pin") { throw "expected Stage='validator-pin' (refused before stage 1), got '$($r.Stage)'" }
        if ($r.ReportRan) { throw "report stage must not run" }
        if (($r.CaptureOutput | Where-Object { $_ -like "*$staleValidatorHash*" }).Count -eq 0) {
            throw "diagnostic does not include the stale validator's SHA-256: $($r.CaptureOutput -join ' | ')"
        }
        if (($r.CaptureOutput | Where-Object { $_ -like "*safe.directory*" }).Count -eq 0) {
            throw "diagnostic does not mention the safe.directory fix"
        }
        if (Test-Path -LiteralPath $captureOut) {
            $written = @(Get-ChildItem -LiteralPath $captureOut -File -ErrorAction SilentlyContinue)
            if ($written.Count -gt 0) { throw "CaptureOutDir has $($written.Count) file(s) - a capture was attempted despite the stale validator" }
        }
    }

    # 2. Fixture "capture" script that always succeeds - proves stage 3 (the REAL report
    # generator) is reached with the right arguments and produces a real PASS report, i.e. the
    # capture-succeeds branch of the orchestration is also actually exercised, not just assumed.
    $fixtureCaptureScript = Join-Path $tmp "fixture_capture_native_window.ps1"
    @'
param(
    [string]$ExePath, [string]$Label, [string]$CandidateRoot, [string]$SourceHead, [string]$RuntimeHash,
    [string]$ExpectedTag, [string]$BuildLog, [string]$TestResultsXml, [string]$RuntimeDllRelativePath,
    [string]$ValidatorScript, [string]$OutDir, [int]$WaitSeconds, [switch]$LeaveRunning, [string]$AttachProcessId
)
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$pngPath = Join-Path $OutDir "${Label}_1920x1080.png"
$jsonPath = Join-Path $OutDir "${Label}_1920x1080.json"
$bmp = New-Object System.Drawing.Bitmap(1920, 1080)
try { $bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png) } finally { $bmp.Dispose() }
$pngHash = (Get-FileHash -LiteralPath $pngPath -Algorithm SHA256).Hash.ToLowerInvariant()
@{ label = $Label; sourceHead = $SourceHead; runtimeHash = $RuntimeHash; pngSha256 = $pngHash } |
    ConvertTo-Json | Set-Content -Path $jsonPath -Encoding utf8
Write-Host "FIXTURE CAPTURE OK (no real player launched)"
exit 0
'@ | Set-Content -Path $fixtureCaptureScript -Encoding utf8

    Check "PASSES a valid candidate through both stages: fixture capture succeeds, real report says PASS" {
        $captureOut = Join-Path $tmp "out_pass"
        $reportJson = Join-Path $tmp "pass_report.json"
        $reportMd = Join-Path $tmp "pass_report.md"
        $r = Invoke-ReleaseCapture -ExePath "unused-by-fixture.exe" -Label "FixtureScreen" `
            -CandidateRoot $repoDir -SourceHead $realHead -RuntimeHash $realDllHash `
            -ExpectedTag "run-release-fixture-frozen" -BuildLog $cleanLog -TestResultsXml $goodXml `
            -CaptureScript $fixtureCaptureScript -ReportScript $realReportScript -ValidatorScript $realValidatorScript `
            -CaptureOutDir $captureOut -ReportOutJson $reportJson -ReportOutMarkdown $reportMd

        if (-not $r.ReportRan) { throw "report stage should have run after a successful capture" }
        if ($r.ExitCode -ne 0) { throw "expected exit 0 (PASS), got $($r.ExitCode): $($r.ReportOutput -join ' | ')" }
        if (-not (Test-Path -LiteralPath $reportJson)) { throw "acceptance report JSON was not written" }
        if (-not (Test-Path -LiteralPath $reportMd)) { throw "acceptance report Markdown was not written" }
        $parsed = Get-Content -LiteralPath $reportJson -Raw | ConvertFrom-Json
        if ($parsed.decision -ne "PASS") { throw "report decision should be PASS, got $($parsed.decision)" }
        if ($parsed.captures.Count -ne 1 -or -not $parsed.captures[0].matches1920x1080) {
            throw "report did not carry through the fixture's real 1920x1080 capture"
        }
        # Stage 3 must receive -BuildLog/-TestResultsXml too, not just stage 1's gate - a report
        # that stops the pipeline succeeding but still says "No build log supplied" is misleading.
        if (-not $parsed.compilerStatus.buildLogSupplied -or -not $parsed.compilerStatus.clean) {
            throw "report's compilerStatus should reflect the same clean build log the gate verified, got: $($parsed.compilerStatus | ConvertTo-Json -Compress)"
        }
        if (-not $parsed.testTotals.resultsSupplied -or $parsed.testTotals.failed -ne 0) {
            throw "report's testTotals should reflect the same passing results the gate verified, got: $($parsed.testTotals | ConvertTo-Json -Compress)"
        }
    }

    Check "Defaults -RequiredScreens to the captured -Label when not explicitly supplied" {
        $captureOut = Join-Path $tmp "out_default_required"
        $reportJson = Join-Path $tmp "default_required_report.json"
        # -ReportOutMarkdown is passed explicitly too (even though this check doesn't inspect it) -
        # omitting it makes the report generator fall back to a default filename in the CURRENT
        # directory, littering whatever directory this self-test happens to be run from.
        $reportMd = Join-Path $tmp "default_required_report.md"
        $r = Invoke-ReleaseCapture -ExePath "unused-by-fixture.exe" -Label "DefaultRequiredScreen" `
            -CandidateRoot $repoDir -SourceHead $realHead -RuntimeHash $realDllHash `
            -ExpectedTag "run-release-fixture-frozen" -BuildLog $cleanLog -TestResultsXml $goodXml `
            -CaptureScript $fixtureCaptureScript -ReportScript $realReportScript -ValidatorScript $realValidatorScript `
            -CaptureOutDir $captureOut -ReportOutJson $reportJson -ReportOutMarkdown $reportMd
        if ($r.ExitCode -ne 0) { throw "expected exit 0, got $($r.ExitCode): $($r.ReportOutput -join ' | ')" }
        $parsed = Get-Content -LiteralPath $reportJson -Raw | ConvertFrom-Json
        if ($parsed.captureCompleteness.requiredScreens -notcontains "DefaultRequiredScreen") {
            throw "expected the captured Label to be used as the default required screen"
        }
        if ($parsed.captureCompleteness.missingScreens.Count -ne 0) { throw "the just-captured screen should not be reported missing" }
    }

    # 3. End-to-end via the real CLI (-File $PSCommandPath), not just the Invoke-ReleaseCapture
    # function - proves the actual command line surface refuses correctly too.
    Check "End-to-end CLI: invalid candidate refuses (non-zero exit), no report files written" {
        $captureOut = Join-Path $tmp "out_cli_refuse"
        $reportJson = Join-Path $tmp "cli_should_not_exist.json"
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "SilentlyContinue"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
            -ExePath (Join-Path $tmp "does_not_exist.exe") -Label "CliShouldNeverCapture" `
            -CandidateRoot $repoDir -SourceHead ("9" * 40) -RuntimeHash $realDllHash `
            -ExpectedTag "run-release-fixture-frozen" -BuildLog $cleanLog -TestResultsXml $goodXml `
            -CaptureOutDir $captureOut -ReportOutJson $reportJson 1>$null 2>$null
        $code = $LASTEXITCODE
        $ErrorActionPreference = $prevEap
        if ($code -eq 0) { throw "expected non-zero exit, got 0" }
        if (Test-Path -LiteralPath $reportJson) { throw "report must not exist after a CLI-level gate refusal" }
    }

    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host ""
    if ($fails -eq 0) { Write-Host "SELFTEST: all guard checks passed."; exit 0 }
    else { Write-Host "SELFTEST: $fails guard check(s) FAILED."; exit 1 }
}

# ---------------------------------------------------------------------------
# Run mode: the real three-stage pipeline against a real candidate/player.
# ---------------------------------------------------------------------------

$result = Invoke-ReleaseCapture -ExePath $ExePath -Label $Label -CandidateRoot $CandidateRoot `
    -SourceHead $SourceHead -RuntimeHash $RuntimeHash -ExpectedTag $ExpectedTag `
    -BuildLog $BuildLog -TestResultsXml $TestResultsXml -RuntimeDllRelativePath $RuntimeDllRelativePath `
    -CaptureScript $CaptureScript -ReportScript $ReportScript -ValidatorScript $ValidatorScript -CaptureOutDir $CaptureOutDir `
    -WaitSeconds $WaitSeconds -LeaveRunning:$LeaveRunning -AttachProcessId $AttachProcessId `
    -RequiredScreens $RequiredScreens -KnownStaleHashesFile $KnownStaleHashesFile `
    -ReportOutJson $ReportOutJson -ReportOutMarkdown $ReportOutMarkdown

Write-Output "run_release_capture: one-command gate -> capture -> report pipeline"
Write-Output "Stage reached: $($result.Stage)"
Write-Output ""
Write-Output "--- capture stage output ---"
foreach ($line in $result.CaptureOutput) { Write-Output $line }

if (-not $result.ReportRan) {
    Write-Output ""
    Write-Output "STOPPED: the pre-capture gate refused (or the capture step failed) - no report was generated."
    exit $result.ExitCode
}

Write-Output ""
Write-Output "--- report stage output ---"
foreach ($line in $result.ReportOutput) { Write-Output $line }
exit $result.ExitCode
