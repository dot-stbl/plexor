import { useCallback, useMemo, useState } from 'react';
import { useLocalStorage } from '@uidotdev/usehooks';
import { useTranslation } from 'react-i18next';
import { Add, Search } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { PageTemplate } from '@/shared/ui/app-shell';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { DataTable, DataTableColumns, type DataTableColumnsState } from '@/shared/ui/data-table';
import type { DbCluster, DbEngine } from '../model/database-types';
import { getDbColumns } from './database-columns';
import { ManagedServiceEmpty } from './managed-service-empty';
import {
  DbStatusStrip,
  countDbByStatusFacet,
  dbStatusLabelKey,
  isDbStatus,
  sumDbTotals,
} from './db-status-strip';

interface ManagedServiceListBodyProps {
  /** Engine catalog entry (resolved by the page shell). */
  engine: DbEngine;
  /** Clusters of THIS engine only — the status chips filter client-side. */
  clusters: ReadonlyArray<DbCluster>;
  /** Create-cluster CTA — navigates to /managed/new with the engine preset. */
  onCreate: () => void;
}

/**
 * Engine cluster list body: status summary strip, column manager, table.
 * Owns the status-chip filter state so `ManagedServicePage` stays a thin
 * data shell (and stories render this component with fixtures).
 *
 * Filtering is client-side over `clusters` — this page has no toolbar, so
 * the chips are the only filter and facet counts span the full set.
 */
export function ManagedServiceListBody({ engine, clusters, onCreate }: ManagedServiceListBodyProps) {
  const { t } = useTranslation();
  const columns = useMemo(() => getDbColumns(t), [t]);
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>(`plexor-cols-managed-${engine.id}`, {
    hidden: [],
    order: [],
  });

  const allClusters = useMemo(() => clusters.slice(), [clusters]);

  const activeStatus = isDbStatus(statusFilter) ? statusFilter : null;
  const filteredClusters = useMemo(
    () => (activeStatus ? allClusters.filter((cluster) => cluster.status === activeStatus) : allClusters),
    [allClusters, activeStatus],
  );

  const counts = useMemo(() => countDbByStatusFacet(allClusters), [allClusters]);
  const totals = useMemo(() => sumDbTotals(filteredClusters), [filteredClusters]);
  const running = counts.running;

  const toggleStatus = useCallback((status: string) => {
    setStatusFilter((prev) => (prev === status ? '' : status));
  }, []);

  const resetFilters = useCallback(() => setStatusFilter(''), []);

  // With the chips as the only filter, an empty table always means the active
  // chip filtered everything out — the copy names it.
  const noResultsTitle =
    activeStatus !== null && filteredClusters.length === 0
      ? t('managed.list.empty.noResultsStatusTitle', { status: t(dbStatusLabelKey(activeStatus)) })
      : null;

  return (
    <PageTemplate
      data-od-id={`managed-${engine.id}`}
      width="wide"
      title={engine.name}
      description={
        allClusters.length > 0 ? (
          <span>
            <MonoNum>{running}</MonoNum> <span className="text-muted-foreground">{t('managed.list.runningOf')}</span>{' '}
            <MonoNum>{allClusters.length}</MonoNum>{' '}
            <span className="text-muted-foreground">{t('managed.list.total')}</span>
          </span>
        ) : (
          engine.blurb
        )
      }
      actions={
        allClusters.length > 0 ? (
          <Button onClick={onCreate}>
            <Add className="size-3.5" />
            {t('managed.list.create')}
          </Button>
        ) : null
      }
    >
      {allClusters.length > 0 ? (
        <div className="space-y-2">
          <DbStatusStrip
            counts={counts}
            activeStatus={activeStatus}
            onToggleStatus={toggleStatus}
            totals={totals}
          />
          <div className="flex justify-end">
            <DataTableColumns columns={columns} value={colState} onChange={setColState} />
          </div>
          <DataTable
            columns={columns}
            data={filteredClusters}
            density="compact"
            hiddenColumns={new Set(colState.hidden)}
            columnOrder={colState.order}
          />
          {noResultsTitle !== null && <DbNoResults title={noResultsTitle} onReset={resetFilters} />}
        </div>
      ) : (
        <ManagedServiceEmpty engine={engine} />
      )}
    </PageTemplate>
  );
}

/** Empty state — the active chip filtered every cluster out. */
function DbNoResults({ title, onReset }: { title: string; onReset: () => void }) {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="managed-no-results"
      icon={Search}
      title={title}
      description={t('managed.list.empty.noResultsDescription')}
      action={
        <Button variant="outline" size="sm" onClick={onReset}>
          {t('managed.list.empty.reset')}
        </Button>
      }
    />
  );
}
