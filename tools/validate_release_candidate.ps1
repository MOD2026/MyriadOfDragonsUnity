# Reusable release-candidate evidence validator (acceptance-gate tooling).
#
# Why this exists: this session repeatedly hand-validated release candidates against a checklist
# (exact HEAD, Runtime.dll hash, native 1920x1080 dimensions, sidecar provenance, duplicate-pixel
# rejection, required-screen coverage, placeholder/error text) - by hand, every time, across
# rc8..rc19+. That is slow and error-prone (a hand check already missed the exact stale-pixel
# duplicate this tool now catches automatically at rc9). This script mechanises the checklist so it
# can be re-run identically on every future candidate.
#
# Usage (validate a real candidate):
#   powershell -File tools/validate_release_candidate.ps1 `
#     -CandidateRoot "C:\Users\zihan\Downloads\MoD-lk-line-019" `
#     -ExpectedHead "d4469a9ce70f5e920753f52e528ffccbadde8fef" `
#     -ExpectedRuntimeHash "aeca62d094b2cc4263396f3fe2ae2605fbdfc8752e956ddb647176235d5b483a" `
#     -ExpectedTag "lk/FROZEN-capture-candidate-rc19" `
#     -CaptureDir "ve_rc19_native_capture" `
#     -RequiredScreens "PackOpen","LoadingSigil","Bazaar","BattlePass","BattleUI","Friends" `
#     -BuildLog "MoD-lk-line-019\lk_rc19_build.log" `
#     -TestResultsXml "MoD-lk-line-019\lk_rc19_results.xml"
#
# Usage (GATE MODE - validate a candidate BEFORE capture exists, omit -CaptureDir):
#   powershell -File tools/validate_release_candidate.ps1 `
#     -CandidateRoot "C:\Users\zihan\Downloads\MoD-lk-line-019" `
#     -ExpectedHead "d4469a9ce70f5e920753f52e528ffccbadde8fef" `
#     -ExpectedRuntimeHash "aeca62d094b2cc4263396f3fe2ae2605fbdfc8752e956ddb647176235d5b483a" `
#     -ExpectedTag "lk/FROZEN-capture-candidate-rc19" `
#     -BuildLog "MoD-lk-line-019\lk_rc19_build.log" `
#     -TestResultsXml "MoD-lk-line-019\lk_rc19_results.xml"
#   This is what tools/capture_native_window.ps1 calls automatically before every capture - checks
#   exact HEAD, tracked dirt, the frozen tag, Runtime.dll hash, and clean build/test evidence, but
#   skips every capture-set check (there's nothing to check yet). -BuildLog and -TestResultsXml are
#   REQUIRED in gate mode (exit 2 if omitted) - a gate that didn't require them could pass a
#   candidate that never actually compiled or tested cleanly.
#
# Usage (run the tool's own automated tests, no candidate/build/Unity needed):
#   powershell -File tools/validate_release_candidate.ps1 -SelfTest
#
# Exit codes: 0 = every check passed. 1 = at least one check failed (a real, reportable acceptance
# failure). 2 = the tool itself could not run (bad arguments, missing required path). -SelfTest
# exits 0 only if every internal guard test passes.

