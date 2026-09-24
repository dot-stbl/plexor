/**
 * Renders one app page + runs the text checks. One call = one (path ×
 * theme × mobile) run; each gets a fresh `BrowserContext` (isolated
 * localStorage — no session leak between runs in the same process).
 *
 * Adapted from `web/apps/console/scripts/agent/lib/page-target.ts`:
 * `seedAuthAndPreferences` (fake login session) is replaced with
 * `seedTheme` — `www` is a public site with no auth, but does have a
 * real theme picker to seed for `--theme light|dark` (see browser-prep.ts).
 */

import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import type { Browser } from 'playwright';
import { ariaOutline, inspectDom, recordPage, visibleText } from './inspect';
import {
  disableAnimations,
  scrollThroughPage,
  seedTheme,
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
  /** Emulate `prefers-reduced-motion: no-preference` instead of the default `reduce`. */
  readonly motion: boolean;
  /** Desktop viewport width override in px — see `viewportFor` in `browser-prep.ts`. */
  readonly width: number | undefined;
  readonly steps: readonly Step[];
}

const PAGE_SHOTS_DIR = join(SHOTS_DIR, 'page');

function outputPaths(
  targetPath: string,
  theme: Theme,
  mobile: boolean,
  width: number | undefined,
): { pngPath: string; mdPath: string } {
  const slug = slugForPath(targetPath);
  // `width` only enters the filename when set and not `mobile` (mobile
  // already picks its own fixed viewport) — so the common desktop case
  // keeps its existing `<slug>.<theme>.png` name, and a `--width 1920`
  // run lands next to it instead of overwriting it.
  const suffix = `${theme}${mobile ? '.mobile' : width ? `.w${width}` : ''}`;
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
  const { pngPath, mdPath } = outputPaths(targetPath, theme, opts.mobile, opts.width);
  mkdirSync(PAGE_SHOTS_DIR, { recursive: true });

  const context = await browser.newContext({ viewport: viewportFor(opts.mobile, opts.width) });
  const page = await context.newPage();
  try {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: opts.motion ? 'no-preference' : 'reduce' });
    await seedTheme(page, theme);

    const recorder = recordPage(page);
    try {
      // The first vite compile can be slow — generous navigation timeout.
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
            hint: 'The dev server may still be compiling, or the route path is wrong. Run `bun run shot routes` to check the path exists.',
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

    // `body`, not `main` — every www page's shared chrome (SiteHeader,
    // SiteFooter) lives OUTSIDE `<main>` as a sibling, and verifying that
    // chrome (header overflow, dropdown controls, footer rails) across
    // every route is exactly what this tool exists for.
    const rootSelector = 'body';
    const domIssues = await inspectDom(page, rootSelector);
    const aria = await ariaOutline(page, rootSelector);
    const text = await visibleText(page, rootSelector);
    recorder.detach();

    if (opts.full) {
      // Force below-the-fold `loading="lazy"` images to load before the
      // stitched capture — see `scrollThroughPage`'s own doc comment.
      await scrollThroughPage(page);
      await waitForNetworkIdleBestEffort(page, 4_000);
    }

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
