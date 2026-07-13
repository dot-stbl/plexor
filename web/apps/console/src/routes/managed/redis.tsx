import { createFileRoute } from '@tanstack/react-router';
import { ManagedServicePage } from '@/features/databases';

export const Route = createFileRoute('/managed/redis')({
  staticData: { crumb: 'Redis' },
  component: () => <ManagedServicePage engineId="redis" />,
});
