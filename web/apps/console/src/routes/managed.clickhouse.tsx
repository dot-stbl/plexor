import { createFileRoute } from '@tanstack/react-router';
import { ManagedServicePage } from '@/features/databases';

export const Route = createFileRoute('/managed/clickhouse')({
  component: () => <ManagedServicePage engineId="clickhouse" />,
});
