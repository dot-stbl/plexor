import type { Meta, StoryObj } from '@storybook/react-vite';
import { Add, Search } from '@nine-thirty-five/material-symbols-react/rounded/700';

import { Button } from './button';

const meta = {
  title: 'Primitives/Button',
  component: Button,
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: ['default', 'outline', 'secondary', 'ghost', 'destructive', 'link'],
    },
    size: {
      control: { type: 'select' },
      options: ['default', 'xs', 'sm', 'lg', 'icon', 'icon-xs', 'icon-sm', 'icon-lg'],
    },
  },
} satisfies Meta<typeof Button>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: { children: 'Create VM' },
};

export const Variants: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Button variant="default">Default</Button>
      <Button variant="outline">Outline</Button>
      <Button variant="secondary">Secondary</Button>
      <Button variant="ghost">Ghost</Button>
      <Button variant="destructive">Destructive</Button>
      <Button variant="link">Link</Button>
    </div>
  ),
};

export const Sizes: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Button size="xs">Extra small</Button>
      <Button size="sm">Small</Button>
      <Button size="default">Default</Button>
      <Button size="lg">Large</Button>
    </div>
  ),
};

export const IconButtons: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Button size="icon-xs" variant="outline" aria-label="Search">
        <Search />
      </Button>
      <Button size="icon-sm" variant="outline" aria-label="Search">
        <Search />
      </Button>
      <Button size="icon" variant="outline" aria-label="Search">
        <Search />
      </Button>
      <Button size="icon-lg" variant="outline" aria-label="Search">
        <Search />
      </Button>
    </div>
  ),
};

export const WithInlineIcons: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Button>
        <Add data-icon="inline-start" />
        Create VM
      </Button>
      <Button variant="outline">
        Search
        <Search data-icon="inline-end" />
      </Button>
    </div>
  ),
};

export const Disabled: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Button disabled>Create VM</Button>
      <Button variant="outline" disabled>
        Cancel
      </Button>
      <Button variant="destructive" disabled>
        Delete
      </Button>
    </div>
  ),
};
