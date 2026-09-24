#!/usr/bin/env bun
/**
 * `bun run shot` — агентский CLI-глаз: рендерит страницу приложения
 * (мок-режим) или Storybook-стори, снимает PNG и печатает текстовый
 * отчёт обо всём, что человек увидел бы глазами (ошибки консоли, битые
 * запросы, горизонтальный скролл, обрезанный текст, aria-структуру).
 *
 * Команды:
 *   shot page <path...> [flags]       — реальные роуты приложения (мок-режим)
 *   shot story <story-id...> [flags]  — Storybook-стори
 *   shot stories [filter]             — список story id (подстрочный фильтр)
 *   shot routes                       — список URL-путей приложения
 *
 * Один браузер на весь запуск; сервер(ы) поднимаются один раз и всегда
 * останавливаются в finally / на SIGINT (см. lib/servers.ts — kill по
 * PID, никогда по имени процесса).
 */

import { chromium } from 'playwright';
import { APP_SERVER, STORYBOOK_SERVER, startServer } from './lib/servers';
import { listRoutes } from './lib/routes';
import { fetchStoryIndex, filterStories } from './lib/story-index';
import { parseShotArgs, themesFor } from './lib/cli-args';
import { paramPathResult, printResult, printSummary, writeLastSummary, writeReport, type RunResult, type Verdict } from './lib/report';
import { renderPageTarget } from './lib/page-target';
import { renderStoryTarget } from './lib/story-target';

type Cleanup = () => Promise<void> | void;
const cleanupFns: Cleanup[] = [];

async function cleanupAll(): Promise<void> {
  for (const fn of cleanupFns.splice(0).reverse()) {
    try {
      await fn();
    } catch (err) {
      console.error('[shot] cleanup step failed:', (err as Error).message);
    }
  }
}

function printUsage(): void {
  console.error(
    [
      'Usage:',
      '  bun run shot page <path...> [--theme light|dark|both] [--mobile] [--full] [--click sel] [--hover sel] [--fill "sel=value"] [--press Key] [--wait ms]',
      '  bun run shot story <story-id...> [--theme light|dark|both] [--mobile] [--full] [--click sel] ...',
      '  bun run shot stories [filter]',
      '  bun run shot routes',
    ].join('\n'),
  );
}

function cmdRoutes(): number {
  const routes = listRoutes();
  for (const r of routes) {
    let extra = '';
    if (r.hasParam) {
      extra = `  ${r.sample ? `sample: ${r.sample}` : '(needs param)'}`;
    }
    console.log(`${r.routePath.padEnd(28)} ${r.file}${extra}`);
  }
  console.log(`ROUTES: ${routes.length} routes`);
  return 0;
}

async function cmdStories(rest: readonly string[]): Promise<number> {
  const filter = rest.join(' ').trim() || undefined;
  const server = await startServer(STORYBOOK_SERVER);
  cleanupFns.push(() => server.stop());

  const index = await fetchStoryIndex(server.url);
  const filtered = filterStories(index, filter);
  for (const e of filtered) {
    console.log(`${e.id}  ${e.title}  ${e.importPath}`);
  }
  console.log(`STORIES: ${filtered.length} of ${index.length}${filter ? ` matching "${filter}"` : ''}`);
  return 0;
}

async function cmdPage(rest: readonly string[]): Promise<number> {
  let parsed;
  try {
    parsed = parseShotArgs(rest);
  } catch (err) {
    console.error((err as Error).message);
    printUsage();
    return 1;
  }
  if (parsed.positionals.length === 0) {
    printUsage();
    return 1;
  }

  const server = await startServer(APP_SERVER);
  cleanupFns.push(() => server.stop());
  const browser = await chromium.launch();
  cleanupFns.push(() => browser.close());

  const results: RunResult[] = [];
  const verdicts: Verdict[] = [];

  for (const target of parsed.positionals) {
    for (const theme of themesFor(parsed.theme)) {
      const result = target.includes('$')
        ? paramPathResult(target, theme, parsed.mobile)
        : await renderPageTarget(browser, server.url, target, theme, {
            full: parsed.full,
            mobile: parsed.mobile,
            steps: parsed.steps,
          });
      writeReport(result);
      verdicts.push(printResult(result));
      results.push(result);
    }
  }

  writeLastSummary(results);
  return printSummary(verdicts);
}

async function cmdStory(rest: readonly string[]): Promise<number> {
  let parsed;
  try {
    parsed = parseShotArgs(rest);
  } catch (err) {
    console.error((err as Error).message);
    printUsage();
    return 1;
  }
  if (parsed.positionals.length === 0) {
    printUsage();
    return 1;
  }

  const server = await startServer(STORYBOOK_SERVER);
  cleanupFns.push(() => server.stop());
  const index = await fetchStoryIndex(server.url);
  const browser = await chromium.launch();
  cleanupFns.push(() => browser.close());

  const results: RunResult[] = [];
  const verdicts: Verdict[] = [];

  for (const target of parsed.positionals) {
    for (const theme of themesFor(parsed.theme)) {
      const result = await renderStoryTarget(
        browser,
        server.url,
        target,
        theme,
        { full: parsed.full, mobile: parsed.mobile, steps: parsed.steps },
        index,
      );
      writeReport(result);
      verdicts.push(printResult(result));
      results.push(result);
    }
  }

  writeLastSummary(results);
  return printSummary(verdicts);
}

async function main(): Promise<number> {
  const [command, ...rest] = process.argv.slice(2);
  switch (command) {
    case 'routes':
      return cmdRoutes();
    case 'stories':
      return cmdStories(rest);
    case 'page':
      return cmdPage(rest);
    case 'story':
      return cmdStory(rest);
    default:
      printUsage();
      return 1;
  }
}

process.on('SIGINT', () => {
  void cleanupAll().finally(() => process.exit(130));
});

main()
  .then(async (code) => {
    await cleanupAll();
    process.exit(code);
  })
  .catch(async (err) => {
    console.error('[shot] fatal:', (err as Error).message);
    await cleanupAll();
    process.exit(1);
  });
