import { createFileRoute } from '@tanstack/react-router';
import { ManagedServicePage } from '@/features/databases';

export const Route = createFileRoute('/managed/postgres')({
  staticData: { crumb: 'PostgreSQL' },
  component: () => <ManagedServicePage engineId="postgres" />,
});
