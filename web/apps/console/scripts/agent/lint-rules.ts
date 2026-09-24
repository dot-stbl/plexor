#!/usr/bin/env bun
/**
 * `bun run agent:rules` — grep-based checks for house frontend
 * conventions that ESLint doesn't catch (DS tokens, banned icon libs,
 * raw form controls, per-route document titles, …). Default scope:
 * changed files (tracked diff vs HEAD + untracked), `--all` scans the
 * whole `src/` tree.
 *
 * Output: `file:line  [rule-id] message  → fix: ...`, one line per hit,
 * final line `RULES: N violations`. Exit 1 on any hit.
 */

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { listChangedFiles } from './lib/changed-files';
import { ROOT } from './lib/servers';

interface Violation {
  readonly file: string;
  readonly line: number;
  readonly ruleId: string;
  readonly message: string;
  readonly fix: string;
}

const SRC_DIR = join(ROOT, 'src');

/** Пути, не проверяемые НИ ОДНИМ правилом — генерённый код и тесты. */
const GLOBAL_SKIP = [/^src\/shared\/api\/src\//, /routeTree\.gen\.ts$/, /\.test\.[jt]sx?$/];

/** Доп. пропуск для цветовых правил — сама тема/токены. */
const COLOR_RULE_SKIP = [/^src\/index\.css$/, /^src\/shared\/lib\/themes\//];

const UNDER_SHARED_UI = /^src\/shared\/ui\//;

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
      else if (/\.tsx?$/.test(name)) acc.push(toRepoRelative(full));
    }
  };
  walk(SRC_DIR);
  return acc;
}

function candidateFiles(all: boolean): string[] {
  const files = all ? walkAllSrcFiles() : listChangedFiles();
  return files
    .filter((f) => /\.tsx?$/.test(f))
    .filter((f) => f.startsWith('src/'))
    .filter((f) => !GLOBAL_SKIP.some((re) => re.test(f)));
}

/** Прогоняет regex по каждой строке файла, зовёт `make` на каждое совпадение. */
function scanLines(
  content: string,
  re: RegExp,
  make: (match: RegExpExecArray, lineNo: number) => { message: string; fix: string } | null,
): Array<{ line: number; message: string; fix: string }> {
  const out: Array<{ line: number; message: string; fix: string }> = [];
  const flags = re.flags.includes('g') ? re.flags : `${re.flags}g`;
  const lines = content.split('\n');
  for (let i = 0; i < lines.length; i++) {
    const lineRe = new RegExp(re.source, flags);
    let m: RegExpExecArray | null;
    while ((m = lineRe.exec(lines[i])) !== null) {
      const found = make(m, i + 1);
      if (found) out.push({ line: i + 1, ...found });
      if (m[0].length === 0) lineRe.lastIndex += 1;
    }
  }
  return out;
}

interface Rule {
  readonly id: string;
  readonly appliesTo: (file: string) => boolean;
  readonly check: (file: string, content: string) => Violation[];
}

const TW_PALETTE_RE =
  /\b(text|bg|border|ring|fill|stroke|from|to|via)-(red|green|blue|yellow|orange|purple|pink|gray|slate|zinc|neutral|stone|emerald|lime|amber|sky|indigo|violet|rose|teal|cyan)-\d{2,3}\b/;

