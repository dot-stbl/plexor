import { createFileRoute } from '@tanstack/react-router';
import { ManagedServicePage } from '@/features/databases';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/managed/postgres')({
  staticData: { crumb: 'PostgreSQL' },
  component: () => <ManagedServicePage engineId="postgres" />,
  ...routeHead('PostgreSQL'),
});