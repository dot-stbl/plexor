import { useRouterState } from '@tanstack/react-router';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { CHAPTERS, flattenPages, type DocsChapter, type DocsPage } from './docs-chapters';
import { ChapterLink } from './docs-sidebar-chapter';

export { CHAPTERS, flattenPages };
export type { DocsChapter, DocsPage };

/**
 * Docs sidebar — a vertical chapter list (numbered eyebrow + label), one
 * entry per top-level docs section. Chapter data lives in `./docs-chapters`
 * (`CHAPTERS`, `flattenPages`) so it's unit-testable without React; this
 * file owns the interactive shell only: auto-expand-active-chapter state,
 * sticky positioning under the header, and delegating each row to
 * `ChapterLink` (`./docs-sidebar-chapter`, backed by the `Collapsible`
 * primitive).
 *
 * The active chapter (the chapter the current route lives in) auto-expands
 * even if the operator previously collapsed it. Collapse state for other
 * chapters is preserved across renders — navigating back to a previously
 * collapsed chapter keeps it collapsed.
 *
 * Width: fixed (w-56) on desktop, hidden on mobile — the header's
 * breadcrumb carries mobile wayfinding. Sticky offset (`top-20`) clears the
 * shared `h-14` `SiteHeader` plus a small gap.
 */
export function DocsSidebar(): ReactNode {
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  });

  const activeChapter = CHAPTERS.find((chapter) => isActiveChapter(pathname, chapter));

  // Auto-expand the active chapter on mount and on every route change.
  // Collapse state for non-active chapters survives across renders.
  const [openSlugs, setOpenSlugs] = useState<readonly string[]>(() =>
    activeChapter ? [activeChapter.slug] : [],
  );

  // Track the last auto-expanded slug so the effect runs only when the
  // active chapter actually changes — not on every render.
  const lastAutoSlugRef = useRef<string | null>(null);
  useEffect(() => {
    if (!activeChapter) return;
    if (lastAutoSlugRef.current === activeChapter.slug) return;
    lastAutoSlugRef.current = activeChapter.slug;
    setOpenSlugs((prev) =>
      prev.includes(activeChapter.slug) ? prev : [...prev, activeChapter.slug],
    );
  }, [activeChapter]);

  return (
    <aside className="hidden w-56 shrink-0 md:block">
      <nav aria-label="Documentation chapters" className="sticky top-20">
        <div className="mb-3 font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
          Chapters
        </div>
        <ol className="space-y-0.5">
          {CHAPTERS.map((chapter) => {
            const num = String(chapter.index).padStart(2, '0');
            const isOpen = openSlugs.includes(chapter.slug);
            return (
              <li key={chapter.slug}>
                <ChapterLink
                  chapter={chapter}
                  num={num}
                  pathname={pathname}
                  active={isActiveChapter(pathname, chapter)}
                  expanded={isOpen}
                  onExpandedChange={(next) => {
                    setOpenSlugs((prev) =>
                      next
                        ? prev.includes(chapter.slug)
                          ? prev
                          : [...prev, chapter.slug]
                        : prev.filter((slug) => slug !== chapter.slug),
                    );
                  }}
                />
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
function isActiveChapter(pathname: string, chapter: DocsChapter): boolean {
  if (pathname === chapter.slug) return true;
  return pathname.startsWith(`${chapter.slug}/`);
}
