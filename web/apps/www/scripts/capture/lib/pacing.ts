/**
 * Human-like pacing helpers shared by every flow in `lib/flows/` — used
 * to drive the console reliably to a flow's end state (waiting for
 * transitions to settle) before the single screenshot is taken. There
 * is no recording any more, so there is nothing here to timestamp.
 */

import type { Locator, Page } from 'playwright';

function rand(minMs: number, maxMs: number): number {
  return Math.floor(Math.random() * (maxMs - minMs + 1)) + minMs;
}

/** A short human "beat" between actions — never instant, never sluggish. */
export async function beat(minMs = 600, maxMs = 900): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, rand(minMs, maxMs)));
}

/** Types like a person: click to focus, then type char-by-char with jitter. */
export async function typeInto(target: Locator, text: string): Promise<void> {
  await target.click();
  await target.pressSequentially(text, { delay: rand(35, 70) });
}

/** Opens a Plexor `Select`/`SimpleSelect` trigger and clicks a matching listbox option. */
export async function chooseOption(page: Page, trigger: Locator, option: RegExp | string): Promise<void> {
  await trigger.click();
  await page.waitForTimeout(150);
  await page.getByRole('option', { name: option }).first().click();
}

/** Opens a select trigger and clicks the Nth option (0-based) — for when the
 *  option text is arbitrary mock data (node hostnames, image names, …). */
export async function chooseNthOption(page: Page, trigger: Locator, index = 0): Promise<void> {
  await trigger.click();
  await page.waitForTimeout(150);
  await page.getByRole('option').nth(index).click();
}
