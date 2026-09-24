import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { routeHead } from '@/shared/lib/route-head';
import { LxcListBody, listLxc } from '@/domains/compute';

export const Route = createFileRoute('/lxc/')({
  component: LxcPage,
  ...routeHead('LXC'),
});

function LxcPage() {
  const navigate = useNavigate();
  // Handmade local inventory (endpoint not in the contract yet) — the strip,
  // search and status chips filter client-side.
  return (
    <LxcListBody
      items={listLxc()}
      onCreate={() => void navigate({ to: '/lxc/new' })}
      onOpenContainer={(container) => void navigate({ to: '/lxc/$id', params: { id: container.id } })}
    />
  );
}
