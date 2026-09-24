import { Outlet, Scripts, createRootRoute } from '@tanstack/react-router';
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
 * in sync after every client-side `<Link>` navigation. This router
 * version has no route-level `<HeadContent/>` auto-wiring into our
 * hand-rolled `index.html` shell (we don't own `<head>` via JSX — see
 * `entry-server.tsx`), so the hook still does this job.
 *
 * `<Scripts/>` (official `@tanstack/react-router` SSR API, not
 * TanStack Start) renders the dehydrated-router payload + hydration
 * bootstrap script server-side, and is a harmless no-op client-side
 * (we register no route-level `scripts` and pass no asset manifest).
 * It must live INSIDE the routed tree (a descendant of `RouterProvider`,
 * which is what `useRouter()` needs) — see `main.tsx`/`entry-server.tsx`
 * for why this is the fix for the root-`<Outlet/>` hydration mismatch
 * (upstream TanStack/router#3305 / #4495).
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
      <Scripts />
    </CommandMenuProvider>
  );
}
