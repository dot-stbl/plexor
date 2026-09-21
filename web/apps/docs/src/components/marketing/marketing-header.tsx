import { Link } from '@tanstack/react-router';
import { PlexorMark } from '@plexor/ui/brand';
import { ThemePickerButton } from '@/components/shared/theme-picker-button';

/**
 * Landing header — PlexorMark + lowercase wordmark on the left, a
 * single "Documentation" link in the centre-right that points at the
 * docs section, and a tiny theme-cycle button on the right.
 *
 * Sticky + blurred so the hero scrolls under it without losing the
 * navigation affordance; v1 ships one outbound link, the docs entry
 * point. Future links (Source, Console, support) live in the footer
 * to keep the header scope to "where am I, where do I go".
 */
export function MarketingHeader() {
  return (
    <header className="sticky top-0 z-sticky border-b border-border bg-background/90 backdrop-blur">
      <div className="mx-auto flex h-14 max-w-7xl items-center gap-6 px-6">
        <Link
          to="/"
          aria-label="plexor — landing"
          className="flex items-center gap-2 font-medium text-foreground hover:text-foreground/80"
        >
          <PlexorMark className="h-6 w-6 text-foreground" />
          <span className="text-sm font-semibold tracking-tight">plexor</span>
        </Link>

        <nav className="ml-auto flex items-center gap-4">
          <Link
            to="/docs/getting-started"
            className="font-mono text-[11px] font-medium uppercase tracking-[0.14em] text-muted-2 transition-colors duration-fast ease-out hover:text-foreground"
          >
            Documentation →
          </Link>
          <ThemePickerButton className="font-mono text-[11px] font-medium uppercase tracking-[0.14em] text-muted-2 transition-colors duration-fast ease-out hover:text-foreground" />
        </nav>
      </div>
    </header>
  );
}
