/**
 * Barrel export — single entry point for shared mock fixtures.
 *
 * ```ts
 * import { FLEET, countByStatus } from '@/mocks';
 * import { CLUSTERS, clusterSummary } from '@/mocks';
 * import { makeLauncherSummary } from '@/mocks';
 * ```
 *
 * Each domain (vms, clusters, branding, auth, audit, launcher-summary)
 * owns its own file; this index re-exports the fixtures the rest of
 * the app consumes.
 */

export * from './utils';
export * from './vms';
export * from './clusters';
export * from './branding';
export * from './auth';
export * from './audit';
export * from './launcher-summary';
