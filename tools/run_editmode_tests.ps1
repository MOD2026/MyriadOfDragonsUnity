# Timeout-guarded EditMode batch runner (HARDENED).
#
# Why this exists: on 2026-08-23 a batch run silently stalled inside SaveSystem.Save's file-write
# sequence and sat unnoticed for 50+ minutes with a live process but a dead log. This wrapper kills
# the run and says so instead of hanging forever unwatched, runs each class in its own short-lived
# Unity process to sidestep a deep-in-one-batch stall, and (2026-08-31 hardening) refuses to run
# unless a set of safety guards pass.
#
# Hardening added 2026-08-31 (AN-runner-harden, isolated worktree from b33a63f):
#   - ProjectPath defaults to the worktree that contains THIS script (never a hardcoded checkout).
#   - ResultsPath / LogPath / BatchOutDir must be relative and stay inside the worktree (absolute
#     paths and `..` escapes are rejected before anything runs).
#   - Refuses to start if Unity.exe OR UnityPackageManager.exe is already running.
#   - Refuses to start if a Git index.lock is present (a Git op is mid-flight).
#   - Stale-output cleanup is guarded (a failed delete is a hard error, never a silent reuse).
#   - Unity launch is guarded (a failed Start-Process is a hard error, not a null-ref later).
#   - `error CS` and `Aborting batchmode` in the log are hard failures (exit 5), not reminders.
#   - Missing / malformed / zero-test results XML is a hard failure (exit 3), never read as green.
#   - `-quit` is never added, and the arg list is asserted to not contain it.
#   - `-SelfTest` exercises every guard/validator with fixtures and launches no Unity.
#
# Exit codes: 0 ok · 1 preflight/guard refusal · 3 XML missing/malformed/zero-test ·
#             5 compiler error / batch abort · 124 timed out/stalled and killed (treat as FAILED).
#
# Usage:
#   powershell -File tools/run_editmode_tests.ps1
#   powershell -File tools/run_editmode_tests.ps1 -TestFilter "MyriadOfDragons.Tests.BattleLogicTests"
#   powershell -File tools/run_editmode_tests.ps1 -ClassListFile tools/batch_classes.txt -BatchOutDir batch_out
#   powershell -File tools/run_editmode_tests.ps1 -SelfTest      # no Unity; validates guards only

param(
    # Empty => derived from the worktree containing this script (see $WorktreeRoot below).
    [string]$ProjectPath = "",
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe",
    [string]$ResultsPath = "results.xml",
    [string]$LogPath = "run.log",
    [int]$TimeoutMinutes = 25,
    [int]$StallCheckSeconds = 120,
    [string]$TestFilter = "",
    [string[]]$TestFilters = @(),
    [string]$ClassListFile = "",
    [string]$BatchOutDir = "batch_out",
    [string]$NamespacePrefix = "MyriadOfDragons.Tests.",
    [ValidateSet("EditMode","PlayMode")]
    [string]$TestPlatform = "EditMode",
    [switch]$SelfTest
)

# ---------------------------------------------------------------------------
# Worktree root: derived from THIS script's location (tools/ -> parent). This is what makes the
# runner safe to use from an isolated `git worktree` - it never defaults to a shared checkout.
# ---------------------------------------------------------------------------
$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$WorktreeRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = $WorktreeRoot
}
else {
    $ProjectPath = (Resolve-Path $ProjectPath).Path
}

# ---------------------------------------------------------------------------
# Guard / validator functions (pure enough to unit-test via -SelfTest, no Unity needed).
# ---------------------------------------------------------------------------

# Reject absolute paths and `..` escapes; return the resolved absolute path when contained.
function Assert-RelativeContained {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Value,
        [Parameter(Mandatory)][string]$Root
    )
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "$Name must not be empty." }
    if ([System.IO.Path]::IsPathRooted($Value)) {
        throw "$Name must be a relative path inside the worktree; got absolute path '$Value'."
    }
    $rootFull = [System.IO.Path]::GetFullPath($Root)
    $rootWithSep = $rootFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $full = [System.IO.Path]::GetFullPath((Join-Path $rootFull $Value))
    if ($full -ne $rootFull -and -not $full.StartsWith($rootWithSep, [System.StringComparison]::Ordinal)) {
        throw "$Name escapes the worktree: '$Value' resolves to '$full' outside '$rootFull'."
    }
    return $full
}

