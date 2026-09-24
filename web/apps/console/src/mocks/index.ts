/**
 * Barrel export — single entry point for shared mock fixtures, grouped
 * by bounded context (`db/`, `identity/`, `compute/`, `network/`,
 * `storage/`, `catalog/`, `branding/`, `audit/`, `billing/`) plus the
 * cross-cutting `launcher-summary`.
 *
 * ```ts
 * import { FLEET, countByStatus } from '@/mocks';
 * import { CLUSTERS, clusterSummary } from '@/mocks';
 * import { makeLauncherSummary } from '@/mocks';
 * ```
 *
 * The MSW-handler plumbing primitives (`db/scenario`, `db/latency`)
 * live under `@/mocks/db/...` but are intentionally not in the top
 * barrel — import them from their direct path when a handler needs
 * them.
 */

export * from './db/seed-config';
export * from './identity/auth';
export * from './identity/users';
export * from './compute/vms';
export * from './compute/images';
export * from './compute/lxc';
export * from './network/networks';
export * from './storage/volumes';
export * from './fleet/clusters';
export * from './catalog/k8s';
export * from './catalog/databases';
export * from './branding/branding';
export * from './audit/audit';
export * from './billing/billing';
export * from './launcher-summary';
