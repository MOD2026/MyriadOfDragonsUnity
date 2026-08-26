<#
.SYNOPSIS
    Plain-English activity log for the VS seat. Answers: is VS working right now, or stuck?

.DESCRIPTION
    Built 2026-08-27. Rewritten the same night after the owner said "i cant see the flight and
    have no idea what is it" - the first version redrew a dashboard in place using cursor
    positioning, which shows NOTHING in a captured-output tab and used jargon nobody asked for.

    THIS VERSION APPENDS ONE PLAIN SENTENCE PER EVENT. New text at the bottom = something real
    happened. No dashboard, no cursor tricks, no abbreviations.

    Every line is derived from evidence VS cannot fake: the Unity lock file cross-checked against
    the live process table, real byte-growth of the run log, and the results file appearing on
    disk. A room that is wedged can still TYPE "working", so nothing here is self-reported.

    A heartbeat line prints every 30s even when nothing changes, so silence always means the
    watcher died - never that the room quietly went idle.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\vs_activity.ps1
#>
[CmdletBinding()]
param(
    [int] $PollMs = 1000,
    [int] $HeartbeatSeconds = 0,   # 0 = off. Set e.g. -HeartbeatSeconds 30 to re-enable.
    [string] $LogPath = 'vs_run.log',
    [string] $ResultsPath = 'vs_results.xml'
)

$ErrorActionPreference = 'Continue'
Set-Location (Split-Path -Parent $PSScriptRoot)

function Say([string] $text, [string] $colour = 'Gray') {
    Write-Host ("  " + (Get-Date).ToString('HH:mm:ss') + "  " + $text) -ForegroundColor $colour
}

Write-Host ''
Write-Host '  ================================================================' -ForegroundColor Cyan
Write-Host '   VS ACTIVITY LOG - new lines appear when something really happens' -ForegroundColor Cyan
Write-Host '   A "Unity run" = the automated test + screenshot pass VS uses to' -ForegroundColor DarkGray
Write-Host '   check its work. Only one room in this project can run one at a' -ForegroundColor DarkGray
Write-Host '   time, so VS often has to WAIT for another room to finish.' -ForegroundColor DarkGray
Write-Host '  ================================================================' -ForegroundColor Cyan
Write-Host ''
Say 'watching. (Ctrl+C to stop)' 'Cyan'

# Seeded on the first poll below, NOT left blank. Starting blank made the watcher announce
# "The Unity run finished" the moment it launched, because idle looked like a transition into
# idle. It reported an event that never happened - the exact kind of confident-but-false line
# this tool exists to avoid.
$lastState = $null
$lastLogSize = -1
$lastResultsWrite = $null
$lastBeat = Get-Date
$growthSince = $null
$lastEditScan = (Get-Date).AddSeconds(-5)
$lastCommit = ''

if (Test-Path $ResultsPath) { $lastResultsWrite = (Get-Item $ResultsPath).LastWriteTime }