# Concurrent-Unity guard: any running Unity.exe or UnityPackageManager.exe.
# The leading comma forces a real array return even when empty (PowerShell otherwise unrolls an
# empty collection to $null on return, which would make .Count blow up at the call site).
function Get-ConcurrentUnityProcesses {
    return ,@(Get-Process -Name "Unity","UnityPackageManager" -ErrorAction SilentlyContinue)
}

# Resolve the correct Git index.lock path even inside a linked worktree.
function Get-GitIndexLockPath {
    param([Parameter(Mandatory)][string]$Root)
    try {
        $p = (& git -C $Root rev-parse --git-path index.lock 2>$null)
        if ($LASTEXITCODE -eq 0 -and $p) {
            if ([System.IO.Path]::IsPathRooted($p)) { return $p }
            return (Join-Path $Root $p)
        }
    }
    catch { }
    return (Join-Path $Root ".git/index.lock")
}

# Delete stale output safely: a failed delete is a hard error, never a silent reuse of stale data.
function Invoke-GuardedRemove {
    param([Parameter(Mandatory)][string]$Path)
    if (Test-Path $Path) {
        try { Remove-Item $Path -Force -ErrorAction Stop }
        catch { throw "Failed to remove stale output '$Path': $($_.Exception.Message)" }
        if (Test-Path $Path) { throw "Stale output still present after removal: '$Path'." }
    }
}

# Return the log lines that prove a compiler error or batch abort (hard-failure evidence).
function Get-LogHardErrors {
    param([Parameter(Mandatory)][string]$LogPath)
    if (-not (Test-Path $LogPath)) { return @() }
    return @(Select-String -Path $LogPath -SimpleMatch -Pattern "error CS","Aborting batchmode" -ErrorAction SilentlyContinue)
}

# Validate a results XML: missing / malformed / no <test-run> / zero tests all throw.
function Get-ValidatedResults {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path $Path)) { throw "Results XML missing: '$Path'." }
    try { $xml = [xml](Get-Content $Path -Raw -ErrorAction Stop) }
    catch { throw "Results XML malformed: '$Path' ($($_.Exception.Message))." }
    $run = $xml.'test-run'
    if ($null -eq $run) { throw "Results XML has no <test-run> root: '$Path'." }
    $count = [int]$run.testcasecount
    if ($count -eq 0) { throw "Zero tests executed (testcasecount=0): '$Path' - not a passing run." }
    return [pscustomobject]@{ Total = $count; Passed = [int]$run.passed; Failed = [int]$run.failed; Skipped = [int]$run.skipped }
}

# Never allow -quit alongside -runTests (it silently no-ops the whole run).
function Assert-NoQuit {
    param([Parameter(Mandatory)][string[]]$UnityArgs)
    if ($UnityArgs -contains "-quit") {
        throw "Refusing to run: '-quit' must never be passed with -runTests (it silently no-ops)."
    }
}

