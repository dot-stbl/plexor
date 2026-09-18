#!/usr/bin/env bash
# ============================================================================
# acceptance-gate.sh — CI acceptance gate for the Plexor NodeAgent host.
# Wraps deploy/scripts/hardware-smoke.sh (which records per-phase
# timings) and asserts the SLA "VM boot + reachable < 30s" from P4-3.
#
# What "VM boot + reachable" means here:
#   The sum of three hardware-smoke phases — virt-install (define +
#   start) + boot (poll for 'running') + network (DHCP + ping). That
#   mirrors the user-visible latency from "create VM" → "guest
#   answers ping", the same metric the Plexor Host eventually emits
#   as plexor.vm.create.duration.
#
# Phases outside this sum (image-pull, qcow2-create, snapshot, restore,
# destroy) are still reported in the JSON output but their failure does
# NOT short-circuit the gate — the smoke script owns those errors via
# its own exit code, and `set -e` propagates them. The gate is purely
# the latency assertion on top.
#
# Output:
#   - Human-readable summary on stdout
#   - JSONL on stdout (one record per phase + a 'boot-to-reachable'
#     summary record); the same lines also go to a machine-readable
#     file at $PLEXOR_GATE_RESULTS_FILE if set (CI artifact).
#
# Optional env vars:
#   SLA_TARGET_SECONDS       Overrides the default 30s SLA.
#   SMOKE_SCRIPT             Override hardware-smoke.sh path; otherwise
#                            the gate tries (a) beside this script,
#                            (b) well-known CI staging path
#                            /tmp/plexor-smoke/hardware-smoke.sh.
#   PLEXOR_GATE_RESULTS_FILE If set, JSONL is also written here.
#
# Usage:
#   sudo ./acceptance-gate.sh
#   # CI:
#   ssh $TEST_VM_USER@$TEST_VM_HOST 'bash -s' < deploy/scripts/acceptance-gate.sh
#
# Exit codes:
#   0 — smoke passed AND boot+reachable within SLA
#   1 — smoke failed OR SLA exceeded
# ============================================================================

set -euo pipefail

SLA_TARGET_SECONDS="${SLA_TARGET_SECONDS:-30}"

# ---- locate the smoke script ----------------------------------------------
SMOKE_SCRIPT="${SMOKE_SCRIPT:-}"

resolve_smoke_script() {
    # Try (a) beside this script via $BASH_SOURCE — works when the
    # script is on a real filesystem path.
    local src="${BASH_SOURCE[0]:-}"
    if [[ -n "$src" && "$src" != "bash" && -f "$src" ]]; then
        local dir
        dir="$(cd "$(dirname "$src")" && pwd 2>/dev/null || true)"
        if [[ -n "$dir" && -x "$dir/hardware-smoke.sh" ]]; then
            echo "$dir/hardware-smoke.sh"
            return 0
        fi
    fi
    # (b) Well-known CI staging path. Set up by
    # .github/workflows/smoke-test.yml when running under the workflow.
    if [[ -x "/tmp/plexor-smoke/hardware-smoke.sh" ]]; then
        echo "/tmp/plexor-smoke/hardware-smoke.sh"
        return 0
    fi
    # (c) Repo-relative location (development on the controller box).
    if [[ -x "./hardware-smoke.sh" ]]; then
        echo "$(pwd)/hardware-smoke.sh"
        return 0
    fi
    return 1
}

if [[ -z "$SMOKE_SCRIPT" ]]; then
    SMOKE_SCRIPT="$(resolve_smoke_script || true)"
fi

if [[ -z "$SMOKE_SCRIPT" ]] || [[ ! -x "$SMOKE_SCRIPT" ]]; then
    echo "error: hardware-smoke.sh not found" >&2
    echo "       set SMOKE_SCRIPT env var, or place it beside this" >&2
    echo "       script, or at /tmp/plexor-smoke/hardware-smoke.sh" >&2
    exit 1
fi

# ---- run the smoke, capturing per-phase results ---------------------------
RESULTS_NDJSON="$(mktemp)"
trap 'rm -f "$RESULTS_NDJSON"' EXIT

# Run under set +e so we can capture the smoke's exit code without
# triggering our own set -e.
set +e
PLEXOR_SMOKE_RESULTS_FILE="$RESULTS_NDJSON" "$SMOKE_SCRIPT"
SMOKE_RC=$?
set -e

# ---- parse phase results --------------------------------------------------
# hardware-smoke.sh emits one JSON object per phase:
#   {"phase":"<name>","duration_ms":<n>,"passed":<0|1>,"note":"<text>"}
# We deliberately stay jq-free — the script is supposed to have only
# POSIX + curl + jq-equivalent shell builtins as deps. The grep+sed
# below handle a record with no escaped quotes (smoke's notes only
# contain phase descriptions with parens, no embedded quotes).
declare -A PHASE_MS=()
declare -A PHASE_NOTE=()
declare -A PHASE_PASSED=()
PHASE_ORDER=()

