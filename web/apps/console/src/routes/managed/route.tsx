import { createFileRoute, Outlet } from '@tanstack/react-router';

/**
 * Layout-route раздела «Платформа данных». Даёт крошку раздела (через
 * staticData → AppHeader) и открывает дочерние страницы движков в `<Outlet/>`
 * (web-frontend.md rule 58). Общий чром раздела при необходимости добавляется
 * здесь вокруг Outlet.
 */
export const Route = createFileRoute('/managed')({
  staticData: { crumb: 'Data platform' },
  component: () => <Outlet />,
});
