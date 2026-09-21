import { join } from 'node:path';
import { mkdir } from 'node:fs/promises';

import type { TestRunnerConfig } from '@storybook/test-runner';
import { toMatchImageSnapshot } from 'jest-image-snapshot';

/**
 * Storybook test-runner config — visual regression pipeline.
 *
 * The Storybook test-runner (Jest + Playwright under the hood) visits each
 * story in a headless browser and runs hooks. We use `postVisit` to capture
 * a PNG screenshot and compare against a committed baseline via
 * jest-image-snapshot. Baselines live in `.storybook/__screenshots__/` and
 * are checked into git, so the same SVG/HTML/text content always renders
 * to the same pixels (modulo OS font rendering — see scripts/visual-tests.md
 * for the CI / local disclaimer).
 *
 * Layout — desktop @ 1280×800, single viewport per run. Story id already
 * encodes kind + variant (`plexor-ui-button--variants`, `...--sizes`),
 * so the snapshot file is `<id>.snap.png` — readable, greppable.
 *
 * Animation determinism — `preVisit` injects a CSS rule that disables
 * transitions and animations globally. We do NOT use `--no-animations`
 * (Chromium flag) because that flag also disables cursor visibility and
 * a few other things that make the screenshot look "wrong" when
 * reviewed; CSS-only is cleaner.
 *
 * Resource cleanup — the test-runner spawns workers that spawn
 * Playwright browsers. We do NOT use `await page.screenshot({ fullPage: true })`
 * because long stories scroll and that's not what visual regression should
 * catch (component layout, not page length). The default viewport screenshot
 * is what we want.
 *
 * See scripts/visual-tests.md for the operator's guide.
 */

const SCREENSHOT_DIR = join(process.cwd(), '.storybook', '__screenshots__');

/** Standard desktop viewport for component baselines. */
const VIEWPORT = { width: 1280, height: 800 } as const;

const config: TestRunnerConfig = {
  setup() {
    // jest-image-snapshot matcher — toMatchImageSnapshot on a Buffer
    // (page.screenshot returns PNG bytes) compares against the committed
    // baseline; updates via `--updateSnapshot` (`bun run test:visual:update`).
    expect.extend({ toMatchImageSnapshot });
  },

  async preVisit(page) {
    // Pin viewport so screenshots are deterministic across host machines.
    await page.setViewportSize(VIEWPORT);

    // Kill animations + transitions before each story renders.
    // We force `prefers-reduced-motion: reduce` + override any CSS animations.
    await page.addStyleTag({
      content: `
        *, *::before, *::after {
          animation-duration: 0s !important;
          animation-delay: 0s !important;
          animation-iteration-count: 1 !important;
          transition-duration: 0s !important;
          transition-delay: 0s !important;
        }
      `,
    });
    // Hint to browsers that they should NOT honour any media query.
    await page.emulateMedia({ reducedMotion: 'reduce' });
  },

  async postVisit(page, context) {
    // Ensure the baseline directory exists (jest-image-snapshot writes
    // there on `--updateSnapshot`; it would fail otherwise on a clean
    // clone with no baselines yet).
    await mkdir(SCREENSHOT_DIR, { recursive: true });

    // Default viewport screenshot — not fullPage. Components in the
    // storybook iframe are centered; viewport snapshot is what humans
    // compare visually.
    const image = await page.screenshot({ fullPage: false });

    expect(image).toMatchImageSnapshot({
      customSnapshotsDir: SCREENSHOT_DIR,
      customSnapshotIdentifier: context.id,
      // Per-snapshot diff ratio — 0.01 (1%) catches visible regressions
      // (color shifts, padding changes, missing elements) while tolerating
      // sub-pixel font-rendering noise on different OSes.
      failureThreshold: 0.01,
      failureThresholdType: 'percent',
      // No custom diff dir; jest-image-snapshot writes diffs next to the
      // snapshot file, which is fine for `bun run test:visual:cleanup`.
      storeReceivedOnFailure: true,
    });
  },
};

export default config;