[CmdletBinding(DefaultParameterSetName = "Validate")]
param(
    [Parameter(ParameterSetName = "Validate", Mandatory = $true)]
    [string]$CandidateRoot,

    [Parameter(ParameterSetName = "Validate", Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$ExpectedHead,

    [Parameter(ParameterSetName = "Validate", Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$ExpectedRuntimeHash,

    [Parameter(ParameterSetName = "Validate")]
    [string]$RuntimeDllRelativePath = "Builds\Windows64\MyriadOfDragons_Data\Managed\MyriadOfDragons.Runtime.dll",

    [Parameter(ParameterSetName = "Validate")]
    [string]$ExpectedTag = "",

    # Optional in "gate" usage (PRODUCTIVE CODING TASK - wire into the capture workflow): when
    # omitted, captures don't exist yet - this validates only what's knowable BEFORE capture
    # (exact HEAD, tracked dirt, frozen tag, Runtime.dll hash, build log, test results) so
    # tools/capture_native_window.ps1 can refuse to run against an unvalidated candidate. Supply
    # it for the original post-hoc full validation (adds provenance/dimension/duplicate/
    # required-screen checks against real capture output).
    [Parameter(ParameterSetName = "Validate")]
    [string]$CaptureDir = "",

    [Parameter(ParameterSetName = "Validate")]
    [string[]]$RequiredScreens = @(),

    [Parameter(ParameterSetName = "Validate")]
    [string]$BuildLog = "",

    [Parameter(ParameterSetName = "Validate")]
    [string]$TestResultsXml = "",

    [Parameter(ParameterSetName = "Validate")]
    [string]$KnownStaleHashesFile = "",

    [Parameter(ParameterSetName = "SelfTest", Mandatory = $true)]
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

# Invoked via `powershell -File`, EVERY argument arrives as a plain string, so
# `-RequiredScreens A,B,C` binds as ONE element "A,B,C" rather than an array (same gotcha already
# documented and fixed in tools/run_editmode_tests.ps1 for -TestFilters). Split defensively so both
# the native array form (calling this script's functions directly) and the -File CLI form behave
# identically.
if ($RequiredScreens.Count -gt 0) {
    $RequiredScreens = @($RequiredScreens | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
}

# ---------------------------------------------------------------------------
# Guard / check functions (pure enough to unit-test via -SelfTest with
# fixtures, no git/Unity/network required for the self-test path).
# ---------------------------------------------------------------------------

function Get-PngDimensions {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw "PNG not found: $Path" }
    if ((Get-Item -LiteralPath $Path).Length -eq 0) { throw "file is empty" }
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $header = New-Object byte[] 24
        if ($stream.Read($header, 0, 24) -ne 24) { throw "PNG header is truncated" }
        $signature = @(137, 80, 78, 71, 13, 10, 26, 10)
        for ($i = 0; $i -lt 8; $i++) {
            if ($header[$i] -ne $signature[$i]) { throw "not a PNG file" }
        }
        $ihdrLength = ([int64]$header[8] -shl 24) -bor ([int64]$header[9] -shl 16) -bor
            ([int64]$header[10] -shl 8) -bor [int64]$header[11]
        if ($ihdrLength -ne 13) { throw "PNG IHDR length is $ihdrLength, expected 13" }
        if ([System.Text.Encoding]::ASCII.GetString($header, 12, 4) -ne "IHDR") {
            throw "PNG has no IHDR at the expected position"
        }
        $width = ([int64]$header[16] -shl 24) -bor ([int64]$header[17] -shl 16) -bor
            ([int64]$header[18] -shl 8) -bor [int64]$header[19]
        $height = ([int64]$header[20] -shl 24) -bor ([int64]$header[21] -shl 16) -bor
            ([int64]$header[22] -shl 8) -bor [int64]$header[23]
        if ($width -le 0 -or $height -le 0) { throw "PNG IHDR dimensions must be positive" }
        return [pscustomobject]@{ Width = [int]$width; Height = [int]$height }
    }
    finally { $stream.Dispose() }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][string]$Path)
    $hash = Get-FileHash -LiteralPath $Path -Algorithm SHA256
    return $hash.Hash.ToLowerInvariant()
}

# PRODUCTIVE CODING TASK - every git command this script runs against -CandidateRoot goes through
# this helper, which prepends `-c safe.directory=<path>` as a PROCESS-LOCAL override (an argument
# to this one git invocation, not a config write) rather than trusting the caller's global/system
# gitconfig to already have it. Git refuses to operate at all (exit 128, "detected dubious
# ownership") on a repo whose directory owner differs from the current user - a real, common
# condition for a candidate checked out or copied by a different account/service than the one
# running this validator. Without this, a genuinely valid, correctly-tagged, byte-identical
# candidate would be REPORTED AS A FAILURE (git's error text landing in $actualHead/$resolved,
# nowhere near a real SHA) rather than validated - the opposite of what an acceptance gate is for.
# Deliberately never writes `git config --global --add safe.directory ...` - that would persist a
# trust decision on the machine beyond this one validation run, which is not this script's call to
# make. `-c` only affects the single git process it's attached to.
function Invoke-GitOnCandidate {
    param(
        [Parameter(Mandatory)][string]$RepoPath,
        [Parameter(Mandatory)][string[]]$GitArgs
    )
    $allArgs = @("-c", "safe.directory=$RepoPath", "-C", $RepoPath) + $GitArgs
    & git @allArgs
}

# Exact HEAD + tracked-dirt check against a git worktree. Returns a result object rather than
# throwing, so callers can accumulate multiple findings instead of stopping at the first one.
function Test-ExactHead {
    param(
        [Parameter(Mandatory)][string]$RepoPath,
        [Parameter(Mandatory)][string]$ExpectedHead
    )
    $actualHead = (Invoke-GitOnCandidate -RepoPath $RepoPath -GitArgs @("rev-parse", "HEAD") 2>$null).Trim()
    $dirtLines = @(Invoke-GitOnCandidate -RepoPath $RepoPath -GitArgs @("status", "--porcelain", "--untracked-files=no") 2>$null)
    return [pscustomobject]@{
        ExpectedHead = $ExpectedHead.ToLowerInvariant()
        ActualHead   = $actualHead.ToLowerInvariant()
        HeadMatches  = ($actualHead.ToLowerInvariant() -eq $ExpectedHead.ToLowerInvariant())
        DirtCount    = $dirtLines.Count
    }
}

# Verifies an annotated/lightweight tag resolves to the exact expected commit (not just "exists").
function Test-FrozenTag {
    param(
        [Parameter(Mandatory)][string]$RepoPath,
        [Parameter(Mandatory)][string]$Tag,
        [Parameter(Mandatory)][string]$ExpectedHead
    )
    # A nonexistent tag is an entirely normal, expected input here (that IS the "missing frozen
    # tag" case this function exists to report) - git exits non-zero and writes to stderr for it.
    # Under this script's own $ErrorActionPreference = "Stop", a native command's non-zero exit
    # can still throw even with its stderr stream redirected to $null (a real, reproducible PS 5.1
    # quirk - confirmed while adding this function's own end-to-end self-test), so the exit code
    # must be checked explicitly rather than relying on redirection alone to keep this non-fatal.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    $resolved = (Invoke-GitOnCandidate -RepoPath $RepoPath -GitArgs @("rev-parse", "$Tag^{commit}") 2>$null)
    $gitExitCode = $LASTEXITCODE
    $ErrorActionPreference = $prevEap
    if ($gitExitCode -ne 0 -or [string]::IsNullOrEmpty($resolved)) {
        return [pscustomobject]@{ TagExists = $false; ResolvesToExpected = $false; Resolved = "" }
    }
    $resolved = $resolved.Trim()
    return [pscustomobject]@{
        TagExists          = $true
        ResolvesToExpected = ($resolved.ToLowerInvariant() -eq $ExpectedHead.ToLowerInvariant())
        Resolved           = $resolved
    }
}

# Loads a capture directory's PNG+JSON sidecar pairs. A PNG with no matching sidecar, or a sidecar
# missing required provenance fields, is reported as a finding rather than silently skipped.
function Get-CaptureSet {
    param([Parameter(Mandatory)][string]$CaptureDir)
    $pngs = @(Get-ChildItem -LiteralPath $CaptureDir -File -Filter "*.png" -ErrorAction SilentlyContinue)
    $captures = New-Object System.Collections.Generic.List[object]
    $findings = New-Object System.Collections.Generic.List[string]
    foreach ($png in $pngs) {
        $sidecarPath = Join-Path $png.DirectoryName ($png.BaseName + ".json")
        if (-not (Test-Path -LiteralPath $sidecarPath)) {
            $findings.Add("MISSING SIDECAR for $($png.Name)")
            continue
        }
        try {
            $sidecar = Get-Content -LiteralPath $sidecarPath -Raw | ConvertFrom-Json
        }
        catch {
            $findings.Add("MALFORMED SIDECAR JSON: $sidecarPath ($($_.Exception.Message))")
            continue
        }
        $captures.Add([pscustomobject]@{
            Png      = $png
            Sidecar  = $sidecar
            Sidecar_Path = $sidecarPath
        })
    }
    return [pscustomobject]@{ Captures = $captures; Findings = $findings }
}

# Provenance: HEAD, Runtime.dll hash (not the .exe), PNG hash, and PNG dimensions, all checked
# against the sidecar's own declared values plus independently recomputed ground truth.
function Test-CaptureProvenance {
    param(
        [Parameter(Mandatory)]$Capture,
        [Parameter(Mandatory)][string]$ExpectedHead,
        [Parameter(Mandatory)][string]$ExpectedRuntimeHash
    )
    $findings = New-Object System.Collections.Generic.List[string]
    $name = $Capture.Png.Name
    $sidecar = $Capture.Sidecar

    $hasSourceHead = $null -ne ($sidecar.PSObject.Properties.Name -match '^sourceHead$' | Select-Object -First 1) -and
        -not [string]::IsNullOrWhiteSpace($sidecar.sourceHead)
    $hasRuntimeHash = $null -ne ($sidecar.PSObject.Properties.Name -match '^runtimeHash$' | Select-Object -First 1) -and
        -not [string]::IsNullOrWhiteSpace($sidecar.runtimeHash)

    if (-not $hasSourceHead -or -not $hasRuntimeHash) {
        $findings.Add("MISSING PROVENANCE in $name`: sidecar lacks sourceHead and/or runtimeHash (exe-only or absent identity is not acceptable)")
    }
    else {
        if ($sidecar.sourceHead.ToLowerInvariant() -ne $ExpectedHead.ToLowerInvariant()) {
            $findings.Add("WRONG HEAD in $name`: expected $ExpectedHead, sidecar says $($sidecar.sourceHead)")
        }
        if ($sidecar.runtimeHash.ToLowerInvariant() -ne $ExpectedRuntimeHash.ToLowerInvariant()) {
            $findings.Add("WRONG RUNTIME HASH in $name`: expected $ExpectedRuntimeHash, sidecar says $($sidecar.runtimeHash)")
        }
    }

    $actualPngHash = Get-Sha256Hex -Path $Capture.Png.FullName
    if ($sidecar.PSObject.Properties.Name -contains 'pngSha256') {
        if ($sidecar.pngSha256.ToLowerInvariant() -ne $actualPngHash) {
            $findings.Add("SIDECAR HASH MISMATCH in $name`: sidecar says $($sidecar.pngSha256), actual file is $actualPngHash")
        }
    }
    else {
        $findings.Add("MISSING pngSha256 field in sidecar for $name")
    }

    try {
        $dims = Get-PngDimensions -Path $Capture.Png.FullName
        if ($dims.Width -ne 1920 -or $dims.Height -ne 1080) {
            $findings.Add("WRONG DIMENSIONS in $name`: expected 1920x1080, got $($dims.Width)x$($dims.Height)")
        }
    }
    catch {
        $findings.Add("INVALID PNG $name`: $($_.Exception.Message)")
    }

    return [pscustomobject]@{ Name = $name; ActualPngHash = $actualPngHash; Findings = $findings }
}

# Cross-capture and cross-candidate duplicate-pixel rejection.
function Find-DuplicatePixelHashes {
    param(
        [Parameter(Mandatory)][hashtable]$HashesByName,
        [string[]]$KnownStaleHashes = @()
    )
    $findings = New-Object System.Collections.Generic.List[string]
    $seenBy = @{}
    foreach ($name in $HashesByName.Keys) {
        $hash = $HashesByName[$name]
        if ($seenBy.ContainsKey($hash)) {
            $findings.Add("DUPLICATE PIXELS: $name is byte-identical to $($seenBy[$hash]) (sha256 $hash)")
        }
        else {
            $seenBy[$hash] = $name
        }
        if ($KnownStaleHashes -contains $hash) {
            $findings.Add("STALE PIXELS: $name matches a previously-known-stale hash ($hash) - this is a recycled frame, not a fresh render")
        }
    }
    return ,$findings
}

# Required-screen coverage: every label in $RequiredScreens must be matched by at least one
# capture's sidecar "label" field (case-insensitive substring match, since real labels carry a
# candidate prefix like "rc19_PackOpen").
function Find-MissingRequiredScreens {
    param(
        [Parameter(Mandatory)][string[]]$RequiredScreens,
        [Parameter(Mandatory)][object[]]$Captures
    )
    $labels = @($Captures | ForEach-Object {
        if ($_.Sidecar.PSObject.Properties.Name -contains 'label') { $_.Sidecar.label } else { "" }
    })
    $missing = New-Object System.Collections.Generic.List[string]
    foreach ($required in $RequiredScreens) {
        $found = $false
        foreach ($label in $labels) {
            if ($label -imatch [regex]::Escape($required)) { $found = $true; break }
        }
        if (-not $found) { $missing.Add($required) }
    }
    return ,$missing
}

# Scans arbitrary text (sidecar note fields, build/test logs) for runtime-error, 404, and
# placeholder-content markers that should never appear in accepted evidence.
#
# PRODUCTIVE CODING TASK - the old "HTTP 404" pattern ((?<![0-9])404(?![0-9])) matched ANY 404 not
# touching another digit - including Unity's own build-progress counters, e.g.
# "[404/660] Importing 'GUID: ...'" (real text from this project's own build logs, not a runtime
# error). That is a real false-positive that would have failed a clean candidate's gate for
# nothing. The fix excludes the "N/M" progress-counter shape specifically (404 immediately
# followed by "/" and a digit, or immediately preceded by "[" - covers both the bracketed and
# unbracketed forms Unity logs use) while still catching every real HTTP-404 shape: "HTTP 404",
# "returned 404", "404 Not Found", "Error 404.", a bare "404" on its own. Also added a dedicated
# "module not found" marker for the real Cloud Code failure mode this project has actually hit
# ("Module could not be found" - see RetentionTelemetryGateway.cs's own history) - that text has
# no digits at all, so the numeric 404 pattern alone could never catch it.
function Find-ForbiddenTextMarkers {
    param([Parameter(Mandatory)][string]$Text, [string]$SourceName = "")
    $patterns = @(
        @{ Name = "NullReferenceException"; Pattern = 'NullReferenceException' },
        @{ Name = "unhandled exception";    Pattern = '(?i)unhandled exception' },
        @{ Name = "HTTP 404";               Pattern = '(?<!\[)\b404\b(?!/\d)' },
        @{ Name = "module not found";       Pattern = '(?i)module (could not be found|not found)' },
        @{ Name = "placeholder text";       Pattern = '(?i)\bplaceholder\b' },
        @{ Name = "lorem ipsum";            Pattern = '(?i)lorem ipsum' },
        @{ Name = "TODO marker";            Pattern = '(?i)\bTODO\b' },
        @{ Name = "missing-asset bracket";  Pattern = '\[MISSING\]' },
        @{ Name = "fatal error";            Pattern = '(?i)fatal error' },
        @{ Name = "aborting batchmode";     Pattern = 'Aborting batchmode' }
    )
    $findings = New-Object System.Collections.Generic.List[string]
    foreach ($p in $patterns) {
        if ($Text -match $p.Pattern) {
            $prefix = if ($SourceName) { "$SourceName`: " } else { "" }
            $findings.Add("FORBIDDEN MARKER ($($p.Name)) found in $prefix(matched '$($Matches[0])')")
        }
    }
    return ,$findings
}

function Test-BuildLog {
    param([Parameter(Mandatory)][string]$Path)
    $findings = New-Object System.Collections.Generic.List[string]
    if (-not (Test-Path -LiteralPath $Path)) {
        $findings.Add("MISSING build log: $Path")
        return ,$findings
    }
    $text = Get-Content -LiteralPath $Path -Raw
    $errorCsCount = ([regex]::Matches($text, '(?im)error CS\d*')).Count
    if ($errorCsCount -gt 0) {
        $findings.Add("COMPILER ERRORS: $errorCsCount 'error CS' occurrence(s) in $Path")
    }
    $findings.AddRange((Find-ForbiddenTextMarkers -Text $text -SourceName (Split-Path -Leaf $Path)))
    return ,$findings
}

function Test-TestResultsXml {
    param([Parameter(Mandatory)][string]$Path)
    $findings = New-Object System.Collections.Generic.List[string]
    if (-not (Test-Path -LiteralPath $Path)) {
        $findings.Add("MISSING test results XML: $Path")
        return [pscustomobject]@{ Findings = $findings; Total = $null; Passed = $null; Failed = $null }
    }
    try {
        [xml]$xml = Get-Content -LiteralPath $Path -Raw
    }
    catch {
        $findings.Add("MALFORMED test results XML: $Path ($($_.Exception.Message))")
        return [pscustomobject]@{ Findings = $findings; Total = $null; Passed = $null; Failed = $null }
    }
    $run = $xml.'test-run'
    if ($null -eq $run) {
        $findings.Add("MISSING <test-run> root element in $Path")
        return [pscustomobject]@{ Findings = $findings; Total = $null; Passed = $null; Failed = $null }
    }
    $total = [int]$run.testcasecount
    $passed = [int]$run.passed
    $failed = [int]$run.failed
    if ($total -eq 0) {
        $findings.Add("ZERO TESTS in $Path - not a passing run")
    }
    if ($failed -ne 0) {
        $findings.Add("TEST FAILURES: $failed of $total failed in $Path")
    }
    return [pscustomobject]@{ Findings = $findings; Total = $total; Passed = $passed; Failed = $failed }
}

# ---------------------------------------------------------------------------
# -SelfTest: exercise every guard function with fixtures. No git, no Unity,
# no network. Prints PASS/FAIL per check and exits 0 only if all pass.
# ---------------------------------------------------------------------------
if ($SelfTest) {
    $fails = 0
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("validate_rc_selftest_" + [guid]::NewGuid().ToString("N"))
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

    # Minimal valid 1x1 PNG (used as a stand-in for a "wrong dimensions" fixture) and a real
    # 1920x1080 PNG built via .NET so IHDR dimensions are genuine, not hand-crafted bytes.
    function New-TestPng {
        param([string]$Path, [int]$Width, [int]$Height)
        Add-Type -AssemblyName System.Drawing -ErrorAction SilentlyContinue
        $bmp = New-Object System.Drawing.Bitmap($Width, $Height)
        try { $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png) }
        finally { $bmp.Dispose() }
    }

    Write-Host "=== validate_release_candidate.ps1 -SelfTest ==="

    # 1. PNG dimension reader: correct on a real 1920x1080 PNG, correct on a real 640x480 PNG.
    $goodPng = Join-Path $tmp "good_1920x1080.png"
    $badPng  = Join-Path $tmp "bad_640x480.png"
    New-TestPng -Path $goodPng -Width 1920 -Height 1080
    New-TestPng -Path $badPng -Width 640 -Height 480
    Check "PNG dimension reader: correct on real 1920x1080 PNG" {
        $d = Get-PngDimensions -Path $goodPng
        if ($d.Width -ne 1920 -or $d.Height -ne 1080) { throw "got $($d.Width)x$($d.Height)" }
    }
    Check "PNG dimension reader: correct on real 640x480 PNG" {
        $d = Get-PngDimensions -Path $badPng
        if ($d.Width -ne 640 -or $d.Height -ne 480) { throw "got $($d.Width)x$($d.Height)" }
    }
    Expect-Throw "PNG dimension reader: rejects a non-PNG file" {
        $notPng = Join-Path $tmp "notpng.png"
        Set-Content -Path $notPng -Value "not a png" -Encoding ascii
        Get-PngDimensions -Path $notPng | Out-Null
    }
    Expect-Throw "PNG dimension reader: rejects a missing file" {
        Get-PngDimensions -Path (Join-Path $tmp "does_not_exist.png") | Out-Null
    }

    # 2. Sha256 helper: deterministic and case-normalised.
    Check "Sha256 helper: lowercase hex, stable across calls" {
        $h1 = Get-Sha256Hex -Path $goodPng
        $h2 = Get-Sha256Hex -Path $goodPng
        if ($h1 -ne $h2) { throw "hash not stable" }
        if ($h1 -cne $h1.ToLowerInvariant()) { throw "hash not lowercase" }
    }

    # 3. Duplicate-pixel detector: flags two names sharing one hash, flags a match to a known-stale
    #    hash, and stays silent when everything is unique and fresh.
    Check "Duplicate detector: flags two captures sharing one hash" {
        $map = @{ "A" = "deadbeef"; "B" = "deadbeef"; "C" = "cafef00d" }
        $findings = Find-DuplicatePixelHashes -HashesByName $map
        if (($findings | Where-Object { $_ -like "*DUPLICATE PIXELS*" }).Count -eq 0) { throw "duplicate not flagged" }
    }
    Check "Duplicate detector: flags a match to a known-stale hash" {
        $map = @{ "A" = "115e4c39" }
        $findings = Find-DuplicatePixelHashes -HashesByName $map -KnownStaleHashes @("115e4c39")
        if (($findings | Where-Object { $_ -like "*STALE PIXELS*" }).Count -eq 0) { throw "stale match not flagged" }
    }
    Check "Duplicate detector: silent when all hashes are unique and fresh" {
        $map = @{ "A" = "aaaa"; "B" = "bbbb" }
        $findings = Find-DuplicatePixelHashes -HashesByName $map -KnownStaleHashes @("cccc")
        if ($findings.Count -ne 0) { throw "false positive: $($findings -join '; ')" }
    }

    # 4. Required-screen coverage: flags a genuinely missing screen, accepts a substring match.
    $fakeCaptures = @(
        [pscustomobject]@{ Sidecar = [pscustomobject]@{ label = "rc19_PackOpen" } },
        [pscustomobject]@{ Sidecar = [pscustomobject]@{ label = "rc19_Friends" } }
    )
    Check "Required-screen check: accepts a real substring match" {
        $missing = Find-MissingRequiredScreens -RequiredScreens @("PackOpen") -Captures $fakeCaptures
        if ($missing.Count -ne 0) { throw "false positive: $($missing -join ', ')" }
    }
    Check "Required-screen check: flags a genuinely missing screen" {
        $missing = Find-MissingRequiredScreens -RequiredScreens @("BattlePass") -Captures $fakeCaptures
        if ($missing.Count -ne 1 -or $missing[0] -ne "BattlePass") { throw "did not flag BattlePass: $($missing -join ', ')" }
    }

    # 5. Forbidden-text scanner: catches each marker class, stays silent on clean text.
    Check "Forbidden-text scanner: catches NullReferenceException" {
        $f = Find-ForbiddenTextMarkers -Text "at Foo.Bar() NullReferenceException: Object reference"
        if (($f | Where-Object { $_ -like "*NullReferenceException*" }).Count -eq 0) { throw "not caught" }
    }
    Check "Forbidden-text scanner: catches a 404 (returned 404)" {
        $f = Find-ForbiddenTextMarkers -Text "GET /api/foo returned 404"
        if (($f | Where-Object { $_ -like "*HTTP 404*" }).Count -eq 0) { throw "not caught" }
    }
    Check "Forbidden-text scanner: catches a 404 (HTTP 404 status line)" {
        $f = Find-ForbiddenTextMarkers -Text "Response: HTTP 404 - resource not found"
        if (($f | Where-Object { $_ -like "*HTTP 404*" }).Count -eq 0) { throw "not caught" }
    }
    Check "Forbidden-text scanner: catches a 404 (trailing punctuation, no slash)" {
        $f = Find-ForbiddenTextMarkers -Text "Cloud Code call failed with error 404."
        if (($f | Where-Object { $_ -like "*HTTP 404*" }).Count -eq 0) { throw "not caught" }
    }
    Check "Forbidden-text scanner: does NOT flag a Unity build-progress counter [404/660]" {
        # Real text shape from this project's own build logs - a bracketed progress counter, not
        # an HTTP error. This is the exact false positive the fix corrects.
        $f = Find-ForbiddenTextMarkers -Text "[404/660] Importing 'GUID: 8ae8063bfa41044ddaa5c5c70943e142'"
        if (($f | Where-Object { $_ -like "*HTTP 404*" }).Count -gt 0) { throw "false positive on a build-progress counter: $($f -join '; ')" }
    }
    Check "Forbidden-text scanner: does NOT flag an unbracketed N/M progress counter either" {
        $f = Find-ForbiddenTextMarkers -Text "404/660 assets imported"
        if (($f | Where-Object { $_ -like "*HTTP 404*" }).Count -gt 0) { throw "false positive on an unbracketed progress counter: $($f -join '; ')" }
    }
    Check "Forbidden-text scanner: catches a real 404 immediately after a progress counter in the same log line" {
        # Guards against an overly broad fix that disables the whole line once any counter appears.
        $f = Find-ForbiddenTextMarkers -Text "[12/660] Cloud Code call to Telemetry/SendEvent returned 404"
        if (($f | Where-Object { $_ -like "*HTTP 404*" }).Count -eq 0) { throw "real 404 in the same line as a progress counter was not caught" }
    }
    Check "Forbidden-text scanner: catches 'Module could not be found' (no digits at all)" {
        $f = Find-ForbiddenTextMarkers -Text "[ServicesCore]: Module could not be found - Telemetry"
        if (($f | Where-Object { $_ -like "*module not found*" }).Count -eq 0) { throw "not caught" }
    }
    Check "Forbidden-text scanner: catches 'module not found' (case-insensitive, alternate phrasing)" {
        $f = Find-ForbiddenTextMarkers -Text "error: module not found: com.example.telemetry"
        if (($f | Where-Object { $_ -like "*module not found*" }).Count -eq 0) { throw "not caught" }
    }
    Check "Forbidden-text scanner: catches placeholder text" {
        $f = Find-ForbiddenTextMarkers -Text "This is placeholder copy pending ST review"
        if (($f | Where-Object { $_ -like "*placeholder*" }).Count -eq 0) { throw "not caught" }
    }
    Check "Forbidden-text scanner: catches Lorem ipsum" {
        $f = Find-ForbiddenTextMarkers -Text "Lorem ipsum dolor sit amet"
        if (($f | Where-Object { $_ -like "*lorem ipsum*" }).Count -eq 0) { throw "not caught" }
    }
    Check "Forbidden-text scanner: silent on clean text" {
        $f = Find-ForbiddenTextMarkers -Text "Empire background bound, raycastTarget=false, all clear"
        if ($f.Count -ne 0) { throw "false positive: $($f -join '; ')" }
    }

    # 6. Capture-provenance check: end-to-end against real fixture PNGs + sidecars.
    $provDir = Join-Path $tmp "provenance_fixture"
    New-Item -ItemType Directory -Force -Path $provDir | Out-Null
    $provPng = Join-Path $provDir "rc_Fixture_1920x1080.png"
    New-TestPng -Path $provPng -Width 1920 -Height 1080
    $realHash = Get-Sha256Hex -Path $provPng
    $goodSidecar = Join-Path $provDir "rc_Fixture_1920x1080.json"
    @{
        label = "rc_Fixture"; sourceHead = "1111111111111111111111111111111111111111"
        runtimeHash = "2222222222222222222222222222222222222222222222222222222222222222"
        pngSha256 = $realHash
    } | ConvertTo-Json | Set-Content -Path $goodSidecar -Encoding utf8
    Check "Capture provenance: passes on matching HEAD/hash/pngSha256/dimensions" {
        $set = Get-CaptureSet -CaptureDir $provDir
        if ($set.Captures.Count -ne 1) { throw "expected 1 capture, got $($set.Captures.Count)" }
        $result = Test-CaptureProvenance -Capture $set.Captures[0] `
            -ExpectedHead "1111111111111111111111111111111111111111" `
            -ExpectedRuntimeHash "2222222222222222222222222222222222222222222222222222222222222222"
        if ($result.Findings.Count -ne 0) { throw "unexpected findings: $($result.Findings -join '; ')" }
    }
    Check "Capture provenance: flags wrong HEAD" {
        $set = Get-CaptureSet -CaptureDir $provDir
        $result = Test-CaptureProvenance -Capture $set.Captures[0] `
            -ExpectedHead "9999999999999999999999999999999999999999" `
            -ExpectedRuntimeHash "2222222222222222222222222222222222222222222222222222222222222222"
        if (($result.Findings | Where-Object { $_ -like "*WRONG HEAD*" }).Count -eq 0) { throw "wrong HEAD not flagged" }
    }
    Check "Capture provenance: flags a sidecar hash that doesn't match the real file" {
        $tamperedDir = Join-Path $tmp "tampered_fixture"
        New-Item -ItemType Directory -Force -Path $tamperedDir | Out-Null
        $tPng = Join-Path $tamperedDir "rc_T_1920x1080.png"
        New-TestPng -Path $tPng -Width 1920 -Height 1080
        @{
            label = "rc_T"; sourceHead = "1111111111111111111111111111111111111111"
            runtimeHash = "2222222222222222222222222222222222222222222222222222222222222222"
            pngSha256 = "0000000000000000000000000000000000000000000000000000000000000000"
        } | ConvertTo-Json | Set-Content -Path (Join-Path $tamperedDir "rc_T_1920x1080.json") -Encoding utf8
        $set = Get-CaptureSet -CaptureDir $tamperedDir
        $result = Test-CaptureProvenance -Capture $set.Captures[0] `
            -ExpectedHead "1111111111111111111111111111111111111111" `
            -ExpectedRuntimeHash "2222222222222222222222222222222222222222222222222222222222222222"
        if (($result.Findings | Where-Object { $_ -like "*SIDECAR HASH MISMATCH*" }).Count -eq 0) { throw "hash mismatch not flagged" }
    }
    Check "Capture provenance: flags exe-only / missing provenance (no sourceHead+runtimeHash)" {
        $exeOnlyDir = Join-Path $tmp "exeonly_fixture"
        New-Item -ItemType Directory -Force -Path $exeOnlyDir | Out-Null
        $ePng = Join-Path $exeOnlyDir "rc_E_1920x1080.png"
        New-TestPng -Path $ePng -Width 1920 -Height 1080
        @{ label = "rc_E"; exeHash = "onlytheexehashwasprovided"; pngSha256 = (Get-Sha256Hex -Path $ePng) } |
            ConvertTo-Json | Set-Content -Path (Join-Path $exeOnlyDir "rc_E_1920x1080.json") -Encoding utf8
        $set = Get-CaptureSet -CaptureDir $exeOnlyDir
        $result = Test-CaptureProvenance -Capture $set.Captures[0] `
            -ExpectedHead "1111111111111111111111111111111111111111" `
            -ExpectedRuntimeHash "2222222222222222222222222222222222222222222222222222222222222222"
        if (($result.Findings | Where-Object { $_ -like "*MISSING PROVENANCE*" }).Count -eq 0) { throw "exe-only provenance not flagged" }
    }
    Check "Capture provenance: flags wrong dimensions" {
        $wrongDimDir = Join-Path $tmp "wrongdim_fixture"
        New-Item -ItemType Directory -Force -Path $wrongDimDir | Out-Null
        $wPng = Join-Path $wrongDimDir "rc_W_1707x1067.png"
        New-TestPng -Path $wPng -Width 1707 -Height 1067
        @{
            label = "rc_W"; sourceHead = "1111111111111111111111111111111111111111"
            runtimeHash = "2222222222222222222222222222222222222222222222222222222222222222"
            pngSha256 = (Get-Sha256Hex -Path $wPng)
        } | ConvertTo-Json | Set-Content -Path (Join-Path $wrongDimDir "rc_W_1707x1067.json") -Encoding utf8
        $set = Get-CaptureSet -CaptureDir $wrongDimDir
        $result = Test-CaptureProvenance -Capture $set.Captures[0] `
            -ExpectedHead "1111111111111111111111111111111111111111" `
            -ExpectedRuntimeHash "2222222222222222222222222222222222222222222222222222222222222222"
        if (($result.Findings | Where-Object { $_ -like "*WRONG DIMENSIONS*" }).Count -eq 0) { throw "wrong dimensions not flagged" }
    }
    Check "Capture set loader: flags a PNG with no sidecar" {
        $orphanDir = Join-Path $tmp "orphan_fixture"
        New-Item -ItemType Directory -Force -Path $orphanDir | Out-Null
        New-TestPng -Path (Join-Path $orphanDir "rc_Orphan_1920x1080.png") -Width 1920 -Height 1080
        $set = Get-CaptureSet -CaptureDir $orphanDir
        if (($set.Findings | Where-Object { $_ -like "*MISSING SIDECAR*" }).Count -eq 0) { throw "orphan PNG not flagged" }
    }

    # 7. Build-log / test-results checks (pure text/xml fixtures, no real Unity build needed).
    $cleanLog = Join-Path $tmp "clean_build.log"
    Set-Content -Path $cleanLog -Value @("Compiling...", "Build succeeded.") -Encoding utf8
    $dirtyLog = Join-Path $tmp "dirty_build.log"
    Set-Content -Path $dirtyLog -Value @("Assets\Foo.cs(1,1): error CS0103: bad", "done") -Encoding utf8
    Check "Build log check: clean log has zero findings" {
        $f = Test-BuildLog -Path $cleanLog
        if ($f.Count -ne 0) { throw "false positive: $($f -join '; ')" }
    }
    Check "Build log check: flags error CS" {
        $f = Test-BuildLog -Path $dirtyLog
        if (($f | Where-Object { $_ -like "*COMPILER ERRORS*" }).Count -eq 0) { throw "error CS not flagged" }
    }
    Check "Build log check: missing file is a finding, not a thrown exception" {
        $f = Test-BuildLog -Path (Join-Path $tmp "missing.log")
        if (($f | Where-Object { $_ -like "*MISSING build log*" }).Count -eq 0) { throw "missing log not flagged" }
    }
    Check "Build log check: a real Unity import log full of [N/M] progress counters, including [404/660], has zero findings" {
        $progressLog = Join-Path $tmp "progress_build.log"
        Set-Content -Path $progressLog -Value @(
            "[402/660] Importing 'GUID: 1234'",
            "[403/660] Importing 'GUID: 5678'",
            "[404/660] Importing 'GUID: 8ae8063bfa41044ddaa5c5c70943e142'",
            "[405/660] Importing 'GUID: 9012'",
            "Build succeeded."
        ) -Encoding utf8
        $f = Test-BuildLog -Path $progressLog
        if ($f.Count -ne 0) { throw "false positive on a real Unity progress-counter log: $($f -join '; ')" }
    }

    $goodXml = Join-Path $tmp "good_results.xml"
    Set-Content -Path $goodXml -Value '<?xml version="1.0"?><test-run testcasecount="5" passed="5" failed="0"></test-run>' -Encoding utf8
    $badXml = Join-Path $tmp "bad_results.xml"
    Set-Content -Path $badXml -Value '<?xml version="1.0"?><test-run testcasecount="5" passed="4" failed="1"></test-run>' -Encoding utf8
    Check "Test-results check: 5/5 passing has zero findings" {
        $r = Test-TestResultsXml -Path $goodXml
        if ($r.Findings.Count -ne 0) { throw "false positive: $($r.Findings -join '; ')" }
        if ($r.Total -ne 5 -or $r.Passed -ne 5 -or $r.Failed -ne 0) { throw "bad totals parsed" }
    }
    Check "Test-results check: flags a real failure" {
        $r = Test-TestResultsXml -Path $badXml
        if (($r.Findings | Where-Object { $_ -like "*TEST FAILURES*" }).Count -eq 0) { throw "failure not flagged" }
    }

    # 8. Frozen-tag resolution against a real throwaway git repo (fast, local, no network).
    $repoDir = Join-Path $tmp "repo_fixture"
    New-Item -ItemType Directory -Force -Path $repoDir | Out-Null
    & git -C $repoDir init -q 2>$null
    & git -C $repoDir config user.email "selftest@example.com" 2>$null
    & git -C $repoDir config user.name "selftest" 2>$null
    Set-Content -Path (Join-Path $repoDir "a.txt") -Value "a" -Encoding utf8
    & git -C $repoDir add a.txt 2>$null
    & git -C $repoDir commit -q -m "init" 2>$null
    $realRepoHead = (& git -C $repoDir rev-parse HEAD).Trim()
    & git -C $repoDir tag -a "frozen-fixture" -m "freeze" 2>$null
    Check "Exact-HEAD check: matches a real repo's real HEAD, zero dirt" {
        $r = Test-ExactHead -RepoPath $repoDir -ExpectedHead $realRepoHead
        if (-not $r.HeadMatches) { throw "HEAD did not match" }
        if ($r.DirtCount -ne 0) { throw "expected 0 dirt, got $($r.DirtCount)" }
    }
    Check "Exact-HEAD check: detects tracked dirt" {
        Set-Content -Path (Join-Path $repoDir "a.txt") -Value "changed" -Encoding utf8
        $r = Test-ExactHead -RepoPath $repoDir -ExpectedHead $realRepoHead
        if ($r.DirtCount -eq 0) { throw "dirt not detected" }
        & git -C $repoDir checkout -q -- a.txt
    }
    Check "Frozen-tag check: resolves to the expected commit" {
        $r = Test-FrozenTag -RepoPath $repoDir -Tag "frozen-fixture" -ExpectedHead $realRepoHead
        if (-not $r.TagExists -or -not $r.ResolvesToExpected) { throw "tag did not resolve as expected" }
    }
    Check "Frozen-tag check: flags a tag resolving to the wrong commit" {
        Set-Content -Path (Join-Path $repoDir "b.txt") -Value "b" -Encoding utf8
        & git -C $repoDir add b.txt 2>$null
        & git -C $repoDir commit -q -m "second" 2>$null
        $r = Test-FrozenTag -RepoPath $repoDir -Tag "frozen-fixture" -ExpectedHead (& git -C $repoDir rev-parse HEAD).Trim()
        if ($r.ResolvesToExpected) { throw "should not have matched" }
    }

    # PRODUCTIVE CODING TASK - dubious-ownership handling. GIT_TEST_ASSUME_DIFFERENT_OWNER is a
    # real, documented git environment variable (git's own test suite uses it) that forces git's
    # ownership check to treat the CURRENT process as NOT owning the repo directory, deterministically
    # reproducing "fatal: detected dubious ownership in repository" without any cross-user/admin
    # trickery. Sanity-checked directly against this repo below before trusting it in the two real
    # checks that follow - if the sanity check itself doesn't reproduce the failure, the two
    # "handles it" checks after it would be proving nothing.
    $ownDir = Join-Path $tmp "dubious_ownership_repo"
    New-Item -ItemType Directory -Force -Path $ownDir | Out-Null
    & git -C $ownDir init -q 2>$null
    & git -C $ownDir config user.email "selftest@example.com" 2>$null
    & git -C $ownDir config user.name "selftest" 2>$null
    Set-Content -Path (Join-Path $ownDir "a.txt") -Value "a" -Encoding utf8
    & git -C $ownDir add a.txt 2>$null
    & git -C $ownDir commit -q -m "init" 2>$null
    $ownHead = (& git -C $ownDir rev-parse HEAD).Trim()
    & git -C $ownDir tag -a "dubious-fixture" -m "freeze" 2>$null

    Check "Sanity check: GIT_TEST_ASSUME_DIFFERENT_OWNER really does reproduce dubious ownership (proves the two checks below test something real)" {
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "SilentlyContinue"
        $env:GIT_TEST_ASSUME_DIFFERENT_OWNER = "1"
        & git -C $ownDir rev-parse HEAD 1>$null 2>$null
        $code = $LASTEXITCODE
        Remove-Item Env:\GIT_TEST_ASSUME_DIFFERENT_OWNER -ErrorAction SilentlyContinue
        $ErrorActionPreference = $prevEap
        if ($code -eq 0) { throw "GIT_TEST_ASSUME_DIFFERENT_OWNER did not reproduce dubious ownership on this git version - the two checks below would be meaningless" }
    }
    Check "Test-ExactHead: accepts a valid candidate despite dubious ownership (safe.directory override)" {
        $env:GIT_TEST_ASSUME_DIFFERENT_OWNER = "1"
        try {
            $r = Test-ExactHead -RepoPath $ownDir -ExpectedHead $ownHead
        }
        finally {
            Remove-Item Env:\GIT_TEST_ASSUME_DIFFERENT_OWNER -ErrorAction SilentlyContinue
        }
        if (-not $r.HeadMatches) { throw "expected HeadMatches=true, got ActualHead='$($r.ActualHead)' (a git dubious-ownership error message landing here instead of a real SHA is exactly the bug this override fixes)" }
        if ($r.DirtCount -ne 0) { throw "expected 0 dirt, got $($r.DirtCount)" }
    }
    Check "Test-FrozenTag: resolves correctly despite dubious ownership (safe.directory override)" {
        $env:GIT_TEST_ASSUME_DIFFERENT_OWNER = "1"
        try {
            $r = Test-FrozenTag -RepoPath $ownDir -Tag "dubious-fixture" -ExpectedHead $ownHead
        }
        finally {
            Remove-Item Env:\GIT_TEST_ASSUME_DIFFERENT_OWNER -ErrorAction SilentlyContinue
        }
        if (-not $r.TagExists -or -not $r.ResolvesToExpected) { throw "tag did not resolve as expected under dubious ownership" }
    }

    # 9. GATE MODE end-to-end (PRODUCTIVE CODING TASK): actually invokes this script as a real
    # subprocess (-File $PSCommandPath), exactly the way tools/capture_native_window.ps1 calls it,
    # against a fresh dedicated fixture repo - proves the gate wiring itself (not just the
    # individual guard functions above) refuses on every real acceptance failure and accepts a
    # genuinely valid candidate, using real exit codes.
    $gateRepoDir = Join-Path $tmp "gate_repo_fixture"
    New-Item -ItemType Directory -Force -Path $gateRepoDir | Out-Null
    & git -C $gateRepoDir init -q 2>$null
    & git -C $gateRepoDir config user.email "selftest@example.com" 2>$null
    & git -C $gateRepoDir config user.name "selftest" 2>$null
    $dllRelPath = "Builds\Windows64\MyriadOfDragons_Data\Managed\MyriadOfDragons.Runtime.dll"
    $dllFullPath = Join-Path $gateRepoDir $dllRelPath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dllFullPath) | Out-Null
    Set-Content -Path $dllFullPath -Value "fake runtime dll contents" -Encoding utf8
    Set-Content -Path (Join-Path $gateRepoDir "a.txt") -Value "a" -Encoding utf8
    & git -C $gateRepoDir add -A 2>$null
    & git -C $gateRepoDir commit -q -m "gate fixture commit" 2>$null
    $gateHead = (& git -C $gateRepoDir rev-parse HEAD).Trim()
    & git -C $gateRepoDir tag -a "gate-fixture-frozen" -m "freeze" 2>$null
    $gateDllHash = Get-Sha256Hex -Path $dllFullPath

    $gateCleanLog = Join-Path $tmp "gate_clean_build.log"
    Set-Content -Path $gateCleanLog -Value @("Compiling...", "Build succeeded.") -Encoding utf8
    $gateDirtyLog = Join-Path $tmp "gate_dirty_build.log"
    Set-Content -Path $gateDirtyLog -Value @("Assets\Foo.cs(1,1): error CS0103: bad", "done") -Encoding utf8
    $gateGoodXml = Join-Path $tmp "gate_good_results.xml"
    Set-Content -Path $gateGoodXml -Value '<?xml version="1.0"?><test-run testcasecount="5" passed="5" failed="0"></test-run>' -Encoding utf8
    $gateBadXml = Join-Path $tmp "gate_bad_results.xml"
    Set-Content -Path $gateBadXml -Value '<?xml version="1.0"?><test-run testcasecount="5" passed="4" failed="1"></test-run>' -Encoding utf8

    function Invoke-GateScript {
        param(
            [string]$Head = $gateHead,
            [string]$RuntimeHash = $gateDllHash,
            [string]$Tag = "gate-fixture-frozen",
            [string]$BuildLog = $gateCleanLog,
            [string]$TestXml = $gateGoodXml
        )
        # Same non-zero-exit-under-Stop-preference quirk as Test-FrozenTag above - a child
        # PowerShell process that exits non-zero (every REFUSES case here does, by design) can
        # throw here too unless $ErrorActionPreference is relaxed for the call itself.
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "SilentlyContinue"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
            -CandidateRoot $gateRepoDir -ExpectedHead $Head -ExpectedRuntimeHash $RuntimeHash `
            -ExpectedTag $Tag -BuildLog $BuildLog -TestResultsXml $TestXml 1>$null 2>$null
        $code = $LASTEXITCODE
        $ErrorActionPreference = $prevEap
        return $code
    }

    Check "Gate mode end-to-end: ACCEPTS a fully valid candidate (exit 0)" {
        $code = Invoke-GateScript
        if ($code -ne 0) { throw "expected exit 0, got $code" }
    }
    Check "Gate mode end-to-end: ACCEPTS a valid candidate whose repo would otherwise trigger dubious-ownership (exit 0)" {
        # This is the task's own required proof, run through the REAL CLI entry point (a fresh
        # child process, exactly how a human or capture_native_window.ps1 would invoke this
        # script) rather than just the unit-level Test-ExactHead/Test-FrozenTag checks above -
        # end-to-end confirmation that the whole gate, not just its two helper functions, honors
        # the safe.directory override. GIT_TEST_ASSUME_DIFFERENT_OWNER is inherited by the child
        # PowerShell process the same way any environment variable is.
        $env:GIT_TEST_ASSUME_DIFFERENT_OWNER = "1"
        try {
            $code = Invoke-GateScript
        }
        finally {
            Remove-Item Env:\GIT_TEST_ASSUME_DIFFERENT_OWNER -ErrorAction SilentlyContinue
        }
        if ($code -ne 0) { throw "expected exit 0 despite dubious ownership, got $code" }
    }
    Check "Gate mode end-to-end: REFUSES wrong HEAD (exit 1)" {
        $code = Invoke-GateScript -Head ("9" * 40)
        if ($code -ne 1) { throw "expected exit 1, got $code" }
    }
    Check "Gate mode end-to-end: REFUSES tracked dirt (exit 1)" {
        Set-Content -Path (Join-Path $gateRepoDir "a.txt") -Value "changed" -Encoding utf8
        $code = Invoke-GateScript
        & git -C $gateRepoDir checkout -q -- a.txt
        if ($code -ne 1) { throw "expected exit 1, got $code" }
    }
    Check "Gate mode end-to-end: REFUSES a wrong/missing frozen tag (exit 1)" {
        $code = Invoke-GateScript -Tag "no-such-tag"
        if ($code -ne 1) { throw "expected exit 1, got $code" }
    }
    Check "Gate mode end-to-end: REFUSES wrong Runtime.dll hash (exit 1)" {
        $code = Invoke-GateScript -RuntimeHash ("0" * 64)
        if ($code -ne 1) { throw "expected exit 1, got $code" }
    }
    Check "Gate mode end-to-end: REFUSES a dirty build log (error CS, exit 1)" {
        $code = Invoke-GateScript -BuildLog $gateDirtyLog
        if ($code -ne 1) { throw "expected exit 1, got $code" }
    }
    Check "Gate mode end-to-end: REFUSES failing test results (exit 1)" {
        $code = Invoke-GateScript -TestXml $gateBadXml
        if ($code -ne 1) { throw "expected exit 1, got $code" }
    }
    Check "Gate mode end-to-end: REFUSES (tool error, exit 2) when BuildLog/TestResultsXml are omitted" {
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "SilentlyContinue"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
            -CandidateRoot $gateRepoDir -ExpectedHead $gateHead -ExpectedRuntimeHash $gateDllHash `
            -ExpectedTag "gate-fixture-frozen" 1>$null 2>$null
        $code = $LASTEXITCODE
        $ErrorActionPreference = $prevEap
        if ($code -ne 2) { throw "expected exit 2, got $code" }
    }

    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host ""
    if ($fails -eq 0) { Write-Host "SELFTEST: all guard checks passed."; exit 0 }
    else { Write-Host "SELFTEST: $fails guard check(s) FAILED."; exit 1 }
}

