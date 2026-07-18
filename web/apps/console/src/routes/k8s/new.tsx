import { useMemo, useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  Add,
  ArrowBack,
  DeployedCode,
  Stacks
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Switch } from '@/shared/ui/primitives/switch';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Badge } from '@/shared/ui/primitives/badge';
import { FieldRow } from '@/shared/ui/primitives/field-row';
import { SegmentedControl } from '@/shared/ui/primitives/segmented-control';
import { Stepper } from '@/shared/ui/primitives/stepper';
import { SizeField } from '@/shared/ui/primitives/size-field';
import { RepeatableRows } from '@/shared/ui/primitives/repeatable-rows';
import { Disclosure } from '@/shared/ui/primitives/disclosure';
import { SimpleSelect } from '@/shared/ui/primitives/simple-select';
import { Size, SizeUtils } from '@/shared/ui/primitives/size';
import { SummaryPanel, SummaryRow } from '@/shared/ui/primitives/summary-panel';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { useListClusters } from '@/features/clusters';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/k8s/new')({
  staticData: { crumb: 'New Kubernetes cluster' },
  component: CreateK8sPage,
  ...routeHead('New Kubernetes cluster'),
});

// A node pool = N identical nodes backed by one runtime, spanning the fleet.
interface NodePool {
  name: string;
  count: number;
  runtime: string;
  vcpu: number;
  ramBytes: number;
  diskBytes: number;
}

const K8S_VERSIONS = ['v1.31.1+k3s1', 'v1.30.5+k3s1', 'v1.29.9+k3s1'];
const CNI_OPTIONS = ['Flannel (VXLAN)', 'Cilium (eBPF)', 'Calico'];

const CP_MODE_OPTIONS = [
  { value: 'single', label: 'Single node' },
  { value: 'ha', label: 'HA (3 nodes)' },
];
const DATASTORE_OPTIONS = [
  { value: 'etcd', label: 'Embedded etcd' },
  { value: 'sql', label: 'External SQL' },
];
const API_PLACEMENT_OPTIONS = [
  { value: 'auto', label: 'Automatic' },
  { value: 'pin', label: 'Pin to nodes' },
];
const RUNTIME_OPTIONS = [
  { value: 'vm', label: 'VM' },
  { value: 'lxc', label: 'LXC' },
  { value: 'bare', label: 'Bare node' },
];
const INGRESS_OPTIONS = [
  { value: 'traefik', label: 'Traefik' },
  { value: 'ingress-nginx', label: 'ingress-nginx' },
  { value: 'none', label: 'None' },
];
const LB_OPTIONS = [
  { value: 'servicelb', label: 'ServiceLB (klipper)' },
  { value: 'metallb', label: 'MetalLB' },
  { value: 'none', label: 'None' },
];

// StorageClass backends → friendly labels. The offered list is DERIVED from the
// fleet's providers (self-hosted: only offer backends the cluster actually runs).
const STORAGE_CLASS_LABELS: Record<string, string> = {
  'local-path': 'Local path (hostPath)',
  'ceph-rbd': 'Ceph RBD',
  longhorn: 'Longhorn',
};

/**
 * Create Kubernetes cluster page — Managed K3s on the Plexor fleet. Unlike a
 * single-VM create, this provisions a *cluster*: control-plane mode + node
 * pools that span the fleet, CNI/ingress/LB, StorageClass and add-ons. Basics
 * stay visible; deep knobs (API server, CIDRs) live in per-card «Advanced».
 * Full-width, cards left + sticky SummaryPanel right (like /vms/new).
 */
