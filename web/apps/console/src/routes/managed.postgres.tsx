import { createFileRoute } from '@tanstack/react-router';
import { ManagedServicePage } from '@/features/databases';

export const Route = createFileRoute('/managed/postgres')({
  component: () => <ManagedServicePage engineId="postgres" />,
});
