import { createRootRoute, Outlet, useLocation } from '@tanstack/react-router';
import { AppShell } from '@/shared/ui/app-shell';

/**
 * Root layout — wraps every route in the AppShell (sidebar + header +
 * scrollable content slot) except the public `/login` route, which
 * renders full-viewport without chrome so the credentials card is the
 * single focus of the page.
 */
export const Route = createRootRoute({
  component: RootComponent,
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