const rules: Rule[] = [
  {
    id: 'tw-palette-color',
    appliesTo: (f) => !COLOR_RULE_SKIP.some((re) => re.test(f)),
    check: (file, content) =>
      scanLines(content, TW_PALETTE_RE, (m) => ({
        message: `Hardcoded Tailwind palette color "${m[0]}"`,
        fix: 'Use a DS token utility instead (bg-ok-soft, text-err-ink, text-muted-foreground, border-border, …).',
      })).map((h) => ({ file, ruleId: 'tw-palette-color', ...h })),
  },
  {
    id: 'raw-color-literal',
    appliesTo: (f) => !COLOR_RULE_SKIP.some((re) => re.test(f)),
    // Требуем className/style/cn(/cva( на той же строке — иначе слишком
    // много ложных срабатываний (issue-номера, хэши коммитов в комментах).
    check: (file, content) =>
      scanLines(content, /#[0-9a-fA-F]{3,8}\b|oklch\(|rgb\(/, (m, lineNo) => {
        const line = content.split('\n')[lineNo - 1];
        if (!/className|style=|\bcn\(|\bcva\(/.test(line)) return null;
        return {
          message: `Raw color literal "${m[0]}" in a styling context`,
          fix: 'Use a DS token utility or CSS variable — never a hex/oklch/rgb literal in className/style.',
        };
      }).map((h) => ({ file, ruleId: 'raw-color-literal', ...h })),
  },
  {
    id: 'banned-icon-import',
    appliesTo: () => true,
    check: (file, content) =>
      scanLines(
        content,
        /from\s+['"](lucide-react|react-icons(?:\/[^'"]*)?|@phosphor-icons(?:\/[^'"]*)?|@heroicons(?:\/[^'"]*)?)['"]/,
        (m) => ({
          message: `Banned icon library import "${m[1]}"`,
          fix: 'Import from @nine-thirty-five/material-symbols-react/rounded/700 instead.',
        }),
      ).map((h) => ({ file, ruleId: 'banned-icon-import', ...h })),
  },
  {
    id: 'raw-form-control',
    appliesTo: (f) => !UNDER_SHARED_UI.test(f),
    check: (file, content) => {
      const patterns: Array<{ re: RegExp; name: string; replacement: string }> = [
        { re: /<select[\s/>]/, name: '<select>', replacement: 'Select / SimpleSelect' },
        { re: /<input[^>]*type=["']checkbox["']/, name: '<input type="checkbox">', replacement: 'Checkbox' },
        { re: /<input[^>]*type=["']radio["']/, name: '<input type="radio">', replacement: 'RadioGroup' },
        { re: /<button[\s/>]/, name: '<button>', replacement: 'Button' },
      ];
      const out: Violation[] = [];
      for (const p of patterns) {
        for (const h of scanLines(content, p.re, () => ({
          message: `Raw form control ${p.name} outside src/shared/ui/**`,
          fix: `Use the DS primitive (${p.replacement}) from src/shared/ui/primitives instead.`,
        }))) {
          out.push({ file, ruleId: 'raw-form-control', ...h });
        }
      }
      return out;
    },
  },
  {
    id: 'join-render',
    appliesTo: (f) => f.endsWith('.tsx') && !UNDER_SHARED_UI.test(f),
    check: (file, content) =>
      scanLines(content, /\.join\(\s*['"],\s*['"]\s*\)/, (m) => ({
        message: `Collection rendered via "${m[0]}" instead of chips`,
        fix: 'Render each item as a <Badge> (or StatusPill) instead of joining into a comma sentence.',
      })).map((h) => ({ file, ruleId: 'join-render', ...h })),
  },
  {
    id: 'as-child-prop',
    appliesTo: () => true,
    check: (file, content) =>
      scanLines(content, /\basChild\b/, () => ({
        message: 'Radix-style "asChild" prop used',
        fix: 'This DS is base/react-aria — use the "render" prop instead of "asChild".',
      })).map((h) => ({ file, ruleId: 'as-child-prop', ...h })),
  },
  {
    id: 'web-document-titles',
    appliesTo: (f) =>
      /^src\/routes\/.*\.tsx$/.test(f) &&
      !f.endsWith('/route.tsx') &&
      !f.endsWith('__root.tsx') &&
      !f.endsWith('.stories.tsx'),
    check: (file, content) => {
      if (!/createFileRoute\(/.test(content)) return [];
      if (/routeHead\(|useDocumentTitle\(/.test(content)) return [];
      return [
        {
          file,
          line: 1,
          ruleId: 'web-document-titles',
          message: 'Route has no routeHead()/useDocumentTitle() — no document title set',
          fix: "Spread ...routeHead('Page name') into the route config, or call useDocumentTitle(name) for data-dependent titles.",
        },
      ];
    },
  },
];

function main(): number {
  const all = process.argv.includes('--all');
  const files = candidateFiles(all);
  const violations: Violation[] = [];

  for (const file of files) {
    const abs = join(ROOT, file);
    let content: string;
    try {
      content = readFileSync(abs, 'utf8');
    } catch {
      continue; // deleted/untracked-but-gone since the git listing was taken
    }
    for (const rule of rules) {
      if (!rule.appliesTo(file)) continue;
      violations.push(...rule.check(file, content));
    }
  }

  for (const v of violations) {
    console.log(`${v.file}:${v.line}  [${v.ruleId}] ${v.message}  → fix: ${v.fix}`);
  }
  console.log(`RULES: ${violations.length} violations`);
  return violations.length > 0 ? 1 : 0;
}

process.exit(main());
