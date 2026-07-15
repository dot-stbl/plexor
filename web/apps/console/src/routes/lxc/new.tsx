import { useMemo, useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { toast } from 'sonner';
import {
  Add,
  ArrowBack,
  DeployedCode,
  MenuBook,
  Stacks
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Switch } from '@/shared/ui/primitives/switch';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Progress } from '@/shared/ui/primitives/progress';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { Badge } from '@/shared/ui/primitives/badge';
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
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/shared/ui/primitives/empty';
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

// Provider → human label. Like VMs, container storage pools are DERIVED from the
// selected node's `providers` — self-hosted reality: a rootfs can only live on a
// backend the node actually runs.
const STORAGE_LABELS: Record<string, string> = {
  'local-lvm': 'Local LVM-Thin',
  'local-zfs': 'Local ZFS',
  'ceph-rbd': 'Ceph RBD (replicated)',
  nfs: 'NFS share',
};

// OS templates a self-hosted host keeps in its template cache.
const OS_TEMPLATES = [
  'ubuntu-24.04-standard',
  'debian-12-standard',
  'alpine-3.20-default',
  'rocky-9-default',
  'fedora-40-default',
];

const NETWORKS = ['prod-vpc', 'staging-vpc'] as const;
const SSH_KEYS = ['alex@workstation', 'deploy@ci'] as const;

/**
 * Create LXC container page — self-hosted depth. A container is lighter than a
 * VM (shared kernel), but Plexor still surfaces the real knobs: node placement,
 * unprivileged mapping, cgroup CPU/memory limits, rootfs pool + mount points,
 * bridged NIC + VLAN, and container features (nesting/keyctl/FUSE).
 * Basics stay visible; deep knobs live in per-card «Advanced» accordions.
 * Full-width, cards left + sticky SummaryPanel right (like /vms/new).
 */
