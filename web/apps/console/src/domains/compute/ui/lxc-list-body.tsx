import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocalStorage } from '@uidotdev/usehooks';
import { toast } from 'sonner';
import { Add, Delete, DeployedCode, PlayArrow, Stop } from '@nine-thirty-five/material-symbols-react/rounded/700';
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
import { getLxcColumns } from './lxc-columns';
import { LxcNoResultsState } from './lxc-states';
import {
  LxcStatusStrip,
  countByStatusFacet,
  isLxcStatus,
  lxcStatusLabelKey,
  sumResourceTotals,
} from './lxc-status-strip';
import type { LxcContainer } from '../model/lxc-types';

interface LxcListBodyProps {
  /** Full container inventory for the page — filtering happens client-side. */
  items: ReadonlyArray<LxcContainer>;
  /** Navigate to the create wizard (/lxc/new). */
  onCreate: () => void;
}

/**
 * The /lxc list page body: status summary strip, toolbar, table, bulk
 * actions. Owns filter/selection state so the route stays a thin data+
 * navigation shell (and stories render this component with fixtures).
 *
 * Filtering is client-side (`applyFilters`): the status chips and the
 * toolbar name search compose over the same `items` array.
 */
export function LxcListBody({ items, onCreate }: LxcListBodyProps) {
  const { t } = useTranslation();
  const columns = useMemo(() => getLxcColumns(t), [t]);
  const filterDefault = useMemo(() => emptyFilters(columns), [columns]);
  const [filters, setFilters] = useState<FilterValues>(filterDefault);
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>('plexor-cols-lxc', {
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
  const activeStatus = isLxcStatus(statusFilter) ? statusFilter : null;

  const sel = useRowSelection(filteredItems);

  const resetFilters = useCallback(() => setFilters(filterDefault), [filterDefault]);

  const toggleStatus = useCallback((status: string) => {
    setFilters((prev) => ({ ...prev, status: prev.status === status ? '' : status }));
  }, []);

  const bulk = (label: string) => {
    toast(`${label} (${sel.selectedIds.size})`);
    sel.clear();
  };

  // No-results copy acknowledges the active chip when it is the only filter.
  const searchActive = (filters.name ?? '') !== '';
  const noResultsTitle =
    activeStatus !== null && !searchActive
      ? t('lxc.list.empty.noResultsStatusTitle', { status: t(lxcStatusLabelKey(activeStatus)) })
      : undefined;

  return (
    <>
      <PageTemplate
        data-od-id="lxc-list"
        width="wide"
        title={t('lxc.list.title')}
        description={
          <span>
            <MonoNum>{running}</MonoNum> <span className="text-muted-foreground">{t('lxc.list.runningOf')}</span>{' '}
            <MonoNum>{allItems.length}</MonoNum> <span className="text-muted-foreground">{t('lxc.list.total')}</span>
          </span>
        }
        actions={
          allItems.length > 0 ? (
            <Button onClick={onCreate}>
              <Add />
              {t('lxc.list.create')}
            </Button>
          ) : undefined
        }
      >
        {allItems.length === 0 ? (
          <EmptyState
            icon={DeployedCode}
            title={t('lxc.list.empty.title')}
            description={t('lxc.list.empty.description')}
            docsLabel={t('lxc.list.docs.label')}
            docs={[
              { href: 'https://plexor.dev/docs/lxc', label: t('lxc.list.docs.howItWorks') },
              { href: 'https://plexor.dev/docs/runtimes', label: t('lxc.list.docs.runtimes') },
            ]}
            action={
              <Button onClick={onCreate}>
                <Add className="size-3.5" />
                {t('lxc.list.create')}
              </Button>
            }
          />
        ) : (
          <div className="space-y-2">
            <LxcStatusStrip
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
            {filteredItems.length === 0 && <LxcNoResultsState title={noResultsTitle} onReset={resetFilters} />}
          </div>
        )}
      </PageTemplate>

      <BulkActionToolbar
        count={sel.selectedIds.size}
        onClear={sel.clear}
        actions={[
          { label: t('common.start'), icon: <PlayArrow />, onClick: () => bulk(t('common.start')) },
          { label: t('common.stop'), icon: <Stop />, onClick: () => bulk(t('common.stop')) },
          { label: t('common.delete'), icon: <Delete />, variant: 'destructive', onClick: () => bulk(t('common.delete')) },
        ]}
      />
    </>
  );
}
