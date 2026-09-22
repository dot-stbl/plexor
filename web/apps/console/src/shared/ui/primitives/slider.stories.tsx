import type { Meta, StoryObj } from '@storybook/react-vite';

import { Slider } from './slider';

const meta = {
  title: 'Primitives/Slider',
  component: Slider,
} satisfies Meta<typeof Slider>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    defaultValue: 35,
    'aria-label': 'Volume',
  },
};

export const MinBounds: Story = {
  args: {
    defaultValue: 0,
    'aria-label': 'Volume (min)',
  },
};

export const MaxBounds: Story = {
  args: {
    defaultValue: 100,
    'aria-label': 'Volume (max)',
  },
};

export const Disabled: Story = {
  args: {
    defaultValue: 50,
    isDisabled: true,
    'aria-label': 'Volume (disabled)',
  },
};

export const Range: Story = {
  args: {
    defaultValue: [25, 75],
    'aria-label': 'Price range',
  },
};

export const SizesAndPadding: Story = {
  render: () => (
    <div className="flex w-80 flex-col gap-6">
      <Slider defaultValue={20} aria-label="Wide" />
      <Slider defaultValue={60} aria-label="Wide (2)" className="w-full" />
    </div>
  ),
};