/**
 * Deterministic-render prep — viewports, theme seeding, disabling
 * animations, waiting for skeletons. Shared by `page-target.ts`.
 *
 * Adapted from `web/apps/console/scripts/agent/lib/browser-prep.ts`:
 * console seeds a fake auth session + preferences (`plexor-preferences`)
 * because its routes gate on a logged-in user. `www` is a public static
 * site with no auth — instead this seeds the `plexor-theme` localStorage
 * key (§ apply-theme.ts / @plexor/ui's theme-picker) so `--theme light|
 * dark` picks a real Plexor DS preset, not just the browser's prefers-
 * color-scheme media emulation.
 */

import type { Page } from 'playwright';

export type Theme = 'light' | 'dark';

export const DESKTOP_VIEWPORT = { width: 1280, height: 800 } as const;
export const MOBILE_VIEWPORT = { width: 390, height: 844 } as const;

/** `width` overrides the desktop viewport's width (e.g. `--width 1920` to check an ultrawide breakpoint); ignored under `mobile`. */
export function viewportFor(mobile: boolean, width?: number): { width: number; height: number } {
  if (mobile) return MOBILE_VIEWPORT;
  return width ? { width, height: DESKTOP_VIEWPORT.height } : DESKTOP_VIEWPORT;
}

const STORAGE_KEY = 'plexor-theme';
const PRESET_BY_THEME: Record<Theme, string> = {
  light: 'plexor-default-light',
  dark: 'plexor-default-dark',
};

/**
 * Seeds the persisted theme preset in localStorage BEFORE the page's own
 * scripts run, so both the inline no-FOUC boot script in `index.html`
 * and `applyBootPreset()` (in `src/lib/apply-theme.ts`) pick the right
 * preset on first paint — same mechanism a real visitor's previous
 * choice would use.
 */
export async function seedTheme(page: Page, theme: Theme): Promise<void> {
  await page.addInitScript(
    ({ key, value }: { key: string; value: string }) => {
      try {
        window.localStorage.setItem(key, value);
      } catch {
        // localStorage unavailable (private mode etc.) — page falls back
        // to its prefers-color-scheme default; a legitimate render to report.
      }
    },
    { key: STORAGE_KEY, value: PRESET_BY_THEME[theme] },
  );
}

/**
 * CSS that kills animations/transitions so screenshots are deterministic.
 * Requires an existing document — call after navigation.
 */
export async function disableAnimations(page: Page): Promise<void> {
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
}

/** Waits for skeletons to disappear (up to maxMs), but never fails the call. */
export async function waitForSkeletonsGone(page: Page, maxMs = 5_000): Promise<void> {
  try {
    await page.waitForFunction(
      () => document.querySelectorAll('[data-slot="skeleton"]').length === 0,
      undefined,
      { timeout: maxMs },
    );
  } catch {
    // Didn't clear within maxMs — don't block the screenshot; the report
    // shows whatever state they're in.
  }
}

/** Waits for network idle, but never fails on timeout (a live site may keep long-poll-ish connections open). */
export async function waitForNetworkIdleBestEffort(page: Page, maxMs = 8_000): Promise<void> {
  try {
    await page.waitForLoadState('networkidle', { timeout: maxMs });
  } catch {
    // Not critical — continue with whatever has rendered.
  }
}
