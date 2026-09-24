import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocalStorage } from '@uidotdev/usehooks';
import { toast } from 'sonner';
import { Add, Delete, Hexagon } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { BulkActionToolbar } from '@/shared/ui/primitives/bulk-action-toolbar';
import { PageTemplate } from '@/shared/ui/app-shell';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import {
  DataTable,
  DataTableToolbar,
  applyFilters,
  emptyFilters,
  useRowSelection,
  type DataTableColumnsState,
  type FilterValues,
} from '@/shared/ui/data-table';
import { getK8sColumns } from './k8s-columns';
import { K8sNoResultsState, K8sSkeleton } from './k8s-states';
import {
  K8sStatusStrip,
  K8sStatusStripSkeleton,
  countByStatusFacet,
  isK8sStatus,
  k8sStatusLabelKey,
  sumResourceTotals,
} from './k8s-status-strip';
import type { K8sCluster } from '../model/k8s-types';

interface K8sListBodyProps {
  /** Full cluster fleet for the page — filtering happens client-side. */
  items: ReadonlyArray<K8sCluster>;
  /**
   * Loading seam — false today (the handmade mock is synchronous), true once
   * the kubb endpoint lands; the strip + table show skeletons while set.
   */
  isPending?: boolean;
  /** Navigate to the create wizard (/k8s/new). */
  onCreate: () => void;
}

/**
 * The /k8s list page body: status summary strip, toolbar, table, bulk
 * actions. Owns filter/selection state so the route stays a thin data+
 * navigation shell (and stories render this component with fixtures).
 *
 * Filtering is client-side (`applyFilters`): the status chips and the
 * toolbar name search compose over the same `items` array.
 */
export function K8sListBody({ items, isPending = false, onCreate }: K8sListBodyProps) {
  const { t } = useTranslation();
  const columns = useMemo(() => getK8sColumns(t), [t]);
  const filterDefault = useMemo(() => emptyFilters(columns), [columns]);
  const [filters, setFilters] = useState<FilterValues>(filterDefault);
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>('plexor-cols-k8s', {
    hidden: [],
    order: [],
  });

  const allItems = useMemo(() => items.slice(), [items]);

  // Chips + toolbar compose: everything applies to the table…
  const filteredItems = useMemo(() => applyFilters(allItems, filters, columns), [allItems, filters, columns]);
  // …while chip COUNTS ignore the status filter (facet counts follow the search only).
  const searchFilters = useMemo(() => {
    const next = { ...filters };
    delete next.status;
    return next;
  }, [filters]);
  const facetItems = useMemo(() => applyFilters(allItems, searchFilters, columns), [allItems, searchFilters, columns]);

  const counts = useMemo(() => countByStatusFacet(facetItems), [facetItems]);
  const totals = useMemo(() => sumResourceTotals(filteredItems), [filteredItems]);
  const running = counts.running;

  const statusFilter = filters.status ?? '';
  const activeStatus = isK8sStatus(statusFilter) ? statusFilter : null;

  const sel = useRowSelection(filteredItems);

  const resetFilters = useCallback(() => setFilters(filterDefault), [filterDefault]);

  const toggleStatus = useCallback((status: string) => {
    setFilters((prev) => ({ ...prev, status: prev.status === status ? '' : status }));
  }, []);

  // No-results copy acknowledges the active chip when it is the only filter.
  const searchActive = (filters.name ?? '') !== '';
  const noResultsTitle =
    activeStatus !== null && !searchActive
      ? t('k8s.list.empty.noResultsStatusTitle', { status: t(k8sStatusLabelKey(activeStatus)) })
      : undefined;

  return (
    <>
      <PageTemplate
        data-od-id="k8s-list"
        width="wide"
        title={t('k8s.list.title')}
        description={
          isPending ? (
            t('common.loading')
          ) : (
            <span>
              <MonoNum>{running}</MonoNum> <span className="text-muted-foreground">{t('k8s.list.runningOf')}</span>{' '}
              <MonoNum>{allItems.length}</MonoNum> <span className="text-muted-foreground">{t('k8s.list.total')}</span>
            </span>
          )
        }
        actions={
          isPending || allItems.length > 0 ? (
            <Button onClick={onCreate}>
              <Add />
              {t('k8s.list.create')}
            </Button>
          ) : undefined
        }
      >
        {isPending ? (
          <div className="space-y-2">
            <K8sStatusStripSkeleton />
            <K8sSkeleton />
          </div>
        ) : allItems.length === 0 ? (
          <EmptyState
            icon={Hexagon}
            title={t('k8s.list.empty.title')}
            description={t('k8s.list.empty.description')}
            docsLabel={t('k8s.list.docs.label')}
            docs={[
              { href: 'https://plexor.dev/docs/k8s', label: t('k8s.list.docs.managed') },
              { href: 'https://plexor.dev/docs/k8s/node-pools', label: t('k8s.list.docs.nodePools') },
            ]}
            action={
              <Button onClick={onCreate}>
                <Add className="size-3.5" />
                {t('k8s.list.create')}
              </Button>
            }
          />
        ) : (
          <div className="space-y-2">
            <K8sStatusStrip
              counts={counts}
              activeStatus={activeStatus}
              onToggleStatus={toggleStatus}
              totals={totals}
            />
            <DataTableToolbar
              columns={columns}
              filters={filters}
              onFiltersChange={setFilters}
              columnsState={colState}
              onColumnsChange={setColState}
            />
            <DataTable
              columns={columns}
              data={filteredItems}
              density="compact"
              selection={sel.selection}
              hiddenColumns={new Set(colState.hidden)}
              columnOrder={colState.order}
            />
            {filteredItems.length === 0 && <K8sNoResultsState title={noResultsTitle} onReset={resetFilters} />}
          </div>
        )}
      </PageTemplate>

      <BulkActionToolbar
        count={sel.selectedIds.size}
        onClear={sel.clear}
        actions={[
          {
            label: t('common.delete'),
            icon: <Delete />,
            variant: 'destructive',
            onClick: () => {
              toast(`${t('common.delete')} (${sel.selectedIds.size})`);
              sel.clear();
            },
          },
        ]}
      />
    </>
  );
}
