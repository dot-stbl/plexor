/**
 * Mock fixtures shared between MSW handlers and component tests.
 *
 * Convention: handlers AND component tests import from this directory.
 * Don't inline mock data inside test files — read it from here so the
 * launcher SUMMARY, VM list, cluster list, etc. always show the same
 * numbers the API would return.
 *
 * Shape compatibility: each fixture mirrors the kubb-generated type in
 * `web/apps/console/src/shared/api/src/types/*` so it's drop-in for both
 * the MSW response body and the kubb hook's input. See the README at
 * the bottom of this file for the import patterns.
 */

import { faker } from '@faker-js/faker';

/**
 * Deterministic faker seed — same data on every reload so screenshots,
 * tests, and dev experience are stable.
 */
export const MOCK_SEED = 1337;

/** Stable, hand-curated date so timestamp-derived fields are predictable. */
export const MOCK_TIMESTAMP = '2026-09-19T10:00:00Z';

/**
 * Reset faker's RNG to the project seed. Call from MSW handlers at module
 * load (so list endpoints produce the same fleet every render) and from
 * component tests that need deterministic fake ids.
 */
export function resetMockRng(seed: number = MOCK_SEED): void {
  faker.seed(seed);
}
