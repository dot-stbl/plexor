import { useCallback, useMemo, useState } from 'react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useListVms } from '@/shared/api';
import { Button } from '@/shared/ui/primitives/button';
import { PageTemplate } from '@/shared/ui/app-shell';
import { useLocalStorage } from '@uidotdev/usehooks';
import { DataTable, DataTableToolbar, emptyFilters, compactFilters, type FilterValues, type DataTableColumnsState } from '@/shared/ui/data-table';
import { VmBulkToolbar, VmEmptyState, VmErrorBanner, VmNoResultsState, VmSkeleton, getVmColumns } from '@/features/vms';
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
import { toast } from 'sonner';
import { MonoNum } from '@/shared/ui/primitives/mono-num';

export const Route = createFileRoute('/vms/')({
  component: VmsPage,
});

function VmsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const columns = useMemo(() => getVmColumns(t), [t]);
  const filterDefault = useMemo(() => emptyFilters(columns), [columns]);
  const [filters, setFilters] = useState<FilterValues>(filterDefault);
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>('plexor-cols-vms', {
    hidden: [],
    order: [],
  });
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());
  const [deleteOpen, setDeleteOpen] = useState(false);

  // Server-side filtering: drop empty filter values, hand the rest to the API.
  const apiParams = useMemo(() => compactFilters(filters), [filters]);

  const { data, isPending, isError, error, refetch } = useListVms(apiParams);

  const allItems = data?.items ?? [];
  const total = data?.total ?? 0;
  const running = useMemo(
    () => allItems.reduce((n, vm) => (vm.status === 'running' ? n + 1 : n), 0),
    [allItems],
  );

  const isEmptyFleet = !isPending && !isError && total === 0;
  const isNoResults = !isPending && !isError && total > 0 && allItems.length === 0;

  const toggle = useCallback((id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const toggleAll = useCallback((nextSelected: boolean) => {
    setSelectedIds(nextSelected ? new Set(allItems.map((vm) => vm.id)) : new Set());
  }, [allItems]);

  const clearSelection = useCallback(() => setSelectedIds(new Set()), []);

  const handleRowClick = useCallback((vm: { id: string; name: string }) => {
    toast(t('table.openRow', { name: vm.name }), { description: `id=${vm.id}` });
  }, [t]);

  const confirmDelete = useCallback(() => {
    setDeleteOpen(false);
    toast(`${t('common.delete')} · ${selectedIds.size}`);
    clearSelection();
  }, [t, selectedIds.size, clearSelection]);

  const resetFilters = useCallback(() => setFilters(filterDefault), [filterDefault]);

  return (
    <>
      <PageTemplate
        data-od-id="vms-list"
        title={t('vms.list.title')}
        width="full"
        description={
          isPending ? (
            t('common.loading')
          ) : (
            <span>
              <MonoNum>{running}</MonoNum> <span className="text-muted-foreground">{t('vms.list.runningOf')}</span>{' '}
              <MonoNum>{total}</MonoNum> <span className="text-muted-foreground">{t('vms.list.total')}</span>
            </span>
          )
        }
        actions={
          <Button onClick={() => navigate({ to: '/vms/new' })}>
            <Add />
            {t('vms.list.create')}
          </Button>
        }
      >
        {isPending ? (
          <VmSkeleton />
        ) : isError ? (
          <VmErrorBanner error={error} onRetry={() => void refetch()} />
        ) : isEmptyFleet ? (
          <VmEmptyState />
        ) : (
          <div className="space-y-2">
            <DataTableToolbar
              columns={columns}
              filters={filters}
              onFiltersChange={setFilters}
              columnsState={colState}
              onColumnsChange={setColState}
            />
            <DataTable
              columns={columns}
              data={allItems}
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
            {isNoResults && (
              <VmNoResultsState onReset={resetFilters} />
            )}
          </div>
        )}
      </PageTemplate>

      <VmBulkToolbar
        count={selectedIds.size}
        onClear={clearSelection}
        onStart={() => {
          toast(`${t('common.start')} · ${selectedIds.size}`);
          clearSelection();
        }}
        onStop={() => {
          toast(`${t('common.stop')} · ${selectedIds.size}`);
          clearSelection();
        }}
        onReboot={() => {
          toast(`${t('common.reboot')} · ${selectedIds.size}`);
          clearSelection();
        }}
        onDelete={() => setDeleteOpen(true)}
      />

      <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <AlertDialogContent size="default">
          <AlertDialogHeader>
            <AlertDialogTitle>{t('vms.list.deleteTitle', { count: selectedIds.size })}</AlertDialogTitle>
            <AlertDialogDescription>
              {t('vms.list.deleteDescription')}
            </AlertDialogDescription>
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