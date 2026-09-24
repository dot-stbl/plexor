#!/usr/bin/env bun
/**
 * `bun run check:domains` — belt-and-suspenders check for the frontend DDD
 * dependency rule (.agents/docs/architecture/frontend-ddd.md §3) that the
 * ESLint `no-restricted-imports` blocks in eslint.config.js can't fully
 * cover: a domain's `index.ts` barrel shape, and any deep cross-domain
 * import string the lint globs don't reach yet (e.g. a `.stories.tsx`).
 *
 * Two checks:
 *   1. Every domain's `index.ts` barrel is export-only — re-exports, type
 *      re-exports, a wildcard re-export, comments, blank lines. Anything else (a
 *      function/const body, a side-effecting statement) is flagged for
 *      manual review — the barrel should never grow logic of its own.
 *   2. No file anywhere under `src/` imports another domain by a deep path
 *      (`@/domains/<ctx>/{model,api,ui,mocks}/...`) instead of its barrel
 *      (`@/domains/<ctx>`).
 *
 * Output: `file:line  [rule-id] message`, one per hit, final line
 * `DOMAIN-BOUNDARIES: N violations`. Exit 1 on any hit, 0 (with a
 * "no domains yet" note) when `src/domains/` doesn't exist yet (steps 0-1
 * of the migration run before it does).
 */

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { ROOT } from './lib/servers';

interface Violation {
  readonly file: string;
  readonly line: number;
  readonly ruleId: string;
  readonly message: string;
}

const SRC_DIR = join(ROOT, 'src');
const DOMAINS_DIR = join(SRC_DIR, 'domains');

function toRepoRelative(absPath: string): string {
  return relative(ROOT, absPath).split('\\').join('/');
}

function walkAllSrcFiles(): string[] {
  const acc: string[] = [];
  const walk = (dir: string) => {
    for (const name of readdirSync(dir)) {
      const full = join(dir, name);
      const st = statSync(full);
      if (st.isDirectory()) walk(full);
      else if (/\.tsx?$/.test(name)) acc.push(full);
    }
  };
  walk(SRC_DIR);
  return acc;
}

/** A barrel line is "export-only" when it's a named re-export, a wildcard
 *  re-export, a type re-export, a line or block comment, or blank —
 *  anything else (a function/const body) is flagged. */
const BARREL_LINE_OK = /^\s*(export \{|export \*|export type|export default \{|\/\/|\/\*|\*|$)/;

/** True when a line opens a multi-line named-export list ("export {" or
 *  "export type {") without also closing it on the same line — the
 *  continuation lines (bare identifiers, the closing "} from '...';") are
 *  legitimate barrel content even though they don't themselves start with
 *  "export". */
const OPENS_MULTILINE_EXPORT = /^\s*export\s+(type\s+)?\{[^}]*$/;

function checkBarrelShapes(): Violation[] {
  if (!existsSync(DOMAINS_DIR)) return [];
  const violations: Violation[] = [];
  for (const ctx of readdirSync(DOMAINS_DIR)) {
    const ctxDir = join(DOMAINS_DIR, ctx);
    if (!statSync(ctxDir).isDirectory()) continue;
    const indexPath = join(ctxDir, 'index.ts');
    if (!existsSync(indexPath)) continue;
    const content = readFileSync(indexPath, 'utf8');
    const lines = content.split('\n');
    let inMultilineExport = false;
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      if (inMultilineExport) {
        // Inside a multi-line named-export list — every line here is
        // legitimate barrel content (identifiers, the closing brace/from
        // clause). The list ends at the line containing the closing "}".
        if (line.includes('}')) inMultilineExport = false;
        continue;
      }
      if (OPENS_MULTILINE_EXPORT.test(line)) {
        inMultilineExport = true;
        continue;
      }
      if (!BARREL_LINE_OK.test(line)) {
        violations.push({
          file: toRepoRelative(indexPath),
          line: i + 1,
          ruleId: 'barrel-not-export-only',
          message: `domains/${ctx}/index.ts has a non-export line — barrels are re-export-only, no logic.`,
        });
      }
    }
  }
  return violations;
}

const DEEP_IMPORT_RE = /from\s+['"]@\/domains\/[a-z-]+\/(model|api|ui|mocks)\/[^'"]*['"]/g;

function checkDeepImports(): Violation[] {
  const violations: Violation[] = [];
  for (const abs of walkAllSrcFiles()) {
    const content = readFileSync(abs, 'utf8');
    const lines = content.split('\n');
    for (let i = 0; i < lines.length; i++) {
      const re = new RegExp(DEEP_IMPORT_RE.source, 'g');
      let m: RegExpExecArray | null;
      while ((m = re.exec(lines[i])) !== null) {
        violations.push({
          file: toRepoRelative(abs),
          line: i + 1,
          ruleId: 'deep-domain-import',
          message: `Deep cross-domain import "${m[0].trim()}" — import via the domain's barrel (@/domains/<context>) instead.`,
        });
      }
    }
  }
  return violations;
}

function main(): number {
  if (!existsSync(DOMAINS_DIR)) {
    console.log('DOMAIN-BOUNDARIES: 0 violations (src/domains/ does not exist yet)');
    return 0;
  }

  const violations = [...checkBarrelShapes(), ...checkDeepImports()];

  for (const v of violations) {
    console.log(`${v.file}:${v.line}  [${v.ruleId}] ${v.message}`);
  }
  console.log(`DOMAIN-BOUNDARIES: ${violations.length} violations`);
  return violations.length > 0 ? 1 : 0;
}

process.exit(main());
