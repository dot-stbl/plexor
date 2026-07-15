import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { createFileRoute, Link } from '@tanstack/react-router';
import {
  Add,
  ArrowBack,
  Key,
  MenuBook,
  Schedule,
  Stacks,
  Support,
  Terminal,
  VerifiedUser
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { Badge } from '@/shared/ui/primitives/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { PageTemplate } from '@/shared/ui/app-shell';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/ui/primitives/tabs';
import { AddNodeDialog, NodeRow, TokenRow, useGetCluster, useListNodes, useListTokens, countNodes, formatUptime } from '@/features/clusters';

export const Route = createFileRoute('/clusters/$id')({
  component: ClusterDetailPage,
});

function ClusterDetailPage() {
  const { t } = useTranslation();
  const { id } = Route.useParams();
  const { cluster } = useGetCluster(id);
  const [addOpen, setAddOpen] = useState(false);
  const [tab, setTab] = useState('nodes');

  if (!cluster) {
    return (
      <PageTemplate
        title={t('clusters.detail.notFound')}
        width="6xl"
        actions={
          <Button variant="ghost" nativeButton={false} render={<Link to="/clusters" />}>
            <ArrowBack />
            {t('clusters.detail.backToClusters')}
          </Button>
        }
      >
        <p className="py-12 text-center text-sm text-muted-foreground">
          {t('clusters.detail.notFoundDescription', { id })}
        </p>
      </PageTemplate>
    );
  }

  const nodes = useListNodes(cluster.id).nodes;
  const tokens = useListTokens(cluster.id).tokens;
  const counts = countNodes(nodes);
  const activeTokens = tokens.filter((t) => t.status === 'active').length;

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
        width="full"
        description={
          <>
            <span className="rounded border border-border bg-background px-1.5 py-0.5 font-mono text-[10px] uppercase">
              v{cluster.hostVersion}
            </span>{' '}
            <span className="text-muted-foreground">·</span>{' '}
            <MonoNum>{counts.ready}</MonoNum>/<MonoNum>{counts.total}</MonoNum>{' '}
            <span className="text-muted-foreground">{t('clusters.detail.nodesReady')} ·</span>{' '}
            <MonoNum>{activeTokens}</MonoNum> <span className="text-muted-foreground">{t('clusters.detail.activeTokens')} ·</span>{' '}
            <MonoNum muted>{formatUptime(cluster.uptimeSeconds)}</MonoNum>{' '}
            <span className="text-muted-foreground">uptime</span>
          </>
        }
        actions={
          <>
            <Button variant="ghost" nativeButton={false} render={<Link to="/clusters" />}>
              <ArrowBack />
              {t('common.back')}
            </Button>
            <Button onClick={() => setAddOpen(true)}>
              <Add />
              {t('clusters.detail.addNode')}
            </Button>
          </>
        }
      >
        {/* Top info card — install providers + self-help links.
            Self-hosted = these are the primary discovery surface. */}
        <Card className="gap-0 p-0">
          <CardHeader className="gap-0.5 border-b border-border p-4">
            <div className="flex items-center justify-between gap-2">
              <div className="space-y-0.5">
                <CardTitle className="text-sm">{t('clusters.detail.installProviders')}</CardTitle>
                <CardDescription>
                  {t('clusters.detail.providersChosenAt')} <code className="rounded bg-muted px-1 font-mono text-[10px]">plx init</code>{' '}
                  · endpoint{' '}
                  <MonoNum muted>{cluster.endpoint}</MonoNum>
                </CardDescription>
              </div>
            </div>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-1.5 p-4">
            {cluster.installProviders.map((p) => (
              <Badge key={p} variant="secondary">
                {p}
              </Badge>
            ))}
          </CardContent>
        </Card>

        <Tabs value={tab} onValueChange={setTab}>
          <TabsList>
            <TabsTrigger value="nodes">
              <Stacks className="size-3.5" /> {t('clusters.detail.nodes')} <span className="ml-1 text-muted-foreground">({counts.total})</span>
            </TabsTrigger>
            <TabsTrigger value="tokens">
              <Key className="size-3.5" /> {t('clusters.detail.joinTokens')} <span className="ml-1 text-muted-foreground">({tokens.length})</span>
            </TabsTrigger>
            <TabsTrigger value="docs">{t('clusters.detail.docs')}</TabsTrigger>
          </TabsList>

          <TabsContent value="nodes" className="space-y-3">
            <Card className="gap-0 p-0">
              <CardHeader className="gap-0.5 border-b border-border p-4">
                <div className="flex items-center justify-between gap-2">
                  <div className="space-y-0.5">
                    <CardTitle className="text-sm">{t('clusters.detail.nodeAgentInstances')}</CardTitle>
                    <CardDescription>
                      {t('clusters.detail.nodesDescription')}
                    </CardDescription>
                  </div>
                  <Button size="sm" onClick={() => setAddOpen(true)}>
                    <Add />
                    {t('clusters.detail.addNode')}
                  </Button>
                </div>
              </CardHeader>
              <CardContent className="p-0">
                {nodes.length === 0 ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">
                    {t('clusters.detail.noNodes')}
                  </div>
                ) : (
                  <div className="divide-y divide-border">
                    {nodes.map((n) => (
                      <NodeRow key={n.id} node={n} />
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="tokens" className="space-y-3">
            <Card className="gap-0 p-0">
              <CardHeader className="gap-0.5 border-b border-border p-4">
                <div className="flex items-center justify-between gap-2">
                  <div className="space-y-0.5">
                    <CardTitle className="text-sm">{t('clusters.detail.joinTokens')}</CardTitle>
                    <CardDescription>
                      {t('clusters.detail.tokensDescription')}
                    </CardDescription>
                  </div>
                  <Button size="sm" onClick={() => setAddOpen(true)}>
                    <Add />
                    {t('clusters.detail.createToken')}
                  </Button>
                </div>
              </CardHeader>
              <CardContent className="p-0">
                {tokens.length === 0 ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">
                    {t('clusters.detail.noTokens')}
                  </div>
                ) : (
                  <div className="divide-y divide-border">
                    {tokens.map((t) => (
                      <TokenRow key={t.id} clusterId={cluster.id} token={t} />
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="docs" className="space-y-3">
            <Card className="gap-0 p-0">
              <CardHeader className="gap-0.5 border-b border-border p-4">
                <CardTitle className="text-sm">{t('clusters.detail.selfHelp')}</CardTitle>
                <CardDescription>
                  {t('clusters.detail.selfHelpDescription')}
                </CardDescription>
              </CardHeader>
              <CardContent className="grid grid-cols-1 gap-1.5 p-4 md:grid-cols-2">
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
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <Schedule className="size-3" />
              {t('clusters.detail.tokensExpireNote')}
            </div>
          </TabsContent>
        </Tabs>
      </PageTemplate>

      <AddNodeDialog open={addOpen} onOpenChange={setAddOpen} clusterId={cluster.id} />
    </div>
  );
}