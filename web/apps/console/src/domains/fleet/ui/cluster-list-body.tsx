import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MenuBook, Search } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Input } from '@/shared/ui/primitives/input';
import type { PlexorCluster } from '../model/cluster-types';
import { clusterHealth, clusterHealthLabelKey, type ClusterHealth } from '../model/cluster-health';
import { ClusterCard } from './cluster-card';
import {
  ClusterErrorBanner,
  ClusterEmptyState,
  ClusterGridSkeleton,
  ClusterNoResultsState,
} from './cluster-states';
import {
  ClusterStatusStrip,
  ClusterStatusStripSkeleton,
  countByHealthFacet,
  isClusterHealth,
  sumFleetTotals,
} from './cluster-status-strip';

const noop = () => {};

interface ClusterListBodyProps {
  /** Full cluster fleet for the page — filtering happens client-side. */
  clusters: ReadonlyArray<PlexorCluster>;
  /** Navigate to a cluster detail (/clusters/$id). */
  onOpenCluster: (cluster: PlexorCluster) => void;
  /**
   * Loading seam — false today (the handmade mock is synchronous), true once
   * the kubb endpoint lands; the strip + grid show skeletons while set.
   */
  isPending?: boolean;
  /** Error seam — renders the retry banner instead of the grid. */
  isError?: boolean;
  error?: unknown;
  onRetry?: () => void;
}

/**
 * The /clusters list page body: health summary strip, name search, card
 * grid. Owns the search + health-chip filter state so the route stays a
 * thin data shell (and stories render this component with fixtures).
 *
 * Filtering is client-side: the health chips and the name search compose
 * over the same `clusters` array. Cards (not table rows) are the result
 * view — a cluster is a rich object operators compare at a glance.
 */
export function ClusterListBody({
  clusters,
  onOpenCluster,
  isPending = false,
  isError = false,
  error,
  onRetry,
}: ClusterListBodyProps) {
  const { t } = useTranslation();
  const [query, setQuery] = useState('');
  const [healthFilter, setHealthFilter] = useState('');

  const allClusters = useMemo(() => clusters.slice(), [clusters]);

  const activeHealth = isClusterHealth(healthFilter) ? healthFilter : null;

  // Chips + search compose: everything applies to the grid…
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return allClusters.filter((cluster) => {
      const matchesQuery = q === '' || cluster.name.toLowerCase().includes(q);
      const matchesHealth = activeHealth === null || clusterHealth(cluster.nodes) === activeHealth;
      return matchesQuery && matchesHealth;
    });
  }, [allClusters, query, activeHealth]);

  // …while chip COUNTS ignore the health filter (facet counts follow the search only).
  const facetItems = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (q === '') return allClusters;
    return allClusters.filter((cluster) => cluster.name.toLowerCase().includes(q));
  }, [allClusters, query]);

  const counts = useMemo(() => countByHealthFacet(facetItems), [facetItems]);
  const totals = useMemo(() => sumFleetTotals(filtered), [filtered]);

  const isEmptyFleet = !isPending && !isError && allClusters.length === 0;
  const isNoResults = !isPending && !isError && allClusters.length > 0 && filtered.length === 0;

  const resetFilters = useCallback(() => {
    setQuery('');
    setHealthFilter('');
  }, []);

  const toggleHealth = useCallback((health: ClusterHealth) => {
    setHealthFilter((prev) => (prev === health ? '' : health));
  }, []);

  // No-results copy acknowledges the active chip when it is the only filter.
  const searchActive = query.trim() !== '';
  const noResultsTitle =
    activeHealth !== null && !searchActive
      ? t('clusters.list.empty.noResultsHealthTitle', { status: t(clusterHealthLabelKey(activeHealth)) })
      : undefined;

  return (
    <PageTemplate
      data-od-id="clusters-list"
      title={t('clusters.list.title')}
      width="wide"
      description={
        isPending ? (
          t('common.loading')
        ) : (
          <span>
            <MonoNum>{totals.ready}</MonoNum>
            <span className="text-muted-foreground">/</span>
            <MonoNum>{totals.nodes}</MonoNum>{' '}
            <span className="text-muted-foreground">{t('clusters.list.nodesReady')}</span>
            <span aria-hidden> · </span>
            <MonoNum>{allClusters.length}</MonoNum>{' '}
            <span className="text-muted-foreground">{t('clusters.list.totalClusters', { count: allClusters.length })}</span>
          </span>
        )
      }
      actions={
        <Button nativeButton={false} render={<a href="https://plexor.dev/docs/install" target="_blank" rel="noreferrer" />}>
          <MenuBook />
          {t('clusters.list.docs')}
        </Button>
      }
    >
      {isPending ? (
        <div className="space-y-2">
          <ClusterStatusStripSkeleton />
          <ClusterGridSkeleton />
        </div>
      ) : isError ? (
        <ClusterErrorBanner error={error} onRetry={onRetry ?? noop} />
      ) : isEmptyFleet ? (
        <ClusterEmptyState />
      ) : (
        <div className="space-y-2">
          <ClusterStatusStrip
            counts={counts}
            activeHealth={activeHealth}
            onToggleHealth={toggleHealth}
            totals={totals}
          />
          <div className="relative max-w-64">
            <Search className="pointer-events-none absolute top-1/2 left-2 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder={t('clusters.list.searchPlaceholder')}
              aria-label={t('clusters.list.searchPlaceholder')}
              className="pl-7"
            />
          </div>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
            {filtered.map((cluster) => (
              <ClusterCard key={cluster.id} cluster={cluster} onOpen={() => onOpenCluster(cluster)} />
            ))}
          </div>
          {isNoResults && <ClusterNoResultsState title={noResultsTitle} onReset={resetFilters} />}
        </div>
      )}
    </PageTemplate>
  );
}
