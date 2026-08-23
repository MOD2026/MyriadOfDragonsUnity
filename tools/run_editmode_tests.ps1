# Timeout-guarded EditMode batch runner.
#
# Why this exists: on 2026-08-23 a batch run silently stalled inside SaveSystem.Save's file-write
# sequence (suspected Windows-level file lock / AV scan) and sat unnoticed for 50+ minutes with a
# live process but a dead log. A bare Unity.exe invocation has no way to notice that on its own.
# This wrapper does: it kills the run and says so instead of hanging forever unwatched.
#
# Same day, AV/ATD were ruled out (folder exceptions added, stall reproduced regardless) and a
# corrupted Library/ artifact cache was ruled out too (full delete+reimport, then a fully warm
# rerun, both still stalled at the same point). The stall is reachable from SaveSystemTests.cs
# itself (not just Shop-purchase tests), so it isn't file-specific either - current best working
# theory is something that only surfaces deep into one continuous 761-test batch process, not a
# code bug. -ClassListFile below runs each class as its own short-lived Unity process instead of
# one long continuous run, which both sidesteps the stall in practice and tests that theory.
#
# Usage:
#   powershell -File tools/run_editmode_tests.ps1
#   powershell -File tools/run_editmode_tests.ps1 -ResultsPath full_results.xml -LogPath full_run.log -TimeoutMinutes 30
#   powershell -File tools/run_editmode_tests.ps1 -TestFilter "MyriadOfDragons.Tests.BattleLogicTests"
#   powershell -File tools/run_editmode_tests.ps1 -ClassListFile tools/batch_classes.txt -BatchOutDir batch_out
#
# Exit code 124 means "timed out and was killed" - treat that as a failed run, not a green one.
# Unity must already be fully closed before running this (same rule as the bare command).
#
# Multi-class filtering note: Unity's -testFilter does NOT support an OR list via comma-separated
# values or repeated flags in this Unity version - both collapse into one literal groupNames string
# that matches nothing. A single exact class name (or a substring, which Unity matches against the
# full Namespace.Class.Method string) works. -ClassListFile runs one exact class per Unity process.
#
# Cross-seat lock file: Working Hands (interactive Editor) and the coding seat (this batch wrapper)
# both need exclusive access to the same project - a bare `tasklist` check before starting is a
# race (two seats can both see "clear" within the same second and both launch). This script now
# claims .unity_batch.lock in the repo root before touching Unity and always releases it on exit,
# success or failure. Working Hands should honor the same file even without using this script:
# check for it before opening the Editor, and touch/remove it around any session that needs
# exclusive Unity access.

param(
    [string]$ProjectPath = "C:\Users\zihan\Downloads\MyriadOfDragonsUnity",
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe",
    [string]$ResultsPath = "results.xml",
    [string]$LogPath = "run.log",
    [int]$TimeoutMinutes = 25,
    [int]$StallCheckSeconds = 120,
    [string]$TestFilter = "",
    [string[]]$TestFilters = @(),
    [string]$ClassListFile = "",
    [string]$BatchOutDir = "batch_out",
    [string]$NamespacePrefix = "MyriadOfDragons.Tests."
)

function Invoke-SingleRun {
    param(
        [string]$ResultsPath,
        [string]$LogPath,
        [string]$TestFilter,
        [int]$TimeoutMinutes,
        [int]$StallCheckSeconds
    )

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
        return 124
    }

    Write-Host "Unity exited with code $($proc.ExitCode). Check $LogPath for 'error CS' before trusting $ResultsPath."
    return $proc.ExitCode
}

if (-not (Test-Path $UnityExe)) {
    Write-Error "Unity.exe not found at $UnityExe"
    exit 1
}

$lockFile = Join-Path $ProjectPath ".unity_batch.lock"

