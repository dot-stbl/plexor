import { createFileRoute } from '@tanstack/react-router';
import { ManagedServicePage } from '@/features/databases';

export const Route = createFileRoute('/managed/garnet')({
  component: () => <ManagedServicePage engineId="garnet" />,
});
