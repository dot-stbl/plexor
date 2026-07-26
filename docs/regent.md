# Regent in Plexor

> **Single entry point for static analysis.** `regent check` runs
> every rule — the central C# bundle + 5 Plexor project rules — and
> emits one report.

This is the Plexor canary for `@dot-stbl/regent` v0.5.2 ([dot-stbl/regent#110](https://github.com/dot-stbl/regent/issues/110)).
The canary validates that "native tools first, regent for the gap" actually
holds on a real Plexor tree (.NET 10 + React 19 + ~280 production .cs files),
without forcing a one-day fix-up of every pre-existing violation.

## What regent does here

`regent` is a multi-mode static analysis framework that runs:

- **60 rules from the central C# bundle** at `~/.agents/rules/csharp/regent-rules/`
  (auto-loaded via the `globalRulesPath` fallback). These are the 60
  LLM-authored / curated rules covering the `.agents/rules/csharp/*.md`
  conventions.
- **5 Plexor project rules** under `tools/audit/rules/plexor.csharp.*.lint.ts`
  (loaded from `tools/audit/rules/`, the convention). Each rule mirrors a
  prose rule in `.agents/rules/coding/*.md` or `AGENTS.md §1`, so the
  doc-as-code pair is the executable + the explanation.

You get both layers in a single `regent check` run.

## Running it

```bash
# List every loaded rule + origin:
npx regent list

# Run all rules against the working tree (changed files only by default):
npx regent check

# Run all rules against every file (slower; the canary output uses this):
npx regent check --all

# Run a single rule family:
npx regent check --include-rules "plexor.*"

# Skip a noisy rule for one run:
npx regent check --exclude-rules "csharp.naming.private-underscore-prefix"

# Lower the severity threshold (default is "error"; use "warning" or "suggestion"):
npx regent check --exit-on warning

# Machine-readable:
npx regent check --format json > regent-audit.json
```

The canary did not wire `regent check` into the build gate. CI/build still
runs `dotnet build plexor.slnx -c Debug` and the format-gate.
Adding `regent check` to the build is a follow-up once the central
C# bundle is trimmed for Plexor-specific FP noise (see "Known gaps"
below).

## Config

The config is `.regentrc.ts` at the project root.

```ts
import { defineConfig } from '@dot-stbl/regent';

export default defineConfig({
  rules: {
    extends: [
      './tools/audit/rules/plexor.csharp.*.lint.ts',
    ],
  },
  excludePaths: [
    '**/node_modules/**',
    '**/dist/**',
    '**/obj/**',
    '**/bin/**',
    '**/.planning/**',
    '**/Migrations/**',
    '**/*.Designer.cs',
    '**/*.g.cs',
    '**/*.AssemblyAttributes.cs',
    '**/Generated/**',
    '**/.idea/**',
    '**/.vscode/**',
    '**/.git/**',
  ],
});
```

- The global C# bundle auto-loads via the `globalRulesPath` fallback;
  `rules.extends[]` is intentionally **project-local only**.
- `excludePaths` skips generated / vendored / build-output dirs
  specific to Plexor's modular-monolith layout.
- Loading order (highest priority first): CLI flags > `.regentrc.ts`
  > `tools/audit/rules/` discovery > global bundle > built-in defaults.

## The 5 Plexor project rules

Each rule lives in `tools/audit/rules/plexor.csharp.<topic>.lint.ts` and
mirrors a project-specific prose rule.

| Rule id | Kind | Severity | Mirrors |
|---|---|---|---|
| `plexor.no-console-write` | detect (RE2) | warning | `.agents/rules/coding/no-console-write.md` |
| `plexor.no-this-qualifier` | detect (RE2) | warning | `.agents/rules/coding/no-this-qualifier.md` |
| `plexor.controller-name-is-const` | detect (RE2) | warning | `.agents/rules/coding/controller-route-names.md` |
| `plexor.no-handrolled-crypto` | detect (RE2) | warning | `.agents/rules/coding/prefer-built-ins-over-hand-rolled.md` |
| `plexor.no-schema-name-in-csharp` | ast (tree-sitter-c-sharp) | warning | `AGENTS.md §1 — Naming` |

**Why only 5?** Per the canary brief: "Curated, 5 rules, plexor is the
source of truth for its own rules." The remainder of the project rules
in `.agents/rules/coding/*.md` either are already covered by the central
C# bundle (re-checked via Phase 1 of dot-stbl/regent#110) or are domain-
specific to features that aren't yet present in this codebase.

**Severity is `warning`** for all five. Each rule is forward-only:
existing violations stay until natural refactor; new violations fail
code review but not the build gate. Promote to `error` once each rule
is clean.

## Canary results (Plexor main branch)

Ran against plexor's `main` branch at canary time:

- 60 global C# rules + 5 Plexor project rules = **65 rules loaded**.
- **`npx regent check --all`** against the whole repo: **140 errors,
  577 warnings, 2 suggestions = 719 violations** total.
- Plexor project rules alone: **22 violations** across 3 rules.

| Rule | Findings |
|---|---|
| `plexor.controller-name-is-const` | 13 (ClustersController / NodeAgentController / AuthController) |
| `plexor.no-this-qualifier` | 2 (CommandDispatcher.cs:57, 58) |
| `plexor.no-handrolled-crypto` | 7 (X509Authority.cs:13,57,95,204 — primary offender; plus 3 others) |
| `plexor.no-console-write` | 0 (project follows the rule; Spectre.Console + Plexor.Installer.Cli are the documented exceptions and are excluded) |
| `plexor.no-schema-name-in-csharp` | 0 (project follows AGENTS.md §1; no naked schema words in class names) |

These are pre-existing — not introduced by the canary. Fixing them is a
follow-up PR. The full canary output is the canonical baseline for
"where Plexor stands against the central C# bundle today".

### Top 3 violations by file

1. `src/shared/security/Plexor.Shared.Mtls/X509Authority.cs` — 4 hits from
   `plexor.no-handrolled-crypto` (`RSA.Create`). Plexor's mTLS layer uses
   `RSA.Create(keySizeInBits: ...)` for cert lifecycle despite the
   `"prefer built-ins"` rule recommending `IDataProtector`. Either the
   rule needs a documented exception for cert / mTLS code, or the
   X509Authority needs to use DataProtection. **Follow-up**: AGENTS.md
   amendment or refactor.
2. `src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Api/Controllers/ClustersController.cs` — 7 hits from
   `plexor.controller-name-is-const` (every `[Http*(Name = "literal")]`
   attribute should reference a `file static class *RouteNames`).
   Same fix already done in WorkloadsController and IamControllers in
   commits `db19917`, `0213f48`. **Follow-up**: rename `<resource>-<verb>`
   to `*RouteNames.X` and rewrite `CreatedAtAction(...)` callers.
3. `src/agents/Plexor.NodeAgent/Composition/CommandDispatcher.cs` — 2 hits
   from `plexor.no-this-qualifier` at lines 57-58
   (`this.executors = byType;`, `this.logger = logger;`). Both inside the
   `CommandDispatcher(IDictionary<Type, ICommandExecutor> byType, ILogger<CommandDispatcher> logger)`
   ctor — rename `byType` → `executorsByType` to disambiguate. **Follow-up**:
   small rename PR.

## Known gaps

- **The 4 Plexor rules that target only RE2 patterns can't fully cover
  their prose rule.** They catch the common shape; edge cases (multi-line
  spans, type-prefixed shapes) are review territory. AST rules (the
  `no-schema-name-in-csharp` flavour) are more accurate but require a
  deeper authoring investment.
- **Pre-existing 14 format-drift violations on `main`** — not introduced
  by this PR. They make `dotnet build plexor.slnx -c Debug` fail on the
  format-gate step. Use `-p:DisableFormatOnBuild=true` for inner-loop work
  on this branch; a separate cleanup PR (or `dotnet format plexor.slnx --severity hidden`)
  is the proper unblock.
- **`regent` is not yet wired into the build gate.** Adding
  `npx regent check --all --exit-on warning` to `Plexor.Build.Tools.targets`
  is a separate decision — it depends on which warnings survive the
  Phase 2 cleanup, and which the central C# bundle trims as
  "native-tool-covers" (see dot-stbl/regent#110).
- **5 rules, not 20.** The brief said 5. The remaining 15+ rules in
  `.agents/rules/coding/*.md` are either covered by the central bundle,
  intended for features not yet in this codebase (Phase 2+), or scoped
  to specific modules that aren't worth a dedicated AST rule yet.

## Adding more rules

When a new house-rule emerges (new `.agents/rules/coding/*.md` doc),
the workflow is:

1. Create the `.md` doc with a self-audit grep + good/bad examples.
2. Hand-author a `.lint.ts` file in `tools/audit/rules/` matching the
   same `plexor.csharp.<topic>.lint.ts` filename pattern. Use
   `regent llm authoring detect` for the skeleton (RE2 detect) or
   `defineAstRule` for tree-sitter AST matching.
3. `npx regent list` to confirm the rule loads (kind = `detect` or
   `ast`, origin = `repo: tools/audit/rules/`).
4. `npx regent check --include-rules "plexor.<id>"` to confirm it
   matches the intended targets.
5. Commit a single rule per commit — do not bundle 5 new rules in one
   PR; let each land independently.

## References

- [ADR-0001: regent positioning — bridge, not linter](https://github.com/dot-stbl/regent/blob/main/docs/adr/0001-regent-positioning-bridge-not-linter.md) (dot-stbl/regent)
- [Issue #110: Plexor dogfood](https://github.com/dot-stbl/regent/issues/110) — the canary brief
- [Issue #86: TS dogfood precedent](https://github.com/dot-stbl/regent/issues/86)
- [Issue #84: bundle conformance harness](https://github.com/dot-stbl/regent/issues/84) — fixture coverage for the central C# bundle
- Central C# bundle: `~/.agents/rules/csharp/regent-rules/` (60 rules)
- Plexor rules: `.agents/rules/coding/` (project-specific doc rules)
