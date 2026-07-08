import { useMemo } from 'react';
import type { PlexorCluster, JoinToken, NodeRole, PlexorNode, TokenStatus } from './cluster-types';
import { getCluster, listClusters } from './cluster-data';

/**
 * Local hooks for cluster/node/token data. Stand-in for kubb-generated
 * hooks until the OpenAPI spec lands.
 */
export function useListClusters(): { clusters: PlexorCluster[]; isPending: false; error: null } {
  return useMemo(() => ({ clusters: listClusters(), isPending: false, error: null }), []);
}

export function useGetCluster(id: string): { cluster: PlexorCluster | undefined; isPending: false } {
  return useMemo(() => ({ cluster: getCluster(id), isPending: false }), [id]);
}

export function useListNodes(clusterId: string): { nodes: PlexorNode[]; isPending: false } {
  return useMemo(() => ({ nodes: getCluster(clusterId)?.nodes ?? [], isPending: false }), [clusterId]);
}

export function useListTokens(clusterId: string): { tokens: JoinToken[]; isPending: false } {
  return useMemo(() => ({ tokens: getCluster(clusterId)?.tokens ?? [], isPending: false }), [clusterId]);
}

/** Issue a new join token. Local mutator — real impl would call a kubb mutation. */
export function issueJoinToken(
  clusterId: string,
  args: { label: string; intendedRole: NodeRole; ttlDays: number },
): JoinToken {
  const cluster = getCluster(clusterId);
  if (!cluster) throw new Error(`Cluster not found: ${clusterId}`);
  const now = new Date();
  const expires = new Date(now.getTime() + args.ttlDays * 24 * 3600 * 1000);
  // Demo token — deterministic but opaque-looking.
  const hex = Math.random().toString(16).slice(2).padEnd(96, '0');
  const token: JoinToken = {
    id: `tok-${Date.now().toString(36)}`,
    label: args.label,
    status: 'active',
    token: `plx_jtok_${hex.slice(0, 96)}`,
    intendedRole: args.intendedRole,
    minIsoVersion: cluster.hostVersion,
    issuedAt: now.toISOString(),
    expiresAt: expires.toISOString(),
  };
  cluster.tokens.unshift(token);
  return token;
}

/** Revoke a join token. */
export function revokeJoinToken(clusterId: string, tokenId: string): void {
  const cluster = getCluster(clusterId);
  if (!cluster) return;
  const token = cluster.tokens.find((t) => t.id === tokenId);
  if (token) token.status = 'revoked' satisfies TokenStatus;
}