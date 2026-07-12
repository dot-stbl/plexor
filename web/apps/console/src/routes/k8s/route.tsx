import { createFileRoute, Outlet } from '@tanstack/react-router';

/** Layout route for the Kubernetes section: crumb «Kubernetes» + `<Outlet/>`
 *  for the list (`index`) and creation (`new`). */
export const Route = createFileRoute('/k8s')({
  staticData: { crumb: 'Kubernetes' },
  component: () => <Outlet />,
});
