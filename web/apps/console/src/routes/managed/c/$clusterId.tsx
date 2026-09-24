import { createFileRoute, useNavigate } from '@tanstack/react-router';
import {
  ManagedClusterDetail,
  ManagedClusterNotFound,
  ManagedClusterSkeleton,
  managedRoute,
  useEngine,
  useListDbClusters,
} from '@/domains/catalog';
import { routeHead } from '@/shared/lib/route-head';
import { useDocumentTitle } from '@/shared/lib/use-document-title';

export const Route = createFileRoute('/managed/c/$clusterId')({
  staticData: { crumb: 'Cluster' },
  component: ManagedClusterPage,
  ...routeHead('DB cluster'),
});

/**
 * DB cluster detail (/managed/c/<clusterId>). Thin data shell: it resolves
 * the cluster by id (ids are globally unique across engines), derives the
 * engine from the row, and renders the detail / skeleton / not-found body.
 *
 * Why no /<engine>/ segment: the engine sections are static routes
 * (/managed/postgres, …) and this router version does not backtrack from a
 * static partial match to a param sibling — /managed/clickhouse/c/<id>
 * would 404 on the static route. The `c` segment collides with no static
 * sibling, so /managed/c/<id> matches cleanly.
 */
function ManagedClusterPage() {
  const navigate = useNavigate();
  const { clusterId } = Route.useParams();
  const { clusters, isPending } = useListDbClusters();

  const cluster = clusters.find((candidate) => candidate.id === clusterId);
  const engine = useEngine(cluster?.engineId);

  useDocumentTitle(cluster?.name ?? null);

  if (isPending) {
    return <ManagedClusterSkeleton onBack={() => void navigate({ to: '/managed' })} />;
  }

  if (!cluster || !engine) {
    return <ManagedClusterNotFound clusterId={clusterId} onBack={() => void navigate({ to: '/managed' })} />;
  }

  return (
    <ManagedClusterDetail
      engine={engine}
      cluster={cluster}
      onBack={() => void navigate({ to: managedRoute(engine.id) })}
    />
  );
}
