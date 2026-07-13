import { useMemo, useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { toast } from 'sonner';
import { ArrowLeft, Plus, Cube, Stack, BookOpen } from '@/shared/ui/icon';
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
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/shared/ui/primitives/empty';
import { useListClusters } from '@/features/clusters';

export const Route = createFileRoute('/k8s/new')({
  staticData: { crumb: 'New Kubernetes cluster' },
  component: CreateK8sPage,
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
      title="Create Kubernetes cluster"
      description="Managed K3s on the Plexor fleet — control plane, node pools, networking and storage are all yours to set."
      actions={
        <Button variant="ghost" nativeButton={false} render={<Link to="/" />}>
          <ArrowLeft />
          Back
        </Button>
      }
    >
      {/* Empty state: 0 clusters → Plexor isn't installed yet. */}
      {clusters.length === 0 && (
        <div className="py-8">
          <Empty data-od-id="k8s-new-no-cluster">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <Cube />
              </EmptyMedia>
              <EmptyTitle>Plexor control plane is not registered</EmptyTitle>
              <EmptyDescription>
                To create a Kubernetes cluster, you first need to install Plexor on a server. The
                installation sets up the control plane and registers it in the UI.
              </EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <Button render={<a href="https://plexor.dev/docs/install" target="_blank" rel="noreferrer" />}>
                <BookOpen />
                Installation docs
              </Button>
            </EmptyContent>
          </Empty>
        </div>
      )}

      {/* Empty state: clusters exist, 0 ready nodes → connect a node first. */}
      {clusters.length > 0 && readyNodes.length === 0 && (
        <div className="py-8">
          <Empty data-od-id="k8s-new-no-ready-nodes">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <Stack />
              </EmptyMedia>
              <EmptyTitle>No ready nodes</EmptyTitle>
              <EmptyDescription>
                Connect Plexor.NodeAgent to the cluster with a join token. After a heartbeat
                (≤ 2 min) the node appears here and you can host a Kubernetes cluster on it.
              </EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <Button nativeButton={false} render={<Link to="/clusters/$id" params={{ id: clusters[0]!.id }} />}>
                <ArrowLeft />
                Go to cluster
              </Button>
            </EmptyContent>
          </Empty>
        </div>
      )}

      {clusters.length > 0 && readyNodes.length > 0 && (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div className="min-w-0 space-y-3">
            {/* ─── Cluster basics ────────────────────────────────────── */}
            <Card data-od-id="k8s-basics">
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Cluster basics</CardTitle>
                <CardDescription>Name, Kubernetes version, and which Plexor fleet hosts it.</CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="Name" htmlFor="k8s-name" required help="Cluster name. Lowercase letters, digits and hyphens.">
                  <Input id="k8s-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. prod-k3s" />
                </FieldRow>
                <FieldRow label="Kubernetes version" htmlFor="k8s-version" required help="K3s distribution + Kubernetes minor version.">
                  <SimpleSelect id="k8s-version" value={version} onChange={setVersion} options={K8S_VERSIONS} />
                </FieldRow>
                <FieldRow label="Plexor cluster" htmlFor="k8s-fleet" required help="Which fleet hosts the cluster — nodes are provisioned from its ready hosts.">
                  <SimpleSelect
                    id="k8s-fleet"
                    value={plexorCluster}
                    onChange={setPlexorCluster}
                    options={clusters.map((c) => c.name)}
                    placeholder="Select a Plexor cluster"
                  />
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Control plane ─────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Control plane</CardTitle>
                <CardDescription>How the K3s API server and datastore are laid out.</CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="Mode" help="HA runs 3 control-plane nodes with embedded etcd quorum.">
                  <SegmentedControl aria-label="Control plane mode" value={cpMode} onValueChange={setCpMode} options={CP_MODE_OPTIONS} />
                </FieldRow>
                <FieldRow label="Datastore" help="Embedded etcd (self-managed quorum) or an external SQL datastore.">
                  <SegmentedControl aria-label="Datastore" value={datastore} onValueChange={setDatastore} options={DATASTORE_OPTIONS} />
                </FieldRow>
                <div className="pt-2">
                  <Disclosure summary="Advanced · API server">
                    <FieldRow label="Node placement" help="Let the scheduler place control-plane nodes automatically, or pin them to specific hosts.">
                      <SegmentedControl aria-label="Node placement" value={apiPlacement} onValueChange={setApiPlacement} options={API_PLACEMENT_OPTIONS} />
                      {apiPlacement === 'pin' && <p className="text-xs text-muted-foreground">Pin selection coming soon.</p>}
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Node pools ────────────────────────────────────────── */}
            <Card data-od-id="k8s-node-pools">
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Node pools</CardTitle>
                <CardDescription>Groups of identical nodes that span the fleet. Sizes are exact — self-hosted, not fixed cloud «flavors».</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="space-y-3">
                  <RepeatableRows
                    rows={pools}
                    onChange={setPools}
                    newRow={() => ({ name: '', count: 1, runtime: 'vm', vcpu: 2, ramBytes: SizeUtils.gibToBytes(4), diskBytes: SizeUtils.gibToBytes(40) })}
                    addLabel="Add node pool"
                    renderRow={(row, update) => (
                      <div className="space-y-2 rounded-md border border-border p-3">
                        <div className="flex flex-wrap items-center gap-2">
                          <Input value={row.name} onChange={(e) => update({ ...row, name: e.target.value })} placeholder="pool name" className="flex-1" />
                          <Stepper value={row.count} onValueChange={(n) => update({ ...row, count: n })} min={1} max={50} suffix="nodes" />
                          <SegmentedControl aria-label="Backing runtime" value={row.runtime} onValueChange={(v) => update({ ...row, runtime: v })} options={RUNTIME_OPTIONS} />
                        </div>
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="text-xs text-muted-foreground">per node</span>
                          <Stepper value={row.vcpu} onValueChange={(n) => update({ ...row, vcpu: n })} min={1} max={64} suffix="vCPU" />
                          <SizeField bytes={row.ramBytes} onValueChange={(b) => update({ ...row, ramBytes: b })} units={['GiB']} min={SizeUtils.gibToBytes(1)} max={SizeUtils.gibToBytes(512)} />
                          <SizeField bytes={row.diskBytes} onValueChange={(b) => update({ ...row, diskBytes: b })} units={['GiB', 'TiB']} min={SizeUtils.gibToBytes(10)} />
                        </div>
                      </div>
                    )}
                  />
                  <p className="text-xs text-muted-foreground">
                    <MonoNum>{totalNodes}</MonoNum> nodes · <MonoNum>{totalVcpu}</MonoNum> vCPU · <Size bytes={totalRamBytes} />
                  </p>
                </div>
              </CardContent>
            </Card>

            {/* ─── Networking ────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Networking</CardTitle>
                <CardDescription>CNI, ingress and the service load balancer. CIDRs live under «Advanced».</CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="CNI" htmlFor="k8s-cni" help="Pod network plugin. Flannel is the K3s default; Cilium/Calico for policy + eBPF.">
                  <SimpleSelect id="k8s-cni" value={cni} onChange={setCni} options={CNI_OPTIONS} />
                </FieldRow>
                <FieldRow label="Ingress" help="Bundled ingress controller for HTTP routing.">
                  <SegmentedControl aria-label="Ingress" value={ingress} onValueChange={setIngress} options={INGRESS_OPTIONS} />
                </FieldRow>
                <FieldRow label="Load balancer" help="How LoadBalancer Services get an address. ServiceLB (klipper) is the K3s default.">
                  <SegmentedControl aria-label="Load balancer" value={loadBalancer} onValueChange={setLoadBalancer} options={LB_OPTIONS} />
                </FieldRow>
                <div className="pt-2">
                  <Disclosure summary="Advanced · CIDRs">
                    <FieldRow label="Pod CIDR" htmlFor="k8s-pod-cidr" help="Address range pods are allocated from.">
                      <Input id="k8s-pod-cidr" value={podCidr} onChange={(e) => setPodCidr(e.target.value)} placeholder="10.42.0.0/16" className="w-56 font-mono" />
                    </FieldRow>
                    <FieldRow label="Service CIDR" htmlFor="k8s-svc-cidr" help="Address range ClusterIP Services are allocated from.">
                      <Input id="k8s-svc-cidr" value={serviceCidr} onChange={(e) => setServiceCidr(e.target.value)} placeholder="10.43.0.0/16" className="w-56 font-mono" />
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Storage ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Storage</CardTitle>
                <CardDescription>Default StorageClass for dynamic volumes. Backends come from the fleet's providers.</CardDescription>
              </CardHeader>
              <CardContent>
                <FieldRow label="Default StorageClass" htmlFor="k8s-storageclass" required help="Only backends the chosen fleet actually runs are offered.">
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
                <CardTitle className="text-sm">Add-ons</CardTitle>
                <CardDescription>Cluster components installed at bootstrap.</CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="metrics-server" htmlFor="k8s-metrics" help="Resource metrics API for `kubectl top` and the HPA.">
                  <Switch id="k8s-metrics" checked={metricsServer} onCheckedChange={setMetricsServer} />
                </FieldRow>
                <FieldRow label="cert-manager" htmlFor="k8s-certmgr" help="Automated TLS certificate issuance and renewal.">
                  <Switch id="k8s-certmgr" checked={certManager} onCheckedChange={setCertManager} />
                </FieldRow>
                <FieldRow label="Monitoring" htmlFor="k8s-monitoring" help="Prometheus + Grafana stack for metrics and dashboards.">
                  <Switch id="k8s-monitoring" checked={monitoring} onCheckedChange={setMonitoring} />
                </FieldRow>
                <FieldRow label="Kubernetes Dashboard" htmlFor="k8s-dashboard" help="Web UI for browsing and managing cluster workloads.">
                  <Switch id="k8s-dashboard" checked={dashboard} onCheckedChange={setDashboard} />
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Options ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Options</CardTitle>
                <CardDescription>Upgrade behavior, protection, labels.</CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="Auto-upgrade" htmlFor="k8s-autoupgrade" help="Follow the K3s release channel and upgrade nodes automatically.">
                  <Switch id="k8s-autoupgrade" checked={autoUpgrade} onCheckedChange={setAutoUpgrade} />
                </FieldRow>
                <FieldRow label="Protection" htmlFor="k8s-protect" help="Blocks accidental deletion until turned off.">
                  <Switch id="k8s-protect" checked={protection} onCheckedChange={setProtection} />
                </FieldRow>
                <FieldRow label="Labels" help="key=value pairs for grouping and search.">
                  <RepeatableRows
                    rows={labels}
                    onChange={setLabels}
                    newRow={() => ({ key: '', value: '' })}
                    addLabel="Add label"
                    renderRow={(row, update) => (
                      <div className="flex gap-2">
                        <Input value={row.key} onChange={(e) => update({ ...row, key: e.target.value })} placeholder="key" className="flex-1" />
                        <Input value={row.value} onChange={(e) => update({ ...row, value: e.target.value })} placeholder="value" className="flex-1" />
                      </div>
                    )}
                  />
                </FieldRow>
              </CardContent>
            </Card>

            <div className="flex items-center justify-between">
              <Button variant="outline" nativeButton={false} render={<Link to="/" />}>
                Cancel
              </Button>
              <Button onClick={handleCreate} disabled={!canCreate}>
                <Plus />
                Create cluster
              </Button>
            </div>
          </div>

          {/* ─── Sticky summary ──────────────────────────────────────── */}
          <SummaryPanel title="What will be deployed">
            <div>
              <SummaryRow label="Name">{name.trim() || '—'}</SummaryRow>
              <SummaryRow label="Version">{version}</SummaryRow>
              <SummaryRow label="Control plane">{controlPlaneSummary}</SummaryRow>
              <SummaryRow label="Nodes">
                <MonoNum>{totalNodes}</MonoNum> across {pools.length} pool(s)
              </SummaryRow>
              <SummaryRow label="Capacity">
                <MonoNum>{totalVcpu}</MonoNum> vCPU · <Size bytes={totalRamBytes} />
              </SummaryRow>
              <SummaryRow label="CNI">{cni}</SummaryRow>
              <SummaryRow label="Ingress">{ingressLabel}</SummaryRow>
              <SummaryRow label="StorageClass">{STORAGE_CLASS_LABELS[effStorageClass] ?? effStorageClass}</SummaryRow>
            </div>
            <div className="mt-2 flex flex-wrap gap-1.5 border-t border-border pt-2">
              {metricsServer && <Badge variant="outline">metrics-server</Badge>}
              {certManager && <Badge variant="outline">cert-manager</Badge>}
              {monitoring && <Badge variant="outline">monitoring</Badge>}
              {dashboard && <Badge variant="outline">dashboard</Badge>}
              {autoUpgrade && <Badge variant="outline">auto-upgrade</Badge>}
              {protection && <Badge variant="outline">protected</Badge>}
            </div>
          </SummaryPanel>
        </div>
      )}
    </PageTemplate>
  );
}
