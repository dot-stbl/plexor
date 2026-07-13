#!/usr/bin/env bash
# tools/review.sh — post-write self-review gate.
#
# Runs the same checks the rules ask the agent to perform, in one
# invocation. Designed for LLM agents: invoked after editing, before
# committing. Non-zero exit = at least one check failed.
#
# Usage:
#   bash tools/review.sh                 # full repo, build included
#   bash tools/review.sh --staged-only    # only files staged in git
#   bash tools/review.sh --format        # also run `dotnet format` verify
#
# The report goes to /tmp/plexor-review-$$.txt; the agent (or human) reads
# the file. `dotnet build plexor.slnx` is the canonical gate from the
# rules' .editorconfig — this script invokes it explicitly so the agent
# can run it without `cd`-ing to the repo root and typing out the args.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

STAGED_ONLY=0
RUN_FORMAT=0
for arg in "$@"; do
    case "$arg" in
        --staged-only) STAGED_ONLY=1 ;;
        --format) RUN_FORMAT=1 ;;
        *) echo "Unknown arg: $arg"; exit 2 ;;
    esac
done

REPORT="/tmp/plexor-review-$$.txt"
exec > "$REPORT" 2>&1

echo "================================================================"
echo "  PLEXOR POST-WRITE REVIEW"
echo "  $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "================================================================"
echo

FAIL=0
PASS=0

# ---- 1. dotnet format verify (only if --format) ----
if [[ $RUN_FORMAT -eq 1 ]]; then
    if dotnet format plexor.slnx --verify-no-changes --severity hidden >/tmp/plexor-format.log 2>&1; then
        echo "[1/8] FORMAT: PASS"
        PASS=$((PASS+1))
    else
        echo "[1/8] FORMAT: FAIL"
        cat /tmp/plexor-format.log
        FAIL=$((FAIL+1))
    fi
    echo
fi

# ---- 2. dotnet build (canonical gate) ----
if [[ $STAGED_ONLY -eq 0 ]]; then
    if dotnet build plexor.slnx -c Debug >/tmp/plexor-build.log 2>&1; then
        echo "[2/8] BUILD: PASS"
        PASS=$((PASS+1))
    else
        echo "[2/8] BUILD: FAIL"
        tail -50 /tmp/plexor-build.log
        FAIL=$((FAIL+1))
    fi
    echo
fi

# ---- 3. no-console-write (rule: .agents/rules/coding/no-console-write.md) ----
echo "[3/8] NO CONSOLE WRITE (rule: no-console-write.md)"
# Spectre.Console.AnsiConsole.Write is a separate, allowed API.
# Only raw Console.* / Debug.WriteLine are forbidden.
NOC=$(rg -c 'Console\.Write(Line)?\b|Console\.Out\.Write|System\.Diagnostics\.Debug\.Write' src/ --type cs 2>/dev/null | head -1)
if [[ -z "$NOC" ]]; then
    echo "  PASS — no hits"
    PASS=$((PASS+1))
else
    echo "  FAIL — $NOC line(s). Replace with ILogger<T>.LogXxx or remove."
    rg -n 'Console\.Write(Line)?\b|Console\.Out\.Write|System\.Diagnostics\.Debug\.Write' src/ --type cs
    FAIL=$((FAIL+1))
fi
echo

# ---- 4. no-this-qualifier (rule: .agents/rules/coding/no-this-qualifier.md) ----
echo "[4/8] NO THIS QUALIFIER (rule: no-this-qualifier.md)"
# Looks for the disambiguating this.<x> = <x> pattern. The 'this' qualifier
# is only needed when parameter and field share a name — rename one
# instead of reaching for the keyword.
NTQ=$(rg -c 'this\.\w+ = \w+;' src/ --type cs 2>/dev/null | head -1)
if [[ -z "$NTQ" ]]; then
    echo "  PASS — no hits"
    PASS=$((PASS+1))
else
    echo "  FAIL — $NTQ line(s). Rename the parameter or the field."
    rg -n 'this\.\w+ = \w+;' src/ --type cs
    FAIL=$((FAIL+1))
fi
echo

# ---- 5. cross-process-discriminators (rule: .agents/rules/coding/...) ----
echo "[5/8] CROSS-PROCESS DISCRIMINATORS (rule: cross-process-discriminators-are-strings.md)"
# Only flags PUBLIC enums that look like wire-format discriminators
# (*Kind, *Type, *Status, *State). Internal enums are domain-only.
CPD=$(rg -c '^public enum \w+(Kind|Type|Status|State)\b' src/ --type cs 2>/dev/null | head -1)
if [[ -z "$CPD" ]]; then
    echo "  PASS — no public *Kind/*Type/*Status/*State enums"
    PASS=$((PASS+1))
else
    echo "  POTENTIAL — $CPD line(s). Check if these cross the process boundary."
    rg -n '^public enum \w+(Kind|Type|Status|State)\b' src/ --type cs
    FAIL=$((FAIL+1))
fi
echo