function CreateLxcPage() {
  const navigate = useNavigate();
  const { clusters } = useListClusters();

  const allNodes = clusters.flatMap((c) => c.nodes);
  // Node gating: a container needs a node running a container runtime.
  const lxcNodes = allNodes.filter(
    (n) => n.status === 'ready' && n.spec.providers.some((p) => p === 'lxd' || p === 'lxc'),
  );

  // Placement
  const [nodeId, setNodeId] = useState<string>('');

  // Template
  const [template, setTemplate] = useState<string>('');
  const [unprivileged, setUnprivileged] = useState(true);

  // Compute — cgroup limits
  const [cores, setCores] = useState(2);
  const [memBytes, setMemBytes] = useState<number>(SizeUtils.gibToBytes(2));
  const [swapBytes, setSwapBytes] = useState<number>(SizeUtils.gibToBytes(1));
  const [cpuLimit, setCpuLimit] = useState(0);
  const [cpuUnits, setCpuUnits] = useState(1024);

  // Root filesystem
  const [pool, setPool] = useState<string>('');
  const [rootfsBytes, setRootfsBytes] = useState<number>(SizeUtils.gibToBytes(8));
  const [mounts, setMounts] = useState<{ sizeGib: number; pool: string; path: string }[]>([]);

  // Network
  const [vpc, setVpc] = useState<string>(NETWORKS[0]);
  const [ipMode, setIpMode] = useState<string>('dhcp');
  const [ipAddress, setIpAddress] = useState('');
  const [gateway, setGateway] = useState('');
  const [nicName, setNicName] = useState('eth0');
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
  const [sshKey, setSshKey] = useState<string>(SSH_KEYS[0]);
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

  // Storage pools offered by THIS node (self-hosted: comes from the node).
  const storagePools = useMemo(() => {
    const pools = new Set<string>(['local-lvm']);
    selectedNode?.spec.providers.forEach((p) => {
      if (STORAGE_LABELS[p]) pools.add(p);
    });
    return [...pools];
  }, [selectedNode]);
  const effBootPool = storagePools.includes(pool) ? pool : (storagePools[0] ?? 'local-lvm');

  const mountsGib = mounts.reduce((sum, m) => sum + (m.sizeGib || 0), 0);
  const rootfsGib = Math.round(rootfsBytes / 1024 ** 3);
  const usedDiskGib = rootfsGib + mountsGib;
  const usedRamGib = Math.round(memBytes / 1024 ** 3);

  const effectiveName = name.trim() || 'ct-1';
  const staticOk = ipMode === 'dhcp' || (ipAddress.trim() !== '' && gateway.trim() !== '');
  const canCreate = Boolean(nodeId && name.trim() && template && effBootPool && staticOk);

  const handleCreate = () => {
    if (!canCreate) return;
    toast(`Creating container ${effectiveName}`, {
      description: `${template} · ${cores} cores / ${SizeUtils.format(memBytes)} · rootfs ${SizeUtils.format(rootfsBytes)} on ${STORAGE_LABELS[effBootPool] ?? effBootPool} · ${selectedNode?.hostname ?? '—'}`,
    });
    // No LXC list yet — land the user on the VM list.
    void navigate({ to: '/vms' });
  };

  return (
    <PageTemplate
      data-od-id="lxc-new"
      width="full"
      title="Create LXC container"
      description="System container config — placement, cgroup limits, rootfs and network are all yours to set."
      actions={
        <Button variant="ghost" nativeButton={false} render={<Link to="/vms" />}>
          <ArrowBack />
          Back
        </Button>
      }
    >
      {/* Empty state: 0 clusters → user hasn't installed Plexor yet. */}
      {clusters.length === 0 && (
        <div className="py-8">
          <Empty data-od-id="lxc-new-no-cluster">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <DeployedCode />
              </EmptyMedia>
              <EmptyTitle>Plexor control plane is not registered</EmptyTitle>
              <EmptyDescription>
                To create containers, you first need to install Plexor on a server. The installation
                sets up the control plane and registers it in the UI.
              </EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <Button render={<a href="https://plexor.dev/docs/install" target="_blank" rel="noreferrer" />}>
                <MenuBook />
                Installation docs
              </Button>
            </EmptyContent>
          </Empty>
        </div>
      )}

      {/* Empty state: clusters exist, but no ready node runs a container runtime. */}
      {clusters.length > 0 && lxcNodes.length === 0 && (
        <div className="py-8">
          <Empty data-od-id="lxc-new-no-ready-nodes">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <Stacks />
              </EmptyMedia>
              <EmptyTitle>No LXC-capable nodes</EmptyTitle>
              <EmptyDescription>
                Containers need a ready node with an LXD/LXC runtime provider. Connect an
                LXD-capable node (select the <span className="font-medium text-foreground">lxd</span>{' '}
                provider at install), then it appears here.
              </EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <Button nativeButton={false} render={<Link to="/clusters/$id" params={{ id: clusters[0]!.id }} />}>
                <ArrowBack />
                Go to cluster
              </Button>
            </EmptyContent>
          </Empty>
        </div>
      )}

      {clusters.length > 0 && lxcNodes.length > 0 && (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div className="min-w-0 space-y-3">
            {/* ─── Placement ─────────────────────────────────────────── */}
            <Card data-od-id="lxc-placement">
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Placement</CardTitle>
                <CardDescription>
                  Which Plexor.NodeAgent hosts the container. Only{' '}
                  <span className="font-medium text-foreground">ready</span> nodes with a container
                  runtime are listed.
                </CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="Node" htmlFor="ct-node" required help="Self-hosted: the container runs on a specific physical node, not an abstract zone.">
                  <SimpleSelect
                    id="ct-node"
                    value={nodeId}
                    onChange={setNodeId}
                    options={lxcNodes.map((n) => n.id)}
                    render={(id) => {
                      const n = lxcNodes.find((x) => x.id === id);
                      return n ? `${n.hostname} · ${n.role === 'control' ? 'control-plane' : 'compute'}` : id;
                    }}
                    placeholder="Select a node"
                    className="w-full"
                  />
                </FieldRow>

                {selectedNode && (
                  <div className="grid grid-cols-1 gap-3 py-3 md:grid-cols-3">
                    <CapacityCell label="CPU" used={cores} total={selectedNode.spec.vcpu} unit="vCPU" />
                    <CapacityCell label="RAM" used={usedRamGib} total={selectedNode.spec.ramGb} unit="GiB" />
                    <CapacityCell label="Disk" used={usedDiskGib} total={selectedNode.spec.diskGb} unit="GiB" />
                  </div>
                )}

                {selectedNode && (
                  <div className="flex flex-wrap items-center gap-x-3 gap-y-1 py-3 text-xs text-muted-foreground">
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
                <CardTitle className="text-sm">Template</CardTitle>
                <CardDescription>Root filesystem template from the host's template cache.</CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="OS template" htmlFor="ct-template" required help="The base rootfs the container is created from.">
                  <SimpleSelect
                    id="ct-template"
                    value={template}
                    onChange={setTemplate}
                    options={OS_TEMPLATES}
                    placeholder="Select a template"
                    className="w-full"
                  />
                </FieldRow>
                <FieldRow label="Unprivileged container" htmlFor="ct-unpriv" help="Maps container UIDs to an unprivileged host range — safer isolation. Recommended.">
                  <Switch id="ct-unpriv" checked={unprivileged} onCheckedChange={setUnprivileged} />
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Compute ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Compute</CardTitle>
                <CardDescription>CPU and memory limits enforced via cgroups. Exact sizes — self-hosted, not fixed cloud «flavors».</CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="Cores" htmlFor="ct-cores" required help="Number of host cores the container may schedule on.">
                  <Stepper id="ct-cores" value={cores} onValueChange={setCores} min={1} max={64} step={1} suffix="cores" />
                </FieldRow>
                <FieldRow label="Memory" htmlFor="ct-mem" required help="Hard memory limit. Down to the MiB, capped by the node's free memory.">
                  <SizeField id="ct-mem" bytes={memBytes} onValueChange={setMemBytes} units={['MiB', 'GiB']} min={128 * 1024 ** 2} max={SizeUtils.gibToBytes(512)} />
                </FieldRow>
                <FieldRow label="Swap" htmlFor="ct-swap" help="Swap the container may use in addition to memory. 0 = no swap.">
                  <SizeField id="ct-swap" bytes={swapBytes} onValueChange={setSwapBytes} units={['MiB', 'GiB']} min={0} max={SizeUtils.gibToBytes(512)} />
                </FieldRow>
                <div className="pt-2">
                  <Disclosure summary="Advanced · CPU limit, CPU units">
                    <FieldRow label="CPU limit" htmlFor="ct-cpulimit" help="Ceiling on total CPU time (in cores). 0 = unlimited.">
                      <Stepper id="ct-cpulimit" value={cpuLimit} onValueChange={setCpuLimit} min={0} max={cores} step={1} suffix="cores" />
                    </FieldRow>
                    <FieldRow label="CPU units" htmlFor="ct-cpuunits" help="Relative scheduling weight vs. other containers (cgroup cpu.shares). Default 1024.">
                      <Stepper id="ct-cpuunits" value={cpuUnits} onValueChange={setCpuUnits} min={8} max={100000} step={1} />
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Root filesystem ───────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Root filesystem</CardTitle>
                <CardDescription>
                  The container's rootfs + extra mount points. Pools come from the node's backends
                  {selectedNode ? '' : ' (select a node first)'}.
                </CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="Storage pool" htmlFor="ct-pool" required help="Backend the rootfs lives on — offered by the selected node (Ceph, LVM-Thin, ZFS…).">
                  <SimpleSelect
                    id="ct-pool"
                    value={effBootPool}
                    onChange={setPool}
                    options={storagePools}
                    render={(p) => STORAGE_LABELS[p] ?? p}
                  />
                </FieldRow>
                <FieldRow label="Rootfs size" htmlFor="ct-rootfs" required help="Size of the root volume. Down to the GiB.">
                  <SizeField id="ct-rootfs" bytes={rootfsBytes} onValueChange={setRootfsBytes} units={['GiB', 'TiB']} min={SizeUtils.gibToBytes(1)} max={SizeUtils.gibToBytes(16384)} />
                </FieldRow>
                <div className="pt-2">
                  <Disclosure summary="Advanced · Mount points">
                    <FieldRow label="Mount points" description="Additional volumes bind-mounted into the container.">
                      <RepeatableRows
                        rows={mounts}
                        onChange={setMounts}
                        newRow={() => ({ sizeGib: 8, pool: effBootPool, path: '' })}
                        addLabel="Add mount point"
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
                <CardTitle className="text-sm">Network</CardTitle>
                <CardDescription>Bridge + addressing for the container's NIC.</CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="Bridge / VPC" htmlFor="ct-vpc" required>
                  <SimpleSelect id="ct-vpc" value={vpc} onChange={setVpc} options={[...NETWORKS]} />
                </FieldRow>
                <FieldRow label="IP address" help="DHCP from the subnet, or pin a static address + gateway.">
                  <SegmentedControl
                    aria-label="IP assignment"
                    value={ipMode}
                    onValueChange={setIpMode}
                    options={[
                      { value: 'dhcp', label: 'DHCP' },
                      { value: 'static', label: 'Static' },
                    ]}
                  />
                  {ipMode === 'static' && (
                    <div className="flex flex-wrap gap-2">
                      <Input value={ipAddress} onChange={(e) => setIpAddress(e.target.value)} placeholder="10.0.0.10/24" className="flex-1" />
                      <Input value={gateway} onChange={(e) => setGateway(e.target.value)} placeholder="gateway 10.0.0.1" className="flex-1" />
                    </div>
                  )}
                </FieldRow>
                <div className="pt-2">
                  <Disclosure summary="Advanced · Interface, VLAN, firewall, rate limit">
                    <FieldRow label="Interface name" htmlFor="ct-nic" help="Name of the NIC inside the container.">
                      <Input id="ct-nic" value={nicName} onChange={(e) => setNicName(e.target.value)} placeholder="eth0" className="w-32" />
                    </FieldRow>
                    <FieldRow label="VLAN tag" htmlFor="ct-vlan" help="802.1Q tag on the port. Empty = untagged.">
                      <Input id="ct-vlan" value={vlan} onChange={(e) => setVlan(e.target.value)} placeholder="e.g. 100" inputMode="numeric" className="w-32" />
                    </FieldRow>
                    <FieldRow label="Firewall" help="Attach the node's firewall to this NIC.">
                      <Switch checked={firewall} onCheckedChange={setFirewall} />
                    </FieldRow>
                    <FieldRow label="Rate limit" help="Cap egress in MB/s. 0 = unlimited.">
                      <Stepper value={rateLimit} onValueChange={setRateLimit} min={0} max={10000} suffix="MB/s" />
                    </FieldRow>
                  </Disclosure>
                </div>
              </CardContent>
            </Card>

            {/* ─── Features ──────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Features</CardTitle>
                <CardDescription>Kernel features exposed to the container. Off by default — enable only what you need.</CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="Nesting" htmlFor="ct-nesting" help="Run containers / Docker inside this container.">
                  <Switch id="ct-nesting" checked={nesting} onCheckedChange={setNesting} />
                </FieldRow>
                <FieldRow label="keyctl" htmlFor="ct-keyctl" help="Allow the keyring syscalls — needed by some systemd services in unprivileged containers.">
                  <Switch id="ct-keyctl" checked={keyctl} onCheckedChange={setKeyctl} />
                </FieldRow>
                <FieldRow label="FUSE" htmlFor="ct-fuse" help="Allow FUSE filesystems inside the container.">
                  <Switch id="ct-fuse" checked={fuse} onCheckedChange={setFuse} />
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Access ────────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Access</CardTitle>
                <CardDescription>Name, credentials and DNS.</CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="Name" htmlFor="ct-name" required help="Used as the hostname. Lowercase letters, digits and hyphens.">
                  <Input id="ct-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. app-01" />
                </FieldRow>
                <FieldRow label="Authentication" help="An SSH key is recommended; a password is optional.">
                  <SegmentedControl
                    aria-label="Authentication"
                    value={authMode}
                    onValueChange={setAuthMode}
                    options={[
                      { value: 'key', label: 'SSH key' },
                      { value: 'password', label: 'Set password' },
                    ]}
                  />
                  {authMode === 'key' ? (
                    <SimpleSelect value={sshKey} onChange={setSshKey} options={[...SSH_KEYS]} />
                  ) : (
                    <PasswordInput value={password} onChange={(e) => setPassword(e.target.value)} placeholder="Password" />
                  )}
                </FieldRow>
                <FieldRow label="DNS servers" htmlFor="ct-dns" help="Space-separated resolvers configured in the container.">
                  <Input id="ct-dns" value={dns} onChange={(e) => setDns(e.target.value)} placeholder="1.1.1.1 8.8.8.8" />
                </FieldRow>
              </CardContent>
            </Card>

            {/* ─── Options ───────────────────────────────────────────── */}
            <Card>
              <CardHeader className="border-b border-border">
                <CardTitle className="text-sm">Options</CardTitle>
                <CardDescription>Boot behavior, protection, labels.</CardDescription>
              </CardHeader>
              <CardContent className="divide-y divide-border">
                <FieldRow label="Start after creation" htmlFor="ct-start">
                  <Switch id="ct-start" checked={startAfterCreate} onCheckedChange={setStartAfterCreate} />
                </FieldRow>
                <FieldRow label="Start on boot" htmlFor="ct-onboot" help="Autostart when the node boots.">
                  <Switch id="ct-onboot" checked={startOnBoot} onCheckedChange={setStartOnBoot} />
                </FieldRow>
                <FieldRow label="Start order" htmlFor="ct-order" help="Boot order priority — lower starts first.">
                  <Stepper id="ct-order" value={startOrder} onValueChange={setStartOrder} min={0} max={999} step={1} />
                </FieldRow>
                <FieldRow label="Protection" htmlFor="ct-protect" help="Blocks accidental deletion until turned off.">
                  <Switch id="ct-protect" checked={protection} onCheckedChange={setProtection} />
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
              <Button variant="outline" nativeButton={false} render={<Link to="/vms" />}>
                Cancel
              </Button>
              <Button onClick={handleCreate} disabled={!canCreate}>
                <Add />
                Create container
              </Button>
            </div>
          </div>

          {/* ─── Sticky summary ──────────────────────────────────────── */}
          <SummaryPanel title="What will be deployed">
            <div>
              <SummaryRow label="Template">{template || '—'}</SummaryRow>
              <SummaryRow label="Placement">
                {selectedNode ? selectedNode.hostname : '—'}
                {selectedCluster ? ` · ${selectedCluster.name}` : ''}
              </SummaryRow>
              <SummaryRow label="Type">{unprivileged ? 'unprivileged' : 'privileged'}</SummaryRow>
              <SummaryRow label="Cores">
                <MonoNum>{cores}</MonoNum> cores
              </SummaryRow>
              <SummaryRow label="Memory">
                <Size bytes={memBytes} />
              </SummaryRow>
              <SummaryRow label="Swap">
                <Size bytes={swapBytes} />
              </SummaryRow>
              <SummaryRow label="Rootfs">
                <Size bytes={rootfsBytes} /> <span className="text-muted-foreground">· {STORAGE_LABELS[effBootPool] ?? effBootPool}</span>
              </SummaryRow>
              {mounts.length > 0 && (
                <SummaryRow label="Mounts">
                  <MonoNum>{mounts.length}</MonoNum> · <Size bytes={SizeUtils.gibToBytes(mountsGib)} />
                </SummaryRow>
              )}
              <SummaryRow label="Network">
                {vpc} · {ipMode === 'dhcp' ? 'DHCP' : ipAddress || 'static'}
              </SummaryRow>
            </div>
            <div className="mt-2 flex flex-wrap gap-1.5 border-t border-border pt-2">
              {nesting && <Badge variant="outline">nesting</Badge>}
              {startOnBoot && <Badge variant="outline">on-boot</Badge>}
              {startAfterCreate && <Badge variant="outline">autostart</Badge>}
              {firewall && <Badge variant="outline">firewall</Badge>}
              {protection && <Badge variant="outline">protected</Badge>}
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
