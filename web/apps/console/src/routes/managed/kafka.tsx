import { createFileRoute } from '@tanstack/react-router';
import { ManagedServicePage } from '@/features/databases';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/managed/kafka')({
  staticData: { crumb: 'Apache Kafka' },
  component: () => <ManagedServicePage engineId="kafka" />,
  ...routeHead('Apache Kafka'),
});