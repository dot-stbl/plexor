import type { Meta, StoryObj } from '@storybook/react-vite';
import {
  ManagedClusterDetail,
  ManagedClusterNotFound,
  ManagedClusterSkeleton,
} from '@/domains/catalog';
import { getEngine } from '@/shared/api/mocks/handmade/databases';

/**
 * /managed/c/<clusterId> detail stories. The body components take the
 * resolved engine + cluster as props (the route shell does the lookup),
 * so stories render them directly with fixtures.
 */

const noop = () => {};

const engine = getEngine('redis');
if (!engine) {
  throw new Error('engine "redis" missing from the catalog mock');
}

const CLUSTER = {
  id: 'db-redis-cache',
  name: 'redis-cache',
  engineId: 'redis',
  kind: 'cache',
  runtime: 'docker',
  nodeId: 'node-pve-r1n02',
  hostname: 'pve-rack1-n02',
  dns: 'redis-cache.db.plexor.internal',
  status: 'running',
  version: '7.4',
  storageGb: 8,
  backupsEnabled: false,
  bindings: 2,
  createdAt: '2026-07-01T09:05:00Z',
} as const;

const meta = {
  title: 'Pages/ManagedClusterDetail',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: running cluster, identity / connection / storage cards. */
export const Default: Story = {
  render: () => <ManagedClusterDetail engine={engine} cluster={CLUSTER} onBack={noop} />,
};

/** Loading: header + card skeletons while the cluster resolves. */
export const Loading: Story = {
  render: () => <ManagedClusterSkeleton onBack={noop} />,
};

/** Not found: unknown cluster id (stale link or deleted cluster). */
export const NotFound: Story = {
  render: () => <ManagedClusterNotFound clusterId="db-gone" onBack={noop} />,
};
