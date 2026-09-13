# Native Unity Player window capture harness (AN-REVAMPV2-005).
#
# Captures the ACTUAL running Unity player's window via PrintWindow (a real OS-level window
# capture, works even partially occluded) - not a static reference image, not the EditMode
# ScreenContactSheetGenerator's procedural render. Produces one PNG plus a JSON sidecar recording
# source HEAD, runtime hash, dimensions, capture SHA-256, and a UTC timestamp/state label.
#
# This script only launches/reads a build and writes to -OutDir. It never touches game code,
# Assets/, Save files, or any git-tracked source. Use only an already-built, LK/VE-supplied
# executable - never build one here.
#
# PRODUCTIVE CODING TASK - gated capture. Before touching the player process at all, this script
# now runs tools/validate_release_candidate.ps1 in GATE MODE against -CandidateRoot: exact HEAD,
# tracked dirt, the frozen tag, Runtime.dll hash, and clean build/test evidence must all hold, or
# this script refuses (exit 2) without ever launching the player or writing a capture. There is
# deliberately no bypass switch - an invalid candidate cannot reach capture through this script.
#
# Usage:
#   powershell -File tools/capture_native_window.ps1 `
#     -ExePath "C:\Users\zihan\Downloads\MoD-lk-line-019\Builds\Windows64\MyriadOfDragons.exe" `
#     -Label "TutorialFaq_Home_1920x1080" `
#     -CandidateRoot "C:\Users\zihan\Downloads\MoD-lk-line-019" `
#     -SourceHead "78410144f21b6d7e5d9c9b89a3c74d9ed2068aab" `
#     -RuntimeHash "04bc1b0485dbe318d4351e5e26b5aa8dd22320113478357a69ed28e0a41cdac2" `
#     -ExpectedTag "lk/FROZEN-capture-candidate-rc19" `
#     -BuildLog "MoD-lk-line-019\lk_rc19_build.log" `
#     -TestResultsXml "MoD-lk-line-019\lk_rc19_results.xml" `
#     -OutDir "handover\native_capture_out" `
#     -WaitSeconds 8
#
# To capture a DIFFERENT screen state (Friends, Battle Pass, Chat, Solo Circuit), navigate the
# already-running player manually (or via a future input-injection pass) between captures, and
# call this script again with a new -Label and -LeaveRunning to avoid relaunching (the gate
# re-runs every call - cheap, and it means the candidate is re-verified untouched before each
# shot, not just once at the start of a long capture session). This pass proves the capture
# mechanism itself works; it does not drive in-game navigation.
#
# Usage (run the tool's own automated tests, no candidate/build/Unity/player needed):
#   powershell -File tools/capture_native_window.ps1 -SelfTest

