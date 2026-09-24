import { useTranslation } from 'react-i18next';
import {
  Cancel,
  DeployedCode,
  DeveloperBoard,
  HardDisk,
  Info,
  Memory,
  ProgressActivity,
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Badge } from '@/shared/ui/primitives/badge';
import { Card, CardContent } from '@/shared/ui/primitives/card';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Size, SizeUtils } from '@/shared/ui/primitives/size';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/shared/ui/primitives/tooltip';
import type { PlexorNode } from '../model/cluster-types';
import { mapNodeStatusToVariant, nodeRoleLabelKey, nodeStatusLabelKey } from '../model/node-status';

const STATUS_ICON: Record<PlexorNode['status'], React.ReactNode> = {
  ready: null,
  pending: <ProgressActivity className="size-3 animate-spin" />,
  draining: <ProgressActivity className="size-3 animate-spin" />,
  offline: <Cancel className="size-3" />,
};

interface NodeCardProps {
  node: PlexorNode;
}

/**
 * Core-importance enumerable: one joined Plexor.NodeAgent as a child
 * mini-card of the cluster's node roster (see INDEX.md "Enumerable data →
 * rendering by importance"). Three bands, matching the classification:
 * identity + status (act on it), capacity (monitor it), categorical role +
 * reference facts (ISO inline for drift scans; joined/last-seen/providers/id
 * behind the info tooltip — audit facts, needed occasionally).
 */
export function NodeCard({ node }: NodeCardProps) {
  const { t } = useTranslation();

  return (
    <Card size="sm" data-od-id="node-card" className="transition-colors hover:ring-foreground/20">
      <CardContent className="flex flex-col gap-2">
        {/* Identity band: who the agent is + live status. */}
        <div className="flex items-center justify-between gap-2">
          <span className="flex min-w-0 items-center gap-2">
            <DeployedCode className="size-4 shrink-0 text-muted-foreground" />
            <MonoNum className="truncate text-sm">{node.hostname}</MonoNum>
          </span>
          <StatusPill variant={mapNodeStatusToVariant(node.status)} size="sm">
            <span className="inline-flex items-center gap-1">
              {STATUS_ICON[node.status]}
              {t(nodeStatusLabelKey(node.status))}
            </span>
          </StatusPill>
        </div>

        {/* Capacity band: what the operator schedules against. */}
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
          <span className="inline-flex items-center gap-1 text-muted-foreground">
            <DeveloperBoard className="size-3" />
            <MonoNum>{node.spec.vcpu}</MonoNum>
            <span className="text-[10px]">vCPU</span>
          </span>
          <span className="inline-flex items-center gap-1 text-muted-foreground">
            <Memory className="size-3" />
            <Size bytes={SizeUtils.gibToBytes(node.spec.ramGb)} />
          </span>
          <span className="inline-flex items-center gap-1 text-muted-foreground">
            <HardDisk className="size-3" />
            <Size bytes={SizeUtils.gibToBytes(node.spec.diskGb)} />
          </span>
          <span className="inline-flex items-center gap-1 text-muted-foreground">
            <MonoNum>{node.vmCount}</MonoNum>
            <span className="text-[10px]">{t('clusters.node.vms')}</span>
          </span>
        </div>

        {/* Categorical + reference band: role chip, ISO inline (version drift
         *  is an upgrade signal), the rest behind the info tooltip. */}
        <div className="flex items-center justify-between gap-2">
          <Badge variant="outline">{t(nodeRoleLabelKey(node.role))}</Badge>
          <span className="flex min-w-0 items-center gap-1.5">
            <span className="truncate rounded border border-border bg-background px-1.5 py-0.5 font-mono text-[10px]">
              {node.isoVersion}
            </span>
            <Tooltip>
              <TooltipTrigger>
                <button
                  type="button"
                  aria-label={t('clusters.node.details')}
                  className="inline-flex shrink-0 items-center text-muted-foreground/70 outline-none transition-colors hover:text-muted-foreground focus-visible:text-foreground"
                >
                  <Info className="size-3.5" />
                </button>
              </TooltipTrigger>
              <TooltipContent side="top">
                {/* Reference trivia inside a tooltip renders as text, not
                 *  Badge chips — chips invert badly on the fg tooltip surface
                 *  (documented exception in INDEX.md). */}
                <span className="flex flex-col items-start gap-0.5 text-left">
                  <span>
                    {t('clusters.node.joined')} {new Date(node.joinedAt).toLocaleDateString()}
                  </span>
                  <span>
                    {t('clusters.node.lastSeen')} {new Date(node.lastSeenAt).toLocaleString()}
                  </span>
                  <span>
                    {t('clusters.node.providers')} {node.spec.providers.join(' \u00b7 ')}
                  </span>
                  <span className="font-mono">{node.id}</span>
                </span>
              </TooltipContent>
            </Tooltip>
          </span>
        </div>
      </CardContent>
    </Card>
  );
}