if (Test-Path $lockFile) {
    $lockInfo = Get-Content $lockFile -Raw | ConvertFrom-Json -ErrorAction SilentlyContinue
    $lockPid = $lockInfo.pid
    $lockOwner = $lockInfo.owner
    $lockStillAlive = $lockPid -and (Get-Process -Id $lockPid -ErrorAction SilentlyContinue)
    if ($lockStillAlive) {
        Write-Error "Unity is locked by another seat ($lockOwner, watcher PID $lockPid, since $($lockInfo.startedAt)). Wait for it to finish - do not delete the lock file or start a run anyway."
        exit 1
    }
    else {
        Write-Host "Stale lock file found (owner process $lockPid no longer running) - clearing it and proceeding."
        Remove-Item $lockFile -Force -ErrorAction SilentlyContinue
    }
}

if (Get-Process -Name "Unity" -ErrorAction SilentlyContinue) {
    Write-Error "Unity is already running (no lock file, so this wasn't started by this script - likely Working Hands' interactive Editor). Close it fully before starting a batch run (exclusive project lock)."
    exit 1
}

@{ pid = $PID; owner = "coding-seat-batch-wrapper"; startedAt = (Get-Date).ToString("o") } | ConvertTo-Json | Set-Content -Path $lockFile -Encoding utf8

try {

if ($ClassListFile -ne "") {
    if (-not (Test-Path $ClassListFile)) {
        Write-Error "ClassListFile not found: $ClassListFile"
        exit 1
    }

    $outDirFull = Join-Path $ProjectPath $BatchOutDir
    New-Item -ItemType Directory -Force -Path $outDirFull | Out-Null

    $classes = Get-Content $ClassListFile | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }
    $totalPassed = 0
    $totalFailed = 0
    $totalCases = 0
    $stalledClasses = @()
    $errorClasses = @()
    $summaryLines = @()

    foreach ($class in $classes) {
        $filter = "$NamespacePrefix$class"
        $classResults = "$BatchOutDir/$class.results.xml"
        $classLog = "$BatchOutDir/$class.log"

        Write-Host "=== Running $class ==="
        $exitCode = Invoke-SingleRun -ResultsPath $classResults -LogPath $classLog -TestFilter $filter -TimeoutMinutes $TimeoutMinutes -StallCheckSeconds $StallCheckSeconds

        $resultsFullPath = Join-Path $ProjectPath $classResults
        if ($exitCode -eq 124) {
            $stalledClasses += $class
            $summaryLines += "$class`tSTALLED`t-`t-`t-"
        }
        elseif (Test-Path $resultsFullPath) {
            $xml = [xml](Get-Content $resultsFullPath -Raw)
            $run = $xml.'test-run'
            $totalCases += [int]$run.testcasecount
            $totalPassed += [int]$run.passed
            $totalFailed += [int]$run.failed
            $summaryLines += "$class`tOK`t$($run.testcasecount)`t$($run.passed)`t$($run.failed)"
        }
        else {
            $errorClasses += $class
            $summaryLines += "$class`tNO_RESULTS`t-`t-`t-"
        }
    }

    $summaryPath = Join-Path $outDirFull "SUMMARY.tsv"
    "class`tstatus`tcases`tpassed`tfailed" | Out-File -FilePath $summaryPath -Encoding utf8
    $summaryLines | Out-File -FilePath $summaryPath -Encoding utf8 -Append

    Write-Host ""
    Write-Host "=== BATCH RUN COMPLETE ==="
    Write-Host "Classes run: $($classes.Count)"
    Write-Host "Aggregate: $totalCases cases, $totalPassed passed, $totalFailed failed"
    Write-Host "Stalled classes ($($stalledClasses.Count)): $($stalledClasses -join ', ')"
    Write-Host "No-results classes ($($errorClasses.Count)): $($errorClasses -join ', ')"
    Write-Host "Per-class summary: $summaryPath"

    if ($stalledClasses.Count -gt 0 -or $errorClasses.Count -gt 0) { exit 1 }
    exit 0
}

$exitCode = Invoke-SingleRun -ResultsPath $ResultsPath -LogPath $LogPath -TestFilter $TestFilter -TimeoutMinutes $TimeoutMinutes -StallCheckSeconds $StallCheckSeconds
exit $exitCode

}
finally {
    Remove-Item $lockFile -Force -ErrorAction SilentlyContinue
}
