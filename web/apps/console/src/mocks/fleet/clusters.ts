/**
 * Cluster fixtures — the fleet bounded context. Single install for the
 * v0.1 self-hosted deploy: one cluster (\"prod-eu-1\") wrapping the node
 * roster from `compute/nodes.ts` plus its join tokens. Feeds the
 * launcher SUMMARY card (`CLUSTERS`/`CLUSTER_BY_ID`/`clusterSummary`)
 * and the clusters feature pages (`listClusters`/`getCluster`) —
 * previously two diverging fixtures (this file + the old
 * `catalog/clusters-install.ts`), now reconciled into one.
 */
import type { JoinToken, PlexorCluster, NodeStatus } from '@/domains/fleet';
import { NODES } from '../compute/nodes';

const TOKENS: JoinToken[] = [
  {
    id: 'tok-001',
    label: 'pve-rack1-n02 initial join',
    status: 'expired',
    token: 'plx_jtok_5f8c2e9a1b4d7e0f3a6b9c2d5e8f1a4b7c0d3e6f9a2b5c8d1e4f7a0b3c6d9e2f5a8b1c4d7e0f3a6b9c2d5e8f1a4b',
    intendedRole: 'compute',
    minIsoVersion: 'plexor-1.0.0',
    issuedAt: '2026-03-01T07:30:00Z',
    expiresAt: '2026-03-08T07:30:00Z',
    redeemedByNodeId: 'node-pve-r1n02',
  },
  {
    id: 'tok-002',
    label: 'pve-rack2-n01 initial join',
    status: 'expired',
    token: 'plx_jtok_6a9d3f0b2c5e8f1a4b7d0e3f6a9c2d5e8f1b4d7e0f3a6b9c2d5e8f1a4b7d0e3f6a9b2c5d8e1f4a7b0c3d6e9f2a5b8c1d',
    intendedRole: 'compute',
    minIsoVersion: 'plexor-1.0.0',
    issuedAt: '2026-04-15T09:30:00Z',
    expiresAt: '2026-04-22T09:30:00Z',
    redeemedByNodeId: 'node-pve-r2n01',
  },
  {
    id: 'tok-003',
    label: 'pve-rack2-n02 initial join',
    status: 'expired',
    token: 'plx_jtok_7b0e4a1c3d6f9a2b5c8e1f4a7b0d3e6f9a2c5d8e1f4a7b0c3d6e9f2a5b8c1d4e7f0a3b6c9d2e5f8a1b4c7d0e3f6a9b2c5d',
    intendedRole: 'compute',
    minIsoVersion: 'plexor-1.2.0',
    issuedAt: '2026-05-20T10:30:00Z',
    expiresAt: '2026-05-27T10:30:00Z',
    redeemedByNodeId: 'node-pve-r2n02',
  },
  {
    id: 'tok-004',
    label: 'edge-pop-amsterdam',
    status: 'active',
    token: 'plx_jtok_8c1f5b2d4e7a0c3d6e9f2a5b8c1d4e7f0a3b6c9d2e5f8a1b4c7d0e3f6a9b2c5d8e1f4a7b0c3d6e9f2a5b8c1d4e7f0a3b6c',
    intendedRole: 'compute',
    minIsoVersion: 'plexor-1.2.3',
    issuedAt: '2026-09-18T19:30:00Z',
    expiresAt: '2026-09-25T19:30:00Z',
  },
];

export const CLUSTERS: ReadonlyArray<PlexorCluster> = [
  {
    id: 'cluster-prod-eu-1',
    name: 'prod-eu-1',
    installProviders: ['kvm', 'ceph-rbd', 'ovs', 'ceph-rgw', 'postgresql', 'nats'],
    hostVersion: '1.2.3',
    uptimeSeconds: 14 * 24 * 3600 + 2 * 3600 + 17 * 60,
    endpoint: 'https://prod-eu-1.plexor.local:8443',
    createdAt: '2026-03-01T08:00:00Z',
    nodes: NODES,
    tokens: TOKENS,
  },
];

/** Index by cluster id. */
export const CLUSTER_BY_ID: ReadonlyMap<string, PlexorCluster> = new Map(
  CLUSTERS.map((c) => [c.id, c]),
);

export function listClusters(): PlexorCluster[] {
  return CLUSTERS.slice();
}

export function getCluster(id: string): PlexorCluster | undefined {
  return CLUSTER_BY_ID.get(id);
}

/** Aggregate counts across all clusters — drives the launcher SUMMARY
 *  card \"N clusters · X/Y nodes ready\" line. */
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
