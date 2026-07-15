import { useMemo, useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { useLocalStorage } from '@uidotdev/usehooks';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { Add, Delete, Hexagon } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { BulkActionToolbar } from '@/shared/ui/primitives/bulk-action-toolbar';
import { PageTemplate } from '@/shared/ui/app-shell';
import {
  DataTable,
  DataTableToolbar,
  applyFilters,
  useRowSelection,
  type DataTableColumnsState,
  type FilterValues,
} from '@/shared/ui/data-table';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { getK8sColumns, listK8s } from '@/features/k8s';

export const Route = createFileRoute('/k8s/')({
  component: K8sPage,
});

/**
 * Managed Kubernetes (K3s) clusters — full-width list. Filters + column-manager
 * via `DataTableToolbar`; local data → `applyFilters`; row selection via
 * `useRowSelection` → `BulkActionToolbar`.
 */
function K8sPage() {
  const { t } = useTranslation();
  const rows = listK8s();
  const columns = useMemo(() => getK8sColumns(t), [t]);
  const [filters, setFilters] = useState<FilterValues>({});
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>('plexor-cols-k8s', {
    hidden: [],
    order: [],
  });

  const filtered = applyFilters(rows, filters, columns);
  const sel = useRowSelection(filtered);

  return (
    <>
      <PageTemplate
        data-od-id="k8s-list"
        width="full"
        title={t('k8s.list.title')}
        description={
          <>
            <MonoNum>{rows.length}</MonoNum> <span className="text-muted-foreground">cluster(s)</span>
          </>
        }
        actions={
          rows.length > 0 ? (
            <Button nativeButton={false} render={<Link to="/k8s/new" />}>
              <Add />
              {t('k8s.list.create')}
            </Button>
          ) : undefined
        }
      >
        {rows.length > 0 ? (
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
              data={filtered}
              density="compact"
              selection={sel.selection}
              hiddenColumns={new Set(colState.hidden)}
              columnOrder={colState.order}
            />
            {filtered.length === 0 && (
              <p className="py-6 text-center text-sm text-muted-foreground">
                {t('k8s.list.empty.noResults')}
              </p>
            )}
          </div>
        ) : (
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
              <Button nativeButton={false} render={<Link to="/k8s/new" />}>
                <Add className="size-3.5" />
                {t('k8s.list.create')}
              </Button>
            }
          />
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
              toast(`${t('common.delete')} · ${sel.selectedIds.size}`);
              sel.clear();
            },
          },
        ]}
      />
    </>
  );
}
