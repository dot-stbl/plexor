/**
 * Cluster types — physical resource pool that hosts VMs.
 *
 * NOT kubb-generated yet: the cluster OpenAPI spec is not part of the
 * kubb codegen pipeline (VPS providers haven't shipped the endpoint set
 * yet). When it does, replace this file with `import type { Cluster,
 * ClusterNode, ClusterFlavor } from '@/shared/api'` and drop the local
 * mock data + hooks.
 *
 * For now: local types + local hooks + hand-curated mock data.
 */

export type ClusterStatus = 'healthy' | 'degraded' | 'offline';
export type ClusterZone = 'eu-central-1' | 'eu-west-1' | 'us-east-1';

export interface ClusterFlavor {
  /** Flavor id matches what `useListVms` returns on `vm.machineType`. */
  id: string;
  vcpu: number;
  ramGb: number;
  diskGb: number;
}

export interface ClusterNode {
  id: string;
  hostname: string;
  role: 'control' | 'worker';
  status: 'ready' | 'draining' | 'offline';
  vcpu: number;
  ramGb: number;
  /** How many VMs are currently running on this node. */
  vmCount: number;
}

export interface Cluster {
  id: string;
  name: string;
  zone: ClusterZone;
  status: ClusterStatus;
  /** Number of nodes in the cluster. */
  nodeCount: number;
  /** Total vCPU available across the cluster. */
  totalCpu: number;
  /** Currently allocated vCPU (sum of all running VMs' vcpu). */
  usedCpu: number;
  /** Total RAM in GB. */
  totalRamGb: number;
  usedRamGb: number;
  /** Total disk in GB (shared volume). */
  totalDiskGb: number;
  usedDiskGb: number;
  /** How many VMs live on this cluster. */
  vmCount: number;
  /** Catalog of machine types that can run on this cluster. */
  flavors: ClusterFlavor[];
  nodes: ClusterNode[];
  createdAt: string;
}

export interface ClusterCapacity {
  totalCpu: number;
  usedCpu: number;
  totalRamGb: number;
  usedRamGb: number;
  totalDiskGb: number;
  usedDiskGb: number;
}

export function clusterCapacity(c: Cluster): ClusterCapacity {
  return {
    totalCpu: c.totalCpu,
    usedCpu: c.usedCpu,
    totalRamGb: c.totalRamGb,
    usedRamGb: c.usedRamGb,
    totalDiskGb: c.totalDiskGb,
    usedDiskGb: c.usedDiskGb,
  };
}

export function clusterUtilizationPct(c: Cluster): { cpu: number; ram: number; disk: number } {
  return {
    cpu: c.totalCpu === 0 ? 0 : Math.round((c.usedCpu / c.totalCpu) * 100),
    ram: c.totalRamGb === 0 ? 0 : Math.round((c.usedRamGb / c.totalRamGb) * 100),
    disk: c.totalDiskGb === 0 ? 0 : Math.round((c.usedDiskGb / c.totalDiskGb) * 100),
  };
}