import { useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { ArrowLeft, Plus } from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Progress } from '@/shared/ui/primitives/progress';
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
import { useGetCluster, useListClusters, clusterFlavors, clusterUtilizationPct } from '@/features/clusters';

export const Route = createFileRoute('/clusters/$id/vms/new')({
  component: CreateVmPage,
});

/**
 * Create VM page — Step 1 (the rest of the wizard is a separate plan).
 *
 * Layout: Cluster Context Card on top (which cluster this VM lands on,
 * current capacity, ability to switch clusters), then VM-specific
 * fields (image, network, SSH key, name, labels). Zone + flavor are
 * NOT here — those are cluster concerns, already decided by the
 * cluster context.
 */
function CreateVmPage() {
  const navigate = useNavigate();
  const { id: clusterId } = Route.useParams();
  const { cluster } = useGetCluster(clusterId);
  const { clusters } = useListClusters();

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

  const util = clusterUtilizationPct(cluster);

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
        description="VM-specific настройки. Cluster, зона и флейвор уже определены выбранным кластером."
        actions={
          <Button variant="ghost" render={<Link to="/clusters/$id" params={{ id: cluster.id }} />}>
            <ArrowLeft />
            Назад к кластеру
          </Button>
        }
      />

      <div className="mx-auto w-full max-w-3xl space-y-3 px-6 py-6 lg:px-8">
        {/* Cluster Context — which cluster the VM will be created on.
            Switch cluster via a Select that re-points to the right route. */}
        <Card data-od-id="vm-cluster-context" className="gap-0 p-0">
          <CardHeader className="gap-0.5 border-b border-border p-4">
            <div className="flex items-center justify-between gap-2">
              <div className="space-y-0.5">
                <CardTitle className="text-sm">Кластер</CardTitle>
                <CardDescription>VM будет создана на этом кластере.</CardDescription>
              </div>
              <Select
                items={clusters.map((c) => ({
                  value: c.id,
                  label: `${c.name} (${c.zone})`,
                }))}
                value={cluster.id}
                onValueChange={(value) => {
                  if (value && value !== cluster.id) {
                    void navigate({ to: '/clusters/$id/vms/new', params: { id: value } });
                  }
                }}
              >
                <SelectTrigger className="min-w-[200px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {clusters.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name} ({c.zone})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </CardHeader>
          <CardContent className="grid grid-cols-1 gap-3 p-4 md:grid-cols-3">
            <CapacityCell label="CPU" used={cluster.usedCpu} total={cluster.totalCpu} pct={util.cpu} unit="vCPU" />
            <CapacityCell label="RAM" used={cluster.usedRamGb} total={cluster.totalRamGb} pct={util.ram} unit="GB" />
            <div className="space-y-1">
              <div className="text-[11px] font-medium text-muted-foreground uppercase tracking-[0.06em]">
                Flavors
              </div>
              <div className="flex flex-wrap gap-1.5">
                {clusterFlavors(cluster).map((f) => (
                  <span
                    key={f.id}
                    className="rounded-md border border-border bg-background px-1.5 py-0.5 text-[10px] font-mono"
                  >
                    {f.id}
                  </span>
                ))}
              </div>
              <p className="text-[10px] text-muted-foreground">
                Флейвор VM будет выбран позже (Screen 03) на основе этих опций.
              </p>
            </div>
          </CardContent>
        </Card>

        {/* VM-specific config — the part that uniquely describes a VM. */}
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
            disabled={!name || !image || !network || !sshKey}
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
  pct,
  unit,
}: {
  label: string;
  used: number;
  total: number;
  pct: number;
  unit: string;
}) {
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

