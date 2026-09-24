/**
 * Рендер одной страницы приложения (мок-режим) + текстовые проверки.
 * Один вызов = один (путь × тема × mobile) прогон; каждый получает свежий
 * `BrowserContext` (изолированный localStorage — без утечки сессии между
 * прогонами внутри одного процесса).
 */

import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import type { Browser } from 'playwright';
import { ariaOutline, inspectDom, recordPage, visibleText } from './inspect';
import {
  disableAnimations,
  seedAuthAndPreferences,
  viewportFor,
  waitForNetworkIdleBestEffort,
  waitForSkeletonsGone,
  type Theme,
} from './browser-prep';
import { runSteps, type Step } from './steps';
import { slugForPath } from './routes';
import { SHOTS_DIR } from './servers';
import type { RunResult } from './report';

export interface PageRunOptions {
  readonly full: boolean;
  readonly mobile: boolean;
  readonly steps: readonly Step[];
}

const PAGE_SHOTS_DIR = join(SHOTS_DIR, 'page');

function outputPaths(targetPath: string, theme: Theme, mobile: boolean): { pngPath: string; mdPath: string } {
  const slug = slugForPath(targetPath);
  const suffix = `${theme}${mobile ? '.mobile' : ''}`;
  return {
    pngPath: join(PAGE_SHOTS_DIR, `${slug}.${suffix}.png`),
    mdPath: join(PAGE_SHOTS_DIR, `${slug}.${suffix}.md`),
  };
}

export async function renderPageTarget(
  browser: Browser,
  appBaseUrl: string,
  targetPath: string,
  theme: Theme,
  opts: PageRunOptions,
): Promise<RunResult> {
  const url = `${appBaseUrl}${targetPath}`;
  const { pngPath, mdPath } = outputPaths(targetPath, theme, opts.mobile);
  mkdirSync(PAGE_SHOTS_DIR, { recursive: true });

  const context = await browser.newContext({ viewport: viewportFor(opts.mobile) });
  const page = await context.newPage();
  try {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await seedAuthAndPreferences(page, theme);

    const recorder = recordPage(page);
    try {
      // Первая компиляция vite может быть медленной — таймаут навигации 90с.
      await page.goto(url, { waitUntil: 'load', timeout: 90_000 });
    } catch (err) {
      recorder.detach();
      return {
        kind: 'page',
        target: targetPath,
        url,
        theme,
        mobile: opts.mobile,
        issues: [
          {
            severity: 'FAIL',
            code: 'navigation-failed',
            message: (err as Error).message.split('\n')[0],
            hint: 'Vite dev server may still be compiling, or the route path is wrong. Run `bun run shot routes` to check the path exists.',
          },
        ],
        aria: '',
        visibleText: '',
        pngPath: null,
        mdPath: null,
      };
    }

    await disableAnimations(page);
    await waitForNetworkIdleBestEffort(page, 8_000);
    await waitForSkeletonsGone(page, 5_000);
    await page.waitForTimeout(300);

    const stepIssues = await runSteps(page, opts.steps);

    const rootSelector = (await page.locator('main').count()) > 0 ? 'main' : 'body';
    const domIssues = await inspectDom(page, rootSelector);
    const aria = await ariaOutline(page, rootSelector);
    const text = await visibleText(page, rootSelector);
    recorder.detach();

    await page.screenshot({ path: pngPath, fullPage: opts.full });

    return {
      kind: 'page',
      target: targetPath,
      url,
      theme,
      mobile: opts.mobile,
      issues: [...recorder.issues, ...stepIssues, ...domIssues],
      aria,
      visibleText: text,
      pngPath,
      mdPath,
    };
  } finally {
    await context.close();
  }
}
