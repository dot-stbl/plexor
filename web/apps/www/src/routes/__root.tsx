import { Outlet, createRootRoute } from '@tanstack/react-router';
import { CommandMenuProvider } from '@/components/chrome/command-menu-store';
import { CommandMenu } from '@/components/chrome/command-menu';
import { useSyncDocumentHead } from '@/lib/use-sync-document-head';

/**
 * Root layout — owns no page chrome (header/footer/sidebar stay in the
 * two pathless route groups). It DOES mount `CommandMenuProvider` +
 * `CommandMenu` once, globally, so `⌘K` / `Ctrl K` works identically on
 * `/` and every `/docs/*` page (spec §2.4) — both route groups render
 * below this `<Outlet/>`, so their headers' search triggers share the
 * same open state via `useCommandMenu()`.
 *
 * Also hosts `useSyncDocumentHead()` — the client-side counterpart to
 * the prerender script's server-side `head()` collection: keeps
 * `<title>`, description, OG/Twitter, and `<link rel="canonical">`
 * in sync after every client-side `<Link>` navigation. TanStack
 * Router 1.91 doesn't ship `HeadContent`, so the hook is the only
 * thing keeping post-hydration navigations from getting stuck on
 * the first prerendered page's head tags.
 */
export const Route = createRootRoute({
  component: RootLayout,
});

function RootLayout() {
  useSyncDocumentHead();
  return (
    <CommandMenuProvider>
      <Outlet />
      <CommandMenu />
    </CommandMenuProvider>
  );
}
