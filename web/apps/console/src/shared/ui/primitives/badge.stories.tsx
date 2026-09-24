import type { Meta, StoryObj } from '@storybook/react-vite';
import { Check } from '@nine-thirty-five/material-symbols-react/rounded/700';

import { Badge } from './badge';

const meta = {
  title: 'Primitives/Badge',
  component: Badge,
} satisfies Meta<typeof Badge>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: { children: 'Beta' },
};

export const Variants: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Badge>Default</Badge>
      <Badge variant="secondary">Secondary</Badge>
      <Badge variant="destructive">Destructive</Badge>
      <Badge variant="outline">Outline</Badge>
      <Badge variant="ghost">Ghost</Badge>
      <Badge variant="link">Link</Badge>
    </div>
  ),
};

export const WithIcon: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Badge>
        <Check />
        Verified
      </Badge>
      <Badge variant="secondary">
        <Check />
        Synced
      </Badge>
    </div>
  ),
};

export const UsageExamples: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Badge variant="outline">v0.1.4</Badge>
      <Badge variant="secondary">team-zero</Badge>
      <Badge variant="destructive">deprecated</Badge>
      <Badge variant="default">new</Badge>
    </div>
  ),
};
