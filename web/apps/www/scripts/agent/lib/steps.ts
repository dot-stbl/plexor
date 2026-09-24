/**
 * Interaction steps (`--click`, `--hover`, `--fill`, `--press`, `--wait`)
 * — run in order after the page loads, before the screenshot. The first
 * failing step records a `step-failed` issue and stops the remaining
 * steps (the screenshot is still taken, with whatever rendered).
 *
 * Ported verbatim from `web/apps/console/scripts/agent/lib/steps.ts`
 * (no changes needed).
 */

import type { Page } from 'playwright';
import type { Issue } from './inspect';

export type Step =
  | { readonly kind: 'click'; readonly selector: string }
  | { readonly kind: 'hover'; readonly selector: string }
  | { readonly kind: 'fill'; readonly selector: string; readonly value: string }
  | { readonly kind: 'press'; readonly key: string }
  | { readonly kind: 'wait'; readonly ms: number }
  | { readonly kind: 'scroll'; readonly px: number };

/** `--scroll`'s own settle wait — a scroll-linked (motion) effect needs a
 * frame or two to catch up with the new scroll position before the next
 * step or the screenshot runs. */
const SCROLL_SETTLE_MS = 300;

const STEP_TIMEOUT_MS = 10_000;

export async function runSteps(page: Page, steps: readonly Step[]): Promise<Issue[]> {
  const issues: Issue[] = [];
  for (const step of steps) {
    try {
      await runOne(page, step);
    } catch (err) {
      const message = (err as Error).message.split('\n')[0];
      issues.push({
        severity: 'FAIL',
        code: 'step-failed',
        message: `${describeStep(step)} — ${message}`,
        hint: 'Selector did not resolve or the action timed out. Check the selector against the aria structure in this report, or the page markup.',
      });
      break; // Skip remaining steps, but still take the screenshot.
    }
  }
  return issues;
}

function describeStep(step: Step): string {
  switch (step.kind) {
    case 'click':
      return `click "${step.selector}"`;
    case 'hover':
      return `hover "${step.selector}"`;
    case 'fill':
      return `fill "${step.selector}" = "${step.value}"`;
    case 'press':
      return `press "${step.key}"`;
    case 'wait':
      return `wait ${step.ms}ms`;
    case 'scroll':
      return `scroll to ${step.px}px`;
  }
}

async function runOne(page: Page, step: Step): Promise<void> {
  switch (step.kind) {
    case 'click':
      await page.click(step.selector, { timeout: STEP_TIMEOUT_MS });
      return;
    case 'hover':
      await page.hover(step.selector, { timeout: STEP_TIMEOUT_MS });
      return;
    case 'fill':
      await page.fill(step.selector, step.value, { timeout: STEP_TIMEOUT_MS });
      return;
    case 'press':
      await page.keyboard.press(step.key);
      return;
    case 'wait':
      await page.waitForTimeout(step.ms);
      return;
    case 'scroll':
      await page.evaluate((px) => window.scrollTo(0, px), step.px);
      await page.waitForTimeout(SCROLL_SETTLE_MS);
      return;
  }
}
