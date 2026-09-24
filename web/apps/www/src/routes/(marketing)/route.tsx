import { Outlet, createFileRoute } from '@tanstack/react-router';
import { SiteHeader } from '@/components/chrome/site-header';
import { SiteFooter } from '@/components/chrome/site-footer';

/**
 * Marketing layout — wraps the `/` route (and `/changelog`) in the
 * shared landing chrome: `SiteHeader variant="marketing"` + free-flowing
 * main + `SiteFooter` (spec §2.4 — one header/footer per route-group
 * layout, not duplicated per page).
 *
 * The `(marketing)` group is pathless (TanStack Router collapses
 * parentheses-prefixed directory segments from the URL), so the
 * landing lives at `/` directly.
 */
export const Route = createFileRoute('/(marketing)')({
  component: MarketingLayout,
});

function MarketingLayout() {
  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <SiteHeader variant="marketing" />
      <main className="flex-1">
        <Outlet />
      </main>
      <SiteFooter />
    </div>
  );
}
