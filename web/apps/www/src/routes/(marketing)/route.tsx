import { Outlet, createFileRoute } from '@tanstack/react-router';
import { MarketingHeader } from '@/components/marketing/marketing-header';
import { MarketingFooter } from '@/components/marketing/marketing-footer';

/**
 * Marketing layout — wraps the `/` route in the landing chrome
 * (sticky header + free-flowing main + footer). Owns no chrome of its
 * own beyond the flex container — both chrome siblings are pure
 * presentation components that take no route state.
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
      <MarketingHeader />
      <main className="flex-1">
        <Outlet />
      </main>
      <MarketingFooter />
    </div>
  );
}