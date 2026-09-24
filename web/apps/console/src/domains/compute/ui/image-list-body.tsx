import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocalStorage } from '@uidotdev/usehooks';
import { Add, Delete, Image, Search } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { toast } from 'sonner';
import type { OsImage } from '../model/image-types';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { BulkActionToolbar } from '@/shared/ui/primitives/bulk-action-toolbar';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import {
  DataTable,
  DataTableToolbar,
  applyFilters,
  useRowSelection,
  type DataTableColumnsState,
  type FilterValues,
} from '@/shared/ui/data-table';
import { getImageColumns } from './image-columns';
import {
  ImageStatusStrip,
  countImageByStatusFacet,
  imageStatusLabelKey,
  isImageStatus,
  sumImageTotals,
} from './image-status-strip';

interface ImageListBodyProps {
  /** Full image catalog for the page — filtering happens client-side. */
  items: ReadonlyArray<OsImage>;
  /** Create-image CTA (the wizard is future work — the route toasts for now). */
  onCreate: () => void;
}

/**
 * The /images list page body: status summary strip, toolbar, table, bulk
 * delete. Owns filter/selection state so the route stays a thin data shell
 * (and stories render this component with fixtures).
 *
 * Filtering is client-side (`applyFilters`): the status chips, the toolbar
 * search and the arch/visibility selects all compose over the same `items`.
 */
export function ImageListBody({ items, onCreate }: ImageListBodyProps) {
  const { t } = useTranslation();
  const columns = useMemo(() => getImageColumns(t), [t]);
  const [filters, setFilters] = useState<FilterValues>({});
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>('plexor-cols-images', {
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

  const counts = useMemo(() => countImageByStatusFacet(facetItems), [facetItems]);
  const totals = useMemo(() => sumImageTotals(filteredItems), [filteredItems]);
  const ready = counts.ready;

  const statusFilter = filters.status ?? '';
  const activeStatus = isImageStatus(statusFilter) ? statusFilter : null;

  const sel = useRowSelection(filteredItems);

  const isEmptyCatalog = allItems.length === 0;
  const isNoResults = allItems.length > 0 && filteredItems.length === 0;

  const resetFilters = useCallback(() => setFilters({}), []);

  const toggleStatus = useCallback((status: string) => {
    setFilters((prev) => ({ ...prev, status: prev.status === status ? '' : status }));
  }, []);

  const createCta = (
    <Button onClick={onCreate}>
      <Add />
      {t('images.create')}
    </Button>
  );

  // No-results copy acknowledges the active chip when it is the only filter.
  const searchActive = Object.values(searchFilters).some((value) => value !== '');
  const noResultsTitle =
    activeStatus !== null && !searchActive
      ? t('images.list.empty.noResultsStatusTitle', { status: t(imageStatusLabelKey(activeStatus)) })
      : undefined;

  return (
    <>
      <PageTemplate
        data-od-id="images-list"
        width="wide"
        title={t('images.title')}
        description={
          <span>
            <MonoNum>{ready}</MonoNum> <span className="text-muted-foreground">{t('images.list.readyOf')}</span>{' '}
            <MonoNum>{allItems.length}</MonoNum> <span className="text-muted-foreground">{t('images.list.total')}</span>
          </span>
        }
        actions={allItems.length > 0 ? createCta : undefined}
      >
        {isEmptyCatalog ? (
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
        ) : (
          <div className="space-y-2">
            <ImageStatusStrip
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
            {isNoResults && <ImageNoResults title={noResultsTitle} onReset={resetFilters} />}
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

/** Empty state — filters returned nothing (images exist but none match). */
function ImageNoResults({ title, onReset }: { title?: string; onReset: () => void }) {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="images-no-results"
      icon={Search}
      title={title ?? t('images.list.empty.noResults')}
      description={t('images.list.empty.noResultsDescription')}
      action={
        <Button variant="outline" size="sm" onClick={onReset}>
          {t('images.list.empty.reset')}
        </Button>
      }
    />
  );
}
