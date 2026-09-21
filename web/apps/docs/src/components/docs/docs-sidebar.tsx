import { Link, useRouterState } from '@tanstack/react-router';
import type { ReactNode } from 'react';

/**
 * Docs sidebar — a vertical chapter list (numbered eyebrow + label),
 * one entry per top-level docs section. v1 ships five chapters; four
 * are placeholders (`soon` eyebrow, no link) until content lands.
 *
 * The active state is computed from the current pathname: exact match
 * for the chapter's URL prefix. We don't walk the TR tree — the
 * chapter list is local on purpose. Future work that needs dynamic
 * chapter loading (per chapter has children) replaces this with the
 * TR `useMatches()` walk; v1 doesn't need it.
 *
 * Width: fixed (w-56) on desktop, hidden on mobile. The header's
 * breadcrumb takes the mobile role for navigation context.
 */
const CHAPTERS: readonly Chapter[] = [
  { index: 1, label: 'Getting started', to: '/docs/getting-started' },
  { index: 2, label: 'Concepts', to: '/docs/concepts' },
  { index: 3, label: 'Marketplace', soon: true },
  { index: 4, label: 'Operations', soon: true },
  { index: 5, label: 'Reference', soon: true },
];

interface Chapter {
  readonly index: number;
  readonly label: string;
  readonly to?: string;
  readonly soon?: boolean;
}

export function DocsSidebar(): ReactNode {
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  });

  return (
    <aside className="hidden w-56 shrink-0 md:block">
      <nav aria-label="Documentation chapters" className="sticky top-20">
        <div className="mb-3 font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
          Chapters
        </div>
        <ol className="space-y-0.5">
          {CHAPTERS.map((chapter) => {
            const num = String(chapter.index).padStart(2, '0');
            const active = isActiveChapter(pathname, chapter);

            if (chapter.soon || !chapter.to) {
              return (
                <li
                  key={chapter.label}
                  className="flex items-center gap-2 rounded-md px-2 py-1.5 text-sm text-muted-2/60"
                >
                  <span className="font-mono text-[10px] tracking-[0.12em] text-muted-2/50">
                    {num}
                  </span>
                  <span>{chapter.label}</span>
                  <span className="ml-auto font-mono text-[10px] uppercase tracking-[0.12em] text-muted-2/40">
                    soon
                  </span>
                </li>
              );
            }

            return (
              <li key={chapter.label}>
                <Link
                  to={chapter.to}
                  className={`flex items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors duration-fast ease-out ${
                    active
                      ? 'bg-muted font-medium text-foreground'
                      : 'text-muted-2 hover:bg-muted/60 hover:text-foreground'
                  }`}
                  aria-current={active ? 'page' : undefined}
                >
                  <span className="font-mono text-[10px] tracking-[0.12em] text-muted-2/80">
                    {num}
                  </span>
                  <span>{chapter.label}</span>
                </Link>
              </li>
            );
          })}
        </ol>
      </nav>
    </aside>
  );
}

/**
 * Active state: the chapter's URL is a prefix of the current pathname
 * (e.g. /docs/concepts/* keeps the Concepts entry highlighted).
 * `/docs/getting-started` doesn't activate Concepts, even though it's
 * structurally under /docs.
 */
function isActiveChapter(pathname: string, chapter: Chapter): boolean {
  if (!chapter.to) return false;
  if (pathname === chapter.to) return true;
  return pathname.startsWith(`${chapter.to}/`);
}