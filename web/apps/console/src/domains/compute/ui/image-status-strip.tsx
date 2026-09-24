import { useTranslation } from 'react-i18next';
import type { ImageStatus, OsImage } from '../model/image-types';
import { mapImageStatusToVariant } from '../model/image-types';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Size } from '@/shared/ui/primitives/size';
import { cn } from '@/shared/lib/utils';

/** Lifecycle order used everywhere the statuses line up (strip, select). */
const STRIP_STATUSES: readonly ImageStatus[] = ['ready', 'creating', 'error'];

/** Static i18n keys — a record keeps the keys greppable instead of dynamic strings. */
const STATUS_LABEL_KEYS: Record<ImageStatus, string> = {
  ready: 'images.status.ready',
  creating: 'images.status.creating',
  error: 'images.status.error',
};

/** Statuses that participate in the strip, in display order. */
export function stripStatuses(): readonly ImageStatus[] {
  return STRIP_STATUSES;
}

/** Type guard — `FilterValues` is `Record<string, string>`, the strip needs the enum. */
export function isImageStatus(value: string): value is ImageStatus {
  return STRIP_STATUSES.includes(value as ImageStatus);
}

/** i18n key for a status label (shared by the strip, the toolbar select, the status cell). */
export function imageStatusLabelKey(status: ImageStatus): string {
  return STATUS_LABEL_KEYS[status];
}

/** Facet counts over an image set — every status key present, zeros included. */
export function countImageByStatusFacet(items: ReadonlyArray<OsImage>): Record<ImageStatus, number> {
  const counts: Record<ImageStatus, number> = {
    ready: 0,
    creating: 0,
    error: 0,
  };
  for (const image of items) {
    counts[image.status] += 1;
  }
  return counts;
}

/** Catalog totals over an image set — image count + summed size in bytes. */
export interface ImageTotals {
  count: number;
  sizeBytes: number;
}

export function sumImageTotals(items: ReadonlyArray<OsImage>): ImageTotals {
  let sizeBytes = 0;
  for (const image of items) {
    sizeBytes += image.sizeBytes;
  }
  return { count: items.length, sizeBytes };
}

interface ImageStatusStripProps {
  /** Facet counts — typically over the search-filtered set (status excluded). */
  counts: Record<ImageStatus, number>;
  /** Currently active status filter, `null` when the status filter is off. */
  activeStatus: ImageStatus | null;
  /** Toggle the status filter — the parent decides set-vs-clear. */
  onToggleStatus: (status: ImageStatus) => void;
  /** Catalog totals over the currently filtered set (chips included). */
  totals: ImageTotals;
}

/**
 * Status summary strip above the images table: clickable status chips (facet
 * counts, toggle the table's status filter) + catalog totals (image count +
 * total size) over the visible set. Composes StatusPill + MonoNum + Size
 * inside a feature-local layout — deliberately not a global primitive.
 */
export function ImageStatusStrip({ counts, activeStatus, onToggleStatus, totals }: ImageStatusStripProps) {
  const { t } = useTranslation();

  return (
    <div
      data-od-id="images-status-strip"
      className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-lg border border-border bg-card px-3 py-2"
    >
      <div
        className="flex flex-wrap items-center gap-1"
        role="group"
        aria-label={t('images.list.strip.filterByStatus')}
      >
        {STRIP_STATUSES.map((status) => {
          const active = activeStatus === status;
          return (
            <button
              key={status}
              type="button"
              aria-pressed={active}
              onClick={() => onToggleStatus(status)}
              className={cn(
                'inline-flex items-center gap-1.5 rounded-full border border-transparent py-0.5 pr-2 pl-1 transition-colors',
                'hover:bg-muted/60 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden',
                active && 'border-border bg-muted',
              )}
            >
              <StatusPill variant={mapImageStatusToVariant(status)} size="sm">
                {t(STATUS_LABEL_KEYS[status])}
              </StatusPill>
              <MonoNum muted className="text-xs">
                {counts[status]}
              </MonoNum>
            </button>
          );
        })}
      </div>

      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{totals.count}</MonoNum>
          <span>{t('images.list.strip.images')}</span>
        </span>
        <span aria-hidden>·</span>
        <span className="inline-flex items-center gap-1">
          <Size bytes={totals.sizeBytes} muted />
          <span>{t('images.list.strip.totalSize')}</span>
        </span>
      </div>
    </div>
  );
}
