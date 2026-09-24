/**
 * Общий формат отчёта для `shot page` / `shot story` — вердикт, markdown-
 * файл и строка в stdout. Формат stdout зафиксирован (его читает слабая
 * модель) — см. AGENTS-facing CLI-контракт в scratch-плане задачи,
 * менять форму строк нельзя без согласования.
 *
 * Issues are deduped by (severity, code, message) before printing/writing —
 * identical issues collapse into one line with a `(×N)` suffix. stdout and
 * `.shots/LAST.md` cap at MAX_STDOUT_ISSUES deduped lines per target (then
 * `… N more, see report`); the per-target `.md` report always keeps the
 * full deduped list (no cap) so nothing is silently lost.
 */

import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, relative } from 'node:path';
import type { Issue } from './inspect';
import { ROOT } from './servers';

/** Max deduped issue lines printed to stdout per target — see module doc. */
const MAX_STDOUT_ISSUES = 10;

interface DedupedIssue {
  readonly issue: Issue;
  readonly count: number;
}

/** Groups issues with the same (severity, code, message) — first-seen order. */
function dedupeIssues(issues: readonly Issue[]): DedupedIssue[] {
  const order: string[] = [];
  const byKey = new Map<string, DedupedIssue>();
  for (const issue of issues) {
    const key = `${issue.severity}|${issue.code}|${issue.message}`;
    const existing = byKey.get(key);
    if (existing === undefined) {
      byKey.set(key, { issue, count: 1 });
      order.push(key);
    } else {
      byKey.set(key, { issue: existing.issue, count: existing.count + 1 });
    }
  }
  // Every key in `order` was just set above, so this is always present.
  return order.map((key) => byKey.get(key)!);
}

function formatIssueLine(d: DedupedIssue): string {
  const suffix = d.count > 1 ? ` (×${d.count})` : '';
  return `[${d.issue.severity} ${d.issue.code}] ${d.issue.message}${suffix}`;
}

export type Verdict = 'PASS' | 'WARN' | 'FAIL';

export interface RunResult {
  readonly kind: 'page' | 'story';
  /** Что просили отрендерить — путь страницы или id стори. */
  readonly target: string;
  readonly url: string;
  readonly theme: 'light' | 'dark';
  readonly mobile: boolean;
  readonly issues: readonly Issue[];
  readonly aria: string;
  readonly visibleText: string;
  /** Абсолютный путь к PNG, или null для валидационных ошибок без рендера
   *  (например, литеральный `$param` в пути — см. shot.ts). */
  readonly pngPath: string | null;
  /** Абсолютный путь к отчёту .md (пишется этим модулем), или null. */
  readonly mdPath: string | null;
}

export function verdictOf(issues: readonly Issue[]): Verdict {
  if (issues.some((i) => i.severity === 'FAIL')) return 'FAIL';
  if (issues.length > 0) return 'WARN';
  return 'PASS';
}

function relToRoot(absPath: string | null): string {
  if (absPath === null) return '(skipped)';
  return relative(ROOT, absPath).split('\\').join('/');
}

function themeLabel(r: RunResult): string {
  return r.mobile ? `${r.theme}, mobile` : r.theme;
}

/** Пишет `<slug>.md` рядом с PNG. Возвращает относительный путь (для stdout).
 *  No-op (returns '(skipped)') when `r.mdPath` is null — validation-only
 *  results (e.g. a literal `$param` in a page path) never rendered. */
export function writeReport(r: RunResult): string {
  if (r.mdPath === null) return '(skipped)';
  mkdirSync(dirname(r.mdPath), { recursive: true });
  const verdict = verdictOf(r.issues);
  const relPng = relToRoot(r.pngPath);
  const lines: string[] = [];

  lines.push(`# ${r.kind} ${r.target} (${themeLabel(r)})`, '');
  lines.push(`Open this PNG to see the render: ${relPng}`, '');
  lines.push(`- target: ${r.target}`);
  lines.push(`- url: ${r.url}`);
  lines.push(`- viewport: ${r.mobile ? '390x844 (mobile)' : '1280x800 (desktop)'}`);
  lines.push(`- theme: ${r.theme}`);
  lines.push(`- verdict: ${verdict}`, '');

  lines.push('## Issues');
  if (r.issues.length === 0) {
    lines.push('(none)');
  } else {
    // Full deduped list — no cap here (stdout is capped, this file is not).
    for (const d of dedupeIssues(r.issues)) {
      lines.push(`- ${formatIssueLine(d)}`);
      lines.push(`  fix: ${d.issue.hint}`);
    }
  }
  lines.push('');

  lines.push('## Structure (aria)', '```', r.aria.trimEnd(), '```', '');
  lines.push('## Visible text', '```', r.visibleText.trimEnd(), '```', '');

  writeFileSync(r.mdPath, lines.join('\n'));
  return relPng;
}

