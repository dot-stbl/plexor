import { StrictMode, type ReactElement } from 'react';
import { renderToReadableStream } from 'react-dom/server';
import {
  RouterProvider,
  createMemoryHistory,
  createRouter,
} from '@tanstack/react-router';
import { attachRouterServerSsrUtils } from '@tanstack/react-router/ssr/server';

import { routeTree } from './routeTree.gen';
import { DocsNotFound } from '@/components/docs/docs-not-found';
import { collectRouteHead } from './lib/collect-route-head';
import { resolveHead, type ResolvedHead } from './lib/head-meta';

const NOT_FOUND_COMPONENT = DocsNotFound;

/**
 * `autoCodeSplitting` (vite.config.ts) makes every route's `component` a
 * lazy chunk that TanStack Router wraps in `<Suspense>` (see
 * `node_modules/@tanstack/react-router/dist/esm/Match.js`, the
 * `React.Suspense` vs `SafeFragment` choice). `renderToString` never emits
 * the `<!--$-->`/`<!--/$-->` boundary comment markers a Suspense boundary
 * needs for `hydrateRoot` to reconcile it later — so the client, expecting
 * a boundary marker, instead finds a plain element and hydration fails on
 * EVERY route (verified: React error #418 on `/`, `/changelog/`, and every
 * `/docs/**` page alike). `renderToPipeableStream` does emit those
 * markers. Uses the Web Streams `renderToReadableStream` (not the Node
 * `renderToPipeableStream`) because this script runs under bun, whose
 * `react-dom/server` export map resolves to its own `server.bun.js` —
 * that build only exports `renderToReadableStream`, not the Node-stream
 * API. Build-time SSG has no request latency to protect, so this awaits
 * `stream.allReady` (fully settled — every Suspense boundary resolved,
 * not just the initial shell) before reading the whole body into a
 * string via the standard `Response` helper.
 */
async function renderToStringWithSuspenseMarkers(element: ReactElement): Promise<string> {
  let caughtError: unknown;
  const stream = await renderToReadableStream(element, {
    onError(error: unknown) {
      caughtError = error;
    },
  });
  await stream.allReady;
  if (caughtError !== undefined) {
    throw caughtError instanceof Error ? caughtError : new Error(String(caughtError));
  }
  return new Response(stream).text();
}

export interface RenderResult {
  readonly appHtml: string;
  readonly finalPathname: string;
  readonly head: ResolvedHead;
}

/**
 * Every real prerenderable leaf path. The filter keeps structural
 * pathless layouts out — `/(docs)` is a pathless group, not a page,
 * and its key (`/docs`, no trailing slash) is intentionally excluded.
 * Real pages end with `/` because every `index.tsx` file under
 * `src/routes/` registers its path with the trailing slash.
 * Stays in sync with the route tree automatically as new pages
 * are added.
 */
export function getAllRoutePaths(): string[] {
  const router = createRouter({
    routeTree,
    trailingSlash: 'preserve',
    defaultNotFoundComponent: NOT_FOUND_COMPONENT,
  });

  // The router keys its internal `routesByPath` map by `trimPathRight(fullPath)`
  // — so the keys carry no leading or trailing slash. But the VALUE at each
  // key is the actual leaf/index route object whose own `.fullPath` already
  // carries the canonical, final trailing-slash URL — that's the one we
  // want to prerender. A trailing-slash fullPath also unconditionally wins
  // the overwrite race when a pathless layout shares a trimmed key with its
  // own index child, so deduping by VALUE-fullPath automatically drops the
  // layout ghost.
  const fullPaths = new Set<string>();
  for (const route of Object.values(router.routesByPath)) {
    if (route && typeof route.fullPath === 'string') {
      fullPaths.add(route.fullPath);
    }
  }
  return Array.from(fullPaths).sort();
}