while IFS= read -r line || [[ -n "$line" ]]; do
    [[ -z "$line" ]] && continue
    # Strip leading/trailing whitespace defensively (smoke script's
    # emit is one record per line, no leading ws).
    line="${line#"${line%%[![:space:]]*}"}"
    line="${line%"${line##*[![:space:]]}"}"
    [[ -z "$line" ]] && continue

    # Key extraction — naive but sufficient for our narrow shape.
    phase="$(printf '%s' "$line" | sed -nE 's/.*"phase":"([^"]+)".*/\1/p')"
    duration_ms="$(printf '%s' "$line" | sed -nE 's/.*"duration_ms":([0-9]+).*/\1/p')"
    passed="$(printf '%s' "$line" | sed -nE 's/.*"passed":([01]).*/\1/p')"
    note="$(printf '%s' "$line" | sed -nE 's/.*"note":"([^"]*)".*/\1/p')"

    # Skip record if parsing failed (smoke's on-disk file should
    # never contain malformed lines, but defend against partial writes).
    [[ -z "$phase" || -z "$duration_ms" ]] && continue

    PHASE_MS[$phase]="$duration_ms"
    PHASE_PASSED[$phase]="$passed"
    PHASE_NOTE[$phase]="$note"
    PHASE_ORDER+=("$phase")
done < "$RESULTS_NDJSON"

if [[ ${#PHASE_ORDER[@]} -eq 0 ]]; then
    echo "error: no phase results parsed from $RESULTS_NDJSON" >&2
    echo "       smoke script may be too old, or produced no output" >&2
    exit 1
fi

# ---- SLA assertion --------------------------------------------------------
# SLA-relevant phases in order: virt-install -> boot -> network.
# Their sum represents "VM create + boot + reachable".
SLA_PHASES=(virt-install boot network)
for required in "${SLA_PHASES[@]}"; do
    if [[ -z "${PHASE_MS[$required]:-}" ]]; then
        echo "error: required SLA phase '$required' missing from results" >&2
        exit 1
    fi
done

SLA_SUM_MS=0
for p in "${SLA_PHASES[@]}"; do
    SLA_SUM_MS=$(( SLA_SUM_MS + PHASE_MS[$p] ))
done

# Float conversion for human-readable output.
sla_sum_s=$(awk -v ms="$SLA_SUM_MS" 'BEGIN { printf "%.2f", ms / 1000.0 }')
sla_target_s="$SLA_TARGET_SECONDS"
# Booleans as 0/1 for arithmetic.
sla_passed=$(awk -v s="$sla_sum_s" -v sla="$sla_target_s" \
    'BEGIN { print (s + 0 <= sla + 0) ? 1 : 0 }')

# ---- JSONL emission -------------------------------------------------------
# Per spec:
#   {"phase": "<name>", "duration_s": <float>, "sla_s": <int|null>,
#    "passed": <bool>, "note": "<text>"}
#
# sla_s is only meaningful for the 'boot-to-reachable' summary; for
# the individual phases we set it to the SLA_TARGET_SECONDS so the
# CI consumer can render any phase against the same threshold.
emit_record() {
    local phase="$1" duration_ms="$2" sla_s="$3" passed="$4" note="$5"
    local duration_s
    duration_s=$(awk -v ms="$duration_ms" 'BEGIN { printf "%.2f", ms / 1000.0 }')
    local passed_json="false"
    [[ "$passed" == "1" ]] && passed_json="true"
    local sla_json="null"
    [[ "$sla_s" != "null" ]] && sla_json="$sla_s"

    # Escape any double-quotes / backslashes inside note.
    local safe_note
    safe_note="${note//\\/\\\\}"
    safe_note="${safe_note//\"/\\\"}"

    local row
    row=$(printf '{"phase":"%s","duration_s":%s,"sla_s":%s,"passed":%s,"note":"%s"}' \
        "$phase" "$duration_s" "$sla_json" "$passed_json" "$safe_note")
    printf '%s\n' "$row"

    # Mirror to the artifact file (CI artifact), if set.
    if [[ -n "${PLEXOR_GATE_RESULTS_FILE:-}" ]]; then
        printf '%s\n' "$row" >> "$PLEXOR_GATE_RESULTS_FILE"
    fi
}

# Emit phases in smoke-script order. sla_s = $SLA_TARGET_SECONDS for all
# so CI dashboards can highlight; the actual decision is on the
# 'boot-to-reachable' record.
overall_passed=0
[[ "$SMOKE_RC" -eq 0 ]] && overall_passed=1

for p in "${PHASE_ORDER[@]}"; do
    emit_record "$p" "${PHASE_MS[$p]}" "$sla_target_s" \
        "${PHASE_PASSED[$p]}" "${PHASE_NOTE[$p]}"
done

# Summary record — the gate's authoritative decision.
emit_record "boot-to-reachable" "$SLA_SUM_MS" "$sla_target_s" \
    "$sla_passed" \
    "sum of virt-install + boot + network"

# ---- final verdict --------------------------------------------------------
if [[ "$SMOKE_RC" -eq 0 ]]; then
    if [[ "$sla_passed" -eq 1 ]]; then
        printf 'GATE PASS (smoke OK, boot-to-reachable %ss < %ss)\n' \
            "$sla_sum_s" "$sla_target_s" >&2
        exit 0
    else
        printf 'GATE FAIL (smoke OK but boot-to-reachable %ss >= %ss)\n' \
            "$sla_sum_s" "$sla_target_s" >&2
        exit 1
    fi
else
    printf 'GATE FAIL (smoke rc=%d, boot-to-reachable=%ss vs SLA=%ss)\n' \
        "$SMOKE_RC" "$sla_sum_s" "$sla_target_s" >&2
    exit 1
fi
