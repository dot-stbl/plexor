import { createFileRoute } from '@tanstack/react-router';
import { ManagedServicePage } from '@/features/databases';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/managed/redis')({
  staticData: { crumb: 'Redis' },
  component: () => <ManagedServicePage engineId="redis" />,
  ...routeHead('Redis'),
});