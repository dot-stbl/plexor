import type { Meta, StoryObj } from '@storybook/react-vite';
import { LxcDetailBody, LxcDetailNotFound, LxcDetailSkeleton } from '@/domains/compute';
import type { LxcContainer } from '@/domains/compute';

/**
 * /lxc/$id page stories.
 *
 * Renders the real domain body components with deterministic fixtures —
 * the lxc detail body is a domain component with an `onBack` callback, so
 * stories exercise the production component directly. If the body's field
 * set changes, update these fixtures.
 */

const GIB = 1024 ** 3;

const RUNNING_CONTAINER: LxcContainer = {
  id: 'lxc-web-01',
  name: 'web-01',
  status: 'running',
  template: 'ubuntu-24.04',
  os: 'Ubuntu',
  osVersion: '24.04',
  cores: 2,
  ramBytes: 2 * GIB,
  rootfsBytes: 12 * GIB,
  unprivileged: true,
  nodeHostname: 'pve-rack1-n01',
  ip: '10.0.0.31',
  createdAt: '2026-03-12T09:20:00Z',
};

const STOPPED_CONTAINER: LxcContainer = {
  ...RUNNING_CONTAINER,
  id: 'lxc-build-01',
  name: 'build-01',
  status: 'stopped',
  template: 'debian-12',
  os: 'Debian',
  osVersion: '12',
  cores: 4,
  ramBytes: 4 * GIB,
  rootfsBytes: 20 * GIB,
  unprivileged: false,
  nodeHostname: 'pve-rack1-n02',
  ip: '10.0.0.34',
  createdAt: '2026-05-18T11:40:00Z',
};

const ERROR_CONTAINER: LxcContainer = {
  ...RUNNING_CONTAINER,
  id: 'lxc-metrics-01',
  name: 'metrics-01',
  status: 'error',
  ramBytes: 1 * GIB,
  rootfsBytes: 10 * GIB,
  nodeHostname: 'pve-rack1-n02',
  ip: '10.0.0.36',
  createdAt: '2026-06-30T17:50:00Z',
};

const noop = () => {};

const meta = {
  title: 'Pages/LxcDetail',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: a running unprivileged Ubuntu container — identity, resources, placement. */
export const Running: Story = {
  render: () => <LxcDetailBody container={RUNNING_CONTAINER} onBack={noop} />,
};

/** Stopped privileged Debian container: outline type badge, idle pill. */
export const Stopped: Story = {
  render: () => <LxcDetailBody container={STOPPED_CONTAINER} onBack={noop} />,
};

/** Error container: err status pill in the header. */
export const Error: Story = {
  render: () => <LxcDetailBody container={ERROR_CONTAINER} onBack={noop} />,
};

/** Loading: skeleton placeholders — the seam the kubb query will drive. */
export const Loading: Story = {
  render: () => <LxcDetailSkeleton />,
};

/** Not found: 404 alert when the container id doesn't exist. */
export const NotFound: Story = {
  render: () => <LxcDetailNotFound id="lxc-does-not-exist" onBack={noop} />,
};
