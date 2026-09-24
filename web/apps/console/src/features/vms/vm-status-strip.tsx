import { useTranslation } from 'react-i18next';
import type { Vm, VmStatus } from '@/shared/api';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { cn } from '@/shared/lib/utils';
import { mapVmStatusToVariant } from './vm-status';

/** Lifecycle order used everywhere the statuses line up (strip, select). */
const STRIP_STATUSES: readonly VmStatus[] = ['running', 'stopped', 'provisioning', 'error', 'idle'];

/** Static i18n keys — a record keeps the keys greppable instead of dynamic strings. */
const STATUS_LABEL_KEYS: Record<VmStatus, string> = {
  running: 'vms.status.running',
  stopped: 'vms.status.stopped',
  provisioning: 'vms.status.provisioning',
  error: 'vms.status.error',
  idle: 'vms.status.idle',
};

/** Statuses that participate in the strip, in display order. */
export function stripStatuses(): readonly VmStatus[] {
  return STRIP_STATUSES;
}

/** Type guard — `FilterValues` is `Record<string, string>`, the strip needs the enum. */
export function isVmStatus(value: string): value is VmStatus {
  return STRIP_STATUSES.includes(value as VmStatus);
}

/** i18n key for a status label (shared by the strip, the toolbar select, the status cell). */
export function vmStatusLabelKey(status: VmStatus): string {
  return STATUS_LABEL_KEYS[status];
}

/** Facet counts over a VM set — every status key present, zeros included. */
export function countByStatusFacet(items: ReadonlyArray<Vm>): Record<VmStatus, number> {
  const counts: Record<VmStatus, number> = {
    running: 0,
    stopped: 0,
    provisioning: 0,
    error: 0,
    idle: 0,
  };
  for (const vm of items) {
    counts[vm.status] += 1;
  }
  return counts;
}

/** Allocated-resource totals over a VM set (vCPU / RAM GB / disk GB). */
export interface VmResourceTotals {
  vcpu: number;
  ramGb: number;
  diskGb: number;
}

export function sumResourceTotals(items: ReadonlyArray<Vm>): VmResourceTotals {
  let vcpu = 0;
  let ramGb = 0;
  let diskGb = 0;
  for (const vm of items) {
    vcpu += vm.vcpu;
    ramGb += vm.ramGb;
    diskGb += vm.diskGb;
  }
  return { vcpu, ramGb, diskGb };
}

/** Disk sum switches to TB past 1 TiB so the strip stays two tokens wide. */
function formatDisk(gb: number): { value: string; unit: string } {
  if (gb >= 1024) {
    return { value: (gb / 1024).toFixed(1), unit: 'TB' };
  }
  return { value: String(gb), unit: 'GB' };
}

interface VmStatusStripProps {
  /** Facet counts — typically over the search-filtered set (status excluded). */
  counts: Record<VmStatus, number>;
  /** Currently active status filter, `null` when the status filter is off. */
  activeStatus: VmStatus | null;
  /** Toggle the status filter — the parent decides set-vs-clear. */
  onToggleStatus: (status: VmStatus) => void;
  /** Resource totals over the currently filtered set (chips included). */
  totals: VmResourceTotals;
}

/**
 * Status summary strip above the VM table: clickable status chips (facet
 * counts, toggle the table's status filter) + allocated-resource totals over
 * the visible set. Composes StatusPill + MonoNum inside a feature-local
 * layout — deliberately not a global primitive (one page, one strip).
 */
export function VmStatusStrip({ counts, activeStatus, onToggleStatus, totals }: VmStatusStripProps) {
  const { t } = useTranslation();
  const disk = formatDisk(totals.diskGb);

  return (
    <div
      data-od-id="vms-status-strip"
      className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-lg border border-border bg-card px-3 py-2"
    >
      <div className="flex flex-wrap items-center gap-1" role="group" aria-label={t('vms.list.strip.filterByStatus')}>
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
              <StatusPill variant={mapVmStatusToVariant(status)} size="sm">
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
        <span>{t('vms.list.strip.allocated')}</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{totals.vcpu}</MonoNum>
          <span>vCPU</span>
        </span>
        <span aria-hidden>·</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{totals.ramGb}</MonoNum>
          <span>GB RAM</span>
        </span>
        <span aria-hidden>·</span>
        <span className="inline-flex items-center gap-1">
          <MonoNum muted>{disk.value}</MonoNum>
          <span>
            {disk.unit} {t('vms.list.strip.disk')}
          </span>
        </span>
      </div>
    </div>
  );
}

/** Skeleton matching the strip layout — chips block left, totals block right. */
export function VmStatusStripSkeleton() {
  return (
    <div
      data-od-id="vms-status-strip-skeleton"
      className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-lg border border-border bg-card px-3 py-2"
    >
      <Skeleton className="h-6 w-72" />
      <Skeleton className="h-6 w-44" />
    </div>
  );
}
