/**
 * Best-effort "page is visually settled" wait — same signals the
 * console's own `bun run shot` uses (skeletons gone, network idle),
 * reimplemented here since `scripts/agent/lib/browser-prep.ts` lives in
 * console (read-only, not importable from www).
 */

import type { Page } from 'playwright';

export async function waitSkeletonsGone(page: Page, maxMs = 5_000): Promise<void> {
  try {
    await page.waitForFunction(() => document.querySelectorAll('[data-slot="skeleton"]').length === 0, undefined, {
      timeout: maxMs,
    });
  } catch {
    // Didn't clear in time — proceed with whatever's rendered.
  }
}

export async function waitNetworkIdle(page: Page, maxMs = 4_000): Promise<void> {
  try {
    await page.waitForLoadState('networkidle', { timeout: maxMs });
  } catch {
    // MSW / long-poll-ish connections can keep the network "busy" — fine.
  }
}

/** Full settle: navigate, wait for skeletons + network, then one human beat. */
export async function settle(page: Page): Promise<void> {
  await waitSkeletonsGone(page);
  await waitNetworkIdle(page);
  await page.waitForTimeout(300);
}
