[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PackageRoot,

    [string]$EvidenceFile = "UI_RUNTIME_CAPTURE_002_EVIDENCE.txt",

    [string]$ConsoleLog = "Logs/UI_RUNTIME_CAPTURE_002_Console.log"
)

$ErrorActionPreference = "Stop"

$requiredScreenCaptures = @(
    "vip_V1_not_subscribed_1920x1080.png",
    "vip_V2_active_monthly_1920x1080.png",
    "vip_V3_back_plate_1920x1080.png",
    "collection_C1_populated_1920x1080.png",
    "collection_C2_mixed_1920x1080.png",
    "collection_C3_detail_v1_1920x1080.png",
    "collection_C4_detail_legacy_1920x1080.png",
    "collection_C5_filtered_empty_1920x1080.png",
    "collection_C6_no_owned_1920x1080.png",
    "collection_C7_scroll_bottom_1920x1080.png",
    "collection_C8_filter_warrior_1920x1080.png",
    "collection_C9_sort_rarity_1920x1080.png",
    "collection_C9_sort_name_1920x1080.png"
)

$requiredNativeCaptures = @(
    "vip_V4_atlas_cell_0.png",
    "vip_V4_atlas_cell_1.png",
    "vip_V4_atlas_cell_2.png",
    "vip_V4_atlas_cell_3.png",
    "vip_V4_atlas_cell_4.png",
    "vip_V4_atlas_cell_5.png",
    "vip_V4_atlas_cell_6.png",
    "vip_V4_atlas_cell_7.png"
)

$requiredCaptures = @($requiredScreenCaptures + $requiredNativeCaptures)

$failures = New-Object System.Collections.Generic.List[string]
$passes = New-Object System.Collections.Generic.List[string]

function Add-Failure([string]$message) { $script:failures.Add($message) }
function Add-Pass([string]$message) { $script:passes.Add($message) }

function Find-UniqueFile([string]$leafName) {
    $matches = @(Get-ChildItem -LiteralPath $PackageRoot -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ceq $leafName })
    if ($matches.Count -eq 0) {
        Add-Failure "MISSING file: $leafName"
        return $null
    }
    if ($matches.Count -gt 1) {
        Add-Failure "AMBIGUOUS file ($($matches.Count) matches): $leafName"
        return $null
    }
    return $matches[0]
}

function Get-PngDimensions([string]$path) {
    if ((Get-Item -LiteralPath $path).Length -eq 0) { throw "file is empty" }
    $stream = [System.IO.File]::OpenRead($path)
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
        return @{ Width = [int]$width; Height = [int]$height }
    }
    finally {
        $stream.Dispose()
    }
}

function Read-KeyValueEvidence([string]$path) {
    $values = @{}
    $lineNumber = 0
    foreach ($rawLine in Get-Content -LiteralPath $path) {
        $lineNumber++
        $line = $rawLine.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith("#")) { continue }
        $separator = $line.IndexOf("=")
        if ($separator -lt 1) {
            Add-Failure "MALFORMED evidence line ${lineNumber}: $rawLine"
            continue
        }
        $key = $line.Substring(0, $separator).Trim()
        $value = $line.Substring($separator + 1).Trim()
        if ($key.Length -eq 0) {
            Add-Failure "EMPTY evidence key on line ${lineNumber}"
            continue
        }
        if ($value.Length -eq 0) {
            Add-Failure "EMPTY evidence value on line ${lineNumber}: $key"
            continue
        }
        if ($values.ContainsKey($key)) {
            Add-Failure "DUPLICATE evidence key on line ${lineNumber}: $key"
            continue
        }
        $values[$key] = $value
    }
    return $values
}

function Require-Evidence([hashtable]$values, [string]$key, [string]$pattern) {
    if (-not $values.ContainsKey($key)) {
        Add-Failure "MISSING evidence key: $key"
        return $null
    }
    $value = [string]$values[$key]
    if ($value -notmatch $pattern) {
        Add-Failure "MALFORMED evidence value: $key=$value"
        return $null
    }
    return $value
}

