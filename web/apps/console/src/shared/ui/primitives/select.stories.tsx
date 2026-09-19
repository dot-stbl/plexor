import type { Meta, StoryObj } from '@storybook/react-vite';

import { Select, SelectContent, SelectTrigger, SelectValue } from './select';

const meta = {
  title: 'Primitives/Select',
  component: Select,
} satisfies Meta<typeof Select>;

export default meta;
type Story = StoryObj<typeof meta>;

const REGIONS = [
  { value: 'eu-north', label: 'eu-north — Stockholm' },
  { value: 'eu-west', label: 'eu-west — Dublin' },
  { value: 'us-east', label: 'us-east — Ashburn' },
  { value: 'ap-south', label: 'ap-south — Mumbai' },
];

export const Default: Story = {
  render: () => (
    <div className="w-64">
      <Select items={REGIONS} defaultValue="eu-north">
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent />
      </Select>
    </div>
  ),
};

export const Placeholder: Story = {
  render: () => (
    <div className="w-64">
      <Select items={REGIONS} placeholder="Select a region">
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent />
      </Select>
    </div>
  ),
};

export const TriggerSizes: Story = {
  render: () => (
    <div className="flex w-64 flex-col gap-3">
      <Select items={REGIONS} defaultValue="eu-west" placeholder="size=default">
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent />
      </Select>
      <Select items={REGIONS} defaultValue="eu-west" placeholder="size=sm">
        <SelectTrigger size="sm">
          <SelectValue />
        </SelectTrigger>
        <SelectContent />
      </Select>
    </div>
  ),
};

export const Invalid: Story = {
  render: () => (
    <div className="w-64">
      <Select items={REGIONS} placeholder="Select a region">
        <SelectTrigger aria-invalid>
          <SelectValue />
        </SelectTrigger>
        <SelectContent />
      </Select>
    </div>
  ),
};

export const Disabled: Story = {
  render: () => (
    <div className="w-64">
      <Select items={REGIONS} defaultValue="us-east" disabled>
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent />
      </Select>
    </div>
  ),
};
