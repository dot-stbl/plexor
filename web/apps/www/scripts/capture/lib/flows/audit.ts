/**
 * audit — the tenant-facing audit timeline (`/admin/audit`, richer than
 * the plain `/audit` page: has an actual filter row + Apply). Scrolls
 * through the log, then filters by action; the end state (filter
 * applied) is the screenshot.
 *
 * The "Since" date input is exactly why `capture.ts` sets the browser
 * context's `locale` to `en-US` — without it, Chromium renders the
 * native date placeholder in whatever locale the OS/CI defaults to
 * (`dd.mm.yyyy`), which reads wrong for an operator-facing screenshot.
 */

import type { Page } from 'playwright';
import { beat, typeInto } from '../pacing';
import { waitNetworkIdle } from '../wait-ready';
import type { Flow } from './types';

export const auditFlow: Flow = {
  id: 'audit',
  title: 'See it in audit',
  startPath: '/admin/audit',
  async run(page: Page) {
    await beat(300, 400);

    await page.mouse.wheel(0, 500);
    await beat(150, 250);
    await page.mouse.wheel(0, 400);
    await beat(150, 250);
    await page.mouse.wheel(0, -900);

    await beat(200, 300);
    await typeInto(page.locator('#audit-filter-action'), 'vm.create');

    await beat(150, 250);
    await page.getByRole('button', { name: 'Apply filters' }).click();
    await waitNetworkIdle(page);
    await beat(300, 400);
  },
};
