import { useTranslation } from 'react-i18next';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { SizeUtils } from '@/shared/ui/primitives/size';
import { cn } from '@/shared/lib/utils';
import type { LxcContainer, LxcStatus } from '../model/lxc-types';
import { mapLxcStatusToVariant } from '../model/lxc-types';

/** Lifecycle order used everywhere the statuses line up (strip, select). */
const STRIP_STATUSES: readonly LxcStatus[] = ['running', 'stopped', 'paused', 'error'];

/** Static i18n keys — a record keeps the keys greppable instead of dynamic strings. */
const STATUS_LABEL_KEYS: Record<LxcStatus, string> = {
  running: 'lxc.status.running',
  stopped: 'lxc.status.stopped',
  paused: 'lxc.status.paused',
  error: 'lxc.status.error',
};

/** Statuses that participate in the strip, in display order. */
export function stripStatuses(): readonly LxcStatus[] {
  return STRIP_STATUSES;
}

/** Type guard — `FilterValues` is `Record<string, string>`, the strip needs the enum. */
export function isLxcStatus(value: string): value is LxcStatus {
  return STRIP_STATUSES.includes(value as LxcStatus);
}

/** i18n key for a status label (shared by the strip, the toolbar select, the status cell). */
export function lxcStatusLabelKey(status: LxcStatus): string {
  return STATUS_LABEL_KEYS[status];
}

/** Facet counts over a container set — every status key present, zeros included. */
export function countLxcByStatusFacet(items: ReadonlyArray<LxcContainer>): Record<LxcStatus, number> {
  const counts: Record<LxcStatus, number> = {
    running: 0,
    stopped: 0,
    paused: 0,
    error: 0,
  };
  for (const container of items) {
    counts[container.status] += 1;
  }
  return counts;
}

/** Allocated-resource totals over a container set (cores / RAM / rootfs bytes). */
export interface LxcResourceTotals {
  cores: number;
  ramBytes: number;
  rootfsBytes: number;
}

export function sumLxcResourceTotals(items: ReadonlyArray<LxcContainer>): LxcResourceTotals {
  let cores = 0;
  let ramBytes = 0;
  let rootfsBytes = 0;
  for (const container of items) {
    cores += container.cores;
    ramBytes += container.ramBytes;
    rootfsBytes += container.rootfsBytes;
  }
  return { cores, ramBytes, rootfsBytes };
}

interface LxcStatusStripProps {
  /** Facet counts — typically over the search-filtered set (status excluded). */
  counts: Record<LxcStatus, number>;
  /** Currently active status filter, `null` when the status filter is off. */
  activeStatus: LxcStatus | null;
  /** Toggle the status filter — the parent decides set-vs-clear. */
  onToggleStatus: (status: LxcStatus) => void;
  /** Resource totals over the currently filtered set (chips included). */
  totals: LxcResourceTotals;
}

/**
 * Status summary strip above the container table: clickable status chips
 * (facet counts, toggle the table's status filter) + allocated-resource
 * totals over the visible set. Composes StatusPill + MonoNum inside a
 * feature-local layout — deliberately not a global primitive (one page,
 * one strip).
 */
export function LxcStatusStrip({ counts, activeStatus, onToggleStatus, totals }: LxcStatusStripProps) {
  const { t } = useTranslation();
  const ram = SizeUtils.format(totals.ramBytes);
  const rootfs = SizeUtils.format(totals.rootfsBytes);

  return (
    <div
      data-od-id="lxc-status-strip"
      className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-lg border border-border bg-card px-3 py-2"
    >
      <div className="flex flex-wrap items-center gap-1" role="group" aria-label={t('lxc.list.strip.filterByStatus')}>
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
              <StatusPill variant={mapLxcStatusToVariant(status)} size="sm">
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
        <span>{t('lxc.list.strip.allocated')}</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{totals.cores}</MonoNum>
          <span>vCPU</span>
        </span>
        <span aria-hidden>·</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{ram}</MonoNum>
          <span>RAM</span>
        </span>
        <span aria-hidden>·</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{rootfs}</MonoNum>
          <span>{t('lxc.list.strip.rootfs')}</span>
        </span>
      </div>
    </div>
  );
}

/** Skeleton matching the strip layout — chips block left, totals block right. */
export function LxcStatusStripSkeleton() {
  return (
    <div
      data-od-id="lxc-status-strip-skeleton"
      className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-lg border border-border bg-card px-3 py-2"
    >
      <Skeleton className="h-6 w-72" />
      <Skeleton className="h-6 w-44" />
    </div>
  );
}