export async function renderRoute(requestedPath: string): Promise<RenderResult> {
  const history = createMemoryHistory({ initialEntries: [requestedPath] });
  const router = createRouter({
    routeTree,
    history,
    /**
     * Every leaf path we prerender is an index route's `fullPath`, which
     * TanStack Router always ends in `/` (see `getAllRoutePaths()` below).
     * The router's own default (`trailingSlash: 'never'`) now enforces
     * that canonical form during MATCHING itself (not just href
     * generation) — feeding `createMemoryHistory` a path that still has
     * the trailing slash the route tree registered under `router.load()`
     * with the default option produced ZERO matches (confirmed: this is
     * the actual cause of the earlier reverted 1.91->1.170 bump "silently
     * break[ing] SSR rendering" — every non-`/` route rendered empty).
     * `'preserve'` matches hrefs exactly as given, restoring the old
     * (1.91) behavior this whole pipeline was built against.
     */
    trailingSlash: 'preserve',
    defaultNotFoundComponent: NOT_FOUND_COMPONENT,
  });

  /**
   * Official `@tanstack/react-router` SSR API (not TanStack Start —
   * that's a separate, framework-shaped package we're not adopting).
   * `attachRouterServerSsrUtils` wires `router.ssr`/`router.serverSsr`
   * onto this router instance BEFORE anything renders. That flag is
   * what `Match.js`'s `canWrapInSuspense` reads for the ROOT match:
   * `!route.isRoot || ... || !((isServer ?? router.isServer) ||
   * router.ssr)` — for the root route, every clause but the last is
   * false, so root-level Suspense wrapping is skipped only when
   * `router.ssr` is truthy. Without this call (the pre-upgrade code),
   * the root `<Outlet/>` was unconditionally wrapped in
   * `<Suspense fallback={null}>` server-side but NOT client-side,
   * which is the exact hydration mismatch this upgrade fixes
   * (upstream TanStack/router#3305, fixed via #4495). Non-root routes
   * are unaffected either way — `!route.isRoot` alone already made
   * `canWrapInSuspense` true for them, so their own code-split
   * Suspense boundaries already hydrated cleanly against
   * `renderToReadableStream`'s `<!--$-->` markers before this change.
   */
  attachRouterServerSsrUtils({ router, manifest: undefined });

  // `router.load()` resolves the route tree — including any
  // internal `beforeLoad` redirects — so after `await`, the location
  // is the final, post-redirect pathname.
  await router.load();

  /**
   * Dehydrates the (loader-free — this site has none) resolved route
   * matches onto the router's internal SSR-script owner. `<Scripts/>`
   * (rendered inside `__root.tsx`, a descendant of `RouterProvider` so
   * it has router context) pulls this via
   * `router.serverSsr.takeInitialHydrationScriptTags()` during the
   * render below and embeds it as a `window.$_TSR = {...}` payload +
   * bootstrap script INSIDE `#root`'s own subtree — we deliberately did
   * NOT move to the full document-owning `RouterServer`/`RouterClient`
   * request-handler pattern (that requires `__root.tsx` to render the
   * entire `<html>`, a TanStack-Start-shaped restructure of
   * `index.html` this pass doesn't need); `<Scripts/>` doesn't care
   * where in the DOM it lands, only that it renders before the
   * client's module script reads `window.$_TSR` (main.tsx).
   */
  await router.serverSsr?.dehydrate({ signal: new AbortController().signal });

  let appHtml: string;
  try {
    appHtml = await renderToStringWithSuspenseMarkers(
      <StrictMode>
        <RouterProvider router={router} />
      </StrictMode>,
    );
  } finally {
    router.serverSsr?.cleanup();
  }

  const finalPathname = router.state.location.pathname;
  const head = resolveHead(
    collectRouteHead(
      router as unknown as Parameters<typeof collectRouteHead>[0],
      router.state.matches,
    ),
    finalPathname,
  );

  return { appHtml, finalPathname, head };
}
