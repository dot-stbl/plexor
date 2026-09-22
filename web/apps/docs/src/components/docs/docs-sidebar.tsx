import { Link, useRouterState } from '@tanstack/react-router';
import { useEffect, useRef, useState, type ReactNode } from 'react';

/**
 * Docs sidebar — a vertical chapter list (numbered eyebrow + label),
 * one entry per top-level docs section. Six chapters follow the
 * Diátaxis split from the docs content plan:
 *
 *   01 Getting started — tutorials (4 pages).
 *   02 Concepts — mental models (8 pages; eight product surfaces).
 *   03 How-to — task recipes (10 pages).
 *   04 Admin — operations + hardening (7 pages).
 *   05 Reference — catalogs (4 pages).
 *   06 FAQ / Troubleshooting — symptom → fix (2 pages).
 *
 * The eight product surfaces are: compute, networking, storage,
 * identity, marketplace, quotas, audit, console theming. Themes is
 * one of them — it lives under Admin as a single page (not a chapter
 * of its own) and does not appear in Concepts.
 *
 * Each chapter with sub-pages is collapsible; the active chapter (the
 * chapter the active route lives in) auto-expands, even if the user
 * previously collapsed it. The active page in an expanded chapter
 * gets a heavier treatment than the rest.
 *
 * Width: fixed (w-56) on desktop, hidden on mobile. The header's
 * breadcrumb takes the mobile role for navigation context.
 */

export interface DocsPage {
  /** Path slug, full URL. e.g. "/docs/getting-started/install". */
  readonly slug: string;
  /** Sidebar label — short, operator voice. */
  readonly title: string;
  /** Roadmap entry: dimmer treatment, "Soon" badge. */
  readonly soon?: boolean;
}

export interface DocsChapter {
  /** Numeric prefix used as the sidebar eyebrow: "01" through "06". */
  readonly index: number;
  /** Sidebar label: "Getting started", "Concepts", etc. */
  readonly label: string;
  /** Top-level path the chapter lives at: "/docs/getting-started". */
  readonly slug: string;
  /** Pages inside the chapter. Empty array means no expand (top-level only). */
  readonly pages: readonly DocsPage[];
}

const CHAPTERS: readonly DocsChapter[] = [
  {
    index: 1,
    label: 'Getting started',
    slug: '/docs/getting-started',
    pages: [
      { slug: '/docs/getting-started', title: 'Welcome' },
      { slug: '/docs/getting-started/install', title: 'Install Plexor' },
      { slug: '/docs/getting-started/first-login', title: 'First login' },
      { slug: '/docs/getting-started/create-scope', title: 'Org, team, folder' },
      {
        slug: '/docs/getting-started/first-api-key',
        title: 'Your first API key',
      },
    ],
  },
  {
    index: 2,
    label: 'Concepts',
    slug: '/docs/concepts',
    pages: [
      { slug: '/docs/concepts', title: 'Overview' },
      { slug: '/docs/concepts/orgs-teams-folders', title: 'Orgs, teams, folders' },
      { slug: '/docs/concepts/auth-and-rbac', title: 'Authentication & RBAC' },
      { slug: '/docs/concepts/compute-and-workloads', title: 'Workloads & runtimes' },
      { slug: '/docs/concepts/networking', title: 'Networking, FIPs, LBs' },
      { slug: '/docs/concepts/storage', title: 'Storage: volumes & buckets' },
      { slug: '/docs/concepts/quotas', title: 'Quotas' },
      { slug: '/docs/concepts/audit', title: 'Audit log' },
    ],
  },
  {
    index: 3,
    label: 'How-to',
    slug: '/docs/how-to',
    pages: [
      { slug: '/docs/how-to/create-workload', title: 'Create a workload' },
      {
        slug: '/docs/how-to/manage-workload-lifecycle',
        title: 'Workload lifecycle',
      },
      { slug: '/docs/how-to/attach-volume', title: 'Attach a volume' },
      { slug: '/docs/how-to/create-bucket', title: 'Create a bucket' },
      { slug: '/docs/how-to/reserve-floating-ip', title: 'Reserve a floating IP' },
      { slug: '/docs/how-to/add-load-balancer', title: 'Add a load balancer' },
      { slug: '/docs/how-to/add-user-and-role', title: 'Add a user & role' },
      { slug: '/docs/how-to/issue-api-key', title: 'Issue an API key' },
      { slug: '/docs/how-to/configure-oidc', title: 'Configure OIDC' },
      { slug: '/docs/how-to/rotate-ssh-key', title: 'Rotate an SSH key' },
    ],
  },
  {
    index: 4,
    label: 'Admin',
    slug: '/docs/admin',
    pages: [
      { slug: '/docs/admin/audit-log', title: 'Reading the audit log' },
      { slug: '/docs/admin/quotas', title: 'Managing quotas' },
      { slug: '/docs/admin/rbac-hardening', title: 'RBAC hardening' },
      { slug: '/docs/admin/lockout-recovery', title: 'Lockout recovery' },
      { slug: '/docs/admin/backup-and-disaster', title: 'Backup & disaster avoidance' },
      { slug: '/docs/admin/capacity-planning', title: 'Capacity planning' },
      { slug: '/docs/admin/theming-console', title: 'Theming the console' },
    ],
  },
  {
    index: 5,
    label: 'Reference',
    slug: '/docs/reference',
    pages: [
      { slug: '/docs/reference/api', title: 'REST API reference' },
      { slug: '/docs/reference/permissions', title: 'Permissions catalog' },
      { slug: '/docs/reference/quotas-reference', title: 'Quotas reference' },
      { slug: '/docs/reference/glossary', title: 'Glossary' },
    ],
  },
  {
    index: 6,
    label: 'FAQ / Troubleshooting',
    slug: '/docs/faq',
    pages: [
      { slug: '/docs/faq', title: 'Frequently asked' },
      { slug: '/docs/faq/troubleshooting', title: 'Troubleshooting recipes' },
    ],
  },
];

