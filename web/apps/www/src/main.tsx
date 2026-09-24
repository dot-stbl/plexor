import { StrictMode } from 'react';
import { createRoot, hydrateRoot } from 'react-dom/client';
import { RouterProvider, createRouter } from '@tanstack/react-router';
import { RouterClient } from '@tanstack/react-router/ssr/client';

import { routeTree } from './routeTree.gen';

import './styles.css';
import { applyBootPreset } from './lib/apply-theme';
import { DocsNotFound } from '@/components/docs/docs-not-found';

/**
 * Apply the persisted/auto theme preset BEFORE first render so the docs
 * site ships its branded chrome (matched OKLCH values) without a flash.
 * Mirrors the inline boot script in `index.html` — the inline script
 * stamps `.dark` and `data-theme-mode` for the no-FOUC first paint;
 * here we apply the full token set so the full Plexor DS vocabulary
 * (surfaces, ink, borders, status semantics) lands on the root element
 * before any React component reads it. Both paths use the same
 * localStorage key (`plexor-theme`) so they agree.
 *
 * Unconditional + targets `<html>`, OUTSIDE the hydrated `#root`
 * subtree — can't cause a hydration mismatch even when the prerendered
 * HTML re-applies the same tokens. Lives ABOVE the
 * `hasChildNodes()` branch on purpose.
 */
applyBootPreset();

const router = createRouter({
  routeTree,
  defaultPreload: 'intent',
  // Matches `entry-server.tsx`'s router exactly — every prerendered page's
  // dehydrated matches were computed against `trailingSlash: 'preserve'`,
  // so the client must resolve the same canonical form when it re-matches
  // candidates during hydration (see `entry-server.tsx`'s comment for why
  // the router's `'never'` default breaks matching for our trailing-slash
  // leaf paths).
  trailingSlash: 'preserve',
  defaultNotFoundComponent: DocsNotFound,
});

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Root element #root not found');

/**
 * Prerendered pages (every real route after the build pipeline's
 * `scripts/prerender/run.ts` runs) leave real React-rendered HTML
 * inside `#root`, plus the `<Scripts/>`-rendered `window.$_TSR`
 * dehydration payload (`__root.tsx`, `entry-server.tsx`) — `RouterClient`
 * + `hydrateRoot` reattach event handlers and keep the DOM. `dist/404.html`
 * is built with an empty `#root` (no dehydration payload either) on
 * purpose so it falls through to the plain `createRoot` path: a pure-CSR
 * fallback for any URL nginx/Caddy hasn't mapped to a real page (see
 * AGENTS.md §9 — the operator-side routing config lands when production
 * hosting does). Dev mode (`bun run dev`) also has an empty `#root` (no
 * prerender pass runs), so it takes the same plain-CSR branch.
 *
 * `RouterClient` (official `@tanstack/react-router` SSR API — see
 * `entry-server.tsx`'s `attachRouterServerSsrUtils` comment) reads that
 * dehydrated payload via its internal `hydrate()`, which — unlike the
 * router-only `<RouterProvider>` path below — sets `router.ssr` on the
 * client BEFORE the routed tree mounts, matching what the server set via
 * `attachRouterServerSsrUtils`. That symmetry is what fixes the root
 * `<Outlet/>` hydration mismatch (upstream TanStack/router#3305 / #4495):
 * `Match.js` now skips wrapping the root match in `<Suspense>` on BOTH
 * sides instead of only server-side. `hydrate()` also preloads every
 * matched route's code-split `component` chunk itself (`autoCodeSplitting`,
 * vite.config.ts) before resolving, so the manual `loadRouteChunk`
 * preloading this file used to do by hand is no longer needed.
 */
if (rootElement.hasChildNodes()) {
  hydrateRoot(
    rootElement,
    <StrictMode>
      <RouterClient router={router} />
    </StrictMode>,
  );
} else {
  createRoot(rootElement).render(
    <StrictMode>
      <RouterProvider router={router} />
    </StrictMode>,
  );
}