# ---------------------------------------------------------------------------
# -SelfTest: exercise every guard/validator with fixtures. Launches NO Unity.
# ---------------------------------------------------------------------------
if ($SelfTest) {
    $fails = 0
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("runner_selftest_" + [guid]::NewGuid().ToString("N"))
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

    Write-Host "=== run_editmode_tests.ps1 -SelfTest (no Unity) ==="

    # 2. absolute / escaping output paths rejected; relative accepted & contained.
    #    (Use a native-OS absolute path so this is correct on both Windows PowerShell and pwsh.)
    $nativeAbs = [System.IO.Path]::GetFullPath((Join-Path $tmp "abs_results.xml"))
    Expect-Throw "reject absolute output path"    { Assert-RelativeContained -Name "ResultsPath" -Value $nativeAbs -Root $tmp }
    Expect-Throw "reject .. escape"               { Assert-RelativeContained -Name "BatchOutDir" -Value "../escape" -Root $tmp }
    Check        "accept relative contained path" { $r = Assert-RelativeContained -Name "ResultsPath" -Value "results.xml" -Root $tmp; if (-not $r.StartsWith((Resolve-Path $tmp).Path)) { throw "not contained: $r" } }

    # 3. concurrent-process guard returns an array (0 on a clean host).
    Check "concurrent-Unity probe returns array" { $p = Get-ConcurrentUnityProcesses; if ($null -eq $p) { throw "null" } }

    # 4. Git index.lock detection (simulated fake git dir, real .git untouched).
    Check "git index.lock detected when present" {
        $fakeRoot = Join-Path $tmp "fakerepo"; New-Item -ItemType Directory -Force -Path (Join-Path $fakeRoot ".git") | Out-Null
        $lock = Join-Path $fakeRoot ".git/index.lock"; Set-Content -Path $lock -Value "x"
        $detected = Get-GitIndexLockPath -Root $fakeRoot
        if (-not (Test-Path $detected)) { throw "did not resolve/detect the lock at $detected" }
    }

    # 5. guarded stale-output removal (happy path removes; missing path is a no-op).
    Check "guarded remove deletes stale output" {
        $f = Join-Path $tmp "stale.xml"; Set-Content -Path $f -Value "old"
        Invoke-GuardedRemove -Path $f
        if (Test-Path $f) { throw "stale file still present" }
        Invoke-GuardedRemove -Path (Join-Path $tmp "does_not_exist.xml")  # must not throw
    }

    # 6. compiler-error / abort log scan.
    Check "log hard-error scan finds error CS" {
        $lg = Join-Path $tmp "cs.log"; Set-Content -Path $lg -Value @("ok","Assets\X.cs(1,1): error CS0103: bad","done")
        if ((Get-LogHardErrors -LogPath $lg).Count -eq 0) { throw "error CS not detected" }
    }
    Check "log hard-error scan finds Aborting batchmode" {
        $lg = Join-Path $tmp "abort.log"; Set-Content -Path $lg -Value @("start","Aborting batchmode due to failure")
        if ((Get-LogHardErrors -LogPath $lg).Count -eq 0) { throw "Aborting batchmode not detected" }
    }
    Check "clean log has no hard errors" {
        $lg = Join-Path $tmp "clean.log"; Set-Content -Path $lg -Value @("compiling","tests done")
        if ((Get-LogHardErrors -LogPath $lg).Count -ne 0) { throw "false positive on clean log" }
    }

    # 7. results XML validation: missing / malformed / zero-test throw; valid returns counts.
    Expect-Throw "missing XML rejected"   { Get-ValidatedResults -Path (Join-Path $tmp "nope.xml") }
    Expect-Throw "malformed XML rejected"  { $b = Join-Path $tmp "bad.xml"; Set-Content -Path $b -Value "<not-closed>"; Get-ValidatedResults -Path $b }
    Expect-Throw "zero-test XML rejected"  { $z = Join-Path $tmp "zero.xml"; Set-Content -Path $z -Value '<test-run testcasecount="0" passed="0" failed="0" skipped="0"></test-run>'; Get-ValidatedResults -Path $z }
    Check        "valid XML returns counts" {
        $v = Join-Path $tmp "ok.xml"; Set-Content -Path $v -Value '<test-run testcasecount="83" passed="83" failed="0" skipped="0"></test-run>'
        $r = Get-ValidatedResults -Path $v; if ($r.Total -ne 83 -or $r.Failed -ne 0) { throw "bad parse: $($r.Total)/$($r.Failed)" }
    }

    # 10 (guard). -quit rejection.
    Expect-Throw "reject -quit in args" { Assert-NoQuit -UnityArgs @("-batchmode","-runTests","-quit") }
    Check        "accept args without -quit" { Assert-NoQuit -UnityArgs @("-batchmode","-runTests","-testPlatform","EditMode") }

    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host ""
    if ($fails -eq 0) { Write-Host "SELFTEST: all guard checks passed."; exit 0 }
    else { Write-Host "SELFTEST: $fails guard check(s) FAILED."; exit 1 }
}

# ---------------------------------------------------------------------------
# Preflight guards (run in order, all BEFORE any Unity launch or lock creation).
# ---------------------------------------------------------------------------

# (1)/(2)/(10-outputs) validate output paths are relative and contained in the worktree.
$resultsFull = Assert-RelativeContained -Name "ResultsPath" -Value $ResultsPath -Root $ProjectPath
$logFull     = Assert-RelativeContained -Name "LogPath"     -Value $LogPath     -Root $ProjectPath
$null        = Assert-RelativeContained -Name "BatchOutDir"  -Value $BatchOutDir  -Root $ProjectPath

