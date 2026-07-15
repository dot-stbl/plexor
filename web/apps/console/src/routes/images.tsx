import { useMemo, useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useLocalStorage } from '@uidotdev/usehooks';
import { toast } from 'sonner';
import { Add, Delete, Image } from '@nine-thirty-five/material-symbols-react/rounded/700';
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
import { getImageColumns, listImages } from '@/features/images';

export const Route = createFileRoute('/images')({
  staticData: { crumb: 'Images' },
  component: ImagesPage,
});

/**
 * Image catalog — global (cross-section) resource: disk templates for VM
 * provisioning. Full-width list; filters + column-manager via `DataTableToolbar`;
 * local data → `applyFilters`; row selection via `useRowSelection`.
 */
function ImagesPage() {
  const { t } = useTranslation();
  const images = listImages();
  const columns = useMemo(() => getImageColumns(t), [t]);
  const [filters, setFilters] = useState<FilterValues>({});
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>('plexor-cols-images', {
    hidden: [],
    order: [],
  });

  const filtered = applyFilters(images, filters, columns);
  const sel = useRowSelection(filtered);

  const createCta = (
    <Button onClick={() => toast('Creating image — coming soon')}>
      <Add />
      {t('images.create')}
    </Button>
  );

  return (
    <>
      <PageTemplate
        data-od-id="images-list"
        width="full"
        title={t('images.title')}
        description={
          <>
            <MonoNum>{images.length}</MonoNum> <span className="text-muted-foreground">image(s)</span>
          </>
        }
        actions={images.length > 0 ? createCta : undefined}
      >
        {images.length > 0 ? (
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
              <p className="py-6 text-center text-sm text-muted-foreground">{t('common.nothingFound')}</p>
            )}
          </div>
        ) : (
          <EmptyState
            icon={Image}
            title={t('images.empty.title')}
            description={t('images.empty.description')}
            docsLabel={t('images.docs.label')}
            docs={[
              { href: 'https://plexor.dev/docs/images', label: t('images.docs.imagesBuilds') },
              { href: 'https://plexor.dev/docs/images/upload', label: t('images.docs.upload') },
            ]}
            action={createCta}
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
