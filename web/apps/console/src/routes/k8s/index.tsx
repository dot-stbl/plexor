import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { routeHead } from '@/shared/lib/route-head';
import { K8sListBody, listK8s } from '@/domains/catalog';

export const Route = createFileRoute('/k8s/')({
  component: K8sPage,
  ...routeHead('Kubernetes'),
});

function K8sPage() {
  const navigate = useNavigate();
  // Handmade local fleet (endpoint not in the contract yet) — the strip,
  // search and status chips filter client-side.
  return (
    <K8sListBody
      items={listK8s()}
      onCreate={() => void navigate({ to: '/k8s/new' })}
      onOpenCluster={(cluster) => void navigate({ to: '/k8s/$id', params: { id: cluster.id } })}
    />
  );
}
