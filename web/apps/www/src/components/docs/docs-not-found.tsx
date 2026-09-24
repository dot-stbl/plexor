import { Link } from '@tanstack/react-router';
import type { ReactNode } from 'react';

/**
 * Docs not-found surface — rendered inside the docs layout's
 * `<Outlet />` slot (`src/routes/(docs)/route.tsx`) whenever a path
 * under `/docs/...` fails to match any registered route. The
 * layout's `<main>` already provides `flex-1 min-w-0`, so this
 * component only owns vertical centering inside that column.
 *
 * Layout notes (why this is not a card):
 *  - The docs chrome is a three-column grid (sidebar / main / TOC).
 *    A 60vh min-height on the inner element makes the 404 feel
 *    like a small floating card inside the middle column, with
 *    unrelated chrome around it.
 *  - The new layout stretches the middle column to fill the
 *    available viewport vertically (60vh minimum, full height
 *    when the sidebar/TOC are tall enough) and centers the
 *    content block within that height.
 *  - No card wrapper. The brief was "just centered, no card" —
 *    the page reads as an empty slot, not a widget.
 *  - Plexor DS tokens only (`text-foreground`, `text-muted-foreground`,
 *    `bg-foreground`, etc.). No `fumadocs-ui` `buttonVariants` and
 *    no `text-fd-*` classes — neither exists in this project.
 *
 * Hierarchy:
 *   1. Large "404" glyph (muted, monospace)
 *   2. <h1> "Page not found"
 *   3. operator-voice subtitle
 *   4. two CTAs (primary "Back to Getting started" + ghost
 *      "Open the landing" with the external-link glyph)
 */
export function DocsNotFound(): ReactNode {
  return (
    <div
      aria-labelledby="docs-not-found-title"
      className="flex min-h-[60vh] items-center justify-center px-6 py-12"
      data-od-id="docs-not-found"
    >
      <div className="w-full max-w-xl text-center">
        <p
          aria-hidden="true"
          className="mb-3 font-mono text-7xl font-semibold tracking-tight text-muted-2"
        >
          404
        </p>

        <h1
          id="docs-not-found-title"
          className="mb-3 text-2xl font-semibold tracking-tight text-foreground"
        >
          Page not found
        </h1>

        <p className="mx-auto mb-8 max-w-md text-sm leading-relaxed text-muted-foreground">
          The page you were looking for might have been removed,
          renamed, or is temporarily unavailable.
        </p>

        <div className="flex flex-wrap items-center justify-center gap-3">
          <Link
            to="/docs/getting-started"
            className="inline-flex items-center gap-2 rounded-md bg-foreground px-4 py-2 text-sm font-medium text-background transition-colors duration-fast ease-out hover:bg-foreground/90"
          >
            Back to Getting started
          </Link>
          <Link
            to="/"
            className="inline-flex items-center gap-2 rounded-md border border-border bg-background px-4 py-2 text-sm font-medium text-foreground transition-colors duration-fast ease-out hover:bg-muted"
          >
            Open the landing
            <span aria-hidden="true" className="font-mono text-xs leading-none">
              ↗
            </span>
          </Link>
        </div>
      </div>
    </div>
  );
}