import type { Meta, StoryObj } from '@storybook/react-vite';

import { StatusPill } from './status-pill';

const meta = {
  title: 'Primitives/StatusPill',
  component: StatusPill,
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: [
        'ok',
        'err',
        'warn',
        'idle',
        'info',
        'running',
        'failed',
        'pending',
        'stopped',
        'archived',
        'new',
        'beta',
        'deprecated',
        'draft',
      ],
    },
    size: { control: { type: 'radio' }, options: ['sm', 'md'] },
  },
} satisfies Meta<typeof StatusPill>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: { variant: 'running', children: 'Running' },
};

export const CoreVariants: Story = {
  args: { variant: 'ok' },
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <StatusPill variant="ok">Ok</StatusPill>
      <StatusPill variant="err">Error</StatusPill>
      <StatusPill variant="warn">Warning</StatusPill>
      <StatusPill variant="idle">Idle</StatusPill>
      <StatusPill variant="info">Info</StatusPill>
    </div>
  ),
};

export const SyncAliases: Story = {
  args: { variant: 'ok' },
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <StatusPill variant="running">Running</StatusPill>
      <StatusPill variant="failed">Failed</StatusPill>
      <StatusPill variant="pending">Pending</StatusPill>
      <StatusPill variant="stopped">Stopped</StatusPill>
    </div>
  ),
};

export const SemanticVariants: Story = {
  args: { variant: 'ok' },
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <StatusPill variant="new">New</StatusPill>
      <StatusPill variant="beta">Beta</StatusPill>
      <StatusPill variant="deprecated">Deprecated</StatusPill>
      <StatusPill variant="draft">Draft</StatusPill>
      <StatusPill variant="archived">Archived</StatusPill>
    </div>
  ),
};

export const Sizes: Story = {
  args: { variant: 'ok' },
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <StatusPill variant="ok" size="sm">
        Small
      </StatusPill>
      <StatusPill variant="ok" size="md">
        Medium
      </StatusPill>
    </div>
  ),
};

export const WithoutDot: Story = {
  args: { variant: 'ok' },
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <StatusPill variant="ok" hideDot>
        Ok
      </StatusPill>
      <StatusPill variant="warn" hideDot>
        Warning
      </StatusPill>
    </div>
  ),
};
