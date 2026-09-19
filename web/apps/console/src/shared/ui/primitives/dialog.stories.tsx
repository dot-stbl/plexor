import type { Meta, StoryObj } from '@storybook/react-vite';

import { Button } from './button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from './dialog';

const meta = {
  title: 'Primitives/Dialog',
  component: Dialog,
  parameters: { layout: 'fullscreen' },
} satisfies Meta<typeof Dialog>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Open: Story = {
  render: () => (
    <Dialog open>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete cluster</DialogTitle>
          <DialogDescription>
            This permanently removes the cluster, its workloads, and all attached
            volumes. This action cannot be undone.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline">Cancel</Button>
          <Button variant="destructive">Delete</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  ),
};

export const WithoutCloseButton: Story = {
  render: () => (
    <Dialog open dismissible={false}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Maintenance in progress</DialogTitle>
          <DialogDescription>
            The control plane is applying a migration. Wait for it to finish —
            this dialog closes automatically.
          </DialogDescription>
        </DialogHeader>
      </DialogContent>
    </Dialog>
  ),
};

export const WithTrigger: Story = {
  render: () => (
    <DialogTrigger>
      <Button variant="outline">Open dialog</Button>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create folder</DialogTitle>
          <DialogDescription>
            Folders scope resources inside a team — dev, staging, prod, or a
            project namespace.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter showCloseButton>
          <Button>Create</Button>
        </DialogFooter>
      </DialogContent>
    </DialogTrigger>
  ),
};
