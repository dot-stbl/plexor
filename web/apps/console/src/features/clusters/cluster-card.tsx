import { Link } from '@tanstack/react-router';
import { ArrowForward, ProgressActivity, Stacks, Warning } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { Badge } from '@/shared/ui/primitives/badge';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { countNodes, formatUptime } from './cluster-types';
import type { PlexorCluster } from './cluster-types';

const HEALTH_VARIANT: Record<'healthy' | 'degraded' | 'down', 'running' | 'pending' | 'err'> = {
  healthy: 'running',
  degraded: 'pending',
  down: 'err',
};

const HEALTH_LABEL: Record<'healthy' | 'degraded' | 'down', string> = {
  healthy: 'healthy',
  degraded: 'degraded',
  down: 'down',
};

interface ClusterCardProps {
  cluster: PlexorCluster;
}

const MAX_VISIBLE_PROVIDERS = 4;

/**
 * Top-level control-plane card. Self-hosted Plexor — name, host version,
 * health pill (healthy/degraded/down from node status mix), uptime,
 * ready/total nodes, install provider chips. Single drill-in to detail
 * for node + token management.
 */
export function ClusterCard({ cluster }: ClusterCardProps) {
  const counts = countNodes(cluster.nodes);
  const offlineRatio = counts.total > 0 ? counts.offline / counts.total : 0;
  const health: 'healthy' | 'degraded' | 'down' =
    counts.offline > 0 && offlineRatio >= 0.5
      ? 'down'
      : counts.offline > 0 || counts.draining > 0
        ? 'degraded'
        : 'healthy';

  const visibleProviders = cluster.installProviders.slice(0, MAX_VISIBLE_PROVIDERS);
  const overflowProviders = cluster.installProviders.length - visibleProviders.length;

  return (
    <div
      data-od-id="cluster-card"
      data-cluster-id={cluster.id}
      className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4 transition-all hover:border-foreground/20"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="truncate text-sm font-semibold tracking-tight">{cluster.name}</h3>
            <StatusPill variant={HEALTH_VARIANT[health]} size="sm">
              {HEALTH_LABEL[health]}
            </StatusPill>
            <Badge variant="outline">v{cluster.hostVersion}</Badge>
          </div>
          <p className="text-xs text-muted-foreground">
            <MonoNum>{counts.ready}</MonoNum>/<MonoNum>{counts.total}</MonoNum> нод(ов) ready ·{' '}
            uptime <MonoNum muted>{formatUptime(cluster.uptimeSeconds)}</MonoNum>
          </p>
        </div>
        <Button size="sm" render={<Link to="/clusters/$id" params={{ id: cluster.id }} />}>
          Управлять
          <ArrowForward className="size-4" />
        </Button>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <MetricCell
          icon={<Stacks className="size-3.5" />}
          label="Ноды"
          value={
            <span>
              <MonoNum>{counts.ready}</MonoNum>
              <span className="text-muted-foreground"> / </span>
              <MonoNum>{counts.total}</MonoNum>
            </span>
          }
        />
        <MetricCell
          icon={counts.pending > 0 ? <ProgressActivity className="size-3.5 animate-spin" /> : <Warning className="size-3.5" />}
          label="Pending"
          value={counts.pending}
          highlight={counts.pending > 0}
        />
      </div>

      <div className="flex flex-wrap items-center gap-1.5">
        {visibleProviders.map((p) => (
          <Badge key={p} variant="secondary">
            {p}
          </Badge>
        ))}
        {overflowProviders > 0 && <Badge variant="secondary">+{overflowProviders}</Badge>}
      </div>
    </div>
  );
}

function MetricCell({
  icon,
  label,
  value,
  highlight,
}: {
  icon: React.ReactNode;
  label: string;
  value: React.ReactNode;
  highlight?: boolean;
}) {
  return (
    <div
      className={`rounded-md border p-2 ${highlight ? 'border-warn/40 bg-warn/5' : 'border-border bg-background'}`}
    >
      <div className="flex items-center gap-1 text-[10px] font-medium text-muted-foreground uppercase tracking-[0.06em]">
        {icon}
        {label}
      </div>
      <div className="mt-0.5 text-sm font-medium">{value}</div>
    </div>
  );
}