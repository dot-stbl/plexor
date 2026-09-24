/**
 * hero — VM list → Create VM → fill the essentials → create → back to
 * the fleet. The end state (back in the fleet list) is the hero's
 * screenshot.
 *
 * Mock-mode substitution (see report): `provisionVmHandler` always
 * returns a fresh fake `VmDetail`, but `listVmsHandler` serves a
 * static fixture — creating a VM does NOT actually add it to the list
 * the mock returns. There is no real provisioning→running progression
 * to show. We drive the honest interaction (list → wizard → submit →
 * back to list) rather than fake a status transition the product
 * doesn't have yet.
 */

import type { Page } from 'playwright';
import { beat, chooseNthOption, typeInto } from '../pacing';
import { settle } from '../wait-ready';
import type { Flow } from './types';

export const heroFlow: Flow = {
  id: 'hero',
  title: 'Create a VM',
  startPath: '/vms',
  async run(page: Page) {
    await beat(300, 400);

    await page.getByRole('button', { name: 'Create VM' }).click();
    await page.waitForURL('**/vms/new');
    await settle(page);

    await beat(200, 300);
    await typeInto(page.locator('#vm-name'), 'edge-cache-03');

    await beat(150, 250);
    await chooseNthOption(page, page.locator('#vm-node'));

    await beat(150, 250);
    await chooseNthOption(page, page.locator('#vm-image'));

    await beat(200, 300);
    await page.locator('#vm-ram').fill('8');
    await page.keyboard.press('Tab');
    await beat(200, 300);

    await page.getByRole('button', { name: 'Create VM' }).click();
    await page.waitForURL('**/vms');
    await settle(page);
    await beat(400, 500);
  },
};
