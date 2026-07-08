import { Link } from '@tanstack/react-router';
import { ArrowRight, Plus } from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Progress } from '@/shared/ui/primitives/progress';
import { clusterUtilizationPct } from './cluster-types';
import type { Cluster } from './cluster-types';

const STATUS_VARIANT: Record<Cluster['status'], 'running' | 'pending' | 'err'> = {
  healthy: 'running',
  degraded: 'pending',
  offline: 'err',
};

const STATUS_LABEL: Record<Cluster['status'], string> = {
  healthy: 'healthy',
  degraded: 'degraded',
  offline: 'offline',
};

interface ClusterCardProps {
  cluster: Cluster;
}

/**
 * Compact cluster summary — name, zone, status, node/VM count, capacity
 * bars (CPU/RAM), and two primary actions (drill into detail, create VM).
 */
export function ClusterCard({ cluster }: ClusterCardProps) {
  const util = clusterUtilizationPct(cluster);

  return (
    <div
      data-od-id="cluster-card"
      data-cluster-id={cluster.id}
      className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4 transition-all hover:border-foreground/20"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-0.5">
          <div className="flex items-center gap-2">
            <h3 className="truncate text-sm font-semibold tracking-tight">{cluster.name}</h3>
            <StatusPill variant={STATUS_VARIANT[cluster.status]} size="sm">
              {STATUS_LABEL[cluster.status]}
            </StatusPill>
          </div>
          <p className="text-xs text-muted-foreground">
            <MonoNum>{cluster.nodeCount}</MonoNum> {pluralize(cluster.nodeCount, 'нода', 'ноды', 'нод')} ·{' '}
            <MonoNum>{cluster.vmCount}</MonoNum> {pluralize(cluster.vmCount, 'VM', 'VM', 'VM')}
          </p>
        </div>
        <Button size="sm" render={<Link to="/clusters/$id/vms/new" params={{ id: cluster.id }} />}>
          <Plus />
          Создать VM
        </Button>
      </div>

      <div className="space-y-1.5">
        <CapacityBar label="CPU" pct={util.cpu} used={cluster.usedCpu} total={cluster.totalCpu} unit="vCPU" />
        <CapacityBar label="RAM" pct={util.ram} used={cluster.usedRamGb} total={cluster.totalRamGb} unit="GB" />
      </div>

      <div className="flex items-center justify-between text-xs text-muted-foreground">
        <span>
          Зона: <MonoNum muted>{cluster.zone}</MonoNum>
        </span>
        <Link
          to="/clusters/$id"
          params={{ id: cluster.id }}
          className="inline-flex items-center gap-1 font-medium text-foreground transition-colors hover:underline"
        >
          Подробнее <ArrowRight className="size-3" />
        </Link>
      </div>
    </div>
  );
}

function CapacityBar({
  label,
  pct,
  used,
  total,
  unit,
}: {
  label: string;
  pct: number;
  used: number;
  total: number;
  unit: string;
}) {
  return (
    <div className="space-y-0.5">
      <div className="flex items-center justify-between text-[11px] text-muted-foreground">
        <span>{label}</span>
        <span>
          <MonoNum>{used}</MonoNum>/<MonoNum>{total}</MonoNum> {unit} · {pct}%
        </span>
      </div>
      <Progress value={pct} />
    </div>
  );
}

function pluralize(n: number, one: string, few: string, many: string): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return one;
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return few;
  return many;
}