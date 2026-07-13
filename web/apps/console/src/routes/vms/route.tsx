import { createFileRoute, Outlet } from '@tanstack/react-router';

/** Layout-роут раздела ВМ: крошка «Виртуальные машины» + `<Outlet/>` для
 *  списка (`index`) и создания (`new`). См. web-frontend.md rule 57-58. */
export const Route = createFileRoute('/vms')({
  staticData: { crumb: 'Virtual machines' },
  component: () => <Outlet />,
});
