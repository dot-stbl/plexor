import { Link, useRouterState } from '@tanstack/react-router';
import type { ReactNode } from 'react';

/**
 * Breadcrumb — the parent chain of the current page, drawn from the
 * router's pathname so we don't have to hand-thread a "current section"
 * prop through every route component. Two segments: "Docs" (always,
 * for `/docs/*` paths) and the current chapter label.
 *
 * The chapter slug → label map mirrors the sidebar's CHAPTERS list. If
 * the page is the chapter's index, the chapter segment IS the current
 * crumb and there's no "current page" tail (the operator reads
 * `Docs › Concepts`, not `Docs › Concepts › Concepts`).
 *
 * Visual: monospaced text nav with `›` chevrons between crumbs. The
 * final crumb is the current page, rendered without a trailing chevron
 * and styled muted-foreground so the eye lands on it last.
 */
const CHAPTER_LABELS: ReadonlyMap<string, string> = new Map([
  ['/docs/getting-started', 'Getting started'],
  ['/docs/concepts', 'Concepts'],
  ['/docs/how-to', 'How-to'],
  ['/docs/admin', 'Admin'],
  ['/docs/reference', 'Reference'],
  ['/docs/faq', 'FAQ / Troubleshooting'],
]);

interface Crumb {
  readonly label: string;
  readonly to?: string;
}

export function DocsBreadcrumb({ className }: { className?: string }): ReactNode {
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  });

  const crumbs = buildCrumbs(pathname);

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

function buildCrumbs(pathname: string): readonly Crumb[] {
  const crumbs: Crumb[] = [];

  if (pathname.startsWith('/docs') && pathname !== '/') {
    crumbs.push({ label: 'Docs', to: '/docs' });
  }

  const segments = pathname.split('/').filter(Boolean);
  if (segments[0] !== 'docs' || segments.length < 2) {
    return crumbs;
  }

  const chapterSlug = `/docs/${segments[1]}`;
  const chapterLabel = CHAPTER_LABELS.get(chapterSlug) ?? segments[1];

  // On a chapter's index page, the chapter segment is the current page
  // (no extra tail). On a sub-page, it becomes a parent crumb and the
  // tail becomes the page title slug — operator-voice labels live in
  // the MDX frontmatter, not in the breadcrumb map.
  const onChapterIndex = segments.length === 2;
  if (onChapterIndex) {
    crumbs.push({ label: chapterLabel });
    return crumbs;
  }

  crumbs.push({ label: chapterLabel, to: chapterSlug });
  return crumbs;
}
