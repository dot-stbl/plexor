import { useMemo } from 'react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEngines, useListDbClusters, ManagedLanding, managedRoute } from '@/domains/catalog';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/managed/')({
  staticData: { crumb: 'Data platform' },
  component: ManagedLandingPage,
  ...routeHead('Data platform'),
});

/**
 * /managed — the «Data platform» landing: the engine catalog (one card per
 * engine, with the deployed-cluster count). Thin data + navigation shell;
 * the grid itself lives in `ManagedLanding` so stories render it with
 * fixtures.
 */
function ManagedLandingPage() {
  const navigate = useNavigate();
  const { engines } = useEngines();
  const { clusters, isPending } = useListDbClusters();

  const clusterCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const engine of engines) {
      counts[engine.id] = 0;
    }
    for (const cluster of clusters) {
      counts[cluster.engineId] = (counts[cluster.engineId] ?? 0) + 1;
    }
    return counts;
  }, [engines, clusters]);

  return (
    <ManagedLanding
      engines={engines}
      clusterCounts={clusterCounts}
      isPending={isPending}
      onOpenEngine={(engine) => void navigate({ to: managedRoute(engine.id) })}
    />
  );
}
