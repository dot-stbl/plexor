import { useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { ArrowLeft, Plus } from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Progress } from '@/shared/ui/primitives/progress';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
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
import { useGetCluster, useListNodes } from '@/features/clusters';
import type { NodeStatus } from '@/features/clusters';

export const Route = createFileRoute('/clusters/$id/vms/new')({
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

/**
 * Create VM page — self-hosted Plexor. The cluster is the control plane
 * we're already inside; the user picks a NODE (Plexor.NodeAgent) to host
 * the VM, then VM-specific config: image, network, SSH key.
 *
 * No "zone", no "flavor" — those are self-configured on each node at
 * `plx init` time. Flavor (CPU/RAM) is implicit in the node's capacity.
 */
function CreateVmPage() {
  const navigate = useNavigate();
  const { id: clusterId } = Route.useParams();
  const { cluster } = useGetCluster(clusterId);
  const { nodes } = useListNodes(clusterId);

  const [nodeId, setNodeId] = useState<string>('');
  const [name, setName] = useState('');
  const [image, setImage] = useState<string>('');
  const [network, setNetwork] = useState<string>('');
  const [sshKey, setSshKey] = useState<string>('');

  if (!cluster) {
    return (
      <main className="mx-auto w-full max-w-3xl px-6 py-12 text-center">
        <p className="text-sm text-muted-foreground">Кластер не найден.</p>
        <Button variant="ghost" render={<Link to="/clusters" />} className="mt-3">
          <ArrowLeft />
          Назад к кластерам
        </Button>
      </main>
    );
  }

  const readyNodes = nodes.filter((n) => n.status === 'ready');
  const selectedNode = nodes.find((n) => n.id === nodeId);

  return (
    <main data-od-id="vms-new">
      <Breadcrumb className="mx-auto w-full max-w-3xl px-6 pt-6 lg:px-8">
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink render={<Link to="/clusters" />}>Кластеры</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink render={<Link to="/clusters/$id" params={{ id: cluster.id }} />}>
              {cluster.name}
            </BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink render={<Link to="/clusters/$id/vms/new" params={{ id: cluster.id }} />}>
              Новая ВМ
            </BreadcrumbLink>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <PageHeader
        title="Создать виртуальную машину"
        description="VM-specific настройки. Cluster уже выбран, нод выбирается ниже."
        actions={
          <Button variant="ghost" render={<Link to="/clusters/$id" params={{ id: cluster.id }} />}>
            <ArrowLeft />
            Назад к кластеру
          </Button>
        }
      />

      <div className="mx-auto w-full max-w-3xl space-y-3 px-6 py-6 lg:px-8">
        {/* Node context — which Plexor.NodeAgent hosts the VM. */}
        <Card data-od-id="vm-node-context" className="gap-0 p-0">
          <CardHeader className="gap-0.5 border-b border-border p-4">
            <div className="flex items-center justify-between gap-2">
              <div className="space-y-0.5">
                <CardTitle className="text-sm">Нод</CardTitle>
                <CardDescription>
                  VM будет размещена на выбранном Plexor.NodeAgent. Только ноды со статусом ready принимают новые VM.
                </CardDescription>
              </div>
              {readyNodes.length > 0 ? (
                <Select
                  items={readyNodes.map((n) => ({ value: n.id, label: n.hostname }))}
                  value={nodeId}
                  onValueChange={(v) => setNodeId(v ?? '')}
                >
                  <SelectTrigger className="min-w-[220px]">
                    <SelectValue placeholder="Выберите нод" />
                  </SelectTrigger>
                  <SelectContent>
                    {readyNodes.map((n) => (
                      <SelectItem key={n.id} value={n.id}>
                        {n.hostname}
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
              used={selectedNode ? selectedNode.vmCount * 2 : 0}
              total={selectedNode?.spec.vcpu ?? 0}
              unit="vCPU"
            />
            <CapacityCell
              label="RAM"
              used={selectedNode ? selectedNode.vmCount * 4 : 0}
              total={selectedNode?.spec.ramGb ?? 0}
              unit="GB"
            />
            <CapacityCell
              label="Диск"
              used={selectedNode ? selectedNode.vmCount * 40 : 0}
              total={selectedNode?.spec.diskGb ?? 0}
              unit="GB"
            />
          </CardContent>
          {selectedNode && (
            <div className="flex items-center gap-2 border-t border-border p-4 text-xs text-muted-foreground">
              <StatusPill variant={STATUS_VARIANT[selectedNode.status]} size="sm">
                {STATUS_LABEL[selectedNode.status]}
              </StatusPill>
              <span>ISO v{selectedNode.isoVersion}</span>
            </div>
          )}
        </Card>

        {/* VM-specific config. */}
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
                  items={[
                    { value: 'ubuntu-24.04', label: 'Ubuntu 24.04 LTS (x86_64)' },
                    { value: 'debian-12', label: 'Debian 12 (bookworm)' },
                    { value: 'alpine-3.20', label: 'Alpine 3.20 (minimal)' },
                    { value: 'rocky-9', label: 'Rocky Linux 9' },
                  ]}
                  value={image}
                  onValueChange={(value) => setImage(value ?? '')}
                >
                  <SelectTrigger id="create-vm-image">
                    <SelectValue placeholder="Выберите образ" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="ubuntu-24.04">Ubuntu 24.04 LTS (x86_64)</SelectItem>
                    <SelectItem value="debian-12">Debian 12 (bookworm)</SelectItem>
                    <SelectItem value="alpine-3.20">Alpine 3.20 (minimal)</SelectItem>
                    <SelectItem value="rocky-9">Rocky Linux 9</SelectItem>
                  </SelectContent>
                </Select>
                <FieldDescription>Базовый образ диска. Cloud-init userdata добавляется в Screen 03.</FieldDescription>
              </Field>

              <Field>
                <FieldLabel htmlFor="create-vm-network">Сеть</FieldLabel>
                <Select
                  items={[
                    { value: 'prod-vpc', label: 'prod-vpc (10.128.0.0/16)' },
                    { value: 'staging-vpc', label: 'staging-vpc (10.140.0.0/16)' },
                  ]}
                  value={network}
                  onValueChange={(value) => setNetwork(value ?? '')}
                >
                  <SelectTrigger id="create-vm-network">
                    <SelectValue placeholder="Выберите VPC / subnet" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="prod-vpc">prod-vpc (10.128.0.0/16)</SelectItem>
                    <SelectItem value="staging-vpc">staging-vpc (10.140.0.0/16)</SelectItem>
                  </SelectContent>
                </Select>
                <FieldDescription>VPC и подсеть для подключения ВМ.</FieldDescription>
              </Field>

              <Field>
                <FieldLabel htmlFor="create-vm-ssh">SSH-ключ</FieldLabel>
                <Select
                  items={[
                    { value: 'alex@workstation', label: 'alex@workstation' },
                    { value: 'deploy@ci', label: 'deploy@ci (read-only)' },
                  ]}
                  value={sshKey}
                  onValueChange={(value) => setSshKey(value ?? '')}
                >
                  <SelectTrigger id="create-vm-ssh">
                    <SelectValue placeholder="Выберите SSH-ключ" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="alex@workstation">alex@workstation</SelectItem>
                    <SelectItem value="deploy@ci">deploy@ci (read-only)</SelectItem>
                  </SelectContent>
                </Select>
                <FieldDescription>Публичный ключ будет добавлен в ~/.ssh/authorized_keys.</FieldDescription>
              </Field>
            </FieldGroup>
          </CardContent>
        </Card>

        <div className="flex items-center justify-between">
          <Button variant="outline" render={<Link to="/clusters/$id" params={{ id: cluster.id }} />}>
            Отмена
          </Button>
          <Button
            onClick={() => navigate({ to: '/clusters/$id', params: { id: cluster.id } })}
            disabled={!nodeId || !name || !image || !network || !sshKey}
          >
            <Plus />
            Создать ВМ
          </Button>
        </div>
      </div>
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