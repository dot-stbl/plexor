import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocalStorage } from '@uidotdev/usehooks';
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { toast } from 'sonner';
import type { Vm } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/shared/ui/primitives/alert-dialog';
import {
  DataTable,
  DataTableToolbar,
  applyFilters,
  emptyFilters,
  type DataTableColumnsState,
  type FilterValues,
} from '@/shared/ui/data-table';
import { VmBulkToolbar } from './vm-bulk-toolbar';
import { VmEmptyState, VmErrorBanner, VmNoResultsState, VmSkeleton } from './vm-states';
import { getVmColumns } from './vm-columns';
import {
  VmStatusStrip,
  VmStatusStripSkeleton,
  countByStatusFacet,
  isVmStatus,
  sumResourceTotals,
  vmStatusLabelKey,
} from './vm-status-strip';

interface VmListBodyProps {
  /** Full VM fleet for the page — filtering happens client-side. */
  items: ReadonlyArray<Vm>;
  isPending: boolean;
  isError: boolean;
  error: unknown;
  onRetry: () => void;
  /** Navigate to the create wizard (/vms/new). */
  onCreate: () => void;
  /** Navigate to a VM detail (/vms/$id). */
  onOpenVm: (vm: Vm) => void;
}

/**
 * The /vms list page body: status summary strip, toolbar, table, bulk
 * actions. Owns filter/selection state so the route stays a thin data+
 * navigation shell (and stories render this component with fixtures).
 *
 * Filtering is client-side (`applyFilters`): the status chips, the toolbar
 * search and the zone filter all compose over the same `items` array.
 */
export function VmListBody({ items, isPending, isError, error, onRetry, onCreate, onOpenVm }: VmListBodyProps) {
  const { t } = useTranslation();
  const columns = useMemo(() => getVmColumns(t), [t]);
  const filterDefault = useMemo(() => emptyFilters(columns), [columns]);
  const [filters, setFilters] = useState<FilterValues>(filterDefault);
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>('plexor-cols-vms', {
    hidden: [],
    order: [],
  });
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());
  const [deleteOpen, setDeleteOpen] = useState(false);

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
  const activeStatus = isVmStatus(statusFilter) ? statusFilter : null;

  const isEmptyFleet = !isPending && !isError && allItems.length === 0;
  const isNoResults = !isPending && !isError && allItems.length > 0 && filteredItems.length === 0;

  const toggle = useCallback((id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const toggleAll = useCallback((nextSelected: boolean) => {
    setSelectedIds(nextSelected ? new Set(filteredItems.map((vm) => vm.id)) : new Set());
  }, [filteredItems]);

  const clearSelection = useCallback(() => setSelectedIds(new Set()), []);

  const handleRowClick = useCallback(
    (vm: Vm) => {
      onOpenVm(vm);
    },
    [onOpenVm],
  );

  const confirmDelete = useCallback(() => {
    setDeleteOpen(false);
    toast(`${t('common.delete')} (${selectedIds.size})`);
    clearSelection();
  }, [t, selectedIds.size, clearSelection]);

  const resetFilters = useCallback(() => setFilters(filterDefault), [filterDefault]);

  const toggleStatus = useCallback((status: string) => {
    setFilters((prev) => ({ ...prev, status: prev.status === status ? '' : status }));
  }, []);

  // No-results copy acknowledges the active chip when it is the only filter.
  const searchActive = (filters.q ?? '') !== '' || (filters.zone ?? '') !== '';
  const noResultsTitle =
    activeStatus !== null && !searchActive
      ? t('vms.list.empty.noResultsStatusTitle', { status: t(vmStatusLabelKey(activeStatus)) })
      : undefined;

  return (
    <>
      <PageTemplate
        data-od-id="vms-list"
        title={t('vms.list.title')}
        width="wide"
        description={
          isPending ? (
            t('common.loading')
          ) : (
            <span>
              <MonoNum>{running}</MonoNum> <span className="text-muted-foreground">{t('vms.list.runningOf')}</span>{' '}
              <MonoNum>{allItems.length}</MonoNum> <span className="text-muted-foreground">{t('vms.list.total')}</span>
            </span>
          )
        }
        actions={
          <Button onClick={onCreate}>
            <Add />
            {t('vms.list.create')}
          </Button>
        }
      >
        {isPending ? (
          <div className="space-y-2">
            <VmStatusStripSkeleton />
            <VmSkeleton />
          </div>
        ) : isError ? (
          <VmErrorBanner error={error} onRetry={onRetry} />
        ) : isEmptyFleet ? (
          <VmEmptyState onCreate={onCreate} />
        ) : (
          <div className="space-y-2">
            <VmStatusStrip
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
              hiddenColumns={new Set(colState.hidden)}
              columnOrder={colState.order}
              selection={{
                selectedIds,
                onToggle: toggle,
                onToggleAll: toggleAll,
              }}
              onRowClick={handleRowClick}
            />
            {isNoResults && <VmNoResultsState title={noResultsTitle} onReset={resetFilters} />}
          </div>
        )}
      </PageTemplate>

      <VmBulkToolbar
        count={selectedIds.size}
        onClear={clearSelection}
        onStart={() => {
          toast(`${t('common.start')} (${selectedIds.size})`);
          clearSelection();
        }}
        onStop={() => {
          toast(`${t('common.stop')} (${selectedIds.size})`);
          clearSelection();
        }}
        onReboot={() => {
          toast(`${t('common.reboot')} (${selectedIds.size})`);
          clearSelection();
        }}
        onDelete={() => setDeleteOpen(true)}
      />

      <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <AlertDialogContent size="default">
          <AlertDialogHeader>
            <AlertDialogTitle>{t('vms.list.deleteTitle', { count: selectedIds.size })}</AlertDialogTitle>
            <AlertDialogDescription>{t('vms.list.deleteDescription')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={confirmDelete}>
              {t('common.delete')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
