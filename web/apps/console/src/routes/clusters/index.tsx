import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { routeHead } from '@/shared/lib/route-head';
import { ClusterListBody, useListClusters } from '@/domains/fleet';

export const Route = createFileRoute('/clusters/')({
  component: ClustersPage,
  ...routeHead('Clusters'),
});

function ClustersPage() {
  const navigate = useNavigate();
  // Full fleet, fetched once — the strip, search and health chips filter
  // client-side (mock data is small; no per-keystroke refetch churn).
  const { clusters } = useListClusters();

  return (
    <ClusterListBody
      clusters={clusters}
      onOpenCluster={(cluster) => void navigate({ to: '/clusters/$id', params: { id: cluster.id } })}
    />
  );
}
