# Timeout-guarded EditMode batch runner.
#
# Why this exists: on 2026-08-23 a batch run silently stalled inside SaveSystem.Save's file-write
# sequence (suspected Windows-level file lock / AV scan) and sat unnoticed for 50+ minutes with a
# live process but a dead log. A bare Unity.exe invocation has no way to notice that on its own.
# This wrapper does: it kills the run and says so instead of hanging forever unwatched.
#
# Usage:
#   powershell -File tools/run_editmode_tests.ps1
#   powershell -File tools/run_editmode_tests.ps1 -ResultsPath full_results.xml -LogPath full_run.log -TimeoutMinutes 30
#
# Exit code 124 means "timed out and was killed" - treat that as a failed run, not a green one.
# Unity must already be fully closed before running this (same rule as the bare command).

param(
    [string]$ProjectPath = "C:\Users\zihan\Downloads\MyriadOfDragonsUnity",
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe",
    [string]$ResultsPath = "results.xml",
    [string]$LogPath = "run.log",
    [int]$TimeoutMinutes = 25,
    [int]$StallCheckSeconds = 120,
    [string]$TestFilter = "",
    [string[]]$TestFilters = @()
)

if (-not (Test-Path $UnityExe)) {
    Write-Error "Unity.exe not found at $UnityExe"
    exit 1
}

if (Get-Process -Name "Unity" -ErrorAction SilentlyContinue) {
    Write-Error "Unity is already running. Close it fully before starting a batch run (exclusive project lock)."
    exit 1
}

$resultsFull = Join-Path $ProjectPath $ResultsPath
$logFull = Join-Path $ProjectPath $LogPath
if (Test-Path $logFull) { Remove-Item $logFull -Force }
if (Test-Path $resultsFull) { Remove-Item $resultsFull -Force }

$unityArgs = @(
    "-batchmode", "-projectPath", $ProjectPath,
    "-runTests", "-testPlatform", "EditMode",
    "-testResults", $ResultsPath, "-logFile", $LogPath
)
if ($TestFilter -ne "") {
    $unityArgs += @("-testFilter", $TestFilter)
}
foreach ($f in $TestFilters) {
    $unityArgs += @("-testFilter", $f)
}

$proc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -WorkingDirectory $ProjectPath -PassThru

$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
$lastLogLength = -1
$lastLogChange = Get-Date

while (-not $proc.HasExited -and (Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 15

    $currentLength = if (Test-Path $logFull) { (Get-Item $logFull).Length } else { 0 }
    if ($currentLength -ne $lastLogLength) {
        $lastLogLength = $currentLength
        $lastLogChange = Get-Date
    }

    $stalledSeconds = ((Get-Date) - $lastLogChange).TotalSeconds
    if ($stalledSeconds -gt $StallCheckSeconds) {
        Write-Host "STALLED: log has not grown in $([int]$stalledSeconds)s (last size $lastLogLength bytes) - killing early instead of waiting out the full timeout."
        break
    }
}

if (-not $proc.HasExited) {
    $reason = if ((Get-Date) -ge $deadline) { "exceeded $TimeoutMinutes minute timeout" } else { "log stalled for over $StallCheckSeconds seconds" }
    Write-Host "TIMEOUT/STALL: Unity EditMode run $reason - killing process tree (PID $($proc.Id))."
    Get-CimInstance Win32_Process -Filter "ParentProcessId=$($proc.Id)" -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Write-Host "Report this as a FAILED run (exit 124) with the stall point from $LogPath, not as 'should pass.'"
    exit 124
}

Write-Host "Unity exited with code $($proc.ExitCode). Check $LogPath for 'error CS' before trusting $ResultsPath."
exit $proc.ExitCode
