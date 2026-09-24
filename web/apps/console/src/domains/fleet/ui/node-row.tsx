import { useTranslation } from 'react-i18next';
import {
  Cancel,
  DeployedCode,
  DeveloperBoard,
  HardDisk,
  Memory,
  ProgressActivity,
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Size, SizeUtils } from '@/shared/ui/primitives/size';
import type { PlexorNode } from '../model/cluster-types';
import { mapNodeStatusToVariant, nodeRoleLabelKey, nodeStatusLabelKey } from '../model/node-status';

const STATUS_ICON: Record<PlexorNode['status'], React.ReactNode> = {
  ready: null,
  pending: <ProgressActivity className="size-3 animate-spin" />,
  draining: <ProgressActivity className="size-3 animate-spin" />,
  offline: <Cancel className="size-3" />,
};

interface NodeRowProps {
  node: PlexorNode;
}

/** One joined Plexor.NodeAgent: identity + role, capacity, live status. */
export function NodeRow({ node }: NodeRowProps) {
  const { t } = useTranslation();

  return (
    <div className="flex items-center justify-between gap-3 p-3">
      <div className="flex min-w-0 items-center gap-3">
        <DeployedCode className="size-4 shrink-0 text-muted-foreground" />
        <div className="min-w-0 space-y-0.5">
          <MonoNum className="text-sm">{node.hostname}</MonoNum>
          <p className="flex items-center gap-1.5 text-[10px] text-muted-foreground uppercase tracking-[0.06em]">
            <span>{t(nodeRoleLabelKey(node.role))}</span>
            <span className="inline-block h-2.5 w-px bg-border" aria-hidden />
            <span>{node.isoVersion}</span>
            <span className="inline-block h-2.5 w-px bg-border" aria-hidden />
            <span>
              {t('clusters.node.joined')} {new Date(node.joinedAt).toLocaleDateString()}
            </span>
          </p>
        </div>
      </div>
      <div className="flex shrink-0 items-center gap-3 text-xs">
        <span className="hidden items-center gap-2 text-muted-foreground md:flex">
          <span className="inline-flex items-center gap-0.5">
            <DeveloperBoard className="size-3" />
            <MonoNum>{node.spec.vcpu}</MonoNum>
            <span className="ml-0.5 text-[10px] text-muted-foreground">vCPU</span>
          </span>
          <span className="inline-flex items-center gap-0.5">
            <Memory className="size-3" />
            <Size bytes={SizeUtils.gibToBytes(node.spec.ramGb)} />
          </span>
          <span className="inline-flex items-center gap-0.5">
            <HardDisk className="size-3" />
            <Size bytes={SizeUtils.gibToBytes(node.spec.diskGb)} />
          </span>
          <span className="inline-flex items-center gap-0.5">
            <MonoNum>{node.vmCount}</MonoNum> {t('clusters.node.vms')}
          </span>
        </span>
        <StatusPill variant={mapNodeStatusToVariant(node.status)} size="sm">
          <span className="inline-flex items-center gap-1">
            {STATUS_ICON[node.status]}
            {t(nodeStatusLabelKey(node.status))}
          </span>
        </StatusPill>
      </div>
    </div>
  );
}
