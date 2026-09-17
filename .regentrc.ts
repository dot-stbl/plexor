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
  /**
   * excludePaths is the SINGLE SOURCE OF TRUTH for "files that aren't
   * linted, formatted, or reviewed". Mirrored in:
   *   - .editorconfig (Roslyn analyzer + ReSharper severity override)
   *   - scripts/format.{sh,ps1} (dotnet format --exclude)
   *   - scripts/lint.{sh,ps1}   (regent check --scope)
   * Keep all four in sync. If you add a new entry here, also add it to the
   * scripts' EXCLUDE_PATHS array (and the [**.cs] block in .editorconfig
   * if it's a generated-file pattern).
   */
  excludePaths: [
    // Canonical generated / build-output paths (canonical 8 — see old
    // Plexor.Build.Tools.targets VerifyFormatOnBuild target for origin).
    '**/Migrations/**',
    '**/*ModelSnapshot.cs',
    '**/*.Designer.cs',
    '**/obj/**',
    '**/bin/**',
    '**/Generated/**',
    '**/*.g.cs',
    '**/*.AssemblyAttributes.cs',
    // Plexor-specific exclusions (project hygiene / IDE metadata).
    '**/node_modules/**',
    '**/dist/**',
    '**/.planning/**',
    '**/.idea/**',
    '**/.vscode/**',
    '**/.git/**',
  ],
});
