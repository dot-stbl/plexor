import { createFileRoute } from '@tanstack/react-router';
import { ManagedServicePage } from '@/domains/catalog';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/managed/clickhouse')({
  staticData: { crumb: 'ClickHouse' },
  component: () => <ManagedServicePage engineId="clickhouse" />,
  ...routeHead('ClickHouse'),
});