function Resolve-FitFile([System.IO.FileInfo]$png) {
    $candidates = New-Object System.Collections.Generic.List[string]
    $candidates.Add((Join-Path $png.DirectoryName ($png.BaseName + "_fit.txt")))
    if ($png.BaseName.EndsWith("_1920x1080")) {
        $shortBase = $png.BaseName.Substring(0, $png.BaseName.Length - "_1920x1080".Length)
        $candidates.Add((Join-Path $png.DirectoryName ($shortBase + "_fit.txt")))
    }
    $existing = @($candidates | Select-Object -Unique | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) {
        Add-Failure "MISSING fit metadata for $($png.Name); expected one of: $($candidates -join ', ')"
        return $null
    }
    if ($existing.Count -gt 1) {
        Add-Failure "AMBIGUOUS fit metadata for $($png.Name): $($existing -join ', ')"
        return $null
    }
    return $existing[0]
}

function Validate-Png([string]$leafName, [Nullable[int]]$expectedWidth, [Nullable[int]]$expectedHeight) {
    $png = Find-UniqueFile $leafName
    if ($null -eq $png) { return }
    try {
        $dimensions = Get-PngDimensions $png.FullName
    }
    catch {
        Add-Failure "INVALID PNG $leafName`: $($_.Exception.Message)"
        return
    }
    if ($null -ne $expectedWidth -and $dimensions.Width -ne $expectedWidth.Value) {
        Add-Failure "WRONG WIDTH $leafName`: expected $($expectedWidth.Value), got $($dimensions.Width)"
    }
    if ($null -ne $expectedHeight -and $dimensions.Height -ne $expectedHeight.Value) {
        Add-Failure "WRONG HEIGHT $leafName`: expected $($expectedHeight.Value), got $($dimensions.Height)"
    }
    if (($null -eq $expectedWidth -or $dimensions.Width -eq $expectedWidth.Value) -and
        ($null -eq $expectedHeight -or $dimensions.Height -eq $expectedHeight.Value)) {
        Add-Pass "PNG $leafName = $($dimensions.Width)x$($dimensions.Height)"
    }

    $fitPath = Resolve-FitFile $png
    if ($null -ne $fitPath) {
        $fitText = Get-Content -LiteralPath $fitPath -Raw
        if ($fitText -notmatch '(?im)(?:^|\s)match\s*=\s*1(?:\s|$)') {
            Add-Failure "FIT FAIL $leafName`: '$fitPath' does not contain match=1"
        }
        else {
            Add-Pass "FIT $leafName = match=1"
        }
        if ($leafName -like "collection_*") {
            if ($fitText -notmatch '(?im)(?:^|\s)HasPack\s*=\s*(?:1|true)(?:\s|$)') {
                Add-Failure "COLLECTION PACK FAIL $leafName`: fit metadata does not declare HasPack=1"
            }
            if ($fitText -notmatch '(?im)(?:^|\s)CompositionCardIds\.Count\s*=\s*[1-9]\d*(?:\s|$)') {
                Add-Failure "COLLECTION COMPOSITION FAIL $leafName`: fit metadata does not declare a non-zero CompositionCardIds.Count"
            }
        }
    }
}

$resolvedRoot = Resolve-Path -LiteralPath $PackageRoot -ErrorAction SilentlyContinue
if ($null -eq $resolvedRoot) {
    Write-Error "Package root does not exist: $PackageRoot"
    exit 2
}
$PackageRoot = $resolvedRoot.Path

$allPngs = @(Get-ChildItem -LiteralPath $PackageRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -ceq ".png" })
if ($allPngs.Count -ne $requiredCaptures.Count) {
    Add-Failure "MANIFEST PNG COUNT: expected exactly $($requiredCaptures.Count), found $($allPngs.Count)"
}
foreach ($png in $allPngs) {
    if ($requiredCaptures -notcontains $png.Name) {
        Add-Failure "UNEXPECTED PNG: $($png.FullName)"
    }
}

