import { useTranslation } from 'react-i18next';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { SizeUtils } from '@/shared/ui/primitives/size';
import { cn } from '@/shared/lib/utils';
import type { K8sCluster, K8sStatus } from '../model/k8s-types';
import { mapK8sStatusToVariant } from '../model/k8s-types';

/** Lifecycle order used everywhere the statuses line up (strip, select). */
const STRIP_STATUSES: readonly K8sStatus[] = ['running', 'provisioning', 'degraded', 'error'];

/** Static i18n keys — a record keeps the keys greppable instead of dynamic strings. */
const STATUS_LABEL_KEYS: Record<K8sStatus, string> = {
  running: 'k8s.status.running',
  provisioning: 'k8s.status.provisioning',
  degraded: 'k8s.status.degraded',
  error: 'k8s.status.error',
};

/** Statuses that participate in the strip, in display order. */
export function stripStatuses(): readonly K8sStatus[] {
  return STRIP_STATUSES;
}

/** Type guard — `FilterValues` is `Record<string, string>`, the strip needs the enum. */
export function isK8sStatus(value: string): value is K8sStatus {
  return STRIP_STATUSES.includes(value as K8sStatus);
}

/** i18n key for a status label (shared by the strip, the toolbar select, the status cell). */
export function k8sStatusLabelKey(status: K8sStatus): string {
  return STATUS_LABEL_KEYS[status];
}

/** Facet counts over a cluster set — every status key present, zeros included. */
export function countByStatusFacet(items: ReadonlyArray<K8sCluster>): Record<K8sStatus, number> {
  const counts: Record<K8sStatus, number> = {
    running: 0,
    provisioning: 0,
    degraded: 0,
    error: 0,
  };
  for (const cluster of items) {
    counts[cluster.status] += 1;
  }
  return counts;
}

/** Fleet-capacity totals over a cluster set (nodes = control-plane + workers). */
export interface K8sResourceTotals {
  vcpu: number;
  ramBytes: number;
  nodes: number;
}

export function sumResourceTotals(items: ReadonlyArray<K8sCluster>): K8sResourceTotals {
  let vcpu = 0;
  let ramBytes = 0;
  let nodes = 0;
  for (const cluster of items) {
    vcpu += cluster.vcpu;
    ramBytes += cluster.ramBytes;
    nodes += cluster.cpNodes + cluster.workerNodes;
  }
  return { vcpu, ramBytes, nodes };
}

interface K8sStatusStripProps {
  /** Facet counts — typically over the search-filtered set (status excluded). */
  counts: Record<K8sStatus, number>;
  /** Currently active status filter, `null` when the status filter is off. */
  activeStatus: K8sStatus | null;
  /** Toggle the status filter — the parent decides set-vs-clear. */
  onToggleStatus: (status: K8sStatus) => void;
  /** Capacity totals over the currently filtered set (chips included). */
  totals: K8sResourceTotals;
}

/**
 * Status summary strip above the cluster table: clickable status chips (facet
 * counts, toggle the table's status filter) + fleet-capacity totals over the
 * visible set. Composes StatusPill + MonoNum inside a feature-local layout —
 * deliberately not a global primitive (one page, one strip).
 */
export function K8sStatusStrip({ counts, activeStatus, onToggleStatus, totals }: K8sStatusStripProps) {
  const { t } = useTranslation();
  const ram = SizeUtils.format(totals.ramBytes);

  return (
    <div
      data-od-id="k8s-status-strip"
      className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-lg border border-border bg-card px-3 py-2"
    >
      <div className="flex flex-wrap items-center gap-1" role="group" aria-label={t('k8s.list.strip.filterByStatus')}>
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
              <StatusPill variant={mapK8sStatusToVariant(status)} size="sm">
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
        <span>{t('k8s.list.strip.allocated')}</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{totals.vcpu}</MonoNum>
          <span>vCPU</span>
        </span>
        <span aria-hidden>·</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{ram}</MonoNum>
          <span>RAM</span>
        </span>
        <span aria-hidden>·</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{totals.nodes}</MonoNum>
          <span>{t('k8s.list.strip.nodes')}</span>
        </span>
      </div>
    </div>
  );
}

/** Skeleton matching the strip layout — chips block left, totals block right. */
export function K8sStatusStripSkeleton() {
  return (
    <div
      data-od-id="k8s-status-strip-skeleton"
      className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-lg border border-border bg-card px-3 py-2"
    >
      <Skeleton className="h-6 w-72" />
      <Skeleton className="h-6 w-44" />
    </div>
  );
}
