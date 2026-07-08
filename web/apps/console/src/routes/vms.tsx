import { useCallback, useMemo, useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { Plus } from '@phosphor-icons/react';
import { useListVms } from '@/shared/api';
import { Button } from '@/shared/ui/primitives/button';
import { PageHeader } from '@/shared/ui/app-shell';
import { DataTable, emptyFilters, compactFilters, type FilterValues } from '@/shared/ui/data-table';
import { VmBulkToolbar, VmEmptyState, VmErrorBanner, VmNoResultsState, VmSkeleton, vmColumns } from '@/features/vms';
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

export const Route = createFileRoute('/vms')({
  component: VmsPage,
});

const FILTER_DEFAULT: FilterValues = emptyFilters(vmColumns);

function VmsPage() {
  const [filters, setFilters] = useState<FilterValues>(FILTER_DEFAULT);
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
    toast(`Открыть ${vm.name}`, { description: `id=${vm.id}` });
  }, []);

  const confirmDelete = useCallback(() => {
    setDeleteOpen(false);
    toast(`Удалено: ${selectedIds.size}`);
    clearSelection();
  }, [selectedIds.size, clearSelection]);

  const resetFilters = useCallback(() => setFilters(FILTER_DEFAULT), []);

  return (
    <main data-od-id="vms-list">
      <PageHeader
        title="Виртуальные машины"
        description={
          isPending ? (
            'Загрузка…'
          ) : (
            <span>
              <MonoNum>{running}</MonoNum> <span className="text-muted-foreground">running of</span>{' '}
              <MonoNum>{total}</MonoNum> <span className="text-muted-foreground">total</span>
            </span>
          )
        }
        actions={
          <Button render={<Link to="/vms/new" />}>
            <Plus />
            Создать ВМ
          </Button>
        }
      />

      <div className="mx-auto w-full max-w-6xl space-y-3 px-6 py-6 lg:px-8">
        {isPending ? (
          <VmSkeleton />
        ) : isError ? (
          <VmErrorBanner error={error} onRetry={() => void refetch()} />
        ) : isEmptyFleet ? (
          <VmEmptyState />
        ) : (
          <>
            <DataTable
              columns={vmColumns}
              data={allItems}
              totalCount={total}
              filters={filters}
              onFiltersChange={setFilters}
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
          </>
        )}
      </div>

      <VmBulkToolbar
        count={selectedIds.size}
        onClear={clearSelection}
        onStart={() => {
          toast(`Запустить: ${selectedIds.size}`);
          clearSelection();
        }}
        onStop={() => {
          toast(`Остановить: ${selectedIds.size}`);
          clearSelection();
        }}
        onReboot={() => {
          toast(`Перезагрузить: ${selectedIds.size}`);
          clearSelection();
        }}
        onDelete={() => setDeleteOpen(true)}
      />

      <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <AlertDialogContent size="default">
          <AlertDialogHeader>
            <AlertDialogTitle>Удалить {selectedIds.size} VM?</AlertDialogTitle>
            <AlertDialogDescription>
              Действие необратимо. Ресурсы ВМ и связанные диски будут освобождены.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Отмена</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={confirmDelete}>
              Удалить
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </main>
  );
}