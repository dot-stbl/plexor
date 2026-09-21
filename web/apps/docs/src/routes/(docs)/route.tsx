import { Outlet, createFileRoute } from '@tanstack/react-router';
import { DocsHeader } from '@/components/docs/docs-header';
import { DocsSidebar } from '@/components/docs/docs-sidebar';
import { DocsToc } from '@/components/docs/docs-toc';
import { DocsFooter } from '@/components/docs/docs-footer';

/**
 * Docs layout — the three-column chrome (sidebar + article + TOC)
 * with a sticky header and a thin footer underneath. The header owns
 * the breadcrumb + theme picker; the sidebar owns the chapter list;
 * the TOC scrapes h2/h3 from the rendered article; the footer owns
 * the "Edit on GitHub" + "Last updated" links.
 *
 * The `(docs)` group is pathless in TanStack Router terms (the
 * parenthesis-prefixed directory is collapsed from the URL), so the
 * route id keeps the parenthesised directory name verbatim —
 * `/(docs)`. Real URLs come from the inner `docs/` folder:
 * `/docs/getting-started`, `/docs/concepts`, etc.
 */
export const Route = createFileRoute('/(docs)')({
  component: DocsLayout,
});

function DocsLayout() {
  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <DocsHeader />
      <div className="mx-auto flex w-full max-w-7xl flex-1 gap-8 px-6 py-8">
        <DocsSidebar />
        <main className="min-w-0 flex-1">
          <Outlet />
        </main>
        <DocsToc />
      </div>
      <DocsFooter />
    </div>
  );
}