# (3) refuse if Unity or UnityPackageManager is already running.
$concurrent = Get-ConcurrentUnityProcesses
if ($concurrent.Count -gt 0) {
    $names = ($concurrent | ForEach-Object { "$($_.ProcessName)#$($_.Id)" }) -join ", "
    Write-Error "Refusing to start: Unity/UnityPackageManager already running ($names). Close it (exclusive project lock)."
    exit 1
}

# (4) refuse if a Git operation is mid-flight (index.lock present).
$gitIndexLock = Get-GitIndexLockPath -Root $ProjectPath
if (Test-Path $gitIndexLock) {
    Write-Error "Refusing to start: Git index.lock present at '$gitIndexLock' (a Git operation is in progress). Do not run mid-commit/mid-rebase."
    exit 1
}

# Unity executable must exist (guarded launch precondition).
if (-not (Test-Path $UnityExe)) {
    Write-Error "Unity.exe not found at '$UnityExe'."
    exit 1
}

function Invoke-SingleRun {
    param(
        [string]$ResultsPath,
        [string]$LogPath,
        [string]$TestFilter,
        [int]$TimeoutMinutes,
        [int]$StallCheckSeconds,
        [string[]]$MultiFilter = @()
    )

    $resultsFull = Assert-RelativeContained -Name "ResultsPath" -Value $ResultsPath -Root $ProjectPath
    $logFull     = Assert-RelativeContained -Name "LogPath"     -Value $LogPath     -Root $ProjectPath
    Invoke-GuardedRemove -Path $logFull
    Invoke-GuardedRemove -Path $resultsFull

    $unityArgs = @(
        "-batchmode", "-projectPath", $ProjectPath,
        "-runTests", "-testPlatform", $TestPlatform,
        "-testResults", $ResultsPath, "-logFile", $LogPath
    )
    if ($MultiFilter.Count -gt 0) {
        # Unity does NOT OR repeated -testFilter flags; it takes a semicolon-separated list.
        $unityArgs += @("-testFilter", ($MultiFilter -join ';'))
        Write-Host "Multi-filter run ($($MultiFilter.Count) filters, one shared Unity process): $($MultiFilter -join ', ')"
    }
    elseif ($TestFilter -ne "") {
        $unityArgs += @("-testFilter", $TestFilter)
    }

    Assert-NoQuit -UnityArgs $unityArgs

    # (6) guarded launch: a failed Start-Process is a hard error, not a null-ref later.
    try {
        $proc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -WorkingDirectory $ProjectPath -PassThru -ErrorAction Stop
    }
    catch {
        Write-Error "Failed to launch Unity ('$UnityExe'): $($_.Exception.Message)"
        return 1
    }
    if ($null -eq $proc) {
        Write-Error "Failed to launch Unity ('$UnityExe'): Start-Process returned no process object."
        return 1
    }

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
            Write-Host "STALLED: log has not grown in $([int]$stalledSeconds)s (last size $lastLogLength bytes) - killing early."
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

    Write-Host "Unity exited with code $($proc.ExitCode)."
    return $proc.ExitCode
}

# ---------------------------------------------------------------------------
# Cross-seat lock: claim .unity_batch.lock atomically; release on exit.
# ---------------------------------------------------------------------------
$lockFile = Join-Path $ProjectPath ".unity_batch.lock"

function Try-AcquireUnityBatchLock {
    param([string]$Path, [int]$OwnerPid)
    try {
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $writer = New-Object System.IO.StreamWriter($stream)
            $json = @{ pid = $OwnerPid; owner = "coding-seat-batch-wrapper"; startedAt = (Get-Date).ToString("o") } | ConvertTo-Json
            $writer.Write($json)
            $writer.Flush()
        }
        finally { $stream.Dispose() }
        return $true
    }
    catch [System.IO.IOException] { return $false }
}

$lockWaitCeilingMinutes = 10
$lockDeadline = (Get-Date).AddMinutes($lockWaitCeilingMinutes)
$lockAttempt = 0
$lockAcquired = $false

