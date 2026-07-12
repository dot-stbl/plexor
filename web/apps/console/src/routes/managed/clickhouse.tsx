import { createFileRoute } from '@tanstack/react-router';
import { ManagedServicePage } from '@/features/databases';

export const Route = createFileRoute('/managed/clickhouse')({
  staticData: { crumb: 'ClickHouse' },
  component: () => <ManagedServicePage engineId="clickhouse" />,
});
