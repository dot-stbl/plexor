import { useMemo, useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { Add, ArrowBack, DeployedCode, Stacks } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Switch } from '@/shared/ui/primitives/switch';
import { Badge } from '@/shared/ui/primitives/badge';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Progress } from '@/shared/ui/primitives/progress';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { FieldRow } from '@/shared/ui/primitives/field-row';
import { SegmentedControl } from '@/shared/ui/primitives/segmented-control';
import { Stepper } from '@/shared/ui/primitives/stepper';
import { SizeField } from '@/shared/ui/primitives/size-field';
import { PasswordInput } from '@/shared/ui/primitives/password-input';
import { RepeatableRows } from '@/shared/ui/primitives/repeatable-rows';
import { Disclosure } from '@/shared/ui/primitives/disclosure';
import { SimpleSelect } from '@/shared/ui/primitives/simple-select';
import { Size, SizeUtils } from '@/shared/ui/primitives/size';
import { SummaryPanel, SummaryRow } from '@/shared/ui/primitives/summary-panel';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { useListClusters } from '@/features/clusters';
import type { NodeStatus } from '@/features/clusters';

export const Route = createFileRoute('/lxc/new')({
  staticData: { crumb: 'New container' },
  component: CreateLxcPage,
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

const TEMPLATES = [
  'ubuntu-24.04-standard',
  'debian-12-standard',
  'alpine-3.20-default',
  'rocky-9-default',
  'fedora-40-default',
];
const NETWORKS = ['prod-vpc', 'staging-vpc'];
const SSH_KEYS = ['alex@workstation', 'deploy@ci'];

// Storage backends → labels. Offered pools are DERIVED from the selected node's
// providers (self-hosted: can't place a rootfs on a backend the node lacks).
const STORAGE_LABELS: Record<string, string> = {
  'local-lvm': 'Local LVM-Thin',
  'local-zfs': 'Local ZFS',
  'ceph-rbd': 'Ceph RBD (replicated)',
  nfs: 'NFS share',
};

const AUTH_OPTIONS = [
  { value: 'key', label: 'SSH key' },
  { value: 'password', label: 'Set password' },
];
const IP_OPTIONS = [
  { value: 'dhcp', label: 'DHCP' },
  { value: 'static', label: 'Static' },
];

/**
 * Create LXC container — self-hosted depth (Proxmox pct model). Unlike a full
 * VM: no firmware/machine type; instead unprivileged/nesting/features, a rootfs
 * on a node-provided storage pool, and veth networking. Basics stay visible;
 * deep knobs live in per-card «Advanced». LXC needs a node with an lxd/lxc
 * runtime provider — placement is filtered to those. Full-width, cards left +
 * sticky SummaryPanel right (like /vms/new, /k8s/new).
 */
function CreateLxcPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { clusters } = useListClusters();

  const allNodes = clusters.flatMap((c) => c.nodes);
  // LXC needs a container runtime on the node (self-hosted: comes from providers).
  const lxcNodes = allNodes.filter(
    (n) => n.status === 'ready' && (n.spec.providers.includes('lxd') || n.spec.providers.includes('lxc')),
  );

  // Placement
  const [nodeId, setNodeId] = useState<string>('');
  // Template
  const [template, setTemplate] = useState<string>(TEMPLATES[0]!);
  const [unprivileged, setUnprivileged] = useState(true);
  // Compute
  const [cores, setCores] = useState(2);
  const [ramBytes, setRamBytes] = useState<number>(SizeUtils.gibToBytes(2));
  const [swapBytes, setSwapBytes] = useState<number>(SizeUtils.gibToBytes(1));
  const [cpuLimit, setCpuLimit] = useState(0);
  const [cpuUnits, setCpuUnits] = useState(1024);
  // Root filesystem
  const [rootPool, setRootPool] = useState<string>('');
  const [rootBytes, setRootBytes] = useState<number>(SizeUtils.gibToBytes(8));
  const [mounts, setMounts] = useState<{ sizeGib: number; pool: string; path: string }[]>([]);
  // Network
  const [vpc, setVpc] = useState<string>(NETWORKS[0]!);
  const [ipMode, setIpMode] = useState<string>('dhcp');
  const [ipAddress, setIpAddress] = useState('');
  const [gateway, setGateway] = useState('');
  const [nic, setNic] = useState('eth0');
  const [vlan, setVlan] = useState('');
  const [firewall, setFirewall] = useState(true);
  const [rateLimit, setRateLimit] = useState(0);
  // Features
  const [nesting, setNesting] = useState(false);
  const [keyctl, setKeyctl] = useState(false);
  const [fuse, setFuse] = useState(false);
  // Access
  const [name, setName] = useState('');
  const [authMode, setAuthMode] = useState<string>('key');
  const [sshKey, setSshKey] = useState<string>(SSH_KEYS[0]!);
  const [password, setPassword] = useState('');
  const [dns, setDns] = useState('1.1.1.1 8.8.8.8');
  // Options
  const [startAfterCreate, setStartAfterCreate] = useState(true);
  const [startOnBoot, setStartOnBoot] = useState(false);
  const [startOrder, setStartOrder] = useState(0);
  const [protection, setProtection] = useState(false);
  const [labels, setLabels] = useState<{ key: string; value: string }[]>([]);

  const selectedNode = allNodes.find((n) => n.id === nodeId);
  const selectedCluster = clusters.find((c) => c.nodes.some((n) => n.id === nodeId));

  const storagePools = useMemo(() => {
    const pools = new Set<string>(['local-lvm']);
    selectedNode?.spec.providers.forEach((p) => {
      if (STORAGE_LABELS[p]) pools.add(p);
    });
    return [...pools];
  }, [selectedNode]);
  const effRootPool = storagePools.includes(rootPool) ? rootPool : (storagePools[0] ?? 'local-lvm');

  const mountBytes = mounts.reduce((sum, m) => sum + SizeUtils.gibToBytes(m.sizeGib || 0), 0);
  const usedDiskGib = Math.round((rootBytes + mountBytes) / 1024 ** 3);
  const usedRamGib = Math.round(ramBytes / 1024 ** 3);

  const effectiveName = name.trim() || 'ct-1';
  const staticOk = ipMode === 'dhcp' || (ipAddress.trim() !== '' && gateway.trim() !== '');
  const canCreate = Boolean(nodeId && name.trim() && template && effRootPool && vpc && staticOk);

  const handleCreate = () => {
    if (!canCreate) return;
    toast(`Creating container ${effectiveName}`, {
      description: `${template} · ${cores} cores / ${SizeUtils.format(ramBytes)} · ${SizeUtils.format(rootBytes)} on ${STORAGE_LABELS[effRootPool]} · ${selectedNode?.hostname ?? '—'}`,
    });
    void navigate({ to: '/lxc' });
  };

  return (
    <PageTemplate
      data-od-id="lxc-new"
      width="full"
      title={t('lxc.new.title')}
      description={t('lxc.new.form.pageDescription')}
      actions={
        <Button variant="ghost" nativeButton={false} render={<Link to="/lxc" />}>
          <ArrowBack />
          {t('common.back')}
        </Button>
      }
    >
      {/* Empty state: 0 clusters → Plexor isn't installed yet. */}
      {clusters.length === 0 && (
        <div className="py-8">
          <EmptyState
            data-od-id="lxc-new-no-cluster"
            icon={DeployedCode}
            title={t('lxc.new.noCluster.title')}
            description={t('lxc.new.noCluster.description')}
            docs={[{ href: 'https://plexor.dev/docs/install', label: t('common.installationDocs') }]}
          />
        </div>
      )}

      {/* Empty state: clusters exist, but no LXC-capable ready node. */}
      {clusters.length > 0 && lxcNodes.length === 0 && (
        <div className="py-8">
          <EmptyState
            data-od-id="lxc-new-no-ready-nodes"
            icon={Stacks}
            title={t('lxc.new.noReady.title')}
            description={t('lxc.new.noReady.description')}
            action={
              <Button nativeButton={false} render={<Link to="/clusters/$id" params={{ id: clusters[0]!.id }} />}>
                <ArrowBack />
                {t('common.goToCluster')}
              </Button>
            }
          />
        </div>
      )}

      {clusters.length > 0 && lxcNodes.length > 0 && (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div className="min-w-0 space-y-3">
            {/* ─── Placement ─────────────────────────────────────────── */}
            <Card data-od-id="lxc-placement">
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('lxc.new.form.placement.title')}</CardTitle>
                <CardDescription>{t('lxc.new.form.placement.description')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('lxc.new.form.placement.nodeLabel')} htmlFor="ct-node" required help={t('lxc.new.form.placement.nodeHelp')}>
                  <SimpleSelect
                    id="ct-node"
                    value={nodeId}
                    onChange={setNodeId}
                    options={lxcNodes.map((n) => n.id)}
                    render={(id) => {
                      const n = lxcNodes.find((x) => x.id === id);
                      return n ? `${n.hostname} · ${n.role === 'control' ? 'control-plane' : 'compute'}` : id;
                    }}
                    placeholder={t('lxc.new.form.placement.nodePlaceholder')}
                  />
                </FieldRow>

                {selectedNode && (
                  <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                    <CapacityCell label="CPU" used={cores} total={selectedNode.spec.vcpu} unit="cores" />
                    <CapacityCell label="RAM" used={usedRamGib} total={selectedNode.spec.ramGb} unit="GiB" />
                    <CapacityCell label="Disk" used={usedDiskGib} total={selectedNode.spec.diskGb} unit="GiB" />
                  </div>
                )}

                {selectedNode && (
                  <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
                    <StatusPill variant={STATUS_VARIANT[selectedNode.status]} size="sm">
                      {STATUS_LABEL[selectedNode.status]}
                    </StatusPill>
                    <span>ISO v{selectedNode.isoVersion}</span>
                    {selectedCluster && (
                      <span>
                        cluster <MonoNum>{selectedCluster.name}</MonoNum>
                      </span>
                    )}
                    <span>
                      providers: <span className="text-foreground">{selectedNode.spec.providers.join(', ')}</span>
                    </span>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* ─── Template ──────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('lxc.new.form.template.title')}</CardTitle>
                <CardDescription>{t('lxc.new.form.template.description')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('lxc.new.form.template.osLabel')} htmlFor="ct-template" required>
                  <SimpleSelect id="ct-template" value={template} onChange={setTemplate} options={TEMPLATES} />
                </FieldRow>
                <FieldRow
                  label={t('lxc.new.form.template.unprivilegedLabel')}
                  htmlFor="ct-unpriv"
                  help={t('lxc.new.form.template.unprivilegedHelp')}
                >
                  <Switch id="ct-unpriv" checked={unprivileged} onCheckedChange={setUnprivileged} />
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Compute ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('lxc.new.form.compute.title')}</CardTitle>
                <CardDescription>{t('lxc.new.form.compute.description')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('lxc.new.form.compute.coresLabel')} required help={t('lxc.new.form.compute.coresHelp')}>
                  <Stepper value={cores} onValueChange={setCores} min={1} max={64} suffix="cores" />
                </FieldRow>
                <FieldRow label={t('lxc.new.form.compute.memoryLabel')} htmlFor="ct-ram" required help={t('lxc.new.form.compute.memoryHelp')}>
                  <SizeField id="ct-ram" bytes={ramBytes} onValueChange={setRamBytes} units={['MiB', 'GiB']} min={128 * 1024 ** 2} max={SizeUtils.gibToBytes(512)} />
                </FieldRow>
                <FieldRow label={t('lxc.new.form.compute.swapLabel')} htmlFor="ct-swap" help={t('lxc.new.form.compute.swapHelp')}>
                  <SizeField id="ct-swap" bytes={swapBytes} onValueChange={setSwapBytes} units={['MiB', 'GiB']} min={0} max={SizeUtils.gibToBytes(64)} />
                </FieldRow>
                <div>
                  <Disclosure variant="card" summary={t('lxc.new.form.compute.advanced')}>
                    <FieldRow label={t('lxc.new.form.compute.cpuLimitLabel')} help={t('lxc.new.form.compute.cpuLimitHelp')}>
                      <Stepper value={cpuLimit} onValueChange={setCpuLimit} min={0} max={cores} suffix="cores" />
                    </FieldRow>
                    <FieldRow label={t('lxc.new.form.compute.cpuUnitsLabel')} help={t('lxc.new.form.compute.cpuUnitsHelp')}>
                      <Stepper value={cpuUnits} onValueChange={setCpuUnits} min={8} max={100000} step={8} />
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Root filesystem ───────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('lxc.new.form.rootfs.title')}</CardTitle>
                <CardDescription>
                  {selectedNode ? t('lxc.new.form.rootfs.description') : t('lxc.new.form.rootfs.descriptionNoNode')}
                </CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('lxc.new.form.rootfs.poolLabel')} htmlFor="ct-pool" required help={t('lxc.new.form.rootfs.poolHelp')}>
                  <SimpleSelect id="ct-pool" value={effRootPool} onChange={setRootPool} options={storagePools} render={(p) => STORAGE_LABELS[p] ?? p} />
                </FieldRow>
                <FieldRow label={t('lxc.new.form.rootfs.sizeLabel')} htmlFor="ct-root" required help={t('lxc.new.form.rootfs.sizeHelp')}>
                  <SizeField id="ct-root" bytes={rootBytes} onValueChange={setRootBytes} units={['GiB', 'TiB']} min={SizeUtils.gibToBytes(1)} max={SizeUtils.gibToBytes(16384)} />
                </FieldRow>
                <div>
                  <Disclosure variant="card" summary={t('lxc.new.form.rootfs.advanced')}>
                    <FieldRow label={t('lxc.new.form.rootfs.mountsLabel')} description={t('lxc.new.form.rootfs.mountsDescription')}>
                      <RepeatableRows
                        rows={mounts}
                        onChange={setMounts}
                        newRow={() => ({ sizeGib: 50, pool: effRootPool, path: '/mnt/data' })}
                        addLabel={t('lxc.new.form.rootfs.addMount')}
                        renderRow={(row, update) => (
                          <div className="flex flex-wrap items-center gap-2">
                            <Stepper value={row.sizeGib} onValueChange={(n) => update({ ...row, sizeGib: n })} min={1} max={16384} suffix="GiB" />
                            <SimpleSelect value={row.pool} onChange={(p) => update({ ...row, pool: p })} options={storagePools} render={(p) => STORAGE_LABELS[p] ?? p} className="w-40" />
                            <Input value={row.path} onChange={(e) => update({ ...row, path: e.target.value })} placeholder="/mnt/data" className="flex-1" />
                          </div>
                        )}
                      />
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Network ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('lxc.new.form.network.title')}</CardTitle>
                <CardDescription>{t('lxc.new.form.network.description')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('lxc.new.form.network.vpcLabel')} htmlFor="ct-vpc" required>
                  <SimpleSelect id="ct-vpc" value={vpc} onChange={setVpc} options={NETWORKS} />
                </FieldRow>
                <FieldRow label={t('lxc.new.form.network.ipLabel')} help={t('lxc.new.form.network.ipHelp')}>
                  <SegmentedControl aria-label={t('lxc.new.form.network.ipAria')} value={ipMode} onValueChange={setIpMode} options={IP_OPTIONS} />
                  {ipMode === 'static' && (
                    <div className="flex flex-wrap gap-2">
                      <Input value={ipAddress} onChange={(e) => setIpAddress(e.target.value)} placeholder="10.0.0.10/24" className="flex-1" />
                      <Input value={gateway} onChange={(e) => setGateway(e.target.value)} placeholder="gateway 10.0.0.1" className="flex-1" />
                    </div>
                  )}
                </FieldRow>
                <div>
                  <Disclosure variant="card" summary={t('lxc.new.form.network.advanced')}>
                    <FieldRow label={t('lxc.new.form.network.nicLabel')} htmlFor="ct-nic" help={t('lxc.new.form.network.nicHelp')}>
                      <Input id="ct-nic" value={nic} onChange={(e) => setNic(e.target.value)} placeholder="eth0" className="w-32" />
                    </FieldRow>
                    <FieldRow label={t('lxc.new.form.network.vlanLabel')} htmlFor="ct-vlan" help={t('lxc.new.form.network.vlanHelp')}>
                      <Input id="ct-vlan" value={vlan} onChange={(e) => setVlan(e.target.value)} placeholder="e.g. 100" inputMode="numeric" className="w-32" />
                    </FieldRow>
                    <FieldRow label={t('lxc.new.form.network.firewallLabel')} help={t('lxc.new.form.network.firewallHelp')}>
                      <Switch checked={firewall} onCheckedChange={setFirewall} />
                    </FieldRow>
                    <FieldRow label={t('lxc.new.form.network.rateLabel')} help={t('lxc.new.form.network.rateHelp')}>
                      <Stepper value={rateLimit} onValueChange={setRateLimit} min={0} max={10000} suffix="MB/s" />
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Features ──────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('lxc.new.form.features.title')}</CardTitle>
                <CardDescription>{t('lxc.new.form.features.description')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('lxc.new.form.features.nestingLabel')} help={t('lxc.new.form.features.nestingHelp')}>
                  <Switch checked={nesting} onCheckedChange={setNesting} />
                </FieldRow>
                <FieldRow label={t('lxc.new.form.features.keyctlLabel')} help={t('lxc.new.form.features.keyctlHelp')}>
                  <Switch checked={keyctl} onCheckedChange={setKeyctl} />
                </FieldRow>
                <FieldRow label={t('lxc.new.form.features.fuseLabel')} help={t('lxc.new.form.features.fuseHelp')}>
                  <Switch checked={fuse} onCheckedChange={setFuse} />
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Access ────────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('lxc.new.form.access.title')}</CardTitle>
                <CardDescription>{t('lxc.new.form.access.description')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('lxc.new.form.access.nameLabel')} htmlFor="ct-name" required help={t('lxc.new.form.access.nameHelp')}>
                  <Input id="ct-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. app-01" />
                </FieldRow>
                <FieldRow label={t('lxc.new.form.access.authLabel')} help={t('lxc.new.form.access.authHelp')}>
                  <SegmentedControl aria-label={t('lxc.new.form.access.authLabel')} value={authMode} onValueChange={setAuthMode} options={AUTH_OPTIONS} />
                  {authMode === 'key' ? (
                    <SimpleSelect value={sshKey} onChange={setSshKey} options={SSH_KEYS} />
                  ) : (
                    <PasswordInput value={password} onChange={(e) => setPassword(e.target.value)} placeholder={t('lxc.new.form.access.passwordPlaceholder')} />
                  )}
                </FieldRow>
                <FieldRow label={t('lxc.new.form.access.dnsLabel')} htmlFor="ct-dns" help={t('lxc.new.form.access.dnsHelp')}>
                  <Input id="ct-dns" value={dns} onChange={(e) => setDns(e.target.value)} placeholder="1.1.1.1 8.8.8.8" />
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Options ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('lxc.new.form.options.title')}</CardTitle>
                <CardDescription>{t('lxc.new.form.options.description')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('lxc.new.form.options.startLabel')} htmlFor="ct-start">
                  <Switch id="ct-start" checked={startAfterCreate} onCheckedChange={setStartAfterCreate} />
                </FieldRow>
                <FieldRow label={t('lxc.new.form.options.onBootLabel')} htmlFor="ct-onboot" help={t('lxc.new.form.options.onBootHelp')}>
                  <Switch id="ct-onboot" checked={startOnBoot} onCheckedChange={setStartOnBoot} />
                </FieldRow>
                <FieldRow label={t('lxc.new.form.options.orderLabel')} htmlFor="ct-order" help={t('lxc.new.form.options.orderHelp')}>
                  <Stepper value={startOrder} onValueChange={setStartOrder} min={0} max={999} />
                </FieldRow>
                <FieldRow label={t('lxc.new.form.options.protectionLabel')} htmlFor="ct-protect" help={t('lxc.new.form.options.protectionHelp')}>
                  <Switch id="ct-protect" checked={protection} onCheckedChange={setProtection} />
                </FieldRow>
                <FieldRow label={t('lxc.new.form.options.labelsLabel')} help={t('lxc.new.form.options.labelsHelp')}>
                  <RepeatableRows
                    rows={labels}
                    onChange={setLabels}
                    newRow={() => ({ key: '', value: '' })}
                    addLabel={t('lxc.new.form.options.addLabel')}
                    renderRow={(row, update) => (
                      <div className="flex gap-2">
                        <Input value={row.key} onChange={(e) => update({ ...row, key: e.target.value })} placeholder={t('lxc.new.form.options.labelKeyPlaceholder')} className="flex-1" />
                        <Input value={row.value} onChange={(e) => update({ ...row, value: e.target.value })} placeholder={t('lxc.new.form.options.labelValuePlaceholder')} className="flex-1" />
                      </div>
                    )}
                  />
                </FieldRow>
              </CardContent>
            </Card>

            <div className="flex items-center justify-between">
              <Button variant="outline" nativeButton={false} render={<Link to="/lxc" />}>
                {t('common.cancel')}
              </Button>
              <Button onClick={handleCreate} disabled={!canCreate}>
                <Add />
                {t('lxc.new.create')}
              </Button>
            </div>
          </div>

          {/* ─── Sticky summary ──────────────────────────────────────── */}
          <SummaryPanel
            title={t('lxc.new.form.summary.title')}
            footer={
              <div className="flex w-full flex-wrap gap-1.5">
                {nesting && <Badge variant="outline">nesting</Badge>}
                {startAfterCreate && <Badge variant="outline">autostart</Badge>}
                {startOnBoot && <Badge variant="outline">on-boot</Badge>}
                {firewall && <Badge variant="outline">firewall</Badge>}
                {protection && <Badge variant="outline">protected</Badge>}
              </div>
            }
          >
            <div>
              <SummaryRow label={t('lxc.new.form.summary.template')}>{template}</SummaryRow>
              <SummaryRow label={t('lxc.new.form.summary.placement')}>
                {selectedNode ? selectedNode.hostname : '—'}
                {selectedCluster ? ` · ${selectedCluster.name}` : ''}
              </SummaryRow>
              <SummaryRow label={t('lxc.new.form.summary.type')}>{unprivileged ? 'unprivileged' : 'privileged'}</SummaryRow>
              <SummaryRow label={t('lxc.new.form.summary.cpu')}>
                <MonoNum>{cores}</MonoNum> cores
              </SummaryRow>
              <SummaryRow label={t('lxc.new.form.summary.memory')}>
                <Size bytes={ramBytes} />
              </SummaryRow>
              <SummaryRow label={t('lxc.new.form.summary.swap')}>
                <Size bytes={swapBytes} />
              </SummaryRow>
              <SummaryRow label={t('lxc.new.form.summary.rootfs')}>
                <Size bytes={rootBytes} /> <span className="text-muted-foreground">· {STORAGE_LABELS[effRootPool] ?? effRootPool}</span>
              </SummaryRow>
              {mounts.length > 0 && (
                <SummaryRow label={t('lxc.new.form.summary.mounts')}>
                  <MonoNum>{mounts.length}</MonoNum> · <Size bytes={mountBytes} />
                </SummaryRow>
              )}
              <SummaryRow label={t('lxc.new.form.summary.network')}>
                {vpc} · {ipMode === 'dhcp' ? 'DHCP' : ipAddress || 'static'}
              </SummaryRow>
            </div>
          </SummaryPanel>
        </div>
      )}
    </PageTemplate>
  );
}

function CapacityCell({ label, used, total, unit }: { label: string; used: number; total: number; unit: string }) {
  const pct = total === 0 ? 0 : Math.round((used / total) * 100);
  const over = pct > 100;
  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between text-[11px] font-medium text-muted-foreground uppercase tracking-[0.06em]">
        <span>{label}</span>
        <span className={over ? 'text-destructive' : undefined}>{pct}%</span>
      </div>
      <div className="flex items-baseline gap-1">
        <MonoNum>{used}</MonoNum>
        <span className="text-muted-foreground">/</span>
        <MonoNum muted>{total}</MonoNum>
        <span className="text-muted-foreground text-xs">{unit}</span>
      </div>
      <Progress value={Math.min(pct, 100)} />
    </div>
  );
}
