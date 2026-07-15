import { useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import {
  Add,
  ArrowBack,
  DeployedCode,
  MenuBook,
  Stacks
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Progress } from '@/shared/ui/primitives/progress';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { Badge } from '@/shared/ui/primitives/badge';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbSeparator,
} from '@/shared/ui/primitives/breadcrumb';
import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/shared/ui/primitives/field';
import { PageHeader } from '@/shared/ui/app-shell';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/shared/ui/primitives/empty';
import { useListClusters } from '@/features/clusters';
import type { NodeStatus } from '@/features/clusters';

export const Route = createFileRoute('/vms/new')({
  component: CreateVmPage,
});

const STATUS_LABEL: Record<NodeStatus, string> = {
  ready: 'ready',
  pending: 'pending',
  draining: 'draining',
  offline: 'offline',
};

const STATUS_VARIANT: Record<NodeStatus, 'running' | 'pending' | 'idle' | 'err'> = {
  ready: 'running',
  pending: 'pending',
  draining: 'idle',
  offline: 'err',
};

const IMAGES = ['ubuntu-24.04', 'debian-12', 'alpine-3.20', 'rocky-9'] as const;
const NETWORKS = ['prod-vpc', 'staging-vpc'] as const;
const SSH_KEYS = ['alex@workstation', 'deploy@ci'] as const;

/**
 * Create VM page. VMs and Clusters are parallel resources — this page
 * lives at /vms/new (NOT /clusters/$id/vms/new). Cluster is picked
 * inline via a Select from the fleet.
 *
 * Single self-hosted cluster per VM; pick the Plexor.NodeAgent, then
 * VM-specific fields (image, network, SSH key).
 */
