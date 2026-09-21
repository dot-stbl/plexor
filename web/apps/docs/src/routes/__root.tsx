import { Link, Outlet, createRootRoute } from '@tanstack/react-router';
import { PlexorMark } from '@plexor/ui/brand';

/**
 * Root layout — owns no chrome. The two surfaces (landing `/` and
 * docs `/docs/*`) each render their own header/footer/sidebar inside
 * their own pathless route group. The root route only forwards to the
 * child outlet; the active theme preset is already applied by
 * `applyBootPreset()` in `main.tsx` before this component mounts.
 *
 * `notFoundComponent` covers URLs that match no registered route —
 * docs has no i18n / no shared `EmptyState`, so this is a self-contained
 * Plexor-branded surface. Brand mark + heading + a single escape hatch
 * back to `/` (landing). The console has its own Plexor-branded 404 at
 * `apps/console/src/shared/ui/primitives/not-found.tsx`; they diverge
 * on purpose because the docs app has no i18n and a different layout
 * vocabulary than the console.
 */
export const Route = createRootRoute({
  component: () => <Outlet />,
  notFoundComponent: DocsNotFound,
});

function DocsNotFound() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center bg-background px-6 text-foreground">
      <div className="flex max-w-md flex-col items-center gap-6 text-center">
        <PlexorMark className="size-10 text-muted-2" />
        <div className="space-y-2">
          <h1 className="text-2xl font-semibold tracking-tight">Page not found</h1>
          <p className="text-sm text-muted-2">
            The page you were looking for doesn't exist or was moved.
          </p>
        </div>
        <Link
          to="/"
          className="inline-flex h-8 items-center rounded-md border border-border bg-background px-3 text-xs font-medium text-foreground transition-colors duration-fast ease-out hover:bg-muted"
        >
          Back to home
        </Link>
      </div>
    </main>
  );
}