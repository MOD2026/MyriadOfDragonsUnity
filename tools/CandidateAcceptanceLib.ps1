# Shared library of release-candidate acceptance-gate guard functions.
#
# Pure function definitions only - no top-level executable script logic, no `exit` calls - so this
# file is safe to dot-source from any caller (a report generator, a future test harness) without the
# caller's process being terminated early.
#
# This mirrors the check semantics of tools/validate_release_candidate.ps1 as of the commit this
# file was added in (that script is under active concurrent editing in this shared tree - gate mode,
# error-handling fixes - so this lib intentionally does NOT refactor or replace it; it exists so
# tools/generate_candidate_acceptance_report.ps1 has the same checks available as structured data
# without re-implementing them or shelling out to parse validate_release_candidate.ps1's text
# output). If validate_release_candidate.ps1's checks change meaningfully, re-sync this file's
# equivalents by hand rather than assuming they stay identical forever.
#
# Dot-source this file, then call the functions directly:
#   . "$PSScriptRoot/CandidateAcceptanceLib.ps1"
#   $result = Invoke-CandidateAcceptanceCheck -CandidateRoot ... -ExpectedHead ... -ExpectedRuntimeHash ... -CaptureDir ...

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

# Exact HEAD + tracked-dirt check against a git worktree.
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
# A nonexistent tag is expected, normal input (the "missing frozen tag" case) - git exits non-zero
# for it, and under a caller's $ErrorActionPreference = "Stop" that non-zero exit can still throw
# even with stderr redirected, so the exit code is checked explicitly rather than relying on
# redirection alone to keep this non-fatal.
function Test-FrozenTag {
    param(
        [Parameter(Mandatory)][string]$RepoPath,
        [Parameter(Mandatory)][string]$Tag,
        [Parameter(Mandatory)][string]$ExpectedHead
    )
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    $resolved = (& git -C $RepoPath rev-parse "$Tag^{commit}" 2>$null)
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
            Png          = $png
            Sidecar      = $sidecar
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
    $width = $null
    $height = $null

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

    $matches1920x1080 = $false
    try {
        $dims = Get-PngDimensions -Path $Capture.Png.FullName
        $width = $dims.Width
        $height = $dims.Height
        $matches1920x1080 = ($dims.Width -eq 1920 -and $dims.Height -eq 1080)
        if (-not $matches1920x1080) {
            $findings.Add("WRONG DIMENSIONS in $name`: expected 1920x1080, got $($dims.Width)x$($dims.Height)")
        }
    }
    catch {
        $findings.Add("INVALID PNG $name`: $($_.Exception.Message)")
    }

    $label = if ($sidecar.PSObject.Properties.Name -contains 'label') { [string]$sidecar.label } else { "" }

    return [pscustomobject]@{
        Name             = $name
        Label            = $label
        ActualPngHash    = $actualPngHash
        Width            = $width
        Height           = $height
        Matches1920x1080 = $matches1920x1080
        Findings         = $findings
        Passed           = ($findings.Count -eq 0)
    }
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
        # Not Mandatory: PowerShell refuses to bind an empty array to a Mandatory array parameter
        # ("Cannot bind argument... because it is an empty array") - a real candidate with zero
        # captures is a legitimate, common case (this is exactly how it should be reported: every
        # required screen missing), so this must not throw on that input.
        [object[]]$Captures = @()
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
        return [pscustomobject]@{ Findings = $findings; ErrorCsCount = $null }
    }
    $text = Get-Content -LiteralPath $Path -Raw
    $errorCsCount = ([regex]::Matches($text, '(?im)error CS\d*')).Count
    if ($errorCsCount -gt 0) {
        $findings.Add("COMPILER ERRORS: $errorCsCount 'error CS' occurrence(s) in $Path")
    }
    foreach ($f in (Find-ForbiddenTextMarkers -Text $text -SourceName (Split-Path -Leaf $Path))) {
        $findings.Add($f)
    }
    return [pscustomobject]@{ Findings = $findings; ErrorCsCount = $errorCsCount }
}

