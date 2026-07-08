import { useMemo } from 'react';
import type { Cluster, ClusterFlavor } from './cluster-types';
import { getCluster, listClusters } from './cluster-data';

/**
 * Local hook — returns the cluster fleet. Stand-in for the kubb-generated
 * `useListClusters` until the cluster OpenAPI spec lands.
 *
 * Returned object is memoized on the hook's identity so it stays referentially
 * stable across renders.
 */
export function useListClusters(): { clusters: Cluster[]; isPending: false; error: null } {
  // Real version: would call useQuery({ queryKey: listClustersQueryKey(), queryFn: listClusters }).
  // For now: synchronous mock.
  return useMemo(() => ({ clusters: listClusters(), isPending: false, error: null }), []);
}

/**
 * Local hook — returns a single cluster by id, or `undefined` while loading.
 * Mirrors the kubb-generated `useGetCluster` API.
 */
export function useGetCluster(id: string): { cluster: Cluster | undefined; isPending: false } {
  return useMemo(() => ({ cluster: getCluster(id), isPending: false }), [id]);
}

/** Convenience: extract available flavors for a cluster. */
export function clusterFlavors(cluster: Cluster | undefined): ClusterFlavor[] {
  return cluster?.flavors ?? [];
}