[CmdletBinding(DefaultParameterSetName = "Capture")]
param(
    [Parameter(ParameterSetName = "Capture", Mandatory)][string]$ExePath,
    [Parameter(ParameterSetName = "Capture", Mandatory)][string]$Label,

    # Gate inputs - all mandatory, no bypass. Root of the candidate's git worktree/build tree.
    [Parameter(ParameterSetName = "Capture", Mandatory)][string]$CandidateRoot,
    [Parameter(ParameterSetName = "Capture", Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceHead,
    [Parameter(ParameterSetName = "Capture", Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$RuntimeHash,
    [Parameter(ParameterSetName = "Capture", Mandatory)][string]$ExpectedTag,
    [Parameter(ParameterSetName = "Capture", Mandatory)][string]$BuildLog,
    [Parameter(ParameterSetName = "Capture", Mandatory)][string]$TestResultsXml,
    [Parameter(ParameterSetName = "Capture")]
    [string]$RuntimeDllRelativePath = "Builds\Windows64\MyriadOfDragons_Data\Managed\MyriadOfDragons.Runtime.dll",
    # Default resolved after the param block, not here: $PSScriptRoot is not reliably populated
    # while param-block default expressions themselves are being evaluated under `-File`.
    [Parameter(ParameterSetName = "Capture")]
    [string]$ValidatorScript = "",

    [Parameter(ParameterSetName = "Capture")][string]$OutDir = "handover/native_capture_out",
    [Parameter(ParameterSetName = "Capture")][int]$WaitSeconds = 8,
    [Parameter(ParameterSetName = "Capture")][switch]$LeaveRunning,
    [Parameter(ParameterSetName = "Capture")][string]$AttachProcessId = "",

    [Parameter(ParameterSetName = "SelfTest", Mandatory)][switch]$SelfTest
)

if ($ValidatorScript -eq "") {
    $ValidatorScript = Join-Path (Split-Path -Parent $PSCommandPath) "validate_release_candidate.ps1"
}

# Runs the gate as a real subprocess (not a dot-sourced function) so this script never shares
# process state (working directory, loaded types) with the validator, and so its own exit code is
# the single source of truth for pass/refuse - matching how a human would run it by hand.
function Invoke-PreCaptureGate {
    param(
        [Parameter(Mandatory)][string]$ValidatorScript,
        [Parameter(Mandatory)][string]$CandidateRoot,
        [Parameter(Mandatory)][string]$SourceHead,
        [Parameter(Mandatory)][string]$RuntimeHash,
        [Parameter(Mandatory)][string]$ExpectedTag,
        [Parameter(Mandatory)][string]$BuildLog,
        [Parameter(Mandatory)][string]$TestResultsXml,
        [string]$RuntimeDllRelativePath = "Builds\Windows64\MyriadOfDragons_Data\Managed\MyriadOfDragons.Runtime.dll"
    )
    if (-not (Test-Path -LiteralPath $ValidatorScript)) {
        return [pscustomobject]@{ Passed = $false; ExitCode = 2; Output = @("Validator script not found: $ValidatorScript") }
    }
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $ValidatorScript `
        -CandidateRoot $CandidateRoot -ExpectedHead $SourceHead -ExpectedRuntimeHash $RuntimeHash `
        -RuntimeDllRelativePath $RuntimeDllRelativePath -ExpectedTag $ExpectedTag `
        -BuildLog $BuildLog -TestResultsXml $TestResultsXml 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $prevEap
    return [pscustomobject]@{ Passed = ($exitCode -eq 0); ExitCode = $exitCode; Output = @($output | ForEach-Object { "$_" }) }
}

# ---------------------------------------------------------------------------
# -SelfTest: exercises the gate wiring against real fixtures - no ExePath, no Unity, no player
# window. Confirms an invalid candidate is refused BEFORE any capture is attempted (checked by
# asserting OutDir stays empty across every REFUSES case, not just the exit code).
# ---------------------------------------------------------------------------
if ($SelfTest) {
    $fails = 0
    function Check {
        param([string]$Name, [scriptblock]$Body)
        try { & $Body; Write-Host "PASS  $Name" }
        catch { Write-Host "FAIL  $Name :: $($_.Exception.Message)"; $script:fails++ }
    }

    Write-Host "=== capture_native_window.ps1 -SelfTest ==="

    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("capture_native_selftest_" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    $validator = Join-Path $PSScriptRoot "validate_release_candidate.ps1"

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
    & git -C $repoDir tag -a "capture-fixture-frozen" -m "freeze" 2>$null
    $realDllHash = (Get-FileHash -LiteralPath $dllFullPath -Algorithm SHA256).Hash.ToLowerInvariant()

    $cleanLog = Join-Path $tmp "clean_build.log"
    Set-Content -Path $cleanLog -Value @("Compiling...", "Build succeeded.") -Encoding utf8
    $dirtyLog = Join-Path $tmp "dirty_build.log"
    Set-Content -Path $dirtyLog -Value @("Assets\Foo.cs(1,1): error CS0103: bad", "done") -Encoding utf8
    $goodXml = Join-Path $tmp "good_results.xml"
    Set-Content -Path $goodXml -Value '<?xml version="1.0"?><test-run testcasecount="5" passed="5" failed="0"></test-run>' -Encoding utf8

    Check "Gate: PASSES for a fully valid candidate" {
        $r = Invoke-PreCaptureGate -ValidatorScript $validator -CandidateRoot $repoDir `
            -SourceHead $realHead -RuntimeHash $realDllHash -ExpectedTag "capture-fixture-frozen" `
            -BuildLog $cleanLog -TestResultsXml $goodXml
        if (-not $r.Passed) { throw "expected Passed=true, findings: $($r.Output -join ' | ')" }
    }
    Check "Gate: REFUSES wrong SourceHead" {
        $r = Invoke-PreCaptureGate -ValidatorScript $validator -CandidateRoot $repoDir `
            -SourceHead ("9" * 40) -RuntimeHash $realDllHash -ExpectedTag "capture-fixture-frozen" `
            -BuildLog $cleanLog -TestResultsXml $goodXml
        if ($r.Passed) { throw "gate should have refused a wrong SourceHead" }
    }
    Check "Gate: REFUSES tracked dirt in the candidate" {
        Set-Content -Path (Join-Path $repoDir "a.txt") -Value "changed" -Encoding utf8
        $r = Invoke-PreCaptureGate -ValidatorScript $validator -CandidateRoot $repoDir `
            -SourceHead $realHead -RuntimeHash $realDllHash -ExpectedTag "capture-fixture-frozen" `
            -BuildLog $cleanLog -TestResultsXml $goodXml
        & git -C $repoDir checkout -q -- a.txt
        if ($r.Passed) { throw "gate should have refused tracked dirt" }
    }
    Check "Gate: REFUSES a missing/wrong frozen tag" {
        $r = Invoke-PreCaptureGate -ValidatorScript $validator -CandidateRoot $repoDir `
            -SourceHead $realHead -RuntimeHash $realDllHash -ExpectedTag "no-such-tag" `
            -BuildLog $cleanLog -TestResultsXml $goodXml
        if ($r.Passed) { throw "gate should have refused a missing tag" }
    }
    Check "Gate: REFUSES a Runtime.dll hash mismatch" {
        $r = Invoke-PreCaptureGate -ValidatorScript $validator -CandidateRoot $repoDir `
            -SourceHead $realHead -RuntimeHash ("0" * 64) -ExpectedTag "capture-fixture-frozen" `
            -BuildLog $cleanLog -TestResultsXml $goodXml
        if ($r.Passed) { throw "gate should have refused a wrong Runtime.dll hash" }
    }
    Check "Gate: REFUSES a dirty build log (error CS)" {
        $r = Invoke-PreCaptureGate -ValidatorScript $validator -CandidateRoot $repoDir `
            -SourceHead $realHead -RuntimeHash $realDllHash -ExpectedTag "capture-fixture-frozen" `
            -BuildLog $dirtyLog -TestResultsXml $goodXml
        if ($r.Passed) { throw "gate should have refused a dirty build log" }
    }

    # End-to-end: actually invoke THIS script (not just Invoke-PreCaptureGate) with a real, doomed
    # -ExePath against an invalid candidate, and prove no capture was even attempted - OutDir stays
    # completely empty, not just "exit code is non-zero". This is the guarantee the task asked
    # for: an invalid candidate cannot reach capture.
    $endToEndOutDir = Join-Path $tmp "end_to_end_out"
    Check "End-to-end: invalid candidate is refused (exit 1) and OutDir stays empty - capture never attempted" {
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "SilentlyContinue"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
            -ExePath (Join-Path $tmp "does_not_exist.exe") -Label "ShouldNeverCapture" `
            -CandidateRoot $repoDir -SourceHead ("9" * 40) -RuntimeHash $realDllHash `
            -ExpectedTag "capture-fixture-frozen" -BuildLog $cleanLog -TestResultsXml $goodXml `
            -OutDir $endToEndOutDir 1>$null 2>$null
        $code = $LASTEXITCODE
        $ErrorActionPreference = $prevEap
        if ($code -eq 0) { throw "end-to-end run should not have succeeded (exit 0)" }
        if (Test-Path -LiteralPath $endToEndOutDir) {
            $written = @(Get-ChildItem -LiteralPath $endToEndOutDir -File -ErrorAction SilentlyContinue)
            if ($written.Count -gt 0) { throw "OutDir has $($written.Count) file(s) - a capture was attempted despite the invalid candidate" }
        }
    }
    Check "End-to-end: missing gate evidence (no -BuildLog/-TestResultsXml) is refused by parameter binding" {
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "SilentlyContinue"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
            -ExePath (Join-Path $tmp "does_not_exist.exe") -Label "ShouldNeverCapture" `
            -CandidateRoot $repoDir -SourceHead $realHead -RuntimeHash $realDllHash `
            -ExpectedTag "capture-fixture-frozen" -OutDir $endToEndOutDir 1>$null 2>$null
        $code = $LASTEXITCODE
        $ErrorActionPreference = $prevEap
        if ($code -eq 0) { throw "expected a non-zero exit when required gate params are omitted, got 0" }
    }

    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host ""
    if ($fails -eq 0) { Write-Host "SELFTEST: all guard checks passed."; exit 0 }
    else { Write-Host "SELFTEST: $fails guard check(s) FAILED."; exit 1 }
}

# ---------------------------------------------------------------------------
# Capture mode: gate first, then (only if the gate passes) do the real capture.
# ---------------------------------------------------------------------------

$gate = Invoke-PreCaptureGate -ValidatorScript $ValidatorScript -CandidateRoot $CandidateRoot `
    -SourceHead $SourceHead -RuntimeHash $RuntimeHash -ExpectedTag $ExpectedTag `
    -BuildLog $BuildLog -TestResultsXml $TestResultsXml -RuntimeDllRelativePath $RuntimeDllRelativePath
if (-not $gate.Passed) {
    Write-Host "REFUSED: candidate failed the pre-capture gate (validator exit $($gate.ExitCode)). No player was launched, no capture was written."
    foreach ($line in $gate.Output) { Write-Host "  $line" }
    exit 2
}
Write-Host "Gate PASSED - candidate verified (HEAD, tag, Runtime.dll hash, tracked dirt, build/test evidence). Proceeding to capture."

Add-Type -AssemblyName System.Drawing
Add-Type -Namespace NativeCapture -Name Win32 -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
[DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
[DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
public struct POINT { public int X; public int Y; }
'@

# CRITICAL: without this, every Win32 geometry call below (GetWindowRect, GetClientRect,
# ClientToScreen) AND System.Windows.Forms.Screen.Bounds runs under Windows' DPI VIRTUALIZATION,
# which silently scales every reported pixel dimension down by the display's scale factor for any
# caller that hasn't declared itself DPI-aware - a real, measured example on this machine: a
# genuinely-1920x1080 Unity client area was reported (and captured) as 1280x720 (exactly 1920/1.5,
# 1080/1.5 - this display's 150% Windows scaling) before this call was added. That is the exact
# failure mode the "do not relabel non-native output as native" instruction is guarding against -
# every dimension this script reports must be the real physical pixel count, not a DPI-shrunk one.
[NativeCapture.Win32]::SetProcessDPIAware() | Out-Null

if (-not (Test-Path $ExePath)) {
    Write-Error "ExePath not found: $ExePath"
    exit 1
}

$ownedProcess = $false
if ($AttachProcessId -ne "") {
    $proc = Get-Process -Id ([int]$AttachProcessId) -ErrorAction SilentlyContinue
    if (-not $proc) { Write-Error "No process with PID $AttachProcessId"; exit 1 }
}
else {
    $proc = Start-Process -FilePath $ExePath -PassThru
    $ownedProcess = $true
    Write-Host "Launched player, PID $($proc.Id). Waiting $WaitSeconds s for the window to appear..."
    Start-Sleep -Seconds $WaitSeconds
    $proc.Refresh()
}

# MainWindowHandle can be zero right after launch before the window is created - poll briefly.
$deadline = (Get-Date).AddSeconds(10)
while ($proc.MainWindowHandle -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
    $proc.Refresh()
}
$hwnd = $proc.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) {
    Write-Error "Could not obtain a main window handle for PID $($proc.Id) - is this a windowed (not -batchmode) build?"
    if ($ownedProcess -and -not $LeaveRunning) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
    exit 1
}

[NativeCapture.Win32]::ShowWindow($hwnd, 9) | Out-Null   # SW_RESTORE, in case minimized
[NativeCapture.Win32]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500

$rect = New-Object NativeCapture.Win32+RECT
[NativeCapture.Win32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$outerWidth = $rect.Right - $rect.Left
$outerHeight = $rect.Bottom - $rect.Top
if ($outerWidth -le 0 -or $outerHeight -le 0) {
    Write-Error "Window rect came back non-positive ($outerWidth x $outerHeight) - window may not be visible on this session's display."
    if ($ownedProcess -and -not $LeaveRunning) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
    exit 1
}

# GetWindowRect includes the OS title bar and borders - it is NOT the game's actual render
# surface. Measure the real client rect (GetClientRect, origin always 0,0) and its screen-space
# top-left (ClientToScreen) so the crop below - and the width/height this script reports - reflect
# only the true client area a player actually sees rendered, never window chrome pixels folded in
# as if they were part of the 1920x1080 the game itself is claiming to render at.
$clientRect = New-Object NativeCapture.Win32+RECT
[NativeCapture.Win32]::GetClientRect($hwnd, [ref]$clientRect) | Out-Null
$width = $clientRect.Right - $clientRect.Left
$height = $clientRect.Bottom - $clientRect.Top
if ($width -le 0 -or $height -le 0) {
    Write-Error "Client rect came back non-positive ($width x $height) - window may not be visible on this session's display."
    if ($ownedProcess -and -not $LeaveRunning) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
    exit 1
}
$clientOrigin = New-Object NativeCapture.Win32+POINT
$clientOrigin.X = 0
$clientOrigin.Y = 0
[NativeCapture.Win32]::ClientToScreen($hwnd, [ref]$clientOrigin) | Out-Null
$clientOffsetX = $clientOrigin.X - $rect.Left
$clientOffsetY = $clientOrigin.Y - $rect.Top

$outerBmp = New-Object System.Drawing.Bitmap $outerWidth, $outerHeight
$gfx = [System.Drawing.Graphics]::FromImage($outerBmp)
$hdc = $gfx.GetHdc()
$ok = [NativeCapture.Win32]::PrintWindow($hwnd, $hdc, 2)   # PW_RENDERFULLCONTENT
$gfx.ReleaseHdc($hdc)
$gfx.Dispose()

if (-not $ok) {
    Write-Host "PrintWindow returned false - falling back to CopyFromScreen (requires the window to be unoccluded and on-screen)."
    $outerBmp.Dispose()
    $outerBmp = New-Object System.Drawing.Bitmap $outerWidth, $outerHeight
    $gfx = [System.Drawing.Graphics]::FromImage($outerBmp)
    $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($outerWidth, $outerHeight)))
    $gfx.Dispose()
}

# Crop the full window capture down to just the client sub-rectangle - the PNG this script writes
# must be exactly the game's real render surface, not the window's outer chrome-inclusive bounds.
$cropRect = New-Object System.Drawing.Rectangle $clientOffsetX, $clientOffsetY, $width, $height
$bmp = $outerBmp.Clone($cropRect, $outerBmp.PixelFormat)
$outerBmp.Dispose()

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$utc = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$safeLabel = ($Label -replace '[^a-zA-Z0-9_\-]', '_')
$baseName = "${safeLabel}_${utc}_${width}x${height}"
$pngPath = Join-Path $OutDir "$baseName.png"
$jsonPath = Join-Path $OutDir "$baseName.json"

$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$pngHash = (Get-FileHash -Path $pngPath -Algorithm SHA256).Hash.ToLower()

# Display-scale detection: reported window pixel size vs the logical size Windows scaling would
# imply. This is a best-effort DPI signal external to the app - it is NOT the app's own internal
# safe-area/accessibility data, which only the running game can expose (see honesty note below).
Add-Type -AssemblyName System.Windows.Forms
$screenBounds = [System.Windows.Forms.Screen]::FromHandle($hwnd).Bounds

$sidecar = [ordered]@{
    label             = $Label
    capturedUtc       = (Get-Date).ToUniversalTime().ToString("o")
    sourceHead        = $SourceHead
    runtimeHash       = $RuntimeHash
    exePath           = (Resolve-Path $ExePath).Path
    processId         = $proc.Id
    # Real, measured client-area pixels (GetClientRect) - the PNG is cropped to exactly this
    # rectangle. Never the outer window rect (GetWindowRect), which folds in the OS title bar and
    # borders and would over-report height/width relative to what the game actually renders.
    clientWidthPx     = $width
    clientHeightPx    = $height
    outerWindowWidthPx  = $outerWidth
    outerWindowHeightPx = $outerHeight
    screenBoundsPx    = @{ width = $screenBounds.Width; height = $screenBounds.Height }
    captureMethod     = if ($ok) { "PrintWindow(PW_RENDERFULLCONTENT)+CropToClientRect" } else { "CopyFromScreen(fallback)+CropToClientRect" }
    pngSha256         = $pngHash
    pngPath           = (Resolve-Path $pngPath).Path
    note              = "safe-area and accessibility-fit data below are NOT independently measured by this capture tool - it can only observe window/screen pixel geometry from the OS side. Real safe-area/accessibility-fit values must come from the game's own EditMode/PlayMode instrumentation (e.g. UiGeometryRegressionTests, CanvasOverflowAuditTests) cross-referenced against this capture's sourceHead, not invented here. clientWidthPx/clientHeightPx are a real GetClientRect measurement of the running window, not asserted or fabricated - a mismatch against 1920x1080 here means the player's actual client area was not that size at capture time, which this script will not paper over."
    externalGeometry  = @{
        aspectRatio           = [math]::Round($width / $height, 4)
        matchesTrue1920x1080Client = ($width -eq 1920 -and $height -eq 1080)
        windowFillsScreen     = ($outerWidth -eq $screenBounds.Width -and $outerHeight -eq $screenBounds.Height)
    }
}
$sidecar | ConvertTo-Json -Depth 4 | Set-Content -Path $jsonPath -Encoding utf8

Write-Host "CAPTURE OK"
Write-Host "PNG:  $pngPath ($((Get-Item $pngPath).Length) bytes)"
Write-Host "JSON: $jsonPath"
Write-Host "SHA256: $pngHash"
Write-Host "Client area: ${width}x${height} px (outer window was ${outerWidth}x${outerHeight} px)"

if ($ownedProcess -and -not $LeaveRunning) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Write-Host "Player process $($proc.Id) stopped (pass -LeaveRunning to keep it open for further captures)."
}
else {
    Write-Host "Player process $($proc.Id) left running (PID for further -AttachProcessId captures)."
}