# ---------------------------------------------------------------------------
# Validate mode: run every check against a real candidate.
# ---------------------------------------------------------------------------

$allFindings = New-Object System.Collections.Generic.List[string]
$passNotes = New-Object System.Collections.Generic.List[string]

if (-not (Test-Path -LiteralPath $CandidateRoot)) {
    # -ErrorAction Continue is required here: this script sets $ErrorActionPreference = "Stop" at
    # the top, so a bare Write-Error would itself become a terminating error and the `exit 2` on
    # the next line would never run - PowerShell's own exit code for an unhandled terminating
    # error under `-File` is 1, silently swallowing the intended "tool could not run" (2) signal.
    # Confirmed as a real, reproducible bug while adding this function's own end-to-end self-test
    # (the omitted-evidence case below returned exit 1, not 2, until this was fixed).
    Write-Error "CandidateRoot does not exist: $CandidateRoot" -ErrorAction Continue
    exit 2
}
$gateOnly = ($CaptureDir -eq "")
if (-not $gateOnly -and -not (Test-Path -LiteralPath $CaptureDir)) {
    Write-Error "CaptureDir does not exist: $CaptureDir" -ErrorAction Continue
    exit 2
}
# Gate mode (no CaptureDir) exists specifically to run BEFORE capture, so the two pieces of
# evidence that can only come from BEFORE this run (a clean compile and a passing test suite)
# must be supplied and real - without this, gate mode would rubber-stamp a candidate that never
# actually built or tested cleanly, defeating "required test/compiler evidence exists".
if ($gateOnly -and ($BuildLog -eq "" -or $TestResultsXml -eq "")) {
    Write-Error "Gate mode (no -CaptureDir) requires both -BuildLog and -TestResultsXml - required compiler/test evidence must exist before a candidate can be gated." -ErrorAction Continue
    exit 2
}

