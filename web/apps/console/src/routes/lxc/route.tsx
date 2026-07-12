import { createFileRoute, Outlet } from '@tanstack/react-router';

/** Layout route for the LXC section: breadcrumb «LXC containers» + `<Outlet/>`
 *  for the list (`index`) and creation (`new`). Mirrors `vms/route.tsx`. */
export const Route = createFileRoute('/lxc')({
  staticData: { crumb: 'LXC containers' },
  component: () => <Outlet />,
});