$evidencePath = if ([System.IO.Path]::IsPathRooted($EvidenceFile)) {
    $EvidenceFile
} else {
    Join-Path $PackageRoot $EvidenceFile
}

$evidence = @{}
if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) {
    Add-Failure "MISSING evidence metadata: $evidencePath"
}
else {
    $evidence = Read-KeyValueEvidence $evidencePath
}

$hasPack = Require-Evidence $evidence "Collection HasPack" '^(?:1|true)$'
$compositionCount = Require-Evidence $evidence "Collection CompositionCardIds.Count" '^\d+$'
$atlasVerdict = Require-Evidence $evidence "VIP AtlasBoundaryVerdict" '^(?:PASS|FAIL)$'
$testsTotal = Require-Evidence $evidence "Tests Total" '^\d+$'
$testsPassed = Require-Evidence $evidence "Tests Passed" '^\d+$'
$testsFailed = Require-Evidence $evidence "Tests Failed" '^\d+$'
$testsSkipped = Require-Evidence $evidence "Tests Skipped" '^\d+$'
$exitCode = Require-Evidence $evidence "Exit Code" '^-?\d+$'
$errorCs = Require-Evidence $evidence "Error CS Count" '^\d+$'
$failureCardIds = Require-Evidence $evidence "Collection FailureCardIds" '^(?:NONE|[a-z0-9_]+(?:,[a-z0-9_]+)*)$'
$failureCellIds = Require-Evidence $evidence "VIP FailureCellIds" '^(?:NONE|[0-7](?:,[0-7])*)$'

if ($null -ne $compositionCount -and [int]$compositionCount -eq 0) {
    Add-Failure "PACK-PRESENT evidence is invalid: Collection CompositionCardIds.Count=0"
}
if ($null -ne $atlasVerdict -and $atlasVerdict -eq "FAIL") {
    Add-Failure "VIP atlas boundary verdict is FAIL"
}
if ($null -ne $failureCardIds -and $failureCardIds -ne "NONE") {
    Add-Failure "Collection evidence declares card failures: $failureCardIds"
}
if ($null -ne $failureCellIds -and $failureCellIds -ne "NONE") {
    Add-Failure "VIP evidence declares cell failures: $failureCellIds"
}
if ($null -ne $failureCellIds -and $failureCellIds -ne "NONE") {
    $cellIds = $failureCellIds -split ','
    if (@($cellIds | Select-Object -Unique).Count -ne $cellIds.Count) {
        Add-Failure "DUPLICATE VIP failure cell ID declared: $failureCellIds"
    }
}
if ($null -ne $failureCardIds -and $failureCardIds -ne "NONE") {
    $cardIds = $failureCardIds -split ','
    if (@($cardIds | Select-Object -Unique).Count -ne $cardIds.Count) {
        Add-Failure "DUPLICATE Collection failure card ID declared: $failureCardIds"
    }
}
if ($null -ne $errorCs -and [int]$errorCs -ne 0) {
    Add-Failure "Compilation evidence reports Error CS Count=$errorCs"
}
if ($null -ne $exitCode -and [int]$exitCode -ne 0) {
    Add-Failure "Capture run evidence reports Exit Code=$exitCode"
}
if ($null -ne $testsTotal -and $null -ne $testsPassed -and $null -ne $testsFailed -and $null -ne $testsSkipped) {
    if ([int]$testsTotal -lt 0 -or [int]$testsPassed -lt 0 -or [int]$testsFailed -lt 0 -or [int]$testsSkipped -lt 0) {
        Add-Failure "MALFORMED test totals: totals must be non-negative"
    }
    $sum = [int]$testsPassed + [int]$testsFailed + [int]$testsSkipped
    if ($sum -ne [int]$testsTotal) {
        Add-Failure "MALFORMED test totals: Passed+Failed+Skipped=$sum, Total=$testsTotal"
    }
    if ([int]$testsFailed -ne 0) {
        Add-Failure "Test evidence reports failed tests: Tests Failed=$testsFailed"
    }
}

