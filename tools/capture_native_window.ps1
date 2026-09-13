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
# Usage:
#   powershell -File tools/capture_native_window.ps1 `
#     -ExePath "C:\Users\zihan\Downloads\MoD-lk-line-019\Builds\Windows64\MyriadOfDragons.exe" `
#     -Label "TutorialFaq_Home_1920x1080" `
#     -SourceHead "78410144f21b6d7e5d9c9b89a3c74d9ed2068aab" `
#     -RuntimeHash "04bc1b0485dbe318d4351e5e26b5aa8dd22320113478357a69ed28e0a41cdac2" `
#     -OutDir "handover\native_capture_out" `
#     -WaitSeconds 8
#
# To capture a DIFFERENT screen state (Friends, Battle Pass, Chat, Solo Circuit), navigate the
# already-running player manually (or via a future input-injection pass) between captures, and
# call this script again with a new -Label and -LeaveRunning to avoid relaunching. This pass
# proves the capture mechanism itself works; it does not drive in-game navigation.

param(
    [Parameter(Mandatory)][string]$ExePath,
    [Parameter(Mandatory)][string]$Label,
    [string]$SourceHead = "",
    [string]$RuntimeHash = "",
    [string]$OutDir = "handover/native_capture_out",
    [int]$WaitSeconds = 8,
    [switch]$LeaveRunning,
    [string]$AttachProcessId = ""
)

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