function CreateK8sPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { clusters } = useListClusters();

  const allNodes = clusters.flatMap((c) => c.nodes);
  const readyNodes = allNodes.filter((n) => n.status === 'ready');

  // Basics
  const [name, setName] = useState('');
  const [version, setVersion] = useState<string>(K8S_VERSIONS[0]!);
  const [plexorCluster, setPlexorCluster] = useState<string>(clusters[0]?.name ?? '');

  // Control plane
  const [cpMode, setCpMode] = useState<string>('single');
  const [datastore, setDatastore] = useState<string>('etcd');
  const [apiPlacement, setApiPlacement] = useState<string>('auto');

  // Node pools
  const [pools, setPools] = useState<NodePool[]>([
    { name: 'workers', count: 3, runtime: 'vm', vcpu: 4, ramBytes: SizeUtils.gibToBytes(8), diskBytes: SizeUtils.gibToBytes(80) },
  ]);

  // Networking
  const [cni, setCni] = useState<string>(CNI_OPTIONS[0]!);
  const [ingress, setIngress] = useState<string>('traefik');
  const [loadBalancer, setLoadBalancer] = useState<string>('servicelb');
  const [podCidr, setPodCidr] = useState('10.42.0.0/16');
  const [serviceCidr, setServiceCidr] = useState('10.43.0.0/16');

  // Storage
  const [storageClass, setStorageClass] = useState<string>('local-path');

  // Add-ons
  const [metricsServer, setMetricsServer] = useState(true);
  const [certManager, setCertManager] = useState(false);
  const [monitoring, setMonitoring] = useState(false);
  const [dashboard, setDashboard] = useState(false);

  // Options
  const [autoUpgrade, setAutoUpgrade] = useState(false);
  const [protection, setProtection] = useState(false);
  const [labels, setLabels] = useState<{ key: string; value: string }[]>([]);

  // Storage backends offered by the chosen fleet's ready nodes (self-hosted).
  const storageClasses = useMemo(() => {
    const selected = clusters.find((c) => c.name === plexorCluster);
    const providers = new Set((selected?.nodes ?? []).filter((n) => n.status === 'ready').flatMap((n) => n.spec.providers));
    return ['local-path', ...(providers.has('ceph-rbd') ? ['ceph-rbd'] : []), 'longhorn'];
  }, [clusters, plexorCluster]);
  const effStorageClass = storageClasses.includes(storageClass) ? storageClass : storageClasses[0]!;

  // Fleet-wide totals across all pools.
  const totalNodes = pools.reduce((sum, p) => sum + p.count, 0);
  const totalVcpu = pools.reduce((sum, p) => sum + p.count * p.vcpu, 0);
  const totalRamBytes = pools.reduce((sum, p) => sum + p.count * p.ramBytes, 0);

  const ingressLabel = INGRESS_OPTIONS.find((o) => o.value === ingress)?.label ?? ingress;
  const controlPlaneSummary = cpMode === 'ha' ? `HA · ${datastore === 'etcd' ? 'etcd' : 'SQL'}` : 'Single';

  const canCreate =
    Boolean(name.trim()) &&
    Boolean(version) &&
    Boolean(plexorCluster) &&
    pools.length > 0 &&
    pools.every((p) => p.count > 0 && p.name.trim() !== '');

  const handleCreate = () => {
    if (!canCreate) return;
    toast(`Creating Kubernetes cluster ${name}`, {
      description: `${version} · ${totalNodes} nodes · ${SizeUtils.format(totalRamBytes)}`,
    });
    void navigate({ to: '/' });
  };

  return (
    <PageTemplate
      data-od-id="k8s-new"
      width="full"
      title={t('k8s.new.title')}
      description={t('k8s.new.description')}
      actions={
        <Button variant="ghost" nativeButton={false} render={<Link to="/" />}>
          <ArrowBack />
          {t('common.back')}
        </Button>
      }
    >
      {/* Empty state: 0 clusters → Plexor isn't installed yet. */}
      {clusters.length === 0 && (
        <div className="py-8">
          <EmptyState
            data-od-id="k8s-new-no-cluster"
            icon={DeployedCode}
            title={t('k8s.new.noCluster.title')}
            description={t('k8s.new.noCluster.description')}
            docs={[{ href: 'https://plexor.dev/docs/install', label: 'Installation docs' }]}
          />
        </div>
      )}

      {/* Empty state: clusters exist, 0 ready nodes → connect a node first. */}
      {clusters.length > 0 && readyNodes.length === 0 && (
        <div className="py-8">
          <EmptyState
            data-od-id="k8s-new-no-ready-nodes"
            icon={Stacks}
            title={t('k8s.new.noReady.title')}
            description={t('k8s.new.noReady.description')}
            action={
              <Button nativeButton={false} render={<Link to="/clusters/$id" params={{ id: clusters[0]!.id }} />}>
                <ArrowBack />
                {t('common.goToCluster')}
              </Button>
            }
          />
        </div>
      )}

      {clusters.length > 0 && readyNodes.length > 0 && (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div className="min-w-0 space-y-3">
            {/* ─── Cluster basics ────────────────────────────────────── */}
            <Card data-od-id="k8s-basics">
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('k8s.new.basics')}</CardTitle>
                <CardDescription>{t('k8s.new.form.basicsDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('k8s.new.form.nameLabel')} htmlFor="k8s-name" required help={t('k8s.new.form.nameHelp')}>
                  <Input id="k8s-name" value={name} onChange={(e) => setName(e.target.value)} placeholder={t('k8s.new.form.namePlaceholder')} />
                </FieldRow>
                <FieldRow label={t('k8s.new.form.versionLabel')} htmlFor="k8s-version" required help={t('k8s.new.form.versionHelp')}>
                  <SimpleSelect id="k8s-version" value={version} onChange={setVersion} options={K8S_VERSIONS} />
                </FieldRow>
                <FieldRow label={t('k8s.new.form.fleetLabel')} htmlFor="k8s-fleet" required help={t('k8s.new.form.fleetHelp')}>
                  <SimpleSelect
                    id="k8s-fleet"
                    value={plexorCluster}
                    onChange={setPlexorCluster}
                    options={clusters.map((c) => c.name)}
                    placeholder={t('k8s.new.form.fleetPlaceholder')}
                  />
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Control plane ─────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('k8s.new.controlPlane')}</CardTitle>
                <CardDescription>{t('k8s.new.form.controlPlaneDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('k8s.new.form.modeLabel')} help={t('k8s.new.form.modeHelp')}>
                  <SegmentedControl aria-label="Control plane mode" value={cpMode} onValueChange={setCpMode} options={CP_MODE_OPTIONS} />
                </FieldRow>
                <FieldRow label={t('k8s.new.form.datastoreLabel')} help={t('k8s.new.form.datastoreHelp')}>
                  <SegmentedControl aria-label="Datastore" value={datastore} onValueChange={setDatastore} options={DATASTORE_OPTIONS} />
                </FieldRow>
                <div>
                  <Disclosure variant="card" summary={t('k8s.new.form.advancedApiServer')}>
                    <FieldRow label={t('k8s.new.form.nodePlacementLabel')} help={t('k8s.new.form.nodePlacementHelp')}>
                      <SegmentedControl aria-label="Node placement" value={apiPlacement} onValueChange={setApiPlacement} options={API_PLACEMENT_OPTIONS} />
                      {apiPlacement === 'pin' && <p className="text-xs text-muted-foreground">{t('k8s.new.form.pinSelectionComingSoon')}</p>}
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Node pools ────────────────────────────────────────── */}
            <Card data-od-id="k8s-node-pools">
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('k8s.new.nodePools')}</CardTitle>
                <CardDescription>{t('k8s.new.form.nodePoolsDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <div className="space-y-3">
                  <RepeatableRows
                    rows={pools}
                    onChange={setPools}
                    newRow={() => ({ name: '', count: 1, runtime: 'vm', vcpu: 2, ramBytes: SizeUtils.gibToBytes(4), diskBytes: SizeUtils.gibToBytes(40) })}
                    addLabel={t('k8s.new.form.addNodePool')}
                    renderRow={(row, update) => (
                      <div className="space-y-2 rounded-md border border-border p-3">
                        <div className="flex flex-wrap items-center gap-2">
                          <Input value={row.name} onChange={(e) => update({ ...row, name: e.target.value })} placeholder={t('k8s.new.form.poolNamePlaceholder')} className="flex-1" />
                          <Stepper value={row.count} onValueChange={(n) => update({ ...row, count: n })} min={1} max={50} suffix="nodes" />
                          <SegmentedControl aria-label="Backing runtime" value={row.runtime} onValueChange={(v) => update({ ...row, runtime: v })} options={RUNTIME_OPTIONS} />
                        </div>
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="text-xs text-muted-foreground">{t('k8s.new.form.perNode')}</span>
                          <Stepper value={row.vcpu} onValueChange={(n) => update({ ...row, vcpu: n })} min={1} max={64} suffix="vCPU" />
                          <SizeField bytes={row.ramBytes} onValueChange={(b) => update({ ...row, ramBytes: b })} units={['GiB']} min={SizeUtils.gibToBytes(1)} max={SizeUtils.gibToBytes(512)} />
                          <SizeField bytes={row.diskBytes} onValueChange={(b) => update({ ...row, diskBytes: b })} units={['GiB', 'TiB']} min={SizeUtils.gibToBytes(10)} />
                        </div>
                      </div>
                    )}
                  />
                  <p className="text-xs text-muted-foreground">
                    <MonoNum>{totalNodes}</MonoNum> {t('k8s.new.form.totalsNodes')} · <MonoNum>{totalVcpu}</MonoNum> {t('k8s.new.form.totalsVcpu')} · <Size bytes={totalRamBytes} />
                  </p>
                </div>
              </CardContent>
            </Card>

            {/* ─── Networking ────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('k8s.new.networking')}</CardTitle>
                <CardDescription>{t('k8s.new.form.networkingDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('k8s.new.form.cniLabel')} htmlFor="k8s-cni" help={t('k8s.new.form.cniHelp')}>
                  <SimpleSelect id="k8s-cni" value={cni} onChange={setCni} options={CNI_OPTIONS} />
                </FieldRow>
                <FieldRow label={t('k8s.new.form.ingressLabel')} help={t('k8s.new.form.ingressHelp')}>
                  <SegmentedControl aria-label="Ingress" value={ingress} onValueChange={setIngress} options={INGRESS_OPTIONS} />
                </FieldRow>
                <FieldRow label={t('k8s.new.form.loadBalancerLabel')} help={t('k8s.new.form.loadBalancerHelp')}>
                  <SegmentedControl aria-label="Load balancer" value={loadBalancer} onValueChange={setLoadBalancer} options={LB_OPTIONS} />
                </FieldRow>
                <div>
                  <Disclosure variant="card" summary={t('k8s.new.form.advancedCidrs')}>
                    <FieldRow label={t('k8s.new.form.podCidrLabel')} htmlFor="k8s-pod-cidr" help={t('k8s.new.form.podCidrHelp')}>
                      <Input id="k8s-pod-cidr" value={podCidr} onChange={(e) => setPodCidr(e.target.value)} placeholder="10.42.0.0/16" className="w-56 font-mono" />
                    </FieldRow>
                    <FieldRow label={t('k8s.new.form.serviceCidrLabel')} htmlFor="k8s-svc-cidr" help={t('k8s.new.form.serviceCidrHelp')}>
                      <Input id="k8s-svc-cidr" value={serviceCidr} onChange={(e) => setServiceCidr(e.target.value)} placeholder="10.43.0.0/16" className="w-56 font-mono" />
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Storage ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('k8s.new.storage')}</CardTitle>
                <CardDescription>{t('k8s.new.form.storageDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('k8s.new.form.storageClassLabel')} htmlFor="k8s-storageclass" required help={t('k8s.new.form.storageClassHelp')}>
                  <SimpleSelect
                    id="k8s-storageclass"
                    value={effStorageClass}
                    onChange={setStorageClass}
                    options={storageClasses}
                    render={(s) => STORAGE_CLASS_LABELS[s] ?? s}
                  />
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Add-ons ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('k8s.new.addons')}</CardTitle>
                <CardDescription>{t('k8s.new.form.addonsDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('k8s.new.form.metricsServerLabel')} htmlFor="k8s-metrics" help={t('k8s.new.form.metricsServerHelp')}>
                  <Switch id="k8s-metrics" checked={metricsServer} onCheckedChange={setMetricsServer} />
                </FieldRow>
                <FieldRow label={t('k8s.new.form.certManagerLabel')} htmlFor="k8s-certmgr" help={t('k8s.new.form.certManagerHelp')}>
                  <Switch id="k8s-certmgr" checked={certManager} onCheckedChange={setCertManager} />
                </FieldRow>
                <FieldRow label={t('k8s.new.form.monitoringLabel')} htmlFor="k8s-monitoring" help={t('k8s.new.form.monitoringHelp')}>
                  <Switch id="k8s-monitoring" checked={monitoring} onCheckedChange={setMonitoring} />
                </FieldRow>
                <FieldRow label={t('k8s.new.form.dashboardLabel')} htmlFor="k8s-dashboard" help={t('k8s.new.form.dashboardHelp')}>
                  <Switch id="k8s-dashboard" checked={dashboard} onCheckedChange={setDashboard} />
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Options ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('k8s.new.options')}</CardTitle>
                <CardDescription>{t('k8s.new.form.optionsDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('k8s.new.form.autoUpgradeLabel')} htmlFor="k8s-autoupgrade" help={t('k8s.new.form.autoUpgradeHelp')}>
                  <Switch id="k8s-autoupgrade" checked={autoUpgrade} onCheckedChange={setAutoUpgrade} />
                </FieldRow>
                <FieldRow label={t('k8s.new.form.protectionLabel')} htmlFor="k8s-protect" help={t('k8s.new.form.protectionHelp')}>
                  <Switch id="k8s-protect" checked={protection} onCheckedChange={setProtection} />
                </FieldRow>
                <FieldRow label={t('k8s.new.form.labelsLabel')} help={t('k8s.new.form.labelsHelp')}>
                  <RepeatableRows
                    rows={labels}
                    onChange={setLabels}
                    newRow={() => ({ key: '', value: '' })}
                    addLabel={t('k8s.new.form.addLabel')}
                    renderRow={(row, update) => (
                      <div className="flex gap-2">
                        <Input value={row.key} onChange={(e) => update({ ...row, key: e.target.value })} placeholder={t('k8s.new.form.labelKeyPlaceholder')} className="flex-1" />
                        <Input value={row.value} onChange={(e) => update({ ...row, value: e.target.value })} placeholder={t('k8s.new.form.labelValuePlaceholder')} className="flex-1" />
                      </div>
                    )}
                  />
                </FieldRow>
              </CardContent>
            </Card>

            <div className="flex items-center justify-between">
              <Button variant="outline" nativeButton={false} render={<Link to="/" />}>
                {t('common.cancel')}
              </Button>
              <Button onClick={handleCreate} disabled={!canCreate}>
                <Add />
                {t('k8s.new.create')}
              </Button>
            </div>
          </div>

          {/* ─── Sticky summary ──────────────────────────────────────── */}
          <SummaryPanel
            title={t('k8s.new.summary')}
            footer={
              <div className="flex w-full flex-wrap gap-1.5">
                {metricsServer && <Badge variant="outline">metrics-server</Badge>}
                {certManager && <Badge variant="outline">cert-manager</Badge>}
                {monitoring && <Badge variant="outline">monitoring</Badge>}
                {dashboard && <Badge variant="outline">dashboard</Badge>}
                {autoUpgrade && <Badge variant="outline">auto-upgrade</Badge>}
                {protection && <Badge variant="outline">protected</Badge>}
              </div>
            }
          >
            <div>
              <SummaryRow label={t('k8s.new.form.summaryName')}>{name.trim() || '—'}</SummaryRow>
              <SummaryRow label={t('k8s.new.form.summaryVersion')}>{version}</SummaryRow>
              <SummaryRow label={t('k8s.new.form.summaryControlPlane')}>{controlPlaneSummary}</SummaryRow>
              <SummaryRow label={t('k8s.new.form.summaryNodes')}>
                <MonoNum>{totalNodes}</MonoNum> {t('k8s.new.form.poolsCount', { count: pools.length })}
              </SummaryRow>
              <SummaryRow label={t('k8s.new.form.summaryCapacity')}>
                <MonoNum>{totalVcpu}</MonoNum> vCPU · <Size bytes={totalRamBytes} />
              </SummaryRow>
              <SummaryRow label={t('k8s.new.form.summaryCni')}>{cni}</SummaryRow>
              <SummaryRow label={t('k8s.new.form.summaryIngress')}>{ingressLabel}</SummaryRow>
              <SummaryRow label={t('k8s.new.form.summaryStorageClass')}>{STORAGE_CLASS_LABELS[effStorageClass] ?? effStorageClass}</SummaryRow>
            </div>
          </SummaryPanel>
        </div>
      )}
    </PageTemplate>
  );
}
