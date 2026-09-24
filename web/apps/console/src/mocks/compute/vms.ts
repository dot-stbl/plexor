/**
 * VM fixtures — the compute bounded context. Hand-curated fleet placed
 * on REAL nodes (`compute/nodes.ts`) and REAL subnets
 * (`network/networks.ts`) so internalIp/zone are actual addresses
 * inside actual VPCs instead of an unrelated made-up range.
 * `FLEET_PLACEMENT` carries the node/subnet/vpc/image cross-references
 * the `Vm`/`VmList` wire type has no room for — the MSW handler uses it
 * when synthesizing a `VmDetail`.
 */
import type { Vm, VmList, VmStatus } from '@/shared/api';
import { faker } from '@faker-js/faker';
import { MOCK_TIMESTAMP, resetMockRng } from '../db/seed-config';
import { hostInSubnet, SUBNET_BY_ID } from '../network/networks';

resetMockRng();

interface FleetSeed {
  id: string;
  name: string;
  status: VmStatus;
  machineType: string;
  vcpu: number;
  ramGb: number;
  diskGb: number;
  subnetId: string;
  hostOctet: number;
  nodeId: string;
  imageId: string;
  ageDays: number;
}

const SEEDS: FleetSeed[] = [
  { id: 'vm-a8c91f2e', name: 'web-prod-01',   status: 'running',      machineType: '4-8',  vcpu: 4, ramGb: 8,  diskGb: 80,  subnetId: 'subnet-prod-eu-compute-a', hostOctet: 10, nodeId: 'node-pve-r1n01', imageId: 'img-ubuntu-2404',  ageDays: 60 },
  { id: 'vm-b7d40e1a', name: 'api-prod-01',   status: 'running',      machineType: '4-8',  vcpu: 4, ramGb: 8,  diskGb: 60,  subnetId: 'subnet-prod-eu-compute-a', hostOctet: 11, nodeId: 'node-pve-r1n01', imageId: 'img-ubuntu-2404',  ageDays: 58 },
  { id: 'vm-c2f8a039', name: 'worker-01',     status: 'running',      machineType: '2-4',  vcpu: 2, ramGb: 4,  diskGb: 40,  subnetId: 'subnet-prod-eu-compute-b', hostOctet: 20, nodeId: 'node-pve-r1n02', imageId: 'img-debian-12',    ageDays: 50 },
  { id: 'vm-9e1b3c47', name: 'db-replica-01', status: 'running',      machineType: '8-32', vcpu: 8, ramGb: 32, diskGb: 500, subnetId: 'subnet-prod-eu-data',      hostOctet: 5,  nodeId: 'node-pve-r1n02', imageId: 'img-ubuntu-2204',  ageDays: 45 },
  { id: 'vm-3a7c5d12', name: 'cache-01',      status: 'running',      machineType: '2-16', vcpu: 2, ramGb: 16, diskGb: 30,  subnetId: 'subnet-prod-eu-compute-b', hostOctet: 7,  nodeId: 'node-pve-r1n02', imageId: 'img-rocky-9',      ageDays: 40 },
  { id: 'vm-6f8d22b8', name: 'build-runner',  status: 'error',        machineType: '4-8',  vcpu: 4, ramGb: 8,  diskGb: 100, subnetId: 'subnet-prod-eu-compute-a', hostOctet: 40, nodeId: 'node-pve-r2n01', imageId: 'img-ci-runner',    ageDays: 35 },
  { id: 'vm-1b9e4f73', name: 'staging-api',   status: 'stopped',      machineType: '2-4',  vcpu: 2, ramGb: 4,  diskGb: 40,  subnetId: 'subnet-staging-eu-a',      hostOctet: 12, nodeId: 'node-pve-r2n01', imageId: 'img-ubuntu-2204',  ageDays: 30 },
  { id: 'vm-4d2a89e1', name: 'ml-trainer',    status: 'provisioning', machineType: '8-64', vcpu: 8, ramGb: 64, diskGb: 250, subnetId: 'subnet-dev-eu-a',          hostOctet: 4,  nodeId: 'node-pve-r1n01', imageId: 'img-app-base-v3',  ageDays: 1  },
];

function zoneOf(subnetId: string): string {
  const subnet = SUBNET_BY_ID.get(subnetId);
  if (!subnet) throw new Error(`Unknown mock subnet id: ${subnetId}`);
  return subnet.zone;
}

export const FLEET: ReadonlyArray<Vm> = SEEDS.map((seed) => ({
  id: seed.id,
  name: seed.name,
  status: seed.status,
  internalIp: hostInSubnet(seed.subnetId, seed.hostOctet),
  zone: zoneOf(seed.subnetId),
  machineType: seed.machineType,
  vcpu: seed.vcpu,
  ramGb: seed.ramGb,
  diskGb: seed.diskGb,
  createdAt: faker.date.recent({ days: seed.ageDays }).toISOString(),
}));

/** Per-VM node/subnet/vpc/image cross-references the `Vm` wire type has
 *  no room for — consumed by the MSW handler that synthesizes `VmDetail`. */
export const FLEET_PLACEMENT: ReadonlyMap<
  string,
  { nodeId: string; subnetId: string; vpcId: string; imageId: string }
> = new Map(
  SEEDS.map((seed) => [
    seed.id,
    {
      nodeId: seed.nodeId,
      subnetId: seed.subnetId,
      vpcId: SUBNET_BY_ID.get(seed.subnetId)!.vpcId,
      imageId: seed.imageId,
    },
  ]),
);

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
 * Group fleet by status — drives the launcher SUMMARY \"running of N\" line.
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
