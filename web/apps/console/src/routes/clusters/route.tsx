import { createFileRoute, Outlet } from '@tanstack/react-router';

/** Layout-роут раздела серверов: крошка «Серверы» + `<Outlet/>` для списка
 *  (`index`) и деталей (`$id`). См. web-frontend.md rule 57-58. */
export const Route = createFileRoute('/clusters')({
  staticData: { crumb: 'Servers' },
  component: () => <Outlet />,
});
