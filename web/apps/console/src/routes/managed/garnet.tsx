import { createFileRoute } from '@tanstack/react-router';
import { ManagedServicePage } from '@/features/databases';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/managed/garnet')({
  staticData: { crumb: 'Garnet' },
  component: () => <ManagedServicePage engineId="garnet" />,
  ...routeHead('Garnet'),
});