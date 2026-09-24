import { Outlet, createRootRoute } from '@tanstack/react-router';
import { CommandMenuProvider } from '@/components/chrome/command-menu-store';
import { CommandMenu } from '@/components/chrome/command-menu';

/**
 * Root layout — owns no page chrome (header/footer/sidebar stay in the
 * two pathless route groups). It DOES mount `CommandMenuProvider` +
 * `CommandMenu` once, globally, so `⌘K` / `Ctrl K` works identically on
 * `/` and every `/docs/*` page (spec §2.4) — both route groups render
 * below this `<Outlet/>`, so their headers' search triggers share the
 * same open state via `useCommandMenu()`.
 */
export const Route = createRootRoute({
  component: RootLayout,
});

function RootLayout() {
  return (
    <CommandMenuProvider>
      <Outlet />
      <CommandMenu />
    </CommandMenuProvider>
  );
}