# 1. Exact HEAD + tracked dirt.
$headResult = Test-ExactHead -RepoPath $CandidateRoot -ExpectedHead $ExpectedHead
if (-not $headResult.HeadMatches) {
    $allFindings.Add("HEAD MISMATCH: expected $ExpectedHead, candidate is at $($headResult.ActualHead)")
}
else { $passNotes.Add("HEAD matches: $ExpectedHead") }
if ($headResult.DirtCount -ne 0) {
    $allFindings.Add("TRACKED DIRT: $($headResult.DirtCount) tracked file(s) modified in $CandidateRoot")
}
else { $passNotes.Add("Tracked dirt: 0") }

# 2. Frozen/tagged candidate (only if a tag was supplied).
if ($ExpectedTag -ne "") {
    $tagResult = Test-FrozenTag -RepoPath $CandidateRoot -Tag $ExpectedTag -ExpectedHead $ExpectedHead
    if (-not $tagResult.TagExists) {
        $allFindings.Add("MISSING FROZEN TAG: $ExpectedTag does not exist in $CandidateRoot")
    }
    elseif (-not $tagResult.ResolvesToExpected) {
        $allFindings.Add("TAG MISMATCH: $ExpectedTag resolves to $($tagResult.Resolved), expected $ExpectedHead")
    }
    else { $passNotes.Add("Frozen tag $ExpectedTag resolves to $ExpectedHead") }
}
else {
    $allFindings.Add("NO FROZEN TAG SUPPLIED: a release candidate must be validated against an immutable tag, not a bare HEAD")
}

