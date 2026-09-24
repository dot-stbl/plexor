import { Link, useRouterState } from '@tanstack/react-router';
import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { flattenPages, type DocsPage } from './docs-chapters';

export interface PagerNeighbors {
  readonly prev: DocsPage | null;
  readonly next: DocsPage | null;
}

/**
 * Pure: given the flattened page order and the current pathname, finds the
 * prev/next neighbors. Returns `{ prev: null, next: null }` when the
 * pathname isn't in the list (e.g. a page not yet registered in
 * `CHAPTERS`) — the pager then renders nothing rather than guessing.
 */
export function getPagerNeighbors(
  pages: readonly DocsPage[],
  pathname: string,
): PagerNeighbors {
  const index = pages.findIndex((page) => page.slug === pathname);
  if (index === -1) return { prev: null, next: null };
  return {
    prev: index > 0 ? pages[index - 1] : null,
    next: index < pages.length - 1 ? pages[index + 1] : null,
  };
}

/**
 * Prev/next pager (spec §4.6) — mounted once, at the bottom of every doc
 * article, by the inner `(docs)/docs/route.tsx` layout (above
 * `DocsFooter`). Derives neighbors from `flattenPages()` (`./docs-chapters`)
 * so the pager and the sidebar read the same page order.
 *
 * First page: no "previous" card, single column, "Next" right-aligned.
 * Last page: no "next" card, single column. A page missing from
 * `CHAPTERS` (or the redirect-only `/docs` index) renders nothing.
 */
export function DocsPrevNext(): ReactNode {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const { prev, next } = getPagerNeighbors(flattenPages(), pathname);

  if (!prev && !next) return null;

  return (
    <nav aria-label="Pager" className="docs-prose mt-12">
      <div
        className={cn(
          'grid gap-4 border-t border-border pt-8',
          prev && next ? 'grid-cols-2' : 'grid-cols-1',
        )}
      >
        {prev && <PagerCard page={prev} direction="prev" />}
        {next && <PagerCard page={next} direction="next" />}
      </div>
    </nav>
  );
}

interface PagerCardProps {
  readonly page: DocsPage;
  readonly direction: 'prev' | 'next';
}

function PagerCard({ page, direction }: PagerCardProps): ReactNode {
  const isNext = direction === 'next';
  return (
    <Link
      to={page.slug}
      className={cn(
        'group rounded-lg border border-border p-4 text-sm transition-colors duration-fast ease-out hover:border-border-2 hover:bg-muted/40',
        isNext && 'text-right',
      )}
    >
      <span className="mb-1 block font-mono text-[10px] uppercase tracking-[0.12em] text-muted-2 group-hover:text-foreground">
        {isNext ? 'Next' : 'Previous'}
      </span>
      <span className="font-medium text-foreground">
        {isNext ? `${page.title} →` : `← ${page.title}`}
      </span>
    </Link>
  );
}
