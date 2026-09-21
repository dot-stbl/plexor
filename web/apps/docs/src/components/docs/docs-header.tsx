import { Link } from '@tanstack/react-router';
import { PlexorMark } from '@plexor/ui/brand';
import { DocsBreadcrumb } from './docs-breadcrumb';
import { ThemePickerButton } from '@/components/shared/theme-picker-button';

/**
 * Docs header — three discrete slots left-to-right:
 *   1. Brand cluster: PlexorMark + "plexor" wordmark + "· docs" eyebrow.
 *      The brand cluster links back to `/` (the landing) so the header
 *      is a constant escape hatch out of the docs section.
 *   2. Breadcrumb — the parent chain of the current page, drawn from
 *      `useMatches()` by `DocsBreadcrumb`. Hidden at /docs/getting-started
 *      (no parent inside the docs group).
 *   3. Theme picker — tiny uppercase button, same one the marketing
 *      chrome uses.
 *
 * Sticky + blurred so the article scrolls under it. Height matches
 * the marketing header (h-14) so the two chromes line up if a reader
 * alt-tab's between them.
 */
export function DocsHeader() {
  return (
    <header className="sticky top-0 z-sticky border-b border-border bg-background/90 backdrop-blur">
      <div className="mx-auto flex h-14 max-w-7xl items-center gap-6 px-6">
        <Link
          to="/"
          aria-label="plexor — landing"
          className="flex shrink-0 items-center gap-2 text-foreground hover:text-foreground/80"
        >
          <PlexorMark className="h-6 w-6 text-foreground" />
          <span className="text-sm font-semibold tracking-tight">plexor</span>
          <span className="font-mono text-[10px] font-medium uppercase tracking-[0.16em] text-muted-2">
            · docs
          </span>
        </Link>

        <DocsBreadcrumb className="min-w-0 flex-1 truncate text-xs text-muted-2" />

        <nav className="ml-auto flex shrink-0 items-center gap-4">
          <ThemePickerButton className="font-mono text-[11px] font-medium uppercase tracking-[0.14em] text-muted-2 transition-colors duration-fast ease-out hover:text-foreground" />
        </nav>
      </div>
    </header>
  );
}