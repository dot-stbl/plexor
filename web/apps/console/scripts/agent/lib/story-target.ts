/**
 * Рендер одной Storybook-стори + текстовые проверки. Валидирует id против
 * `/index.json` до навигации — опечатка даёт FAIL с подсказками, без
 * похода в браузер.
 */

import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import type { Browser, Page } from 'playwright';
import { ariaOutline, inspectDom, recordPage, visibleText } from './inspect';
import { disableAnimations, viewportFor, type Theme } from './browser-prep';
import { SHOTS_DIR } from './servers';
import { skippedResult, type RunResult } from './report';
import { runSteps, type Step } from './steps';
import { suggestStoryIds, type StoryIndexEntry } from './story-index';

export interface StoryRunOptions {
  readonly full: boolean;
  readonly mobile: boolean;
  readonly steps: readonly Step[];
}

const STORY_SHOTS_DIR = join(SHOTS_DIR, 'story');

function outputPaths(storyId: string, theme: Theme, mobile: boolean): { pngPath: string; mdPath: string } {
  const suffix = `${theme}${mobile ? '.mobile' : ''}`;
  return {
    pngPath: join(STORY_SHOTS_DIR, `${storyId}.${suffix}.png`),
    mdPath: join(STORY_SHOTS_DIR, `${storyId}.${suffix}.md`),
  };
}

export async function renderStoryTarget(
  browser: Browser,
  storybookBaseUrl: string,
  storyId: string,
  theme: Theme,
  opts: StoryRunOptions,
  index: readonly StoryIndexEntry[],
): Promise<RunResult> {
  if (!index.some((e) => e.id === storyId)) {
    const suggestions = suggestStoryIds(storyId, index);
    return skippedResult('story', storyId, theme, opts.mobile, {
      severity: 'FAIL',
      code: 'unknown-story',
      message: `No story with id "${storyId}". Closest ids: ${suggestions.join(', ')}`,
      hint: 'Run `bun run shot stories <filter>` to search by substring for the right id.',
    });
  }

  const url = `${storybookBaseUrl}/iframe.html?id=${encodeURIComponent(storyId)}&viewMode=story&globals=theme:${theme}`;
  const { pngPath, mdPath } = outputPaths(storyId, theme, opts.mobile);
  mkdirSync(STORY_SHOTS_DIR, { recursive: true });

  const context = await browser.newContext({ viewport: viewportFor(opts.mobile) });
  const page = await context.newPage();
  try {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const recorder = recordPage(page);

    try {
      await page.goto(url, { waitUntil: 'load', timeout: 90_000 });
    } catch (err) {
      recorder.detach();
      return {
        kind: 'story',
        target: storyId,
        url,
        theme,
        mobile: opts.mobile,
        issues: [
          {
            severity: 'FAIL',
            code: 'navigation-failed',
            message: (err as Error).message.split('\n')[0],
            hint: 'Storybook dev server may still be compiling — rerun. If it persists, check the story id and vite config.',
          },
        ],
        aria: '',
        visibleText: '',
        pngPath: null,
        mdPath: null,
      };
    }

    await disableAnimations(page);

    const ready = await waitForStoryReady(page);
    if (!ready.ok) {
      const issues = [...recorder.issues];
      recorder.detach();
      issues.push({
        severity: 'FAIL',
        code: 'story-error',
        message: ready.errorText ?? 'Story failed to render.',
        hint: 'Open Storybook locally to see the full stack. Fix the thrown error in the story or the component it renders.',
      });
      return { kind: 'story', target: storyId, url, theme, mobile: opts.mobile, issues, aria: '', visibleText: '', pngPath: null, mdPath: null };
    }

    await page.waitForTimeout(300);

    const stepIssues = await runSteps(page, opts.steps);

    const dialogVisible = await page
      .locator('[role="dialog"]')
      .first()
      .isVisible()
      .catch(() => false);
    const rootSelector = dialogVisible ? 'body' : '#storybook-root';

    const domIssues = await inspectDom(page, rootSelector);
    const aria = await ariaOutline(page, rootSelector);
    const text = await visibleText(page, rootSelector);
    recorder.detach();

    await page.screenshot({ path: pngPath, fullPage: opts.full });

    return {
      kind: 'story',
      target: storyId,
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

interface StoryReadyResult {
  readonly ok: boolean;
  readonly errorText?: string;
}

async function waitForStoryReady(page: Page, maxMs = 20_000): Promise<StoryReadyResult> {
  try {
    await page.waitForFunction(
      () => {
        const root = document.getElementById('storybook-root');
        const hasChildren = root !== null && root.children.length > 0;
        const errorShown = document.body.classList.contains('sb-show-errordisplay');
        return hasChildren || errorShown;
      },
      undefined,
      { timeout: maxMs },
    );
  } catch {
    return { ok: false, errorText: `Story did not render within ${maxMs}ms (#storybook-root stayed empty).` };
  }

  const isError = await page.evaluate(() => document.body.classList.contains('sb-show-errordisplay'));
  if (!isError) return { ok: true };

  const errorText = await page.evaluate(() => {
    const message = document.querySelector('#error-message')?.textContent ?? '';
    const stackFull = document.querySelector('#error-stack')?.textContent ?? '';
    const stack = stackFull.split('\n').slice(0, 6).join('\n');
    return `${message}\n${stack}`;
  });
  return { ok: false, errorText };
}
