import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { routeHead } from '@/shared/lib/route-head';
import { useListVms, VmListBody } from '@/domains/compute';

export const Route = createFileRoute('/vms/')({
  component: VmsPage,
  ...routeHead('VMs'),
});

function VmsPage() {
  const navigate = useNavigate();
  // Full fleet, fetched once — the strip, search and status chips filter
  // client-side (mock data is small; no per-keystroke refetch churn).
  const { data, isPending, isError, error, refetch } = useListVms();

  return (
    <VmListBody
      items={data?.items ?? []}
      isPending={isPending}
      isError={isError}
      error={error}
      onRetry={() => void refetch()}
      onCreate={() => void navigate({ to: '/vms/new' })}
      onOpenVm={(vm) => void navigate({ to: '/vms/$id', params: { id: vm.id } })}
    />
  );
}
