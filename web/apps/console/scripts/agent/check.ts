#!/usr/bin/env bun
/**
 * `bun run agent:check` — definition-of-done gate: typecheck → lint →
 * test → agent:rules → shot the UI you changed. Runs every step even
 * after a failure (so a weak agent sees the whole picture in one pass),
 * then prints exactly one final line: `AGENT-CHECK: PASS` or
 * `AGENT-CHECK: FAIL (<failed steps>)`.
 */

import { chromium } from 'playwright';
import { listChangedFiles } from './lib/changed-files';
import { routePathForRouteFile, SAMPLES } from './lib/routes';
import { APP_SERVER, ROOT, STORYBOOK_SERVER, startServer } from './lib/servers';
import { fetchStoryIndex, type StoryIndexEntry } from './lib/story-index';
import { renderPageTarget } from './lib/page-target';
import { renderStoryTarget } from './lib/story-target';
import { printResult, printSummary, writeLastSummary, writeReport, type RunResult, type Verdict } from './lib/report';

/** Стопы серверов/браузера из шага shot — вызываются и в finally, и на
 *  SIGINT (см. `.agents/rules/process/agent-runtime-safety.md`: kill
 *  только по точному PID, никогда по имени процесса — это уже гарантирует
 *  `servers.ts`; здесь только гарантируем, что stop() вызовется вообще). */
const activeStops: Array<() => void> = [];
process.on('SIGINT', () => {
  for (const stop of activeStops.splice(0)) {
    try {
      stop();
    } catch {
      // best effort — процесс всё равно завершается ниже
    }
  }
  process.exit(130);
});

interface StepOutcome {
  readonly name: string;
  readonly ok: boolean;
  /** Короткая деталь для итоговой строки, например "2 errors". Пусто при ok. */
  readonly detail: string;
}

function tail(text: string, n: number): string {
  const lines = text.split('\n');
  if (lines.length <= n) return text.trim();
  return lines.slice(-n).join('\n').trim();
}

function runCommand(name: string, cmd: readonly string[]): StepOutcome {
  console.log(`\n=== ${name} ===`);
  const res = Bun.spawnSync({
    cmd: [...cmd],
    cwd: ROOT,
    stdout: 'pipe',
    stderr: 'pipe',
    env: { ...process.env, FORCE_COLOR: '0' },
  });
  const out = `${res.stdout.toString('utf8')}\n${res.stderr.toString('utf8')}`.trim();
  const ok = res.exitCode === 0;
  console.log(tail(out, ok ? 5 : 60) || '(no output)');
  console.log(ok ? `--- ${name}: OK ---` : `--- ${name}: FAILED (exit ${res.exitCode}) ---`);
  return { name, ok, detail: ok ? '' : describeFailure(name, out) };
}

/** Best-effort однострочная деталь для итоговой строки FAIL (...). */
function describeFailure(name: string, out: string): string {
  if (name === 'typecheck') {
    const n = (out.match(/: error TS\d+:/g) ?? []).length;
    return n > 0 ? `${n} errors` : 'failed';
  }
  if (name === 'lint') {
    const m = /(\d+)\s+problems?/.exec(out);
    return m ? `${m[1]} problems` : 'failed';
  }
  if (name === 'test') {
    const m = /(\d+)\s+failed/.exec(out);
    return m ? `${m[1]} failed` : 'failed';
  }
  if (name === 'agent:rules') {
    const m = /RULES: (\d+) violations/.exec(out);
    return m ? `${m[1]} violations` : 'failed';
  }
  if (name === 'check:domains') {
    const m = /DOMAIN-BOUNDARIES: (\d+) violations/.exec(out);
    return m ? `${m[1]} violations` : 'failed';
  }
  return 'failed';
}

interface ParsedCheckArgs {
  readonly pages: string[];
  readonly stories: string[];
}

function parseCheckArgs(argv: readonly string[]): ParsedCheckArgs {
  const pages: string[] = [];
  const stories: string[] = [];
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--page') {
      i += 1;
      const v = argv[i];
      if (v) pages.push(v);
    } else if (argv[i] === '--story') {
      i += 1;
      const v = argv[i];
      if (v) stories.push(v);
    }
  }
  return { pages, stories };
}

/** Изменённые route-файлы → конкретные URL для шота; `$param`-роуты без
 *  известного sample id — заметка, не таргет. */
function pageTargetsFromChangedRoutes(changed: readonly string[]): { targets: string[]; notes: string[] } {
  const targets = new Set<string>();
  const notes: string[] = [];
  for (const file of changed) {
    const routePath = routePathForRouteFile(file);
    if (routePath === null) continue;
    if (routePath.includes('$')) {
      const sample = SAMPLES[routePath];
      if (sample) targets.add(sample);
      else notes.push(`skip ${routePath} (${file}) — no sample id in SAMPLES map; pass --page <concrete-url> to shot it`);
    } else {
      targets.add(routePath);
    }
  }
  return { targets: Array.from(targets), notes };
}

