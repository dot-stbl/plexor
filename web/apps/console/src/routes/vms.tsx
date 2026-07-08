import { useCallback, useMemo, useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import { Plus } from '@phosphor-icons/react';
import { useListVms } from '@/shared/api';
import { Button } from '@/shared/ui/primitives/button';
import { PageHeader } from '@/shared/ui/app-shell';
import {
  CreateVmDialog,
  VmBulkToolbar,
  VmEmptyState,
  VmErrorBanner,
  VmFiltersBar,
  VmNoResultsState,
  VmSkeleton,
  VmTable,
  filterVms,
  summarizeStatus,
  uniqueZones,
  VM_FILTERS_DEFAULT,
} from '@/features/vms';
import type { VmFilters } from '@/features/vms';
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

function VmsPage() {
  const { data, isPending, isError, error, refetch } = useListVms();
  const [filters, setFilters] = useState<VmFilters>(VM_FILTERS_DEFAULT);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const allItems = data?.items ?? [];
  const filteredItems = useMemo(() => filterVms(allItems, filters), [allItems, filters]);
  const zones = useMemo(() => uniqueZones(allItems), [allItems]);
  const { total, running } = useMemo(() => summarizeStatus(allItems), [allItems]);

  const noResults = allItems.length > 0 && filteredItems.length === 0;

  const toggle = useCallback((id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const toggleAll = useCallback(
    (nextSelected: boolean) => {
      setSelectedIds((prev) => {
        if (nextSelected) return new Set(filteredItems.map((vm) => vm.id));
        if (prev.size === filteredItems.length) return new Set();
        // Mixed state — selecting all fills with filtered ids.
        return new Set(filteredItems.map((vm) => vm.id));
      });
    },
    [filteredItems],
  );

  const clearSelection = useCallback(() => setSelectedIds(new Set()), []);

  const handleRowClick = useCallback((vm: { id: string; name: string }) => {
    // VM detail screen is a separate plan. Surface as a toast so the click is
    // visibly wired without faking the route.
    toast(`Открыть ${vm.name}`, { description: `id=${vm.id}` });
  }, []);

  const confirmDelete = useCallback(() => {
    setDeleteOpen(false);
    toast(`Удалено: ${selectedIds.size}`);
    clearSelection();
  }, [selectedIds.size, clearSelection]);

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
          <Button onClick={() => setCreateOpen(true)}>
            <Plus />
            Создать ВМ
          </Button>
        }
      />

      <div className="mx-auto w-full max-w-6xl px-6 py-6 lg:px-8">
        <VmFiltersBar
          value={filters}
          onChange={setFilters}
          zones={zones}
        />

        {isPending ? (
          <VmSkeleton />
        ) : isError ? (
          <VmErrorBanner error={error} onRetry={() => void refetch()} />
        ) : allItems.length === 0 ? (
          <VmEmptyState onCreate={() => setCreateOpen(true)} />
        ) : noResults ? (
          <VmNoResultsState />
        ) : (
          <VmTable
            items={filteredItems}
            selectedIds={selectedIds}
            onToggle={toggle}
            onToggleAll={toggleAll}
            onRowClick={handleRowClick}
          />
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

      <CreateVmDialog open={createOpen} onOpenChange={setCreateOpen} />
    </main>
  );
}