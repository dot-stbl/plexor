/**
 * catalog — managed-service catalog entry (`/managed/postgres`, empty in
 * mock data → the rich onboarding `EmptyState`) → "Create PostgreSQL
 * cluster" → the create-cluster wizard → submit → back to the catalog
 * entry. The end state is the screenshot ("Managed PostgreSQL" on the
 * landing — see `src/content/media-manifest.ts`).
 */

import type { Page } from 'playwright';
import { beat, typeInto } from '../pacing';
import { settle } from '../wait-ready';
import type { Flow } from './types';

export const catalogFlow: Flow = {
  id: 'catalog',
  title: 'Install an app from the catalog',
  startPath: '/managed/postgres',
  async run(page: Page) {
    await beat(300, 400);

    await page.getByRole('link', { name: /Create PostgreSQL cluster/ }).click();
    await page.waitForURL('**/managed/new**');
    await settle(page);

    await beat(200, 300);
    await typeInto(page.locator('#db-name'), 'billing-pg');

    await beat(150, 250);
    await page.getByText('4 vCPU, 16 GiB', { exact: true }).first().click();

    await beat(200, 300);
    await page.getByRole('radio', { name: 'Generate' }).click();

    await beat(200, 300);
    await page.getByRole('button', { name: 'Create cluster' }).click();
    await page.waitForURL('**/managed/postgres');
    await settle(page);
    await beat(400, 500);
  },
};
