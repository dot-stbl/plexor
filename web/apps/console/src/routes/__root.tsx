import { createRootRoute, Outlet, useLocation } from '@tanstack/react-router';
import { AppShell } from '@/shared/ui/app-shell';
import { NotFound } from '@/shared/ui/primitives/not-found';

/**
 * Root layout — wraps every route in the AppShell (sidebar + header +
 * scrollable content slot) except the public `/login` route, which
 * renders full-viewport without chrome so the credentials card is the
 * single focus of the page.
 *
 * `notFoundComponent` covers URLs that match no registered route —
 * TanStack Router renders it inside the same `RootComponent` chrome,
 * so the 404 lives inside the AppShell (sidebar + header) like any
 * other console page.
 */
export const Route = createRootRoute({
  component: RootComponent,
  notFoundComponent: NotFound,
});

function RootComponent() {
  const { pathname } = useLocation();
  const isLogin = pathname === '/login';

  if (isLogin) {
    return <Outlet />;
  }

  return (
    <AppShell>
      <Outlet />
    </AppShell>
  );
}
