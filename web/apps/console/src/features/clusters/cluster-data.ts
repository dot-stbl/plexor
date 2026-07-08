import type { Cluster } from './cluster-types';

/**
 * Hand-curated clusters. Realistic enough to populate the
 * /clusters list and detail pages. Once the OpenAPI spec lands,
 * delete this file and wire `useListClusters` / `useGetCluster` to
 * the kubb-generated MSW handlers.
 */

const CLUSTERS: Cluster[] = [
  {
    id: 'cluster-prod-eu-1',
    name: 'prod-eu-1',
    zone: 'eu-central-1',
    status: 'healthy',
    nodeCount: 3,
    totalCpu: 96,
    usedCpu: 32,
    totalRamGb: 192,
    usedRamGb: 48,
    totalDiskGb: 4000,
    usedDiskGb: 920,
    vmCount: 5,
    flavors: [
      { id: 'small', vcpu: 2, ramGb: 4, diskGb: 20 },
      { id: 'medium', vcpu: 4, ramGb: 8, diskGb: 40 },
      { id: 'large', vcpu: 8, ramGb: 16, diskGb: 80 },
    ],
    nodes: [
      { id: 'node-prod-eu-1-a', hostname: 'node-a.prod-eu-1.local', role: 'control', status: 'ready', vcpu: 32, ramGb: 64, vmCount: 1 },
      { id: 'node-prod-eu-1-b', hostname: 'node-b.prod-eu-1.local', role: 'worker', status: 'ready', vcpu: 32, ramGb: 64, vmCount: 2 },
      { id: 'node-prod-eu-1-c', hostname: 'node-c.prod-eu-1.local', role: 'worker', status: 'ready', vcpu: 32, ramGb: 64, vmCount: 2 },
    ],
    createdAt: '2025-08-12T09:00:00Z',
  },
  {
    id: 'cluster-staging-eu-1',
    name: 'staging-eu-1',
    zone: 'eu-central-1',
    status: 'degraded',
    nodeCount: 2,
    totalCpu: 32,
    usedCpu: 12,
    totalRamGb: 64,
    usedRamGb: 18,
    totalDiskGb: 1500,
    usedDiskGb: 220,
    vmCount: 2,
    flavors: [
      { id: 'small', vcpu: 2, ramGb: 4, diskGb: 20 },
      { id: 'medium', vcpu: 4, ramGb: 8, diskGb: 40 },
    ],
    nodes: [
      { id: 'node-staging-eu-1-a', hostname: 'node-a.staging-eu-1.local', role: 'control', status: 'ready', vcpu: 16, ramGb: 32, vmCount: 1 },
      { id: 'node-staging-eu-1-b', hostname: 'node-b.staging-eu-1.local', role: 'worker', status: 'draining', vcpu: 16, ramGb: 32, vmCount: 1 },
    ],
    createdAt: '2025-09-20T14:00:00Z',
  },
  {
    id: 'cluster-dev-us-1',
    name: 'dev-us-1',
    zone: 'us-east-1',
    status: 'offline',
    nodeCount: 1,
    totalCpu: 16,
    usedCpu: 0,
    totalRamGb: 32,
    usedRamGb: 0,
    totalDiskGb: 500,
    usedDiskGb: 0,
    vmCount: 0,
    flavors: [
      { id: 'small', vcpu: 2, ramGb: 4, diskGb: 20 },
    ],
    nodes: [
      { id: 'node-dev-us-1-a', hostname: 'node-a.dev-us-1.local', role: 'control', status: 'offline', vcpu: 16, ramGb: 32, vmCount: 0 },
    ],
    createdAt: '2025-10-01T10:00:00Z',
  },
];

export function listClusters(): Cluster[] {
  return CLUSTERS;
}

export function getCluster(id: string): Cluster | undefined {
  return CLUSTERS.find((c) => c.id === id);
}