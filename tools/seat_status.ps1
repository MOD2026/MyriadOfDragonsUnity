# Seat status - answers "is a room running or idling?" from real evidence only.
#
# Every line below is derived from something on disk that a room cannot fake by
# claiming to be busy: the batch lock file, the live process table, the growth of
# run.log, and real git commits. No seat self-reports here.
#
#   powershell -ExecutionPolicy Bypass -File tools/seat_status.ps1

$ErrorActionPreference = "SilentlyContinue"
$repo = Split-Path -Parent $PSScriptRoot
$now = Get-Date

function Line($label, $value) { "{0,-22} {1}" -f $label, $value }

""
"=== SEAT STATUS  $($now.ToString('HH:mm:ss'))  ==="
""

# --- 1. Who holds Unity? -----------------------------------------------------
$lockPath = Join-Path $repo ".unity_batch.lock"
if (Test-Path $lockPath) {
    $lock = Get-Content $lockPath -Raw | ConvertFrom-Json
    $holder = Get-Process -Id $lock.pid -ErrorAction SilentlyContinue
    $age = [math]::Round(($now - [datetime]$lock.startedAt).TotalMinutes, 1)

    if ($holder) {
        Line "UNITY" "BUSY - a run is in progress"
        Line "  holder pid" "$($lock.pid) ($($holder.ProcessName))"
        Line "  running for" "$age min"
        if ($age -gt 20) {
            Line "  WARNING" "over 20 min - the wrapper kills stalls at 2 min of no log growth, so this is likely a genuinely long run"
        }
    }
    else {
        Line "UNITY" "STALE LOCK - pid $($lock.pid) is NOT running"
        Line "  meaning" "a run died without releasing. Safe to investigate; do not delete blindly."
        Line "  lock age" "$age min"
    }
}
else {
    Line "UNITY" "FREE - no run in progress, tree is available"
}

# --- 2. Is a run actually progressing, or hung? ------------------------------
$runLog = Join-Path $repo "run.log"
if (Test-Path $runLog) {
    $log = Get-Item $runLog
    $idle = [math]::Round(($now - $log.LastWriteTime).TotalSeconds, 0)
    $mb = [math]::Round($log.Length / 1MB, 1)
    Line "run.log" "$mb MB, last written $idle s ago"
    if ($idle -lt 30) { Line "  -> " "GROWING - the run is alive" }
    elseif (Test-Path $lockPath) { Line "  -> " "NOT growing while locked - possible stall" }
}

# --- 3. Last real numbers ----------------------------------------------------
$results = Join-Path $repo "results.xml"
if (Test-Path $results) {
    try {
        [xml]$xml = Get-Content $results
        $r = $xml.'test-run'
        Line "last results.xml" "total $($r.total)  passed $($r.passed)  failed $($r.failed)  skipped $($r.skipped)"
        Line "  written" (Get-Item $results).LastWriteTime.ToString('HH:mm:ss')
    }
    catch { Line "last results.xml" "present but unreadable (a run may be rewriting it now)" }
}
else {
    Line "last results.xml" "ABSENT - cleared by a run that has not finished writing"
}

# --- 4. Real recent work -----------------------------------------------------
""
"--- commits in the last 30 min (real work landing) ---"
Push-Location $repo
$since = $now.AddMinutes(-30).ToString("yyyy-MM-ddTHH:mm:ss")
$commits = git log --since=$since --pretty=format:"  %h  %ad  %s" --date=format:"%H:%M" 2>$null
if ($commits) { $commits } else { "  (none - no commits landed in the last 30 minutes)" }

""
"--- last mailbox exchange ---"
$mb = Join-Path $repo "tools/seat_mailbox.md"
if (Test-Path $mb) {
    $lastHeader = Select-String -Path $mb -Pattern '^\*\*\[(CC|VS) -> (CC|VS)\]' |
        Select-Object -Last 1
    if ($lastHeader) {
        "  line $($lastHeader.LineNumber): $($lastHeader.Line.Substring(0, [Math]::Min(110, $lastHeader.Line.Length)))"
        # Count the array length, not Measure-Object -Line: on this file the latter undercounts
        # (mixed CRLF), which produced a NEGATIVE "lines since" figure the first time this ran.
        $total = @(Get-Content $mb).Count
        $since = [Math]::Max(0, $total - $lastHeader.LineNumber)
        "  ($since lines written since that header, out of $total)"
    }
}
Pop-Location
""
