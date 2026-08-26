# Live seat monitor - leave this running in a terminal pane and glance at it.
#
# Redraws every 2 seconds with a moving spinner and a ticking clock, so the pane visibly
# MOVES whether or not anything is happening. That is the point: a frozen pane means the
# monitor died, not that the room is idle - the two must never look the same.
#
#   powershell -ExecutionPolicy Bypass -File tools/seat_watch.ps1
#
# Ctrl+C to stop.  -IntervalSeconds to change the refresh rate.

param(
    [int]$IntervalSeconds = 2
)

$ErrorActionPreference = "SilentlyContinue"
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

$spinner = @('|', '/', '-', '\')
$tick = 0
$lastCommit = ""
$lastLockState = ""
$events = New-Object System.Collections.ArrayList

function Add-Event($text, $color) {
    $stamp = (Get-Date).ToString('HH:mm:ss')
    [void]$events.Add([pscustomobject]@{ Time = $stamp; Text = $text; Color = $color })
    while ($events.Count -gt 8) { $events.RemoveAt(0) }
}

Add-Event "monitor started" "DarkGray"

while ($true) {
    $tick++
    $now = Get-Date
    $spin = $spinner[$tick % 4]

    # --- gather real state (nothing here is self-reported) ---
    $lockPath = Join-Path $repo ".unity_batch.lock"
    $lockState = "FREE"; $lockDetail = ""; $lockColor = "Green"

    if (Test-Path $lockPath) {
        $lock = Get-Content $lockPath -Raw | ConvertFrom-Json
        $holder = Get-Process -Id $lock.pid -ErrorAction SilentlyContinue
        $mins = [math]::Round(($now - [datetime]$lock.startedAt).TotalMinutes, 1)
        if ($holder) {
            $lockState = "BUSY"; $lockColor = "Yellow"
            $lockDetail = "pid $($lock.pid), $mins min"
        }
        else {
            $lockState = "STALE LOCK"; $lockColor = "Red"
            $lockDetail = "pid $($lock.pid) is DEAD - a room may be waiting forever"
        }
    }

    if ($lockState -ne $lastLockState -and $lastLockState -ne "") {
        Add-Event "Unity: $lastLockState -> $lockState" $lockColor
    }
    $lastLockState = $lockState

    $runLog = Join-Path $repo "run.log"
    $logLine = "no run.log"
    $logColor = "DarkGray"
    if (Test-Path $runLog) {
        $log = Get-Item $runLog
        $idle = [int]($now - $log.LastWriteTime).TotalSeconds
        $mb = [math]::Round($log.Length / 1MB, 1)
        if ($idle -lt 20) { $logLine = "$mb MB, GROWING (${idle}s ago)"; $logColor = "Green" }
        elseif ($lockState -eq "BUSY") { $logLine = "$mb MB, NOT growing for ${idle}s - possible STALL"; $logColor = "Red" }
        else { $logLine = "$mb MB, idle ${idle}s"; $logColor = "DarkGray" }
    }

    $head = git log -1 --pretty=format:"%h %s" 2>$null
    if ($head -ne $lastCommit) {
        if ($lastCommit -ne "") { Add-Event "COMMIT $head" "Cyan" }
        $lastCommit = $head
    }

    $commitCount = (git log --since="30 minutes ago" --oneline 2>$null | Measure-Object -Line).Lines
    $activity = if ($commitCount -ge 4) { "ACTIVE" } elseif ($commitCount -ge 1) { "SLOW" } else { "NO COMMITS 30m" }
    $activityColor = if ($commitCount -ge 4) { "Green" } elseif ($commitCount -ge 1) { "Yellow" } else { "Red" }

    # --- draw ---
    Clear-Host
    Write-Host ""
    Write-Host "  $spin  SEAT MONITOR   $($now.ToString('HH:mm:ss'))   tick $tick" -ForegroundColor White
    Write-Host "  ================================================================" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  UNITY     " -NoNewline; Write-Host "$lockState  $lockDetail" -ForegroundColor $lockColor
    Write-Host "  run.log   " -NoNewline; Write-Host $logLine -ForegroundColor $logColor
    Write-Host "  ACTIVITY  " -NoNewline; Write-Host "$activity  ($commitCount commits in 30 min)" -ForegroundColor $activityColor
    Write-Host ""
    Write-Host "  HEAD      " -NoNewline; Write-Host $head -ForegroundColor Gray
    Write-Host ""
    Write-Host "  ---- recent events ----" -ForegroundColor DarkGray
    foreach ($e in $events) {
        Write-Host "  $($e.Time)  " -NoNewline -ForegroundColor DarkGray
        Write-Host $e.Text -ForegroundColor $e.Color
    }
    Write-Host ""
    Write-Host "  (Ctrl+C to stop - if the spinner freezes, the MONITOR died, not the room)" -ForegroundColor DarkGray

    Start-Sleep -Seconds $IntervalSeconds
}
