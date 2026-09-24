import type { Meta, StoryObj } from '@storybook/react-vite';
import { K8sDetailBody, K8sDetailNotFound, K8sDetailSkeleton } from '@/domains/catalog';
import type { K8sCluster } from '@/domains/catalog';

/**
 * /k8s/$id page stories.
 *
 * Renders the real domain body components with deterministic fixtures —
 * unlike `vms/$id.stories.tsx` (which duplicates the body because the
 * route renders it inline), the k8s detail body is a domain component
 * with an `onBack` callback, so stories exercise the production component
 * directly. If the body's field set changes, update these fixtures.
 */

const GIB = 1024 ** 3;

const RUNNING_CLUSTER: K8sCluster = {
  id: 'k8s-prod',
  name: 'prod-k3s',
  status: 'running',
  version: 'v1.31.1+k3s1',
  cpNodes: 3,
  workerNodes: 5,
  vcpu: 40,
  ramBytes: 96 * GIB,
  cni: 'Cilium',
  fleet: 'prod-cluster',
  endpoint: 'https://10.10.1.50:6443',
  createdAt: '2026-03-12T00:00:00Z',
};

const DEGRADED_CLUSTER: K8sCluster = {
  ...RUNNING_CLUSTER,
  id: 'k8s-obs',
  name: 'obs-k3s',
  status: 'degraded',
  version: 'v1.30.5+k3s1',
  cpNodes: 1,
  workerNodes: 4,
  vcpu: 12,
  ramBytes: 24 * GIB,
  endpoint: 'https://10.0.0.30:6443',
  createdAt: '2026-05-22T08:00:00Z',
};

const PROVISIONING_CLUSTER: K8sCluster = {
  ...RUNNING_CLUSTER,
  id: 'k8s-edge',
  name: 'edge-k3s',
  status: 'provisioning',
  cpNodes: 1,
  workerNodes: 2,
  vcpu: 8,
  ramBytes: 16 * GIB,
  cni: 'Flannel',
  fleet: 'edge-cluster',
  endpoint: 'https://10.30.1.50:6443',
  createdAt: '2026-07-08T09:15:00Z',
};

const noop = () => {};

const meta = {
  title: 'Pages/K8sDetail',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: a running HA cluster — identity card + capacity totals. */
export const Running: Story = {
  render: () => <K8sDetailBody cluster={RUNNING_CLUSTER} onBack={noop} />,
};

/** Degraded cluster: warn status pill in the header, same field set. */
export const Degraded: Story = {
  render: () => <K8sDetailBody cluster={DEGRADED_CLUSTER} onBack={noop} />,
};

/** Provisioning cluster: info status pill. */
export const Provisioning: Story = {
  render: () => <K8sDetailBody cluster={PROVISIONING_CLUSTER} onBack={noop} />,
};

/** Loading: skeleton placeholders — the seam the kubb query will drive. */
export const Loading: Story = {
  render: () => <K8sDetailSkeleton />,
};

/** Not found: 404 alert when the cluster id doesn't exist. */
export const NotFound: Story = {
  render: () => <K8sDetailNotFound id="k8s-does-not-exist" onBack={noop} />,
};
