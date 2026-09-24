import type { Meta, StoryObj } from '@storybook/react-vite';
import { ClusterDetailBody, type JoinToken, type PlexorCluster, type PlexorNode } from '@/domains/fleet';

/**
 * /clusters/$id detail page stories.
 *
 * Renders `ClusterDetailBody` (identity + docs cards, node roster, join
 * tokens) with deterministic fixtures so baselines never depend on mock
 * state. The add-node dialog stays closed in every story.
 */

const node = (overrides: Partial<PlexorNode> & Pick<PlexorNode, 'id' | 'hostname'>): PlexorNode => ({
  role: 'compute',
  status: 'ready',
  spec: { vcpu: 32, ramGb: 128, diskGb: 4000, providers: ['kvm', 'ceph-rbd', 'ovs'] },
  isoVersion: 'plexor-1.2.3',
  joinedAt: '2026-03-01T08:15:00Z',
  lastSeenAt: '2026-09-19T09:59:45Z',
  vmCount: 3,
  ...overrides,
});

const noop = () => {};

const TOKENS: JoinToken[] = [
  {
    id: 'tok-001',
    label: 'rack1-n02 initial join',
    status: 'expired',
    token: 'plx_jtok_5f8c2e9a1b4d7e0f3a6b9c2d5e8f1a4b7c0d3e6f9a2b5c8d1e4f7a0b3c6d9e2f5a8b',
    intendedRole: 'compute',
    minIsoVersion: 'plexor-1.0.0',
    issuedAt: '2026-03-01T07:30:00Z',
    expiresAt: '2026-03-08T07:30:00Z',
    redeemedByNodeId: 'node-r1n2',
  },
  {
    id: 'tok-004',
    label: 'edge-pop-amsterdam',
    status: 'active',
    token: 'plx_jtok_8c1f5b2d4e7a0c3d6e9f2a5b8c1d4e7f0a3b6c8d2e5f8a1b4c7d0e3f6a9b2c5d8e1f4a7b0c3d6e',
    intendedRole: 'compute',
    minIsoVersion: 'plexor-1.2.3',
    issuedAt: '2026-09-18T19:30:00Z',
    expiresAt: '2026-09-25T19:30:00Z',
  },
];

const CLUSTER: PlexorCluster = {
  id: 'cluster-prod-eu-1',
  name: 'prod-eu-1',
  installProviders: ['kvm', 'ceph-rbd', 'ovs', 'ceph-rgw', 'postgresql', 'nats'],
  hostVersion: '1.2.3',
  uptimeSeconds: 14 * 24 * 3600 + 2 * 3600 + 17 * 60,
  endpoint: 'https://prod-eu-1.plexor.local:8443',
  createdAt: '2026-03-01T08:00:00Z',
  nodes: [
    node({ id: 'node-r1n1', hostname: 'rack1-n01', role: 'control' }),
    node({ id: 'node-r1n2', hostname: 'rack1-n02' }),
    node({ id: 'node-r2n1', hostname: 'rack2-n01', status: 'draining', vmCount: 0, isoVersion: 'plexor-1.2.2' }),
    node({ id: 'node-edge1', hostname: 'edge-ams-01', status: 'pending', spec: { vcpu: 8, ramGb: 32, diskGb: 1000, providers: ['kvm'] }, vmCount: 0 }),
  ],
  tokens: TOKENS,
};

const NO_NODES_CLUSTER: PlexorCluster = {
  ...CLUSTER,
  id: 'cluster-fresh-1',
  name: 'fresh-install',
  nodes: [],
  tokens: [],
};

const meta = {
  title: 'Pages/ClusterDetail',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: healthy-ish cluster with a mixed node roster + join tokens. */
export const Default: Story = {
  render: () => <ClusterDetailBody clusterId={CLUSTER.id} cluster={CLUSTER} onBack={noop} />,
};

/** Empty-ish: control plane registered, no nodes and no tokens yet. */
export const NoNodes: Story = {
  render: () => <ClusterDetailBody clusterId={NO_NODES_CLUSTER.id} cluster={NO_NODES_CLUSTER} onBack={noop} />,
};

/** Loading: header skeleton while the cluster resolves. */
export const Loading: Story = {
  render: () => <ClusterDetailBody clusterId="cluster-prod-eu-1" cluster={undefined} onBack={noop} isPending />,
};

/** Not found: destructive alert for an unknown cluster id. */
export const NotFound: Story = {
  render: () => <ClusterDetailBody clusterId="cluster-nope" cluster={undefined} onBack={noop} />,
};
