import type { JoinToken, PlexorCluster, PlexorNode } from './cluster-types';

const NODES: PlexorNode[] = [
  {
    id: 'node-prod-eu-1-a',
    hostname: 'node-a.prod-eu-1.local',
    role: 'control',
    status: 'ready',
    spec: { vcpu: 32, ramGb: 64, diskGb: 2000, providers: ['kvm', 'ovs', 'ceph-rbd'] },
    isoVersion: 'plexor-1.2.3',
    joinedAt: '2025-10-12T09:00:00Z',
    lastSeenAt: '2025-12-01T12:34:56Z',
    vmCount: 1,
  },
  {
    id: 'node-prod-eu-1-b',
    hostname: 'node-b.prod-eu-1.local',
    role: 'compute',
    status: 'ready',
    spec: { vcpu: 32, ramGb: 64, diskGb: 2000, providers: ['kvm', 'ovs', 'ceph-rbd'] },
    isoVersion: 'plexor-1.2.3',
    joinedAt: '2025-10-12T09:15:00Z',
    lastSeenAt: '2025-12-01T12:35:01Z',
    vmCount: 2,
  },
  {
    id: 'node-prod-eu-1-c',
    hostname: 'node-c.prod-eu-1.local',
    role: 'compute',
    status: 'ready',
    spec: { vcpu: 32, ramGb: 64, diskGb: 2000, providers: ['kvm', 'ovs', 'ceph-rbd'] },
    isoVersion: 'plexor-1.2.2',
    joinedAt: '2025-10-12T09:30:00Z',
    lastSeenAt: '2025-12-01T12:34:59Z',
    vmCount: 2,
  },
  {
    id: 'node-prod-eu-1-d',
    hostname: 'node-d.prod-eu-1.local',
    role: 'compute',
    status: 'draining',
    spec: { vcpu: 16, ramGb: 32, diskGb: 1000, providers: ['kvm', 'ovs', 'ceph-rbd'] },
    isoVersion: 'plexor-1.2.0',
    joinedAt: '2025-08-20T11:00:00Z',
    lastSeenAt: '2025-12-01T11:00:00Z',
    vmCount: 0,
  },
  {
    id: 'node-prod-eu-1-e',
    hostname: 'node-e.prod-eu-1.local',
    role: 'compute',
    status: 'offline',
    spec: { vcpu: 16, ramGb: 32, diskGb: 1000, providers: ['kvm', 'ovs', 'ceph-rbd'] },
    isoVersion: 'plexor-1.1.9',
    joinedAt: '2025-06-12T11:00:00Z',
    lastSeenAt: '2025-11-29T03:00:00Z',
    vmCount: 0,
  },
];

const TOKENS: JoinToken[] = [
  {
    id: 'tok-001',
    label: 'node-b initial join',
    status: 'expired',
    token: 'plx_jtok_5f8c2e9a1b4d7e0f3a6b9c2d5e8f1a4b7c0d3e6f9a2b5c8d1e4f7a0b3c6d9e2f5a8b1c4d7e0f3a6b9c2d5e8f1a4b',
    intendedRole: 'compute',
    minIsoVersion: 'plexor-1.0.0',
    issuedAt: '2025-10-12T08:00:00Z',
    expiresAt: '2025-10-19T08:00:00Z',
    redeemedByNodeId: 'node-prod-eu-1-b',
  },
  {
    id: 'tok-002',
    label: 'node-c initial join',
    status: 'expired',
    token: 'plx_jtok_6a9d3f0b2c5e8f1a4b7d0e3f6a9c2d5e8f1b4d7e0f3a6b9c2d5e8f1a4b7d0e3f6a9b2c5d8e1f4a7b0c3d6e9f2a5b8c1d',
    intendedRole: 'compute',
    minIsoVersion: 'plexor-1.0.0',
    issuedAt: '2025-10-12T08:30:00Z',
    expiresAt: '2025-10-19T08:30:00Z',
    redeemedByNodeId: 'node-prod-eu-1-c',
  },
  {
    id: 'tok-003',
    label: 'expansion-week-2025-11',
    status: 'expired',
    token: 'plx_jtok_7b0e4a1c3d6f9a2b5c8e1f4a7b0d3e6f9a2c5d8e1f4a7b0c3d6e9f2a5b8c1d4e7f0a3b6c9d2e5f8a1b4c7d0e3f6a9b2c5d',
    intendedRole: 'compute',
    minIsoVersion: 'plexor-1.2.0',
    issuedAt: '2025-11-01T10:00:00Z',
    expiresAt: '2025-11-08T10:00:00Z',
  },
  {
    id: 'tok-004',
    label: 'edge-pop-amsterdam',
    status: 'active',
    token: 'plx_jtok_8c1f5b2d4e7a0c3d6e9f2a5b8c1d4e7f0a3b6c9d2e5f8a1b4c7d0e3f6a9b2c5d8e1f4a7b0c3d6e9f2a5b8c1d4e7f0a3b6c',
    intendedRole: 'compute',
    minIsoVersion: 'plexor-1.2.3',
    issuedAt: '2025-12-01T08:00:00Z',
    expiresAt: '2025-12-08T08:00:00Z',
  },
];

const CLUSTER: PlexorCluster = {
  id: 'cluster-prod-eu-1',
  name: 'prod-eu-1',
  installProviders: ['kvm', 'ovs', 'ceph-rbd', 'ceph-rgw', 'postgresql', 'nats'],
  hostVersion: '1.2.3',
  uptimeSeconds: 14 * 24 * 3600 + 2 * 3600 + 17 * 60,
  endpoint: 'https://prod-eu-1.acme.internal:8443',
  createdAt: '2025-10-12T09:00:00Z',
  nodes: NODES,
  tokens: TOKENS,
};

export function listClusters(): PlexorCluster[] {
  return [CLUSTER];
}

export function getCluster(id: string): PlexorCluster | undefined {
  return id === CLUSTER.id ? CLUSTER : undefined;
}