import { Cube, Cpu, Memory, HardDrives, CircleNotch, XCircle } from '@phosphor-icons/react';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import type { PlexorNode } from './cluster-types';

const STATUS_VARIANT: Record<PlexorNode['status'], 'running' | 'pending' | 'idle' | 'err'> = {
  ready: 'running',
  pending: 'pending',
  draining: 'idle',
  offline: 'err',
};

const STATUS_LABEL: Record<PlexorNode['status'], string> = {
  ready: 'ready',
  pending: 'pending (waiting for first heartbeat)',
  draining: 'draining',
  offline: 'offline',
};

const ROLE_LABEL: Record<PlexorNode['role'], string> = {
  control: 'control-plane',
  compute: 'compute',
};

const STATUS_ICON: Record<PlexorNode['status'], React.ReactNode> = {
  ready: null,
  pending: <CircleNotch className="size-3 animate-spin" />,
  draining: <CircleNotch className="size-3 animate-spin" />,
  offline: <XCircle className="size-3" />,
};

interface NodeRowProps {
  node: PlexorNode;
}

export function NodeRow({ node }: NodeRowProps) {
  return (
    <div className="flex items-center justify-between gap-3 p-3">
      <div className="flex min-w-0 items-center gap-3">
        <Cube className="size-4 shrink-0 text-muted-foreground" />
        <div className="min-w-0 space-y-0.5">
          <MonoNum className="text-sm">{node.hostname}</MonoNum>
          <p className="flex items-center gap-1.5 text-[10px] text-muted-foreground uppercase tracking-[0.06em]">
            <span>{ROLE_LABEL[node.role]}</span>
            <span aria-hidden>·</span>
            <span>v{node.isoVersion}</span>
            <span aria-hidden>·</span>
            <span>joined {new Date(node.joinedAt).toLocaleDateString('ru-RU')}</span>
          </p>
        </div>
      </div>
      <div className="flex shrink-0 items-center gap-3 text-xs">
        <span className="hidden items-center gap-2 text-muted-foreground md:flex">
          <span className="inline-flex items-center gap-0.5">
            <Cpu className="size-3" />
            <MonoNum>{node.spec.vcpu}</MonoNum>
          </span>
          <span className="inline-flex items-center gap-0.5">
            <Memory className="size-3" />
            <MonoNum>{node.spec.ramGb}</MonoNum>
            <span>GB</span>
          </span>
          <span className="inline-flex items-center gap-0.5">
            <HardDrives className="size-3" />
            <MonoNum>{node.spec.diskGb}</MonoNum>
            <span>GB</span>
          </span>
          <span className="inline-flex items-center gap-0.5">
            <MonoNum>{node.vmCount}</MonoNum> VM
          </span>
        </span>
        <StatusPill variant={STATUS_VARIANT[node.status]} size="sm">
          <span className="inline-flex items-center gap-1">
            {STATUS_ICON[node.status]}
            {STATUS_LABEL[node.status]}
          </span>
        </StatusPill>
      </div>
    </div>
  );
}