function CreateVmPage() {
  const navigate = useNavigate();
  const { clusters } = useListClusters();

  // Flatten all nodes across all clusters into one selector — in single-
  // cluster setups the cluster context is implicit.
  const allNodes = clusters.flatMap((c) => c.nodes);
  const readyNodes = allNodes.filter((n) => n.status === 'ready');

  const [nodeId, setNodeId] = useState<string>('');
  const [name, setName] = useState('');
  const [image, setImage] = useState<string>('');
  const [network, setNetwork] = useState<string>('');
  const [sshKey, setSshKey] = useState<string>('');

  const selectedNode = allNodes.find((n) => n.id === nodeId);
  const selectedCluster = clusters.find((c) => c.nodes.some((n) => n.id === nodeId));

  // VM count placeholder (real impl reads from cluster.totals — not
  // modeled yet). Used 2 vCPU / 4 GB / 40 GB per VM as a rough estimate.
  const estVmUsage = { cpu: 2, ramGb: 4, diskGb: 40 };

  return (
    <main data-od-id="vms-new">
      <Breadcrumb className="mx-auto w-full max-w-3xl px-6 pt-6 lg:px-8">
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink render={<Link to="/vms" />}>Виртуальные машины</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink render={<Link to="/vms/new" />}>Новая ВМ</BreadcrumbLink>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <PageHeader
        title="Создать виртуальную машину"
        description="VM-specific настройки. Нод и кластер выбираются ниже."
        actions={
          <Button variant="ghost" nativeButton={false} render={<Link to="/vms" />}>
            <ArrowBack />
            Назад
          </Button>
        }
      />

      {/* Empty state: 0 clusters → user hasn't installed Plexor yet. */}
      {clusters.length === 0 && (
        <div className="mx-auto w-full max-w-3xl px-6 py-12 lg:px-8">
          <Empty data-od-id="vms-new-no-cluster">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <DeployedCode />
              </EmptyMedia>
              <EmptyTitle>Plexor control plane не зарегистрирован</EmptyTitle>
              <EmptyDescription>
                Чтобы создавать VM, нужно сначала установить Plexor на сервере. Установка
                ставит control plane и регистрирует его в UI.
              </EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <Button render={<a href="https://plexor.dev/docs/install" target="_blank" rel="noreferrer" />}>
                <MenuBook />
                Документация по установке
              </Button>
            </EmptyContent>
          </Empty>
        </div>
      )}

      {/* Empty state: clusters exist, 0 ready nodes → user needs to connect a node. */}
      {clusters.length > 0 && readyNodes.length === 0 && (
        <div className="mx-auto w-full max-w-3xl px-6 py-12 lg:px-8">
          <Empty data-od-id="vms-new-no-ready-nodes">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <Stacks />
              </EmptyMedia>
              <EmptyTitle>Нет ready-нодов</EmptyTitle>
              <EmptyDescription>
                Подключите Plexor.NodeAgent к кластеру через join-токен. После heartbeat
                (≤ 2 мин) нод появится здесь и можно будет создавать VM.
              </EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <Button nativeButton={false} render={<Link to="/clusters/$id" params={{ id: clusters[0]!.id }} />}>
                <ArrowBack />
                Перейти к кластеру
              </Button>
            </EmptyContent>
          </Empty>
        </div>
      )}

      {clusters.length > 0 && readyNodes.length > 0 && (
      <div className="mx-auto w-full max-w-3xl space-y-3 px-6 py-6 lg:px-8">
        {/* Node context — which Plexor.NodeAgent hosts the VM. */}
        <Card data-od-id="vm-node-context" className="gap-0 p-0">
          <CardHeader className="gap-0.5 border-b border-border p-4">
            <div className="flex items-center justify-between gap-2">
              <div className="space-y-0.5">
                <CardTitle className="text-sm">Нод</CardTitle>
                <CardDescription>
                  Только <Badge variant="secondary">ready</Badge> ноды принимают новые VM. Pending/draining/offline — фильтруются.
                </CardDescription>
              </div>
              {readyNodes.length > 0 ? (
                <Select
                  items={readyNodes.map((n) => ({
                    value: n.id,
                    label: `${n.hostname} · ${n.role === 'control' ? 'control-plane' : 'compute'}`,
                  }))}
                  value={nodeId}
                  onValueChange={(value) => setNodeId(value ?? '')}
                >
                  <SelectTrigger className="min-w-[280px]">
                    <SelectValue placeholder="Выберите нод" />
                  </SelectTrigger>
                  <SelectContent>
                    {readyNodes.map((n) => (
                      <SelectItem key={n.id} value={n.id}>
                        {n.hostname} · {n.role === 'control' ? 'control-plane' : 'compute'}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <span className="text-xs text-muted-foreground">Нет ready нодов</span>
              )}
            </div>
          </CardHeader>
          <CardContent className="grid grid-cols-1 gap-3 p-4 md:grid-cols-3">
            <CapacityCell
              label="CPU"
              used={selectedNode ? estVmUsage.cpu : 0}
              total={selectedNode?.spec.vcpu ?? 0}
              unit="vCPU"
            />
            <CapacityCell
              label="RAM"
              used={selectedNode ? estVmUsage.ramGb : 0}
              total={selectedNode?.spec.ramGb ?? 0}
              unit="GB"
            />
            <CapacityCell
              label="Диск"
              used={selectedNode ? estVmUsage.diskGb : 0}
              total={selectedNode?.spec.diskGb ?? 0}
              unit="GB"
            />
          </CardContent>
          {selectedNode && (
            <div className="flex flex-wrap items-center gap-2 border-t border-border p-4 text-xs">
              <StatusPill variant={STATUS_VARIANT[selectedNode.status]} size="sm">
                {STATUS_LABEL[selectedNode.status]}
              </StatusPill>
              <Badge variant="outline">ISO v{selectedNode.isoVersion}</Badge>
              {selectedCluster && (
                <Badge variant="outline">
                  Cluster <MonoNum>{selectedCluster.name}</MonoNum>
                </Badge>
              )}
            </div>
          )}
        </Card>

        <Card className="gap-0 p-0">
          <CardHeader className="gap-0.5 border-b border-border p-4">
            <CardTitle className="text-sm">Параметры ВМ</CardTitle>
            <CardDescription>Имя, образ, сеть и SSH-ключ.</CardDescription>
          </CardHeader>
          <CardContent className="p-4">
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="create-vm-name">Имя</FieldLabel>
                <Input
                  id="create-vm-name"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  placeholder="например, web-prod-02"
                />
                <FieldDescription>
                  Используется как hostname. Строчные латинские буквы, цифры и дефис.
                </FieldDescription>
              </Field>

              <Field>
                <FieldLabel htmlFor="create-vm-image">Образ ОС</FieldLabel>
                <Select
                  items={IMAGES.map((i) => ({ value: i, label: i }))}
                  value={image}
                  onValueChange={(value) => setImage(value ?? '')}
                >
                  <SelectTrigger id="create-vm-image">
                    <SelectValue placeholder="Выберите образ" />
                  </SelectTrigger>
                  <SelectContent>
                    {IMAGES.map((i) => (
                      <SelectItem key={i} value={i}>
                        {i}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldDescription>Базовый образ диска.</FieldDescription>
              </Field>

              <Field>
                <FieldLabel htmlFor="create-vm-network">Сеть</FieldLabel>
                <Select
                  items={NETWORKS.map((n) => ({ value: n, label: n }))}
                  value={network}
                  onValueChange={(value) => setNetwork(value ?? '')}
                >
                  <SelectTrigger id="create-vm-network">
                    <SelectValue placeholder="VPC / subnet" />
                  </SelectTrigger>
                  <SelectContent>
                    {NETWORKS.map((n) => (
                      <SelectItem key={n} value={n}>
                        {n}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>

              <Field>
                <FieldLabel htmlFor="create-vm-ssh">SSH-ключ</FieldLabel>
                <Select
                  items={SSH_KEYS.map((k) => ({ value: k, label: k }))}
                  value={sshKey}
                  onValueChange={(value) => setSshKey(value ?? '')}
                >
                  <SelectTrigger id="create-vm-ssh">
                    <SelectValue placeholder="Выберите ключ" />
                  </SelectTrigger>
                  <SelectContent>
                    {SSH_KEYS.map((k) => (
                      <SelectItem key={k} value={k}>
                        {k}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            </FieldGroup>
          </CardContent>
        </Card>

        <div className="flex items-center justify-between">
          <Button variant="outline" nativeButton={false} render={<Link to="/vms" />}>
            Отмена
          </Button>
          <Button
            onClick={() => navigate({ to: '/vms' })}
            disabled={!nodeId || !name || !image || !network || !sshKey}
          >
            <Add />
            Создать ВМ
          </Button>
        </div>
      </div>
      )}
    </main>
  );
}

function CapacityCell({
  label,
  used,
  total,
  unit,
}: {
  label: string;
  used: number;
  total: number;
  unit: string;
}) {
  const pct = total === 0 ? 0 : Math.round((used / total) * 100);
  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between text-[11px] font-medium text-muted-foreground uppercase tracking-[0.06em]">
        <span>{label}</span>
        <span>{pct}%</span>
      </div>
      <div className="flex items-baseline gap-1">
        <MonoNum>{used}</MonoNum>
        <span className="text-muted-foreground">/</span>
        <MonoNum muted>{total}</MonoNum>
        <span className="text-muted-foreground text-xs">{unit}</span>
      </div>
      <Progress value={pct} />
    </div>
  );
}