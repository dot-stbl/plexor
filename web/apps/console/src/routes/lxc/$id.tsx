import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { LxcDetailBody, LxcDetailNotFound, listLxc } from '@/domains/compute';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/lxc/$id')({
  component: LxcContainerDetailPage,
  ...routeHead('LXC container'),
});

/**
 * Single LXC container detail.
 *
 * Data: the handmade mock is synchronous, so the page resolves the
 * container on render — found / not-found are the only live states today.
 * The pending state (`LxcDetailSkeleton`) is the seam the kubb query will
 * drive once GET /lxc/containers/{id} lands in the contract (see the
 * `isPending` seam on LxcListBody for the same arrangement).
 */
function LxcContainerDetailPage() {
  const navigate = useNavigate();
  const { id } = Route.useParams();
  const container = listLxc().find((candidate) => candidate.id === id) ?? null;

  useDocumentTitle(container?.name ?? null);

  const onBack = () => void navigate({ to: '/lxc' });

  if (!container) {
    return <LxcDetailNotFound id={id} onBack={onBack} />;
  }

  return <LxcDetailBody container={container} onBack={onBack} />;
}
