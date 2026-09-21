import { createRootRoute, Link, Outlet } from '@tanstack/react-router';
import { PlexorMark } from '@plexor/ui/brand';

/**
 * Root layout — wraps every docs route in a Plexor-branded chrome
 * (mark + product wordmark in the header, single-column content slot
 * for the article, persistent footer). The header is intentionally
 * minimal for v1 — no sidebar yet, since the docs site has only two
 * routes. A sidebar is a follow-up task once the chapter tree is
 * decided.
 */
export const Route = createRootRoute({
  component: RootComponent,
});

function RootComponent() {
  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <header className="sticky top-0 z-sticky border-b border-border bg-background/95 backdrop-blur">
        <div className="mx-auto flex h-14 max-w-5xl items-center gap-3 px-6">
          <Link
            to="/"
            className="flex items-center gap-2 font-medium text-foreground hover:text-foreground/80"
          >
            <PlexorMark className="h-6 w-6 text-foreground" />
            <span className="text-sm font-semibold tracking-tight">plexor</span>
          </Link>
          <span className="text-muted-2 text-xs font-medium uppercase tracking-[0.12em]">
            docs
          </span>
        </div>
      </header>

      <main className="flex-1">
        <div className="mx-auto w-full max-w-3xl px-6 py-12">
          <Outlet />
        </div>
      </main>

      <footer className="border-t border-border">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-6 text-xs text-muted-2">
          <span>plexor docs</span>
          <span className="font-mono">@plexor/docs</span>
        </div>
      </footer>
    </div>
  );
}