function Test-TestResultsXml {
    param([Parameter(Mandatory)][string]$Path)
    $findings = New-Object System.Collections.Generic.List[string]
    $empty = { [pscustomobject]@{ Findings = $findings; Total = $null; Passed = $null; Failed = $null; Skipped = $null } }
    if (-not (Test-Path -LiteralPath $Path)) {
        $findings.Add("MISSING test results XML: $Path")
        return (& $empty)
    }
    try {
        [xml]$xml = Get-Content -LiteralPath $Path -Raw
    }
    catch {
        $findings.Add("MALFORMED test results XML: $Path ($($_.Exception.Message))")
        return (& $empty)
    }
    $run = $xml.'test-run'
    if ($null -eq $run) {
        $findings.Add("MISSING <test-run> root element in $Path")
        return (& $empty)
    }
    $total = [int]$run.testcasecount
    $passed = [int]$run.passed
    $failed = [int]$run.failed
    # 'skipped' is an optional attribute in some NUnit-style results files - absent means 0, not
    # unknown, so this is never left $null the way a missing/malformed file is above.
    $skipped = if ($run.PSObject.Properties.Name -contains 'skipped' -and $run.skipped) { [int]$run.skipped } else { 0 }
    if ($total -eq 0) {
        $findings.Add("ZERO TESTS in $Path - not a passing run")
    }
    if ($failed -ne 0) {
        $findings.Add("TEST FAILURES: $failed of $total failed in $Path")
    }
    return [pscustomobject]@{ Findings = $findings; Total = $total; Passed = $passed; Failed = $failed; Skipped = $skipped }
}

