import type { Meta, StoryObj } from '@storybook/react-vite';

import { Tabs, TabsContent, TabsList, TabsTrigger } from './tabs';

const meta = {
  title: 'Primitives/Tabs',
  component: Tabs,
} satisfies Meta<typeof Tabs>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => (
    <Tabs defaultValue="overview" className="w-96">
      <TabsList>
        <TabsTrigger value="overview">Overview</TabsTrigger>
        <TabsTrigger value="nodes">Nodes</TabsTrigger>
        <TabsTrigger value="settings">Settings</TabsTrigger>
      </TabsList>
      <TabsContent value="overview">Cluster overview panel.</TabsContent>
      <TabsContent value="nodes">Node list panel.</TabsContent>
      <TabsContent value="settings">Cluster settings panel.</TabsContent>
    </Tabs>
  ),
};

export const LineVariant: Story = {
  render: () => (
    <Tabs defaultValue="overview" className="w-96">
      <TabsList variant="line">
        <TabsTrigger value="overview">Overview</TabsTrigger>
        <TabsTrigger value="nodes">Nodes</TabsTrigger>
        <TabsTrigger value="settings">Settings</TabsTrigger>
      </TabsList>
      <TabsContent value="overview">Cluster overview panel.</TabsContent>
      <TabsContent value="nodes">Node list panel.</TabsContent>
      <TabsContent value="settings">Cluster settings panel.</TabsContent>
    </Tabs>
  ),
};

export const DisabledTrigger: Story = {
  render: () => (
    <Tabs defaultValue="overview" className="w-96">
      <TabsList>
        <TabsTrigger value="overview">Overview</TabsTrigger>
        <TabsTrigger value="nodes" disabled>
          Nodes
        </TabsTrigger>
        <TabsTrigger value="settings">Settings</TabsTrigger>
      </TabsList>
      <TabsContent value="overview">Cluster overview panel.</TabsContent>
      <TabsContent value="nodes">Node list panel.</TabsContent>
      <TabsContent value="settings">Cluster settings panel.</TabsContent>
    </Tabs>
  ),
};

export const Vertical: Story = {
  render: () => (
    <Tabs defaultValue="overview" orientation="vertical" className="w-96">
      <TabsList variant="line">
        <TabsTrigger value="overview">Overview</TabsTrigger>
        <TabsTrigger value="nodes">Nodes</TabsTrigger>
        <TabsTrigger value="settings">Settings</TabsTrigger>
      </TabsList>
      <TabsContent value="overview">Cluster overview panel.</TabsContent>
      <TabsContent value="nodes">Node list panel.</TabsContent>
      <TabsContent value="settings">Cluster settings panel.</TabsContent>
    </Tabs>
  ),
};
