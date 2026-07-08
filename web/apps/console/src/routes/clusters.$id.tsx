import type { ReactNode } from 'react';
import { useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { ArrowLeft, Plus, HardDrives, Cpu, Memory, HardDrive } from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import { PageHeader } from '@/shared/ui/app-shell';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Progress } from '@/shared/ui/primitives/progress';
import { Stat } from '@/shared/ui/primitives/stat';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/ui/primitives/tabs';
import { clusterUtilizationPct, useGetCluster } from '@/features/clusters';
import type { Cluster } from '@/features/clusters';

export const Route = createFileRoute('/clusters/$id')({
  component: ClusterDetailPage,
});

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

const NODE_STATUS_VARIANT = {
  ready: 'running',
  draining: 'pending',
  offline: 'err',
} as const;

const NODE_STATUS_LABEL = {
  ready: 'ready',
  draining: 'draining',
  offline: 'offline',
} as const;

function ClusterDetailPage() {
  const { id } = Route.useParams();
  const { cluster } = useGetCluster(id);
  const [tab, setTab] = useState('overview');

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

  const util = clusterUtilizationPct(cluster);

  return (
    <main data-od-id="cluster-detail">
      <PageHeader
        title={cluster.name}
        description={
          <>
            <StatusPill variant={STATUS_VARIANT[cluster.status]} size="sm">
              {STATUS_LABEL[cluster.status]}
            </StatusPill>{' '}
            <span className="text-muted-foreground">·</span>{' '}
            <MonoNum>{cluster.nodeCount}</MonoNum> <span className="text-muted-foreground">нод ·</span>{' '}
            <MonoNum>{cluster.vmCount}</MonoNum> <span className="text-muted-foreground">VM ·</span>{' '}
            <MonoNum muted>{cluster.zone}</MonoNum>
          </>
        }
        actions={
          <>
            <Button variant="ghost" render={<Link to="/clusters" />}>
              <ArrowLeft />
              Назад
            </Button>
            <Button render={<Link to="/clusters/$id/vms/new" params={{ id: cluster.id }} />}>
              <Plus />
              Создать VM
            </Button>
          </>
        }
      />

      <div className="mx-auto w-full max-w-6xl space-y-3 px-6 py-6 lg:px-8">
        {/* Capacity strip — 3 stat cards (CPU / RAM / Disk) with bars */}
        <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
          <CapacityStat
            icon={<Cpu className="size-4" />}
            label="CPU"
            used={cluster.usedCpu}
            total={cluster.totalCpu}
            pct={util.cpu}
            unit="vCPU"
          />
          <CapacityStat
            icon={<Memory className="size-4" />}
            label="RAM"
            used={cluster.usedRamGb}
            total={cluster.totalRamGb}
            pct={util.ram}
            unit="GB"
          />
          <CapacityStat
            icon={<HardDrive className="size-4" />}
            label="Диск"
            used={cluster.usedDiskGb}
            total={cluster.totalDiskGb}
            pct={util.disk}
            unit="GB"
          />
        </div>

        <Tabs value={tab} onValueChange={setTab}>
          <TabsList>
            <TabsTrigger value="overview">Обзор</TabsTrigger>
            <TabsTrigger value="nodes">
              Ноды <span className="ml-1 text-muted-foreground">({cluster.nodeCount})</span>
            </TabsTrigger>
            <TabsTrigger value="flavors">
              Флейворы <span className="ml-1 text-muted-foreground">({cluster.flavors.length})</span>
            </TabsTrigger>
          </TabsList>

          <TabsContent value="overview" className="space-y-3">
            <Card className="gap-0 p-0">
              <CardHeader className="gap-0.5 border-b border-border p-4">
                <CardTitle className="text-sm">Состав кластера</CardTitle>
                <CardDescription>Ноды, доступные ресурсы и текущая нагрузка.</CardDescription>
              </CardHeader>
              <CardContent className="grid grid-cols-1 gap-3 p-4 md:grid-cols-3">
                <Stat label="Нод" value={cluster.nodeCount} context={`${cluster.vmCount} VM размещено`} />
                <Stat label="Flavors" value={cluster.flavors.length} context="доступно для ВМ" />
                <Stat label="Зона" value={cluster.zone} context="eu-central-1 / Frankfurt" />
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="nodes" className="space-y-3">
            <Card className="gap-0 p-0">
              <CardHeader className="gap-0.5 border-b border-border p-4">
                <CardTitle className="text-sm">Ноды кластера</CardTitle>
                <CardDescription>Роль, состояние и количество VM на каждом узле.</CardDescription>
              </CardHeader>
              <CardContent className="p-0">
                <div className="divide-y divide-border">
                  {cluster.nodes.map((node) => (
                    <div key={node.id} className="flex items-center justify-between p-3">
                      <div className="flex items-center gap-2 min-w-0">
                        <HardDrives className="size-4 shrink-0 text-muted-foreground" />
                        <div className="min-w-0">
                          <MonoNum className="text-sm">{node.hostname}</MonoNum>
                          <p className="text-[10px] text-muted-foreground uppercase tracking-[0.06em]">
                            {node.role}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center gap-3 text-xs">
                        <span className="text-muted-foreground">
                          <MonoNum>{node.vcpu}</MonoNum> vCPU ·{' '}
                          <MonoNum>{node.ramGb}</MonoNum> GB
                        </span>
                        <span className="text-muted-foreground">
                          <MonoNum>{node.vmCount}</MonoNum> VM
                        </span>
                        <StatusPill variant={NODE_STATUS_VARIANT[node.status]} size="sm">
                          {NODE_STATUS_LABEL[node.status]}
                        </StatusPill>
                      </div>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="flavors" className="space-y-3">
            <Card className="gap-0 p-0">
              <CardHeader className="gap-0.5 border-b border-border p-4">
                <CardTitle className="text-sm">Доступные флейворы</CardTitle>
                <CardDescription>
                  Шаблоны CPU/RAM/диск, которые могут работать на этом кластере.
                </CardDescription>
              </CardHeader>
              <CardContent className="p-0">
                <div className="divide-y divide-border">
                  {cluster.flavors.map((flavor) => (
                    <div key={flavor.id} className="flex items-center justify-between p-3">
                      <div className="min-w-0">
                        <span className="text-sm font-medium">{flavor.id}</span>
                      </div>
                      <div className="flex items-center gap-3 text-xs text-muted-foreground">
                        <span>
                          <MonoNum>{flavor.vcpu}</MonoNum> vCPU
                        </span>
                        <span>
                          <MonoNum>{flavor.ramGb}</MonoNum> GB RAM
                        </span>
                        <span>
                          <MonoNum>{flavor.diskGb}</MonoNum> GB диск
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      </div>
    </main>
  );
}

function CapacityStat({
  icon,
  label,
  used,
  total,
  pct,
  unit,
}: {
  icon: ReactNode;
  label: string;
  used: number;
  total: number;
  pct: number;
  unit: string;
}) {
  return (
    <Card className="gap-0 p-0">
      <CardContent className="space-y-2 p-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground uppercase tracking-[0.06em]">
            {icon}
            {label}
          </div>
          <span className="text-xs text-muted-foreground">{pct}%</span>
        </div>
        <div className="flex items-baseline gap-1">
          <MonoNum className="text-xl">{used}</MonoNum>
          <span className="text-muted-foreground">/</span>
          <MonoNum muted>{total}</MonoNum>
          <span className="text-muted-foreground text-xs">{unit}</span>
        </div>
        <Progress value={pct} />
      </CardContent>
    </Card>
  );
}