import type { Meta, StoryObj } from '@storybook/react-vite';
import { Cloud, DeployedCode } from '@nine-thirty-five/material-symbols-react/rounded/700';

import { Button } from './button';
import { EmptyState } from './empty-state';

const meta = {
  title: 'Primitives/EmptyState',
  component: EmptyState,
  parameters: { layout: 'fullscreen' },
} satisfies Meta<typeof EmptyState>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: { title: 'No virtual machines yet' },
  render: () => (
    <EmptyState
      icon={Cloud}
      title="No virtual machines yet"
      description="Create your first VM — Plexor brings it up on the chosen runtime and wires it into the folder network."
      docs={[{ href: 'https://plexor.dev/docs/vm', label: 'How VMs work' }]}
      action={<Button>Create VM</Button>}
    />
  ),
};

export const Compact: Story = {
  args: { title: 'No workloads in this folder' },
  render: () => (
    <EmptyState
      compact
      icon={DeployedCode}
      title="No workloads in this folder"
      description="Deploy a workload from a template or connect an existing image registry."
      action={<Button variant="outline">Deploy workload</Button>}
    />
  ),
};

export const WithDocsList: Story = {
  args: { title: 'No clusters yet' },
  render: () => (
    <EmptyState
      icon={DeployedCode}
      title="No clusters yet"
      description="A cluster is a group of nodes that run your workloads. Start from a preset or bring your own Kubernetes."
      docsLabel="Getting started:"
      docs={[
        { href: 'https://plexor.dev/docs/clusters', label: 'Cluster presets' },
        { href: 'https://plexor.dev/docs/byok', label: 'Bring your own k8s' },
      ]}
      action={<Button>Create cluster</Button>}
    />
  ),
};

export const Minimal: Story = {
  args: { title: 'Nothing here yet' },
  render: () => (
    <EmptyState icon={Cloud} title="Nothing here yet" description="Items will appear once the team starts using this section." />
  ),
};