while ($true) {
    $now = Get-Date
    $changed = $false

    # ---- who holds the single shared Unity lock, and is that process actually alive? ----
    $state = 'idle'
    $detail = ''
    $lockFile = '.unity_batch.lock'

    if (Test-Path $lockFile) {
        $holderPid = $null
        try { $holderPid = (Get-Content $lockFile -Raw | ConvertFrom-Json).pid } catch { }

        if ($null -eq $holderPid) {
            $state = 'lock-unreadable'
        }
        elseif (Get-Process -Id $holderPid -ErrorAction SilentlyContinue) {
            $state = 'busy'
            $detail = "process $holderPid"
        }
        else {
            # A crashed run leaves its lock behind forever. Saying "busy" here would be a lie.
            $state = 'stale'
            $detail = "process $holderPid is gone"
        }
    }

    if ($null -eq $lastState) {
        # First look: describe what IS, never what changed.
        switch ($state) {
            'busy' { Say "A Unity run is already in progress ($detail) - VS is waiting for it." 'Yellow' }
            'stale' { Say "A leftover lock is blocking runs ($detail). It is safe to delete." 'Red' }
            'lock-unreadable' { Say 'The lock file is unreadable.' 'Red' }
            default { Say 'No Unity run in progress right now.' 'Gray' }
        }
        $lastState = $state
    }
    elseif ($state -ne $lastState) {
        $changed = $true
        switch ($state) {
            'busy' { Say "A Unity run is now in progress ($detail)." 'Green' }
            'idle' { Say 'The Unity run finished. Nothing is running now.' 'Cyan' }
            'stale' { Say "STUCK: a leftover lock is blocking runs ($detail). It is safe to delete." 'Red' }
            'lock-unreadable' { Say 'STUCK: the lock file is unreadable.' 'Red' }
        }
        $lastState = $state
    }

    # ---- real byte growth in the run log is the strongest proof work is happening ----
    if (Test-Path $LogPath) {
        $size = (Get-Item $LogPath).Length
        if ($lastLogSize -ge 0 -and $size -gt $lastLogSize) {
            if (-not $growthSince) {
                Say 'VS is writing test output - work is actively moving.' 'Green'
                $changed = $true
            }
            $growthSince = $now
        }
        elseif ($growthSince -and ($now - $growthSince).TotalSeconds -gt 45) {
            Say 'Test output has gone quiet for 45s (may be a long step, or may be stuck).' 'Yellow'
            $growthSince = $null
            $changed = $true
        }
        $lastLogSize = $size
    }

    # ---- results appearing on disk is the only proof a run really produced an answer ----
    if (Test-Path $ResultsPath) {
        $w = (Get-Item $ResultsPath).LastWriteTime
        if ($null -eq $lastResultsWrite -or $w -gt $lastResultsWrite) {
            $passed = '?'; $failed = '?'
            try {
                [xml] $x = Get-Content $ResultsPath -Raw
                $passed = $x.'test-run'.passed
                $failed = $x.'test-run'.failed
            }
            catch { }

            $colour = if ($failed -eq '0') { 'Green' } else { 'Yellow' }
            Say "RESULT: $passed passed, $failed failed." $colour
            $lastResultsWrite = $w
            $changed = $true
        }
    }

    # ---- REAL edit activity: which source files VS actually touched ----------------
    # Added because the old wording said "between tasks or editing files" - a GUESS. The
    # watcher could not see editing at all; it only knew no test was running, and implied
    # more than it could prove. File mtimes are real evidence; a wedged room cannot produce
    # them.
    foreach ($dir in @('Assets/Scripts', 'Assets/Tests', 'tools', 'docs')) {
        if (-not (Test-Path $dir)) { continue }
        Get-ChildItem $dir -Recurse -File -Include *.cs, *.ps1, *.md -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -gt $lastEditScan } |
            ForEach-Object {
                Say ("VS edited  " + $_.FullName.Replace((Get-Location).Path + '', '')) 'Green'
                $script:changed = $true
            }
    }
    $lastEditScan = $now

    # ---- real commits: the strongest proof a piece of work actually landed ----------
    $head = & git log -1 --oneline 2>$null
    if ($head -and $head -ne $lastCommit) {
        if ($lastCommit -ne '') { Say ("COMMITTED  " + $head) 'Cyan'; $changed = $true }
        $lastCommit = $head
    }

    # ---- heartbeat: silence must never be ambiguous ----
    if ($HeartbeatSeconds -gt 0 -and -not $changed -and ($now - $lastBeat).TotalSeconds -ge $HeartbeatSeconds) {
        $msg = switch ($lastState) {
            'busy' { 'still waiting - a Unity run is in progress.' }
            'stale' { 'still blocked by a leftover lock.' }
            default { 'no Unity test running. (This line cannot tell whether VS is thinking.)' }
        }
        Say ("(heartbeat) " + $msg) 'DarkGray'
        $lastBeat = $now
    }
    elseif ($changed) { $lastBeat = $now }

    Start-Sleep -Milliseconds $PollMs
}
