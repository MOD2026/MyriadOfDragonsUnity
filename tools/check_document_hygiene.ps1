[CmdletBinding()]
param(
    [string]$RepoRoot = ''
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $scriptRoot '..')).Path
}
$authority = Join-Path $RepoRoot 'docs/DOCUMENT_AUTHORITY_AND_STATUS_2026-09-16.md'
$index = Join-Path $RepoRoot 'handover/MD_CLEANUP_DECISION_INDEX_2026-09-16.md'
$map = Join-Path $RepoRoot 'handover/MD_CLEANUP_CONFLICT_MAP_2026-09-16.md'

$required = @($authority, $index, $map)
$missing = @($required | Where-Object { -not (Test-Path -LiteralPath $_) })
if ($missing.Count -gt 0) {
    $missing | ForEach-Object { Write-Error "Missing document-control file: $_" }
    exit 1
}

$authorityText = Get-Content -LiteralPath $authority -Raw
$controlText = $authorityText + "`n" + (Get-Content -LiteralPath $index -Raw) + "`n" + (Get-Content -LiteralPath $map -Raw)
$requiredClaims = @(
    'MOS_v1\.1\.md.*govern',
    'rc31 remains frozen',
    'rc32 remains a candidate',
    'blocker protocol',
    'DOCUMENT_AUTHORITY_AND_STATUS'
)
$missingClaims = @($requiredClaims | Where-Object { $controlText -notmatch $_ })
if ($missingClaims.Count -gt 0) {
    $missingClaims | ForEach-Object { Write-Error "Authority document is missing required control statement: $_" }
    exit 1
}

$ignore = Get-Content -LiteralPath (Join-Path $RepoRoot '.gitignore') -Raw
if ($ignore -notmatch '(?m)^/\.worktrees/\r?$') {
    Write-Error 'Nested .worktrees directory is not ignored.'
    exit 1
}

Write-Output 'Document hygiene: PASS'
Write-Output 'Authority, cleanup index, conflict map, release status, blocker protocol, and nested-worktree ignore rule are present.'
