/**
 * Public surface of the fleet domain (physical node fleet: clusters,
 * nodes, join tokens). Routes import from '@/domains/fleet'; internal
 * model/api/ui files stay unexported outside this barrel (see
 * .agents/docs/architecture/frontend-ddd.md).
 */
export type {
  PlexorCluster,
  PlexorNode,
  JoinToken,
  NodeStatus,
  NodeRole,
  TokenStatus,
  NodeSpec,
  NodeCounts,
} from './model/cluster-types';
export { countNodes, formatUptime } from './model/cluster-types';
export type { ClusterHealth } from './model/cluster-health';
export {
  clusterHealth,
  clusterHealthFromCounts,
  clusterHealthLabelKey,
  mapClusterHealthToVariant,
} from './model/cluster-health';
export {
  mapNodeStatusToVariant,
  nodeRoleLabelKey,
  nodeStatusLabelKey,
  tokenStatusLabelKey,
} from './model/node-status';
export { useListClusters, useGetCluster, useListNodes, useListTokens, issueJoinToken, revokeJoinToken } from './api/use-clusters';
export { listClusters, getCluster } from '@/shared/api/mocks/handmade/clusters';
export { ClusterCard } from './ui/cluster-card';
export { ClusterListBody } from './ui/cluster-list-body';
export {
  ClusterErrorBanner,
  ClusterEmptyState,
  ClusterGridSkeleton,
  ClusterNoResultsState,
} from './ui/cluster-states';
export {
  ClusterStatusStrip,
  ClusterStatusStripSkeleton,
  countByHealthFacet,
  isClusterHealth,
  sumFleetTotals,
} from './ui/cluster-status-strip';
export type { FleetTotals } from './ui/cluster-status-strip';
export { ClusterDetailBody } from './ui/cluster-detail-body';
export { NodeCard } from './ui/node-card';
export { TokenRow } from './ui/token-row';
export { AddNodeDialog } from './ui/add-node-dialog';
