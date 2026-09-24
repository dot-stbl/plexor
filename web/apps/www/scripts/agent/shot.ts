#!/usr/bin/env bun
/**
 * `bun run shot` — agent CLI eyes for `@plexor/www`: renders a real page
 * and prints a text report of everything a human would see (console
 * errors, broken requests, horizontal scroll, clipped text, aria
 * structure), plus the PNG itself.
 *
 * Commands:
 *   shot page <path...> [flags]   — real app routes
 *   shot routes                   — list the app's URL paths
 *
 * One browser for the whole run; the dev server starts once and is
 * always stopped in `finally` / on SIGINT (see lib/servers.ts — killed
 * by PID, never by process name).
 *
 * Adapted from `web/apps/console/scripts/agent/shot.ts`: dropped the
 * `story`/`stories` subcommands (www has no Storybook) and mock-mode env
 * (www is a public static site with no MSW layer).
 */

import { chromium } from 'playwright';
import { APP_SERVER, startServer } from './lib/servers';
import { listRoutes } from './lib/routes';
import { parseShotArgs, themesFor } from './lib/cli-args';
import { paramPathResult, printResult, printSummary, writeLastSummary, writeReport, type RunResult, type Verdict } from './lib/report';
import { renderPageTarget } from './lib/page-target';

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
      '  bun run shot page <path...> [--theme light|dark|both] [--mobile] [--full] [--motion] [--width px] [--click sel] [--hover sel] [--fill "sel=value"] [--press Key] [--wait ms] [--scroll px]',
      '  bun run shot routes',
      '',
      '  --motion         Emulate prefers-reduced-motion: no-preference (default: reduce).',
      '  --width px       Override the desktop viewport width (default 1280). Ignored under --mobile.',
      '  --scroll px      Scroll to an absolute Y offset, then a short settle wait.',
      '                   Repeatable, ordered with --wait/--click/etc. (e.g. checking a',
      '                   scroll-scrubbed section mid-animation: --motion --scroll 2600).',
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
            motion: parsed.motion,
            width: parsed.width,
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

async function main(): Promise<number> {
  const [command, ...rest] = process.argv.slice(2);
  switch (command) {
    case 'routes':
      return cmdRoutes();
    case 'page':
      return cmdPage(rest);
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
