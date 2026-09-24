import type { Meta, StoryObj } from '@storybook/react-vite';

import { Input } from './input';

const meta = {
  title: 'Primitives/Input',
  component: Input,
} satisfies Meta<typeof Input>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    placeholder: 'vm-prod-01',
  },
};

export const WithValue: Story = {
  args: {
    defaultValue: 'k8s-prod-eu',
  },
};

export const Types: Story = {
  render: () => (
    <div className="flex w-64 flex-col gap-3">
      <Input type="text" placeholder="Text" />
      <Input type="password" defaultValue="hunter2" placeholder="Password" />
      <Input type="number" placeholder="Port" />
      <Input type="email" placeholder="ops@plexor.dev" />
    </div>
  ),
};

export const Invalid: Story = {
  render: () => (
    <div className="flex w-64 flex-col gap-3">
      <Input aria-invalid placeholder="Required" />
      <Input aria-invalid defaultValue="not-a-hostname" />
    </div>
  ),
};

export const Disabled: Story = {
  render: () => (
    <div className="flex w-64 flex-col gap-3">
      <Input disabled placeholder="Disabled" />
      <Input disabled defaultValue="locked-value" />
    </div>
  ),
};
