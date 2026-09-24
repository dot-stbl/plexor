/**
 * Node fixtures — the compute bounded context's physical node roster for
 * the one self-hosted install this mock system represents. Real
 * Proxmox-style hostnames + plausible server capacities. This is the
 * SINGLE source of truth for node identity — `fleet/clusters.ts`
 * embeds these same objects, and `compute/vms.ts` places VMs on them via
 * `FLEET_PLACEMENT`.
 */
import type { PlexorNode } from '@/domains/fleet';

export const NODES: PlexorNode[] = [
  {
    id: 'node-pve-r1n01',
    hostname: 'pve-rack1-n01',
    role: 'control',
    status: 'ready',
    spec: { vcpu: 32, ramGb: 128, diskGb: 4000, providers: ['kvm', 'ceph-rbd', 'ovs'] },
    isoVersion: 'plexor-1.2.3',
    joinedAt: '2026-03-01T08:00:00Z',
    lastSeenAt: '2026-09-19T09:59:30Z',
    vmCount: 3,
  },
  {
    id: 'node-pve-r1n02',
    hostname: 'pve-rack1-n02',
    role: 'compute',
    status: 'ready',
    spec: { vcpu: 32, ramGb: 128, diskGb: 4000, providers: ['kvm', 'ceph-rbd', 'ovs'] },
    isoVersion: 'plexor-1.2.3',
    joinedAt: '2026-03-01T08:15:00Z',
    lastSeenAt: '2026-09-19T09:59:45Z',
    vmCount: 3,
  },
  {
    id: 'node-pve-r2n01',
    hostname: 'pve-rack2-n01',
    role: 'compute',
    status: 'ready',
    spec: { vcpu: 24, ramGb: 96, diskGb: 3000, providers: ['kvm', 'lvm-thin', 'ovs'] },
    isoVersion: 'plexor-1.2.2',
    joinedAt: '2026-04-15T10:00:00Z',
    lastSeenAt: '2026-09-19T09:58:50Z',
    vmCount: 2,
  },
  {
    id: 'node-pve-r2n02',
    hostname: 'pve-rack2-n02',
    role: 'compute',
    status: 'draining',
    spec: { vcpu: 16, ramGb: 64, diskGb: 2000, providers: ['kvm', 'lvm-thin'] },
    isoVersion: 'plexor-1.2.0',
    joinedAt: '2026-05-20T11:00:00Z',
    lastSeenAt: '2026-09-19T09:50:00Z',
    vmCount: 0,
  },
  {
    id: 'node-pve-edge-ams01',
    hostname: 'pve-edge-ams-01',
    role: 'compute',
    status: 'pending',
    spec: { vcpu: 8, ramGb: 32, diskGb: 1000, providers: ['kvm'] },
    isoVersion: 'plexor-1.2.3',
    joinedAt: '2026-09-18T20:00:00Z',
    lastSeenAt: '2026-09-19T09:55:00Z',
    vmCount: 0,
  },
];

export const NODE_BY_ID: ReadonlyMap<string, PlexorNode> = new Map(NODES.map((n) => [n.id, n]));
