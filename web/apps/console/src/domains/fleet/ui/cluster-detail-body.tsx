import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Add,
  ArrowBack,
  Key,
  MenuBook,
  Schedule,
  Stacks,
  Support,
  Terminal,
  VerifiedUser,
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { Badge } from '@/shared/ui/primitives/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { PageTemplate } from '@/shared/ui/app-shell';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { CopyableText } from '@/shared/ui/primitives/copyable-text';
import { CopyButton } from '@/shared/ui/primitives/copy-button';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { Alert, AlertDescription, AlertTitle } from '@/shared/ui/primitives/alert';
import { countNodes, formatDiskGb, formatUptime } from '../model/cluster-types';
import type { PlexorCluster } from '../model/cluster-types';
import {
  clusterHealth,
  clusterHealthLabelKey,
  mapClusterHealthToVariant,
} from '../model/cluster-health';
import { AddNodeDialog } from './add-node-dialog';
import { NodeRow } from './node-row';
import { TokenRow } from './token-row';

interface ClusterDetailBodyProps {
  clusterId: string;
  cluster: PlexorCluster | undefined;
  /** Navigate back to the cluster list (/clusters). */
  onBack: () => void;
  /**
   * Loading seam — false today (the handmade mock is synchronous), true once
   * the kubb endpoint lands; shows the detail skeleton while set.
   */
  isPending?: boolean;
}

/**
 * /clusters/$id body: identity + docs cards on top, nodes and join-token
 * rosters below. Structure mirrors /vms/$id — thin route, this component
 * owns layout, the add-node dialog and the not-found/loading states.
 */
