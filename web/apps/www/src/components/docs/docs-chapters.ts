/**
 * Docs chapter data — six Diátaxis chapters (see `CONTENT-PLAN.md`):
 *
 *   01 Getting started — tutorials (5 pages).
 *   02 Concepts — mental models (8 pages).
 *   03 How-to — task recipes (10 pages).
 *   04 Admin — operations + hardening (7 pages).
 *   05 Reference — catalogs (4 pages).
 *   06 FAQ / Troubleshooting — symptom → fix (2 pages).
 *
 * Kept in its own module (no React) so `flattenPages()` is unit-testable
 * without the DOM. `DocsSidebar` (`./docs-sidebar`) re-exports `CHAPTERS`/
 * `flattenPages` — import from either path.
 */
export interface DocsPage {
  /** Path slug, full URL. e.g. "/docs/getting-started/install". */
  readonly slug: string;
  /** Sidebar label — short, operator voice. */
  readonly title: string;
  /** Roadmap entry: dimmer treatment, "Soon" badge, no live route. */
  readonly soon?: boolean;
}

export interface DocsChapter {
  /** Numeric prefix used as the sidebar eyebrow: "01" through "06". */
  readonly index: number;
  /** Sidebar label: "Getting started", "Concepts", etc. */
  readonly label: string;
  /** Top-level path the chapter lives at: "/docs/getting-started". */
  readonly slug: string;
  /** Pages inside the chapter. Length 1 means no expand (top-level only). */
  readonly pages: readonly DocsPage[];
}

export const CHAPTERS: readonly DocsChapter[] = [
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

/**
 * Flattens `CHAPTERS` into one ordered page list, dropping `soon` pages
 * (no live route to link to). Consumed by `DocsPrevNext`
 * (`./docs-prev-next`, spec §4.6) so the pager and the sidebar can't drift
 * out of sync — one data source, two views.
 */
export function flattenPages(): readonly DocsPage[] {
  return CHAPTERS.flatMap((chapter) => chapter.pages.filter((page) => !page.soon));
}