# 3. Runtime.dll hash.
$dllPath = Join-Path $CandidateRoot $RuntimeDllRelativePath
if (-not (Test-Path -LiteralPath $dllPath)) {
    $allFindings.Add("MISSING Runtime.dll at $dllPath")
}
else {
    $actualDllHash = Get-Sha256Hex -Path $dllPath
    if ($actualDllHash -ne $ExpectedRuntimeHash.ToLowerInvariant()) {
        $allFindings.Add("RUNTIME.DLL HASH MISMATCH: expected $ExpectedRuntimeHash, actual $actualDllHash")
    }
    else { $passNotes.Add("Runtime.dll hash matches: $ExpectedRuntimeHash") }
}

# 4-7. Captures: provenance, dimensions, duplicate pixels, required screens. Skipped entirely in
# gate mode (no CaptureDir) - there is nothing to check yet, that's the point of gating BEFORE
# capture runs.
if (-not $gateOnly) {
    $captureSet = Get-CaptureSet -CaptureDir $CaptureDir
    foreach ($f in $captureSet.Findings) { $allFindings.Add($f) }

    $hashesByName = @{}
    foreach ($capture in $captureSet.Captures) {
        $result = Test-CaptureProvenance -Capture $capture -ExpectedHead $ExpectedHead -ExpectedRuntimeHash $ExpectedRuntimeHash
        if ($result.Findings.Count -eq 0) { $passNotes.Add("Capture OK: $($result.Name)") }
        foreach ($f in $result.Findings) { $allFindings.Add($f) }
        $hashesByName[$result.Name] = $result.ActualPngHash

        if ($capture.Sidecar.PSObject.Properties.Name -contains 'note') {
            foreach ($f in (Find-ForbiddenTextMarkers -Text ([string]$capture.Sidecar.note) -SourceName "$($result.Name) sidecar note")) {
                $allFindings.Add($f)
            }
        }
    }

    $knownStale = @()
    if ($KnownStaleHashesFile -ne "" -and (Test-Path -LiteralPath $KnownStaleHashesFile)) {
        $knownStale = @(Get-Content -LiteralPath $KnownStaleHashesFile | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ -ne "" })
    }
    foreach ($f in (Find-DuplicatePixelHashes -HashesByName $hashesByName -KnownStaleHashes $knownStale)) { $allFindings.Add($f) }

    if ($RequiredScreens.Count -gt 0) {
        $missingScreens = Find-MissingRequiredScreens -RequiredScreens $RequiredScreens -Captures $captureSet.Captures
        foreach ($m in $missingScreens) { $allFindings.Add("MISSING REQUIRED SCREEN: $m") }
        if ($missingScreens.Count -eq 0) { $passNotes.Add("All $($RequiredScreens.Count) required screens present") }
    }
}
else {
    $passNotes.Add("GATE MODE: capture-set checks (provenance/dimensions/duplicates/required screens) skipped - no CaptureDir yet")
}

