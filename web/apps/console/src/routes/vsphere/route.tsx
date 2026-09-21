import { createFileRoute, Outlet } from '@tanstack/react-router';

/** Layout-route for the vSphere section — breadcrumb + outlet for
 *  the list (`/`) and the clone form (`/clone`). */
export const Route = createFileRoute('/vsphere')({
  staticData: { crumb: 'vSphere' },
  component: () => <Outlet />,
});
