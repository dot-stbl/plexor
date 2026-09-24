import { createRootRoute, Outlet, useLocation } from '@tanstack/react-router';
import { AppShell } from '@/shared/ui/app-shell';
import { useLauncherSummary } from '@/domains/dashboard';

/**
 * Root layout — wraps every route in the AppShell (sidebar + header +
 * scrollable content slot) except the public `/login` route, which
 * renders full-viewport without chrome so the credentials card is the
 * single focus of the page.
 *
 * Fetches the launcher SUMMARY cards here (the one place allowed to
 * import a domain into the shell) and passes them down as a prop —
 * `AppShell` → `AppSidebar` → `AppLauncher` never import a domain or a
 * mock module themselves (see
 * .agents/docs/architecture/frontend-ddd.md §3).
 */
export const Route = createRootRoute({
  component: RootComponent,
});

function RootComponent() {
  const { pathname } = useLocation();
  const isLogin = pathname === '/login';
  const launcherSummary = useLauncherSummary();

  if (isLogin) {
    return <Outlet />;
  }

  return (
    <AppShell launcherSummary={launcherSummary}>
      <Outlet />
    </AppShell>
  );
}
