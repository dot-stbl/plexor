import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { routeHead } from '@/shared/lib/route-head';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import { ClusterDetailBody, useGetCluster } from '@/domains/fleet';

export const Route = createFileRoute('/clusters/$id')({
  component: ClusterDetailPage,
  ...routeHead('Cluster'),
});

function ClusterDetailPage() {
  const navigate = useNavigate();
  const { id } = Route.useParams();
  const { cluster } = useGetCluster(id);
  useDocumentTitle(cluster?.name ?? null);

  return (
    <ClusterDetailBody
      clusterId={id}
      cluster={cluster}
      onBack={() => void navigate({ to: '/clusters' })}
    />
  );
}
