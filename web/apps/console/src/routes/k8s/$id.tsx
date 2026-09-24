import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { K8sDetailBody, K8sDetailNotFound, listK8s } from '@/domains/catalog';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/k8s/$id')({
  component: K8sClusterDetailPage,
  ...routeHead('Kubernetes cluster'),
});

/**
 * Single managed-K3s cluster detail.
 *
 * Data: the handmade mock is synchronous, so the page resolves the cluster
 * on render — found / not-found are the only live states today. The
 * pending state (`K8sDetailSkeleton`) is the seam the kubb `useGetK8s(id)`
 * query will drive once GET /k8s/{id} lands in the contract (see the
 * `isPending` seam on K8sListBody for the same arrangement).
 */
function K8sClusterDetailPage() {
  const navigate = useNavigate();
  const { id } = Route.useParams();
  const cluster = listK8s().find((candidate) => candidate.id === id) ?? null;

  useDocumentTitle(cluster?.name ?? null);

  const onBack = () => void navigate({ to: '/k8s' });

  if (!cluster) {
    return <K8sDetailNotFound id={id} onBack={onBack} />;
  }

  return <K8sDetailBody cluster={cluster} onBack={onBack} />;
}
