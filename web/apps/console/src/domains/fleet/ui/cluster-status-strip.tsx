import { useTranslation } from 'react-i18next';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { cn } from '@/shared/lib/utils';
import type { PlexorCluster } from '../model/cluster-types';
import {
  clusterHealth,
  clusterHealthLabelKey,
  mapClusterHealthToVariant,
  type ClusterHealth,
} from '../model/cluster-health';

/** Health order used everywhere the chips line up. */
const STRIP_HEALTH: readonly ClusterHealth[] = ['healthy', 'degraded', 'down'];

/** Type guard — the strip filter is a string state until it lands here. */
export function isClusterHealth(value: string): value is ClusterHealth {
  return STRIP_HEALTH.includes(value as ClusterHealth);
}

/** Facet counts over a cluster set — every health key present, zeros included. */
export function countByHealthFacet(items: ReadonlyArray<PlexorCluster>): Record<ClusterHealth, number> {
  const counts: Record<ClusterHealth, number> = { healthy: 0, degraded: 0, down: 0 };
  for (const cluster of items) {
    counts[clusterHealth(cluster.nodes)] += 1;
  }
  return counts;
}

/** Fleet totals over a cluster set (Σ clusters · nodes · ready). */
export interface FleetTotals {
  clusters: number;
  nodes: number;
  ready: number;
}

export function sumFleetTotals(items: ReadonlyArray<PlexorCluster>): FleetTotals {
  let nodes = 0;
  let ready = 0;
  for (const cluster of items) {
    nodes += cluster.nodes.length;
    for (const node of cluster.nodes) {
      if (node.status === 'ready') ready += 1;
    }
  }
  return { clusters: items.length, nodes, ready };
}

interface ClusterStatusStripProps {
  /** Facet counts — typically over the search-filtered set (health excluded). */
  counts: Record<ClusterHealth, number>;
  /** Currently active health filter, `null` when the health filter is off. */
  activeHealth: ClusterHealth | null;
  /** Toggle the health filter — the parent decides set-vs-clear. */
  onToggleHealth: (health: ClusterHealth) => void;
  /** Fleet totals over the currently filtered set (chips included). */
  totals: FleetTotals;
}

/**
 * Status summary strip above the cluster card grid: clickable health chips
 * (facet counts, toggle the grid's health filter) + fleet totals over the
 * visible set. Same composition as the compute-domain strips — deliberately
 * not a global primitive (one page, one strip).
 */
export function ClusterStatusStrip({ counts, activeHealth, onToggleHealth, totals }: ClusterStatusStripProps) {
  const { t } = useTranslation();

  return (
    <div
      data-od-id="clusters-status-strip"
      className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-lg border border-border bg-card px-3 py-2"
    >
      <div className="flex flex-wrap items-center gap-1" role="group" aria-label={t('clusters.list.strip.filterByHealth')}>
        {STRIP_HEALTH.map((health) => {
          const active = activeHealth === health;
          return (
            <button
              key={health}
              type="button"
              aria-pressed={active}
              onClick={() => onToggleHealth(health)}
              className={cn(
                'inline-flex items-center gap-1.5 rounded-full border border-transparent py-0.5 pr-2 pl-1 transition-colors',
                'hover:bg-muted/60 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden',
                active && 'border-border bg-muted',
              )}
            >
              <StatusPill variant={mapClusterHealthToVariant(health)} size="sm">
                {t(clusterHealthLabelKey(health))}
              </StatusPill>
              <MonoNum muted className="text-xs">
                {counts[health]}
              </MonoNum>
            </button>
          );
        })}
      </div>

      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
        <span aria-hidden>Σ</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{totals.clusters}</MonoNum>
          <span>{t('clusters.list.strip.clusters', { count: totals.clusters })}</span>
        </span>
        <span aria-hidden>·</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{totals.nodes}</MonoNum>
          <span>{t('clusters.list.strip.nodes', { count: totals.nodes })}</span>
        </span>
        <span aria-hidden>·</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{totals.ready}</MonoNum>
          <span>{t('clusters.list.strip.ready')}</span>
        </span>
      </div>
    </div>
  );
}

/** Skeleton matching the strip layout — chips block left, totals block right. */
export function ClusterStatusStripSkeleton() {
  return (
    <div
      data-od-id="clusters-status-strip-skeleton"
      className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-lg border border-border bg-card px-3 py-2"
    >
      <Skeleton className="h-6 w-56" />
      <Skeleton className="h-6 w-44" />
    </div>
  );
}
