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

    [Parameter(ParameterSetName = "Validate", Mandatory = $true)]
    [string]$CaptureDir,

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

# Exact HEAD + tracked-dirt check against a git worktree. Returns a result object rather than
# throwing, so callers can accumulate multiple findings instead of stopping at the first one.
function Test-ExactHead {
    param(
        [Parameter(Mandatory)][string]$RepoPath,
        [Parameter(Mandatory)][string]$ExpectedHead
    )
    $actualHead = (& git -C $RepoPath rev-parse HEAD 2>$null).Trim()
    $dirtLines = @(& git -C $RepoPath status --porcelain --untracked-files=no 2>$null)
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
    $resolved = (& git -C $RepoPath rev-parse "$Tag^{commit}" 2>$null).Trim()
    if ([string]::IsNullOrEmpty($resolved)) {
        return [pscustomobject]@{ TagExists = $false; ResolvesToExpected = $false; Resolved = "" }
    }
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
function Find-ForbiddenTextMarkers {
    param([Parameter(Mandatory)][string]$Text, [string]$SourceName = "")
    $patterns = @(
        @{ Name = "NullReferenceException"; Pattern = 'NullReferenceException' },
        @{ Name = "unhandled exception";    Pattern = '(?i)unhandled exception' },
        @{ Name = "HTTP 404";               Pattern = '(?<![0-9])404(?![0-9])' },
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
    Check "Forbidden-text scanner: catches a 404" {
        $f = Find-ForbiddenTextMarkers -Text "GET /api/foo returned 404"
        if (($f | Where-Object { $_ -like "*404*" }).Count -eq 0) { throw "not caught" }
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
    Write-Error "CandidateRoot does not exist: $CandidateRoot"
    exit 2
}
if (-not (Test-Path -LiteralPath $CaptureDir)) {
    Write-Error "CaptureDir does not exist: $CaptureDir"
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

# 4-7. Captures: provenance, dimensions, duplicate pixels, required screens.
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

Write-Output "release-candidate acceptance validator"
Write-Output "CandidateRoot: $CandidateRoot"
Write-Output "CaptureDir:    $CaptureDir"
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
