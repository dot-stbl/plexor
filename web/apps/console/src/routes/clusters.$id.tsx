import { useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { ArrowLeft, Plus, Key, Stack, ShieldCheck, Clock, Terminal, BookOpen, Lifebuoy } from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { PageHeader } from '@/shared/ui/app-shell';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/ui/primitives/tabs';
import { AddNodeDialog, NodeRow, TokenRow, useGetCluster, useListNodes, useListTokens, countNodes, formatUptime } from '@/features/clusters';

export const Route = createFileRoute('/clusters/$id')({
  component: ClusterDetailPage,
});

const SELF_HELP_LINKS = [
  { href: 'https://plexor.dev/docs/install', label: 'Установка Plexor', icon: BookOpen },
  { href: 'https://plexor.dev/docs/iso', label: 'Скачать ISO', icon: Terminal },
  { href: 'https://plexor.dev/docs/upgrade', label: 'Обновление', icon: ShieldCheck },
  { href: 'https://plexor.dev/docs/troubleshooting', label: 'Troubleshooting', icon: Lifebuoy },
];

function ClusterDetailPage() {
  const { id } = Route.useParams();
  const { cluster } = useGetCluster(id);
  const [addOpen, setAddOpen] = useState(false);
  const [tab, setTab] = useState('nodes');

  if (!cluster) {
    return (
      <main className="mx-auto w-full max-w-6xl px-6 py-12 text-center">
        <p className="text-sm text-muted-foreground">Кластер не найден.</p>
        <Button variant="ghost" render={<Link to="/clusters" />} className="mt-3">
          <ArrowLeft />
          Назад к кластерам
        </Button>
      </main>
    );
  }

  const nodes = useListNodes(cluster.id).nodes;
  const tokens = useListTokens(cluster.id).tokens;
  const counts = countNodes(nodes);
  const activeTokens = tokens.filter((t) => t.status === 'active').length;

  return (
    <main data-od-id="cluster-detail">
      <PageHeader
        title={cluster.name}
        description={
          <>
            <span className="rounded border border-border bg-background px-1.5 py-0.5 font-mono text-[10px] uppercase">
              v{cluster.hostVersion}
            </span>{' '}
            <span className="text-muted-foreground">·</span>{' '}
            <MonoNum>{counts.ready}</MonoNum>/<MonoNum>{counts.total}</MonoNum>{' '}
            <span className="text-muted-foreground">нод(ов) ready ·</span>{' '}
            <MonoNum>{activeTokens}</MonoNum> <span className="text-muted-foreground">активных токенов ·</span>{' '}
            <MonoNum muted>{formatUptime(cluster.uptimeSeconds)}</MonoNum>{' '}
            <span className="text-muted-foreground">uptime</span>
          </>
        }
        actions={
          <>
            <Button variant="ghost" render={<Link to="/clusters" />}>
              <ArrowLeft />
              Назад
            </Button>
            <Button onClick={() => setAddOpen(true)}>
              <Plus />
              Добавить нод
            </Button>
          </>
        }
      />

      <div className="mx-auto w-full max-w-6xl space-y-3 px-6 py-6 lg:px-8">
        {/* Top info card — install providers + self-help links.
            Self-hosted = these are the primary discovery surface. */}
        <Card className="gap-0 p-0">
          <CardHeader className="gap-0.5 border-b border-border p-4">
            <div className="flex items-center justify-between gap-2">
              <div className="space-y-0.5">
                <CardTitle className="text-sm">Install providers</CardTitle>
                <CardDescription>
                  Выбраны при <code className="rounded bg-muted px-1 font-mono text-[10px]">plx init</code>{' '}
                  · endpoint{' '}
                  <MonoNum muted>{cluster.endpoint}</MonoNum>
                </CardDescription>
              </div>
            </div>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-1.5 p-4">
            {cluster.installProviders.map((p) => (
              <span
                key={p}
                className="rounded-md border border-border bg-background px-1.5 py-0.5 text-[10px] font-mono"
              >
                {p}
              </span>
            ))}
          </CardContent>
        </Card>

        <Tabs value={tab} onValueChange={setTab}>
          <TabsList>
            <TabsTrigger value="nodes">
              <Stack className="size-3.5" /> Ноды <span className="ml-1 text-muted-foreground">({counts.total})</span>
            </TabsTrigger>
            <TabsTrigger value="tokens">
              <Key className="size-3.5" /> Join-токены <span className="ml-1 text-muted-foreground">({tokens.length})</span>
            </TabsTrigger>
            <TabsTrigger value="docs">Документация</TabsTrigger>
          </TabsList>

          <TabsContent value="nodes" className="space-y-3">
            <Card className="gap-0 p-0">
              <CardHeader className="gap-0.5 border-b border-border p-4">
                <div className="flex items-center justify-between gap-2">
                  <div className="space-y-0.5">
                    <CardTitle className="text-sm">Plexor.NodeAgent инстансы</CardTitle>
                    <CardDescription>
                      Запускаются на вашем железе. Подключаются через join-токен из вкладки «Join-токены».
                    </CardDescription>
                  </div>
                  <Button size="sm" onClick={() => setAddOpen(true)}>
                    <Plus />
                    Добавить нод
                  </Button>
                </div>
              </CardHeader>
              <CardContent className="p-0">
                {nodes.length === 0 ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">
                    Нодов нет. Сгенерируйте join-токен и установите Plexor ISO.
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
                    <CardTitle className="text-sm">Join-токены</CardTitle>
                    <CardDescription>
                      Генерируются здесь, копируются на нод, используются один раз.
                    </CardDescription>
                  </div>
                  <Button size="sm" onClick={() => setAddOpen(true)}>
                    <Plus />
                    Создать токен
                  </Button>
                </div>
              </CardHeader>
              <CardContent className="p-0">
                {tokens.length === 0 ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">
                    Токенов нет.
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
                <CardTitle className="text-sm">Самостоятельная установка</CardTitle>
                <CardDescription>
                  Plexor self-hosted — вы ставите control-plane и ноды на своём железе.
                </CardDescription>
              </CardHeader>
              <CardContent className="grid grid-cols-1 gap-1.5 p-4 md:grid-cols-2">
                {SELF_HELP_LINKS.map((link) => (
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
              <Clock className="size-3" />
              Токены истекают — после подключения нод остаётся в списке Nodes.
            </div>
          </TabsContent>
        </Tabs>
      </div>

      <AddNodeDialog open={addOpen} onOpenChange={setAddOpen} clusterId={cluster.id} />
    </main>
  );
}