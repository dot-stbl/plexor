import { Link } from '@tanstack/react-router';
import type { ReactNode } from 'react';

/**
 * Docs not-found panel — Plexor-styled 404 surface rendered inside the
 * docs chrome (header / sidebar / footer) whenever a path under
 * `/docs/...` fails to match any registered route.
 *
 * Layout: a centred card inside the prose column. The outer wrapper
 * vertically centres the panel in `min-h-[60vh]` so the article slot
 * reads as a quiet "this URL didn't match anything" — not a full-bleed
 * error screen. The inner wrapper carries the `.docs-prose` typography
 * so the page inherits the same headings, paragraphs, and link styles
 * as the surrounding articles.
 *
 * Two CTAs land the operator somewhere useful — the docs Getting
 * started (most likely intent) and the marketing landing (full escape
 * hatch). Both use `Link` from `@tanstack/react-router` so navigation
 * stays inside the SPA without a page reload.
 *
 * The file is plain TSX with no client interactivity, so no `'use client'`
 * boundary is needed. It is referenced from `src/routes/(docs)/route.tsx`
 * via the `notFoundComponent` route option — TanStack Router renders it
 * inside the docs layout's `<Outlet />` slot when no child route matches.
 */
export function DocsNotFound(): ReactNode {
  return (
    <section
      aria-labelledby="docs-not-found-title"
      className="docs-prose flex min-h-[60vh] items-center justify-center py-16"
      data-od-id="docs-not-found"
    >
      <div className="mx-auto w-full max-w-2xl rounded-xl border border-border bg-card p-8 text-center">
        <p
          aria-hidden="true"
          className="font-mono text-[7.5rem] font-semibold leading-none tracking-tight text-muted-2"
        >
          404
        </p>

        <h1
          id="docs-not-found-title"
          className="mt-6 text-3xl font-semibold tracking-tight text-foreground"
        >
          Page not found
        </h1>

        <p className="mt-3 text-base text-muted-foreground">
          The URL you opened doesn't match a page in this guide. The
          section may have been renamed, moved, or there's a typo in the
          address.
        </p>

        <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
          <Link
            to="/docs/getting-started"
            className="inline-flex items-center gap-2 rounded-md bg-foreground px-4 py-2 text-sm font-medium text-background transition-colors duration-fast ease-out hover:bg-foreground/90"
          >
            Back to Getting started
            <span aria-hidden="true">→</span>
          </Link>
          <Link
            to="/"
            className="inline-flex items-center gap-2 rounded-md border border-border bg-background px-4 py-2 text-sm font-medium text-foreground transition-colors duration-fast ease-out hover:bg-muted"
          >
            Open the landing
            <span aria-hidden="true">↗</span>
          </Link>
        </div>
      </div>
    </section>
  );
}