/** Изменённые *.stories.tsx → story id, сматченные по importPath из
 *  Storybook index.json (один файл — часто несколько историй). */
function storyTargetsFromChangedFiles(
  changed: readonly string[],
  index: readonly StoryIndexEntry[],
): { targets: string[]; notes: string[] } {
  const targets = new Set<string>();
  const notes: string[] = [];
  for (const file of changed) {
    if (!file.endsWith('.stories.tsx')) continue;
    const importPath = `./${file}`;
    const matches = index.filter((e) => e.importPath === importPath);
    if (matches.length === 0) {
      notes.push(`skip ${file} — no matching story id in Storybook index (importPath mismatch, or Storybook not yet rebuilt)`);
      continue;
    }
    for (const m of matches) targets.add(m.id);
  }
  return { targets: Array.from(targets), notes };
}

async function runShotStep(pageArgs: readonly string[], storyArgs: readonly string[]): Promise<StepOutcome> {
  console.log('\n=== shot ===');
  const changed = listChangedFiles();
  const { targets: routePages, notes: routeNotes } = pageTargetsFromChangedRoutes(changed);
  const pageTargets = Array.from(new Set([...pageArgs, ...routePages]));

  const storyFilesChanged = changed.some((f) => f.endsWith('.stories.tsx'));
  const needsStoryIndex = storyFilesChanged || storyArgs.length > 0;

  let storyIndex: StoryIndexEntry[] = [];
  let storybookUrl: string | null = null;
  let stopStorybook: (() => void) | null = null;
  if (needsStoryIndex) {
    const server = await startServer(STORYBOOK_SERVER);
    storybookUrl = server.url;
    stopStorybook = server.stop;
    activeStops.push(server.stop);
    storyIndex = await fetchStoryIndex(server.url);
  }

  const { targets: routeStories, notes: storyNotes } = storyTargetsFromChangedFiles(changed, storyIndex);
  const storyTargets = Array.from(new Set([...storyArgs, ...routeStories]));

  for (const note of [...routeNotes, ...storyNotes]) console.log(`  note: ${note}`);

  if (pageTargets.length === 0 && storyTargets.length === 0) {
    console.log('  note: nothing UI changed (no route/story files in the diff) — step skipped');
    console.log('--- shot: OK (skipped) ---');
    if (stopStorybook) stopStorybook();
    return { name: 'shot', ok: true, detail: '' };
  }

  let appUrl: string | null = null;
  let stopApp: (() => void) | null = null;
  if (pageTargets.length > 0) {
    const server = await startServer(APP_SERVER);
    appUrl = server.url;
    stopApp = server.stop;
    activeStops.push(server.stop);
  }

  const browser = await chromium.launch();
  activeStops.push(() => void browser.close());
  const results: RunResult[] = [];
  const verdicts: Verdict[] = [];
  try {
    for (const target of pageTargets) {
      if (appUrl === null) continue;
      for (const theme of ['light', 'dark'] as const) {
        const result = await renderPageTarget(browser, appUrl, target, theme, { full: false, mobile: false, steps: [] });
        writeReport(result);
        verdicts.push(printResult(result));
        results.push(result);
      }
    }
    for (const target of storyTargets) {
      if (storybookUrl === null) continue;
      for (const theme of ['light', 'dark'] as const) {
        const result = await renderStoryTarget(browser, storybookUrl, target, theme, { full: false, mobile: false, steps: [] }, storyIndex);
        writeReport(result);
        verdicts.push(printResult(result));
        results.push(result);
      }
    }
  } finally {
    await browser.close();
    if (stopApp) stopApp();
    if (stopStorybook) stopStorybook();
  }

  writeLastSummary(results);
  const exitCode = printSummary(verdicts);
  const failCount = verdicts.filter((v) => v === 'FAIL').length;
  return { name: 'shot', ok: exitCode === 0, detail: failCount > 0 ? `${failCount} fail` : 'failed' };
}

async function main(): Promise<number> {
  const { pages, stories } = parseCheckArgs(process.argv.slice(2));

  const steps: StepOutcome[] = [];
  steps.push(runCommand('typecheck', ['bun', 'run', 'typecheck']));
  steps.push(runCommand('lint', ['bun', 'run', 'lint']));
  steps.push(runCommand('test', ['bun', 'run', 'test']));
  steps.push(runCommand('agent:rules', ['bun', 'run', 'agent:rules']));
  steps.push(runCommand('check:domains', ['bun', 'run', 'check:domains']));
  steps.push(await runShotStep(pages, stories));

  const failed = steps.filter((s) => !s.ok);
  if (failed.length === 0) {
    console.log('\nAGENT-CHECK: PASS');
    return 0;
  }
  const detail = failed.map((s) => `${s.name}: ${s.detail}`).join(', ');
  console.log(`\nAGENT-CHECK: FAIL (${detail})`);
  return 1;
}

main()
  .then((code) => process.exit(code))
  .catch((err) => {
    console.error('[agent:check] fatal:', (err as Error).message);
    process.exit(1);
  });
