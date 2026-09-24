import { useTranslation } from 'react-i18next';
import type { DbCluster, DbStatus } from '../model/database-types';
import { mapDbStatusToVariant } from '../model/database-types';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { cn } from '@/shared/lib/utils';

/** Lifecycle order used everywhere the statuses line up (strip, cells). */
const STRIP_STATUSES: readonly DbStatus[] = ['running', 'deploying', 'degraded', 'stopped', 'error'];

/** Static i18n keys — a record keeps the keys greppable instead of dynamic strings. */
const STATUS_LABEL_KEYS: Record<DbStatus, string> = {
  running: 'managed.status.running',
  deploying: 'managed.status.deploying',
  degraded: 'managed.status.degraded',
  stopped: 'managed.status.stopped',
  error: 'managed.status.error',
};

/** Statuses that participate in the strip, in display order. */
export function stripStatuses(): readonly DbStatus[] {
  return STRIP_STATUSES;
}

/** Type guard — the page keeps the filter as a plain string, the strip needs the enum. */
export function isDbStatus(value: string): value is DbStatus {
  return STRIP_STATUSES.includes(value as DbStatus);
}

/** i18n key for a status label (shared by the strip and the status cell). */
export function dbStatusLabelKey(status: DbStatus): string {
  return STATUS_LABEL_KEYS[status];
}

/** Facet counts over a cluster set — every status key present, zeros included. */
export function countDbByStatusFacet(items: ReadonlyArray<DbCluster>): Record<DbStatus, number> {
  const counts: Record<DbStatus, number> = {
    running: 0,
    deploying: 0,
    degraded: 0,
    stopped: 0,
    error: 0,
  };
  for (const cluster of items) {
    counts[cluster.status] += 1;
  }
  return counts;
}

/** Fleet totals over a cluster set — cluster count, storage GB, bindings. */
export interface DbTotals {
  clusters: number;
  storageGb: number;
  bindings: number;
}

export function sumDbTotals(items: ReadonlyArray<DbCluster>): DbTotals {
  let storageGb = 0;
  let bindings = 0;
  for (const cluster of items) {
    storageGb += cluster.storageGb;
    bindings += cluster.bindings;
  }
  return { clusters: items.length, storageGb, bindings };
}

interface DbStatusStripProps {
  /** Facet counts — over the engine's full cluster set (no other filters on this page). */
  counts: Record<DbStatus, number>;
  /** Currently active status filter, `null` when the filter is off. */
  activeStatus: DbStatus | null;
  /** Toggle the status filter — the parent decides set-vs-clear. */
  onToggleStatus: (status: DbStatus) => void;
  /** Fleet totals over the currently filtered set (chips included). */
  totals: DbTotals;
}

/**
 * Status summary strip above an engine's cluster table: clickable status
 * chips (facet counts, toggle the table's status filter) + fleet totals
 * (clusters, storage, bindings) over the visible set. Lives in the shared
 * `ManagedServicePage`, so every engine section gets it for free.
 */
export function DbStatusStrip({ counts, activeStatus, onToggleStatus, totals }: DbStatusStripProps) {
  const { t } = useTranslation();

  return (
    <div
      data-od-id="db-status-strip"
      className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-lg border border-border bg-card px-3 py-2"
    >
      <div
        className="flex flex-wrap items-center gap-1"
        role="group"
        aria-label={t('managed.list.strip.filterByStatus')}
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
              <StatusPill variant={mapDbStatusToVariant(status)} size="sm">
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
          <MonoNum muted>{totals.clusters}</MonoNum>
          <span>{t('managed.list.strip.clusters')}</span>
        </span>
        <span aria-hidden>·</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{totals.storageGb}</MonoNum>
          <span>GB {t('managed.list.strip.storage')}</span>
        </span>
        <span aria-hidden>·</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{totals.bindings}</MonoNum>
          <span>{t('managed.list.strip.bindings')}</span>
        </span>
      </div>
    </div>
  );
}

/** Skeleton matching the strip layout — chips block left, totals block right. */
export function DbStatusStripSkeleton() {
  return (
    <div
      data-od-id="db-status-strip-skeleton"
      className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-lg border border-border bg-card px-3 py-2"
    >
      <Skeleton className="h-6 w-72" />
      <Skeleton className="h-6 w-52" />
    </div>
  );
}