/** Печатает результат в контрактном формате stdout (см. CLI-контракт).
 *  Issues are deduped (see module doc) and capped at MAX_STDOUT_ISSUES
 *  lines — the `.md` report (path printed on the summary line) always has
 *  the full deduped list. */
export function printResult(r: RunResult): Verdict {
  const verdict = verdictOf(r.issues);
  const relPng = relToRoot(r.pngPath);
  const relMd = relToRoot(r.mdPath);
  console.log(
    `${verdict}  ${r.kind} ${r.target} (${themeLabel(r)})  png: ${relPng}  report: ${relMd}`,
  );
  const deduped = dedupeIssues(r.issues);
  const shown = deduped.slice(0, MAX_STDOUT_ISSUES);
  for (const d of shown) {
    console.log(`  - ${formatIssueLine(d)}`);
    console.log(`    fix: ${d.issue.hint}`);
  }
  const hidden = deduped.length - shown.length;
  if (hidden > 0) {
    console.log(`  … ${hidden} more, see report`);
  }
  return verdict;
}

/** Результат-заглушка для случаев, когда до навигации даже не дошло
 *  (невалидный `$param`-путь, неизвестный id стори) — без PNG/отчёта. */
export function skippedResult(
  kind: 'page' | 'story',
  target: string,
  theme: 'light' | 'dark',
  mobile: boolean,
  issue: Issue,
): RunResult {
  return {
    kind,
    target,
    url: '(not navigated)',
    theme,
    mobile,
    issues: [issue],
    aria: '',
    visibleText: '',
    pngPath: null,
    mdPath: null,
  };
}

/** Результат для путей с литеральным `$param` — не рендерим вообще, сразу
 *  сообщаем, чем это плохо, и куда за подсказкой (см. `bun run shot routes`). */
export function paramPathResult(target: string, theme: 'light' | 'dark', mobile: boolean): RunResult {
  return skippedResult('page', target, theme, mobile, {
    severity: 'FAIL',
    code: 'param-path',
    message: `Path contains a raw '$param' placeholder: ${target}`,
    hint: "Use a concrete id instead of the route's $param segment. Run `bun run shot routes` to see sample URLs for parametrized routes.",
  });
}

/** Печатает итоговую сводку `SHOT: N pass, N warn, N fail` и возвращает exit code. */
export function printSummary(verdicts: readonly Verdict[]): number {
  const pass = verdicts.filter((v) => v === 'PASS').length;
  const warn = verdicts.filter((v) => v === 'WARN').length;
  const fail = verdicts.filter((v) => v === 'FAIL').length;
  console.log(`SHOT: ${pass} pass, ${warn} warn, ${fail} fail`);
  return fail > 0 ? 1 : 0;
}

/** Пишет `.shots/LAST.md` — сводка последнего прогона. Issues per target are
 *  deduped and capped like stdout (see module doc) — full detail is always
 *  in the per-target `.md` report linked on each line. */
export function writeLastSummary(results: readonly RunResult[]): void {
  const path = `${ROOT}/.shots/LAST.md`;
  mkdirSync(dirname(path), { recursive: true });
  const lines: string[] = ['# Last shot run', ''];
  for (const r of results) {
    const verdict = verdictOf(r.issues);
    lines.push(
      `- ${verdict} ${r.kind} ${r.target} (${themeLabel(r)}) — png: ${relToRoot(r.pngPath)}, report: ${relToRoot(r.mdPath)}`,
    );
    const deduped = dedupeIssues(r.issues);
    const shown = deduped.slice(0, MAX_STDOUT_ISSUES);
    for (const d of shown) {
      lines.push(`  - ${formatIssueLine(d)}`);
    }
    const hidden = deduped.length - shown.length;
    if (hidden > 0) {
      lines.push(`  … ${hidden} more, see report`);
    }
  }
  const verdicts = results.map((r) => verdictOf(r.issues));
  const pass = verdicts.filter((v) => v === 'PASS').length;
  const warn = verdicts.filter((v) => v === 'WARN').length;
  const fail = verdicts.filter((v) => v === 'FAIL').length;
  lines.push('', `SHOT: ${pass} pass, ${warn} warn, ${fail} fail`);
  writeFileSync(path, lines.join('\n'));
}
