#!/usr/bin/env bash
# scripts/format.sh — dotnet format verify wrapper.
#
# Replaces the VerifyFormatOnBuild target deleted from Plexor.Build.Tools.
# Run from anywhere — resolves repo root from $0. Exit 0 = clean.
#
# MIRROR of .regentrc.ts excludePaths (single source of truth).
# When adding an entry to regent's excludePaths, also add it here.
#
# Usage:
#   bash scripts/format.sh              # verify (CI gate, default)
#   bash scripts/format.sh --fix        # apply fixes in-place (inner-loop)
#   bash scripts/format.sh --dry-run    # print the dotnet format command, don't run
#   bash scripts/format.sh --help

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

# MIRROR of .regentrc.ts excludePaths — canonical 8 .cs-pattern paths.
# node_modules/dist/.planning/.idea/.vscode/.git are irrelevant to
# dotnet format (it only operates on .cs files within plexor.slnx).
EXCLUDE_PATHS=(
    '**/Migrations/**.cs'
    '**/*ModelSnapshot.cs'
    '**/*.Designer.cs'
    '**/obj/**.cs'
    '**/bin/**.cs'
    '**/Generated/**.cs'
    '**/*.g.cs'
    '**/*.AssemblyAttributes.cs'
)
# Same diagnostics the old .targets excluded. RCS1141/RCS1140 are
# intentionally `none` in .editorconfig but `--severity hidden` re-enables
# them — exclude explicitly. RCS1021 / MA0136 kept for parity.
EXCLUDE_DIAGNOSTICS=(RCS1141 RCS1140 RCS1021 MA0136)

VERIFY=1
DRY_RUN=0
for arg in "$@"; do
    case "$arg" in
        --fix)        VERIFY=0 ;;
        --dry-run)    DRY_RUN=1 ;;
        --help|-h)
            sed -n '2,16p' "$0"
            exit 0
            ;;
        *) echo "Unknown arg: $arg (use --help)" >&2; exit 2 ;;
    esac
done

CMD=(dotnet format plexor.slnx --severity hidden --no-restore)
if [[ $VERIFY -eq 1 ]]; then
    CMD+=(--verify-no-changes)
fi
for p in "${EXCLUDE_PATHS[@]}"; do CMD+=(--exclude "$p"); done
for d in "${EXCLUDE_DIAGNOSTICS[@]}"; do CMD+=(--exclude-diagnostics "$d"); done

echo "+ ${CMD[*]}"
if [[ $DRY_RUN -eq 1 ]]; then exit 0; fi
exec "${CMD[@]}"