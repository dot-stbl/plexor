import type { ReactNode } from 'react';

/**
 * Docs not-found panel — Plexor-styled 404 surface rendered inside the
 * docs chrome (header / sidebar / footer) whenever a path under
 * `/docs/...` fails to match any registered route.
 *
 * Layout: a centred column with the same `max-w-44rem` reading width
 * as the article body, so a 404 reads like the next page rather than
 * a separate full-bleed screen. Two CTAs land the operator somewhere
 * useful — the docs Getting started (most likely intent) and the
 * marketing landing (full escape hatch).
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
      className="docs-prose flex min-h-[60vh] flex-col items-start justify-center"
      data-od-id="docs-not-found"
    >
      <p className="font-mono text-xs font-medium uppercase tracking-[0.18em] text-muted-2">
        Plexor docs · error 404
      </p>

      <p
        aria-hidden="true"
        className="my-6 font-mono text-[7.5rem] font-semibold leading-none tracking-tight text-foreground"
      >
        404
      </p>

      <h1
        id="docs-not-found-title"
        className="text-2xl font-semibold tracking-tight text-foreground"
      >
        Страница не найдена
      </h1>

      <p className="mt-3 text-base text-muted-2">
        Адрес, на который вы перешли, не ведёт ни к одной странице
        документации. Возможно, раздел был переименован, удалён, или
        в адресе опечатка.
      </p>

      <p className="mt-6 text-sm text-muted-2">
        English: the page you were looking for might have been removed,
        renamed, or is temporarily unavailable.
      </p>

      <div className="mt-8 flex flex-wrap gap-3">
        <a
          href="/docs/getting-started/"
          className="inline-flex items-center gap-2 rounded-md bg-foreground px-4 py-2 text-sm font-medium text-background transition-colors duration-fast ease-out hover:bg-foreground/90"
        >
          Начать с Getting started
          <span aria-hidden="true">→</span>
        </a>
        <a
          href="/"
          className="inline-flex items-center gap-2 rounded-md border border-border bg-card px-4 py-2 text-sm font-medium text-foreground transition-colors duration-fast ease-out hover:bg-muted"
        >
          На главную
        </a>
      </div>
    </section>
  );
}
