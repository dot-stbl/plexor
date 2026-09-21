import { Link, useMatches } from '@tanstack/react-router';
import type { ReactNode } from 'react';

/**
 * Breadcrumb — the parent chain of the current page, drawn from
 * `useMatches()` so we don't have to hand-thread a "current section"
 * prop through every route component. Each match with a pathname
 * becomes one crumb; the root segment and the pathless `(docs)` group
 * are filtered out (the group's pathname is `/docs`, which we keep as
 * a static link).
 *
 * Visual: monospaced text nav with `›` chevrons between crumbs. The
 * final crumb is the current page, rendered without a trailing chevron
 * and styled muted-foreground so the eye lands on it last.
 */
export function DocsBreadcrumb({ className }: { className?: string }): ReactNode {
  const matches = useMatches();

  const crumbs = matches
    .map((match) => buildCrumb(match.id, match.pathname))
    .filter((crumb): crumb is Crumb => crumb !== null);

  if (crumbs.length === 0) {
    return <div className={className} aria-hidden="true" />;
  }

  return (
    <nav aria-label="Breadcrumb" className={className}>
      <ol className="flex items-center gap-1.5">
        {crumbs.map((crumb, index) => {
          const isLast = index === crumbs.length - 1;
          return (
            <li key={crumb.label} className="flex items-center gap-1.5">
              {crumb.to && !isLast ? (
                <Link
                  to={crumb.to}
                  className="truncate text-muted-2 transition-colors duration-fast ease-out hover:text-foreground"
                >
                  {crumb.label}
                </Link>
              ) : (
                <span
                  aria-current={isLast ? 'page' : undefined}
                  className="truncate text-foreground"
                >
                  {crumb.label}
                </span>
              )}
              {!isLast && (
                <span aria-hidden="true" className="text-muted-2/60">
                  ›
                </span>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

interface Crumb {
  readonly label: string;
  readonly to?: string;
}

/**
 * Map a TR match id + pathname to a breadcrumb entry. The root match
 * (`__root__`) and the pathless `(docs)` group id (`_docs`) carry no
 * breadcrumb — they're scaffolding, not navigation waypoints. The
 * `/docs` index is rendered as "Docs" so a page like
 * `/docs/concepts` reads `plexor · docs › Concepts`.
 */
function buildCrumb(id: string, pathname: string): Crumb | null {
  if (id === '__root__') return null;
  if (id === '/(docs)') return null;
  if (id === '/(docs)/docs/') return null;

  if (pathname === '/docs') return { label: 'Docs', to: '/docs' };

  if (pathname === '/docs/getting-started') {
    return { label: 'Getting started' };
  }
  if (pathname === '/docs/concepts' || pathname.startsWith('/docs/concepts/')) {
    return { label: 'Concepts' };
  }

  return null;
}