# ---------------------------------------------------------------------------
# Invoke-CandidateAcceptanceCheck: orchestrates every guard above into one structured result
# object. Never calls `exit` and never prints - callers (a report generator, a CLI) decide how to
# present the result. Mirrors validate_release_candidate.ps1's Validate-mode logic, including its
# gate-mode support (CaptureDir omitted = pre-capture check only: HEAD, dirt, frozen tag,
# Runtime.dll hash, build log, test results - no capture-set checks, since nothing exists yet).
# ---------------------------------------------------------------------------
function Invoke-CandidateAcceptanceCheck {
    param(
        [Parameter(Mandatory)][string]$CandidateRoot,
        [Parameter(Mandatory)][string]$ExpectedHead,
        [Parameter(Mandatory)][string]$ExpectedRuntimeHash,
        [string]$RuntimeDllRelativePath = "Builds\Windows64\MyriadOfDragons_Data\Managed\MyriadOfDragons.Runtime.dll",
        [string]$ExpectedTag = "",
        [string]$CaptureDir = "",
        [string[]]$RequiredScreens = @(),
        [string]$BuildLog = "",
        [string]$TestResultsXml = "",
        [string]$KnownStaleHashesFile = ""
    )

    # Same `powershell -File` comma-joining gotcha as -TestFilters in tools/run_editmode_tests.ps1 -
    # defend here too, since this function is the shared entry point every caller goes through.
    if ($RequiredScreens.Count -gt 0) {
        $RequiredScreens = @($RequiredScreens | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
    }

    $allFindings = New-Object System.Collections.Generic.List[string]
    $passNotes = New-Object System.Collections.Generic.List[string]

    if (-not (Test-Path -LiteralPath $CandidateRoot)) { throw "CandidateRoot does not exist: $CandidateRoot" }
    $gateOnly = ($CaptureDir -eq "")
    if (-not $gateOnly -and -not (Test-Path -LiteralPath $CaptureDir)) { throw "CaptureDir does not exist: $CaptureDir" }
    if ($gateOnly -and ($BuildLog -eq "" -or $TestResultsXml -eq "")) {
        throw "Gate mode (no CaptureDir) requires both BuildLog and TestResultsXml - required compiler/test evidence must exist before a candidate can be gated."
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

    # 2. Frozen/tagged candidate.
    $tagInfo = [pscustomobject]@{ Tag = $ExpectedTag; Supplied = ($ExpectedTag -ne ""); Exists = $false; ResolvesToExpected = $false; Resolved = "" }
    if ($ExpectedTag -ne "") {
        $tagResult = Test-FrozenTag -RepoPath $CandidateRoot -Tag $ExpectedTag -ExpectedHead $ExpectedHead
        $tagInfo.Exists = $tagResult.TagExists
        $tagInfo.ResolvesToExpected = $tagResult.ResolvesToExpected
        $tagInfo.Resolved = $tagResult.Resolved
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
    $buildHashInfo = [pscustomobject]@{
        RelativePath = $RuntimeDllRelativePath; Expected = $ExpectedRuntimeHash.ToLowerInvariant()
        Actual = $null; Exists = (Test-Path -LiteralPath $dllPath); Matches = $false
    }
    if (-not $buildHashInfo.Exists) {
        $allFindings.Add("MISSING Runtime.dll at $dllPath")
    }
    else {
        $actualDllHash = Get-Sha256Hex -Path $dllPath
        $buildHashInfo.Actual = $actualDllHash
        $buildHashInfo.Matches = ($actualDllHash -eq $ExpectedRuntimeHash.ToLowerInvariant())
        if (-not $buildHashInfo.Matches) {
            $allFindings.Add("RUNTIME.DLL HASH MISMATCH: expected $ExpectedRuntimeHash, actual $actualDllHash")
        }
        else { $passNotes.Add("Runtime.dll hash matches: $ExpectedRuntimeHash") }
    }

    # 4-7. Captures: provenance, dimensions, duplicate pixels, required screens. Skipped in gate
    # mode - there is nothing to check yet.
    $captureResults = New-Object System.Collections.Generic.List[object]
    $missingScreens = @()
    $duplicateFindings = @()
    if (-not $gateOnly) {
        $captureSet = Get-CaptureSet -CaptureDir $CaptureDir
        foreach ($f in $captureSet.Findings) { $allFindings.Add($f) }

        $hashesByName = @{}
        foreach ($capture in $captureSet.Captures) {
            $result = Test-CaptureProvenance -Capture $capture -ExpectedHead $ExpectedHead -ExpectedRuntimeHash $ExpectedRuntimeHash
            $captureResults.Add($result)
            if ($result.Passed) { $passNotes.Add("Capture OK: $($result.Name)") }
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
        # Find-DuplicatePixelHashes/Find-MissingRequiredScreens already return a single
        # non-unrolled List via the leading-comma operator - wrapping the call again with @()
        # here would nest that List as ONE element of a new array instead of exposing its
        # contents (a real bug caught by this file's own report-generator self-test).
        $duplicateFindings = Find-DuplicatePixelHashes -HashesByName $hashesByName -KnownStaleHashes $knownStale
        foreach ($f in $duplicateFindings) { $allFindings.Add($f) }

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
    $buildLogInfo = [pscustomobject]@{ Path = $BuildLog; Supplied = ($BuildLog -ne ""); ErrorCsCount = $null; Findings = @() }
    if ($BuildLog -ne "") {
        $blResult = Test-BuildLog -Path $BuildLog
        $buildLogInfo.ErrorCsCount = $blResult.ErrorCsCount
        $buildLogInfo.Findings = @($blResult.Findings)
        foreach ($f in $blResult.Findings) { $allFindings.Add($f) }
        if ($blResult.Findings.Count -eq 0) { $passNotes.Add("Build log clean: $BuildLog") }
    }

    # 9. Test results: totals + failures.
    $testResultsInfo = [pscustomobject]@{ Path = $TestResultsXml; Supplied = ($TestResultsXml -ne ""); Total = $null; Passed = $null; Failed = $null; Skipped = $null; Findings = @() }
    if ($TestResultsXml -ne "") {
        $trResult = Test-TestResultsXml -Path $TestResultsXml
        $testResultsInfo.Total = $trResult.Total
        $testResultsInfo.Passed = $trResult.Passed
        $testResultsInfo.Failed = $trResult.Failed
        $testResultsInfo.Skipped = $trResult.Skipped
        $testResultsInfo.Findings = @($trResult.Findings)
        foreach ($f in $trResult.Findings) { $allFindings.Add($f) }
        if ($trResult.Findings.Count -eq 0) { $passNotes.Add("Tests: $($trResult.Passed)/$($trResult.Total) passed, 0 failed") }
    }

    return [pscustomobject]@{
        GeneratedUtc    = (Get-Date).ToUniversalTime().ToString("o")
        CandidateRoot   = $CandidateRoot
        CaptureDir      = $CaptureDir
        GateOnly        = $gateOnly
        Identity        = [pscustomobject]@{
            ExpectedHead = $ExpectedHead.ToLowerInvariant()
            ActualHead   = $headResult.ActualHead
            HeadMatches  = $headResult.HeadMatches
            DirtCount    = $headResult.DirtCount
            Tag          = $tagInfo
        }
        BuildHash       = $buildHashInfo
        Captures        = $captureResults
        RequiredScreens = [pscustomobject]@{ Required = $RequiredScreens; Missing = $missingScreens }
        Duplicates      = $duplicateFindings
        BuildLog        = $buildLogInfo
        TestResults     = $testResultsInfo
        AllFindings     = @($allFindings)
        PassNotes       = @($passNotes)
        Decision        = if ($allFindings.Count -eq 0) { "PASS" } else { "FAIL" }
    }
}
