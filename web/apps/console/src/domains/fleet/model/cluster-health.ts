import type { StatusVariant } from '@/shared/ui/primitives/status-pill';
import type { NodeCounts, PlexorNode } from './cluster-types';

/**
 * Derived fleet-level health of a cluster — the node-status mix rolled up
 * into one operator-glance value. Shared by the list strip, the cluster
 * card pill and the detail header so all three always agree.
 *
 * Rule (mirrors the shipped ClusterCard behaviour):
 * - `down`      — ≥ 50% of nodes offline;
 * - `degraded`  — any node offline or draining;
 * - `healthy`   — everything else (pending joins don't degrade the cluster).
 */
export type ClusterHealth = 'healthy' | 'degraded' | 'down';

export function clusterHealth(nodes: ReadonlyArray<PlexorNode>): ClusterHealth {
  const total = nodes.length;
  let offline = 0;
  let draining = 0;
  for (const node of nodes) {
    if (node.status === 'offline') offline += 1;
    else if (node.status === 'draining') draining += 1;
  }
  if (total > 0 && offline / total >= 0.5) return 'down';
  if (offline > 0 || draining > 0) return 'degraded';
  return 'healthy';
}

/** Same rule over pre-computed `NodeCounts` (the card already counts). */
export function clusterHealthFromCounts(counts: NodeCounts): ClusterHealth {
  if (counts.total > 0 && counts.offline / counts.total >= 0.5) return 'down';
  if (counts.offline > 0 || counts.draining > 0) return 'degraded';
  return 'healthy';
}

/** Cluster health → Plexor DS status variant (single closed-enum switch). */
export function mapClusterHealthToVariant(health: ClusterHealth): StatusVariant {
  switch (health) {
    case 'healthy':
      return 'running';
    case 'degraded':
      return 'pending';
    case 'down':
      return 'err';
  }
}

/** i18n key for a health label (strip chips, card pill, detail header). */
export function clusterHealthLabelKey(health: ClusterHealth): string {
  switch (health) {
    case 'healthy':
      return 'clusters.health.healthy';
    case 'degraded':
      return 'clusters.health.degraded';
    case 'down':
      return 'clusters.health.down';
  }
}
