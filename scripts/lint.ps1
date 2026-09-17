# scripts/lint.ps1 — regent lint gate.
# Windows counterpart of scripts/lint.sh.
#
# Runs `regent check` with the project-level config (.regentrc.ts).
# Excludes are read from .regentrc.ts excludePaths (single source of truth).
# Exit non-zero on errors.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\lint.ps1                # default scope: src
#   powershell -ExecutionPolicy Bypass -File scripts\lint.ps1 -Scope all    # also check tests/
#   powershell -ExecutionPolicy Bypass -File scripts\lint.ps1 -Scope tests  # tests only
#   powershell -ExecutionPolicy Bypass -File scripts\lint.ps1 -Help

[CmdletBinding()]
param(
    [string]$Scope = 'src',
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Resolve-Path (Join-Path $ScriptDir '..')
Push-Location $Root
try {
    if ($Help) {
        Get-Content (Join-Path $ScriptDir 'lint.sh') -TotalCount 16 | Select-Object -Skip 1
        return
    }
    Write-Host "+ regent check --scope $Scope --all --no-color"
    & regent check --scope $Scope --all --no-color
    if ($LASTEXITCODE -ne 0) { throw "regent check exited $LASTEXITCODE" }
} finally {
    Pop-Location
}