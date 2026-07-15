import { useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import {
  Add,
  ArrowBack,
  DeployedCode,
  Stacks
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Progress } from '@/shared/ui/primitives/progress';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { Badge } from '@/shared/ui/primitives/badge';

import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/shared/ui/primitives/field';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { SummaryPanel, SummaryRow } from '@/shared/ui/primitives/summary-panel';
import { useListClusters } from '@/features/clusters';
import type { NodeStatus } from '@/features/clusters';

export const Route = createFileRoute('/vms/new')({
  staticData: { crumb: 'New VM' },
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
  const { t } = useTranslation();
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
    <PageTemplate
      data-od-id="vms-new"
      title={t('vms.new.title')}
      width="full"
      description={t('vms.new.description')}
      actions={
        <Button variant="ghost" nativeButton={false} render={<Link to="/vms" />}>
          <ArrowBack />
          {t('common.back')}
        </Button>
      }
    >

      {/* Empty state: 0 clusters → user hasn't installed Plexor yet. */}
      {clusters.length === 0 && (
        <EmptyState
          data-od-id="vms-new-no-cluster"
          icon={DeployedCode}
          title={t('vms.new.noCluster.title')}
          description={t('vms.new.noCluster.description')}
          docs={[{ href: 'https://plexor.dev/docs/install', label: t('common.installationDocs') }]}
        />
      )}

      {/* Empty state: clusters exist, 0 ready nodes → user needs to connect a node. */}
      {clusters.length > 0 && readyNodes.length === 0 && (
        <EmptyState
          data-od-id="vms-new-no-ready-nodes"
          icon={Stacks}
          title={t('vms.new.noReady.title')}
          description={t('vms.new.noReady.description')}
          action={
            <Button nativeButton={false} render={<Link to="/clusters/$id" params={{ id: clusters[0]!.id }} />}>
              <ArrowBack />
              {t('common.goToCluster')}
            </Button>
          }
        />
      )}

      {clusters.length > 0 && readyNodes.length > 0 && (
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <div className="min-w-0 space-y-3">
        {/* Node context — which Plexor.NodeAgent hosts the VM. */}
        <Card data-od-id="vm-node-context" className="gap-0 p-0">
          <CardHeader className="gap-0.5 border-b border-border p-4">
            <div className="flex items-center justify-between gap-2">
              <div className="space-y-0.5">
                <CardTitle className="text-sm">{t('vms.new.node')}</CardTitle>
                <CardDescription>
                  {t('vms.new.nodeDescription', { badge: 'ready' })}
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
                    <SelectValue placeholder={t('vms.new.nodePlaceholder')} />
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
            <CardTitle className="text-sm">{t('vms.new.params')}</CardTitle>
            <CardDescription>{t('vms.new.paramsDescription')}</CardDescription>
          </CardHeader>
          <CardContent className="p-4">
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="create-vm-name">{t('vms.new.name')}</FieldLabel>
                <Input
                  id="create-vm-name"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  placeholder={t('vms.new.namePlaceholder')}
                />
                <FieldDescription>
                  {t('vms.new.nameDescription')}
                </FieldDescription>
              </Field>

              <Field>
                <FieldLabel htmlFor="create-vm-image">{t('vms.new.image')}</FieldLabel>
                <Select
                  items={IMAGES.map((i) => ({ value: i, label: i }))}
                  value={image}
                  onValueChange={(value) => setImage(value ?? '')}
                >
                  <SelectTrigger id="create-vm-image">
                    <SelectValue placeholder={t('vms.new.imagePlaceholder')} />
                  </SelectTrigger>
                  <SelectContent>
                    {IMAGES.map((i) => (
                      <SelectItem key={i} value={i}>
                        {i}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldDescription>{t('vms.new.imageDescription')}</FieldDescription>
              </Field>

              <Field>
                <FieldLabel htmlFor="create-vm-network">{t('vms.new.network')}</FieldLabel>
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
                <FieldLabel htmlFor="create-vm-ssh">{t('vms.new.sshKey')}</FieldLabel>
                <Select
                  items={SSH_KEYS.map((k) => ({ value: k, label: k }))}
                  value={sshKey}
                  onValueChange={(value) => setSshKey(value ?? '')}
                >
                  <SelectTrigger id="create-vm-ssh">
                    <SelectValue placeholder={t('vms.new.sshKeyPlaceholder')} />
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
              {t('common.cancel')}
            </Button>
            <Button
              onClick={() => navigate({ to: '/vms' })}
              disabled={!nodeId || !name || !image || !network || !sshKey}
            >
              <Add />
              {t('vms.new.create')}
            </Button>
          </div>
        </div>

        {/* ─── Sticky summary ──────────────────────────────────────── */}
        <SummaryPanel title={t('vms.new.summary')}>
          <div>
            <SummaryRow label="Name">{name.trim() || '—'}</SummaryRow>
            <SummaryRow label="Node">
              {selectedNode ? <MonoNum>{selectedNode.hostname}</MonoNum> : '—'}
            </SummaryRow>
            <SummaryRow label="Image">{image || '—'}</SummaryRow>
            <SummaryRow label="Network">{network || '—'}</SummaryRow>
            <SummaryRow label="SSH key">{sshKey || '—'}</SummaryRow>
          </div>
          {selectedNode && (
            <div className="mt-2 flex flex-wrap gap-1.5 border-t border-border pt-2">
              <Badge variant="outline">{estVmUsage.cpu} vCPU</Badge>
              <Badge variant="outline">{estVmUsage.ramGb} GB RAM</Badge>
              <Badge variant="outline">{estVmUsage.diskGb} GB disk</Badge>
            </div>
          )}
        </SummaryPanel>
      </div>
      )}
    </PageTemplate>
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