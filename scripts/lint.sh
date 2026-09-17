#!/usr/bin/env bash
# scripts/lint.sh — regent lint gate.
#
# Runs `regent check` with the project-level config (.regentrc.ts).
# Excludes are read from .regentrc.ts excludePaths (single source of truth).
# Run from anywhere — resolves repo root from $0. Exit non-zero on errors.
#
# Usage:
#   bash scripts/lint.sh                # default scope: src
#   bash scripts/lint.sh --scope all    # also check tests/
#   bash scripts/lint.sh --scope tests  # tests only
#   bash scripts/lint.sh --help

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

SCOPE="src"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --scope) SCOPE="$2"; shift 2 ;;
        --scope=*) SCOPE="${1#*=}"; shift ;;
        --help|-h)
            sed -n '2,16p' "$0"
            exit 0
            ;;
        *) echo "Unknown arg: $1 (use --help)" >&2; exit 2 ;;
    esac
done

exec regent check --scope "$SCOPE" --all --no-color