/**
 * Шаги взаимодействия (`--click`, `--hover`, `--fill`, `--press`, `--wait`)
 * — выполняются по порядку после загрузки страницы/стори, перед снятием
 * скриншота. Первый упавший шаг даёт issue `step-failed` и останавливает
 * дальнейшие шаги (снимок всё равно делается — с тем, что успело
 * отрендериться).
 */

import type { Page } from 'playwright';
import type { Issue } from './inspect';

export type Step =
  | { readonly kind: 'click'; readonly selector: string }
  | { readonly kind: 'hover'; readonly selector: string }
  | { readonly kind: 'fill'; readonly selector: string; readonly value: string }
  | { readonly kind: 'press'; readonly key: string }
  | { readonly kind: 'wait'; readonly ms: number };

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
        hint: 'Selector did not resolve or the action timed out. Check the selector against the aria structure in this report, or the story/page markup.',
      });
      break; // остальные шаги пропускаем, но скриншот всё равно снимаем
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
  }
}
