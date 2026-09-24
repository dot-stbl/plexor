import { StrictMode } from 'react';
import { createRoot, hydrateRoot } from 'react-dom/client';
import { RouterProvider, createRouter } from '@tanstack/react-router';

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
  defaultNotFoundComponent: DocsNotFound,
});

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Root element #root not found');

const tree = (
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>
);

/**
 * Prerendered pages (every real route after the build pipeline's
 * `scripts/prerender/run.ts` runs) leave real React-rendered HTML
 * inside `#root` — `hydrateRoot` reattaches event handlers and
 * keeps the DOM. `dist/404.html` is built with an empty `#root`
 * on purpose so it falls through to the original `createRoot`
 * path: a pure-CSR fallback for any URL nginx/Caddy hasn't mapped
 * to a real page (see AGENTS.md §9 — the operator-side routing
 * config lands when production hosting does).
 */
if (rootElement.hasChildNodes()) {
  /**
   * Preload each matched route's code-split `component` chunk
   * (`autoCodeSplitting`, vite.config.ts) before hydrating, so a slow
   * chunk fetch can't make a NESTED lazy route suspend on the client's
   * first hydration pass where the server already rendered real content.
   * `router.loadRouteChunk` is the same public method `router.load()`
   * calls internally (idempotent — returns the already-cached promise).
   *
   * KNOWN LIMITATION (not fixed by the preload above): TanStack Router's
   * root `<Outlet/>` unconditionally wraps its child match in
   * `<Suspense fallback={null}>` (`Match.js`, `matchId === rootRouteId`)
   * regardless of code-splitting. On this pinned version (1.91.0) that
   * root-level boundary itself fails to hydrate against
   * `renderToReadableStream` output — a confirmed upstream bug
   * (github.com/TanStack/router issue #3305 / PR #8484 area), fixed only
   * in the newer `@tanstack/react-router/ssr/*` API (`^1.170`+), which
   * requires `__root.tsx` to own the whole `<html>` document — a
   * TanStack-Start-shaped restructure explicitly out of scope here. The
   * effect is a single console hydration-mismatch warning per page load;
   * React recovers by regenerating that subtree client-side (confirmed:
   * no user-visible breakage, correct content, all interactivity works).
   * SSR/SEO output (what crawlers see) is entirely unaffected — this is
   * a client-side-only symptom. Revisit if/when the router version bump
   * gets its own dedicated, tested migration.
   */
  void router.load().then(async () => {
    const routes = Object.values(router.routesById);
    await Promise.all(
      router.state.matches.map((match) => {
        const route = routes.find((candidate) => candidate.id === match.routeId);
        return route ? router.loadRouteChunk(route) : Promise.resolve();
      }),
    );
    hydrateRoot(rootElement, tree);
  });
} else {
  createRoot(rootElement).render(tree);
}
