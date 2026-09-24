import { useNavigate, Link } from '@tanstack/react-router';
import { PlexorMark } from '@plexor/ui/brand';
import { Menu, Search } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/components/ui/button';
import { StatusPill } from '@/components/ui/status-pill';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { DocsBreadcrumb } from '@/components/docs/docs-breadcrumb';
import { FRAME_CLASS } from './site-frame';
import { GitHubIcon } from './github-icon';
import { ThemeToggle } from './theme-toggle';
import { useCommandMenu } from './command-menu-store';
import { SiteHeaderServicesMenu } from './site-header-services-menu';
import { GITHUB_URL, MARKETING_NAV_ITEMS, VERSION } from './nav-config';

export interface SiteHeaderProps {
  readonly variant: 'marketing' | 'docs';
}

/** `navigator.platform` is deprecated but still universally supported and needs no cast. */
function isMacPlatform(): boolean {
  if (typeof navigator === 'undefined') return false;
  return /Mac|iPhone|iPad|iPod/i.test(navigator.platform || navigator.userAgent);
}

/**
 * One header, two variants (spec §2.2/§2.4). The docs variant is a
 * single row: logo + wordmark + version badge, breadcrumb, then search
 * trigger / GitHub / theme toggle — unchanged.
 *
 * The marketing variant is TWO rows (YC-informed density, 2026-09-24
 * restyle): row 1 is logo/version + search/GitHub/theme/primary CTA
 * (the mobile hamburger lives here too, since row 2 is desktop-only);
 * row 2 (`hidden md:flex`) is the nav proper — the "Product areas"
 * mega-menu (`SiteHeaderServicesMenu`) followed by the flat
 * `MARKETING_NAV_ITEMS` (Docs, Changelog).
 */
export function SiteHeader({ variant }: SiteHeaderProps) {
  const { setOpen } = useCommandMenu();
  const navigate = useNavigate();
  const shortcutHint = isMacPlatform() ? '⌘K' : 'Ctrl K';

  return (
    <header className="sticky top-0 z-sticky border-b border-border bg-background/90 backdrop-blur">
      <div className={`flex h-14 items-center gap-3 ${FRAME_CLASS}`}>
        <Link
          to="/"
          aria-label="plexor — landing"
          className="flex shrink-0 items-center gap-2 text-foreground hover:text-foreground/80"
        >
          <PlexorMark className="h-6 w-6 text-foreground" />
          <span className="text-sm font-semibold tracking-tight">plexor</span>
          <StatusPill variant="idle" hideDot size="sm" className="font-mono">
            {VERSION}
          </StatusPill>
        </Link>

        {variant === 'docs' && <DocsBreadcrumb className="min-w-0 flex-1 truncate text-xs" />}

        <div className={variant === 'marketing' ? 'ml-auto flex items-center gap-2' : 'flex shrink-0 items-center gap-2'}>
          <Button
            variant="outline"
            onClick={() => setOpen(true)}
            aria-label="Search documentation"
            className="hidden w-[220px] justify-between text-muted-2 sm:flex"
          >
            <span className="flex items-center gap-1.5">
              <Search className="size-3.5" />
              Search docs…
            </span>
            <kbd className="rounded border border-border px-1 font-mono text-[10px] text-muted-2">
              {shortcutHint}
            </kbd>
          </Button>
          <Button
            variant="ghost"
            size="icon"
            onClick={() => setOpen(true)}
            aria-label="Search documentation"
            className="sm:hidden"
          >
            <Search />
          </Button>

          {/* aria-label goes on the <a> itself, not the Button wrapper —
              Button's `render` path (composeRender) only merges className
              + children onto the cloned element, not the rest of Button's
              own props (a gap in the ported primitive, same as the
              DropdownMenuTrigger note above). */}
          <Button
            variant="ghost"
            size="icon"
            render={
              <a href={GITHUB_URL} target="_blank" rel="noreferrer" aria-label="Plexor on GitHub">
                <GitHubIcon className="size-4" />
              </a>
            }
          />

          <ThemeToggle />

          {variant === 'marketing' && (
            <>
              <Button variant="default" render={<Link to="/docs/getting-started">Get started</Link>} />
              <DropdownMenu>
                {/* Icon goes as DropdownMenuTrigger's own child, not nested
                    inside `render` — see the note in theme-toggle.tsx. */}
                <DropdownMenuTrigger
                  render={<Button variant="ghost" size="icon" aria-label="Open menu" className="md:hidden" />}
                >
                  <Menu />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onClick={() => void navigate({ to: '/', hash: 'services' })}>
                    Services
                  </DropdownMenuItem>
                  {MARKETING_NAV_ITEMS.map((item) => (
                    <DropdownMenuItem key={item.to} onClick={() => void navigate({ to: item.to })}>
                      {item.label}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuContent>
              </DropdownMenu>
            </>
          )}
        </div>
      </div>

      {variant === 'marketing' && (
        <div className={`hidden h-10 items-center gap-5 border-t border-border/60 md:flex ${FRAME_CLASS}`}>
          <SiteHeaderServicesMenu />
          {MARKETING_NAV_ITEMS.map((item) => (
            <Link
              key={item.to}
              to={item.to}
              className="text-xs text-muted-2 transition-colors duration-fast ease-out hover:text-foreground"
            >
              {item.label}
            </Link>
          ))}
        </div>
      )}
    </header>
  );
}