foreach ($file in $requiredScreenCaptures) {
    Validate-Png $file ([Nullable[int]]1920) ([Nullable[int]]1080)
}

foreach ($file in $requiredNativeCaptures) {
    $widthKey = "Image.$file.Width"
    $heightKey = "Image.$file.Height"
    $declaredWidth = Require-Evidence $evidence $widthKey '^\d+$'
    $declaredHeight = Require-Evidence $evidence $heightKey '^\d+$'
    $width = if ($null -ne $declaredWidth) { [Nullable[int]][int]$declaredWidth } else { [Nullable[int]]$null }
    $height = if ($null -ne $declaredHeight) { [Nullable[int]][int]$declaredHeight } else { [Nullable[int]]$null }
    Validate-Png $file $width $height
}

$consolePath = if ([System.IO.Path]::IsPathRooted($ConsoleLog)) {
    $ConsoleLog
} else {
    Join-Path $PackageRoot $ConsoleLog
}

if (-not (Test-Path -LiteralPath $consolePath -PathType Leaf)) {
    Add-Failure "MISSING console log: $consolePath"
}
else {
    $logText = Get-Content -LiteralPath $consolePath -Raw
    $requiredLogMarkers = @(
        @{ Name = "Collection pack-present HasPack"; Pattern = 'Collection\s+HasPack\s*=\s*(?:1|true)' },
        @{ Name = "Collection CompositionCardIds.Count"; Pattern = 'Collection\s+CompositionCardIds\.Count\s*=\s*\d+' },
        @{ Name = "Collection failure card IDs"; Pattern = 'Collection\s+FailureCardIds\s*=\s*(?:NONE|[a-z0-9_]+(?:,[a-z0-9_]+)*)' },
        @{ Name = "VIP atlas verdict"; Pattern = 'VIP\s+AtlasBoundaryVerdict\s*=\s*(?:PASS|FAIL)' },
        @{ Name = "VIP failure cell IDs"; Pattern = 'VIP\s+FailureCellIds\s*=\s*(?:NONE|[0-7](?:,[0-7])*)' },
        @{ Name = "test total"; Pattern = 'Tests\s+Total\s*=\s*\d+' },
        @{ Name = "tests passed"; Pattern = 'Tests\s+Passed\s*=\s*\d+' },
        @{ Name = "tests failed"; Pattern = 'Tests\s+Failed\s*=\s*\d+' },
        @{ Name = "tests skipped"; Pattern = 'Tests\s+Skipped\s*=\s*\d+' },
        @{ Name = "exit code"; Pattern = 'Exit\s+Code\s*=\s*-?\d+' },
        @{ Name = "error CS count"; Pattern = 'Error\s+CS\s+Count\s*=\s*\d+' }
    )
    foreach ($marker in $requiredLogMarkers) {
        if ($logText -notmatch $marker.Pattern) {
            Add-Failure "MISSING console marker: $($marker.Name)"
        }
        else {
            Add-Pass "Console marker present: $($marker.Name)"
        }
    }

    $forbiddenWarnings = @(
        '\[VipSubscription\]\s+Failed to load shell sprite',
        '\[Collection\]\s+Failed to load rarity .* card frame sprite',
        'CardDatabase:\s+no sprite found at Resources/',
        'Aborting batchmode',
        'error\s+CS(?:\d+)?'
    )
    foreach ($pattern in $forbiddenWarnings) {
        if ($logText -match $pattern) {
            Add-Failure "FORBIDDEN console warning/error matched: $pattern"
        }
    }
}

Write-Output "UI-CAPTURE-PACKAGE-002 artifact validator"
Write-Output "Package: $PackageRoot"
Write-Output "PASS checks: $($passes.Count)"
foreach ($pass in $passes) { Write-Output "  PASS $pass" }
Write-Output "FAIL checks: $($failures.Count)"
foreach ($failure in $failures) { Write-Output "  FAIL $failure" }

if ($failures.Count -gt 0) { exit 1 }
Write-Output "RESULT: PASS (artifact structure only; rendered visual correctness is NOT proven)"
exit 0
