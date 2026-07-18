import { useMemo, useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { useLocalStorage } from '@uidotdev/usehooks';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { Add, DeployedCode, Delete, PlayArrow, Stop } from '@nine-thirty-five/material-symbols-react/rounded/700';
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
import { getLxcColumns, listLxc } from '@/features/lxc';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/lxc/')({
  component: LxcPage,
  ...routeHead('LXC'),
});

/**
 * LXC container catalog — full-width list. Filters + column-manager via the
 * shared `DataTableToolbar`; local data → client `applyFilters`; row selection
 * via `useRowSelection` → `BulkActionToolbar`. Creation from the header CTA.
 */
function LxcPage() {
  const { t } = useTranslation();
  const rows = listLxc();
  const columns = useMemo(() => getLxcColumns(t), [t]);
  const [filters, setFilters] = useState<FilterValues>({});
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>('plexor-cols-lxc', {
    hidden: [],
    order: [],
  });

  const filtered = applyFilters(rows, filters, columns);
  const sel = useRowSelection(filtered);

  const bulk = (label: string) => {
    toast(`${label} · ${sel.selectedIds.size}`);
    sel.clear();
  };

  return (
    <>
      <PageTemplate
        data-od-id="lxc-list"
        width="full"
        title={t('lxc.list.title')}
        description={
          <>
            <MonoNum>{rows.length}</MonoNum>{' '}
            <span className="text-muted-foreground">container(s)</span>
          </>
        }
        actions={
          rows.length > 0 ? (
            <Button nativeButton={false} render={<Link to="/lxc/new" />}>
              <Add />
              {t('lxc.list.create')}
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
                {t('lxc.list.empty.noResults')}
              </p>
            )}
          </div>
        ) : (
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
              <Button nativeButton={false} render={<Link to="/lxc/new" />}>
                <Add className="size-3.5" />
                {t('lxc.list.create')}
              </Button>
            }
          />
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
