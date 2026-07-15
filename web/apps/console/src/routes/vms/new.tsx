import { useMemo, useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { Add, ArrowBack, DeployedCode, Stacks } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Textarea } from '@/shared/ui/primitives/textarea';
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
import { TechIcon } from '@/shared/ui/primitives/tech-icon';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { useListClusters } from '@/features/clusters';
import type { NodeStatus } from '@/features/clusters';
import { listImages } from '@/features/images';

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

// Hypervisor knobs a managed cloud hides but a self-hosted host must expose.
const MACHINE_TYPES = [
  { value: 'q35', label: 'q35 (modern PCIe)' },
  { value: 'i440fx', label: 'i440fx (legacy)' },
];
const FIRMWARE = [
  { value: 'uefi', label: 'UEFI (OVMF)' },
  { value: 'seabios', label: 'SeaBIOS' },
];
const CPU_TYPES = ['host', 'x86-64-v3', 'kvm64', 'qemu64', 'max'];
const DISK_BUSES = [
  { value: 'virtio-scsi', label: 'VirtIO SCSI' },
  { value: 'virtio-blk', label: 'VirtIO Block' },
  { value: 'sata', label: 'SATA' },
];
const CACHE_MODES = ['none', 'writeback', 'writethrough', 'directsync'];
const NIC_MODELS = [
  { value: 'virtio', label: 'VirtIO (paravirtualized)' },
  { value: 'e1000', label: 'Intel E1000' },
  { value: 'rtl8139', label: 'Realtek RTL8139' },
  { value: 'vmxnet3', label: 'VMware vmxnet3' },
];

// Storage backends → labels. Offered pools DERIVE from the selected node's
// providers (self-hosted: can't place a disk on a backend the node lacks).
const STORAGE_LABELS: Record<string, string> = {
  'local-lvm': 'Local LVM-Thin',
  'local-zfs': 'Local ZFS',
  'ceph-rbd': 'Ceph RBD (replicated)',
  nfs: 'NFS share',
};

const NETWORKS = ['prod-vpc', 'staging-vpc'];
const SSH_KEYS = ['alex@workstation', 'deploy@ci'];
const AUTH_OPTIONS = [
  { value: 'key', label: 'SSH key' },
  { value: 'password', label: 'Set password' },
];
const IP_OPTIONS = [
  { value: 'dhcp', label: 'DHCP' },
  { value: 'static', label: 'Static' },
];

/**
 * Create VM page — self-hosted depth (Proxmox-like). Unlike a managed cloud
 * (which hides placement/hypervisor/storage/network), Plexor surfaces them:
 * node placement + machine/firmware/CPU type, storage pool + bus + cache,
 * NIC model + VLAN + static IP, cloud-init, guest agent. Basics stay visible;
 * deep knobs live in per-card «Advanced». Full-width, cards left + sticky
 * SummaryPanel right (like /lxc/new, /k8s/new).
 */
function CreateVmPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { clusters } = useListClusters();
  const images = useMemo(() => listImages().filter((i) => i.status === 'ready'), []);

  const allNodes = clusters.flatMap((c) => c.nodes);
  const readyNodes = allNodes.filter((n) => n.status === 'ready');

  // Placement
  const [nodeId, setNodeId] = useState<string>('');
  const [machineType, setMachineType] = useState<string>('q35');
  const [firmware, setFirmware] = useState<string>('uefi');
  const [cpuType, setCpuType] = useState<string>('host');
  // Identity + image
  const [name, setName] = useState('');
  const [imageId, setImageId] = useState<string>(images[0]?.id ?? '');
  // Compute — Proxmox model: sockets × cores = vCPU
  const [sockets, setSockets] = useState(1);
  const [cores, setCores] = useState(2);
  const [ramBytes, setRamBytes] = useState<number>(SizeUtils.gibToBytes(4));
  const [numa, setNuma] = useState(false);
  const [ballooning, setBallooning] = useState(true);
  const [minRamBytes, setMinRamBytes] = useState<number>(SizeUtils.gibToBytes(1));
  // Storage
  const [bootPool, setBootPool] = useState<string>('');
  const [bootDiskBytes, setBootDiskBytes] = useState<number>(SizeUtils.gibToBytes(40));
  const [bootBus, setBootBus] = useState<string>('virtio-scsi');
  const [cache, setCache] = useState<string>('none');
  const [ssdEmulation, setSsdEmulation] = useState(false);
  const [discard, setDiscard] = useState(true);
  const [ioThread, setIoThread] = useState(true);
  const [extraDisks, setExtraDisks] = useState<{ sizeGib: number; pool: string }[]>([]);
  // Network
  const [vpc, setVpc] = useState<string>(NETWORKS[0]!);
  const [ipMode, setIpMode] = useState<string>('dhcp');
  const [ipAddress, setIpAddress] = useState('');
  const [gateway, setGateway] = useState('');
  const [nicModel, setNicModel] = useState<string>('virtio');
  const [vlan, setVlan] = useState('');
  const [firewall, setFirewall] = useState(true);
  const [rateLimit, setRateLimit] = useState(0);
  // Access + cloud-init
  const [username, setUsername] = useState('plexor');
  const [authMode, setAuthMode] = useState<string>('key');
  const [sshKey, setSshKey] = useState<string>(SSH_KEYS[0]!);
  const [password, setPassword] = useState('');
  const [cloudInit, setCloudInit] = useState('');
  const [dns, setDns] = useState('1.1.1.1 8.8.8.8');
  // Options
  const [startAfterCreate, setStartAfterCreate] = useState(true);
  const [startOnBoot, setStartOnBoot] = useState(false);
  const [guestAgent, setGuestAgent] = useState(true);
  const [protection, setProtection] = useState(false);
  const [labels, setLabels] = useState<{ key: string; value: string }[]>([]);

  const selectedNode = allNodes.find((n) => n.id === nodeId);
  const selectedCluster = clusters.find((c) => c.nodes.some((n) => n.id === nodeId));
  const selectedImage = images.find((i) => i.id === imageId);

  const storagePools = useMemo(() => {
    const pools = new Set<string>(['local-lvm']);
    selectedNode?.spec.providers.forEach((p) => {
      if (STORAGE_LABELS[p]) pools.add(p);
    });
    return [...pools];
  }, [selectedNode]);
  const effBootPool = storagePools.includes(bootPool) ? bootPool : (storagePools[0] ?? 'local-lvm');

  const vcpu = sockets * cores;
  const extraDiskBytes = extraDisks.reduce((sum, d) => sum + SizeUtils.gibToBytes(d.sizeGib || 0), 0);
  const usedDiskGib = Math.round((bootDiskBytes + extraDiskBytes) / 1024 ** 3);
  const usedRamGib = Math.round(ramBytes / 1024 ** 3);

  const effectiveName = name.trim() || 'vm-1';
  const staticOk = ipMode === 'dhcp' || (ipAddress.trim() !== '' && gateway.trim() !== '');
  const canCreate = Boolean(nodeId && name.trim() && imageId && effBootPool && vpc && staticOk);

  const handleCreate = () => {
    if (!canCreate) return;
    toast(`Creating VM ${effectiveName}`, {
      description: `${selectedImage?.name ?? 'image'} · ${vcpu} vCPU / ${SizeUtils.format(ramBytes)} · ${SizeUtils.format(bootDiskBytes)} on ${STORAGE_LABELS[effBootPool]} · ${selectedNode?.hostname ?? '—'}`,
    });
    void navigate({ to: '/vms' });
  };

  return (
    <PageTemplate
      data-od-id="vms-new"
      width="full"
      title={t('vms.new.title')}
      description={t('vms.new.form.pageDescription')}
      actions={
        <Button variant="ghost" nativeButton={false} render={<Link to="/vms" />}>
          <ArrowBack />
          {t('common.back')}
        </Button>
      }
    >
      {/* Empty state: 0 clusters → Plexor isn't installed yet. */}
      {clusters.length === 0 && (
        <div className="py-8">
          <EmptyState
            data-od-id="vms-new-no-cluster"
            icon={DeployedCode}
            title={t('vms.new.noCluster.title')}
            description={t('vms.new.noCluster.description')}
            docs={[{ href: 'https://plexor.dev/docs/install', label: t('common.installationDocs') }]}
          />
        </div>
      )}

      {/* Empty state: clusters exist, 0 ready nodes → connect a node first. */}
      {clusters.length > 0 && readyNodes.length === 0 && (
        <div className="py-8">
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
        </div>
      )}

      {clusters.length > 0 && readyNodes.length > 0 && (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div className="min-w-0 space-y-3">
            {/* ─── Placement ─────────────────────────────────────────── */}
            <Card data-od-id="vm-placement">
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('vms.new.form.placement')}</CardTitle>
                <CardDescription>{t('vms.new.form.placementDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('vms.new.node')} htmlFor="vm-node" required help={t('vms.new.form.nodeHelp')}>
                  <SimpleSelect
                    id="vm-node"
                    value={nodeId}
                    onChange={setNodeId}
                    options={readyNodes.map((n) => n.id)}
                    render={(id) => {
                      const n = readyNodes.find((x) => x.id === id);
                      return n ? `${n.hostname} · ${n.role === 'control' ? 'control-plane' : 'compute'}` : id;
                    }}
                    placeholder={t('vms.new.nodePlaceholder')}
                  />
                </FieldRow>

                {selectedNode && (
                  <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                    <CapacityCell label="CPU" used={vcpu} total={selectedNode.spec.vcpu} unit="vCPU" />
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

                <div>
                  <Disclosure variant="card" summary={t('vms.new.form.hypervisorAdvanced')}>
                    <FieldRow label={t('vms.new.form.machineType')} help={t('vms.new.form.machineTypeHelp')}>
                      <SegmentedControl aria-label="Machine type" value={machineType} onValueChange={setMachineType} options={MACHINE_TYPES} />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.firmware')} help={t('vms.new.form.firmwareHelp')}>
                      <SegmentedControl aria-label="Firmware" value={firmware} onValueChange={setFirmware} options={FIRMWARE} />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.cpuType')} htmlFor="vm-cputype" help={t('vms.new.form.cpuTypeHelp')}>
                      <SimpleSelect id="vm-cputype" value={cpuType} onChange={setCpuType} options={CPU_TYPES} />
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── OS image ──────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('vms.new.image')}</CardTitle>
                <CardDescription>{t('vms.new.form.imageDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('vms.new.image')} htmlFor="vm-image" required>
                  <SimpleSelect
                    id="vm-image"
                    value={imageId}
                    onChange={setImageId}
                    options={images.map((i) => i.id)}
                    render={(id) => images.find((i) => i.id === id)?.name ?? id}
                    placeholder={t('vms.new.imagePlaceholder')}
                  />
                  {selectedImage && (
                    <div className="flex flex-wrap items-center gap-2">
                      <TechIcon slug={selectedImage.techSlug ?? ''} className="size-4" />
                      <Badge variant="outline">{`${selectedImage.os} ${selectedImage.version}`}</Badge>
                      <Badge variant="outline" className="font-mono">{selectedImage.arch}</Badge>
                      <span className="text-xs text-muted-foreground">
                        image <Size bytes={selectedImage.sizeBytes} /> · min disk <Size bytes={selectedImage.minDiskBytes} />
                      </span>
                    </div>
                  )}
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Compute ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('vms.new.form.compute')}</CardTitle>
                <CardDescription>{t('vms.new.form.computeDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('vms.new.form.cpu')} required help={t('vms.new.form.cpuHelp')}>
                  <div className="flex flex-wrap items-center gap-2">
                    <Stepper value={sockets} onValueChange={setSockets} min={1} max={4} suffix="sock" />
                    <span className="text-muted-foreground">×</span>
                    <Stepper value={cores} onValueChange={setCores} min={1} max={64} suffix="cores" />
                    <span className="text-sm text-muted-foreground">
                      = <MonoNum>{vcpu}</MonoNum> vCPU
                    </span>
                  </div>
                </FieldRow>
                <FieldRow label={t('vms.new.form.memory')} htmlFor="vm-ram" required help={t('vms.new.form.memoryHelp')}>
                  <SizeField id="vm-ram" bytes={ramBytes} onValueChange={setRamBytes} units={['MiB', 'GiB']} min={SizeUtils.gibToBytes(1)} max={SizeUtils.gibToBytes(1024)} />
                </FieldRow>
                <div>
                  <Disclosure variant="card" summary={t('vms.new.form.computeAdvanced')}>
                    <FieldRow label={t('vms.new.form.cpuType')} htmlFor="vm-cputype2" help={t('vms.new.form.cpuTypeHelp2')}>
                      <SimpleSelect id="vm-cputype2" value={cpuType} onChange={setCpuType} options={CPU_TYPES} />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.numa')} help={t('vms.new.form.numaHelp')}>
                      <Switch checked={numa} onCheckedChange={setNuma} />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.ballooning')} help={t('vms.new.form.ballooningHelp')}>
                      <Switch checked={ballooning} onCheckedChange={setBallooning} />
                    </FieldRow>
                    {ballooning && (
                      <FieldRow label={t('vms.new.form.minMemory')} htmlFor="vm-minram" help={t('vms.new.form.minMemoryHelp')}>
                        <SizeField id="vm-minram" bytes={minRamBytes} onValueChange={setMinRamBytes} units={['MiB', 'GiB']} min={SizeUtils.gibToBytes(1)} max={ramBytes} />
                      </FieldRow>
                    )}
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Storage ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('vms.new.form.storage')}</CardTitle>
                <CardDescription>
                  {t('vms.new.form.storageDescription')}{selectedNode ? '' : t('vms.new.form.storageSelectNodeHint')}.
                </CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('vms.new.form.bootDiskPool')} htmlFor="vm-pool" required help={t('vms.new.form.bootDiskPoolHelp')}>
                  <SimpleSelect id="vm-pool" value={effBootPool} onChange={setBootPool} options={storagePools} render={(p) => STORAGE_LABELS[p] ?? p} />
                </FieldRow>
                <FieldRow label={t('vms.new.form.bootDiskSize')} htmlFor="vm-disk" required help={t('vms.new.form.bootDiskSizeHelp')}>
                  <SizeField
                    id="vm-disk"
                    bytes={bootDiskBytes}
                    onValueChange={setBootDiskBytes}
                    units={['GiB', 'TiB']}
                    min={selectedImage?.minDiskBytes ?? SizeUtils.gibToBytes(8)}
                    max={SizeUtils.gibToBytes(16384)}
                  />
                </FieldRow>
                <FieldRow label={t('vms.new.form.bus')} help={t('vms.new.form.busHelp')}>
                  <SegmentedControl aria-label="Disk bus" value={bootBus} onValueChange={setBootBus} options={DISK_BUSES} />
                </FieldRow>
                <div>
                  <Disclosure variant="card" summary={t('vms.new.form.storageAdvanced')}>
                    <FieldRow label={t('vms.new.form.cacheMode')} htmlFor="vm-cache" help={t('vms.new.form.cacheModeHelp')}>
                      <SimpleSelect id="vm-cache" value={cache} onChange={setCache} options={CACHE_MODES} />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.ssdEmulation')} help={t('vms.new.form.ssdEmulationHelp')}>
                      <Switch checked={ssdEmulation} onCheckedChange={setSsdEmulation} />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.discardTrim')} help={t('vms.new.form.discardTrimHelp')}>
                      <Switch checked={discard} onCheckedChange={setDiscard} />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.ioThread')} help={t('vms.new.form.ioThreadHelp')}>
                      <Switch checked={ioThread} onCheckedChange={setIoThread} />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.extraDisks')} description={t('vms.new.form.extraDisksDescription')}>
                      <RepeatableRows
                        rows={extraDisks}
                        onChange={setExtraDisks}
                        newRow={() => ({ sizeGib: 50, pool: effBootPool })}
                        addLabel={t('vms.new.form.addDisk')}
                        renderRow={(row, update) => (
                          <div className="flex flex-wrap items-center gap-2">
                            <Stepper value={row.sizeGib} onValueChange={(n) => update({ ...row, sizeGib: n })} min={1} max={16384} suffix="GiB" />
                            <SimpleSelect value={row.pool} onChange={(p) => update({ ...row, pool: p })} options={storagePools} render={(p) => STORAGE_LABELS[p] ?? p} className="w-40" />
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
                <CardTitle className="text-sm">{t('vms.new.network')}</CardTitle>
                <CardDescription>{t('vms.new.form.networkDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('vms.new.form.vpcSubnet')} htmlFor="vm-vpc" required>
                  <SimpleSelect id="vm-vpc" value={vpc} onChange={setVpc} options={NETWORKS} />
                </FieldRow>
                <FieldRow label={t('vms.new.form.ipAddress')} help={t('vms.new.form.ipAddressHelp')}>
                  <SegmentedControl aria-label="IP assignment" value={ipMode} onValueChange={setIpMode} options={IP_OPTIONS} />
                  {ipMode === 'static' && (
                    <div className="flex flex-wrap gap-2">
                      <Input value={ipAddress} onChange={(e) => setIpAddress(e.target.value)} placeholder="10.0.0.10/24" className="flex-1" />
                      <Input value={gateway} onChange={(e) => setGateway(e.target.value)} placeholder="gateway 10.0.0.1" className="flex-1" />
                    </div>
                  )}
                </FieldRow>
                <div>
                  <Disclosure variant="card" summary={t('vms.new.form.networkAdvanced')}>
                    <FieldRow label={t('vms.new.form.nicModel')} htmlFor="vm-nic" help={t('vms.new.form.nicModelHelp')}>
                      <SimpleSelect id="vm-nic" value={nicModel} onChange={setNicModel} options={NIC_MODELS.map((m) => m.value)} render={(v) => NIC_MODELS.find((m) => m.value === v)?.label ?? v} />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.vlanTag')} htmlFor="vm-vlan" help={t('vms.new.form.vlanTagHelp')}>
                      <Input id="vm-vlan" value={vlan} onChange={(e) => setVlan(e.target.value)} placeholder="e.g. 100" inputMode="numeric" className="w-32" />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.firewall')} help={t('vms.new.form.firewallHelp')}>
                      <Switch checked={firewall} onCheckedChange={setFirewall} />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.rateLimit')} help={t('vms.new.form.rateLimitHelp')}>
                      <Stepper value={rateLimit} onValueChange={setRateLimit} min={0} max={10000} suffix="MB/s" />
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Access & cloud-init ───────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('vms.new.form.access')}</CardTitle>
                <CardDescription>{t('vms.new.form.accessDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('vms.new.name')} htmlFor="vm-name" required help={t('vms.new.nameDescription')}>
                  <Input id="vm-name" value={name} onChange={(e) => setName(e.target.value)} placeholder={t('vms.new.namePlaceholder')} />
                </FieldRow>
                <FieldRow label={t('vms.new.form.user')} htmlFor="vm-user" help={t('vms.new.form.userHelp')}>
                  <Input id="vm-user" value={username} onChange={(e) => setUsername(e.target.value)} placeholder="plexor" className="w-56" />
                </FieldRow>
                <FieldRow label={t('vms.new.form.authentication')} help={t('vms.new.form.authenticationHelp')}>
                  <SegmentedControl aria-label="Authentication" value={authMode} onValueChange={setAuthMode} options={AUTH_OPTIONS} />
                  {authMode === 'key' ? (
                    <SimpleSelect value={sshKey} onChange={setSshKey} options={SSH_KEYS} />
                  ) : (
                    <PasswordInput value={password} onChange={(e) => setPassword(e.target.value)} placeholder={t('vms.new.form.passwordPlaceholder')} />
                  )}
                </FieldRow>
                <div>
                  <Disclosure variant="card" summary={t('vms.new.form.accessAdvanced')}>
                    <FieldRow label={t('vms.new.form.userData')} htmlFor="vm-cloudinit" help={t('vms.new.form.userDataHelp')}>
                      <Textarea
                        id="vm-cloudinit"
                        value={cloudInit}
                        onChange={(e) => setCloudInit(e.target.value)}
                        rows={4}
                        placeholder={'#cloud-config\npackages:\n  - htop'}
                        className="font-mono text-xs"
                      />
                    </FieldRow>
                    <FieldRow label={t('vms.new.form.dnsServers')} htmlFor="vm-dns" help={t('vms.new.form.dnsServersHelp')}>
                      <Input id="vm-dns" value={dns} onChange={(e) => setDns(e.target.value)} placeholder="1.1.1.1 8.8.8.8" />
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Options ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">{t('vms.new.form.options')}</CardTitle>
                <CardDescription>{t('vms.new.form.optionsDescription')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-2">
                <FieldRow label={t('vms.new.form.startAfterCreation')} htmlFor="vm-start">
                  <Switch id="vm-start" checked={startAfterCreate} onCheckedChange={setStartAfterCreate} />
                </FieldRow>
                <FieldRow label={t('vms.new.form.startOnBoot')} htmlFor="vm-onboot" help={t('vms.new.form.startOnBootHelp')}>
                  <Switch id="vm-onboot" checked={startOnBoot} onCheckedChange={setStartOnBoot} />
                </FieldRow>
                <FieldRow label={t('vms.new.form.guestAgent')} htmlFor="vm-agent" help={t('vms.new.form.guestAgentHelp')}>
                  <Switch id="vm-agent" checked={guestAgent} onCheckedChange={setGuestAgent} />
                </FieldRow>
                <FieldRow label={t('vms.new.form.protection')} htmlFor="vm-protect" help={t('vms.new.form.protectionHelp')}>
                  <Switch id="vm-protect" checked={protection} onCheckedChange={setProtection} />
                </FieldRow>
                <FieldRow label={t('vms.new.form.labels')} help={t('vms.new.form.labelsHelp')}>
                  <RepeatableRows
                    rows={labels}
                    onChange={setLabels}
                    newRow={() => ({ key: '', value: '' })}
                    addLabel={t('vms.new.form.addLabel')}
                    renderRow={(row, update) => (
                      <div className="flex gap-2">
                        <Input value={row.key} onChange={(e) => update({ ...row, key: e.target.value })} placeholder={t('vms.new.form.labelKeyPlaceholder')} className="flex-1" />
                        <Input value={row.value} onChange={(e) => update({ ...row, value: e.target.value })} placeholder={t('vms.new.form.labelValuePlaceholder')} className="flex-1" />
                      </div>
                    )}
                  />
                </FieldRow>
              </CardContent>
            </Card>

            <div className="flex items-center justify-between">
              <Button variant="outline" nativeButton={false} render={<Link to="/vms" />}>
                {t('common.cancel')}
              </Button>
              <Button onClick={handleCreate} disabled={!canCreate}>
                <Add />
                {t('vms.new.create')}
              </Button>
            </div>
          </div>

          {/* ─── Sticky summary ──────────────────────────────────────── */}
          <SummaryPanel
            title={t('vms.new.summary')}
            footer={
              <div className="flex w-full flex-wrap gap-1.5">
                {guestAgent && <Badge variant="outline">guest-agent</Badge>}
                {startAfterCreate && <Badge variant="outline">autostart</Badge>}
                {startOnBoot && <Badge variant="outline">on-boot</Badge>}
                {firewall && <Badge variant="outline">firewall</Badge>}
                {protection && <Badge variant="outline">protected</Badge>}
              </div>
            }
          >
            <div>
              <SummaryRow label={t('vms.new.form.image')}>{selectedImage ? selectedImage.name : '—'}</SummaryRow>
              <SummaryRow label={t('vms.new.form.placement')}>
                {selectedNode ? selectedNode.hostname : '—'}
                {selectedCluster ? ` · ${selectedCluster.name}` : ''}
              </SummaryRow>
              <SummaryRow label={t('vms.new.form.cpu')}>
                <MonoNum>{vcpu}</MonoNum> vCPU <span className="text-muted-foreground">({sockets}×{cores}, {cpuType})</span>
              </SummaryRow>
              <SummaryRow label={t('vms.new.form.memory')}>
                <Size bytes={ramBytes} />
                {ballooning ? <span className="text-muted-foreground"> · {t('vms.new.form.balloon')}</span> : null}
              </SummaryRow>
              <SummaryRow label={t('vms.new.form.bootDisk')}>
                <Size bytes={bootDiskBytes} /> <span className="text-muted-foreground">· {STORAGE_LABELS[effBootPool] ?? effBootPool}</span>
              </SummaryRow>
              {extraDisks.length > 0 && (
                <SummaryRow label={t('vms.new.form.extraDisks')}>
                  <MonoNum>{extraDisks.length}</MonoNum> · <Size bytes={extraDiskBytes} />
                </SummaryRow>
              )}
              <SummaryRow label={t('vms.new.network')}>
                {vpc} · {ipMode === 'dhcp' ? 'DHCP' : ipAddress || 'static'}
              </SummaryRow>
              <SummaryRow label={t('vms.new.form.firmware')}>
                {machineType} · {firmware.toUpperCase()}
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
