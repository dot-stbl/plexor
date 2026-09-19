/**
 * Cluster fixtures — shared by MSW handlers (`shared/api/mocks/handlers.ts`)
 * and the launcher SUMMARY card.
 *
 * Cluster / node endpoints aren't kubb-generated yet (see
 * `features/clusters/cluster-types.ts`); the local PlexorCluster shape
 * is the source of truth until the OpenAPI contract lands. Once
 * kubb generates the types, swap the local `PlexorCluster` alias for
 * the kubb-generated type without touching the fixture data.
 */

import type { PlexorCluster, PlexorNode, NodeStatus } from '@/features/clusters/cluster-types';

const PLEXOR_VERSION = '0.1.0-dev';

const NODES: PlexorNode[] = [
  {
    id: 'node-prod-eu-1',
    hostname: 'prod-eu-1.plexor.local',
    role: 'control',
    status: 'ready',
    spec: { vcpu: 32, ramGb: 128, diskGb: 4000, providers: ['kvm', 'ceph-rbd', 'ovs'] },
    isoVersion: PLEXOR_VERSION,
    joinedAt: '2026-07-12T08:00:00Z',
    lastSeenAt: '2026-09-19T09:59:30Z',
    vmCount: 6,
  },
  {
    id: 'node-staging-eu-1',
    hostname: 'staging-eu-1.plexor.local',
    role: 'compute',
    status: 'ready',
    spec: { vcpu: 16, ramGb: 64, diskGb: 2000, providers: ['kvm', 'lvm-thin'] },
    isoVersion: PLEXOR_VERSION,
    joinedAt: '2026-08-01T12:00:00Z',
    lastSeenAt: '2026-09-19T09:59:45Z',
    vmCount: 2,
  },
  {
    id: 'node-edge-ams-1',
    hostname: 'edge-ams-1.plexor.local',
    role: 'compute',
    status: 'pending',
    spec: { vcpu: 8, ramGb: 32, diskGb: 1000, providers: ['kvm'] },
    isoVersion: PLEXOR_VERSION,
    joinedAt: '2026-09-18T20:00:00Z',
    lastSeenAt: '2026-09-19T09:55:00Z',
    vmCount: 0,
  },
];

/** Single cluster for the v0.1 single-tenant deploy. The summary card
 *  counts its nodes; future multi-cluster fixtures add a second entry. */
export const CLUSTERS: ReadonlyArray<PlexorCluster> = [
  {
    id: 'cluster-prod-eu',
    name: 'prod-eu-1',
    installProviders: ['kvm', 'ceph-rbd', 'ovs'],
    hostVersion: PLEXOR_VERSION,
    uptimeSeconds: 60 * 60 * 24 * 14 + 60 * 60 * 2,
    endpoint: 'https://control.plexor.local:8443',
    createdAt: '2026-07-12T08:00:00Z',
    nodes: NODES,
    tokens: [],
  },
];

/** Index by cluster id — handy for MSW handlers that look up by id. */
export const CLUSTER_BY_ID: ReadonlyMap<string, PlexorCluster> = new Map(
  CLUSTERS.map((c) => [c.id, c]),
);

/** Aggregate counts across all clusters — drives the launcher SUMMARY
 *  card "N clusters · X/Y nodes ready" line. */
export function clusterSummary(): {
  clusters: number;
  total: number;
  ready: number;
  pending: number;
  offline: number;
  draining: number;
} {
  let total = 0;
  let ready = 0;
  let pending = 0;
  let offline = 0;
  let draining = 0;
  for (const cluster of CLUSTERS) {
    for (const node of cluster.nodes) {
      total += 1;
      if (node.status === 'ready') ready += 1;
      else if (node.status === 'pending') pending += 1;
      else if (node.status === 'offline') offline += 1;
      else if (node.status === 'draining') draining += 1;
    }
  }
  return { clusters: CLUSTERS.length, total, ready, pending, offline, draining };
}

/** Re-export node status so callers don't need to import from clusters. */
export type { NodeStatus };