# 8. Build log: compiler errors + forbidden text.
if ($BuildLog -ne "") {
    foreach ($f in (Test-BuildLog -Path $BuildLog)) { $allFindings.Add($f) }
    if (($allFindings | Where-Object { $_ -like "*$BuildLog*" -or $_ -like "*COMPILER ERRORS*" }).Count -eq 0) {
        $passNotes.Add("Build log clean: $BuildLog")
    }
}

# 9. Test results: totals + failures.
if ($TestResultsXml -ne "") {
    $testResult = Test-TestResultsXml -Path $TestResultsXml
    foreach ($f in $testResult.Findings) { $allFindings.Add($f) }
    if ($testResult.Findings.Count -eq 0) {
        $passNotes.Add("Tests: $($testResult.Passed)/$($testResult.Total) passed, 0 failed")
    }
}

Write-Output "release-candidate acceptance validator$(if ($gateOnly) { ' (GATE MODE - pre-capture)' })"
Write-Output "CandidateRoot: $CandidateRoot"
Write-Output "CaptureDir:    $(if ($gateOnly) { '(none - gate mode)' } else { $CaptureDir })"
Write-Output ""
Write-Output "PASS notes ($($passNotes.Count)):"
foreach ($p in $passNotes) { Write-Output "  PASS $p" }
Write-Output ""
Write-Output "FAIL findings ($($allFindings.Count)):"
foreach ($f in $allFindings) { Write-Output "  FAIL $f" }

if ($allFindings.Count -gt 0) { exit 1 }
Write-Output ""
Write-Output "RESULT: ACCEPT"
exit 0