# ---- 6. naming-and-types (rule: .agents/rules/coding/naming-and-types.md) ----
echo "[6/8] NAMING (rule: naming-and-types.md)"
echo "  - 'Dto' suffix on classes/records (excluding XML doc comments):"
DTO=$(rg -c 'class \w+Dto\b|record \w+Dto\b' src/ --type cs 2>/dev/null | head -1)
if [[ -z "$DTO" ]]; then
    echo "    PASS — no hits"
    PASS=$((PASS+1))
else
    echo "    FAIL — $DTO line(s) with 'Dto' suffix."
    rg -n 'class \w+Dto\b|record \w+Dto\b' src/ --type cs
    FAIL=$((FAIL+1))
fi
echo "  - Forbidden postfixes (*Model, *Impl, etc.) on public types:"
FORB=$(rg -c 'public (?:sealed )?(?:class|record) \w*(Model|Impl)\b' src/ --type cs 2>/dev/null | head -1)
if [[ -z "$FORB" ]]; then
    echo "    PASS — no hits"
    PASS=$((PASS+1))
else
    echo "    FAIL — $FORB line(s) with 'Model/Impl' suffix."
    rg -n 'public (?:sealed )?(?:class|record) \w*(Model|Impl)\b' src/ --type cs
    FAIL=$((FAIL+1))
fi
echo

# ---- 7. constructors-and-fields (rule: .agents/rules/coding/...) ----
echo "[7/8] CONSTRUCTOR SHAPE (rule: constructors-and-fields.md)"
echo "  - duplicate field: primary ctor param + private readonly same name:"
DUP=$(rg -c 'private readonly \w+ (_\w+|\w+) = \w+' src/ --type cs 2>/dev/null | head -1)
if [[ -z "$DUP" ]]; then
    echo "    PASS — no hits"
    PASS=$((PASS+1))
else
    echo "    POTENTIAL — $DUP line(s). Review for primary ctor duplication."
    rg -n 'private readonly \w+ (_\w+|\w+) = \w+' src/ --type cs
    FAIL=$((FAIL+1))
fi
echo

# ---- 8. async-and-tasks §6 (rule: .agents/rules/coding/async-and-tasks.md) ----
echo "[8/8] MEANINGLESS DISCARD (rule: async-and-tasks.md §6)"
echo "  - '_ = X()' on awaited / chained-return / expression / placeholder"
echo "    parameter is a meaningless discard — bare 'X()' suffices."
echo "    Three patterns checked:"
echo "      1. _ = await ...           (await already awaits synchronously)"
echo "      2. _ = services.Add*() / fluent chain  (return unused)"
echo "      3. _ = expression;        (computed, not consumed)"
DISC=$(rg -n '_ = ' src/ --type cs 2>/dev/null \
    | grep -vE '_ = (binary|hex|dec) |//|///|_ = \(' \
    | head -1)
if [[ -z "$DISC" ]]; then
    echo "    PASS — no hits"
    PASS=$((PASS+1))
else
    echo "    FAIL — see hits below. Each '_ = X()' should be plain 'X();'."
    rg -n '_ = ' src/ --type cs 2>/dev/null \
        | grep -vE '_ = (binary|hex|dec) |//|///|_ = \(' \
        | head -20
    FAIL=$((FAIL+1))
fi
echo

echo "================================================================"
echo "  RESULT: $PASS passed, $FAIL failed"
echo "  Report: $REPORT"
echo "================================================================"

# ---- 9. EF migrations must be tool-generated (rule: .agents/rules/coding/ef-migrations-are-tool-generated.md) ----
echo
echo "[9/9] EF MIGRATIONS (rule: ef-migrations-are-tool-generated.md)"
echo "  - Any staged change to src/**/Migrations/** must reference 'dotnet ef'"
echo "    (migrations add/remove/script). Hand-written or hand-edited migrations"
echo "    corrupt the model snapshot and break future EF Core migration tooling."
STAGED_MIG=$(git diff --cached --name-only 2>/dev/null | grep -E 'src/.*/Migrations/.*\.cs$' || true)
if [[ -z "$STAGED_MIG" ]]; then
    echo "    PASS — no staged migration changes"
    PASS=$((PASS+1))
else
    LAST_MSG=$(git log -1 --format='%s' 2>/dev/null)
    if echo "$LAST_MSG" | grep -qiE 'dotnet ef|migrations? (add|remove|script)|ef core migration'; then
        echo "    PASS — staged migrations accompanied by EF-tool commit message."
        PASS=$((PASS+1))
    else
        echo "    FAIL — staged migration files without EF-tool commit message:"
        echo "      $STAGED_MIG"
        echo "      Last commit: $LAST_MSG"
        echo "      Migrations must be generated via 'dotnet ef migrations add/remove'."
        echo "      If recovering from a hand-written migration, follow"
        echo "      ef-migrations-are-tool-generated.md recovery procedure."
        FAIL=$((FAIL+1))
    fi
fi
echo
echo "================================================================"
echo "  RESULT: $PASS passed, $FAIL failed"
echo "  Report: $REPORT"
echo "================================================================"
exit $FAIL
