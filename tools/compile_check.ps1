# Fast local compile check - seconds, no Unity, no .unity_batch.lock.
#
# WHY THIS EXISTS (2026-08-25): five compile breaks landed in this shared tree in one session, one
# of which blocked another seat mid-task. Every one was found the same expensive way - by launching
# a full Unity batch run, waiting ~3 minutes, and reading 'error CS' out of a 40MB log. Worse, a
# broken file blocks EVERY seat's ability to test, not just the author's, because Unity holds an
# exclusive project lock.
#
# Unity already generates .csproj files for every assembly, and the dotnet SDK on this machine
# compiles them directly. Measured: Runtime 9s, Tests.Editor 6s, versus ~180s for a Unity run that
# also takes the lock away from everyone else.
#
# This catches exactly what the ad-hoc text checks could NOT: missing `using` directives, wrong type
# names, bad signatures, unterminated strings, brace damage. It does NOT run tests - it answers one
# question, "does this tree still compile", which is the question that matters before you take the
# lock.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/compile_check.ps1
#   powershell -ExecutionPolicy Bypass -File tools/compile_check.ps1 -Projects MyriadOfDragons.Runtime
#
# Exit 0 = compiles. Exit 1 = real errors, printed. Run this BEFORE queueing for Unity.

param(
    [string]$ProjectPath = "C:\Users\zihan\Downloads\MyriadOfDragonsUnity",
    [string]$DotnetExe = "C:\Program Files\dotnet\dotnet.exe",
    [string[]]$Projects = @("MyriadOfDragons.Runtime", "MyriadOfDragons.Tests.Editor")
)

if (-not (Test-Path $DotnetExe)) {
    Write-Error "dotnet not found at $DotnetExe - cannot run a local compile check."
    exit 2
}

Push-Location $ProjectPath
try {
    $anyErrors = $false

    # --- Unindexed-file check, added 2026-08-25 after this script twice reported a confusing
    # --- CS0103/CS0246 for a brand-new file that was perfectly correct.
    #
    # Unity OWNS these .csproj files and lists every source file explicitly. A .cs file created
    # since Unity last refreshed is therefore not in the project at all, and the compiler reports
    # it as an undefined NAME at each use site - never as "file missing". The error points at the
    # caller, so the natural reading is "my new type is broken" when the real answer is "Unity has
    # not seen it yet".
    #
    # This fails SAFE either way (a missing file can only cause a false FAIL, never a false pass),
    # but a phantom error costs whoever chases it. Naming the real cause up front is the whole fix.
    # NOTE the EXACT paths. Assets/Tests also holds Assets/Tests/PlayMode, which is a SEPARATE
    # assembly with its own .csproj - scanning all of Assets/Tests reported 7 correctly-placed
    # PlayMode files as "unindexed" and would have sent someone to "fix" files that were fine.
    $indexMap = @{
        "MyriadOfDragons.Runtime"      = "Assets/Scripts"
        "MyriadOfDragons.Tests.Editor" = "Assets/Tests/Editor"
    }

    $unindexed = @()
    foreach ($proj in $Projects) {
        if (-not $indexMap.ContainsKey($proj)) { continue }
        $csprojPath = Join-Path $ProjectPath "$proj.csproj"
        if (-not (Test-Path $csprojPath)) { continue }

        $sourceRoot = Join-Path $ProjectPath $indexMap[$proj]
        if (-not (Test-Path $sourceRoot)) { continue }

        $csprojText = Get-Content $csprojPath -Raw
        foreach ($file in Get-ChildItem $sourceRoot -Recurse -Filter *.cs -File) {
            # Compare on filename: the csproj uses backslash-separated relative paths, and a
            # basename match is enough to tell "Unity knows about this file" from "it has never
            # been indexed". Duplicate basenames across folders would only make this quieter, not
            # wrong - it never reports a file that IS indexed.
            if ($csprojText -notmatch [regex]::Escape($file.Name)) {
                $unindexed += "$($file.Name)  ->  $proj"
            }
        }
    }

    if ($unindexed.Count -gt 0) {
        Write-Host ""
        Write-Host "NOTE  $($unindexed.Count) source file(s) exist on disk but are NOT in the .csproj:"
        $unindexed | Select-Object -Unique -First 10 | ForEach-Object { Write-Host "        $_" }
        Write-Host "      Unity regenerates the .csproj; until it does, every USE of a type in these"
        Write-Host "      files reports CS0103/CS0246 at the CALL SITE. If that is the only error"
        Write-Host "      below, the code is fine - refresh Unity, or add a <Compile Include>."
        Write-Host ""
    }

    foreach ($proj in $Projects) {
        $csproj = Join-Path $ProjectPath "$proj.csproj"
        if (-not (Test-Path $csproj)) {
            # Unity regenerates these; a missing one is worth saying out loud rather than skipping
            # silently, since a silent skip would make this check pass while verifying nothing.
            Write-Host "SKIP  $proj (no .csproj - open Unity once to regenerate)"
            continue
        }

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $output = & $DotnetExe build $csproj -v quiet --nologo 2>&1
        $sw.Stop()

        # Only real errors - warnings are pre-existing across this codebase and would drown the signal.
        $errors = $output | Select-String -Pattern ": error " | ForEach-Object { $_.ToString().Trim() }

        if ($errors.Count -gt 0) {
            $anyErrors = $true
            Write-Host ("FAIL  {0}  ({1:N1}s, {2} error(s))" -f $proj, $sw.Elapsed.TotalSeconds, $errors.Count)
            $errors | Select-Object -Unique -First 15 | ForEach-Object { Write-Host "        $_" }
        }
        else {
            Write-Host ("OK    {0}  ({1:N1}s)" -f $proj, $sw.Elapsed.TotalSeconds)
        }
    }

    if ($anyErrors) {
        Write-Host ""
        Write-Host "TREE DOES NOT COMPILE - fix before taking the Unity lock. A broken file blocks every seat."
        exit 1
    }

    Write-Host ""
    Write-Host "Tree compiles. Safe to queue for Unity."
    exit 0
}
finally {
    Pop-Location
}
