/**
 * .regentrc.ts — Plexor
 *
 * Project-level regent config. The global `~/.agents/rules/csharp/regent-rules/`
 * bundle auto-loads via regent's globalRulesPath (see v0.5.1 docs), so we
 * don't re-declare it here.
 *
 * What's local to Plexor:
 *   - `tools/audit/rules/plexor.csharp.*.lint.ts` — 5 project AST rules
 *     that mirror the prose in `.agents/rules/coding/*.md` and `AGENTS.md §1`.
 *   - `excludePaths[]` — skip generated / vendored / build-output dirs
 *     specific to Plexor's modular-monolith layout.
 *
 * Loading order (highest priority → lowest):
 *   1. CLI flags (`regent check --include ...`)
 *   2. `.regentrc.ts` (this file)
 *   3. The auto-discovered `tools/audit/rules/plexor.csharp.*.lint.ts`
 *      glob below (extends default; we list it explicitly so the discovery is
 *      visible to reviewers)
 *   4. Global bundle (`~/.agents/rules/csharp/regent-rules/`)
 *   5. Regent built-in defaults
 *
 * NOTE: Plexor already has a `.regentrc.yaml` from an earlier scaffold that
 * extended the central bundle. This `.regentrc.ts` supersedes it — see
 * dot-stbl/regent#110 (canary) for the bridge-validation reasoning.
 */
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
    // Plexor-specific exclusions
    '**/.idea/**',
    '**/.vscode/**',
    '**/.git/**',
  ],
});
