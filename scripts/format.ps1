# scripts/format.ps1 — dotnet format verify wrapper.
# Windows counterpart of scripts/format.sh.
#
# Replaces the VerifyFormatOnBuild target deleted from Plexor.Build.Tools.
# Exit 0 = clean.
#
# MIRROR of .regentrc.ts excludePaths (single source of truth).
# When adding an entry to regent's excludePaths, also add it here.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\format.ps1            # verify (CI gate)
#   powershell -ExecutionPolicy Bypass -File scripts\format.ps1 -Fix       # apply fixes in-place
#   powershell -ExecutionPolicy Bypass -File scripts\format.ps1 -DryRun    # print command, don't run
#   powershell -ExecutionPolicy Bypass -File scripts\format.ps1 -Help

[CmdletBinding()]
param(
    [switch]$Fix,
    [switch]$DryRun,
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Resolve-Path (Join-Path $ScriptDir '..')
Push-Location $Root
try {
    # MIRROR of .regentrc.ts excludePaths — canonical 8 .cs-pattern paths.
    # See format.sh for the rationale on which globs are excluded.
    $ExcludePaths = @(
        '**/Migrations/**.cs'
        '**/*ModelSnapshot.cs'
        '**/*.Designer.cs'
        '**/obj/**.cs'
        '**/bin/**.cs'
        '**/Generated/**.cs'
        '**/*.g.cs'
        '**/*.AssemblyAttributes.cs'
    )
    # Same diagnostics the old .targets excluded — see format.sh.
    $ExcludeDiagnostics = @('RCS1141', 'RCS1140', 'RCS1021', 'MA0136')

    if ($Help) {
        Get-Content (Join-Path $ScriptDir 'format.sh') -TotalCount 16 | Select-Object -Skip 1
        return
    }

    $cmd = @('dotnet', 'format', 'plexor.slnx', '--severity', 'hidden', '--no-restore')
    if (-not $Fix) { $cmd += '--verify-no-changes' }
    foreach ($p in $ExcludePaths)      { $cmd += @('--exclude', $p) }
    foreach ($d in $ExcludeDiagnostics) { $cmd += @('--exclude-diagnostics', $d) }

    Write-Host "+ $($cmd -join ' ')"
    if ($DryRun) { return }
    & $cmd[0] @($cmd[1..($cmd.Count - 1)])
    if ($LASTEXITCODE -ne 0) { throw "dotnet format exited $LASTEXITCODE" }
} finally {
    Pop-Location
}