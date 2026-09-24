import { Outlet, createFileRoute } from '@tanstack/react-router';
import { DocsPrevNext } from '@/components/docs/docs-prev-next';

/**
 * Inner docs layout — wraps every `/docs/*` page with the prev/next pager
 * (spec §4.6) without touching ~40 individual page files or the outer
 * `(docs)/route.tsx` (foundation-owned: `SiteHeader`/`SiteFooter`/sidebar/
 * TOC/`DocsFooter` chrome — untouched here).
 *
 * This file lives inside the real `docs/` path segment (not a pathless
 * `(group)`), so it becomes the layout route for every `/docs/*` URL —
 * same pattern as console's `src/routes/vms/route.tsx`. `DocsPrevNext`
 * renders after the article `<Outlet/>`, still inside the outer layout's
 * `<main>` column, above `DocsFooter`.
 */
export const Route = createFileRoute('/(docs)/docs')({
  component: DocsPagerLayout,
});

function DocsPagerLayout() {
  return (
    <>
      <Outlet />
      <DocsPrevNext />
    </>
  );
}
