/**
 * VM fixtures — shared by MSW handlers (`shared/api/mocks/handlers.ts`)
 * and the launcher SUMMARY card (so it can show real numbers instead of
 * `— / нет данных`).
 *
 * Hand-curated fleet with stable ids, names, IPs and zones. The kubb
 * factories' faker fields (`createdAt`, etc.) are derived deterministically
 * via `resetMockRng()` so the same fleet renders on every page load.
 *
 * Shape: each fixture matches `Vm` from `@/shared/api` so it's drop-in
 * for both `createVmList({ items: FLEET, ... })` and the kubb query
 * hook's `data` field.
 */

import type { Vm, VmList, VmStatus } from '@/shared/api';
import { faker } from '@faker-js/faker';
import { MOCK_TIMESTAMP, resetMockRng } from './utils';

resetMockRng();

/**
 * Hand-curated fleet — 8 VMs covering every status so the launcher /
 * list pages render a realistic mix. Names, IPs, zones, and vcpu/ram
 * are deterministic; `createdAt` comes from faker so each VM has a
 * unique timestamp but the fleet as a whole is stable.
 *
 * `ip` mirrors `internalIp` so the launcher can surface it without
 * re-mapping; the kubb type only carries `internalIp`, so we expose
 * a derived view via `getFleetForView()` below.
 */
export const FLEET: ReadonlyArray<Vm> = [
  { id: 'vm-a8c91f2e', name: 'web-prod-01',   status: 'running',      internalIp: '10.128.1.10', zone: 'eu-central-1', machineType: '4-8',  vcpu: 4, ramGb: 8,  diskGb: 80,  createdAt: faker.date.recent({ days: 60 }).toISOString() },
  { id: 'vm-b7d40e1a', name: 'api-prod-01',   status: 'running',      internalIp: '10.128.1.11', zone: 'eu-central-1', machineType: '4-8',  vcpu: 4, ramGb: 8,  diskGb: 60,  createdAt: faker.date.recent({ days: 58 }).toISOString() },
  { id: 'vm-c2f8a039', name: 'worker-01',      status: 'running',      internalIp: '10.128.2.20', zone: 'eu-central-1', machineType: '2-4',  vcpu: 2, ramGb: 4,  diskGb: 40,  createdAt: faker.date.recent({ days: 50 }).toISOString() },
  { id: 'vm-9e1b3c47', name: 'db-replica-01', status: 'running',      internalIp: '10.128.3.5',  zone: 'eu-central-1', machineType: '8-32', vcpu: 8, ramGb: 32, diskGb: 500, createdAt: faker.date.recent({ days: 45 }).toISOString() },
  { id: 'vm-3a7c5d12', name: 'cache-01',      status: 'running',      internalIp: '10.128.4.7',  zone: 'eu-central-1', machineType: '2-16', vcpu: 2, ramGb: 16, diskGb: 30,  createdAt: faker.date.recent({ days: 40 }).toISOString() },
  { id: 'vm-6f8d22b8', name: 'build-runner',  status: 'error',        internalIp: '10.128.5.3',  zone: 'eu-central-1', machineType: '4-8',  vcpu: 4, ramGb: 8,  diskGb: 100, createdAt: faker.date.recent({ days: 35 }).toISOString() },
  { id: 'vm-1b9e4f73', name: 'staging-api',   status: 'stopped',      internalIp: '10.128.6.12', zone: 'eu-central-1', machineType: '2-4',  vcpu: 2, ramGb: 4,  diskGb: 40,  createdAt: faker.date.recent({ days: 30 }).toISOString() },
  { id: 'vm-4d2a89e1', name: 'ml-trainer',    status: 'provisioning', internalIp: '10.128.7.4',  zone: 'eu-central-1', machineType: '8-64', vcpu: 8, ramGb: 64, diskGb: 250, createdAt: faker.date.recent({ days: 1  }).toISOString() },
];

/** Map of vm-id → fleet entry so MSW handlers and tests can resolve
 *  a specific id (e.g. `/vms/vm-a8c91f2e`) without re-running faker. */
export const FLEET_BY_ID: ReadonlyMap<string, Vm> = new Map(
  FLEET.map((vm) => [vm.id, vm]),
);

/** Default page shape — handy for handlers that just want a list. */
export function makeVmList(): VmList {
  return {
    items: FLEET.slice(),
    page: 1,
    pageSize: 20,
    total: FLEET.length,
  };
}

/**
 * Group fleet by status — drives the launcher SUMMARY "running of N" line.
 *
 * `VmStatus` covers `provisioning | running | stopped | error | idle`.
 * Every status appears as a key in the returned record (zero for ones
 * with no VMs in the fleet), so callers don't need to handle missing keys.
 */
export function countByStatus(): Record<VmStatus, number> {
  const counts: Record<VmStatus, number> = {
    provisioning: 0,
    running: 0,
    stopped: 0,
    error: 0,
    idle: 0,
  };
  for (const vm of FLEET) {
    counts[vm.status] += 1;
  }
  return counts;
}

/** MOCK_TIMESTAMP re-exported so callers can pin a fleet build date. */
export { MOCK_TIMESTAMP };