export function DocsSidebar(): ReactNode {
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  });

  const activeChapter = CHAPTERS.find((chapter) =>
    isActiveChapter(pathname, chapter),
  );

  // Auto-expand the active chapter on mount and on every route change.
  // Collapse state for non-active chapters is preserved across renders
  // — when the operator navigates back to a previously-collapsed
  // chapter, the user-set state survives.
  const [openSlugs, setOpenSlugs] = useState<readonly string[]>(
    () => (activeChapter ? [activeChapter.slug] : []),
  );

  // Track the last auto-expanded slug in a ref so the effect runs only
  // when the active chapter actually changes — not on every render.
  const lastAutoSlugRef = useRef<string | null>(null);
  useEffect(() => {
    if (!activeChapter) return;
    if (lastAutoSlugRef.current === activeChapter.slug) return;
    lastAutoSlugRef.current = activeChapter.slug;
    setOpenSlugs((prev) =>
      prev.includes(activeChapter.slug) ? prev : [...prev, activeChapter.slug],
    );
  }, [activeChapter]);

  return (
    <aside className="hidden w-56 shrink-0 md:block">
      <nav aria-label="Documentation chapters" className="sticky top-20">
        <div className="mb-3 font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
          Chapters
        </div>
        <ol className="space-y-0.5">
          {CHAPTERS.map((chapter) => {
            const num = String(chapter.index).padStart(2, '0');
            const isActive = isActiveChapter(pathname, chapter);
            const isOpen = openSlugs.includes(chapter.slug);

            return (
              <li key={chapter.slug}>
                <ChapterLink
                  chapter={chapter}
                  num={num}
                  active={isActive}
                  expanded={isOpen}
                  onToggle={() => {
                    setOpenSlugs((prev) =>
                      prev.includes(chapter.slug)
                        ? prev.filter((slug) => slug !== chapter.slug)
                        : [...prev, chapter.slug],
                    );
                  }}
                />
                {isOpen ? (
                  <ul className="ml-6 mt-0.5 space-y-0.5 border-l border-border/60 pl-3">
                    {chapter.pages.map((page) => {
                      const active = pathname === page.slug;
                      const className = `block rounded-md px-2 py-1 text-[13px] transition-colors duration-fast ease-out ${
                        active
                          ? 'bg-muted font-medium text-foreground'
                          : 'text-muted-2 hover:text-foreground'
                      }`;
                      if (page.soon) {
                        return (
                          <li key={page.slug} className="flex items-center justify-between gap-2">
                            <span className="rounded-md px-2 py-1 text-[13px] text-muted-2/60">
                              {page.title}
                            </span>
                            <span className="font-mono text-[10px] uppercase tracking-[0.12em] text-muted-2/40">
                              soon
                            </span>
                          </li>
                        );
                      }
                      return (
                        <li key={page.slug}>
                          <Link
                            to={page.slug}
                            className={className}
                            aria-current={active ? 'page' : undefined}
                          >
                            {page.title}
                          </Link>
                        </li>
                      );
                    })}
                  </ul>
                ) : null}
              </li>
            );
          })}
        </ol>
      </nav>
    </aside>
  );
}

interface ChapterLinkProps {
  readonly chapter: DocsChapter;
  readonly num: string;
  readonly active: boolean;
  readonly expanded: boolean;
  readonly onToggle: () => void;
}

function ChapterLink({
  chapter,
  num,
  active,
  expanded,
  onToggle,
}: ChapterLinkProps): ReactNode {
  const hasPages = chapter.pages.length > 1;
  const labelClass = active
    ? 'bg-muted font-medium text-foreground'
    : 'text-muted-2 hover:bg-muted/60 hover:text-foreground';

  if (hasPages) {
    return (
      <div className="flex items-center gap-1">
        <Link
          to={chapter.slug}
          className={`flex flex-1 items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors duration-fast ease-out ${labelClass}`}
          aria-current={active ? 'page' : undefined}
        >
          <span className="font-mono text-[10px] tracking-[0.12em] text-muted-2/80">
            {num}
          </span>
          <span>{chapter.label}</span>
        </Link>
        <button
          type="button"
          aria-label={expanded ? 'Collapse chapter' : 'Expand chapter'}
          aria-expanded={expanded}
          onClick={onToggle}
          className="flex h-6 w-6 items-center justify-center rounded-md text-muted-2/70 transition-colors duration-fast ease-out hover:text-foreground"
        >
          <span aria-hidden="true" className="font-mono text-[11px] leading-none">
            {expanded ? '−' : '+'}
          </span>
        </button>
      </div>
    );
  }

  return (
    <Link
      to={chapter.slug}
      className={`flex items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors duration-fast ease-out ${labelClass}`}
      aria-current={active ? 'page' : undefined}
    >
      <span className="font-mono text-[10px] tracking-[0.12em] text-muted-2/80">
        {num}
      </span>
      <span>{chapter.label}</span>
    </Link>
  );
}

/**
 * Active state: the chapter's URL is a prefix of the current pathname
 * (e.g. /docs/concepts/* keeps the Concepts entry highlighted).
 * `/docs/getting-started` doesn't activate Concepts, even though it's
 * structurally under /docs.
 */
function isActiveChapter(pathname: string, chapter: DocsChapter): boolean {
  if (pathname === chapter.slug) return true;
  return pathname.startsWith(`${chapter.slug}/`);
}