while (-not $lockAcquired) {
    $lockAttempt++
    if (Try-AcquireUnityBatchLock -Path $lockFile -OwnerPid $PID) {
        # Re-check for a live interactive Editor after acquiring, so nothing slips in before launch.
        if ((Get-ConcurrentUnityProcesses).Count -gt 0) {
            Remove-Item $lockFile -Force -ErrorAction SilentlyContinue
            Write-Host "Unity started outside this lock (likely an interactive Editor) - releasing and waiting."
        }
        else { $lockAcquired = $true; break }
    }
    else {
        if (Test-Path $lockFile) {
            try {
                $lockInfo = Get-Content $lockFile -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
                $lockPid = $lockInfo.pid
                $lockStillAlive = $lockPid -and (Get-Process -Id $lockPid -ErrorAction SilentlyContinue)
                if (-not $lockStillAlive) {
                    Write-Host "Stale lock file found (owner process $lockPid no longer running) - reclaiming it."
                    Remove-Item $lockFile -Force -ErrorAction SilentlyContinue
                    continue
                }
            }
            catch { }
        }
    }

    if ((Get-Date) -ge $lockDeadline) {
        Write-Error "Starved waiting for the Unity batch lock after $lockAttempt attempts over $lockWaitCeilingMinutes minutes. Check $lockFile."
        exit 1
    }

    $baseDelaySeconds = [Math]::Min(30, [Math]::Pow(2, [Math]::Min($lockAttempt, 5)))
    $jitterSeconds = Get-Random -Minimum 0.0 -Maximum ($baseDelaySeconds * 0.5)
    Start-Sleep -Seconds ($baseDelaySeconds + $jitterSeconds)
}

try {

if ($ClassListFile -ne "") {
    if (-not (Test-Path $ClassListFile)) {
        Write-Error "ClassListFile not found: $ClassListFile"
        exit 1
    }

    $outDirFull = Assert-RelativeContained -Name "BatchOutDir" -Value $BatchOutDir -Root $ProjectPath
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

        $classLogFull = Join-Path $ProjectPath $classLog
        $hardErrors = Get-LogHardErrors -LogPath $classLogFull
        if ($exitCode -eq 124) {
            $stalledClasses += $class
            $summaryLines += "$class`tSTALLED`t-`t-`t-"
        }
        elseif ($hardErrors.Count -gt 0) {
            $errorClasses += $class
            $summaryLines += "$class`tCOMPILE_OR_ABORT`t-`t-`t-"
        }
        else {
            try {
                $r = Get-ValidatedResults -Path (Join-Path $ProjectPath $classResults)
                $totalCases += $r.Total
                $totalPassed += $r.Passed
                $totalFailed += $r.Failed
                $summaryLines += "$class`tOK`t$($r.Total)`t$($r.Passed)`t$($r.Failed)"
            }
            catch {
                $errorClasses += $class
                $summaryLines += "$class`tBAD_XML`t-`t-`t-"
            }
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
    Write-Host "Compile/abort/bad-XML classes ($($errorClasses.Count)): $($errorClasses -join ', ')"
    Write-Host "Per-class summary: $summaryPath"

    if ($stalledClasses.Count -gt 0 -or $errorClasses.Count -gt 0 -or $totalFailed -gt 0) { exit 1 }
    exit 0
}

$resolvedMulti = @()
if ($TestFilters.Count -gt 0) {
    $resolvedMulti = $TestFilters |
        ForEach-Object { $_ -split ',' } |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -ne '' } |
        ForEach-Object {
            if ($_ -like "$NamespacePrefix*") { $_ } else { "$NamespacePrefix$_" }
        }
}

$exitCode = Invoke-SingleRun -ResultsPath $ResultsPath -LogPath $LogPath -TestFilter $TestFilter -TimeoutMinutes $TimeoutMinutes -StallCheckSeconds $StallCheckSeconds -MultiFilter $resolvedMulti

if ($exitCode -eq 124) { exit 124 }

# (8) compiler errors / batch aborts are hard failures.
$hardErrors = Get-LogHardErrors -LogPath $logFull
if ($hardErrors.Count -gt 0) {
    Write-Error "HARD FAILURE: compiler error or batch abort in $LogPath -> $($hardErrors[0].Line.Trim())"
    exit 5
}

# (9) missing / malformed / zero-test XML is a hard failure.
try {
    $r = Get-ValidatedResults -Path $resultsFull
    Write-Host "Tests executed: $($r.Total) (passed $($r.Passed), failed $($r.Failed), skipped $($r.Skipped))"
    if ($r.Failed -gt 0) { exit 1 }
}
catch {
    Write-Error "HARD FAILURE: $($_.Exception.Message)"
    exit 3
}

exit $exitCode

}
finally {
    Remove-Item $lockFile -Force -ErrorAction SilentlyContinue
}