export function ClusterDetailBody({ clusterId, cluster, onBack, isPending = false }: ClusterDetailBodyProps) {
  const { t } = useTranslation();
  const [addOpen, setAddOpen] = useState(false);

  if (isPending) {
    return (
      <PageTemplate
        title={t('clusters.detail.title')}
        width="wide"
        data-od-id="cluster-detail-skeleton"
        actions={<BackAction onBack={onBack} />}
      >
        <div className="flex flex-col gap-2">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-20 w-full" />
        </div>
      </PageTemplate>
    );
  }

  if (!cluster) {
    return (
      <PageTemplate
        title={t('clusters.detail.notFound')}
        width="default"
        data-od-id="cluster-detail-not-found"
        actions={<BackAction onBack={onBack} />}
      >
        <Alert variant="destructive">
          <AlertTitle>{t('clusters.detail.notFound')}</AlertTitle>
          <AlertDescription>{t('clusters.detail.notFoundDescription', { id: clusterId })}</AlertDescription>
        </Alert>
      </PageTemplate>
    );
  }

  const counts = countNodes(cluster.nodes);
  const health = clusterHealth(cluster.nodes);
  const activeTokens = cluster.tokens.filter((token) => token.status === 'active').length;

  // Aggregate node-spec totals for the Nodes card summary line.
  let vcpu = 0;
  let ramGb = 0;
  let diskGb = 0;
  let vms = 0;
  for (const node of cluster.nodes) {
    vcpu += node.spec.vcpu;
    ramGb += node.spec.ramGb;
    diskGb += node.spec.diskGb;
    vms += node.vmCount;
  }

  const selfHelpLinks = [
    { href: 'https://plexor.dev/docs/install', label: t('clusters.detail.docLinks.install'), icon: MenuBook },
    { href: 'https://plexor.dev/docs/iso', label: t('clusters.detail.docLinks.iso'), icon: Terminal },
    { href: 'https://plexor.dev/docs/upgrade', label: t('clusters.detail.docLinks.upgrade'), icon: VerifiedUser },
    { href: 'https://plexor.dev/docs/troubleshooting', label: t('clusters.detail.docLinks.troubleshooting'), icon: Support },
  ];

  return (
    <div data-od-id="cluster-detail">
      <PageTemplate
        title={cluster.name}
        width="wide"
        description={
          <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
            <StatusPill variant={mapClusterHealthToVariant(health)} size="sm">
              {t(clusterHealthLabelKey(health))}
            </StatusPill>
            <span className="inline-block h-3 w-px bg-border" aria-hidden />
            <span className="rounded border border-border bg-background px-1.5 py-0.5 font-mono text-[10px] uppercase">
              v{cluster.hostVersion}
            </span>
            <span className="inline-block h-3 w-px bg-border" aria-hidden />
            <MonoNum>{counts.ready}</MonoNum>/<MonoNum>{counts.total}</MonoNum>
            <span className="text-muted-foreground">{t('clusters.detail.nodesReady')}</span>
            <span className="inline-block h-3 w-px bg-border" aria-hidden />
            <MonoNum>{activeTokens}</MonoNum>
            <span className="text-muted-foreground">{t('clusters.detail.activeTokens')}</span>
            <span className="inline-block h-3 w-px bg-border" aria-hidden />
            <MonoNum muted>{formatUptime(cluster.uptimeSeconds)}</MonoNum>
            <span className="text-muted-foreground">{t('clusters.detail.uptime')}</span>
          </span>
        }
        actions={
          <>
            <BackAction onBack={onBack} />
            <Button onClick={() => setAddOpen(true)}>
              <Add />
              {t('clusters.detail.addNode')}
            </Button>
          </>
        }
      >
        <div className="flex flex-col gap-3">
          <div className="grid gap-3 md:grid-cols-2">
            {/* Identity — who this control plane is and where to reach it. */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('clusters.detail.section.identity')}</CardTitle>
                <CardDescription>{t('clusters.detail.identityDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <DetailRow label={t('clusters.detail.field.name')}>
                  <InlineValue>
                    {cluster.name}
                    <CopyButton value={cluster.name} copyLabel={t('clusters.detail.copy.name')} />
                  </InlineValue>
                </DetailRow>
                <DetailRow label={t('clusters.detail.field.id')}>
                  <CopyableText value={cluster.id} copyLabel={t('clusters.detail.copy.id')}>
                    <MonoNum muted>{cluster.id}</MonoNum>
                  </CopyableText>
                </DetailRow>
                <DetailRow label={t('clusters.detail.field.health')}>
                  <StatusPill variant={mapClusterHealthToVariant(health)} size="sm">
                    {t(clusterHealthLabelKey(health))}
                  </StatusPill>
                </DetailRow>
                <DetailRow label={t('clusters.detail.field.version')}>
                  <MonoNum>v{cluster.hostVersion}</MonoNum>
                </DetailRow>
                <DetailRow label={t('clusters.detail.field.endpoint')}>
                  <CopyableText value={cluster.endpoint} copyLabel={t('clusters.detail.copy.endpoint')}>
                    <MonoNum muted>{cluster.endpoint}</MonoNum>
                  </CopyableText>
                </DetailRow>
                <DetailRow label={t('clusters.detail.field.uptime')}>
                  <MonoNum muted>{formatUptime(cluster.uptimeSeconds)}</MonoNum>
                </DetailRow>
                <DetailRow label={t('clusters.detail.field.createdAt')}>
                  {new Date(cluster.createdAt).toLocaleString()}
                </DetailRow>
                <DetailRow label={t('clusters.detail.field.installProviders')}>
                  <span className="flex flex-wrap justify-end gap-1">
                    {cluster.installProviders.map((provider) => (
                      <Badge key={provider} variant="secondary">
                        {provider}
                      </Badge>
                    ))}
                  </span>
                </DetailRow>
              </CardContent>
            </Card>

            {/* Self-service docs — self-hosted = the primary discovery surface. */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('clusters.detail.selfHelp')}</CardTitle>
                <CardDescription>{t('clusters.detail.selfHelpDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="grid grid-cols-1 gap-1.5 md:grid-cols-2">
                {selfHelpLinks.map((link) => (
                  <a
                    key={link.href}
                    href={link.href}
                    target="_blank"
                    rel="noreferrer"
                    className="group flex items-center justify-between gap-2 rounded-md border border-border bg-background p-2.5 transition-colors hover:border-foreground/20"
                  >
                    <span className="flex items-center gap-2">
                      <link.icon className="size-4 text-muted-foreground" />
                      <span className="text-sm">{link.label}</span>
                    </span>
                    <span className="text-xs text-muted-foreground group-hover:text-foreground">→</span>
                  </a>
                ))}
              </CardContent>
            </Card>
          </div>

          {/* Nodes — the Plexor.NodeAgent roster with aggregate capacity. */}
          <Card>
            <CardHeader className="border-b border-border">
              <div className="flex items-center justify-between gap-2">
                <div className="space-y-0.5">
                  <CardTitle className="flex items-center gap-1.5 text-sm">
                    <Stacks className="size-3.5" />
                    {t('clusters.detail.nodeAgentInstances')}
                  </CardTitle>
                  <CardDescription>
                    <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-1">
                      <MonoNum muted>{vcpu}</MonoNum>
                      <span>vCPU</span>
                      <span aria-hidden>·</span>
                      <MonoNum muted>{ramGb}</MonoNum>
                      <span>GB RAM</span>
                      <span aria-hidden>·</span>
                      <MonoNum muted>{formatDiskGb(diskGb)}</MonoNum>
                      <span>{t('clusters.detail.field.disk')}</span>
                      <span aria-hidden>·</span>
                      <MonoNum muted>{vms}</MonoNum>
                      <span>{t('clusters.detail.field.vms')}</span>
                    </span>
                  </CardDescription>
                </div>
                <Button size="sm" onClick={() => setAddOpen(true)}>
                  <Add />
                  {t('clusters.detail.addNode')}
                </Button>
              </div>
            </CardHeader>
            <CardContent className="p-0">
              {cluster.nodes.length === 0 ? (
                <div className="p-6">
                  <EmptyState
                    data-od-id="cluster-detail-no-nodes"
                    icon={Stacks}
                    compact
                    title={t('clusters.detail.noNodes')}
                    description={t('clusters.detail.noNodesDescription')}
                    action={
                      <Button size="sm" onClick={() => setAddOpen(true)}>
                        <Add />
                        {t('clusters.detail.addNode')}
                      </Button>
                    }
                  />
                </div>
              ) : (
                <div className="divide-y divide-border">
                  {cluster.nodes.map((node) => (
                    <NodeRow key={node.id} node={node} />
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {/* Join tokens — issue flow + revocation. */}
          <Card>
            <CardHeader className="border-b border-border">
              <div className="flex items-center justify-between gap-2">
                <div className="space-y-0.5">
                  <CardTitle className="flex items-center gap-1.5 text-sm">
                    <Key className="size-3.5" />
                    {t('clusters.detail.joinTokens')}
                  </CardTitle>
                  <CardDescription>{t('clusters.detail.tokensDescription')}</CardDescription>
                </div>
                <Button size="sm" onClick={() => setAddOpen(true)}>
                  <Add />
                  {t('clusters.detail.createToken')}
                </Button>
              </div>
            </CardHeader>
            <CardContent className="p-0">
              {cluster.tokens.length === 0 ? (
                <div className="p-6">
                  <EmptyState
                    data-od-id="cluster-detail-no-tokens"
                    icon={Key}
                    compact
                    title={t('clusters.detail.noTokensTitle')}
                    description={t('clusters.detail.noTokens')}
                    action={
                      <Button size="sm" onClick={() => setAddOpen(true)}>
                        <Add />
                        {t('clusters.detail.createToken')}
                      </Button>
                    }
                  />
                </div>
              ) : (
                <div className="divide-y divide-border">
                  {cluster.tokens.map((token) => (
                    <TokenRow key={token.id} clusterId={cluster.id} token={token} />
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            <Schedule className="size-3" />
            {t('clusters.detail.tokensExpireNote')}
          </div>
        </div>
      </PageTemplate>

      <AddNodeDialog open={addOpen} onOpenChange={setAddOpen} clusterId={cluster.id} />
    </div>
  );
}

/** Shared "back to /clusters" ghost button used by every header state. */
function BackAction({ onBack }: { onBack: () => void }) {
  const { t } = useTranslation();
  return (
    <Button variant="ghost" onClick={onBack}>
      <ArrowBack />
      {t('common.back')}
    </Button>
  );
}

interface DetailRowProps {
  label: string;
  children: React.ReactNode;
}

function DetailRow({ label, children }: DetailRowProps) {
  return (
    <div className="flex items-start justify-between gap-3 border-b border-border/50 py-1.5 last:border-b-0">
      <span className="shrink-0 text-xs text-muted-foreground">{label}</span>
      <span className="min-w-0 text-right text-xs">{children}</span>
    </div>
  );
}

/** Right-aligned cell that places a value and an inline Copy affordance flush
 *  against each other. Single-line when both fit; the value truncates. */
function InlineValue({ children }: { children: React.ReactNode }) {
  return <span className="inline-flex items-center justify-end gap-1.5">{children}</span>;
}
