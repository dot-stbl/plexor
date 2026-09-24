import { Outlet, createFileRoute } from '@tanstack/react-router';
import { SiteHeader } from '@/components/chrome/site-header';
import { SiteFooter } from '@/components/chrome/site-footer';
import { FRAME_CLASS } from '@/components/chrome/site-frame';
import { DocsSidebar } from '@/components/docs/docs-sidebar';
import { DocsToc } from '@/components/docs/docs-toc';
import { DocsFooter } from '@/components/docs/docs-footer';
import { DocsNotFound } from '@/components/docs/docs-not-found';

/**
 * Docs layout — the three-column chrome (sidebar + article + TOC),
 * wrapped in the shared `SiteHeader variant="docs"` / `SiteFooter`. The
 * grid shares `FRAME_CLASS` with every other full-width frame in the
 * app (header, footer, marketing `SiteFrame`) — sidebar hugs the left
 * gutter, TOC hugs the right one, and the article column between them
 * grows with the viewport; the article's own `.docs-prose` class (see
 * `src/styles.css`) caps prose at a 44rem readable width so widening
 * the grid doesn't stretch paragraph line length. The page-scoped
 * "Edit on GitHub" + version bar (`DocsFooter`) stays directly under the
 * article, above the full-width `SiteFooter`.
 *
 * The `(docs)` group is pathless in TanStack Router terms (the
 * parenthesis-prefixed directory is collapsed from the URL), so the
 * route id keeps the parenthesised directory name verbatim —
 * `/(docs)`. Real URLs come from the inner `docs/` folder:
 * `/docs/getting-started`, `/docs/concepts`, etc.
 *
 * `notFoundComponent` here handles any unmatched path under
 * `/docs/...` — fuzzy-matched to the docs layout so the operator
 * keeps the sidebar/header chrome instead of seeing TanStack
 * Router's bare `<p>Not Found</p>`. The root route owns the
 * non-docs fallback.
 */
export const Route = createFileRoute('/(docs)')({
  component: DocsLayout,
  notFoundComponent: DocsNotFound,
});

function DocsLayout() {
  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <SiteHeader variant="docs" />
      <div className={`flex flex-1 gap-8 py-8 xl:gap-12 ${FRAME_CLASS}`}>
        <DocsSidebar />
        <main className="min-w-0 flex-1">
          <Outlet />
        </main>
        <DocsToc />
      </div>
      <DocsFooter />
      <SiteFooter />
    </div>
  );
}
